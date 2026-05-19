using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.ColorPicker;

public class ColorSliderTests
{
    [Fact]
    public void PercentValueMapping_ClampsToRange()
    {
        Assert.Equal(0f, MGColorSlider.GetPercentFromValue(-1f, 0f, 10f));
        Assert.Equal(0.5f, MGColorSlider.GetPercentFromValue(5f, 0f, 10f));
        Assert.Equal(1f, MGColorSlider.GetPercentFromValue(12f, 0f, 10f));
        Assert.Equal(5f, MGColorSlider.GetValueFromPercent(0.5f, 0f, 10f));
    }

    [Fact]
    public void PointMapping_UsesOrientation()
    {
        Rectangle bounds = new(10, 20, 100, 200);

        Assert.Equal(0.5f, MGColorSlider.GetPercentFromPoint(new Point(60, 0), bounds, Orientation.Horizontal));
        Assert.Equal(0.25f, MGColorSlider.GetPercentFromPoint(new Point(0, 70), bounds, Orientation.Vertical));
    }

    [Fact]
    public void GradientColor_HueProducesExpectedPrimaryColors()
    {
        ColorValue baseColor = new(1f, 1f, 1f, 1f);

        Assert.Equal(new ColorValue(1f, 0f, 0f, 1f), MGColorSlider.GetGradientColor(ColorSliderChannel.Hue, 0f, baseColor));
        Assert.Equal(new ColorValue(0f, 1f, 0f, 1f), MGColorSlider.GetGradientColor(ColorSliderChannel.Hue, 1f / 3f, baseColor));
        Assert.Equal(new ColorValue(0f, 0f, 1f, 1f), MGColorSlider.GetGradientColor(ColorSliderChannel.Hue, 2f / 3f, baseColor));
    }

    [Fact]
    public void GradientColor_ChannelSlidersModifyOnlyTargetChannel()
    {
        ColorValue baseColor = new(0.1f, 0.2f, 0.3f, 0.4f, ColorSpaceMode.Linear);

        Assert.Equal(new ColorValue(0.75f, 0.2f, 0.3f, 0.4f, ColorSpaceMode.Linear), MGColorSlider.GetGradientColor(ColorSliderChannel.Red, 0.75f, baseColor));
        Assert.Equal(new ColorValue(0.1f, 0.75f, 0.3f, 0.4f, ColorSpaceMode.Linear), MGColorSlider.GetGradientColor(ColorSliderChannel.Green, 0.75f, baseColor));
        Assert.Equal(new ColorValue(0.1f, 0.2f, 0.75f, 0.4f, ColorSpaceMode.Linear), MGColorSlider.GetGradientColor(ColorSliderChannel.Blue, 0.75f, baseColor));
        Assert.Equal(baseColor.WithAlpha(0.75f), MGColorSlider.GetGradientColor(ColorSliderChannel.Alpha, 0.75f, baseColor));
    }
}