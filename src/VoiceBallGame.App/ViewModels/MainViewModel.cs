using System;
using System.IO;
using RxVoid = ReactiveUI.Primitives.RxVoid;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using VoiceBallGame.App.Services;
using VoiceBallGame.Core.Calibration;
using VoiceBallGame.Core.Configuration;

namespace VoiceBallGame.App.ViewModels;

/// <summary>
/// The application shell. It owns the settings, selects the current screen, and reports startup
/// failures to the operator.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private ViewModelBase? _current;
    private string? _fatalError;

    public MainViewModel()
    {
        // The parameterless constructor exists for the XAML previewer, which cannot supply
        // settings. It shows the setup screen with the built-in defaults.
        try
        {
            var path = SettingsPath();
            Settings = path is null ? new GameSettings() : SettingsLoader.Load(path);
            SettingsDirectory = path is null
                ? AppContext.BaseDirectory
                : Path.GetDirectoryName(Path.GetFullPath(path))!;
            SettingsFile = path;
        }
        catch (SettingsException e)
        {
            Settings = new GameSettings();
            SettingsDirectory = AppContext.BaseDirectory;
            FatalError = e.Message;
        }

        Calibrations = new CalibrationStore(CalibrationStore.DefaultPath);
        Catalog = new SourceCatalog(
            Settings,
            SettingsDirectory,
            HardwareSources.Flow,
            () => HardwareSources.Volume(Calibrations));

        ShowSetup();
    }

    public GameSettings Settings { get; }

    public CalibrationStore Calibrations { get; }

    public string SettingsDirectory { get; }

    public string? SettingsFile { get; }

    public SourceCatalog Catalog { get; }

    /// <summary>The screen currently showing.</summary>
    public ViewModelBase? Current
    {
        get => _current;
        private set => this.RaiseAndSetIfChanged(ref _current, value);
    }

    /// <summary>
    /// Set when the settings file could not be read. The window shows this instead of the game,
    /// because a bad settings file means the target and limits are not what the experimenter
    /// intended. Continuing with those values could silently collect invalid data.
    /// </summary>
    public string? FatalError
    {
        get => _fatalError;
        private set
        {
            this.RaiseAndSetIfChanged(ref _fatalError, value);
            this.RaisePropertyChanged(nameof(HasFatalError));
        }
    }

    public bool HasFatalError => FatalError is not null;

    public void ShowSetup()
    {
        var setup = new SetupViewModel(this);
        setup.Started.Subscribe(session => Current = session);
        setup.CalibrationRequested.Subscribe(ShowCalibration);
        Current = setup;
    }

    private void ShowCalibration(MicrophoneOption microphone)
    {
        var calibration = new CalibrationViewModel(this, microphone);

        // Returning through ShowSetup rebuilds the device list, so a calibration just saved shows
        // against the microphone immediately.
        calibration.Finished.Subscribe(_ =>
        {
            calibration.Dispose();
            ShowSetup();
        });

        Current = calibration;
    }

    /// <summary>
    /// Starts a session against the simulated participant immediately, skipping the setup
    /// screen. Used by the --demo switch to show the game running on a machine with no hardware.
    /// </summary>
    public void StartSimulatedSession()
    {
        Current = new PlayingViewModel(
            this,
            new SimulatedFlowOption(),
            new SimulatedVolumeOption(),
            subjectId: "demo",
            sessionId: "simulated");
    }

    /// <summary>Locates app_settings.toml beside the executable, or in a config folder above it.</summary>
    private static string? SettingsPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(directory.FullName, "app_settings.toml"),
                         Path.Combine(directory.FullName, "config", "app_settings.toml"),
                     })
            {
                if (File.Exists(candidate)) return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
