namespace VoiceBallGame.Core.Dsp;

public static class SignalMath
{
    /// <summary>
    /// Root mean square of a block of samples.
    /// </summary>
    /// <remarks>
    /// The original application summed the squares and took the square root without dividing by
    /// the sample count, so its "RMS" grew with the square root of the buffer length. Changing
    /// buffer_ms silently rescaled every calibration. Dividing by N here makes the level
    /// independent of buffer size. This change makes old calibration files incomparable, so the
    /// microphone must be calibrated again for the rebuilt application.
    /// </remarks>
    public static double Rms(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0) return 0.0;

        double sumOfSquares = 0;
        foreach (var sample in samples)
            sumOfSquares += (double)sample * sample;

        return Math.Sqrt(sumOfSquares / samples.Length);
    }

    /// <summary>
    /// The same measure the original computed, kept so recordings made against the old
    /// calibration can still be interpreted.
    /// </summary>
    public static double LegacyRms(ReadOnlySpan<float> samples)
    {
        double sumOfSquares = 0;
        foreach (var sample in samples)
            sumOfSquares += (double)sample * sample;

        return Math.Sqrt(sumOfSquares);
    }
}
