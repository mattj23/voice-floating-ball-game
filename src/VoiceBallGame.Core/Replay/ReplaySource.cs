using System.Reactive.Linq;
using System.Reactive.Subjects;
using VoiceBallGame.Core.Signals;

namespace VoiceBallGame.Core.Replay;

/// <summary>
/// Plays a recorded signal back in real time, standing in for a device. Playback loops, so a
/// short recording can drive the game indefinitely during development.
/// </summary>
public abstract class ReplaySource<T> : ISignalSource<T>
{
    private readonly IReadOnlyList<RecordedPoint> _points;
    private readonly Subject<T> _samples = new();
    private readonly BehaviorSubject<SourceStatus> _status = new(SourceStatus.Stopped);
    private readonly IGameClock _clock;
    private readonly bool _loop;

    private CancellationTokenSource? _cancellation;
    private Task? _pump;

    protected ReplaySource(string description, IReadOnlyList<RecordedPoint> points, IGameClock clock, bool loop = true)
    {
        Description = description;
        _points = points;
        _clock = clock;
        _loop = loop;
    }

    public string Description { get; }

    public IObservable<T> Samples => _samples.AsObservable();

    public IObservable<SourceStatus> Status => _status.AsObservable();

    /// <summary>Builds the sample this source emits from a recorded value.</summary>
    protected abstract T CreateSample(TimeSpan timestamp, double value);

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_pump is not null) return Task.CompletedTask;

        if (_points.Count == 0)
        {
            _status.OnNext(SourceStatus.Faulted($"The recording '{Description}' has no samples."));
            return Task.CompletedTask;
        }

        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _status.OnNext(SourceStatus.Connected);
        _pump = Task.Run(() => PumpAsync(_cancellation.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task PumpAsync(CancellationToken token)
    {
        // Playback is paced against the shared game clock rather than a sleep-per-sample loop, so
        // a slow frame does not make the recording drift steadily later.
        var start = _clock.Elapsed;
        double loopLengthMs = _points[^1].TimeMs - _points[0].TimeMs;

        try
        {
            int pass = 0;
            while (!token.IsCancellationRequested)
            {
                double offsetMs = pass * (loopLengthMs + AverageIntervalMs());

                foreach (var point in _points)
                {
                    var due = start + TimeSpan.FromMilliseconds(offsetMs + point.TimeMs - _points[0].TimeMs);
                    var wait = due - _clock.Elapsed;

                    if (wait > TimeSpan.Zero)
                        await Task.Delay(wait, token).ConfigureAwait(false);

                    _samples.OnNext(CreateSample(_clock.Elapsed, point.Value));
                }

                if (!_loop) break;
                pass++;
            }
        }
        catch (OperationCanceledException)
        {
            // Stopping is not a fault.
        }
        catch (Exception e)
        {
            _status.OnNext(SourceStatus.Faulted($"Replay of '{Description}' failed: {e.Message}"));
            return;
        }

        _status.OnNext(SourceStatus.Stopped);
    }

    private double AverageIntervalMs() =>
        _points.Count > 1 ? (_points[^1].TimeMs - _points[0].TimeMs) / (_points.Count - 1) : 50.0;

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
        _status.OnNext(SourceStatus.Stopped);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _samples.Dispose();
        _status.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed class ReplayFlowSource : ReplaySource<FlowSample>, IFlowSource
{
    public ReplayFlowSource(string description, IReadOnlyList<RecordedPoint> points, IGameClock clock, bool loop = true)
        : base(description, points, clock, loop) { }

    public static ReplayFlowSource FromFile(string path, IGameClock clock, bool loop = true)
    {
        var (header, points) = RecordedSignal.Load(path);

        if (header.Kind != SignalKind.Flow)
            throw new ReplayFormatException($"'{path}' is a {header.Kind} recording and cannot drive the flow input.");

        return new ReplayFlowSource(Path.GetFileName(path), points, clock, loop);
    }

    protected override FlowSample CreateSample(TimeSpan timestamp, double value) => new(timestamp, value);
}

public sealed class ReplayVolumeSource : ReplaySource<VolumeSample>, IVolumeSource
{
    public ReplayVolumeSource(string description, IReadOnlyList<RecordedPoint> points, IGameClock clock, bool loop = true)
        : base(description, points, clock, loop) { }

    public static ReplayVolumeSource FromFile(string path, IGameClock clock, bool loop = true)
    {
        var (header, points) = RecordedSignal.Load(path);

        if (header.Kind != SignalKind.Volume)
            throw new ReplayFormatException($"'{path}' is a {header.Kind} recording and cannot drive the microphone input.");

        return new ReplayVolumeSource(Path.GetFileName(path), points, clock, loop);
    }

    protected override VolumeSample CreateSample(TimeSpan timestamp, double value) => new(timestamp, value);

    /// <summary>
    /// A replayed recording is already calibrated, so there is no meaningful raw level to offer
    /// the calibration screen.
    /// </summary>
    public IObservable<double> RawLevels => Observable.Empty<double>();
}
