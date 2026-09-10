using VoiceBallGame.Core.Configuration;
using VoiceBallGame.Core.Engine;

namespace VoiceBallGame.Core.Tests;

public class BallPhysicsTests
{
    private static GameSettings Settings() => new()
    {
        GoalRatio = 800,
        UpperFlowLimit = 0.1,
        LowerFlowLimit = 0.08,
        FlowPositionScale = 20,
        FlowPositionOffset = 1.25,
        GoalHalfHeightFactor = 2.4,
        VolumeFloorDb = 45,
        FrequencyBase = 27.7,
        Frequency = 1.0,
        GraphicsScale = 100,
        GraphicsOrigin = 100,
        BallSize = 30,
    };

    [Fact]
    public void GoalGeometryFollowsFlow()
    {
        var physics = new BallPhysics(Settings());

        // At 0.09 L/s the goal center in game units is 20 * 0.09 - 1.25 = 0.55, which the canvas
        // flips and scales to -0.55 * 100 + 100 = 45. The half height is 2.4 * 0.09 = 0.216, so
        // the box is 2 * 0.216 * 100 + 30 = 73.2 pixels tall including the ball's diameter.
        var state = physics.Update(volumeDb: 45.0, flowLps: 0.09, boundedFlowLps: 0.09, TimeSpan.Zero);

        Assert.Equal(45.0, state.GoalCenter, 9);
        Assert.Equal(73.2, state.GoalHeight, 9);
    }

    [Fact]
    public void AtTheVolumeFloor_TheBallDoesNotOscillate()
    {
        var physics = new BallPhysics(Settings());

        var state = physics.Update(volumeDb: 45.0, flowLps: 0.09, boundedFlowLps: 0.09,
            TimeSpan.FromMilliseconds(50));

        Assert.Equal(0.0, state.Amplitude, 9);
        Assert.Equal(state.BallRestPosition, state.BallCenter, 9);
    }

    [Fact]
    public void AtTheGoalLoudness_TheSwingFillsTheGoalBox()
    {
        var physics = new BallPhysics(Settings());

        // 800 * 0.09 = 72 dB puts the participant exactly on the goal ratio, where the amplitude
        // should equal the goal half height of 2.4 * 0.09 * 100 = 21.6 pixels.
        var state = physics.Update(volumeDb: 72.0, flowLps: 0.09, boundedFlowLps: 0.09, TimeSpan.Zero);

        Assert.Equal(21.6, state.Amplitude, 9);
    }

    [Fact]
    public void BallRestPositionUsesUnboundedFlow_WhileTheGoalUsesBoundedFlow()
    {
        var physics = new BallPhysics(Settings());

        // Flow above the upper limit carries the ball past the goal box, which is what makes an
        // out-of-limits participant see the ball leave the target.
        var state = physics.Update(volumeDb: 45.0, flowLps: 0.15, boundedFlowLps: 0.1, TimeSpan.Zero);

        Assert.Equal(-(20 * 0.15 - 1.25) * 100 + 100, state.BallRestPosition, 9);
        Assert.Equal(-(20 * 0.10 - 1.25) * 100 + 100, state.GoalCenter, 9);
        Assert.True(state.BallRestPosition < state.GoalCenter);
    }

    [Fact]
    public void PhaseAdvancesWithElapsedTime()
    {
        var physics = new BallPhysics(Settings());

        // 72 dB gives 72 * 27.7 / 1000 = 1.9944 Hz, so a quarter second is 0.4986 of a cycle.
        physics.Update(72.0, 0.09, 0.09, TimeSpan.FromMilliseconds(250));

        double expected = 2 * Math.PI * (72.0 * 27.7 / 1000.0) * 0.25;
        Assert.Equal(expected, physics.Phase, 9);
    }

    [Fact]
    public void PhaseIsContinuousWhenFrequencyChanges()
    {
        var physics = new BallPhysics(Settings());
        var step = TimeSpan.FromMilliseconds(50);

        physics.Update(72.0, 0.09, 0.09, step);
        double before = physics.Phase;

        // A large jump in loudness changes the frequency but must not make the phase jump, or the
        // ball would visibly snap to a new position.
        physics.Update(95.0, 0.09, 0.09, step);
        double advance = physics.Phase - before;

        Assert.Equal(2 * Math.PI * (95.0 * 27.7 / 1000.0) * 0.05, advance, 9);
    }

    [Fact]
    public void PhaseStaysWithinOneTurn()
    {
        var physics = new BallPhysics(Settings());

        for (int i = 0; i < 500; i++)
            physics.Update(90.0, 0.09, 0.09, TimeSpan.FromMilliseconds(50));

        Assert.InRange(physics.Phase, 0.0, 2 * Math.PI);
    }

    [Fact]
    public void ResetPhaseReturnsTheBallToTheTopOfItsSwing()
    {
        var physics = new BallPhysics(Settings());
        physics.Update(72.0, 0.09, 0.09, TimeSpan.FromMilliseconds(120));

        physics.ResetPhase();

        Assert.Equal(0.0, physics.Phase);
    }
}
