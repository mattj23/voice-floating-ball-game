using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using ReactiveUI;
using VoiceBallGame.App.Services;
using VoiceBallGame.Core.Calibration;
using VoiceBallGame.Core.Signals;
using VoiceBallGame.Hardware;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace VoiceBallGame.App.ViewModels;

/// <summary>
/// Calibrates a microphone against a sound level meter.
/// </summary>
/// <remarks>
/// A microphone reports a signal level with no absolute meaning, so the game cannot know what
/// loudness a participant is producing until one known level has been measured. The operator
/// produces a steady sound, reads the level from a sound level meter, and types it in.
///
/// The original asked for three levels and then used only the first. This screen asks for the one
/// required level. It finds the held level by locating a cluster in the recorded values, which
/// excludes the quiet periods before and after the sound.
/// </remarks>
public class CalibrationViewModel : ViewModelBase, IDisposable
{
    private readonly MainViewModel _shell;
    private readonly MicrophoneOption _microphone;
    private readonly StopwatchClock _clock = new();
    private readonly Subject<RxVoid> _finished = new();
    private readonly List<double> _recorded = [];

    private PortAudioVolumeSource? _source;
    private IDisposable? _levels;

    private bool _isRecording;
    private double _liveLevel;
    private double _elapsedSeconds;
    private double? _measuredLevel;
    private string _actualDb = string.Empty;
    private string? _error;

    /// <summary>
    /// The shortest recording the level finder can use. A shorter recording does not contain
    /// enough data for the histogram to produce a reliable cluster.
    /// </summary>
    public const double MinimumSeconds = 3.0;

    public CalibrationViewModel(MainViewModel shell, MicrophoneOption microphone)
    {
        _shell = shell;
        _microphone = microphone;

        var canSave = this.WhenAnyValue(
            x => x.MeasuredLevel,
            x => x.ActualDb,
            (level, db) => level is > 0 && double.TryParse(db, out var value) && double.IsFinite(value));

        RecordCommand = ReactiveCommand.CreateFromTask(ToggleRecordingAsync);
        SaveCommand = ReactiveCommand.Create(Save, canSave);
        CancelCommand = ReactiveCommand.CreateFromTask(CancelAsync);
    }

    public string DeviceName => _microphone.Name;

    public IObservable<RxVoid> Finished => _finished.AsObservable();

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isRecording, value);
            this.RaisePropertyChanged(nameof(RecordButtonText));
        }
    }

    public string RecordButtonText => IsRecording ? "Stop recording" : "Start recording";

    /// <summary>The level arriving right now, so the operator can see the microphone is live.</summary>
    public double LiveLevel
    {
        get => _liveLevel;
        private set => this.RaiseAndSetIfChanged(ref _liveLevel, value);
    }

    public double ElapsedSeconds
    {
        get => _elapsedSeconds;
        private set => this.RaiseAndSetIfChanged(ref _elapsedSeconds, value);
    }

    /// <summary>The steady level found in the recording.</summary>
    public double? MeasuredLevel
    {
        get => _measuredLevel;
        private set
        {
            this.RaiseAndSetIfChanged(ref _measuredLevel, value);
            this.RaisePropertyChanged(nameof(HasMeasurement));
        }
    }

    public bool HasMeasurement => MeasuredLevel is > 0;

    /// <summary>What the sound level meter read while the sound was held.</summary>
    public string ActualDb
    {
        get => _actualDb;
        set => this.RaiseAndSetIfChanged(ref _actualDb, value);
    }

    public string? Error
    {
        get => _error;
        private set
        {
            this.RaiseAndSetIfChanged(ref _error, value);
            this.RaisePropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => Error is not null;

    public ReactiveCommand<RxVoid, RxVoid> RecordCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> SaveCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> CancelCommand { get; }

    private async Task ToggleRecordingAsync()
    {
        if (IsRecording)
        {
            await StopRecordingAsync();
            return;
        }

        Error = null;
        MeasuredLevel = null;
        _recorded.Clear();
        ElapsedSeconds = 0;

        _source = _microphone.CreateForCalibration(_clock, _shell.Settings);

        var start = _clock.Elapsed;
        _levels = _source.RawLevels.SubscribeOnUiThread(level =>
        {
            LiveLevel = level;
            ElapsedSeconds = (_clock.Elapsed - start).TotalSeconds;
            _recorded.Add(level);
        });

        var faults = _source.Status.SubscribeOnUiThread(status =>
        {
            if (status.State == SourceState.Faulted) Error = status.Message;
        });

        IsRecording = true;
        await _source.StartAsync();

        // Keep both subscriptions alive for as long as the source is running.
        _levels = new CompositeSubscription(_levels, faults);
    }

    private async Task StopRecordingAsync()
    {
        IsRecording = false;

        if (_source is not null)
        {
            await _source.StopAsync();
            await _source.DisposeAsync();
            _source = null;
        }

        _levels?.Dispose();
        _levels = null;

        if (ElapsedSeconds < MinimumSeconds)
        {
            Error = $"That recording was {ElapsedSeconds:F1} seconds long. " +
                    $"Hold the sound steady for at least {MinimumSeconds:F0} seconds and record again.";
            return;
        }

        var level = PeakEstimator.FindLevel(_recorded);

        if (level is not > 0)
        {
            Error = "No steady level was found in that recording. Check that the microphone is " +
                    "picking up the sound, then record again.";
            return;
        }

        MeasuredLevel = level;
    }

    private void Save()
    {
        if (MeasuredLevel is not { } level || !double.TryParse(ActualDb, out var db)) return;

        try
        {
            _microphone.SaveCalibration(
                MicCalibration.FromReference(_microphone.DeviceKey, _microphone.Name, level, db));

            _finished.OnNext(default);
        }
        catch (Exception e)
        {
            Error = $"The calibration could not be saved: {e.Message}";
        }
    }

    private async Task CancelAsync()
    {
        if (IsRecording) await StopRecordingAsync();
        _finished.OnNext(default);
    }

    public void Dispose()
    {
        _levels?.Dispose();
        _ = _source?.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private sealed class CompositeSubscription(params IDisposable[] items) : IDisposable
    {
        public void Dispose()
        {
            foreach (var item in items) item.Dispose();
        }
    }
}
