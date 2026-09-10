using System;
using System.Collections.ObjectModel;
using RxVoid = ReactiveUI.Primitives.RxVoid;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using VoiceBallGame.App.Services;
using VoiceBallGame.Core.Engine;
using VoiceBallGame.Core.Recording;
using VoiceBallGame.Core.Signals;

namespace VoiceBallGame.App.ViewModels;

/// <summary>
/// The playing screen. It owns the game loop and is the one place where engine frames cross from
/// the background onto the UI thread.
/// </summary>
public class PlayingViewModel : ViewModelBase, IDisposable
{
    private readonly MainViewModel _shell;
    private readonly GameLoop _loop;
    private readonly StopwatchClock _clock = new();
    private readonly CompositeDisposableBag _subscriptions = new();

    private GameFrame? _frame;
    private string? _trialMessage;
    private string? _error;
    private bool _signalLost;

    public PlayingViewModel(
        MainViewModel shell,
        FlowSourceOption flowOption,
        VolumeSourceOption volumeOption,
        string subjectId,
        string sessionId)
    {
        _shell = shell;

        var settings = shell.Settings;
        var engine = new GameEngine(settings)
        {
            SubjectId = string.IsNullOrWhiteSpace(subjectId) ? null : subjectId.Trim(),
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId.Trim(),
        };

        FlowName = flowOption.Name;
        VolumeName = volumeOption.Name;
        BallSize = settings.BallSize;

        _loop = new GameLoop(
            settings,
            flowOption.Create(_clock, settings),
            volumeOption.Create(_clock, settings),
            _clock,
            engine,
            new TrialRecorder(settings));

        // The game loop runs on a background thread. These three subscriptions are the only
        // boundary: no bound property is touched except through the UI dispatcher.
        _subscriptions.Add(_loop.Frames.SubscribeOnUiThread(OnFrame));
        _subscriptions.Add(_loop.CompletedTrials.SubscribeOnUiThread(OnTrialCompleted));
        _subscriptions.Add(_loop.Errors.SubscribeOnUiThread(message => Error = message));

        StopCommand = ReactiveCommand.CreateFromTask(StopAsync);

        _ = _loop.StartAsync();
    }

    public string FlowName { get; }

    public string VolumeName { get; }

    public double BallSize { get; }

    public ObservableCollection<TrialResult> Trials { get; } = [];

    /// <summary>The latest engine frame, handed straight to the drawing control.</summary>
    public GameFrame? Frame
    {
        get => _frame;
        private set
        {
            this.RaiseAndSetIfChanged(ref _frame, value);
            this.RaisePropertyChanged(nameof(Volume));
            this.RaisePropertyChanged(nameof(Flow));
            this.RaisePropertyChanged(nameof(RatioFraction));
            this.RaisePropertyChanged(nameof(IsInTrial));
            this.RaisePropertyChanged(nameof(FlowOutOfLimits));
            this.RaisePropertyChanged(nameof(TrialElapsed));
        }
    }

    public double Volume => _frame?.Volume ?? 0;

    public double Flow => _frame?.Flow ?? 0;

    public double RatioFraction => _frame?.Score.RatioFraction ?? 0;

    public bool IsInTrial => _frame?.IsInTrial ?? false;

    public bool FlowOutOfLimits => _frame?.FlowOutOfLimits ?? false;

    public double TrialElapsed => _frame?.TrialElapsed ?? 0;

    /// <summary>The feedback shown after a trial, cleared when the next one begins.</summary>
    public string? TrialMessage
    {
        get => _trialMessage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _trialMessage, value);
            this.RaisePropertyChanged(nameof(HasTrialMessage));
        }
    }

    public bool HasTrialMessage => TrialMessage is not null;

    public bool SignalLost
    {
        get => _signalLost;
        private set => this.RaiseAndSetIfChanged(ref _signalLost, value);
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

    public ReactiveCommand<RxVoid, RxVoid> StopCommand { get; }

    private void OnFrame(GameFrame frame)
    {
        if (frame.TrialStarted) TrialMessage = null;

        SignalLost = frame.SignalLost;
        Frame = frame;
    }

    private void OnTrialCompleted(TrialResult trial)
    {
        Trials.Insert(0, trial);
        TrialMessage = Describe(trial);
    }

    /// <summary>
    /// Builds the end-of-trial feedback.
    /// </summary>
    /// <remarks>
    /// The original reported only the length and an average error labeled "pixels", although the
    /// error was a ratio. It also computed and discarded the time in the goal and the number of
    /// goal entries. This method reports the length, the share of the trial on target, and the
    /// average distance from the target ratio as a percentage. That percentage has the same
    /// meaning whether flow stayed within its limits or exceeded them.
    /// </remarks>
    private static string Describe(TrialResult trial)
    {
        if (trial.SampleCount == 0)
            return "That trial was too short to score. Try voicing for a little longer.";

        return $"You voiced for {trial.DurationSeconds:F1} seconds and were on target for " +
               $"{trial.FractionInGoal:P0} of it, entering the target {trial.GoalEntryCount} " +
               $"{(trial.GoalEntryCount == 1 ? "time" : "times")}. " +
               $"Your average distance from the target was {trial.AverageRatioError:P0}.";
    }

    private async Task StopAsync()
    {
        await _loop.StopAsync();
        _shell.ShowSetup();
        Dispose();
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
        _ = _loop.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Holds a disposable resource without adding Rx's disposables API to the view model.</summary>
internal sealed class CompositeDisposableBag : IDisposable
{
    private readonly System.Collections.Generic.List<IDisposable> _items = [];

    public void Add(IDisposable item) => _items.Add(item);

    public void Dispose()
    {
        foreach (var item in _items) item.Dispose();
        _items.Clear();
    }
}
