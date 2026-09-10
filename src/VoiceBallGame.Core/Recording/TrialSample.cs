namespace VoiceBallGame.Core.Recording;

/// <summary>
/// One recorded frame of a trial. Field names match the original application's trial files where
/// the meaning is unchanged, so existing analysis scripts keep working.
/// </summary>
public class TrialSample
{
    /// <summary>Seconds since the first frame of the trial.</summary>
    public double Time { get; set; }

    /// <summary>Smoothed loudness in dB SPL.</summary>
    public double Volume { get; set; }

    /// <summary>Smoothed flow in L/s.</summary>
    public double Flow { get; set; }

    /// <summary>Instantaneous loudness for this frame, before smoothing.</summary>
    public double VolumeRaw { get; set; }

    /// <summary>Instantaneous flow for this frame, before smoothing.</summary>
    public double FlowRaw { get; set; }

    /// <summary>Vertical position of the ball in pixels.</summary>
    public double BallCenter { get; set; }

    /// <summary>
    /// Upper edge of the goal box in pixels. The original recorded the box center plus its full
    /// height rather than half of it, making the recorded band twice as tall as the one drawn.
    /// </summary>
    public double GoalUpper { get; set; }

    /// <summary>Lower edge of the goal box in pixels.</summary>
    public double GoalLower { get; set; }

    public byte? BallRed { get; set; }
    public byte? BallGreen { get; set; }
    public byte? BallBlue { get; set; }

    /// <summary>Protocol error for this frame. See <see cref="Engine.ScoreResult"/>.</summary>
    public double Error { get; set; }

    /// <summary>Flow component of the error while out of limits, otherwise null.</summary>
    public double? FlowError { get; set; }

    /// <summary>Loudness component of the error while out of limits, otherwise null.</summary>
    public double? VolumeError { get; set; }

    /// <summary>Current loudness-to-flow ratio divided by the goal ratio.</summary>
    public double RatioFraction { get; set; }

    /// <summary>Dimensionless distance from target, <c>|RatioFraction - 1|</c>.</summary>
    public double RatioError { get; set; }

    /// <summary>Whether this frame counted as being in the goal.</summary>
    public bool InGoal { get; set; }

    /// <summary>Whether flow was outside the configured limits for this frame.</summary>
    public bool FlowOutOfLimits { get; set; }
}
