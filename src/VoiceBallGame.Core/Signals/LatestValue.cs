namespace VoiceBallGame.Core.Signals;

/// <summary>
/// Holds the most recent value a source produced, along with when it arrived.
/// </summary>
/// <remarks>
/// The two inputs run at different rates on different threads: the flow meter streams over a
/// serial port while the microphone delivers a buffer every 50 ms, and neither is aligned to the
/// other or to the game's tick. Rather than trying to pair samples up, each source writes into
/// one of these and the game tick reads whatever is current. That keeps the engine on a steady
/// cadence regardless of how the devices behave.
/// </remarks>
public sealed class LatestValue<T> where T : struct
{
    private readonly object _gate = new();
    private T _value;
    private TimeSpan _updatedAt;
    private bool _hasValue;

    public void Set(T value, TimeSpan at)
    {
        lock (_gate)
        {
            _value = value;
            _updatedAt = at;
            _hasValue = true;
        }
    }

    /// <summary>
    /// Reads the current value and reports whether it is older than <paramref name="maxAge"/>.
    /// A value that has never been set reads as stale.
    /// </summary>
    public (T Value, bool IsStale) Read(TimeSpan now, TimeSpan maxAge)
    {
        lock (_gate)
        {
            if (!_hasValue) return (default, true);
            return (_value, now - _updatedAt > maxAge);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _value = default;
            _updatedAt = default;
            _hasValue = false;
        }
    }
}
