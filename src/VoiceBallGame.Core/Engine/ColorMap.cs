using VoiceBallGame.Core.Configuration;

namespace VoiceBallGame.Core.Engine;

/// <summary>
/// Maps the ratio fraction (current loudness-to-flow ratio divided by the goal ratio) to the
/// ball's color.
/// </summary>
/// <remarks>
/// <para>
/// Each keypoint's ratio is the upper bound of the band its color covers, which is how the
/// comments in app_settings.toml describe the scale: with the shipped keypoints, everything below
/// 0.90 is blue, 0.90 to 0.95 is light blue, 0.95 to 1.05 is white, and so on. Transitions are
/// blended across a zone straddling each band boundary.
/// </para>
/// <para>
/// The original implementation blended from the previous keypoint's color <i>up to</i> each
/// keypoint's ratio and left the space between blend zones empty, so a lookup landing in the gap
/// picked up the following blend group's first entry, which carried the previous band's color. As
/// a result, every band rendered one step low: an on-target ratio of 1.0 showed light blue, and white
/// only appeared between about 1.05 and 1.10. The visual feedback therefore disagreed with
/// scoring_ratio_min/max, which count 0.95 to 1.05 as on target. This implementation matches the
/// documented bands, so the white ball and the scoring window now coincide.
/// </para>
/// </remarks>
public sealed class ColorMap
{
    private readonly ColorBand[] _bands;
    private readonly double _blendZone;
    private readonly int _blendSteps;

    private readonly record struct ColorBand(double UpperRatio, RgbColor Color);

    public ColorMap(GameSettings settings)
    {
        _blendZone = settings.ColorBlendZone;
        _blendSteps = Math.Max(1, settings.ColorBlendSteps);
        _bands = settings.BallColorScale
            .OrderBy(k => k.Ratio)
            .Select(k => new ColorBand(k.Ratio, new RgbColor(k.Rgb[0], k.Rgb[1], k.Rgb[2])))
            .ToArray();

        if (_bands.Length == 0)
            throw new ArgumentException("The color scale needs at least one keypoint.", nameof(settings));
    }

    /// <summary>The color shown when flow is outside the limits and ratio feedback is suppressed.</summary>
    public RgbColor OutOfLimitsColor { get; } = RgbColor.Gray;

    public RgbColor GetColor(double ratioFraction)
    {
        if (double.IsNaN(ratioFraction))
            return OutOfLimitsColor;

        int index = 0;
        while (index < _bands.Length - 1 && ratioFraction > _bands[index].UpperRatio)
            index++;

        var color = _bands[index].Color;

        if (_blendZone <= 0)
            return color;

        // Just above the boundary below this band: finish the blend that started in the band below.
        if (index > 0 && ratioFraction < _bands[index - 1].UpperRatio + _blendZone)
        {
            return BlendAcross(_bands[index - 1].UpperRatio, _bands[index - 1].Color, color, ratioFraction);
        }

        // Just below this band's own upper boundary: start blending toward the band above.
        if (index < _bands.Length - 1 && ratioFraction > _bands[index].UpperRatio - _blendZone)
        {
            return BlendAcross(_bands[index].UpperRatio, color, _bands[index + 1].Color, ratioFraction);
        }

        return color;
    }

    private RgbColor BlendAcross(double boundary, RgbColor below, RgbColor above, double ratioFraction)
    {
        double t = (ratioFraction - (boundary - _blendZone)) / (2 * _blendZone);

        // color_blend_steps quantizes the transition, as the settings file describes: fewer steps
        // give visibly banded transitions, more steps give a smooth one.
        t = Math.Round(Math.Clamp(t, 0.0, 1.0) * _blendSteps) / _blendSteps;

        return RgbColor.Lerp(below, above, t);
    }
}
