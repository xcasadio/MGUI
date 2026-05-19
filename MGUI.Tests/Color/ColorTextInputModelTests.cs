using MGUI.Core.UI;

namespace MGUI.Tests.ColorPicker;

public class ColorTextInputModelTests
{
    [Fact]
    public void SetValue_RefreshesHexRgbAndHsvText()
    {
        MGColorTextInputModel model = new(new ColorValue(1f, 0.5f, 0f, 0.25f));

        Assert.Equal("#FF800040", model.HexText);
        Assert.Equal("255", model.RedText);
        Assert.Equal("128", model.GreenText);
        Assert.Equal("0", model.BlueText);
        Assert.Equal("0.25", model.AlphaText);
        Assert.Equal("30", model.HueText);
        Assert.Equal("1", model.SaturationText);
        Assert.Equal("1", model.ValueText);
        Assert.False(model.HasValidationError);
    }

    [Fact]
    public void TrySetHexText_UpdatesValueAndSynchronizedTexts()
    {
        MGColorTextInputModel model = new(new ColorValue(0f, 0f, 0f, 1f));
        ColorValueChangedEventArgs? changed = null;
        model.ValueChanged += (sender, e) => changed = e;

        Assert.True(model.TrySetHexText("#3366CC80"));

        AssertClose(0.2f, model.Value.R);
        AssertClose(0.4f, model.Value.G);
        AssertClose(0.8f, model.Value.B);
        AssertClose(128f / 255f, model.Value.A);
        Assert.Equal("51", model.RedText);
        Assert.Equal("#3366CC80", model.HexText);
        Assert.NotNull(changed);
    }

    [Fact]
    public void TrySetRgbByteText_UpdatesHexAndHsv()
    {
        MGColorTextInputModel model = new(new ColorValue(0f, 0f, 0f, 1f));

        Assert.True(model.TrySetRgbByteText("255", "128", "0", "64"));

        Assert.Equal("#FF800040", model.HexText);
        AssertClose(30.11765f, float.Parse(model.HueText, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("1", model.SaturationText);
        Assert.Equal("1", model.ValueText);
    }

    [Fact]
    public void TrySetRgbFloatText_AllowsHdrValues()
    {
        MGColorTextInputModel model = new(new ColorValue(0f, 0f, 0f, 1f, ColorSpaceMode.Linear));

        Assert.True(model.TrySetRgbFloatText("2.5", "1.5", "0.5", "1"));

        Assert.Equal(new ColorValue(2.5f, 1.5f, 0.5f, 1f, ColorSpaceMode.Linear, true), model.Value);
        Assert.True(model.Value.IsHdr);
    }

    [Fact]
    public void TrySetHsvText_UpdatesRgbAndHex()
    {
        MGColorTextInputModel model = new(new ColorValue(0f, 0f, 0f, 1f));

        Assert.True(model.TrySetHsvText("120", "1", "1", "1"));

        Assert.Equal(new ColorValue(0f, 1f, 0f, 1f), model.Value);
        Assert.Equal("#00FF00FF", model.HexText);
    }

    [Fact]
    public void InvalidText_SetsValidationErrorAndPreservesValue()
    {
        MGColorTextInputModel model = new(new ColorValue(1f, 0f, 0f, 1f));

        Assert.False(model.TrySetHexText("#not-a-color"));

        Assert.True(model.HasValidationError);
        Assert.Equal(new ColorValue(1f, 0f, 0f, 1f), model.Value);
        Assert.Equal("#not-a-color", model.HexText);
    }

    private static void AssertClose(float expected, float actual)
        => Assert.InRange(MathF.Abs(expected - actual), 0f, 0.0001f);
}