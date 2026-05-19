using Microsoft.Xna.Framework;
using MGUI.Core.UI;

namespace MGUI.Tests.ColorPicker;

public class ColorPickerModelTests
{
    [Fact]
    public void SetValue_SynchronizesHsvAndTextInput()
    {
        MGColorPickerModel model = new();

        model.SetValue(new ColorValue(1f, 0.5f, 0f, 0.25f));

        Assert.Equal("#FF800040", model.TextInput.HexText);
        Assert.Equal("30", model.TextInput.HueText);
        Assert.Equal("1", model.TextInput.SaturationText);
        Assert.Equal("1", model.TextInput.ValueText);
    }

    [Fact]
    public void SetSaturationValue_UpdatesRgbValueAndPreservesAlpha()
    {
        MGColorPickerModel model = new(new ColorValue(1f, 0f, 0f, 0.5f), new ColorPickerConstraints());

        model.SetSaturationValue(1f, 1f);

        Assert.Equal(new ColorValue(1f, 0f, 0f, 0.5f), model.Value);
    }

    [Fact]
    public void SetHue_UpdatesRgbValue()
    {
        MGColorPickerModel model = new(new ColorValue(1f, 0f, 0f, 1f), new ColorPickerConstraints());

        model.SetHue(120f);

        Assert.Equal(new ColorValue(0f, 1f, 0f, 1f), model.Value);
    }

    [Fact]
    public void SetAlpha_ClampsAndUpdatesTextInput()
    {
        MGColorPickerModel model = new(new ColorValue(1f, 0f, 0f, 1f), new ColorPickerConstraints());

        model.SetAlpha(2f);

        Assert.Equal(1f, model.Value.A);
        Assert.Equal("1", model.TextInput.AlphaText);
    }

    [Fact]
    public void GetSaturationValueFromPoint_MapsPointToPercentages()
    {
        Rectangle bounds = new(10, 20, 100, 200);

        (float saturation, float value) = MGColorPicker.GetSaturationValueFromPoint(new Point(60, 70), bounds);

        AssertClose(0.5f, saturation);
        AssertClose(0.75f, value);
    }

    [Fact]
    public void GetSaturationValueColor_ReturnsHsvColor()
    {
        ColorValue color = MGColorPicker.GetSaturationValueColor(120f, 1f, 1f, ColorSpaceMode.Srgb);

        Assert.Equal(new ColorValue(0f, 1f, 0f, 1f), color);
    }

    private static void AssertClose(float expected, float actual)
        => Assert.InRange(MathF.Abs(expected - actual), 0f, 0.0001f);
}