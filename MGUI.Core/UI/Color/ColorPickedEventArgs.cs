using System;

namespace MGUI.Core.UI
{
    public sealed class ColorPickedEventArgs : EventArgs
    {
        public ColorValue Value { get; }

        public ColorPickedEventArgs(ColorValue value)
        {
            Value = value;
        }
    }
}