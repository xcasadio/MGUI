namespace MGUI.Core.UI;

public sealed class MGColorPaletteSerializationResult
{
    public bool Success { get; }
    public MGColorPalette Palette { get; }
    public IReadOnlyList<string> Diagnostics { get; }

    public MGColorPaletteSerializationResult(bool success, MGColorPalette palette, IReadOnlyList<string> diagnostics)
    {
        Success = success;
        Palette = palette;
        Diagnostics = diagnostics ?? new List<string>();
    }
}