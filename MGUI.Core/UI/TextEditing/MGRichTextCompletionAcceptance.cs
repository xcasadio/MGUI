namespace MGUI.Core.UI.TextEditing;

public readonly record struct MGRichTextCompletionAcceptance(MGTextRange ReplacementRange, string InsertText, int NewCaretIndex);