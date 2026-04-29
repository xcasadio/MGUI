namespace MGUI.Core.UI.TextEditing
{
    public readonly record struct MGRichTextCompletionContext(string Text, int CaretIndex, MGRichTextCompletionTrigger Trigger,
        char? TriggerCharacter, string Prefix, MGTextRange ReplacementRange, int Version);
}