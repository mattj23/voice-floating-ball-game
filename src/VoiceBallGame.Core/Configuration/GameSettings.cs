namespace VoiceBallGame.Core.Configuration;

/// <summary>
/// The experimenter-facing configuration, loaded from app_settings.toml. Property names map to
/// snake_case keys in the file. Values here are the defaults used when a key is absent.
/// </summary>
public class GameSettings
{
    // ---- Acquisition -------------------------------------------------------

    /// <summary>Microphone sample rate in Hz.</summary>
    public int SampleRate { get; set; } = 22050;

    /// <summary>Microphone buffer length in milliseconds; determines the volume frame rate.</summary>
    public int BufferMs { get; set; } = 50;

    /// <summary>Period of the fixed game tick that drives the engine, in milliseconds.</summary>
    public int EngineTickMs { get; set; } = 50;

    /// <summary>
    /// Number of engine ticks a source may go without delivering a sample before it is
    /// considered stale and the signal is reported as lost.
    /// </summary>
    public int StaleTicks { get; set; } = 10;

    /// <summary>Serial baud rate used to talk to the flow meter.</summary>
    public int SerialBaudRate { get; set; } = 115200;

    /// <summary>
    /// Multiplier applied to the flow reading after conversion from standard L/min to L/s.
    /// Use this to apply a BTPS (body temperature, pressure, saturated) correction if the
    /// research protocol calls for one. 1.0 leaves the sensor's standard-conditions reading alone.
    /// </summary>
    public double FlowCorrectionFactor { get; set; } = 1.0;

    // ---- Smoothing and trial detection ------------------------------------

    /// <summary>Number of frames averaged to produce the flow and volume values used by the game.</summary>
    public int HistoryWindow { get; set; } = 11;

    /// <summary>Number of frames averaged when deciding whether a trial has started or stopped.</summary>
    public int TrialStartWindow { get; set; } = 5;

    /// <summary>Flow in L/s above which a trial starts and below which it ends.</summary>
    public double TrialStartThreshold { get; set; } = 0.001;

    // ---- Game targets ------------------------------------------------------

    /// <summary>Target ratio of loudness (dB SPL) to flow (L/s).</summary>
    public double GoalRatio { get; set; } = 800;

    /// <summary>Flow in L/s above which the participant is out of limits.</summary>
    public double UpperFlowLimit { get; set; } = 0.1;

    /// <summary>Flow in L/s below which the participant is out of limits.</summary>
    public double LowerFlowLimit { get; set; } = 0.08;

    /// <summary>Lower bound of the ratio fraction that counts as being in the goal.</summary>
    public double ScoringRatioMin { get; set; } = 0.95;

    /// <summary>Upper bound of the ratio fraction that counts as being in the goal.</summary>
    public double ScoringRatioMax { get; set; } = 1.05;

    // ---- Graphics ----------------------------------------------------------

    /// <summary>Diameter of the ball in pixels.</summary>
    public double BallSize { get; set; } = 30;

    /// <summary>Scale factor converting game units to pixels.</summary>
    public double GraphicsScale { get; set; } = 100;

    /// <summary>Vertical offset in pixels; positive values shift everything lower.</summary>
    public double GraphicsOrigin { get; set; } = 100;

    // ---- Ball physics ------------------------------------------------------

    /// <summary>Frequency scaling factor; larger makes the ball oscillate faster.</summary>
    public double Frequency { get; set; } = 1.0;

    /// <summary>
    /// Base frequency coefficient. Oscillation frequency is
    /// <c>volume * frequency_base / 1000 * frequency</c>.
    /// </summary>
    public double FrequencyBase { get; set; } = 27.7;

    /// <summary>Slope of the flow-to-vertical-position mapping.</summary>
    public double FlowPositionScale { get; set; } = 20.0;

    /// <summary>Intercept of the flow-to-vertical-position mapping.</summary>
    public double FlowPositionOffset { get; set; } = 1.25;

    /// <summary>Goal box half-height per unit of bounded flow.</summary>
    public double GoalHalfHeightFactor { get; set; } = 2.4;

    /// <summary>Volume in dB SPL at or below which the ball does not oscillate.</summary>
    public double VolumeFloorDb { get; set; } = 45.0;

    // ---- Ball color scale --------------------------------------------------

    /// <summary>
    /// Color keypoints indexed by the fraction of the goal ratio at which each color applies.
    /// </summary>
    public ColorScaleKeypoint[] BallColorScale { get; set; } =
    [
        new() { Ratio = 0.90, Rgb = [0.0, 0.0, 1.0] },
        new() { Ratio = 0.95, Rgb = [0.5, 0.5, 1.0] },
        new() { Ratio = 1.05, Rgb = [1.0, 1.0, 1.0] },
        new() { Ratio = 1.10, Rgb = [1.0, 0.5, 0.5] },
        new() { Ratio = 2.00, Rgb = [1.0, 0.0, 0.0] },
    ];

    /// <summary>The plus-or-minus zone around a keypoint over which the color blends smoothly.</summary>
    public double ColorBlendZone { get; set; } = 0.02;

    /// <summary>Number of interpolation steps used to transition between keypoint colors.</summary>
    public int ColorBlendSteps { get; set; } = 5;

    // ---- Output ------------------------------------------------------------

    /// <summary>
    /// Directory to which trial files are written. Relative paths resolve against the working
    /// directory; an empty value means the working directory itself.
    /// </summary>
    public string OutputDirectory { get; set; } = "data";

    /// <summary>When true, a CSV sibling is written next to each trial's JSON file.</summary>
    public bool WriteCsv { get; set; } = true;

    // ---- Compatibility -----------------------------------------------------

    /// <summary>
    /// When true, the scorer reproduces the original application's error formulas, including
    /// their defects, so that output can be compared against data from the WPF version.
    /// </summary>
    public bool LegacyCompatScoring { get; set; }

    // ---- Development -------------------------------------------------------

    /// <summary>Replay sources offered alongside real hardware, for development without a sensor.</summary>
    public ReplayProviderConfig[] ReplayProviders { get; set; } = [];
}

/// <summary>A single keypoint on the ball's color scale.</summary>
public class ColorScaleKeypoint
{
    /// <summary>The fraction of the goal ratio at which this keypoint's color applies.</summary>
    public double Ratio { get; set; }

    /// <summary>Red, green and blue components in the range 0 to 1.</summary>
    public double[] Rgb { get; set; } = [0, 0, 0];
}

/// <summary>A pair of recorded signal files that can be replayed in place of live hardware.</summary>
public class ReplayProviderConfig
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Path to a recorded volume stream, relative to the settings file.</summary>
    public string? VolumeFile { get; set; }

    /// <summary>Path to a recorded flow stream, relative to the settings file.</summary>
    public string? FlowFile { get; set; }
}
