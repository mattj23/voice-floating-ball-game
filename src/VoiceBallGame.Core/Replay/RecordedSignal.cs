using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoiceBallGame.Core.Replay;

/// <summary>What a recorded signal file holds, so a replay cannot be attached to the wrong input.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SignalKind
{
    Flow,
    Volume,
}

/// <summary>The header line of a recorded signal file.</summary>
public sealed class SignalHeader
{
    public SignalKind Kind { get; set; }

    /// <summary>Engineering units of the values, for the benefit of anyone reading the file.</summary>
    public string Units { get; set; } = string.Empty;

    public DateTimeOffset? Recorded { get; set; }

    /// <summary>The device or provider the recording came from.</summary>
    public string? Source { get; set; }

    public string? Note { get; set; }
}

public readonly record struct RecordedPoint(double TimeMs, double Value);

/// <summary>
/// Reads and writes recorded signal files.
/// </summary>
/// <remarks>
/// The original replayed WAV files, which worked only because both inputs were audio. Flow now
/// arrives from a serial flow meter as engineering values, so recordings are stored as JSON
/// Lines: one header object followed by one object per sample. The format supports streaming,
/// remains readable in a text editor, and preserves all complete lines if a write is truncated.
/// </remarks>
public static class RecordedSignal
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static (SignalHeader Header, IReadOnlyList<RecordedPoint> Points) Load(string path)
    {
        using var reader = new StreamReader(path);
        return Read(reader, path);
    }

    public static (SignalHeader Header, IReadOnlyList<RecordedPoint> Points) Parse(string content)
    {
        using var reader = new StringReader(content);
        return Read(reader, "<string>");
    }

    private static (SignalHeader, IReadOnlyList<RecordedPoint>) Read(TextReader reader, string path)
    {
        SignalHeader? header = null;
        var points = new List<RecordedPoint>();
        int lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                if (header is null)
                {
                    header = JsonSerializer.Deserialize<SignalHeader>(line, Options)
                             ?? throw new ReplayFormatException($"{path} line {lineNumber}: the header is empty.");
                    continue;
                }

                var point = JsonSerializer.Deserialize<PointLine>(line, Options);
                if (point is null) continue;
                points.Add(new RecordedPoint(point.TMs, point.V));
            }
            catch (JsonException e)
            {
                throw new ReplayFormatException($"{path} line {lineNumber} could not be read: {e.Message}", e);
            }
        }

        if (header is null)
            throw new ReplayFormatException($"{path} has no header line.");

        return (header, points);
    }

    public static void Save(string path, SignalHeader header, IEnumerable<RecordedPoint> points)
    {
        using var writer = new StreamWriter(path, append: false, Encoding.UTF8);
        Write(writer, header, points);
    }

    public static void Write(TextWriter writer, SignalHeader header, IEnumerable<RecordedPoint> points)
    {
        writer.WriteLine(JsonSerializer.Serialize(header, Options));

        foreach (var point in points)
        {
            // Write this two-number object directly because this code runs once per sample during
            // a live recording.
            writer.Write("{\"t_ms\":");
            writer.Write(point.TimeMs.ToString("G9", CultureInfo.InvariantCulture));
            writer.Write(",\"v\":");
            writer.Write(point.Value.ToString("G9", CultureInfo.InvariantCulture));
            writer.WriteLine("}");
        }
    }

    private sealed class PointLine
    {
        [JsonPropertyName("t_ms")] public double TMs { get; set; }
        [JsonPropertyName("v")] public double V { get; set; }
    }
}

public class ReplayFormatException : Exception
{
    public ReplayFormatException(string message) : base(message) { }
    public ReplayFormatException(string message, Exception inner) : base(message, inner) { }
}
