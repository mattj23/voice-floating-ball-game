using Tomlyn.Model;

namespace VoiceBallGame.Core.Configuration;

/// <summary>
/// Reads typed values from a parsed TOML table and tracks each requested key. It reports any
/// unrequested keys so that they are not silently ignored.
/// </summary>
/// <remarks>
/// The reader collects all problems before reporting them, which lets an experimenter correct
/// multiple configuration errors in one attempt.
/// </remarks>
internal sealed class TomlReader
{
    private readonly TomlTable _table;
    private readonly string? _context;
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);

    public TomlReader(TomlTable table, string? context = null, List<string>? problems = null)
    {
        _table = table;
        _context = context;
        Problems = problems ?? [];
    }

    public List<string> Problems { get; }

    public double Double(string key, double fallback)
    {
        if (!TryGet(key, out var value)) return fallback;

        // TOML distinguishes integers from floats. Accept whole numbers for decimal settings so
        // values such as "goal_ratio = 800" have the same meaning as 800.0.
        return value switch
        {
            double d => d,
            long l => l,
            _ => Reject(key, "a number", value, fallback),
        };
    }

    public int Int(string key, int fallback)
    {
        if (!TryGet(key, out var value)) return fallback;

        return value switch
        {
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            long l => Reject(key, "a whole number in range", l, fallback),
            double d when d == Math.Floor(d) && Math.Abs(d) < int.MaxValue => (int)d,
            _ => Reject(key, "a whole number", value, fallback),
        };
    }

    public bool Bool(string key, bool fallback)
    {
        if (!TryGet(key, out var value)) return fallback;

        return value switch
        {
            bool b => b,
            _ => Reject(key, "true or false", value, fallback),
        };
    }

    public string String(string key, string fallback)
    {
        if (!TryGet(key, out var value)) return fallback;

        return value switch
        {
            string s => s,
            _ => Reject(key, "text in quotes", value, fallback),
        };
    }

    public string? OptionalString(string key) => TryGet(key, out _) ? String(key, string.Empty) : null;

    public double[]? DoubleArray(string key)
    {
        if (!TryGet(key, out var value)) return null;

        if (value is not TomlArray array)
            return Reject<double[]?>(key, "a list of numbers", value, null);

        var result = new double[array.Count];

        for (int i = 0; i < array.Count; i++)
        {
            result[i] = array[i] switch
            {
                double d => d,
                long l => l,
                _ => Reject(key, $"a list of numbers; item {i + 1} is invalid", array[i], 0.0),
            };
        }

        return result;
    }

    public TomlTableArray? TableArray(string key)
    {
        if (!TryGet(key, out var value)) return null;

        return value switch
        {
            TomlTableArray array => array,
            // An empty "key = []" is a legitimate way to say there are none.
            TomlArray { Count: 0 } => new TomlTableArray(),
            _ => Reject<TomlTableArray?>(key, $"one or more [[{key}]] sections", value, null),
        };
    }

    /// <summary>
    /// Reports every key that was not requested during mapping. These keys usually contain
    /// spelling errors and would otherwise have no effect.
    /// </summary>
    public void ReportUnknownKeys()
    {
        foreach (var key in _table.Keys)
        {
            if (_used.Contains(key)) continue;

            var suggestion = Closest(key);
            Problems.Add(suggestion is null
                ? $"{Where(key)} is not a recognized setting."
                : $"{Where(key)} is not a recognized setting. Did you mean '{suggestion}'?");
        }
    }

    private bool TryGet(string key, out object? value)
    {
        _used.Add(key);
        return _table.TryGetValue(key, out value) && value is not null;
    }

    private T Reject<T>(string key, string expected, object? actual, T fallback)
    {
        Problems.Add($"{Where(key)} must be {expected}. Found '{Describe(actual)}'.");
        return fallback;
    }

    private string Where(string key) => _context is null ? $"'{key}'" : $"'{key}' in {_context}";

    private static string Describe(object? value) => value switch
    {
        null => "no value",
        bool b => b ? "true" : "false",
        TomlArray => "a list",
        TomlTable or TomlTableArray => "a section",
        _ => value.ToString() ?? "no value",
    };

    /// <summary>
    /// The known key nearest to an unrecognized one, when it is near enough that a typo is the
    /// likely explanation.
    /// </summary>
    private string? Closest(string unknown)
    {
        string? best = null;
        int bestDistance = int.MaxValue;

        foreach (var candidate in _used)
        {
            int distance = Distance(unknown, candidate);
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = candidate;
        }

        // Suggest a key only when no more than approximately one-quarter of its characters differ.
        return bestDistance <= Math.Max(2, unknown.Length / 4) ? best : null;
    }

    private static int Distance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (int j = 0; j <= b.Length; j++) previous[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;

            for (int j = 1; j <= b.Length; j++)
            {
                int substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
