namespace MGUI.Core.UI;

public sealed class ColorValueChangedEventArgs : EventArgs
{
    public ColorValue PreviousValue { get; }
    public ColorValue NewValue { get; }

    public ColorValueChangedEventArgs(ColorValue previousValue, ColorValue newValue)
    {
        PreviousValue = previousValue;
        NewValue = newValue;
    }
}