namespace VoiceBallGame.Core.Recording;

/// <summary>The summary of a completed trial, both shown to the participant and saved with the data.</summary>
public class TrialResult
{
    public int TrialNumber { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Trial length in seconds.</summary>
    public double DurationSeconds { get; set; }

    public int SampleCount { get; set; }

    /// <summary>Seconds spent with the ratio inside the scoring window.</summary>
    public double SecondsInGoal { get; set; }

    /// <summary>Fraction of the trial spent inside the scoring window, 0 to 1.</summary>
    public double FractionInGoal { get; set; }

    /// <summary>Number of times the ratio entered the scoring window from outside it.</summary>
    public int GoalEntryCount { get; set; }

    /// <summary>Mean protocol error across the trial.</summary>
    public double AverageError { get; set; }

    /// <summary>
    /// Mean of <c>|ratio fraction - 1|</c> across the trial. This is the measure reported to the
    /// participant because its meaning is consistent whether flow was within or outside the limits.
    /// </summary>
    public double AverageRatioError { get; set; }

    /// <summary>Seconds spent with flow outside the configured limits.</summary>
    public double SecondsOutOfLimits { get; set; }

    public string? SubjectId { get; set; }

    public string? SessionId { get; set; }

    /// <summary>Path of the JSON file the trial's samples were written to, when it was saved.</summary>
    public string? DataFile { get; set; }
}
