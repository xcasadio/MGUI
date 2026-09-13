namespace MGUI.Core.UI;

public static class ColorHdrHelper
{
    public static float GetIntensity(ColorValue value)
        => MathF.Max(value.R, MathF.Max(value.G, value.B));

    public static ColorValue GetNormalizedBaseColor(ColorValue value)
    {
        var intensity = GetIntensity(value);
        if (intensity <= 0f)
        {
            return new ColorValue(0f, 0f, 0f, value.A, value.ColorSpace, value.IsHdr);
        }

        return new ColorValue(value.R / intensity, value.G / intensity, value.B / intensity, value.A, value.ColorSpace, value.IsHdr);
    }

    public static ColorValue WithIntensity(ColorValue baseColor, float intensity)
    {
        var actualIntensity = Math.Max(0f, intensity);
        var normalized = GetNormalizedBaseColor(baseColor);
        return new ColorValue(
            normalized.R * actualIntensity,
            normalized.G * actualIntensity,
            normalized.B * actualIntensity,
            normalized.A,
            normalized.ColorSpace,
            actualIntensity > 1f || baseColor.IsHdr);
    }

    public static ColorValue ToneMapReinhard(ColorValue value)
        => new(
            ToneMapChannel(value.R),
            ToneMapChannel(value.G),
            ToneMapChannel(value.B),
            value.A,
            value.ColorSpace,
            false);

    private static float ToneMapChannel(float value)
    {
        var actual = Math.Max(0f, value);
        return actual / (1f + actual);
    }
}