using System.Text.Json;

namespace VoiceBallGame.Core.Configuration;

/// <summary>
/// Reads <see cref="GameSettings"/> from the experimenter's JSON file. The file uses
/// <c>//</c> comments to document each parameter, so the parser accepts comments and trailing
/// commas. The loader preserves the comments by never writing back to the file.
/// </summary>
public static class SettingsLoader
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public static GameSettings Parse(string json)
    {
        var settings = JsonSerializer.Deserialize<GameSettings>(json, Options)
                       ?? throw new SettingsException("The settings file parsed to nothing.");
        Validate(settings);
        return settings;
    }

    public static GameSettings Load(string path)
    {
        if (!File.Exists(path))
            throw new SettingsException($"No settings file was found at '{Path.GetFullPath(path)}'.");

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException e)
        {
            throw new SettingsException($"The settings file at '{path}' could not be read: {e.Message}", e);
        }

        try
        {
            return Parse(json);
        }
        catch (JsonException e)
        {
            throw new SettingsException(
                $"The settings file at '{path}' is not valid JSON: {e.Message}", e);
        }
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

        if (problems.Count > 0)
            throw new SettingsException("The settings file has problems:" + Environment.NewLine + " - " +
                                        string.Join(Environment.NewLine + " - ", problems));
    }
}

public class SettingsException : Exception
{
    public SettingsException(string message) : base(message) { }
    public SettingsException(string message, Exception inner) : base(message, inner) { }
}
