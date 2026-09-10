using VoiceBallGame.Core.Dsp;

namespace VoiceBallGame.Core.Tests;

public class MovingAverageTests
{
    [Fact]
    public void AveragesOnlyWhatItHasSeen_BeforeTheWindowFills()
    {
        var average = new MovingAverage(4);

        average.Add(10);
        Assert.Equal(10, average.Average, 9);
        Assert.False(average.IsFull);

        average.Add(20);
        Assert.Equal(15, average.Average, 9);
        Assert.Equal(2, average.Count);
    }

    [Fact]
    public void DropsTheOldestValue_OnceFull()
    {
        var average = new MovingAverage(3);

        foreach (var value in new double[] { 1, 2, 3 })
            average.Add(value);

        Assert.True(average.IsFull);
        Assert.Equal(2, average.Average, 9);

        average.Add(4);
        Assert.Equal(3, average.Average, 9);

        average.Add(5);
        Assert.Equal(4, average.Average, 9);
    }

    [Fact]
    public void DoesNotAccumulateDrift_OverManyValues()
    {
        var average = new MovingAverage(5);

        for (int i = 0; i < 10_000; i++)
            average.Add(i % 2 == 0 ? 1.0 : 3.0);

        // The running sum is updated incrementally, so this checks it has not drifted away from
        // the true mean of the five values still held: those from i = 9995 to 9999.
        var expected = Enumerable.Range(9995, 5).Select(i => i % 2 == 0 ? 1.0 : 3.0).Average();
        Assert.Equal(expected, average.Average, 9);
    }

    [Fact]
    public void ClearResetsEverything()
    {
        var average = new MovingAverage(2);
        average.Add(5);
        average.Add(7);

        average.Clear();

        Assert.Equal(0, average.Average);
        Assert.Equal(0, average.Count);
        Assert.False(average.IsFull);
    }

    [Fact]
    public void RejectsAnEmptyWindow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovingAverage(0));
    }
}
