using VoiceBallGame.Core.Configuration;
using VoiceBallGame.Core.Engine;

namespace VoiceBallGame.Core.Tests;

public class ColorMapTests
{
    private static readonly RgbColor Blue = new(0, 0, 1);
    private static readonly RgbColor LightBlue = new(0.5, 0.5, 1);
    private static readonly RgbColor White = new(1, 1, 1);
    private static readonly RgbColor LightRed = new(1, 0.5, 0.5);
    private static readonly RgbColor Red = new(1, 0, 0);

    private static ColorMap ShippedScale(double blendZone = 0.02) =>
        new(new GameSettings { ColorBlendZone = blendZone, ColorBlendSteps = 5 });

    [Theory]
    // The bands the settings file documents: below 0.90 blue, 0.90-0.95 light blue,
    // 0.95-1.05 white, 1.05-1.10 light red, above that red.
    [InlineData(0.50)]
    [InlineData(0.85)]
    public void BelowTheFirstKeypoint_IsBlue(double ratio) =>
        AssertColor(Blue, ShippedScale().GetColor(ratio));

    [Theory]
    [InlineData(0.925)]
    public void BetweenTheFirstTwoKeypoints_IsLightBlue(double ratio) =>
        AssertColor(LightBlue, ShippedScale().GetColor(ratio));

    [Theory]
    [InlineData(0.98)]
    [InlineData(1.00)]
    [InlineData(1.02)]
    public void OnTarget_IsWhite(double ratio) =>
        AssertColor(White, ShippedScale().GetColor(ratio));

    [Theory]
    [InlineData(1.075)]
    public void JustAboveTheGoal_IsLightRed(double ratio) =>
        AssertColor(LightRed, ShippedScale().GetColor(ratio));

    [Theory]
    [InlineData(1.5)]
    [InlineData(5.0)]
    public void WellAboveTheGoal_IsRed(double ratio) =>
        AssertColor(Red, ShippedScale().GetColor(ratio));

    [Fact]
    public void TheWhiteBandMatchesTheScoringWindow()
    {
        // The point of the fix: the ball is white across the window that scoring_ratio_min to
        // scoring_ratio_max counts as being in the goal, and the transitions are centered on
        // those bounds. In the original the two disagreed by a whole band, so an on-target
        // participant saw light blue.
        var settings = new GameSettings();
        var map = new ColorMap(settings);

        AssertColor(White, map.GetColor(settings.ScoringRatioMin + settings.ColorBlendZone + 0.001));
        AssertColor(White, map.GetColor(settings.ScoringRatioMax - settings.ColorBlendZone - 0.001));

        // Each transition is centered on its scoring bound: fully the band below one blend zone
        // under it, fully the band above one blend zone over it.
        AssertColor(LightBlue, map.GetColor(settings.ScoringRatioMin - settings.ColorBlendZone));
        AssertColor(White, map.GetColor(settings.ScoringRatioMin + settings.ColorBlendZone));
        AssertColor(White, map.GetColor(settings.ScoringRatioMax - settings.ColorBlendZone));
        AssertColor(LightRed, map.GetColor(settings.ScoringRatioMax + settings.ColorBlendZone));
    }

    [Fact]
    public void ColorsBlendAcrossABandBoundary()
    {
        var map = ShippedScale();

        // Right at the 1.05 boundary the color is halfway between white and light red.
        var midpoint = map.GetColor(1.05);

        Assert.True(midpoint.R > 0.99);
        Assert.InRange(midpoint.G, 0.70, 0.80);
        Assert.InRange(midpoint.B, 0.70, 0.80);
    }

    [Fact]
    public void BlendingCanBeTurnedOff()
    {
        var map = ShippedScale(blendZone: 0.0);

        AssertColor(White, map.GetColor(1.0499));
        AssertColor(LightRed, map.GetColor(1.0501));
    }

    [Fact]
    public void ANotANumberRatioGivesTheOutOfLimitsColor()
    {
        var map = ShippedScale();
        Assert.Equal(map.OutOfLimitsColor, map.GetColor(double.NaN));
    }

    /// <summary>
    /// Documents the behavior this replaces. The original built a lookup list holding only the
    /// blend zones around each keypoint, leaving the space between them empty, so a lookup in the
    /// gap fell through to the next blend group's first entry, which carried the previous band's
    /// color. Every band therefore rendered one step low.
    /// </summary>
    [Fact]
    public void TheOriginalAlgorithmShiftedEveryBandDown()
    {
        var settings = new GameSettings();
        var legacy = BuildLegacyLookup(settings);

        // On target, the original produced the band below white.
        AssertColor(LightBlue, LegacyLookup(legacy, 1.00));

        // White only appeared above the scoring window, between roughly 1.06 and 1.08.
        AssertColor(White, LegacyLookup(legacy, 1.075));

        // The corrected map differs at the on-target values that affect participant feedback.
        AssertColor(White, new ColorMap(settings).GetColor(1.00));
    }

    private static List<(double Ratio, RgbColor Color)> BuildLegacyLookup(GameSettings settings)
    {
        var lookup = new List<(double, RgbColor)>();
        var lastColor = RgbColor.Gray;

        foreach (var keypoint in settings.BallColorScale.OrderBy(k => k.Ratio))
        {
            double start = keypoint.Ratio - settings.ColorBlendZone;
            double end = keypoint.Ratio + settings.ColorBlendZone;
            var target = new RgbColor(keypoint.Rgb[0], keypoint.Rgb[1], keypoint.Rgb[2]);

            for (int i = 0; i <= settings.ColorBlendSteps; i++)
            {
                double fraction = (double)i / settings.ColorBlendSteps;
                lookup.Add((start + fraction * (end - start), RgbColor.Lerp(lastColor, target, fraction)));
            }

            lastColor = target;
        }

        return lookup;
    }

    private static RgbColor LegacyLookup(List<(double Ratio, RgbColor Color)> lookup, double ratio)
    {
        foreach (var entry in lookup)
        {
            if (entry.Ratio > ratio) return entry.Color;
        }

        return lookup[^1].Color;
    }

    private static void AssertColor(RgbColor expected, RgbColor actual)
    {
        Assert.Equal(expected.R, actual.R, 6);
        Assert.Equal(expected.G, actual.G, 6);
        Assert.Equal(expected.B, actual.B, 6);
    }
}
