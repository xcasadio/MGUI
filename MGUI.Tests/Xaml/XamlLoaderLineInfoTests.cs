using MGUI.Core.UI.XAML;

namespace MGUI.Tests.Xaml;

/// <summary>The line info the reader captures is forwarded to the object writer (<see cref="XAMLParser"/>'s private
/// <c>ParseWithSourcePositions</c>), so a failure the writer raises while building an object -- a value that cannot be converted, a
/// setter that throws -- carries a line and a column. Empirically, for both failure kinds the column points at the first character of
/// the attribute NAME whose value triggered the failure (not the element name, not the value, not the node after it): see
/// <see cref="ConversionFailure_SameLineAsTag_ColumnIsAttributeNameStart"/> and <see cref="ThrowingSetter_ColumnIsAttributeNameStart"/>.</summary>
public class XamlLoaderLineInfoTests
{
    private const string Ns = "clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core";

    /// <summary>Locates <paramref name="marker"/> (for example <c>"Width="</c>) in <paramref name="text"/> and returns the 1-based
    /// (line, column) of its first character -- computed independently of the parser under test, by counting the newlines and the
    /// offset from the last one.</summary>
    private static (int Line, int Column) LocatePosition(string text, string marker)
    {
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Marker not found: {marker}");

        var line = 1;
        var lastNewline = -1;
        for (var i = 0; i < index; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lastNewline = i;
            }
        }

        var column = index - lastNewline;
        return (line, column);
    }

    private static XamlLoaderException ParseStrictExpectingFailure(string xaml, bool replaceLinebreakLiterals = true)
    {
        return Assert.Throws<XamlLoaderException>(() =>
            XAMLParser.ParseWindowDefinition(XamlDocumentSource.FromString(xaml, "probe.xaml"), null, XamlLoaderMode.Strict, false, replaceLinebreakLiterals));
    }

    [Fact]
    public void ConversionFailure_SameLineAsTag_ColumnIsAttributeNameStart()
    {
        string xaml =
            $"<Window xmlns=\"{Ns}\" Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\">\n" +
            "    <StackPanel>\n" +
            "        <Button Width=\"abc\" Content=\"Hi\" />\n" +
            "    </StackPanel>\n" +
            "</Window>\n";

        var error = ParseStrictExpectingFailure(xaml);

        var (expectedLine, expectedColumn) = LocatePosition(xaml, "Width=\"abc\"");
        Assert.Equal(3, expectedLine);
        Assert.Equal(expectedLine, error.Diagnostic.LineNumber);
        Assert.Equal(expectedColumn, error.Diagnostic.LinePosition);
    }

    [Fact]
    public void ConversionFailure_AttributeOnItsOwnLine_ColumnIsAttributeNameStart()
    {
        string xaml =
            $"<Window xmlns=\"{Ns}\" Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\">\n" +
            "    <StackPanel>\n" +
            "        <Button\n" +
            "            Width=\"abc\"\n" +
            "            Content=\"Hi\" />\n" +
            "    </StackPanel>\n" +
            "</Window>\n";

        var error = ParseStrictExpectingFailure(xaml);

        var (expectedLine, expectedColumn) = LocatePosition(xaml, "Width=\"abc\"");
        Assert.Equal(4, expectedLine);
        Assert.Equal(expectedLine, error.Diagnostic.LineNumber);
        Assert.Equal(expectedColumn, error.Diagnostic.LinePosition);
    }

    [Fact]
    public void ThrowingSetter_ColumnIsAttributeNameStart()
    {
        string xaml =
            $"<Window xmlns=\"{Ns}\" Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\">\n" +
            "    <Button Content=\"Hi\">\n" +
            "        <Button.Transitions>\n" +
            "            <Transition Property=\"Nope\" Duration=\"0.1\" />\n" +
            "        </Button.Transitions>\n" +
            "    </Button>\n" +
            "</Window>\n";

        var error = ParseStrictExpectingFailure(xaml);

        var (expectedLine, expectedColumn) = LocatePosition(xaml, "Property=\"Nope\"");
        Assert.Equal(4, expectedLine);
        Assert.NotNull(error.Diagnostic.LineNumber);
        Assert.NotNull(error.Diagnostic.LinePosition);
        Assert.Equal(expectedLine, error.Diagnostic.LineNumber);
        Assert.Equal(expectedColumn, error.Diagnostic.LinePosition);
    }

    /// <summary>Same failing element as <see cref="ConversionFailure_SameLineAsTag_ColumnIsAttributeNameStart"/>, but with a literal
    /// backslash-n earlier on the same line: <see cref="XAMLParser"/>'s <c>ReplaceLinebreakLiterals</c> pre-processing turns each 2-char
    /// <c>\n</c> occurrence into the 6-char <c>&amp;#x0a;</c> XML entity before the document is handed to the reader, so the reported
    /// column is 4 characters further right per occurrence, measured on that PREPARED markup (not on the original source text).</summary>
    [Fact]
    public void ConversionFailure_WithEarlierBackslashN_ColumnIsOnPreparedMarkup()
    {
        string rawXaml =
            $"<Window xmlns=\"{Ns}\" Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\">\n" +
            "    <StackPanel>\n" +
            "        <TextBlock Text=\"a\\nb\" /><Button Width=\"abc\" Content=\"Hi\" />\n" +
            "    </StackPanel>\n" +
            "</Window>\n";
        string preparedXaml = rawXaml.Replace(@"\n", "&#x0a;");

        var error = ParseStrictExpectingFailure(rawXaml);

        var (rawLine, rawColumn) = LocatePosition(rawXaml, "Width=\"abc\"");
        var (preparedLine, preparedColumn) = LocatePosition(preparedXaml, "Width=\"abc\"");
        Assert.Equal(3, rawLine);
        Assert.Equal(rawLine, preparedLine);
        Assert.Equal(preparedColumn, rawColumn + 4); // one "\n" -> "&#x0a;" occurrence before the failing attribute: +4 columns

        Assert.Equal(preparedLine, error.Diagnostic.LineNumber);
        Assert.Equal(preparedColumn, error.Diagnostic.LinePosition);
    }

    [Fact]
    public void CompatibilityMode_RawObjectWriterException_HasNonZeroLineNumber()
    {
        string xaml =
            $"<Window xmlns=\"{Ns}\" Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\">\n" +
            "    <StackPanel>\n" +
            "        <Button Width=\"abc\" Content=\"Hi\" />\n" +
            "    </StackPanel>\n" +
            "</Window>\n";

        Exception caught = Assert.ThrowsAny<Exception>(() =>
            XAMLParser.ParseWindowDefinition(XamlDocumentSource.FromString(xaml, "probe.xaml"), null, XamlLoaderMode.Compatibility, false, true));
        Assert.IsNotType<XamlLoaderException>(caught);

        var lineProperty = caught.GetType().GetProperty("LineNumber");
        Assert.NotNull(lineProperty);
        var lineNumber = (int)lineProperty.GetValue(caught);
        Assert.NotEqual(0, lineNumber);

        var (expectedLine, _) = LocatePosition(xaml, "Width=\"abc\"");
        Assert.Equal(expectedLine, lineNumber);
    }
}
