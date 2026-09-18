using MGUI.Core.UI.TextEditing.Xaml;

namespace MGUI.Tests.Text;

/// <summary>Tokenizer cases of backlog task 3 (<c>Docs/Tasks/richtextbox-autocomplete-tasks.md</c>), acceptance
/// bullets of Docs/Tasks/xaml-editor-tasks.md: kinds, spans and texts on complete markup, and no exception
/// on incomplete input.</summary>
public class XamlTokenizerTests
{
    private static string TextOf(string source, MGUI.Core.UI.TextEditing.MGTextRange range)
        => source.Substring(range.StartIndex, range.Length);

    [Fact]
    public void Tokenize_SelfClosingTagWithNoAttributes_ProducesExpectedKindsSpansAndTexts()
    {
        const string text = "<Button />";
        var tokens = XamlTokenizer.Tokenize(text);

        Assert.Equal(
            new[] { XamlTokenKind.LessThan, XamlTokenKind.Identifier, XamlTokenKind.Whitespace, XamlTokenKind.Slash, XamlTokenKind.GreaterThan, XamlTokenKind.EndOfFile },
            tokens.Select(t => t.Kind));

        var identifier = tokens.Single(t => t.Kind == XamlTokenKind.Identifier);
        Assert.Equal("Button", identifier.Text);
        Assert.Equal("Button", TextOf(text, identifier.Range));
        Assert.Equal(new MGUI.Core.UI.TextEditing.MGTextRange(1, 7), identifier.Range);
    }

    [Fact]
    public void Tokenize_TagWithTwoAttributes_ProducesIdentifiersEqualsAndStrings()
    {
        const string text = "<Button Width=\"100\" Height=\"50\" />";
        var tokens = XamlTokenizer.Tokenize(text);

        Assert.Equal(
            new[]
            {
                XamlTokenKind.LessThan, XamlTokenKind.Identifier, XamlTokenKind.Whitespace,
                XamlTokenKind.Identifier, XamlTokenKind.Equals, XamlTokenKind.String, XamlTokenKind.Whitespace,
                XamlTokenKind.Identifier, XamlTokenKind.Equals, XamlTokenKind.String, XamlTokenKind.Whitespace,
                XamlTokenKind.Slash, XamlTokenKind.GreaterThan, XamlTokenKind.EndOfFile
            },
            tokens.Select(t => t.Kind));

        var strings = tokens.Where(t => t.Kind == XamlTokenKind.String).ToList();
        Assert.Equal("\"100\"", strings[0].Text);
        Assert.Equal("\"50\"", strings[1].Text);
        Assert.Equal(text.Substring(strings[0].Range.StartIndex, strings[0].Range.Length), strings[0].Text);
    }

    [Fact]
    public void Tokenize_NestedTags_ProducesAnOpeningAndAClosingTagPerElement()
    {
        const string text = "<Outer><Inner /></Outer>";
        var tokens = XamlTokenizer.Tokenize(text);

        Assert.Equal(
            new[]
            {
                XamlTokenKind.LessThan, XamlTokenKind.Identifier, XamlTokenKind.GreaterThan,
                XamlTokenKind.LessThan, XamlTokenKind.Identifier, XamlTokenKind.Whitespace, XamlTokenKind.Slash, XamlTokenKind.GreaterThan,
                XamlTokenKind.LessThan, XamlTokenKind.Slash, XamlTokenKind.Identifier, XamlTokenKind.GreaterThan,
                XamlTokenKind.EndOfFile
            },
            tokens.Select(t => t.Kind));

        var identifiers = tokens.Where(t => t.Kind == XamlTokenKind.Identifier).Select(t => t.Text).ToList();
        Assert.Equal(new[] { "Outer", "Inner", "Outer" }, identifiers);
    }

    [Fact]
    public void Tokenize_Comment_ProducesOneCommentTokenCoveringTheWholeComment()
    {
        const string text = "<!-- comment -->";
        var tokens = XamlTokenizer.Tokenize(text);

        var comment = Assert.Single(tokens, t => t.Kind == XamlTokenKind.Comment);
        Assert.Equal(text, comment.Text);
        Assert.Equal(new MGUI.Core.UI.TextEditing.MGTextRange(0, text.Length), comment.Range);
    }

    [Fact]
    public void Tokenize_MarkupExtensionAttributeValue_IsOneStringTokenIncludingTheBraces()
    {
        const string text = "<TextBlock Text=\"{Binding Name}\" />";
        var tokens = XamlTokenizer.Tokenize(text);

        var stringToken = Assert.Single(tokens, t => t.Kind == XamlTokenKind.String);
        Assert.Equal("\"{Binding Name}\"", stringToken.Text);
    }

    [Theory]
    [InlineData("<")]
    [InlineData("<Button")]
    [InlineData("<Button Width=\"")]
    public void Tokenize_IncompleteInput_NeverThrowsAndEndsWithEndOfFile(string text)
    {
        var exception = Record.Exception(() => XamlTokenizer.Tokenize(text));
        Assert.Null(exception);

        var tokens = XamlTokenizer.Tokenize(text);
        Assert.Equal(XamlTokenKind.EndOfFile, tokens[^1].Kind);
        Assert.True(tokens[^1].Range.IsEmpty);
    }

    [Fact]
    public void Tokenize_NullText_NeverThrowsAndReturnsOnlyEndOfFile()
    {
        var exception = Record.Exception(() => XamlTokenizer.Tokenize(null));
        Assert.Null(exception);

        var tokens = XamlTokenizer.Tokenize(null);
        Assert.Single(tokens);
        Assert.Equal(XamlTokenKind.EndOfFile, tokens[0].Kind);
    }
}
