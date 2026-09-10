using VoiceBallGame.Core.Configuration;

namespace VoiceBallGame.Core.Engine;

/// <summary>The geometry of one frame, in screen pixels with y increasing downward.</summary>
/// <param name="BallCenter">Vertical position of the ball including its oscillation.</param>
/// <param name="BallRestPosition">
/// Vertical position the ball oscillates about. The renderer interpolates this between engine
/// frames and adds the oscillation itself, which is how ~20 Hz data drives a smooth 60 fps ball.
/// </param>
/// <param name="Amplitude">Oscillation amplitude in pixels.</param>
/// <param name="FrequencyHz">Oscillation frequency in Hz.</param>
/// <param name="Phase">Oscillation phase in radians at this frame's timestamp.</param>
/// <param name="GoalCenter">Vertical center of the goal box.</param>
/// <param name="GoalHeight">Height of the goal box, including the ball's diameter.</param>
public readonly record struct BallState(
    double BallCenter,
    double BallRestPosition,
    double Amplitude,
    double FrequencyHz,
    double Phase,
    double GoalCenter,
    double GoalHeight);

/// <summary>
/// Turns smoothed flow and loudness into the on-screen geometry of the ball and goal box.
/// </summary>
/// <remarks>
/// Flow sets where the ball and goal sit vertically, and how tall the goal box is. Loudness sets
/// how far and how fast the ball oscillates about that position: at the volume floor there is no
/// oscillation, and at the loudness matching the goal ratio the swing fills the goal box.
///
/// Phase is integrated from the elapsed time passed in rather than read from a clock, both so the
/// class stays testable and so a change in frequency does not make the ball jump.
/// </remarks>
public sealed class BallPhysics
{
    private const double TwoPi = Math.PI * 2;

    private readonly GameSettings _settings;

    public BallPhysics(GameSettings settings) => _settings = settings;

    /// <summary>The current oscillation phase in radians.</summary>
    public double Phase { get; private set; }

    public void ResetPhase() => Phase = 0;

    public BallState Update(double volumeDb, double flowLps, double boundedFlowLps, TimeSpan elapsed)
    {
        double goalCenterUnits = _settings.FlowPositionScale * boundedFlowLps - _settings.FlowPositionOffset;
        double goalHalfHeightUnits = _settings.GoalHalfHeightFactor * boundedFlowLps;
        double ballRestUnits = _settings.FlowPositionScale * flowLps - _settings.FlowPositionOffset;

        // The loudness that would put the participant exactly on the goal ratio at this flow.
        double goalVolume = _settings.GoalRatio * boundedFlowLps;

        // Oscillation grows from nothing at the volume floor to the full goal half-height at the
        // goal loudness. A goal loudness at or below the floor would divide by zero, so it is
        // treated as no oscillation.
        double denominator = goalVolume - _settings.VolumeFloorDb;
        double fraction = Math.Abs(denominator) < double.Epsilon
            ? 0.0
            : (volumeDb - _settings.VolumeFloorDb) / denominator;
        double amplitudeUnits = goalHalfHeightUnits * fraction;

        double frequency = volumeDb * _settings.FrequencyBase / 1000.0 * _settings.Frequency;
        Phase = (Phase + TwoPi * frequency * elapsed.TotalSeconds) % TwoPi;
        if (Phase < 0) Phase += TwoPi;

        // The canvas has y increasing downward while the game's units increase upward, hence the
        // sign flip before applying the scale and origin.
        double scale = _settings.GraphicsScale;
        double origin = _settings.GraphicsOrigin;

        double ballRest = -ballRestUnits * scale + origin;
        double amplitude = amplitudeUnits * scale;
        double ballCenter = ballRest - amplitude * Math.Cos(Phase);

        return new BallState(
            BallCenter: ballCenter,
            BallRestPosition: ballRest,
            Amplitude: amplitude,
            FrequencyHz: frequency,
            Phase: Phase,
            GoalCenter: -goalCenterUnits * scale + origin,
            GoalHeight: 2 * goalHalfHeightUnits * scale + _settings.BallSize);
    }
}
