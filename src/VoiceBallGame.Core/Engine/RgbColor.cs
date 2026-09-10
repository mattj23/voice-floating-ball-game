namespace VoiceBallGame.Core.Engine;

/// <summary>
/// A color with components in the range 0 to 1. Core deliberately has no UI dependency, so the
/// view layer converts this to whatever Avalonia brush it needs.
/// </summary>
public readonly record struct RgbColor(double R, double G, double B)
{
    public static readonly RgbColor Gray = new(0.5, 0.5, 0.5);

    public byte R8 => ToByte(R);
    public byte G8 => ToByte(G);
    public byte B8 => ToByte(B);

    public static RgbColor Lerp(RgbColor from, RgbColor to, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return new RgbColor(
            from.R + (to.R - from.R) * t,
            from.G + (to.G - from.G) * t,
            from.B + (to.B - from.B) * t);
    }

    private static byte ToByte(double component) =>
        (byte)Math.Clamp(Math.Round(component * 255.0), 0, 255);
}
