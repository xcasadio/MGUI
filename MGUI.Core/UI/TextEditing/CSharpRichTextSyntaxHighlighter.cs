namespace MGUI.Core.UI.TextEditing;

public sealed class CSharpRichTextSyntaxHighlighter : IRichTextSyntaxHighlighter
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "class", "const", "continue",
        "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false",
        "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal",
        "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private",
        "protected", "public", "readonly", "record", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc",
        "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while", "var"
    };

    public MGRichTextHighlightResult Highlight(MGRichTextHighlightContext context)
    {
        var text = context.Text ?? string.Empty;
        var palette = context.Palette ?? MGRichTextSyntaxPalette.Default;
        List<MGStyledTextSpan> spans = new();
        var index = 0;

        while (index < text.Length)
        {
            var character = text[index];

            if (character == '/' && index + 1 < text.Length && text[index + 1] == '/')
            {
                var startIndex = index;
                index += 2;
                while (index < text.Length && text[index] != '\n')
                {
                    index++;
                }

                spans.Add(CreateSpan(startIndex, index, palette.Comment, "comment"));
                continue;
            }

            if (character == '"')
            {
                var startIndex = index;
                index++;
                var isEscaped = false;
                while (index < text.Length)
                {
                    var stringCharacter = text[index++];
                    if (stringCharacter == '"' && !isEscaped)
                    {
                        break;
                    }

                    isEscaped = stringCharacter == '\\' && !isEscaped;
                    if (stringCharacter != '\\')
                    {
                        isEscaped = false;
                    }
                }

                spans.Add(CreateSpan(startIndex, index, palette.String, "string"));
                continue;
            }

            if (char.IsDigit(character))
            {
                var startIndex = index;
                index++;
                while (index < text.Length && (char.IsDigit(text[index]) || text[index] == '.'))
                {
                    index++;
                }

                spans.Add(CreateSpan(startIndex, index, palette.Number, "number"));
                continue;
            }

            if (IsIdentifierStart(character))
            {
                var startIndex = index;
                index++;
                while (index < text.Length && IsIdentifierPart(text[index]))
                {
                    index++;
                }

                var token = text.Substring(startIndex, index - startIndex);
                if (Keywords.Contains(token))
                {
                    spans.Add(CreateSpan(startIndex, index, palette.Keyword, "keyword", isBold: true));
                }
                else if (char.IsUpper(token[0]))
                {
                    spans.Add(CreateSpan(startIndex, index, palette.TypeName, "type"));
                }

                continue;
            }

            index++;
        }

        return new MGRichTextHighlightResult(context.Version, spans);
    }

    private static bool IsIdentifierStart(char character)
        => char.IsLetter(character) || character == '_';

    private static bool IsIdentifierPart(char character)
        => char.IsLetterOrDigit(character) || character == '_';

    private static MGStyledTextSpan CreateSpan(int startIndex, int endIndex, Microsoft.Xna.Framework.Color color, string classification, bool isBold = false)
        => new(new MGTextRange(startIndex, endIndex), new MGRichTextStyle(Foreground: color, IsBold: isBold), classification);
}