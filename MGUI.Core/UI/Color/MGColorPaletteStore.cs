namespace MGUI.Core.UI
{
    public sealed class MGColorPaletteStore
    {
        public MGColorPalette RecentColors { get; }
        public MGColorPalette Favorites { get; }
        public int MaxRecentColors { get; set; } = 16;

        public MGColorPaletteStore()
        {
            RecentColors = new MGColorPalette("Recent");
            Favorites = new MGColorPalette("Favorites");
        }

        public MGColorSwatch AddRecent(ColorValue value, string name = null)
            => RecentColors.AddOrMoveToFront(name ?? value.ToHex(ColorValueFormat.HexRgba), value, MaxRecentColors);

        public MGColorSwatch AddFavorite(string name, ColorValue value)
            => Favorites.AddSwatch(name, value);

        public bool RemoveFavorite(MGColorSwatch swatch)
            => Favorites.RemoveSwatch(swatch);
    }
}