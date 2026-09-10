using System;
using System.Collections.Generic;
using RxVoid = ReactiveUI.Primitives.RxVoid;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;
using VoiceBallGame.App.Services;

namespace VoiceBallGame.App.ViewModels;

/// <summary>
/// The initial screen where the operator selects both inputs, identifies the participant, and
/// starts the game.
/// </summary>
public class SetupViewModel : ViewModelBase
{
    private readonly MainViewModel _shell;
    private readonly Subject<PlayingViewModel> _started = new();

    private FlowSourceOption? _flow;
    private VolumeSourceOption? _volume;
    private string _subjectId = string.Empty;
    private string _sessionId = string.Empty;
    private string? _error;

    public SetupViewModel(MainViewModel shell)
    {
        _shell = shell;

        FlowOptions = shell.Catalog.FlowOptions();
        VolumeOptions = shell.Catalog.VolumeOptions();

        // Preselect the first of each so the operator can start immediately in the common case
        // where exactly one flow meter and one microphone are attached.
        _flow = FlowOptions.Count > 0 ? FlowOptions[0] : null;
        _volume = VolumeOptions.Count > 0 ? VolumeOptions[0] : null;

        // A microphone with no calibration reports a signal level rather than a loudness, so the
        // game cannot score against the target ratio until it has been calibrated once.
        var canStart = this.WhenAnyValue(
            x => x.Flow,
            x => x.Volume,
            (flow, volume) => flow is not null && volume is { RequiresCalibration: false });

        var canCalibrate = this.WhenAnyValue(x => x.Volume)
            .Select(volume => volume is MicrophoneOption);

        StartCommand = ReactiveCommand.Create(Start, canStart);
        RefreshCommand = ReactiveCommand.Create(Refresh);
        CalibrateCommand = ReactiveCommand.Create(Calibrate, canCalibrate);

        this.WhenAnyValue(x => x.Volume).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(NeedsCalibration));
            this.RaisePropertyChanged(nameof(CalibrationMessage));
        });
    }

    public IReadOnlyList<FlowSourceOption> FlowOptions { get; private set; }

    public IReadOnlyList<VolumeSourceOption> VolumeOptions { get; private set; }

    public FlowSourceOption? Flow
    {
        get => _flow;
        set => this.RaiseAndSetIfChanged(ref _flow, value);
    }

    public VolumeSourceOption? Volume
    {
        get => _volume;
        set => this.RaiseAndSetIfChanged(ref _volume, value);
    }

    public string SubjectId
    {
        get => _subjectId;
        set => this.RaiseAndSetIfChanged(ref _subjectId, value);
    }

    public string SessionId
    {
        get => _sessionId;
        set => this.RaiseAndSetIfChanged(ref _sessionId, value);
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

    /// <summary>The directory where the game will write trials from this session.</summary>
    public string OutputDirectory => 
        new Core.Recording.TrialRecorder(_shell.Settings, _shell.Paths.DataRoot).OutputDirectory;

    public string SettingsSummary => _shell.SettingsFile is { } file
        ? $"Settings loaded from {file}"
        : "No settings file found; using built-in defaults";

    public string TargetSummary =>
        $"Target {_shell.Settings.GoalRatio:F0} dB·s/L, " +
        $"flow {_shell.Settings.LowerFlowLimit:F3} to {_shell.Settings.UpperFlowLimit:F3} L/s";

    public ReactiveCommand<RxVoid, RxVoid> StartCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RefreshCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> CalibrateCommand { get; }

    public bool NeedsCalibration => Volume is { RequiresCalibration: true };

    public string CalibrationMessage => Volume is null
        ? string.Empty
        : $"{Volume.Name} has not been calibrated, so the game cannot tell how loud the " +
          "participant is. Calibrate it against a sound level meter before starting.";

    private void Calibrate()
    {
        if (Volume is not MicrophoneOption microphone) return;
        _calibrationRequested.OnNext(microphone);
    }

    private readonly Subject<MicrophoneOption> _calibrationRequested = new();

    /// <summary>Fires when the operator asks to calibrate the selected microphone.</summary>
    public IObservable<MicrophoneOption> CalibrationRequested => _calibrationRequested.AsObservable();

    /// <summary>Fires with the playing screen once a session has been started.</summary>
    public IObservable<PlayingViewModel> Started => _started.AsObservable();

    private void Refresh()
    {
        FlowOptions = _shell.Catalog.FlowOptions();
        VolumeOptions = _shell.Catalog.VolumeOptions();

        this.RaisePropertyChanged(nameof(FlowOptions));
        this.RaisePropertyChanged(nameof(VolumeOptions));

        if (Flow is null && FlowOptions.Count > 0) Flow = FlowOptions[0];
        if (Volume is null && VolumeOptions.Count > 0) Volume = VolumeOptions[0];
    }

    private void Start()
    {
        if (Flow is null || Volume is null) return;

        Error = null;

        try
        {
            var session = new PlayingViewModel(_shell, Flow, Volume, SubjectId, SessionId);
            _started.OnNext(session);
        }
        catch (Exception e)
        {
            // Opening a serial port or capture device is the most likely failure at this point.
            // Identify the affected input for the operator instead of displaying a stack trace.
            Error = $"The session could not be started: {e.Message}";
        }
    }
}
