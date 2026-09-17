using MGUI.Core.UI.TextEditing;
using MGUI.Editor.Document;

namespace MGUI.Tests.Editor;

/// <summary>X1: <see cref="XamlDocumentModel"/> never throws, and its nodes' ranges and ordinals match the raw text and the loader.</summary>
public class XamlDocumentModelTests
{
    private const string Ns = "clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core";

    [Theory]
    [InlineData("<")]
    [InlineData("<Button")]
    [InlineData("<Button Width=\"")]
    [InlineData("")]
    public void TryParse_NeverThrows_AndFailsWithLineAndColumn_OnMalformedOrEmptyText(string text)
    {
        var succeeded = XamlDocumentModel.TryParse(text, out var model, out var error);

        Assert.False(succeeded);
        Assert.Null(model);
        Assert.NotNull(error);
        Assert.True(error.LineNumber >= 1);
        Assert.True(error.LinePosition >= 1);
    }

    [Fact]
    public void TryParse_Null_IsTreatedAsEmptyText_AndFails()
    {
        Assert.False(XamlDocumentModel.TryParse(null, out var model, out var error));
        Assert.Null(model);
        Assert.NotNull(error);
    }

    [Fact]
    public void SelfClosingTag_HasEqualStartTagAndFullRange_CoveringTheWholeTag()
    {
        string text = $"<Window xmlns=\"{Ns}\"><Button Name=\"B\" /></Window>";
        Assert.True(XamlDocumentModel.TryParse(text, out var model, out _));

        var button = model.Root.Children.Single();
        Assert.True(button.IsSelfClosing);
        Assert.Equal(button.StartTagRange, button.FullRange);

        int start = text.IndexOf("<Button", StringComparison.Ordinal);
        int end = text.IndexOf("/>", start, StringComparison.Ordinal) + 2;
        Assert.Equal(new MGTextRange(start, end), button.FullRange);
    }

    [Fact]
    public void TagWithChildren_FullRange_SpansFromOpeningToClosingTag()
    {
        string text = $"<Window xmlns=\"{Ns}\">\n  <StackPanel>\n    <TextBlock Text=\"Hi\" />\n  </StackPanel>\n</Window>\n";
        Assert.True(XamlDocumentModel.TryParse(text, out var model, out _));

        var stackPanel = model.Root.Children.Single();
        Assert.False(stackPanel.IsSelfClosing);

        int start = text.IndexOf("<StackPanel>", StringComparison.Ordinal);
        int end = text.IndexOf("</StackPanel>", StringComparison.Ordinal) + "</StackPanel>".Length;
        Assert.Equal(new MGTextRange(start, end), stackPanel.FullRange);

        int startTagEnd = text.IndexOf('>', start) + 1;
        Assert.Equal(new MGTextRange(start, startTagEnd), stackPanel.StartTagRange);
    }

    [Fact]
    public void MultiLineStartTag_RangesAreExact()
    {
        string text = $"<Window xmlns=\"{Ns}\">\n" +
                       "  <Button\n" +
                       "      Name=\"B\"\n" +
                       "      Width=\"120\" />\n" +
                       "</Window>\n";
        Assert.True(XamlDocumentModel.TryParse(text, out var model, out _));

        var button = model.Root.Children.Single();
        int start = text.IndexOf("<Button", StringComparison.Ordinal);
        int end = text.IndexOf("/>", start, StringComparison.Ordinal) + 2;
        Assert.Equal(new MGTextRange(start, end), button.StartTagRange);
        Assert.True(button.IsSelfClosing);
    }

