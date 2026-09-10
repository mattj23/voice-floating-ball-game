using VoiceBallGame.Core.Recording;

namespace VoiceBallGame.Core.Engine;

/// <summary>
/// Everything the view needs for one engine frame. The renderer keeps the two most recent frames
/// and interpolates between them, so this carries the oscillation parameters rather than just a
/// ball position.
/// </summary>
public sealed class GameFrame
{
    public required TimeSpan Timestamp { get; init; }

    /// <summary>Smoothed loudness in dB SPL.</summary>
    public required double Volume { get; init; }

    /// <summary>Smoothed flow in L/s.</summary>
    public required double Flow { get; init; }

    public required BallState Ball { get; init; }

    public required RgbColor BallColor { get; init; }

    public required FlowBoundState FlowState { get; init; }

    public bool FlowOutOfLimits => FlowState != FlowBoundState.InLimits;

    public required ScoreResult Score { get; init; }

    public required bool InGoal { get; init; }

    public required bool IsInTrial { get; init; }

    /// <summary>Seconds since the current trial started, or zero when no trial is underway.</summary>
    public required double TrialElapsed { get; init; }

    /// <summary>True while either signal is stale, meaning the reading shown is not live.</summary>
    public required bool SignalLost { get; init; }

    /// <summary>
    /// True once enough frames have been collected to fill the smoothing window. Before that the
    /// averages are still settling and the ball should not be treated as meaningful.
    /// </summary>
    public required bool IsWarmedUp { get; init; }

    /// <summary>Set on the frame where a trial started.</summary>
    public bool TrialStarted { get; init; }

    /// <summary>The completed trial's summary, set on the frame where a trial ended.</summary>
    public TrialResult? CompletedTrial { get; init; }

    /// <summary>The completed trial's samples, set on the frame where a trial ended.</summary>
    public IReadOnlyList<TrialSample>? CompletedSamples { get; init; }
}
