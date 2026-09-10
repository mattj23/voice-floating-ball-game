using Tomlyn;
using Tomlyn.Model;

namespace VoiceBallGame.Core.Configuration;

/// <summary>
/// Reads <see cref="GameSettings"/> from the experimenter's TOML file.
/// </summary>
/// <remarks>
/// The loader reads the file into Tomlyn's document model and manually maps each value to
/// <see cref="GameSettings"/>. Automatic binding silently ignores unrecognized keys. For example,
/// a mistyped key such as "goal_ration" would leave the default target active. Manual mapping
/// ensures that the loader either recognizes or reports every key.
///
/// The loader preserves the experimenter's comments by never writing back to the file.
/// </remarks>
public static class SettingsLoader
{
    /// <summary>The file name the application looks for.</summary>
    public const string FileName = "app_settings.toml";

    public static GameSettings Parse(string toml)
    {
        TomlTable table;

        try
        {
            table = TomlSerializer.Deserialize<TomlTable>(toml)
                    ?? throw new SettingsException("The settings file is empty.");
        }
        catch (TomlException e)
        {
            throw new SettingsException($"The settings file could not be read.{Environment.NewLine}{e.Message}", e);
        }

        var reader = new TomlReader(table);
        var settings = new GameSettings();

        settings.SampleRate = reader.Int("sample_rate", settings.SampleRate);
        settings.BufferMs = reader.Int("buffer_ms", settings.BufferMs);
        settings.EngineTickMs = reader.Int("engine_tick_ms", settings.EngineTickMs);
        settings.StaleTicks = reader.Int("stale_ticks", settings.StaleTicks);
        settings.SerialBaudRate = reader.Int("serial_baud_rate", settings.SerialBaudRate);
        settings.FlowCorrectionFactor = reader.Double("flow_correction_factor", settings.FlowCorrectionFactor);

        settings.HistoryWindow = reader.Int("history_window", settings.HistoryWindow);
        settings.TrialStartWindow = reader.Int("trial_start_window", settings.TrialStartWindow);
        settings.TrialStartThreshold = reader.Double("trial_start_threshold", settings.TrialStartThreshold);

        settings.GoalRatio = reader.Double("goal_ratio", settings.GoalRatio);
        settings.UpperFlowLimit = reader.Double("upper_flow_limit", settings.UpperFlowLimit);
        settings.LowerFlowLimit = reader.Double("lower_flow_limit", settings.LowerFlowLimit);
        settings.ScoringRatioMin = reader.Double("scoring_ratio_min", settings.ScoringRatioMin);
        settings.ScoringRatioMax = reader.Double("scoring_ratio_max", settings.ScoringRatioMax);

        settings.BallSize = reader.Double("ball_size", settings.BallSize);
        settings.GraphicsScale = reader.Double("graphics_scale", settings.GraphicsScale);
        settings.GraphicsOrigin = reader.Double("graphics_origin", settings.GraphicsOrigin);

        settings.Frequency = reader.Double("frequency", settings.Frequency);
        settings.FrequencyBase = reader.Double("frequency_base", settings.FrequencyBase);
        settings.VolumeFloorDb = reader.Double("volume_floor_db", settings.VolumeFloorDb);
        settings.FlowPositionScale = reader.Double("flow_position_scale", settings.FlowPositionScale);
        settings.FlowPositionOffset = reader.Double("flow_position_offset", settings.FlowPositionOffset);
        settings.GoalHalfHeightFactor = reader.Double("goal_half_height_factor", settings.GoalHalfHeightFactor);

        settings.ColorBlendZone = reader.Double("color_blend_zone", settings.ColorBlendZone);
        settings.ColorBlendSteps = reader.Int("color_blend_steps", settings.ColorBlendSteps);
        settings.BallColorScale = ReadColorScale(reader) ?? settings.BallColorScale;

        settings.OutputDirectory = reader.String("output_directory", settings.OutputDirectory);
        settings.WriteCsv = reader.Bool("write_csv", settings.WriteCsv);

        settings.LegacyCompatScoring = reader.Bool("legacy_compat_scoring", settings.LegacyCompatScoring);

        settings.ReplayProviders = ReadReplayProviders(reader) ?? settings.ReplayProviders;

        reader.ReportUnknownKeys();

        if (reader.Problems.Count > 0) throw Problem(reader.Problems);

        Validate(settings);
        return settings;
    }

    public static GameSettings Load(string path)
    {
        if (!File.Exists(path))
            throw new SettingsException($"No settings file was found at '{Path.GetFullPath(path)}'.");

        string toml;

        try
        {
            toml = File.ReadAllText(path);
        }
        catch (IOException e)
        {
            throw new SettingsException($"The settings file at '{path}' could not be read: {e.Message}", e);
        }

        try
        {
            return Parse(toml);
        }
        catch (SettingsException e)
        {
            // Include the file name so an operator with multiple copies knows which one to edit.
            throw new SettingsException($"There is a problem with '{Path.GetFullPath(path)}'.{Environment.NewLine}{e.Message}", e);
        }
    }

