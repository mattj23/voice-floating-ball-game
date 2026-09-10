using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VoiceBallGame.Core.Configuration;
using VoiceBallGame.Core.Recording;
using VoiceBallGame.Core.Signals;

namespace VoiceBallGame.Core.Engine;

/// <summary>
/// Drives the engine from two live signal sources at a fixed tick, and saves each completed trial.
/// </summary>
/// <remarks>
/// Everything here runs off the UI thread. Frames are published on <see cref="Frames"/> and
/// subscribers are responsible for marshaling them to the UI. The playing view model handles this
/// responsibility at a single ObserveOn boundary. The original ran capture, signal
/// processing, scoring and file writing all on the dispatcher, which a serial device delivering
/// on a background thread would not have survived.
/// </remarks>
public sealed class GameLoop : IAsyncDisposable
{
    private readonly GameSettings _settings;
    private readonly IFlowSource _flowSource;
    private readonly IVolumeSource _volumeSource;
    private readonly IGameClock _clock;
    private readonly GameEngine _engine;
    private readonly TrialRecorder? _recorder;

    private readonly LatestValue<FlowSample> _latestFlow = new();
    private readonly LatestValue<VolumeSample> _latestVolume = new();

    private readonly Subject<GameFrame> _frames = new();
    private readonly Subject<TrialResult> _completedTrials = new();
    private readonly Subject<string> _errors = new();
    private readonly CompositeDisposable _subscriptions = [];

    private CancellationTokenSource? _cancellation;
    private Task? _ticker;

    public GameLoop(
        GameSettings settings,
        IFlowSource flowSource,
        IVolumeSource volumeSource,
        IGameClock clock,
        GameEngine? engine = null,
        TrialRecorder? recorder = null)
    {
        _settings = settings;
        _flowSource = flowSource;
        _volumeSource = volumeSource;
        _clock = clock;
        _engine = engine ?? new GameEngine(settings);
        _recorder = recorder;
    }

    public GameEngine Engine => _engine;

    /// <summary>One item per engine tick, published on a background thread.</summary>
    public IObservable<GameFrame> Frames => _frames.AsObservable();

    /// <summary>Fires once per completed trial, after it has been saved.</summary>
    public IObservable<TrialResult> CompletedTrials => _completedTrials.AsObservable();

    /// <summary>
    /// Problems that should be shown to the operator but are not worth stopping the session for,
    /// such as a trial that could not be written to disk.
    /// </summary>
    public IObservable<string> Errors => _errors.AsObservable();

    /// <summary>Device status for both inputs, tagged so the view can tell them apart.</summary>
    public IObservable<(string Input, SourceStatus Status)> SourceStatus =>
        _flowSource.Status.Select(s => ("flow", s))
            .Merge(_volumeSource.Status.Select(s => ("volume", s)));

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_ticker is not null) return;

        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _subscriptions.Add(_flowSource.Samples.Subscribe(sample =>
            _latestFlow.Set(sample, sample.Timestamp)));

        _subscriptions.Add(_volumeSource.Samples.Subscribe(sample =>
            _latestVolume.Set(sample, sample.Timestamp)));

        await _flowSource.StartAsync(_cancellation.Token).ConfigureAwait(false);
        await _volumeSource.StartAsync(_cancellation.Token).ConfigureAwait(false);

        _ticker = Task.Run(() => TickAsync(_cancellation.Token), CancellationToken.None);
    }

    private async Task TickAsync(CancellationToken token)
    {
        var interval = TimeSpan.FromMilliseconds(_settings.EngineTickMs);
        var maxAge = interval * _settings.StaleTicks;
        var next = _clock.Elapsed;

        while (!token.IsCancellationRequested)
        {
            next += interval;
            var wait = next - _clock.Elapsed;

            try
            {
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // If the loop has fallen far behind, give up on catching up rather than running a
            // burst of ticks that would all carry nearly the same data.
            var now = _clock.Elapsed;
            if (now - next > maxAge) next = now;

            try
            {
                Advance(now, maxAge);
            }
            catch (Exception e)
            {
                _errors.OnNext($"The game loop hit an error: {e.Message}");
            }
        }
    }

    private void Advance(TimeSpan now, TimeSpan maxAge)
    {
        var (flow, flowStale) = _latestFlow.Read(now, maxAge);
        var (volume, volumeStale) = _latestVolume.Read(now, maxAge);

        var frame = _engine.Step(new GameInput(
            now,
            flow.FlowLps,
            volume.Spl,
            flowStale,
            volumeStale));

        _frames.OnNext(frame);

        if (frame.CompletedTrial is { } trial && frame.CompletedSamples is { } samples)
        {
            if (_recorder is not null && samples.Count > 0)
            {
                try
                {
                    _recorder.Save(trial, samples);
                }
                catch (Exception e)
                {
                    // A trial that cannot be written must not take the session down; the operator
                    // is told and the game keeps running.
                    _errors.OnNext($"Trial {trial.TrialNumber} could not be saved: {e.Message}");
                }
            }

            _completedTrials.OnNext(trial);
        }
    }

    public async Task StopAsync()
    {
        if (_cancellation is not null)
        {
            await _cancellation.CancelAsync().ConfigureAwait(false);
        }

        if (_ticker is not null)
        {
            try
            {
                await _ticker.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping.
            }

            _ticker = null;
        }

        await _flowSource.StopAsync().ConfigureAwait(false);
        await _volumeSource.StopAsync().ConfigureAwait(false);

        _subscriptions.Clear();
        _cancellation?.Dispose();
        _cancellation = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);

        _subscriptions.Dispose();
        _frames.Dispose();
        _completedTrials.Dispose();
        _errors.Dispose();

        await _flowSource.DisposeAsync().ConfigureAwait(false);
        await _volumeSource.DisposeAsync().ConfigureAwait(false);
    }
}
