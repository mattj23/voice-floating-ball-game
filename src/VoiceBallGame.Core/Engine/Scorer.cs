using VoiceBallGame.Core.Configuration;

namespace VoiceBallGame.Core.Engine;

/// <summary>The error measures produced for a single frame.</summary>
/// <param name="Error">
/// The error as specified by the research protocol: inside the flow limits it is the absolute
/// difference between the current loudness-to-flow ratio and the goal ratio, in dB·s/L; outside
/// them it is the sum of the flow and loudness distances from the violated limit.
/// </param>
/// <param name="FlowError">
/// Distance in L/s from the violated flow limit, or NaN while flow is within limits.
/// </param>
/// <param name="VolumeError">
/// Distance in dB from the loudness associated with the violated flow limit, or NaN while flow
/// is within limits.
/// </param>
/// <param name="RatioFraction">The current ratio divided by the goal ratio; 1.0 is on target.</param>
/// <param name="RatioError">
/// The dimensionless distance from target, <c>|RatioFraction - 1|</c>. Unlike
/// <paramref name="Error"/>, this value has a consistent meaning whether flow is within or outside
/// the limits, so the end-of-trial feedback reports it.
/// </param>
public readonly record struct ScoreResult(
    double Error,
    double FlowError,
    double VolumeError,
    double RatioFraction,
    double RatioError);

/// <summary>
/// Computes per-frame error from the smoothed loudness and flow.
/// </summary>
/// <remarks>
/// The formulas come from the research protocol (an email from Jarrad dated 9 January 2019,
/// quoted in the original AudioProcessor):
/// <list type="number">
/// <item>Within the flow limits, error is the absolute difference between the current ratio and
/// the goal ratio.</item>
/// <item>Above the upper flow limit, error is
/// <c>|flow - upper_flow_limit| + |loudness - loudness_at_upper_flow_limit|</c>.</item>
/// <item>Below the lower flow limit, the same against the lower limit.</item>
/// </list>
/// The loudness associated with a flow limit is <c>goal_ratio * flow_limit</c>. The original code
/// divided instead of multiplying, producing 0.000125 dB rather than 80 dB for the upper limit, so
/// the loudness term was effectively the raw dB value and out-of-limit error was inflated by
/// roughly five orders of magnitude. Set legacy_compat_scoring to reproduce that behavior when
/// comparing against data recorded by the WPF version.
/// </remarks>
public sealed class Scorer
{
    private readonly GameSettings _settings;

    public Scorer(GameSettings settings) => _settings = settings;

    public ScoreResult Score(double volumeDb, double flowLps, FlowBoundState state)
    {
        double ratio = volumeDb / flowLps;
        double ratioFraction = ratio / _settings.GoalRatio;

        double error;
        double flowError = double.NaN;
        double volumeError = double.NaN;

        switch (state)
        {
            case FlowBoundState.InLimits:
                error = Math.Abs(ratio - _settings.GoalRatio);
                break;

            case FlowBoundState.AboveLimit:
                flowError = Math.Abs(flowLps - _settings.UpperFlowLimit);
                volumeError = Math.Abs(volumeDb - LoudnessAtLimit(_settings.UpperFlowLimit));
                error = flowError + volumeError;
                break;

            case FlowBoundState.BelowLimit:
                flowError = Math.Abs(flowLps - _settings.LowerFlowLimit);
                volumeError = Math.Abs(volumeDb - LoudnessAtLimit(_settings.LowerFlowLimit));
                error = flowError + volumeError;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unhandled flow state.");
        }

        return new ScoreResult(error, flowError, volumeError, ratioFraction, Math.Abs(ratioFraction - 1.0));
    }

    public bool IsInGoal(double ratioFraction) =>
        ratioFraction >= _settings.ScoringRatioMin && ratioFraction <= _settings.ScoringRatioMax;

    private double LoudnessAtLimit(double flowLimit) =>
        _settings.LegacyCompatScoring
            ? flowLimit / _settings.GoalRatio
            : _settings.GoalRatio * flowLimit;
}
