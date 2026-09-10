using VoiceBallGame.Core.Configuration;
using VoiceBallGame.Core.Dsp;
using VoiceBallGame.Core.Recording;
using VoiceBallGame.Core.Signals;

namespace VoiceBallGame.Core.Engine;

/// <summary>
/// The game itself: smoothing, trial detection, ball geometry, color and scoring. It owns no
/// timers, threads or files, and every frame is a pure function of the inputs it has been given,
/// which is what lets a recorded session be replayed through it and compared frame for frame.
/// </summary>
public sealed class GameEngine
{
    /// <summary>
    /// Flow is floored at this value before any division, so a silent or disconnected sensor
    /// cannot produce an infinite ratio.
    /// </summary>
    public const double MinimumFlowLps = 0.001;

    private readonly GameSettings _settings;
    private readonly MovingAverage _flowAverage;
    private readonly MovingAverage _volumeAverage;
    private readonly TrialDetector _trialDetector;
    private readonly BallPhysics _physics;
    private readonly Scorer _scorer;
    private readonly ColorMap _colorMap;

    private readonly TimeProvider _timeProvider;
    private readonly List<TrialSample> _samples = [];

    private TimeSpan? _lastTimestamp;
    private TimeSpan _trialStartTimestamp;
    private DateTimeOffset _trialStartWallClock;
    private int _trialNumber;

    public GameEngine(GameSettings settings, TimeProvider? timeProvider = null)
    {
        _settings = settings;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _flowAverage = new MovingAverage(settings.HistoryWindow);
        _volumeAverage = new MovingAverage(settings.HistoryWindow);
        _trialDetector = new TrialDetector(settings);
        _physics = new BallPhysics(settings);
        _scorer = new Scorer(settings);
        _colorMap = new ColorMap(settings);
    }

    /// <summary>Identifiers stamped onto each trial this session produces.</summary>
    public string? SubjectId { get; set; }

    public string? SessionId { get; set; }

    public bool IsInTrial => _trialDetector.IsInTrial;

    public int CompletedTrialCount => _trialNumber;

    public ColorMap Colors => _colorMap;

    /// <summary>
    /// Advances the game by one frame.
    /// </summary>
    public GameFrame Step(GameInput input)
    {
        TimeSpan elapsed = _lastTimestamp is { } last && input.Timestamp > last
            ? input.Timestamp - last
            : TimeSpan.Zero;
        _lastTimestamp = input.Timestamp;

        bool flowValid = !input.FlowStale;

        _flowAverage.Add(Math.Max(input.FlowLps, 0.0));
        _volumeAverage.Add(input.Spl);

        double flow = Math.Max(_flowAverage.Average, MinimumFlowLps);
        double volume = _volumeAverage.Average;

        var transition = _trialDetector.Update(Math.Max(input.FlowLps, 0.0), flowValid);

        bool trialStarted = false;
        TrialResult? completedTrial = null;
        IReadOnlyList<TrialSample>? completedSamples = null;

        if (transition == TrialTransition.Started)
        {
            StartTrial(input.Timestamp);
            trialStarted = true;
        }

        var flowState = GetFlowState(flow);
        double boundedFlow = Math.Clamp(flow, _settings.LowerFlowLimit, _settings.UpperFlowLimit);

        var ball = _physics.Update(volume, flow, boundedFlow, elapsed);
        var score = _scorer.Score(volume, flow, flowState);
        bool inGoal = _scorer.IsInGoal(score.RatioFraction);

        // Ratio feedback is meaningless while flow is outside the limits, so the ball goes flat
        // gray instead of showing a color the participant could chase.
        var color = flowState == FlowBoundState.InLimits
            ? _colorMap.GetColor(score.RatioFraction)
            : _colorMap.OutOfLimitsColor;

        double trialElapsed = _trialDetector.IsInTrial
            ? (input.Timestamp - _trialStartTimestamp).TotalSeconds
            : 0.0;

        if (_trialDetector.IsInTrial)
        {
            _samples.Add(new TrialSample
            {
                Time = trialElapsed,
                Volume = volume,
                Flow = flow,
                VolumeRaw = input.Spl,
                FlowRaw = input.FlowLps,
                BallCenter = ball.BallCenter,
                GoalUpper = ball.GoalCenter + ball.GoalHeight / 2,
                GoalLower = ball.GoalCenter - ball.GoalHeight / 2,
                BallRed = color.R8,
                BallGreen = color.G8,
                BallBlue = color.B8,
                Error = score.Error,
                FlowError = double.IsNaN(score.FlowError) ? null : score.FlowError,
                VolumeError = double.IsNaN(score.VolumeError) ? null : score.VolumeError,
                RatioFraction = score.RatioFraction,
                RatioError = score.RatioError,
                InGoal = inGoal,
                FlowOutOfLimits = flowState != FlowBoundState.InLimits,
            });
        }

        if (transition == TrialTransition.Ended)
        {
            completedSamples = _samples.ToArray();
            completedTrial = Summarize(completedSamples);
            _samples.Clear();
        }

        return new GameFrame
        {
            Timestamp = input.Timestamp,
            Volume = volume,
            Flow = flow,
            Ball = ball,
            BallColor = color,
            FlowState = flowState,
            Score = score,
            InGoal = inGoal,
            IsInTrial = _trialDetector.IsInTrial,
            TrialElapsed = trialElapsed,
            SignalLost = input.AnyStale,
            IsWarmedUp = _flowAverage.IsFull && _volumeAverage.IsFull,
            TrialStarted = trialStarted,
            CompletedTrial = completedTrial,
            CompletedSamples = completedSamples,
        };
    }

