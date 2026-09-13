namespace MGUI.Core.UI;

public sealed class ColorFieldValueChangedEventArgs : EventArgs
{
    public ColorValue? PreviousValue { get; }
    public ColorValue? NewValue { get; }

    public ColorFieldValueChangedEventArgs(ColorValue? previousValue, ColorValue? newValue)
    {
        PreviousValue = previousValue;
        NewValue = newValue;
    }
}