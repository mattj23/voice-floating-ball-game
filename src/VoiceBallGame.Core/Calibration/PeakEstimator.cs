namespace VoiceBallGame.Core.Calibration;

/// <summary>
/// Finds the level at which a recording settled by locating clusters in its values.
/// </summary>
/// <remarks>
/// During calibration the operator holds a steady tone while the level is recorded. The recording
/// also contains the quiet periods before and after the tone and the transitions between them.
/// Therefore, the mean of the full recording does not represent the held level. The estimator bins
/// the values, selects the densest region, and refines the result by averaging nearby samples.
///
/// This is the estimator from the original's calibration screen, which located three held levels
/// this way. Only one is needed now, so the same machinery reports the strongest cluster.
///
/// A cluster is recognized only if it reaches half the density of the densest cluster. A level held
/// far more briefly than another will therefore not be reported. This behavior suits the single
/// held level that the calibration process requests.
/// </remarks>
public static class PeakEstimator
{
    /// <param name="values">The recorded levels.</param>
    /// <param name="bins">How finely the range is divided when looking for clusters.</param>
    /// <returns>The clusters found, strongest first.</returns>
    public static IReadOnlyList<double> FindLevels(IReadOnlyList<double> values, int bins = 100)
    {
        if (values.Count == 0) return [];

        double max = values.Max();
        if (max <= 0) return [];

        // Bin centers run from zero to the largest value inclusive, so a recording that sits at a
        // single steady level lands on the last center rather than past the end of the array.
        double binWidth = max / (bins - 1);
        var counts = new int[bins];

        foreach (var value in values)
        {
            // Each value counts toward every bin whose center is within one bin width, which
            // smooths the histogram so a cluster is not split across two adjacent bins.
            int center = (int)Math.Round(value / binWidth);
            for (int i = Math.Max(0, center - 1); i <= Math.Min(bins - 1, center + 1); i++)
            {
                if (Math.Abs(value - i * binWidth) < binWidth) counts[i]++;
            }
        }

        int threshold = counts.Max() / 2;
        var peaks = new List<(double Center, int Weight)>();

        bool inside = false;
        double risingEdge = 0;
        int weight = 0;

        for (int i = 0; i < bins; i++)
        {
            double center = i * binWidth;

            if (!inside && counts[i] > threshold)
            {
                inside = true;
                risingEdge = center;
                weight = 0;
            }

            if (inside) weight += counts[i];

            if (inside && counts[i] <= threshold)
            {
                peaks.Add(((risingEdge + center) / 2.0, weight));
                inside = false;
            }
        }

        if (inside) peaks.Add(((risingEdge + max) / 2.0, weight));

        return peaks
            .OrderByDescending(p => p.Weight)
            .Select(p => Refine(values, p.Center, binWidth * 2))
            .ToList();
    }

    /// <summary>The strongest level in the recording, or null if there is nothing to find.</summary>
    public static double? FindLevel(IReadOnlyList<double> values, int bins = 100) =>
        FindLevels(values, bins) is { Count: > 0 } levels ? levels[0] : null;

    private static double Refine(IReadOnlyList<double> values, double peak, double window)
    {
        var nearby = values.Where(v => Math.Abs(peak - v) < window).ToList();
        return nearby.Count > 0 ? nearby.Average() : peak;
    }
}
