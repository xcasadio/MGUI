# MGUI ColorPicker Usage Guide

This guide covers the implemented color editor API: compact fields, the visual picker, PropertyGrid integration, XAML, palettes, color spaces, HDR, eyedropper services, and migration from `MGGridColorPicker`.

## Core Types

- `ColorValue`: float RGBA value with `ColorSpaceMode.Srgb` or `ColorSpaceMode.Linear` and HDR metadata.
- `MGColorField`: compact field intended for forms and PropertyGrid editors.
- `MGColorPicker`: visual HSV picker with alpha, text input strip, HDR intensity, Kelvin, previews, and keyboard navigation.
- `MGColorPickerPopup`: popup host used by `MGColorField`.
- `MGColorPaletteView`: modern swatch palette view backed by `MGColorPalette` and `MGColorSwatch`.
- `MGColorPreview`: standalone current/previous preview.
- `MGColorSlider`: reusable color-aware slider.

## XAML Quick Start

```xml
<ColorField Value="#3366CCFF"
            DefaultValue="#3366CCFF"
            ShowAlpha="True"
            ShowTextInput="True"
            DisplayFormat="HexRgba"
            CommitMode="ExplicitOkCancel" />
```

```xml
<ColorPicker Value="#3366CCFF"
             PreviousValue="#CC6633FF"
             ShowAlpha="True"
             ShowTextInput="True"
             ShowLightDarkPreview="True"
             ShowContrastWarning="True"
             ContrastTextColor="#FFFFFFFF"
             DisplayFormat="HexRgba"
             CommitMode="Live" />
```

```xml
<ColorPaletteView PaletteName="Project"
                  Columns="8"
                  SwatchSize="22"
                  Spacing="4"
                  CommaSeparatedColors="#1B263BFF,#EF476FFF,#FFD166FF,#06D6A0FF" />
```

## C# Quick Start

```csharp
MGColorField field = new(window, new ColorValue(0.2f, 0.4f, 0.8f, 1f), new ColorPickerOptions
{
    ShowAlpha = true,
    ShowTextInput = true,
    DisplayFormat = ColorValueFormat.HexRgba,
    CommitMode = ColorEditCommitMode.ExplicitOkCancel,
});

field.ValueChanged += (sender, e) => ApplyColor(e.NewValue);
```

```csharp
MGColorPicker picker = new(window, new ColorPickerOptions
{
    InitialValue = new ColorValue(1f, 0.8f, 0.35f, 1f),
    ShowAlpha = true,
    ShowTextInput = true,
    ShowTemperature = true,
    ShowLightDarkPreview = true,
    ShowContrastWarning = true,
    ContrastTextColor = new ColorValue(1f, 1f, 1f, 1f),
});

picker.ValueChanging += (sender, e) => PreviewColor(e.PreviewValue);
picker.ValueChanged += (sender, e) => CommitColor(e.NewValue);
```

## PropertyGrid

`ColorValue`, `Microsoft.Xna.Framework.Color`, `Vector3`, `Vector4`, `System.Numerics.Vector3`, and `System.Numerics.Vector4` are supported by the color editor workflow added for PropertyGrid.

Use normal properties on your inspected object:

```csharp
public sealed class MaterialSettings
{
    public ColorValue BaseColor { get; set; } = new(0.8f, 0.8f, 0.8f, 1f);
    public Microsoft.Xna.Framework.Color DebugColor { get; set; } = Microsoft.Xna.Framework.Color.CornflowerBlue;
    public System.Numerics.Vector4 EmissiveColor { get; set; } = new(1f, 0.6f, 0.2f, 1f);
}
```

Then set the object as usual:

```csharp
propertyGrid.SelectedObject = new MaterialSettings();
```

## Text Formats

The parser supports:

- `#RRGGBB`
- `#RRGGBBAA` by default
- `#AARRGGBB` when `ColorValueFormat.HexArgb` is selected
- `rgb(255, 128, 0)`
- `rgba(255, 128, 0, 0.5)`
- `Vector3(1, 0.5, 0)`
- `Vector4(1, 0.5, 0, 1)`

The important ambiguity is 8-digit hex:

- `HexRgba` means `#RRGGBBAA`.
- `HexArgb` means `#AARRGGBB`.

Prefer setting `DisplayFormat` explicitly when exchanging text with external tools.

