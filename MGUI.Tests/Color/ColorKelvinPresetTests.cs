using System.Linq;
using MGUI.Core.UI;

namespace MGUI.Tests.ColorPicker;

public class ColorKelvinPresetTests
{
    [Fact]
    public void KelvinToRgb_ProducesWarmCandleWithinDocumentedTolerance()
    {
        ColorValue color = ColorTemperatureConverter.KelvinToRgb(1900f);

        Assert.Equal(1f, color.R);
        Assert.InRange(color.G, 0.51f, 0.57f);
        Assert.Equal(0f, color.B);
    }

    [Fact]
    public void KelvinToRgb_ProducesNearWhiteDaylightWithinDocumentedTolerance()
    {
        ColorValue color = ColorTemperatureConverter.KelvinToRgb(6500f);

        Assert.InRange(color.R, 0.99f, 1f);
        Assert.InRange(color.G, 0.99f, 1f);
        Assert.InRange(color.B, 0.97f, 1f);
    }

    [Fact]
    public void KelvinToRgb_ClampsToConfiguredRange()
    {
        ColorValue below = ColorTemperatureConverter.KelvinToRgb(100f, minKelvin: 2000f, maxKelvin: 8000f);
        ColorValue min = ColorTemperatureConverter.KelvinToRgb(2000f, minKelvin: 2000f, maxKelvin: 8000f);
        ColorValue above = ColorTemperatureConverter.KelvinToRgb(20000f, minKelvin: 2000f, maxKelvin: 8000f);
        ColorValue max = ColorTemperatureConverter.KelvinToRgb(8000f, minKelvin: 2000f, maxKelvin: 8000f);

        Assert.Equal(min, below);
        Assert.Equal(max, above);
    }

    [Fact]
    public void KelvinToRgb_LinearOutputMatchesExplicitSrgbConversion()
    {
        ColorValue srgb = ColorTemperatureConverter.KelvinToRgb(4000f, ColorSpaceMode.Srgb);
        ColorValue linear = ColorTemperatureConverter.KelvinToRgb(4000f, ColorSpaceMode.Linear);

        Assert.Equal(ColorSpaceMode.Linear, linear.ColorSpace);
        Assert.Equal(ColorSpaceConverter.Convert(srgb, ColorSpaceMode.Linear), linear);
    }

    [Fact]
    public void SetTemperatureKelvin_UsesDisplaySpaceAndPreservesAlpha()
    {
        MGColorPickerModel model = new(new ColorValue(0.1f, 0.2f, 0.3f, 0.25f, ColorSpaceMode.Linear), new ColorPickerConstraints())
        {
            DisplayColorSpace = ColorSpaceMode.Srgb,
        };

        model.SetTemperatureKelvin(6500f);

        Assert.Equal(ColorSpaceMode.Linear, model.Value.ColorSpace);
        Assert.Equal(0.25f, model.Value.A);
        ColorValue display = model.DisplayValue;
        Assert.InRange(display.R, 0.99f, 1f);
        Assert.InRange(display.G, 0.99f, 1f);
        Assert.InRange(display.B, 0.97f, 1f);
    }

    [Fact]
    public void EnginePresets_ExposeExpectedCategoriesAndTemperatureNames()
    {
        Assert.Contains(MGColorEnginePresets.Defaults, x => x.Category == MGColorPresetCategory.Material);
        Assert.Contains(MGColorEnginePresets.Defaults, x => x.Category == MGColorPresetCategory.Emissive && x.Value.IsHdr);
        Assert.Contains(MGColorEnginePresets.Defaults, x => x.Category == MGColorPresetCategory.Light);
        Assert.Contains(MGColorEnginePresets.Defaults, x => x.Category == MGColorPresetCategory.Fog);
        Assert.Contains(MGColorEnginePresets.Defaults, x => x.Category == MGColorPresetCategory.Sky);
        Assert.Contains(MGColorEnginePresets.Defaults, x => x.Category == MGColorPresetCategory.UITheme);
        Assert.Contains(MGColorEnginePresets.Defaults, x => x.Category == MGColorPresetCategory.DebugGizmo);

        string[] names = MGColorEnginePresets.TemperaturePresets.Select(x => x.Name).ToArray();
        Assert.Contains("Candle", names);
        Assert.Contains("Tungsten", names);
        Assert.Contains("Warm White", names);
        Assert.Contains("Neutral White", names);
        Assert.Contains("Daylight", names);
        Assert.Contains("Overcast", names);
        Assert.Contains("Blue Sky", names);
    }

    [Fact]
    public void CreatePalette_CanFilterPresetsAndPreservesMetadata()
    {
        MGColorPalette palette = MGColorEnginePresets.CreatePalette("Lights", MGColorPresetCategory.Light);

        Assert.NotEmpty(palette.Swatches);
        Assert.All(palette.Swatches, swatch => Assert.Equal(nameof(MGColorPresetCategory.Light), swatch.Metadata["Category"]));
        Assert.All(palette.Swatches, swatch => Assert.True(swatch.Metadata.ContainsKey("ColorSpace")));
    }
}
