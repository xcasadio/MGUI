using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.TextEditing;

public sealed class MGRichTextSyntaxPalette
{
    public static MGRichTextSyntaxPalette Default { get; } = new();

    public Color Keyword { get; set; } = new(86, 156, 214);
    public Color String { get; set; } = new(214, 157, 133);
    public Color Comment { get; set; } = new(87, 166, 74);
    public Color Number { get; set; } = new(181, 206, 168);
    public Color TypeName { get; set; } = new(78, 201, 176);

    //  Additive for the XAML highlighter (Docs/Tasks/xaml-editor-tasks.md): a dedicated palette type was
    //  rejected because MGRichTextBox exposes a single SyntaxPalette property, and a second palette type would need a
    //  second property on that public control. String and Comment above are reused as-is for XAML strings/comments.
    public Color ElementName { get; set; } = new(86, 156, 214);
    public Color AttributeName { get; set; } = new(156, 220, 254);
    public Color Punctuation { get; set; } = new(212, 212, 212);
    public Color MarkupExtension { get; set; } = new(197, 134, 192);
}