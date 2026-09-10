using VoiceBallGame.Core.Dsp;

namespace VoiceBallGame.Core.Tests;

public class SignalMathTests
{
    [Fact]
    public void RmsOfAConstantSignal_IsThatConstant()
    {
        var samples = Enumerable.Repeat(0.5f, 128).ToArray();
        Assert.Equal(0.5, SignalMath.Rms(samples), 9);
    }

    [Fact]
    public void RmsOfASineWave_IsTheAmplitudeOverRootTwo()
    {
        const int sampleCount = 4096;
        const int cycles = 16;
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
            samples[i] = (float)Math.Sin(2 * Math.PI * cycles * i / sampleCount);

        Assert.Equal(1.0 / Math.Sqrt(2), SignalMath.Rms(samples), 4);
    }

    [Fact]
    public void RmsIsIndependentOfBufferLength()
    {
        // This is the property the original lacked: it summed the squares without dividing by the
        // sample count, so doubling buffer_ms scaled every level by the square root of two and
        // silently invalidated the microphone calibration.
        var shortBuffer = Enumerable.Repeat(0.25f, 256).ToArray();
        var longBuffer = Enumerable.Repeat(0.25f, 1024).ToArray();

        Assert.Equal(SignalMath.Rms(shortBuffer), SignalMath.Rms(longBuffer), 9);

        Assert.NotEqual(
            Math.Round(SignalMath.LegacyRms(shortBuffer), 6),
            Math.Round(SignalMath.LegacyRms(longBuffer), 6));
    }

    [Fact]
    public void RmsOfNothingIsZero()
    {
        Assert.Equal(0.0, SignalMath.Rms([]));
    }
}
