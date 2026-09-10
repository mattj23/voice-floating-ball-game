namespace VoiceBallGame.Core.Dsp;

/// <summary>
/// A fixed-length window of the most recent values and their mean. Replaces the original
/// FixedListContainer, which rebuilt a List and called Average() on every frame.
/// </summary>
public sealed class MovingAverage
{
    private readonly double[] _values;
    private int _next;
    private double _sum;

    public MovingAverage(int windowSize)
    {
        if (windowSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(windowSize), windowSize, "The window must hold at least one value.");

        _values = new double[windowSize];
    }

    public int WindowSize => _values.Length;

    public int Count { get; private set; }

    /// <summary>True once the window has seen at least <see cref="WindowSize"/> values.</summary>
    public bool IsFull => Count >= _values.Length;

    /// <summary>The mean of the values currently held, or zero before anything has been added.</summary>
    public double Average { get; private set; }

    public void Add(double value)
    {
        if (IsFull)
        {
            _sum -= _values[_next];
        }
        else
        {
            Count++;
        }

        _values[_next] = value;
        _sum += value;
        _next = (_next + 1) % _values.Length;

        Average = _sum / Count;
    }

    public void Clear()
    {
        Array.Clear(_values);
        _next = 0;
        _sum = 0;
        Count = 0;
        Average = 0;
    }
}
