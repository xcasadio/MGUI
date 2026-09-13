namespace MGUI.Core.UI;

public sealed class ColorValueChangingEventArgs : EventArgs
{
    public ColorValue InitialValue { get; }
    public ColorValue PreviewValue { get; }

    public ColorValueChangingEventArgs(ColorValue initialValue, ColorValue previewValue)
    {
        InitialValue = initialValue;
        PreviewValue = previewValue;
    }
}