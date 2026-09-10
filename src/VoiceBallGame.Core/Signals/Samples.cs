namespace VoiceBallGame.Core.Signals;

/// <summary>A calibrated flow reading in liters per second, stamped when it arrived.</summary>
public readonly record struct FlowSample(TimeSpan Timestamp, double FlowLps);

/// <summary>A calibrated loudness reading in dB SPL, stamped when it arrived.</summary>
public readonly record struct VolumeSample(TimeSpan Timestamp, double Spl);

/// <summary>
/// The pair of readings the engine works from on a single tick. <see cref="FlowStale"/> and
/// <see cref="VolumeStale"/> mark a source that has not delivered within the configured window,
/// which lets the engine suppress gameplay rather than coast on a frozen value.
/// </summary>
public readonly record struct GameInput(
    TimeSpan Timestamp,
    double FlowLps,
    double Spl,
    bool FlowStale = false,
    bool VolumeStale = false)
{
    public bool AnyStale => FlowStale || VolumeStale;
}

/// <summary>Connection state of a signal source, so the UI can report a device that has gone away.</summary>
public enum SourceState
{
    Stopped,
    Connecting,
    Connected,
    Faulted,
}

public readonly record struct SourceStatus(SourceState State, string? Message = null)
{
    public static SourceStatus Stopped => new(SourceState.Stopped);
    public static SourceStatus Connecting => new(SourceState.Connecting);
    public static SourceStatus Connected => new(SourceState.Connected);
    public static SourceStatus Faulted(string message) => new(SourceState.Faulted, message);
}
