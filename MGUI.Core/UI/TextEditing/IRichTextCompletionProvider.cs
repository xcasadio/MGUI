namespace MGUI.Core.UI.TextEditing
{
    public interface IRichTextCompletionProvider
    {
        MGRichTextCompletionResult GetCompletions(MGRichTextCompletionContext context);
    }
}