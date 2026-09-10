using System.IO.Ports;
using System.Runtime.InteropServices;
using NicolaySerialSFM3x00;

namespace VoiceBallGame.Hardware;

public sealed record SerialPortInfo(string PortName, string Description);

/// <summary>
/// Lists the serial ports a flow meter might be attached to, and offers a one-shot reading so the
/// operator can confirm the right port before starting a session.
/// </summary>
public static class SerialPortCatalog
{
    public static IReadOnlyList<SerialPortInfo> List()
    {
        var ports = SerialPort.GetPortNames()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        // On Linux GetPortNames reports every /dev/tty*, most of which are virtual consoles rather
        // than serial devices. Restrict the list to USB and hardware serial prefixes that could
        // represent an attached flow meter.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            ports = ports
                .Where(p => p.Contains("ttyUSB", StringComparison.Ordinal)
                            || p.Contains("ttyACM", StringComparison.Ordinal)
                            || p.Contains("ttyS", StringComparison.Ordinal))
                .ToList();
        }

        return ports.Select(p => new SerialPortInfo(p, DescribePort(p))).ToList();
    }

    private static string DescribePort(string portName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (portName.Contains("ttyUSB", StringComparison.Ordinal)) return "USB serial adapter";
            if (portName.Contains("ttyACM", StringComparison.Ordinal)) return "USB device";
            return "Serial port";
        }

        return "Serial port";
    }

    /// <summary>
    /// Opens the port, runs the device's self test and reads one flow value.
    /// </summary>
    /// <returns>
    /// A message suitable for display next to the port picker and a value that indicates whether
    /// the device responded.
    /// </returns>
    public static async Task<(bool Ok, string Message)> TestAsync(
        string portName, byte address = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var device = new SfmDevice(portName, address);
            await device.Connect().ConfigureAwait(false);

            if (!await device.Check(cancellationToken: cancellationToken).ConfigureAwait(false))
                return (false, $"{portName} answered but did not pass its self test.");

            var flow = await device.GetFlowSlmAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            if (flow is null)
                return (false, $"{portName} is connected but its flow sensor could not be read.");

            return (true, $"Connected. Reading {flow / 60.0:F4} L/s ({flow:F2} slm).");
        }
        catch (SfmTimeoutException)
        {
            return (false, $"Nothing answered on {portName}. Check the cable and that the meter has power.");
        }
        catch (UnauthorizedAccessException)
        {
            // On Linux, this error usually requires adding the user to the dialout group.
            return (false, RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? $"No permission to open {portName}. Add your user to the 'dialout' group and log in again."
                : $"No permission to open {portName}. It may be in use by another program.");
        }
        catch (Exception e)
        {
            return (false, $"{portName} could not be opened: {e.Message}");
        }
    }
}
