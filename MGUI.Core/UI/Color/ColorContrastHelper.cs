namespace MGUI.Core.UI;

public static class ColorContrastHelper
{
    public const float DefaultMinimumTextContrastRatio = 4.5f;

    public static float GetRelativeLuminance(ColorValue color)
    {
        var srgb = ColorSpaceConverter.Convert(color.ClampLdr(), ColorSpaceMode.Srgb);
        var r = SrgbComponentToLinear(srgb.R);
        var g = SrgbComponentToLinear(srgb.G);
        var b = SrgbComponentToLinear(srgb.B);
        return 0.2126f * r + 0.7152f * g + 0.0722f * b;
    }

    public static float GetContrastRatio(ColorValue first, ColorValue second)
    {
        var firstLuminance = GetRelativeLuminance(first);
        var secondLuminance = GetRelativeLuminance(second);
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05f) / (darker + 0.05f);
    }

    public static bool MeetsContrast(ColorValue foreground, ColorValue background, float minimumRatio = DefaultMinimumTextContrastRatio)
        => GetContrastRatio(foreground, background) >= minimumRatio;

    private static float SrgbComponentToLinear(float component)
    {
        var value = Math.Clamp(component, 0f, 1f);
        return value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    }
}