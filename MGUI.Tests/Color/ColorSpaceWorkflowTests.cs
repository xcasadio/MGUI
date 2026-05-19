using MGUI.Core.UI;

namespace MGUI.Tests.ColorPicker;

public class ColorSpaceWorkflowTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void SrgbLinearRoundTrip_PreservesAlpha(float channel)
    {
        ColorValue srgb = new(channel, channel, channel, 0.25f, ColorSpaceMode.Srgb);

        ColorValue linear = ColorSpaceConverter.Convert(srgb, ColorSpaceMode.Linear);
        ColorValue roundTrip = ColorSpaceConverter.Convert(linear, ColorSpaceMode.Srgb);

        Assert.Equal(0.25f, linear.A);
        Assert.Equal(0.25f, roundTrip.A);
        AssertClose(srgb.R, roundTrip.R);
        AssertClose(srgb.G, roundTrip.G);
        AssertClose(srgb.B, roundTrip.B);
    }

    [Fact]
    public void ChangingDisplayColorSpace_DoesNotChangeStoredValue()
    {
        ColorValue storedLinear = ColorSpaceConverter.Convert(new ColorValue(0.5f, 0.5f, 0.5f, 0.75f), ColorSpaceMode.Linear);
        MGColorPickerModel model = new(storedLinear, new ColorPickerConstraints())
        {
            DisplayColorSpace = ColorSpaceMode.Srgb,
        };

        ColorValue before = model.Value;
        model.DisplayColorSpace = ColorSpaceMode.Linear;
        model.DisplayColorSpace = ColorSpaceMode.Srgb;

        Assert.Equal(before, model.Value);
        Assert.Equal(ColorSpaceMode.Linear, model.StorageColorSpace);
        Assert.True(model.IsDisplayDifferentFromStorage);
    }

    [Fact]
    public void PreviewDisplayValue_ConvertsToStorageSpaceOnce()
    {
        ColorValue initialLinear = ColorSpaceConverter.Convert(new ColorValue(0.25f, 0.25f, 0.25f, 1f), ColorSpaceMode.Linear);
        MGColorPickerModel model = new(initialLinear, new ColorPickerConstraints())
        {
            DisplayColorSpace = ColorSpaceMode.Srgb,
            CommitMode = ColorEditCommitMode.OnMouseRelease,
        };
        ColorValue displayValue = new(0.5f, 0.5f, 0.5f, 0.6f, ColorSpaceMode.Srgb);
        ColorValue expectedStored = ColorSpaceConverter.Convert(displayValue, ColorSpaceMode.Linear);

        model.BeginEdit();
        model.PreviewDisplayValue(displayValue);
        model.CommitEdit();

        AssertClose(expectedStored.R, model.Value.R);
        AssertClose(expectedStored.G, model.Value.G);
        AssertClose(expectedStored.B, model.Value.B);
        Assert.Equal(0.6f, model.Value.A);
        Assert.Equal(ColorSpaceMode.Linear, model.Value.ColorSpace);
        Assert.Equal("#80808099 [Srgb]", model.GetDisplayText(ColorValueFormat.HexRgba));
    }

    [Fact]
    public void StoreAsLinear_ConvertsStoredValueAndPreservesDisplayedColor()
    {
        MGColorPickerModel model = new(new ColorValue(0.5f, 0.5f, 0.5f, 0.4f), new ColorPickerConstraints())
        {
            DisplayColorSpace = ColorSpaceMode.Srgb,
        };

        model.StoreAsLinear = true;

        Assert.Equal(ColorSpaceMode.Linear, model.Value.ColorSpace);
        AssertClose(0.5f, model.DisplayValue.R);
        AssertClose(0.5f, model.DisplayValue.G);
        AssertClose(0.5f, model.DisplayValue.B);
        Assert.Equal(0.4f, model.DisplayValue.A);
    }

    private static void AssertClose(float expected, float actual)
        => Assert.InRange(MathF.Abs(expected - actual), 0f, 0.0001f);
}
