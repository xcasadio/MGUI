using MGUI.Editor.Document;

namespace MGUI.Tests.Editor;

/// <summary>X1: <see cref="XamlAttributeEdit"/> produces minimal edits that re-parse to the expected value, and never touch the rest of
/// the text outside the edited range.</summary>
public class XamlAttributeEditTests
{
    private const string Ns = "clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core";

    private static XamlDocumentNode Parse(string text, out string parsedText)
    {
        parsedText = text;
        Assert.True(XamlDocumentModel.TryParse(text, out var model, out var error), error?.Message);
        return model.Root.Children.Single();
    }

    [Theory]
    [InlineData('"')]
    [InlineData('\'')]
    public void SetAttribute_Replace_KeepsTheExistingQuoteCharacter(char quote)
    {
        string text = $"<Window xmlns={quote}{Ns}{quote}><Button Width={quote}100{quote} /></Window>";
        var button = Parse(text, out _);

        var edit = XamlAttributeEdit.SetAttribute(button, "Width", "250");
        string result = XamlAttributeEdit.Apply(text, edit);

        Assert.True(XamlDocumentModel.TryParse(result, out var model, out _));
        var edited = model.Root.Children.Single().Attributes.Single(a => a.Name == "Width");
        Assert.Equal("250", edited.Value);
        Assert.Equal(quote, edited.QuoteChar);

        //  The rest of the text outside the edited range is byte-identical.
        Assert.Equal(text[..edit.Range.StartIndex], result[..edit.Range.StartIndex]);
        Assert.Equal(text[edit.Range.EndIndex..], result[(edit.Range.StartIndex + edit.Text.Length)..]);
    }

    [Fact]
    public void SetAttribute_Insert_IntoASelfClosingTagWithoutASlashSpace()
    {
        string text = $"<Window xmlns=\"{Ns}\"><Button/></Window>";
        var button = Parse(text, out _);

        var edit = XamlAttributeEdit.SetAttribute(button, "Width", "120");
        string result = XamlAttributeEdit.Apply(text, edit);

        Assert.Equal($"<Window xmlns=\"{Ns}\"><Button Width=\"120\"/></Window>", result);
        Assert.True(XamlDocumentModel.TryParse(result, out var model, out _));
        Assert.Equal("120", model.Root.Children.Single().Attributes.Single(a => a.Name == "Width").Value);
    }

    [Fact]
    public void SetAttribute_Insert_IntoASelfClosingTagWithASlashSpace()
    {
        string text = $"<Window xmlns=\"{Ns}\"><Button /></Window>";
        var button = Parse(text, out _);

        var edit = XamlAttributeEdit.SetAttribute(button, "Width", "120");
        string result = XamlAttributeEdit.Apply(text, edit);

        Assert.Equal($"<Window xmlns=\"{Ns}\"><Button Width=\"120\" /></Window>", result);
    }

    [Fact]
    public void SetAttribute_Insert_IntoATagWithChildren()
    {
        string text = $"<Window xmlns=\"{Ns}\"><Button><TextBlock Text=\"Hi\" /></Button></Window>";
        var button = Parse(text, out _);

        var edit = XamlAttributeEdit.SetAttribute(button, "Width", "120");
        string result = XamlAttributeEdit.Apply(text, edit);

        Assert.Equal($"<Window xmlns=\"{Ns}\"><Button Width=\"120\"><TextBlock Text=\"Hi\" /></Button></Window>", result);
    }

    [Fact]
    public void SetAttribute_Insert_IntoAMultiLineStartTag()
    {
        string text = $"<Window xmlns=\"{Ns}\">\n  <Button\n      Name=\"B\"\n      />\n</Window>\n";
        var button = Parse(text, out _);

        var edit = XamlAttributeEdit.SetAttribute(button, "Width", "120");
        string result = XamlAttributeEdit.Apply(text, edit);

        Assert.True(XamlDocumentModel.TryParse(result, out var model, out var error), error?.Message);
        var edited = model.Root.Children.Single();
        Assert.Equal("120", edited.Attributes.Single(a => a.Name == "Width").Value);
        Assert.Equal("B", edited.Attributes.Single(a => a.Name == "Name").Value);
    }

