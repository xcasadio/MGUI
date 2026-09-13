namespace MGUI.Core.UI.TextEditing;

public readonly record struct MGStyledTextSpan(MGTextRange Range, MGRichTextStyle Style, string Classification = null)
{
    public bool IsEmpty => Range.IsEmpty;

    public MGStyledTextSpan Normalize() => this with { Range = Range.Normalize() };
    public MGStyledTextSpan Clamp(int textLength) => this with { Range = Range.Clamp(textLength).Normalize() };
}