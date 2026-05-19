using System.Collections.Generic;
using System.Linq;

namespace MGUI.Core.UI
{
    public static class MGColorEnginePresets
    {
        public static IReadOnlyList<MGColorPreset> TemperaturePresets { get; } = new[]
        {
            KelvinPreset("Candle", 1900f),
            KelvinPreset("Tungsten", 2850f),
            KelvinPreset("Warm White", 3200f),
            KelvinPreset("Neutral White", 4000f),
            KelvinPreset("Daylight", 6500f),
            KelvinPreset("Overcast", 7500f),
            KelvinPreset("Blue Sky", 10000f),
        };
        public static IReadOnlyList<MGColorPreset> Defaults { get; } = CreateDefaults();

        public static MGColorPalette CreatePalette(string name = "Engine Presets", MGColorPresetCategory? category = null)
        {
            MGColorPalette palette = new(name);
            IEnumerable<MGColorPreset> presets = category.HasValue ? Defaults.Where(x => x.Category == category.Value) : Defaults;
            foreach (MGColorPreset preset in presets)
            {
                MGColorSwatch swatch = palette.AddSwatch(preset.Name, preset.Value);
                swatch.Metadata["Category"] = preset.Category.ToString();
                swatch.Metadata["ColorSpace"] = preset.Value.ColorSpace.ToString();
                swatch.Metadata["IsHdr"] = preset.Value.IsHdr.ToString();
            }

            return palette;
        }

        private static IReadOnlyList<MGColorPreset> CreateDefaults()
        {
            List<MGColorPreset> presets = new()
            {
                new(MGColorPresetCategory.Material, "Albedo White", new ColorValue(0.8f, 0.8f, 0.8f, 1f)),
                new(MGColorPresetCategory.Material, "Albedo Charcoal", new ColorValue(0.04f, 0.04f, 0.04f, 1f)),
                new(MGColorPresetCategory.Emissive, "Emissive Cyan", new ColorValue(0f, 4f, 6f, 1f, ColorSpaceMode.Srgb, true)),
                new(MGColorPresetCategory.Emissive, "Emissive Amber", new ColorValue(6f, 3f, 0.75f, 1f, ColorSpaceMode.Srgb, true)),
                new(MGColorPresetCategory.Light, "Key Light", new ColorValue(1f, 0.92f, 0.82f, 1f)),
                new(MGColorPresetCategory.Light, "Fill Light", new ColorValue(0.55f, 0.68f, 1f, 1f)),
                new(MGColorPresetCategory.Fog, "Morning Fog", new ColorValue(0.62f, 0.68f, 0.72f, 1f)),
                new(MGColorPresetCategory.Sky, "Zenith Sky", new ColorValue(0.18f, 0.42f, 0.95f, 1f)),
                new(MGColorPresetCategory.UITheme, "UI Accent", new ColorValue(0.13f, 0.55f, 0.85f, 1f)),
                new(MGColorPresetCategory.DebugGizmo, "Gizmo X", new ColorValue(1f, 0.1f, 0.08f, 1f)),
                new(MGColorPresetCategory.DebugGizmo, "Gizmo Y", new ColorValue(0.15f, 0.9f, 0.2f, 1f)),
                new(MGColorPresetCategory.DebugGizmo, "Gizmo Z", new ColorValue(0.15f, 0.35f, 1f, 1f)),
            };
            presets.AddRange(TemperaturePresets);
            return presets;
        }

        private static MGColorPreset KelvinPreset(string name, float kelvin)
            => new(MGColorPresetCategory.Temperature, name, ColorTemperatureConverter.KelvinToRgb(kelvin));
    }
}