    [Fact]
    public void NestedSameNameElements_AreMatchedByAProperStack()
    {
        string text = $"<Window xmlns=\"{Ns}\">\n" +
                       "  <StackPanel Name=\"Outer\">\n" +
                       "    <StackPanel Name=\"Inner\">\n" +
                       "      <TextBlock Text=\"Hi\" />\n" +
                       "    </StackPanel>\n" +
                       "  </StackPanel>\n" +
                       "</Window>\n";
        Assert.True(XamlDocumentModel.TryParse(text, out var model, out _));

        var outer = model.Root.Children.Single();
        var inner = outer.Children.Single();

        int outerStart = text.IndexOf("<StackPanel Name=\"Outer\"", StringComparison.Ordinal);
        int outerEnd = text.LastIndexOf("</StackPanel>", StringComparison.Ordinal) + "</StackPanel>".Length;
        Assert.Equal(new MGTextRange(outerStart, outerEnd), outer.FullRange);

        int innerStart = text.IndexOf("<StackPanel Name=\"Inner\"", StringComparison.Ordinal);
        int innerEnd = text.IndexOf("</StackPanel>", StringComparison.Ordinal) + "</StackPanel>".Length;
        Assert.Equal(new MGTextRange(innerStart, innerEnd), inner.FullRange);
        Assert.True(innerEnd < outerEnd);
    }

    [Theory]
    [InlineData('"')]
    [InlineData('\'')]
    public void Attribute_NameAndValueRanges_AreExact_ForBothQuoteCharacters(char quote)
    {
        string text = $"<Window xmlns={quote}{Ns}{quote}><Button Width={quote}120{quote} /></Window>";
        Assert.True(XamlDocumentModel.TryParse(text, out var model, out _));

        var button = model.Root.Children.Single();
        var attribute = button.Attributes.Single(a => a.Name == "Width");
        Assert.Equal(quote, attribute.QuoteChar);
        Assert.Equal("120", attribute.Value);

        int nameStart = text.IndexOf("Width", StringComparison.Ordinal);
        Assert.Equal(new MGTextRange(nameStart, nameStart + "Width".Length), attribute.NameRange);

        int valueStart = text.IndexOf("120", StringComparison.Ordinal);
        Assert.Equal(new MGTextRange(valueStart, valueStart + "120".Length), attribute.ValueRange);
    }

    [Fact]
    public void Attribute_WithAnEntity_DecodesTheValue_ButKeepsTheRawValueRange()
    {
        string text = $"<Window xmlns=\"{Ns}\"><TextBlock Text=\"A &amp; B\" /></Window>";
        Assert.True(XamlDocumentModel.TryParse(text, out var model, out _));

        var textBlock = model.Root.Children.Single();
        var attribute = textBlock.Attributes.Single(a => a.Name == "Text");
        Assert.Equal("A & B", attribute.Value);

        int valueStart = text.IndexOf("A &amp; B", StringComparison.Ordinal);
        Assert.Equal(new MGTextRange(valueStart, valueStart + "A &amp; B".Length), attribute.ValueRange);
    }

    [Fact]
    public void Attribute_WithAMarkupExtensionValue_KeepsTheRawTextAsTheDecodedValue()
    {
        string text = $"<Window xmlns=\"{Ns}\" xmlns:dataBinding=\"clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core\">" +
                      "<TextBlock Text=\"{dataBinding:MGBinding Path=Foo}\" /></Window>";
        Assert.True(XamlDocumentModel.TryParse(text, out var model, out _));

        var textBlock = model.Root.Children.Single();
        var attribute = textBlock.Attributes.Single(a => a.Name == "Text");
        Assert.Equal("{dataBinding:MGBinding Path=Foo}", attribute.Value);
    }

    [Fact]
    public void NamespaceDeclarations_AreNeverListedAsAttributes()
    {
        string text = $"<Window xmlns=\"{Ns}\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" Width=\"1\" />";
        Assert.True(XamlDocumentModel.TryParse(text, out var model, out _));

        Assert.Single(model.Root.Attributes);
        Assert.Equal("Width", model.Root.Attributes[0].Name);
    }