    private static ColorScaleKeypoint[]? ReadColorScale(TomlReader reader)
    {
        var entries = reader.TableArray("ball_color_scale");
        if (entries is null) return null;

        var keypoints = new List<ColorScaleKeypoint>();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = new TomlReader(entries[i], $"ball_color_scale[{i}]", reader.Problems);
            var rgb = entry.DoubleArray("rgb") ?? [0, 0, 0];

            keypoints.Add(new ColorScaleKeypoint
            {
                Ratio = entry.Double("ratio", 0),
                Rgb = rgb,
            });

            entry.ReportUnknownKeys();
        }

        return keypoints.ToArray();
    }

    private static ReplayProviderConfig[]? ReadReplayProviders(TomlReader reader)
    {
        var entries = reader.TableArray("replay_providers");
        if (entries is null) return null;

        var providers = new List<ReplayProviderConfig>();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = new TomlReader(entries[i], $"replay_providers[{i}]", reader.Problems);

            providers.Add(new ReplayProviderConfig
            {
                Name = entry.String("name", string.Empty),
                FlowFile = entry.OptionalString("flow_file"),
                VolumeFile = entry.OptionalString("volume_file"),
            });

            entry.ReportUnknownKeys();
        }

        return providers.ToArray();
    }

    /// <summary>
    /// Checks for invalid settings that would otherwise cause behavior that is difficult to
    /// diagnose from the screen. Reports each problem to the experimenter.
    /// </summary>
    public static void Validate(GameSettings s)
    {
        var problems = new List<string>();

        if (s.SampleRate <= 0) problems.Add("sample_rate must be greater than zero.");
        if (s.BufferMs <= 0) problems.Add("buffer_ms must be greater than zero.");
        if (s.EngineTickMs <= 0) problems.Add("engine_tick_ms must be greater than zero.");
        if (s.StaleTicks <= 0) problems.Add("stale_ticks must be greater than zero.");
        if (s.HistoryWindow <= 0) problems.Add("history_window must be greater than zero.");
        if (s.TrialStartWindow <= 0) problems.Add("trial_start_window must be greater than zero.");
        if (s.GoalRatio <= 0) problems.Add("goal_ratio must be greater than zero.");
        if (s.FlowCorrectionFactor <= 0) problems.Add("flow_correction_factor must be greater than zero.");

        if (s.LowerFlowLimit >= s.UpperFlowLimit)
            problems.Add($"lower_flow_limit ({s.LowerFlowLimit}) must be below upper_flow_limit ({s.UpperFlowLimit}).");

        if (s.ScoringRatioMin >= s.ScoringRatioMax)
            problems.Add($"scoring_ratio_min ({s.ScoringRatioMin}) must be below scoring_ratio_max ({s.ScoringRatioMax}).");

        if (s.BallColorScale.Length == 0)
            problems.Add("ball_color_scale must contain at least one keypoint.");

        foreach (var keypoint in s.BallColorScale)
        {
            if (keypoint.Rgb.Length != 3)
            {
                problems.Add($"The ball_color_scale keypoint at ratio {keypoint.Ratio} needs exactly three rgb values.");
                continue;
            }

            if (keypoint.Rgb.Any(c => c is < 0 or > 1))
                problems.Add($"The ball_color_scale keypoint at ratio {keypoint.Ratio} has rgb values outside the range 0 to 1.");
        }

        if (s.ColorBlendSteps <= 0) problems.Add("color_blend_steps must be greater than zero.");
        if (s.ColorBlendZone < 0) problems.Add("color_blend_zone cannot be negative.");

        // The blend zones around adjacent keypoints must not overlap, or the color lookup ends up
        // non-monotonic and the ball's color stops tracking the ratio in a predictable way.
        var ordered = s.BallColorScale.OrderBy(k => k.Ratio).ToArray();
        for (int i = 1; i < ordered.Length; i++)
        {
            if (ordered[i - 1].Ratio + s.ColorBlendZone > ordered[i].Ratio - s.ColorBlendZone)
            {
                problems.Add(
                    $"The color blend zones around ratios {ordered[i - 1].Ratio} and {ordered[i].Ratio} overlap. " +
                    $"Reduce color_blend_zone below {(ordered[i].Ratio - ordered[i - 1].Ratio) / 2:G3}.");
            }
        }

        if (problems.Count > 0) throw Problem(problems);
    }

    private static SettingsException Problem(IEnumerable<string> problems) =>
        new("The settings file has problems:" + Environment.NewLine + " - " +
            string.Join(Environment.NewLine + " - ", problems));
}

public class SettingsException : Exception
{
    public SettingsException(string message) : base(message) { }
    public SettingsException(string message, Exception inner) : base(message, inner) { }
}
