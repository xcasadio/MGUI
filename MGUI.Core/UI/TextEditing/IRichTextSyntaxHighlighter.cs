namespace MGUI.Core.UI.TextEditing
{
    public interface IRichTextSyntaxHighlighter
    {
        MGRichTextHighlightResult Highlight(MGRichTextHighlightContext context);
    }
}