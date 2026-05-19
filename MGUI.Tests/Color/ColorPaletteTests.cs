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
}