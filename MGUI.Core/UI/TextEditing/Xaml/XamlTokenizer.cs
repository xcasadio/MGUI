namespace MGUI.Core.UI.TextEditing.Xaml;

/// <summary>A tolerant, non-validating XAML tokenizer (backlog task 3 of
/// <c>Docs/Tasks/richtextbox-autocomplete-tasks.md</c>): it recognizes the punctuation and lexical shapes of XML/XAML
/// markup (<c>&lt; &gt; / = " ' : . {{ }}</c>, identifiers, strings, comments, whitespace and free text) without
/// validating structure, and it never throws, even on an incomplete document such as <c>&lt;</c>, <c>&lt;Button</c>
/// or <c>&lt;Button Width="</c>. There is no incremental re-tokenization: the whole document is re-scanned every
/// time, which is deliberately simple until a profiler shows it needs to change.</summary>
public static class XamlTokenizer
{
    public static IReadOnlyList<XamlToken> Tokenize(string text)
    {
        var source = text ?? string.Empty;
        List<XamlToken> tokens = new();
        var index = 0;

        while (index < source.Length)
        {
            var startIndex = index;
            var character = source[index];

            if (character == '<' && index + 3 < source.Length && source[index + 1] == '!' && source[index + 2] == '-' && source[index + 3] == '-')
            {
                index += 4;
                var closingIndex = source.IndexOf("-->", index, StringComparison.Ordinal);
                index = closingIndex >= 0 ? closingIndex + 3 : source.Length;
                Add(tokens, XamlTokenKind.Comment, source, startIndex, index);
                continue;
            }

            switch (character)
            {
                case '<':
                    index++;
                    Add(tokens, XamlTokenKind.LessThan, source, startIndex, index);
                    continue;
                case '>':
                    index++;
                    Add(tokens, XamlTokenKind.GreaterThan, source, startIndex, index);
                    continue;
                case '/':
                    index++;
                    Add(tokens, XamlTokenKind.Slash, source, startIndex, index);
                    continue;
                case '=':
                    index++;
                    Add(tokens, XamlTokenKind.Equals, source, startIndex, index);
                    continue;
                case ':':
                    index++;
                    Add(tokens, XamlTokenKind.Colon, source, startIndex, index);
                    continue;
                case '.':
                    index++;
                    Add(tokens, XamlTokenKind.Dot, source, startIndex, index);
                    continue;
                case '{':
                    index++;
                    Add(tokens, XamlTokenKind.OpenBrace, source, startIndex, index);
                    continue;
                case '}':
                    index++;
                    Add(tokens, XamlTokenKind.CloseBrace, source, startIndex, index);
                    continue;
                case '"':
                case '\'':
                    index = ScanString(source, index, character);
                    Add(tokens, XamlTokenKind.String, source, startIndex, index);
                    continue;
            }

            if (char.IsWhiteSpace(character))
            {
                index++;
                while (index < source.Length && char.IsWhiteSpace(source[index]))
                {
                    index++;
                }

                Add(tokens, XamlTokenKind.Whitespace, source, startIndex, index);
                continue;
            }

            if (IsIdentifierStart(character))
            {
                index++;
                while (index < source.Length && IsIdentifierPart(source[index]))
                {
                    index++;
                }

                Add(tokens, XamlTokenKind.Identifier, source, startIndex, index);
                continue;
            }

            //  Anything else (free text between tags, stray punctuation this tokenizer does not special-case) is
            //  consumed one character at a time as Text, so that a single Unknown-looking character never grows
            //  into a run that swallows the next recognizable token.
            index++;
            Add(tokens, XamlTokenKind.Text, source, startIndex, index);
        }

        tokens.Add(new XamlToken(XamlTokenKind.EndOfFile, MGTextRange.EmptyAt(source.Length), string.Empty));
        return tokens;
    }

    /// <summary>Scans a quoted string starting at <paramref name="index"/> (which points at the opening quote
    /// character). Tolerant of an unterminated string: if the closing quote is never found, the string runs to the
    /// end of the text instead of throwing.</summary>
    private static int ScanString(string source, int index, char quoteCharacter)
    {
        var cursor = index + 1;
        while (cursor < source.Length && source[cursor] != quoteCharacter)
        {
            cursor++;
        }

        return cursor < source.Length ? cursor + 1 : source.Length;
    }

    private static bool IsIdentifierStart(char character)
        => char.IsLetter(character) || character == '_';

    private static bool IsIdentifierPart(char character)
        => char.IsLetterOrDigit(character) || character == '_' || character == '-';

    private static void Add(List<XamlToken> tokens, XamlTokenKind kind, string source, int startIndex, int endIndex)
        => tokens.Add(new XamlToken(kind, new MGTextRange(startIndex, endIndex), source.Substring(startIndex, endIndex - startIndex)));
}
