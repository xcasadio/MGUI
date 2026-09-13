namespace MGUI.Core.UI;

public sealed class MGColorPaletteStore
{
    public MGColorPalette RecentColors { get; }
    public MGColorPalette Favorites { get; }
    public System.Collections.Generic.List<MGColorPalette> ProjectPalettes { get; }
    public int MaxRecentColors { get; set; } = 16;

    public MGColorPaletteStore()
    {
        RecentColors = new MGColorPalette("Recent");
        Favorites = new MGColorPalette("Favorites");
        ProjectPalettes = new System.Collections.Generic.List<MGColorPalette>();
    }

    public MGColorSwatch AddRecent(ColorValue value, string name = null)
        => RecentColors.AddOrMoveToFront(name ?? value.ToHex(ColorValueFormat.HexRgba), value, MaxRecentColors);

    public MGColorSwatch AddFavorite(string name, ColorValue value)
        => Favorites.AddSwatch(name, value);

    public bool RemoveFavorite(MGColorSwatch swatch)
        => Favorites.RemoveSwatch(swatch);

    public MGColorPalette AddProjectPalette(MGColorPalette palette)
    {
        if (palette != null)
        {
            ProjectPalettes.Add(palette);
        }

        return palette;
    }

    public string ExportPalette(MGColorPalette palette, bool indented = true)
        => MGColorPaletteSerializer.ToJson(palette, indented);

    public bool TryImportProjectPalette(string json, out MGColorPalette palette, out System.Collections.Generic.IReadOnlyList<string> diagnostics)
    {
        var success = MGColorPaletteSerializer.TryFromJson(json, out palette, out diagnostics);
        if (success && palette != null)
        {
            ProjectPalettes.Add(palette);
        }

        return success;
    }
}