    [Fact]
    public void PropertyElements_AreWalked_ButCarryNoOrdinal_AndAreNotElementDtos()
    {
        string text = $"<Window xmlns=\"{Ns}\"><Button><Button.Content><TextBlock Text=\"Hi\" /></Button.Content></Button></Window>";
        Assert.True(XamlDocumentModel.TryParse(text, out var model, out _));

        var button = model.Root.Children.Single();
        var propertyElement = button.Children.Single();
        Assert.True(propertyElement.IsPropertyElement);
        Assert.Null(propertyElement.Ordinal);
        Assert.Null(propertyElement.DtoType);

        var textBlock = propertyElement.Children.Single();
        Assert.False(textBlock.IsPropertyElement);
        Assert.NotNull(textBlock.Ordinal);
        Assert.Equal(typeof(MGUI.Core.UI.XAML.TextBlock), textBlock.DtoType);
    }

    [Fact]
    public void StyleAndSetterElements_ResolveToNoDtoType_AndCarryNoOrdinal()
    {
        string text = $"<Window xmlns=\"{Ns}\"><Window.Styles><Style TargetType=\"TextBlock\"><Setter Property=\"FontSize\" Value=\"10\" /></Style></Window.Styles></Window>";
        Assert.True(XamlDocumentModel.TryParse(text, out var model, out _));

        var stylesPropertyElement = model.Root.Children.Single();
        var style = stylesPropertyElement.Children.Single();
        Assert.Equal("Style", style.LocalName);
        Assert.Null(style.DtoType);
        Assert.Null(style.Ordinal);

        var setter = style.Children.Single();
        Assert.Equal("Setter", setter.LocalName);
        Assert.Null(setter.DtoType);
        Assert.Null(setter.Ordinal);
    }

    [Fact]
    public void FindDeepestNodeAt_ReturnsTheDeepestContainingNode_ForStartTag_TextContent_AndChild()
    {
        string text = $"<Window xmlns=\"{Ns}\">\n  <StackPanel>\n    <TextBlock Text=\"Hi\" />\n  </StackPanel>\n</Window>";
        Assert.True(XamlDocumentModel.TryParse(text, out var model, out _));

        var stackPanel = model.Root.Children.Single();
        var textBlock = stackPanel.Children.Single();

        int insideStackPanelStartTag = text.IndexOf("StackPanel", StringComparison.Ordinal) + 2;
        Assert.Same(stackPanel, model.FindDeepestNodeAt(insideStackPanelStartTag));

        int insideTextBlockStartTag = text.IndexOf("TextBlock", StringComparison.Ordinal) + 2;
        Assert.Same(textBlock, model.FindDeepestNodeAt(insideTextBlockStartTag));

        int stackPanelTextContent = text.IndexOf("<StackPanel>", StringComparison.Ordinal) + "<StackPanel>".Length;
        Assert.Same(stackPanel, model.FindDeepestNodeAt(stackPanelTextContent));

        int outsideRoot = text.Length; // right after the closing tag: outside the root's FullRange.
        Assert.Null(model.FindDeepestNodeAt(outsideRoot));
        Assert.Null(model.FindDeepestNodeAt(-1));
    }

    [Fact]
    public void TryGetNodeByOrdinal_ReturnsTheMatchingNode_AndFailsForAnUnknownOrdinal()
    {
        string text = $"<Window xmlns=\"{Ns}\"><StackPanel><TextBlock Text=\"Hi\" /></StackPanel></Window>";
        Assert.True(XamlDocumentModel.TryParse(text, out var model, out _));

        Assert.True(model.TryGetNodeByOrdinal(0, out var window));
        Assert.Equal("Window", window.LocalName);
        Assert.True(model.TryGetNodeByOrdinal(1, out var stackPanel));
        Assert.Equal("StackPanel", stackPanel.LocalName);
        Assert.True(model.TryGetNodeByOrdinal(2, out var textBlock));
        Assert.Equal("TextBlock", textBlock.LocalName);
        Assert.False(model.TryGetNodeByOrdinal(3, out _));
    }
}
