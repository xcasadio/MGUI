namespace MGUI.Core.UI;

public sealed class ColorSwatchSelectedEventArgs : EventArgs
{
    public MGColorSwatch Swatch { get; }

    public ColorSwatchSelectedEventArgs(MGColorSwatch swatch)
    {
        Swatch = swatch;
    }
}