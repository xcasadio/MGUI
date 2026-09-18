using MGUI.Core.UI.TextEditing;
using MGUI.Core.UI.TextEditing.Xaml;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Text;

/// <summary>Highlighter cases of backlog task 7 (<c>Docs/Tasks/richtextbox-autocomplete-tasks.md</c>), acceptance
/// bullets of Docs/Tasks/xaml-editor-tasks.md: element name, attribute name, string, comment and markup
/// extension spans, the version invariant, and no exception on incomplete XAML.</summary>
public class XamlSyntaxHighlighterTests
{
    private static string TextOf(string source, MGStyledTextSpan span) => source.Substring(span.Range.StartIndex, span.Range.Length);

    [Fact]
    public void Highlight_EchoesTheContextVersion()
    {
        XamlSyntaxHighlighter highlighter = new();
        MGRichTextHighlightResult result = highlighter.Highlight(new MGRichTextHighlightContext("<Button />", 42, MGRichTextSyntaxPalette.Default));
        Assert.Equal(42, result.Version);
    }

    [Fact]
    public void Highlight_ClassifiesElementNameAttributeNameStringCommentAndMarkupExtension()
    {
        const string text = "<!-- header -->\n<TextBlock Text=\"{Binding Name}\" Width=\"100\" />";
        XamlSyntaxHighlighter highlighter = new();

        MGRichTextHighlightResult result = highlighter.Highlight(new MGRichTextHighlightContext(text, 1, MGRichTextSyntaxPalette.Default));

        Assert.Contains(result.Spans, s => s.Classification == "xaml.comment" && TextOf(text, s) == "<!-- header -->");
        Assert.Contains(result.Spans, s => s.Classification == "xaml.element-name" && TextOf(text, s) == "TextBlock");
        Assert.Contains(result.Spans, s => s.Classification == "xaml.attribute-name" && TextOf(text, s) == "Text");
        Assert.Contains(result.Spans, s => s.Classification == "xaml.attribute-name" && TextOf(text, s) == "Width");
        Assert.Contains(result.Spans, s => s.Classification == "xaml.markup-extension" && TextOf(text, s) == "\"{Binding Name}\"");
        Assert.Contains(result.Spans, s => s.Classification == "xaml.string" && TextOf(text, s) == "\"100\"");
        Assert.Contains(result.Spans, s => s.Classification == "xaml.punctuation" && TextOf(text, s) == "<");
        Assert.Contains(result.Spans, s => s.Classification == "xaml.punctuation" && TextOf(text, s) == "=");
    }

    [Fact]
    public void Highlight_ClosingTagNameIsAlsoAnElementNameSpan()
    {
        const string text = "<Outer></Outer>";
        XamlSyntaxHighlighter highlighter = new();

        MGRichTextHighlightResult result = highlighter.Highlight(new MGRichTextHighlightContext(text, 1, MGRichTextSyntaxPalette.Default));

        var elementNameSpans = result.Spans.Where(s => s.Classification == "xaml.element-name").ToList();
        Assert.Equal(2, elementNameSpans.Count);
        Assert.All(elementNameSpans, s => Assert.Equal("Outer", TextOf(text, s)));
    }

    [Fact]
    public void Highlight_UsesProvidedPaletteColors()
    {
        MGRichTextSyntaxPalette palette = new() { ElementName = Color.HotPink, AttributeName = Color.LimeGreen, Punctuation = Color.Yellow, MarkupExtension = Color.Cyan };
        const string text = "<Button Width=\"{Binding W}\" />";
        XamlSyntaxHighlighter highlighter = new();

        MGRichTextHighlightResult result = highlighter.Highlight(new MGRichTextHighlightContext(text, 1, palette));

        Assert.Equal(Color.HotPink, result.Spans.Single(s => s.Classification == "xaml.element-name").Style.Foreground);
        Assert.Equal(Color.LimeGreen, result.Spans.Single(s => s.Classification == "xaml.attribute-name").Style.Foreground);
        Assert.Equal(Color.Cyan, result.Spans.Single(s => s.Classification == "xaml.markup-extension").Style.Foreground);
        Assert.Contains(result.Spans, s => s.Classification == "xaml.punctuation" && s.Style.Foreground == Color.Yellow);
    }

    [Fact]
    public void Highlight_ReturnsNoOverlappingSpans()
    {
        const string text = "<!-- c -->\n<Outer Width=\"{Binding W}\"><Inner Text=\"hi\" /></Outer>";
        XamlSyntaxHighlighter highlighter = new();

        MGRichTextHighlightResult result = highlighter.Highlight(new MGRichTextHighlightContext(text, 1, MGRichTextSyntaxPalette.Default));

        var ordered = result.Spans.OrderBy(s => s.Range.StartIndex).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            Assert.True(ordered[i].Range.StartIndex >= ordered[i - 1].Range.EndIndex,
                $"Span '{TextOf(text, ordered[i - 1])}' overlaps '{TextOf(text, ordered[i])}'");
        }
    }

    [Theory]
    [InlineData("<")]
    [InlineData("<Button")]
    [InlineData("<Button Width=\"")]
    public void Highlight_IncompleteXaml_NeverThrows(string text)
    {
        XamlSyntaxHighlighter highlighter = new();
        var exception = Record.Exception(() => highlighter.Highlight(new MGRichTextHighlightContext(text, 1, MGRichTextSyntaxPalette.Default)));
        Assert.Null(exception);
    }

    [Fact]
    public void Highlight_DifferentVersion_StillEchoesTheContextVersionSoTheCallerCanDiscardStaleResults()
    {
        XamlSyntaxHighlighter highlighter = new();
        MGRichTextHighlightResult result = highlighter.Highlight(new MGRichTextHighlightContext("<Button />", 5, MGRichTextSyntaxPalette.Default));
        Assert.Equal(5, result.Version);
        Assert.NotEqual(6, result.Version);
    }
}
