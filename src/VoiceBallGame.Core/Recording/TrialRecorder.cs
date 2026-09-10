using System.Globalization;
using System.Text;
using System.Text.Json;
using VoiceBallGame.Core.Configuration;

namespace VoiceBallGame.Core.Recording;

/// <summary>
/// Writes completed trials to disk.
/// </summary>
/// <remarks>
/// The original wrote "trial {yyyy-MM-dd-hh-mm-ss}.json" into whatever directory the process
/// happened to start in, with no subject or session identifier and a 12-hour hour field that made
/// trials recorded twelve hours apart overwrite each other. Files now carry the subject and
/// session, use a 24-hour timestamp, and go to a configured directory.
/// </remarks>
public sealed class TrialRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    private readonly GameSettings _settings;

    /// <param name="settings">The settings that specify the output directory and CSV option.</param>
    /// <param name="baseDirectory">
    /// The directory against which to resolve a relative output directory. Defaults to the current
    /// directory. An installed build passes a per-user folder, because its working directory is
    /// the application folder, which updates replace.
    /// </param>
    public TrialRecorder(GameSettings settings, string? baseDirectory = null)
    {
        _settings = settings;

        var root = Path.GetFullPath(baseDirectory ?? Directory.GetCurrentDirectory());
        OutputDirectory = string.IsNullOrWhiteSpace(settings.OutputDirectory)
            ? root
            : Path.GetFullPath(settings.OutputDirectory, root);
    }

    public string OutputDirectory { get; }

    /// <summary>
    /// Saves a trial's samples and summary, returning the path of the JSON file written. The
    /// summary's <see cref="TrialResult.DataFile"/> is set to that path.
    /// </summary>
    public string Save(TrialResult result, IReadOnlyList<TrialSample> samples)
    {
        Directory.CreateDirectory(OutputDirectory);

        string baseName = BuildBaseName(result);
        string jsonPath = Path.Combine(OutputDirectory, baseName + ".json");

        var document = new TrialFile { Summary = result, Samples = samples };
        result.DataFile = jsonPath;

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(document, JsonOptions));

        if (_settings.WriteCsv)
            File.WriteAllText(Path.Combine(OutputDirectory, baseName + ".csv"), BuildCsv(samples));

        return jsonPath;
    }

    private static string BuildBaseName(TrialResult result)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(result.SubjectId)) parts.Add(Sanitize(result.SubjectId));
        if (!string.IsNullOrWhiteSpace(result.SessionId)) parts.Add(Sanitize(result.SessionId));

        parts.Add(result.StartedAt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
        parts.Add($"trial{result.TrialNumber:D3}");

        return string.Join("_", parts);
    }

    /// <summary>
    /// Characters replaced in subject and session identifiers. Path.GetInvalidFileNameChars is
    /// platform-specific, and a colon is legal on Linux but not on Windows, so a name written on
    /// the lab's Linux machine could not be copied to a Windows one. The set is fixed instead, so
    /// the same identifiers produce the same file names everywhere.
    /// </summary>
    private static readonly char[] UnsafeNameChars =
        [.. """<>:"/\|?*""", ' ', '\t', '\n', '\r', '\0', '.', ','];

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (char c in value.Trim())
            builder.Append(UnsafeNameChars.Contains(c) || char.IsControl(c) ? '-' : c);

        return builder.ToString();
    }

    private static string BuildCsv(IReadOnlyList<TrialSample> samples)
    {
        var builder = new StringBuilder();
        builder.AppendLine("time,volume,flow,volume_raw,flow_raw,ball_center,goal_lower,goal_upper," +
                           "error,flow_error,volume_error,ratio_fraction,ratio_error,in_goal,flow_out_of_limits");

        foreach (var s in samples)
        {
            builder.Append(F(s.Time)).Append(',')
                .Append(F(s.Volume)).Append(',')
                .Append(F(s.Flow)).Append(',')
                .Append(F(s.VolumeRaw)).Append(',')
                .Append(F(s.FlowRaw)).Append(',')
                .Append(F(s.BallCenter)).Append(',')
                .Append(F(s.GoalLower)).Append(',')
                .Append(F(s.GoalUpper)).Append(',')
                .Append(F(s.Error)).Append(',')
                .Append(s.FlowError is { } fe ? F(fe) : string.Empty).Append(',')
                .Append(s.VolumeError is { } ve ? F(ve) : string.Empty).Append(',')
                .Append(F(s.RatioFraction)).Append(',')
                .Append(F(s.RatioError)).Append(',')
                .Append(s.InGoal ? '1' : '0').Append(',')
                .Append(s.FlowOutOfLimits ? '1' : '0')
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string F(double value) => value.ToString("G9", CultureInfo.InvariantCulture);

    private sealed class TrialFile
    {
        public required TrialResult Summary { get; init; }
        public required IReadOnlyList<TrialSample> Samples { get; init; }
    }
}
