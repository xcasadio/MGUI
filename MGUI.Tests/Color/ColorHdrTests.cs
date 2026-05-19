using MGUI.Core.UI;

namespace MGUI.Tests.ColorPicker;

public class ColorHdrTests
{
    [Fact]
    public void HdrConstraints_PreserveValuesAboveOneWhenAllowed()
    {
        ColorPickerConstraints constraints = new()
        {
            AllowHdr = true,
            MaxChannelValue = 16f,
        };
        MGColorPickerModel model = new(new ColorValue(4f, 2f, 1f, 1f), constraints);

        Assert.Equal(new ColorValue(4f, 2f, 1f, 1f, ColorSpaceMode.Srgb, true), model.Value);
        Assert.True(model.Value.IsHdr);
    }

    [Fact]
    public void LdrConstraints_StillClampValuesAboveOne()
    {
        MGColorPickerModel model = new(new ColorValue(4f, 2f, 1f, 0.5f), new ColorPickerConstraints());

        Assert.Equal(new ColorValue(1f, 1f, 1f, 0.5f), model.Value);
        Assert.False(model.Value.IsHdr);
    }

    [Fact]
    public void NormalizeBaseColorAndIntensity_PreserveRatio()
    {
        ColorValue value = new(4f, 2f, 1f, 1f, ColorSpaceMode.Srgb, true);

        ColorValue baseColor = ColorHdrHelper.GetNormalizedBaseColor(value);
        ColorValue rebuilt = ColorHdrHelper.WithIntensity(baseColor, 4f);

        Assert.Equal(4f, ColorHdrHelper.GetIntensity(value));
        Assert.Equal(new ColorValue(1f, 0.5f, 0.25f, 1f, ColorSpaceMode.Srgb, true), baseColor);
        Assert.Equal(value, rebuilt);
    }

    [Fact]
    public void SetIntensity_ClampsToConfiguredRangeAndPreservesAlpha()
    {
        ColorPickerConstraints constraints = new()
        {
            AllowHdr = true,
            MaxChannelValue = 16f,
            MinIntensity = 0.5f,
            MaxIntensity = 4f,
        };
        MGColorPickerModel model = new(new ColorValue(2f, 1f, 0.5f, 0.25f, ColorSpaceMode.Srgb, true), constraints);

        model.SetIntensity(10f);

        Assert.Equal(4f, ColorHdrHelper.GetIntensity(model.Value));
        Assert.Equal(0.25f, model.Value.A);
        Assert.Equal(new ColorValue(4f, 2f, 1f, 0.25f, ColorSpaceMode.Srgb, true), model.Value);
    }

    [Fact]
    public void ToneMappedPreview_DoesNotReplaceStoredHdrValue()
    {
        ColorValue hdr = new(4f, 2f, 1f, 1f, ColorSpaceMode.Srgb, true);
        MGColorPickerModel model = new(hdr, new ColorPickerConstraints { AllowHdr = true, MaxChannelValue = 16f });

        ColorValue preview = model.GetToneMappedPreview();

        Assert.Equal(hdr, model.Value);
        Assert.InRange(preview.R, 0.79f, 0.81f);
        Assert.InRange(preview.G, 0.66f, 0.67f);
        Assert.Equal(0.5f, preview.B);
        Assert.False(preview.IsHdr);
    }
}
