using System.Diagnostics;

namespace VoiceBallGame.Core.Signals;

/// <summary>
/// A monotonic clock shared by every signal source so their timestamps are on one timeline.
/// Tests substitute a clock they control, which is what makes engine behavior reproducible.
/// </summary>
public interface IGameClock
{
    TimeSpan Elapsed { get; }
}

public sealed class StopwatchClock : IGameClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public void Restart() => _stopwatch.Restart();
}

/// <summary>A clock that only moves when a test moves it.</summary>
public sealed class ManualClock : IGameClock
{
    public TimeSpan Elapsed { get; private set; }

    public ManualClock(TimeSpan? start = null) => Elapsed = start ?? TimeSpan.Zero;

    public void Advance(TimeSpan by) => Elapsed += by;

    public void AdvanceMs(double ms) => Advance(TimeSpan.FromMilliseconds(ms));

    public void SetTo(TimeSpan time) => Elapsed = time;
}
