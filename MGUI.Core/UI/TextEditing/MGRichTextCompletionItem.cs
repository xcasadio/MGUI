namespace MGUI.Core.UI.TextEditing
{
    public sealed class MGRichTextCompletionItem
    {
        public string Label { get; }
        public string InsertText { get; }
        public string Detail { get; }
        public string Kind { get; }
        public string TextToInsert => InsertText ?? Label;

        public MGRichTextCompletionItem(string label, string insertText = null, string detail = null, string kind = null)
        {
            Label = label ?? string.Empty;
            InsertText = insertText;
            Detail = detail;
            Kind = kind;
        }
    }
}