namespace MGUI.Core.UI.TextEditing
{
    public sealed class PlainTextSyntaxHighlighter : IRichTextSyntaxHighlighter
    {
        public static PlainTextSyntaxHighlighter Instance { get; } = new();

        public MGRichTextHighlightResult Highlight(MGRichTextHighlightContext context)
            => new(context.Version, System.Array.Empty<MGStyledTextSpan>());
    }
}