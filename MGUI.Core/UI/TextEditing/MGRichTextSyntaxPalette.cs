using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.TextEditing
{
    public sealed class MGRichTextSyntaxPalette
    {
        public static MGRichTextSyntaxPalette Default { get; } = new();

        public Color Keyword { get; set; } = new(86, 156, 214);
        public Color String { get; set; } = new(214, 157, 133);
        public Color Comment { get; set; } = new(87, 166, 74);
        public Color Number { get; set; } = new(181, 206, 168);
        public Color TypeName { get; set; } = new(78, 201, 176);
    }
}