using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.TextEditing;

public readonly record struct MGRichTextStyle(Color? Foreground = null, Color? Background = null, bool IsBold = false, bool IsItalic = false, bool IsUnderlined = false)
{
    public static MGRichTextStyle Default { get; } = new();

    public MGRichTextStyle MergeOver(MGRichTextStyle baseStyle)
        => new(Foreground ?? baseStyle.Foreground, Background ?? baseStyle.Background, IsBold || baseStyle.IsBold, IsItalic || baseStyle.IsItalic, IsUnderlined || baseStyle.IsUnderlined);
}