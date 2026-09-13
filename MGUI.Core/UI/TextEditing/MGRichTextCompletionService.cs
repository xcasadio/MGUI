namespace MGUI.Core.UI.TextEditing;

public static class MGRichTextCompletionService
{
    public static MGRichTextCompletionContext CreateContext(string text, int caretIndex, MGRichTextCompletionTrigger trigger,
        char? triggerCharacter, int version)
    {
        var sourceText = text ?? string.Empty;
        var actualCaretIndex = Math.Clamp(caretIndex, 0, sourceText.Length);
        var replacementRange = GetPrefixRange(sourceText, actualCaretIndex);
        var prefix = sourceText.Substring(replacementRange.StartIndex, replacementRange.Length);
        return new MGRichTextCompletionContext(sourceText, actualCaretIndex, trigger, triggerCharacter, prefix, replacementRange, version);
    }

    public static MGTextRange GetPrefixRange(string text, int caretIndex)
    {
        var sourceText = text ?? string.Empty;
        var actualCaretIndex = Math.Clamp(caretIndex, 0, sourceText.Length);
        var startIndex = actualCaretIndex;
        while (startIndex > 0 && IsIdentifierPart(sourceText[startIndex - 1]))
        {
            startIndex--;
        }

        return new MGTextRange(startIndex, actualCaretIndex);
    }

    public static MGRichTextCompletionAcceptance CreateAcceptance(MGRichTextCompletionItem item, MGTextRange replacementRange)
    {
        var insertText = item?.TextToInsert ?? string.Empty;
        var newCaretIndex = replacementRange.StartIndex + insertText.Length;
        return new MGRichTextCompletionAcceptance(replacementRange.Normalize(), insertText, newCaretIndex);
    }

    public static IReadOnlyList<MGRichTextCompletionItem> FilterByPrefix(IEnumerable<MGRichTextCompletionItem> items, string prefix)
    {
        List<MGRichTextCompletionItem> filteredItems = new();
        var actualPrefix = prefix ?? string.Empty;

        if (items == null)
        {
            return filteredItems;
        }

        foreach (var item in items)
        {
            if (item == null)
            {
                continue;
            }

            if (actualPrefix.Length == 0 || item.Label.StartsWith(actualPrefix, StringComparison.OrdinalIgnoreCase))
            {
                filteredItems.Add(item);
            }
        }

        return filteredItems;
    }

    private static bool IsIdentifierPart(char character)
        => char.IsLetterOrDigit(character) || character == '_';
}