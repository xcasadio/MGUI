using MGUI.Core.UI;

namespace MGUI.Tests.ColorPicker;

public class ColorContrastTests
{
    [Fact]
    public void RelativeLuminance_BlackAndWhiteMatchReferenceValues()
    {
        Assert.Equal(0f, ColorContrastHelper.GetRelativeLuminance(new ColorValue(0f, 0f, 0f, 1f)));
        Assert.InRange(ColorContrastHelper.GetRelativeLuminance(new ColorValue(1f, 1f, 1f, 1f)), 0.999f, 1.001f);
    }

    [Fact]
    public void ContrastRatio_BlackWhiteMeetsWcagReferenceRatio()
    {
        float ratio = ColorContrastHelper.GetContrastRatio(new ColorValue(0f, 0f, 0f, 1f), new ColorValue(1f, 1f, 1f, 1f));

        Assert.InRange(ratio, 20.99f, 21.01f);
        Assert.True(ColorContrastHelper.MeetsContrast(new ColorValue(0f, 0f, 0f, 1f), new ColorValue(1f, 1f, 1f, 1f)));
    }

    [Fact]
    public void ContrastRatio_LowContrastFailsDefaultTextThreshold()
    {
        ColorValue foreground = new(0.45f, 0.45f, 0.45f, 1f);
        ColorValue background = new(0.5f, 0.5f, 0.5f, 1f);

        Assert.False(ColorContrastHelper.MeetsContrast(foreground, background));
    }

    [Fact]
    public void RelativeLuminance_LinearInputMatchesSrgbEquivalent()
    {
        ColorValue srgb = new(0.25f, 0.5f, 0.75f, 1f, ColorSpaceMode.Srgb);
        ColorValue linear = ColorSpaceConverter.Convert(srgb, ColorSpaceMode.Linear);

        Assert.InRange(MathF.Abs(ColorContrastHelper.GetRelativeLuminance(srgb) - ColorContrastHelper.GetRelativeLuminance(linear)), 0f, 0.0001f);
    }
}
