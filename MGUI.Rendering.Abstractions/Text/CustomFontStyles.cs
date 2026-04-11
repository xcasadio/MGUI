using System;
using System.ComponentModel;

namespace MGUI.Shared.Text
{
    [Flags]
    public enum CustomFontStyles
    {
        [Description("None")]
        None = 0b_0000_0000,
        [Description("Normal")]
        Normal = 0b_0000_0001,
        [Description("Bold")]
        Bold = 0b_0000_0010,
        [Description("Italic")]
        Italic = 0b_0001_0000
    }
}