using System;

namespace MGUI.Core.UI.Styling
{
    [Flags]
    public enum UIInvalidationKind
    {
        None = 0,
        Draw = 1 << 0,
        Measure = 1 << 1,
        Arrange = 1 << 2,
        Structure = 1 << 3,
        Input = 1 << 4,
        Navigation = 1 << 5,
    }
}