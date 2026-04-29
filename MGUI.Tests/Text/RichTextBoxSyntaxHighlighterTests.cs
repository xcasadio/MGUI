using MGUI.Core.UI.TextEditing;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Text;

public class RichTextBoxSyntaxHighlighterTests
{
    [Fact]
    public void PlainTextSyntaxHighlighter_ReturnsNoSpansForCurrentVersion()
    {
        MGRichTextHighlightResult result = PlainTextSyntaxHighlighter.Instance.Highlight(new MGRichTextHighlightContext("class Demo", 7, MGRichTextSyntaxPalette.Default));

        Assert.Equal(7, result.Version);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void CSharpHighlighter_ClassifiesKeywordsTypesStringsNumbersAndComments()
    {
        string text = "public class Demo { string Name = \"A\"; int Count = 42; // note";
        CSharpRichTextSyntaxHighlighter highlighter = new();

        MGRichTextHighlightResult result = highlighter.Highlight(new MGRichTextHighlightContext(text, 3, MGRichTextSyntaxPalette.Default));

        Assert.Equal(3, result.Version);
        Assert.Contains(result.Spans, span => span.Classification == "keyword" && text.Substring(span.Range.StartIndex, span.Range.Length) == "public");
        Assert.Contains(result.Spans, span => span.Classification == "keyword" && text.Substring(span.Range.StartIndex, span.Range.Length) == "class");
        Assert.Contains(result.Spans, span => span.Classification == "type" && text.Substring(span.Range.StartIndex, span.Range.Length) == "Demo");
        Assert.Contains(result.Spans, span => span.Classification == "string" && text.Substring(span.Range.StartIndex, span.Range.Length) == "\"A\"");
        Assert.Contains(result.Spans, span => span.Classification == "number" && text.Substring(span.Range.StartIndex, span.Range.Length) == "42");
        Assert.Contains(result.Spans, span => span.Classification == "comment" && text.Substring(span.Range.StartIndex, span.Range.Length) == "// note");
    }

    [Fact]
    public void CSharpHighlighter_UsesProvidedPaletteColors()
    {
        MGRichTextSyntaxPalette palette = new() { Keyword = Color.HotPink };
        CSharpRichTextSyntaxHighlighter highlighter = new();

        MGRichTextHighlightResult result = highlighter.Highlight(new MGRichTextHighlightContext("return value;", 1, palette));

        MGStyledTextSpan keyword = Assert.Single(result.Spans, span => span.Classification == "keyword");
        Assert.Equal(Color.HotPink, keyword.Style.Foreground);
    }
}