    [Fact]
    public void RemoveAttribute_First_Middle_And_Last_RemovesTheAttributeAndItsLeadingWhitespace()
    {
        string text = $"<Window xmlns=\"{Ns}\"><Button A=\"1\" B=\"2\" C=\"3\" /></Window>";
        var button = Parse(text, out _);

        Assert.True(XamlAttributeEdit.TryRemoveAttribute(button, "A", out var removeFirst));
        string afterFirst = XamlAttributeEdit.Apply(text, removeFirst);
        Assert.True(XamlDocumentModel.TryParse(afterFirst, out var afterFirstModel, out _));
        Assert.Equal(new[] { "B", "C" }, afterFirstModel.Root.Children.Single().Attributes.Select(a => a.Name));

        Assert.True(XamlAttributeEdit.TryRemoveAttribute(button, "B", out var removeMiddle));
        string afterMiddle = XamlAttributeEdit.Apply(text, removeMiddle);
        Assert.True(XamlDocumentModel.TryParse(afterMiddle, out var afterMiddleModel, out _));
        Assert.Equal(new[] { "A", "C" }, afterMiddleModel.Root.Children.Single().Attributes.Select(a => a.Name));

        Assert.True(XamlAttributeEdit.TryRemoveAttribute(button, "C", out var removeLast));
        string afterLast = XamlAttributeEdit.Apply(text, removeLast);
        Assert.True(XamlDocumentModel.TryParse(afterLast, out var afterLastModel, out _));
        Assert.Equal(new[] { "A", "B" }, afterLastModel.Root.Children.Single().Attributes.Select(a => a.Name));
        Assert.DoesNotContain(" C=\"3\"", afterLast);
        Assert.Equal($"<Window xmlns=\"{Ns}\"><Button A=\"1\" B=\"2\" /></Window>", afterLast);
    }

    [Fact]
    public void RemoveAttribute_OnAMultiLineStartTag_RemovesItsOwnLineIndentation()
    {
        string text = $"<Window xmlns=\"{Ns}\">\n  <Button\n      Name=\"B\"\n      Width=\"120\" />\n</Window>\n";
        var button = Parse(text, out _);

        Assert.True(XamlAttributeEdit.TryRemoveAttribute(button, "Width", out var edit));
        string result = XamlAttributeEdit.Apply(text, edit);

        Assert.True(XamlDocumentModel.TryParse(result, out var model, out var error), error?.Message);
        var edited = model.Root.Children.Single();
        Assert.Single(edited.Attributes);
        Assert.Equal("Name", edited.Attributes[0].Name);
    }

    [Fact]
    public void RemoveAttribute_Absent_ReturnsFalse()
    {
        string text = $"<Window xmlns=\"{Ns}\"><Button A=\"1\" /></Window>";
        var button = Parse(text, out _);

        Assert.False(XamlAttributeEdit.TryRemoveAttribute(button, "DoesNotExist", out var edit));
        Assert.Equal(default, edit);
    }

    [Theory]
    [InlineData("A & B", "A &amp; B")]
    [InlineData("A < B", "A &lt; B")]
    [InlineData("A > B", "A > B")]
    public void SetAttribute_Escapes_Ampersand_LessThan_ButLeavesGreaterThanAsIs(string value, string expectedEscaped)
    {
        string text = $"<Window xmlns=\"{Ns}\"><Button /></Window>";
        var button = Parse(text, out _);

        var edit = XamlAttributeEdit.SetAttribute(button, "Content", value);
        Assert.Contains(expectedEscaped, edit.Text);

        string result = XamlAttributeEdit.Apply(text, edit);
        Assert.True(XamlDocumentModel.TryParse(result, out var model, out var error), error?.Message);
        Assert.Equal(value, model.Root.Children.Single().Attributes.Single(a => a.Name == "Content").Value);
    }

    [Fact]
    public void SetAttribute_Escapes_TheQuoteCharacterInUse_WhenReplacingASingleQuotedValue()
    {
        string text = $"<Window xmlns=\"{Ns}\"><Button Content='old' /></Window>";
        var button = Parse(text, out _);

        var edit = XamlAttributeEdit.SetAttribute(button, "Content", "it's here");
        string result = XamlAttributeEdit.Apply(text, edit);

        Assert.Contains("&apos;", result);
        Assert.True(XamlDocumentModel.TryParse(result, out var model, out var error), error?.Message);
        Assert.Equal("it's here", model.Root.Children.Single().Attributes.Single(a => a.Name == "Content").Value);
    }

    [Fact]
    public void Apply_ThrowsOnNullText()
    {
        Assert.Throws<ArgumentNullException>(() => XamlAttributeEdit.Apply(null, default));
    }

    [Fact]
    public void SetAttribute_ThrowsOnNullNodeOrBlankName()
    {
        string text = $"<Window xmlns=\"{Ns}\"><Button /></Window>";
        var button = Parse(text, out _);

        Assert.Throws<ArgumentNullException>(() => XamlAttributeEdit.SetAttribute(null, "Width", "1"));
        Assert.Throws<ArgumentException>(() => XamlAttributeEdit.SetAttribute(button, "", "1"));
    }
}
