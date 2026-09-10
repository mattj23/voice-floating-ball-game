using PortAudioSharp;

namespace VoiceBallGame.Hardware;

/// <param name="Key">
/// Identifies the device for calibration storage. PortAudio device indexes change as hardware is
/// plugged and unplugged, so the key is built from the name and host API instead.
/// </param>
public sealed record AudioInputDevice(int Index, string Name, string Key, int HostApi, double DefaultSampleRate)
{
    public bool IsDefault { get; init; }
}

/// <summary>Lists the microphones PortAudio can capture from.</summary>
public static class AudioDeviceCatalog
{
    public static IReadOnlyList<AudioInputDevice> List() => PortAudioRuntime.Use(() =>
    {
        var devices = new List<AudioInputDevice>();
        int defaultIndex = PortAudio.DefaultInputDevice;

        for (int i = 0; i < PortAudio.DeviceCount; i++)
        {
            DeviceInfo info;

            try
            {
                info = PortAudio.GetDeviceInfo(i);
            }
            catch (Exception)
            {
                // A device without a readable description cannot be selected for use.
                continue;
            }

            if (info.maxInputChannels < 1) continue;

            string name = string.IsNullOrWhiteSpace(info.name) ? $"Input {i}" : info.name;

            devices.Add(new AudioInputDevice(
                Index: i,
                Name: name,
                Key: $"{info.hostApi}:{name}",
                HostApi: info.hostApi,
                DefaultSampleRate: info.defaultSampleRate)
            {
                IsDefault = i == defaultIndex,
            });
        }

        // Put the system default first so the setup screen preselects it.
        return devices.OrderByDescending(d => d.IsDefault).ThenBy(d => d.Name, StringComparer.CurrentCulture).ToList();
    });
}
