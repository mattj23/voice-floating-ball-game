using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoiceBallGame.Core.Calibration;

/// <summary>
/// Maps a microphone's measured signal level to loudness in dB SPL.
/// </summary>
/// <remarks>
/// The operator produces a steady sound and reads its level from a sound level meter. The
/// calibration pairs the recorded signal level with that reading and then uses the decibel
/// relationship <c>dB = 20 log10(level / reference) + referenceDb</c>.
///
/// The original collected three levels through its calibration screen but only ever used the
/// first, so the other two were recorded and discarded. The calculation requires one reference
/// point, which is the value stored here.
/// </remarks>
public sealed class MicCalibration
{
    /// <summary>Identifies the microphone this calibration belongs to.</summary>
    public string DeviceKey { get; set; } = string.Empty;

    /// <summary>A readable device name that helps the operator recognize a stale calibration.</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>The measured signal level at the reference, as a root mean square amplitude.</summary>
    public double ReferenceLevel { get; set; }

    /// <summary>The sound level meter's reading at that level, in dB SPL.</summary>
    public double ReferenceDb { get; set; }

    public DateTimeOffset Created { get; set; }

    [JsonIgnore]
    public bool IsUsable => ReferenceLevel > 0 && double.IsFinite(ReferenceDb);

    /// <summary>Converts a measured signal level to dB SPL.</summary>
    public double ToDecibels(double level)
    {
        if (!IsUsable) return double.NaN;

        // Silence would take the logarithm of zero. The floor keeps the result finite and well
        // below any level the game reacts to.
        if (level <= 1e-12) return ReferenceDb - 240.0;

        return 20.0 * Math.Log10(level / ReferenceLevel) + ReferenceDb;
    }

    /// <summary>Builds a calibration from one measured level and its true loudness.</summary>
    public static MicCalibration FromReference(string deviceKey, string deviceName, double level, double db,
        TimeProvider? timeProvider = null)
    {
        if (level <= 0)
            throw new ArgumentOutOfRangeException(nameof(level), level,
                "The measured level must be above zero. Was the microphone silent during the recording?");

        return new MicCalibration
        {
            DeviceKey = deviceKey,
            DeviceName = deviceName,
            ReferenceLevel = level,
            ReferenceDb = db,
            Created = (timeProvider ?? TimeProvider.System).GetUtcNow().ToLocalTime(),
        };
    }
}

/// <summary>
/// Keeps microphone calibrations on disk between sessions, keyed by device.
/// </summary>
/// <remarks>
/// The original keyed calibrations by the Windows device GUID that NAudio reported. That has no
/// cross-platform equivalent. The key therefore combines the device name with its host API index,
/// which PortAudio provides on every supported platform. Two identical microphones on the same
/// host API are indistinguishable, but the stored device name helps the operator identify a stale
/// calibration.
/// </remarks>
public sealed class CalibrationStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _path;
    private Dictionary<string, MicCalibration> _entries = new(StringComparer.Ordinal);

    public CalibrationStore(string path)
    {
        _path = path;
        Load();
    }

    public static string DefaultPath =>
        Path.Combine(AppContext.BaseDirectory, "calibrations.json");

    public MicCalibration? Find(string deviceKey) =>
        _entries.TryGetValue(deviceKey, out var calibration) && calibration.IsUsable ? calibration : null;

    public void Save(MicCalibration calibration)
    {
        _entries[calibration.DeviceKey] = calibration;
        Persist();
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;

        try
        {
            var entries = JsonSerializer.Deserialize<List<MicCalibration>>(File.ReadAllText(_path), Options);
            if (entries is null) return;

            _entries = entries
                .Where(e => e.IsUsable)
                .GroupBy(e => e.DeviceKey, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.Created).First(), StringComparer.Ordinal);
        }
        catch (Exception)
        {
            // A corrupt calibration file must not stop the game from starting. The operator is
            // asked to calibrate again, which rewrites the file.
            _entries = new Dictionary<string, MicCalibration>(StringComparer.Ordinal);
        }
    }

    private void Persist()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        File.WriteAllText(_path, JsonSerializer.Serialize(_entries.Values.ToList(), Options));
    }
}
