using System.Reactive.Linq;
using System.Reactive.Subjects;
using VoiceBallGame.Core.Signals;

namespace VoiceBallGame.Core.Replay;

/// <summary>
/// Generates a signal from a function of elapsed time. Unlike a replay this needs no recording,
/// so the game can be started and demonstrated on a machine that has never seen the hardware.
/// </summary>
public abstract class SyntheticSource<T> : ISignalSource<T>
{
    private readonly Subject<T> _samples = new();
    private readonly BehaviorSubject<SourceStatus> _status = new(SourceStatus.Stopped);
    private readonly IGameClock _clock;
    private readonly TimeSpan _interval;
    private readonly Func<TimeSpan, double> _generator;

    private CancellationTokenSource? _cancellation;
    private Task? _pump;

    protected SyntheticSource(string description, Func<TimeSpan, double> generator, IGameClock clock, TimeSpan interval)
    {
        Description = description;
        _generator = generator;
        _clock = clock;
        _interval = interval;
    }

    public string Description { get; }

    public IObservable<T> Samples => _samples.AsObservable();

    public IObservable<SourceStatus> Status => _status.AsObservable();

    protected abstract T CreateSample(TimeSpan timestamp, double value);

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_pump is not null) return Task.CompletedTask;

        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _status.OnNext(SourceStatus.Connected);
        _pump = Task.Run(() => PumpAsync(_cancellation.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task PumpAsync(CancellationToken token)
    {
        var next = _clock.Elapsed;

        try
        {
            while (!token.IsCancellationRequested)
            {
                next += _interval;
                var wait = next - _clock.Elapsed;

                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, token).ConfigureAwait(false);

                var now = _clock.Elapsed;
                _samples.OnNext(CreateSample(now, _generator(now)));
            }
        }
        catch (OperationCanceledException)
        {
            // Stopping is not a fault.
        }

        _status.OnNext(SourceStatus.Stopped);
    }

    public async Task StopAsync()
    {
        if (_cancellation is null) return;

        await _cancellation.CancelAsync().ConfigureAwait(false);

        if (_pump is not null)
        {
            try
            {
                await _pump.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping.
            }
        }

        _cancellation.Dispose();
        _cancellation = null;
        _pump = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _samples.Dispose();
        _status.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed class SyntheticFlowSource : SyntheticSource<FlowSample>, IFlowSource
{
    public SyntheticFlowSource(string description, Func<TimeSpan, double> generator, IGameClock clock, TimeSpan interval)
        : base(description, generator, clock, interval) { }

    protected override FlowSample CreateSample(TimeSpan timestamp, double value) => new(timestamp, value);
}

public sealed class SyntheticVolumeSource : SyntheticSource<VolumeSample>, IVolumeSource
{
    public SyntheticVolumeSource(string description, Func<TimeSpan, double> generator, IGameClock clock, TimeSpan interval)
        : base(description, generator, clock, interval) { }

    protected override VolumeSample CreateSample(TimeSpan timestamp, double value) => new(timestamp, value);

    public IObservable<double> RawLevels => Observable.Empty<double>();
}

/// <summary>
/// A simulated participant who voices in bouts and drifts around the target ratio. The resulting
/// signal moves the ball, changes its color, and triggers trials as a real session would.
/// </summary>
public static class SimulatedParticipant
{
    public const string Name = "Simulated participant";

    /// <param name="bout">How long each period of voicing lasts.</param>
    /// <param name="rest">How long the silence between bouts lasts.</param>
    public static (IFlowSource Flow, IVolumeSource Volume) Create(
        IGameClock clock,
        TimeSpan interval,
        double goalRatio = 800,
        TimeSpan? bout = null,
        TimeSpan? rest = null)
    {
        var boutLength = bout ?? TimeSpan.FromSeconds(6);
        var restLength = rest ?? TimeSpan.FromSeconds(3);
        double cycle = (boutLength + restLength).TotalSeconds;

        double FlowAt(TimeSpan t)
        {
            double phase = t.TotalSeconds % cycle;
            if (phase > boutLength.TotalSeconds) return 0.0;

            // Ramp in and out so trials open and close cleanly instead of snapping on. The
            // variation stays inside the default flow limits of 0.08 to 0.1 L/s, so the bout
            // looks like a participant holding a steady breath rather than one fighting the
            // limits the whole time.
            double ramp = Math.Min(1.0, Math.Min(phase, boutLength.TotalSeconds - phase) / 0.4);
            return ramp * (0.09 + 0.007 * Math.Sin(t.TotalSeconds * 0.9));
        }

        double VolumeAt(TimeSpan t)
        {
            double flow = FlowAt(t);
            if (flow <= 0.0) return 32.0;

            // Wander slowly around the goal ratio so the ball's color sweeps through its bands,
            // spending a good share of each bout inside the scoring window.
            double ratioFraction = 1.0 + 0.06 * Math.Sin(t.TotalSeconds * 0.35);
            return goalRatio * flow * ratioFraction;
        }

        return (
            new SyntheticFlowSource(Name, FlowAt, clock, interval),
            new SyntheticVolumeSource(Name, VolumeAt, clock, interval));
    }
}
