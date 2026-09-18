namespace MGUI.Core.UI.TextEditing.Xaml;

/// <summary>A single lexical unit produced by <see cref="XamlTokenizer"/>. <see cref="Range"/> is a 0-based
/// character span into the tokenized text, and <see cref="Text"/> is that same substring, kept alongside the
/// range so a consumer (highlighter, analyzer, completion) does not need to re-slice the source text.</summary>
public readonly record struct XamlToken(XamlTokenKind Kind, MGTextRange Range, string Text);
