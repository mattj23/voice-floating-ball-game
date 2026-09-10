using VoiceBallGame.Core.Configuration;
using VoiceBallGame.Core.Engine;

namespace VoiceBallGame.Core.Tests;

public class ScorerTests
{
    private static GameSettings Settings(bool legacy = false) => new()
    {
        GoalRatio = 800,
        UpperFlowLimit = 0.1,
        LowerFlowLimit = 0.08,
        ScoringRatioMin = 0.95,
        ScoringRatioMax = 1.05,
        LegacyCompatScoring = legacy,
    };

    [Fact]
    public void OnTarget_ScoresZeroError()
    {
        // 0.09 L/s at the goal ratio of 800 is 72 dB.
        var result = new Scorer(Settings()).Score(72.0, 0.09, FlowBoundState.InLimits);

        Assert.Equal(0.0, result.Error, 9);
        Assert.Equal(1.0, result.RatioFraction, 9);
        Assert.Equal(0.0, result.RatioError, 9);
    }

    [Fact]
    public void WithinLimits_ErrorIsTheDistanceFromTheGoalRatio()
    {
        var result = new Scorer(Settings()).Score(81.0, 0.09, FlowBoundState.InLimits);

        Assert.Equal(900.0, 81.0 / 0.09, 9);
        Assert.Equal(100.0, result.Error, 9);
        Assert.Equal(1.125, result.RatioFraction, 9);
        Assert.Equal(0.125, result.RatioError, 9);
        Assert.True(double.IsNaN(result.FlowError));
        Assert.True(double.IsNaN(result.VolumeError));
    }

    [Fact]
    public void AboveTheLimit_ErrorSumsTheFlowAndLoudnessDistances()
    {
        // The loudness associated with the 0.1 L/s upper limit is 800 * 0.1 = 80 dB.
        var result = new Scorer(Settings()).Score(85.0, 0.12, FlowBoundState.AboveLimit);

        Assert.Equal(0.02, result.FlowError, 9);
        Assert.Equal(5.0, result.VolumeError, 9);
        Assert.Equal(5.02, result.Error, 9);
    }

    [Fact]
    public void BelowTheLimit_ErrorSumsTheFlowAndLoudnessDistances()
    {
        // The loudness associated with the 0.08 L/s lower limit is 800 * 0.08 = 64 dB.
        var result = new Scorer(Settings()).Score(60.0, 0.05, FlowBoundState.BelowLimit);

        Assert.Equal(0.03, result.FlowError, 9);
        Assert.Equal(4.0, result.VolumeError, 9);
        Assert.Equal(4.03, result.Error, 9);
    }

    [Fact]
    public void LegacyModeReproducesTheDivisionDefect()
    {
        // The original computed the loudness at a flow limit as limit / goal_ratio rather than
        // goal_ratio * limit, giving 0.000125 dB instead of 80 dB. The loudness term then
        // amounted to the raw dB reading, inflating out-of-limit error by orders of magnitude.
        var legacy = new Scorer(Settings(legacy: true)).Score(85.0, 0.12, FlowBoundState.AboveLimit);
        var fixedUp = new Scorer(Settings()).Score(85.0, 0.12, FlowBoundState.AboveLimit);

        Assert.Equal(85.0 - 0.1 / 800.0, legacy.VolumeError, 9);
        Assert.Equal(5.0, fixedUp.VolumeError, 9);
        Assert.True(legacy.Error > fixedUp.Error * 15);
    }

    [Fact]
    public void LegacyModeDoesNotChangeErrorWithinTheLimits()
    {
        // Only the out-of-limit branch was affected, so in-limit data stays comparable.
        var legacy = new Scorer(Settings(legacy: true)).Score(81.0, 0.09, FlowBoundState.InLimits);
        var fixedUp = new Scorer(Settings()).Score(81.0, 0.09, FlowBoundState.InLimits);

        Assert.Equal(fixedUp.Error, legacy.Error, 9);
    }

    [Theory]
    [InlineData(0.949, false)]
    [InlineData(0.95, true)]
    [InlineData(1.00, true)]
    [InlineData(1.05, true)]
    [InlineData(1.051, false)]
    public void TheGoalWindowIsInclusiveOfItsBounds(double ratioFraction, bool expected)
    {
        Assert.Equal(expected, new Scorer(Settings()).IsInGoal(ratioFraction));
    }
}
