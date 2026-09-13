namespace MGUI.Core.UI;

public static class ColorContrastHelper
{
    public const float DefaultMinimumTextContrastRatio = 4.5f;

    public static float GetRelativeLuminance(ColorValue color)
    {
        ColorValue srgb = ColorSpaceConverter.Convert(color.ClampLdr(), ColorSpaceMode.Srgb);
        float r = SrgbComponentToLinear(srgb.R);
        float g = SrgbComponentToLinear(srgb.G);
        float b = SrgbComponentToLinear(srgb.B);
        return 0.2126f * r + 0.7152f * g + 0.0722f * b;
    }

    public static float GetContrastRatio(ColorValue first, ColorValue second)
    {
        float firstLuminance = GetRelativeLuminance(first);
        float secondLuminance = GetRelativeLuminance(second);
        float lighter = Math.Max(firstLuminance, secondLuminance);
        float darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05f) / (darker + 0.05f);
    }

    public static bool MeetsContrast(ColorValue foreground, ColorValue background, float minimumRatio = DefaultMinimumTextContrastRatio)
        => GetContrastRatio(foreground, background) >= minimumRatio;

    private static float SrgbComponentToLinear(float component)
    {
        float value = Math.Clamp(component, 0f, 1f);
        return value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    }
}