## Commit Modes

- `Live`: preview and commit are effectively immediate.
- `OnMouseRelease`: drag previews update continuously, then commit on release.
- `ExplicitOkCancel`: popup or picker edit stays pending until OK/Enter commits or Cancel/Escape restores.

Undo/redo is intentionally host-driven. Inject `IColorEditTransaction` through `ColorPickerOptions.EditTransaction` when an editor wants color edits to participate in its command stack.

## sRGB, Linear, and HDR

`ColorValue.ColorSpace` stores whether a value is sRGB or Linear. `ColorPickerOptions.StorageColorSpace` controls stored values, while `DisplayColorSpace` controls editing/display math.

HDR is opt-in:

```csharp
ColorPickerOptions options = new()
{
    IsHdr = true,
    ShowIntensity = true,
    UseExposureSlider = true,
    ShowToneMappedPreview = true,
    MinIntensity = 0f,
    MaxIntensity = 16f,
};
```

HDR values above `1.0` are preserved when `AllowHdr`/`IsHdr` is enabled. `ToXnaColor()` is still an LDR conversion and clamps for rendering; it does not replace the stored HDR value.

## Kelvin and Engine Presets

Enable Kelvin editing on the picker with:

```csharp
new ColorPickerOptions
{
    ShowTemperature = true,
    MinKelvin = 1000f,
    MaxKelvin = 12000f,
};
```

Engine presets are opt-in and can be loaded into a palette view:

```csharp
paletteView.SetEnginePresets();
paletteView.BindPicker(picker);
```

Use `MGColorEnginePresets.CreatePalette("Lights", MGColorPresetCategory.Light)` to load one category only.

## Palette Persistence

`MGColorPaletteSerializer` serializes palettes to JSON without using the filesystem.

```csharp
string json = MGColorPaletteSerializer.ToJson(palette);
MGColorPaletteSerializationResult result = MGColorPaletteSerializer.FromJson(json);

if (result.Success)
{
    MGColorPalette imported = result.Palette;
}
```

The JSON format stores palette name, swatch name, RGBA float value, color space, HDR flag, and string metadata. Invalid JSON returns diagnostics instead of crashing. Invalid swatches are skipped with diagnostics.

## Eyedropper Service

`MGUI.Core` defines the contract but does not own platform or viewport sampling. Provide an `IColorPickService` implementation from the host editor:

```csharp
public sealed class EditorColorPickService : IColorPickService
{
    public bool IsSupported => true;
    public event EventHandler<ColorPickedEventArgs> ColorPicked;
    public event EventHandler ColorPickCancelled;

    public bool BeginPick(ColorPickRequest request)
    {
        StartViewportPick(request);
        return true;
    }

    private void Complete(ColorValue value)
        => ColorPicked?.Invoke(this, new ColorPickedEventArgs(value));
}
```

Assign it through `ColorPickerOptions.ColorPickService`, `MGColorPicker.ColorPickService`, or `MGColorField.ColorPickService`.

## Migration From MGGridColorPicker

`MGGridColorPicker` remains public and compatible. Existing XAML and C# usages do not need to change.

Use `MGColorPaletteView` for new code when you need named swatches, recent/favorites/project palettes, JSON persistence, palette-to-picker binding, or `ColorValue` support.

Old style:

```xml
<GridColorPicker Columns="8" CommaSeparatedColors="Black,White,Red,Green,Blue" />
```

New style:

```xml
<ColorPaletteView PaletteName="Project"
                  Columns="8"
                  CommaSeparatedColors="#000000FF,#FFFFFFFF,#FF0000FF,#00FF00FF,#0000FFFF" />
```

When migrating, keep existing `MGGridColorPicker` screens intact and introduce `MGColorPaletteView` only in the new color editor surfaces. This avoids changing legacy selected-index behavior or byte-based `Microsoft.Xna.Framework.Color` assumptions.

## Known Limits

- HSL and color wheel UI modes are not fully implemented in the visual picker.
- Color blindness simulation, gradient/ramp editing, material sphere previews, and `.gpl`/`.mgpalette` formats are documented in the V3 backlog.
- Checkerboard rendering is still rectangle-based in the current draw layer.
- Text input strip rendering is minimal; `MGColorPickerModel.GetQuickInfoText()` provides compact inspection text for hosts that want a separate label.
