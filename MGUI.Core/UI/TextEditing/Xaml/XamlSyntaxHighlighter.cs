namespace MGUI.Core.UI.TextEditing.Xaml;

/// <summary>Colors XAML markup through the same non-destructive span pipeline as <see cref="CSharpRichTextSyntaxHighlighter"/>
/// (backlog task 7 of <c>Docs/Tasks/richtextbox-autocomplete-tasks.md</c>), built on top of <see cref="XamlTokenizer"/>.
/// It never touches <see cref="MGRichTextHighlightContext.Text"/> and always echoes <see cref="MGRichTextHighlightContext.Version"/>,
/// as <see cref="MGUI.Core.UI.MGRichTextBox.RefreshSyntaxHighlighting"/> requires. Colored roles: punctuation
/// (<c>&lt; &gt; / = :</c>), element names, attribute names, strings, comments and markup extensions (a string whose
/// trimmed content is wrapped in <c>{ }</c>, such as <c>{Binding Name}</c>). Classification names are stable and
/// lowercase (<c>xaml.punctuation</c>, <c>xaml.element-name</c>, <c>xaml.attribute-name</c>, <c>xaml.string</c>,
/// <c>xaml.comment</c>, <c>xaml.markup-extension</c>), so a later slice can find spans by role.</summary>
public sealed class XamlSyntaxHighlighter : IRichTextSyntaxHighlighter
{
    public const string PunctuationClassification = "xaml.punctuation";
    public const string ElementNameClassification = "xaml.element-name";
    public const string AttributeNameClassification = "xaml.attribute-name";
    public const string StringClassification = "xaml.string";
    public const string CommentClassification = "xaml.comment";
    public const string MarkupExtensionClassification = "xaml.markup-extension";

    public MGRichTextHighlightResult Highlight(MGRichTextHighlightContext context)
    {
        var text = context.Text ?? string.Empty;
        var palette = context.Palette ?? MGRichTextSyntaxPalette.Default;
        List<MGStyledTextSpan> spans = new();

        //  True from a '<' or '</' until the matching '>': only inside a tag do bare identifiers mean an element or
        //  attribute name. Text between tags (or after an unterminated '<') is left uncolored, same as free text.
        var insideTag = false;
        //  Cleared every time a tag opens: the first identifier encountered inside a tag is its element name
        //  (including a closing tag's name, such as "Button" in "</Button>"); every identifier after that, until the
        //  tag closes, is an attribute name.
        var consumedElementName = false;

        foreach (var token in XamlTokenizer.Tokenize(text))
        {
            switch (token.Kind)
            {
                case XamlTokenKind.Comment:
                    spans.Add(CreateSpan(token.Range, palette.Comment, CommentClassification));
                    break;

                case XamlTokenKind.String:
                    if (IsMarkupExtension(token.Text))
                    {
                        spans.Add(CreateSpan(token.Range, palette.MarkupExtension, MarkupExtensionClassification));
                    }
                    else
                    {
                        spans.Add(CreateSpan(token.Range, palette.String, StringClassification));
                    }
                    break;

                case XamlTokenKind.LessThan:
                    insideTag = true;
                    consumedElementName = false;
                    spans.Add(CreateSpan(token.Range, palette.Punctuation, PunctuationClassification));
                    break;

                case XamlTokenKind.GreaterThan:
                    spans.Add(CreateSpan(token.Range, palette.Punctuation, PunctuationClassification));
                    insideTag = false;
                    break;

                case XamlTokenKind.Slash:
                case XamlTokenKind.Equals:
                case XamlTokenKind.Colon:
                    spans.Add(CreateSpan(token.Range, palette.Punctuation, PunctuationClassification));
                    break;

                case XamlTokenKind.Identifier:
                    if (insideTag)
                    {
                        if (!consumedElementName)
                        {
                            spans.Add(CreateSpan(token.Range, palette.ElementName, ElementNameClassification));
                            consumedElementName = true;
                        }
                        else
                        {
                            spans.Add(CreateSpan(token.Range, palette.AttributeName, AttributeNameClassification));
                        }
                    }
                    break;
            }
        }

        return new MGRichTextHighlightResult(context.Version, spans);
    }

    /// <summary>True when a string token's quoted content, trimmed of surrounding whitespace, is wrapped in
    /// <c>{ }</c> (a markup extension such as <c>"{Binding Name}"</c> or <c>"{StaticResource Foo}"</c>).</summary>
    private static bool IsMarkupExtension(string tokenText)
    {
        if (string.IsNullOrEmpty(tokenText) || tokenText.Length < 2)
        {
            return false;
        }

        var quoteCharacter = tokenText[0];
        if (quoteCharacter != '"' && quoteCharacter != '\'')
        {
            return false;
        }

        var contentEndIndex = tokenText.Length > 1 && tokenText[^1] == quoteCharacter ? tokenText.Length - 1 : tokenText.Length;
        var content = tokenText.Substring(1, contentEndIndex - 1).Trim();
        return content.Length >= 2 && content[0] == '{' && content[^1] == '}';
    }

    private static MGStyledTextSpan CreateSpan(MGTextRange range, Microsoft.Xna.Framework.Color color, string classification)
        => new(range, new MGRichTextStyle(Foreground: color), classification);
}
