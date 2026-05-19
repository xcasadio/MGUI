using Microsoft.Xna.Framework;
using MGUI.Core.UI;

namespace MGUI.Tests.ColorPicker;

public class ColorPaletteTests
{
    [Fact]
    public void Palette_AddMoveAndRemoveSwatches()
    {
        MGColorPalette palette = new("Project");
        MGColorSwatch red = palette.AddSwatch("Red", new ColorValue(1f, 0f, 0f, 1f));
        MGColorSwatch green = palette.AddSwatch("Green", new ColorValue(0f, 1f, 0f, 1f));

        Assert.True(palette.MoveSwatch(1, 0));
        Assert.Same(green, palette.Swatches[0]);
        Assert.True(palette.RemoveSwatch(red));
        Assert.DoesNotContain(red, palette.Swatches);
    }

    [Fact]
    public void PaletteStore_AddRecentMovesDuplicatesAndTrims()
    {
        MGColorPaletteStore store = new() { MaxRecentColors = 2 };
        ColorValue red = new(1f, 0f, 0f, 1f);
        ColorValue green = new(0f, 1f, 0f, 1f);
        ColorValue blue = new(0f, 0f, 1f, 1f);

        store.AddRecent(red, "Red");
        store.AddRecent(green, "Green");
        store.AddRecent(red, "Red again");
        store.AddRecent(blue, "Blue");

        Assert.Equal(2, store.RecentColors.Swatches.Count);
        Assert.Equal(blue, store.RecentColors.Swatches[0].Value);
        Assert.Equal(red, store.RecentColors.Swatches[1].Value);
    }

    [Fact]
    public void PaletteView_GetSwatchIndexFromPoint_MapsGridCell()
    {
        Rectangle bounds = new(10, 10, 100, 100);

        int? index = MGColorPaletteView.GetSwatchIndexFromPoint(new Point(35, 12), bounds, 8, 4, 18, 4, 1);

        Assert.Equal(1, index);
    }

    [Fact]
    public void PaletteView_GetSwatchIndexFromPoint_ReturnsNullForSpacing()
    {
        Rectangle bounds = new(10, 10, 100, 100);

        int? index = MGColorPaletteView.GetSwatchIndexFromPoint(new Point(30, 12), bounds, 8, 4, 18, 4, 1);

        Assert.Null(index);
    }

    [Fact]
    public void PaletteView_GetNavigationIndex_MapsDirectionalActions()
    {
        Assert.Equal(4, MGColorPaletteView.GetNavigationIndex(1, 10, 3, UINavigationAction.MoveDown));
        Assert.Equal(1, MGColorPaletteView.GetNavigationIndex(4, 10, 3, UINavigationAction.MoveUp));
        Assert.Equal(9, MGColorPaletteView.GetNavigationIndex(4, 10, 3, UINavigationAction.End));
        Assert.Equal(0, MGColorPaletteView.GetNavigationIndex(4, 10, 3, UINavigationAction.Home));
    }

    [Fact]
    public void PaletteSerializer_RoundTripPreservesNamesValuesSpacesAndMetadata()
    {
        MGColorPalette palette = new("Project");
        palette.AddSwatch("Albedo", new ColorValue(0.25f, 0.5f, 0.75f, 1f, ColorSpaceMode.Srgb));
        MGColorSwatch emissive = palette.AddSwatch("Emissive", new ColorValue(3f, 1.5f, 0.25f, 0.8f, ColorSpaceMode.Linear, true));
        emissive.Metadata["Usage"] = "Light";

        string json = MGColorPaletteSerializer.ToJson(palette);
        MGColorPaletteSerializationResult result = MGColorPaletteSerializer.FromJson(json);

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("Project", result.Palette.Name);
        Assert.Equal(2, result.Palette.Swatches.Count);
        Assert.Equal("Albedo", result.Palette.Swatches[0].Name);
        Assert.Equal(new ColorValue(0.25f, 0.5f, 0.75f, 1f, ColorSpaceMode.Srgb), result.Palette.Swatches[0].Value);
        Assert.Equal("Emissive", result.Palette.Swatches[1].Name);
        Assert.Equal(new ColorValue(3f, 1.5f, 0.25f, 0.8f, ColorSpaceMode.Linear, true), result.Palette.Swatches[1].Value);
        Assert.Equal("Light", result.Palette.Swatches[1].Metadata["Usage"]);
    }

    [Fact]
    public void PaletteSerializer_InvalidInputDoesNotCrash()
    {
        MGColorPaletteSerializationResult result = MGColorPaletteSerializer.FromJson("{ not json");

        Assert.False(result.Success);
        Assert.Null(result.Palette);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void PaletteSerializer_SkipsInvalidSwatchesAndDisambiguatesNames()
    {
        string json = """
        {
          "name": "Imported",
          "swatches": [
            { "name": "Key", "value": { "r": 1, "g": 0.5, "b": 0.25, "a": 1 }, "space": "Srgb" },
            { "name": "Key", "value": { "r": 2, "g": 1, "b": 0.5, "a": 1 }, "space": "Linear", "isHdr": true },
            { "name": "Broken", "value": { "r": 1, "g": 0 } }
          ]
        }
        """;

        MGColorPaletteSerializationResult result = MGColorPaletteSerializer.FromJson(json);

        Assert.True(result.Success);
        Assert.Single(result.Diagnostics);
        Assert.Equal(2, result.Palette.Swatches.Count);
        Assert.Equal("Key", result.Palette.Swatches[0].Name);
        Assert.Equal("Key (2)", result.Palette.Swatches[1].Name);
        Assert.True(result.Palette.Swatches[1].Value.IsHdr);
        Assert.Equal(ColorSpaceMode.Linear, result.Palette.Swatches[1].Value.ColorSpace);
    }

    [Fact]
    public void PaletteStore_ImportsProjectPaletteWithoutFileSystemDependency()
    {
        MGColorPaletteStore store = new();
        MGColorPalette palette = new("Favorites");
        palette.AddSwatch("Accent", new ColorValue(0.1f, 0.2f, 0.3f, 1f));
        string json = store.ExportPalette(palette, indented: false);

        bool success = store.TryImportProjectPalette(json, out MGColorPalette imported, out IReadOnlyList<string> diagnostics);

        Assert.True(success);
        Assert.Empty(diagnostics);
        Assert.Same(imported, store.ProjectPalettes[0]);
        Assert.Equal("Favorites", imported.Name);
    }
}