    public void Reset()
    {
        _flowAverage.Clear();
        _volumeAverage.Clear();
        _trialDetector.Reset();
        _physics.ResetPhase();
        _samples.Clear();
        _lastTimestamp = null;
    }

    private void StartTrial(TimeSpan timestamp)
    {
        _samples.Clear();
        _physics.ResetPhase();
        _trialStartTimestamp = timestamp;
        _trialStartWallClock = _timeProvider.GetUtcNow().ToLocalTime();
    }

    /// <summary>
    /// Builds the end-of-trial summary. A trial that produced no samples returns a zeroed summary
    /// rather than throwing, which is what the original did when a trial opened and closed inside
    /// a single frame.
    /// </summary>
    private TrialResult Summarize(IReadOnlyList<TrialSample> samples)
    {
        _trialNumber++;

        var result = new TrialResult
        {
            TrialNumber = _trialNumber,
            StartedAt = _trialStartWallClock,
            SampleCount = samples.Count,
            SubjectId = SubjectId,
            SessionId = SessionId,
        };

        if (samples.Count == 0) return result;

        result.DurationSeconds = samples[^1].Time;
        result.AverageError = samples.Average(s => s.Error);
        result.AverageRatioError = samples.Average(s => s.RatioError);

        // Each sample accounts for the interval that follows it, so the last sample contributes
        // no time of its own.
        for (int i = 0; i < samples.Count - 1; i++)
        {
            double interval = samples[i + 1].Time - samples[i].Time;

            if (samples[i].InGoal) result.SecondsInGoal += interval;
            if (samples[i].FlowOutOfLimits) result.SecondsOutOfLimits += interval;
            if (!samples[i].InGoal && samples[i + 1].InGoal) result.GoalEntryCount++;
        }

        // A trial that begins already in the goal has entered it once.
        if (samples[0].InGoal) result.GoalEntryCount++;

        result.FractionInGoal = result.DurationSeconds > 0
            ? result.SecondsInGoal / result.DurationSeconds
            : 0.0;

        return result;
    }

    private FlowBoundState GetFlowState(double flow)
    {
        if (flow < _settings.LowerFlowLimit) return FlowBoundState.BelowLimit;
        if (flow > _settings.UpperFlowLimit) return FlowBoundState.AboveLimit;
        return FlowBoundState.InLimits;
    }
}
