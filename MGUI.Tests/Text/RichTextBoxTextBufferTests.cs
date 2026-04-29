using MGUI.Core.UI.TextEditing;

namespace MGUI.Tests.Text;

public class RichTextBoxTextBufferTests
{
    [Fact]
    public void Constructor_NormalizesLineEndingsAndBuildsLineIndex()
    {
        MGTextBuffer buffer = new("alpha\r\nbeta\rgamma\n");

        Assert.Equal("alpha\nbeta\ngamma\n", buffer.Text);
        Assert.Equal(4, buffer.LineCount);
        Assert.Equal("alpha", buffer.GetLineText(0));
        Assert.Equal("beta", buffer.GetLineText(1));
        Assert.Equal("gamma", buffer.GetLineText(2));
        Assert.Equal(string.Empty, buffer.GetLineText(3));
    }

    [Fact]
    public void TryGetPosition_MapsAbsoluteIndicesToLineColumns()
    {
        MGTextBuffer buffer = new("ab\ncd\nef");

        Assert.True(buffer.TryGetPosition(0, out MGTextPosition start));
        Assert.Equal(new MGTextPosition(0, 0), start);

        Assert.True(buffer.TryGetPosition(2, out MGTextPosition lineEnd));
        Assert.Equal(new MGTextPosition(0, 2), lineEnd);

        Assert.True(buffer.TryGetPosition(3, out MGTextPosition secondLineStart));
        Assert.Equal(new MGTextPosition(1, 0), secondLineStart);

        Assert.True(buffer.TryGetPosition(buffer.Length, out MGTextPosition documentEnd));
        Assert.Equal(new MGTextPosition(2, 2), documentEnd);
    }

    [Fact]
    public void TryGetIndex_MapsLineColumnsToAbsoluteIndices()
    {
        MGTextBuffer buffer = new("ab\ncd\nef");

        Assert.True(buffer.TryGetIndex(new MGTextPosition(1, 1), out int index));
        Assert.Equal(4, index);

        Assert.True(buffer.TryGetIndex(new MGTextPosition(2, 2), out int endIndex));
        Assert.Equal(buffer.Length, endIndex);

        Assert.False(buffer.TryGetIndex(new MGTextPosition(9, 0), out _));
        Assert.False(buffer.TryGetIndex(new MGTextPosition(0, 9), out _));
    }

    [Fact]
    public void NormalizeRange_OrdersAndClampsRangesToDocumentBounds()
    {
        MGTextBuffer buffer = new("abcdef");

        MGTextRange range = buffer.NormalizeRange(new MGTextRange(99, -4));

        Assert.Equal(0, range.StartIndex);
        Assert.Equal(6, range.EndIndex);
    }

    [Fact]
    public void Replace_NormalizesInsertedTextAndRebuildsLineIndex()
    {
        MGTextBuffer buffer = new("one two");

        MGTextRange insertedRange = buffer.Replace(new MGTextRange(4, 7), "alpha\r\nbeta");

        Assert.Equal("one alpha\nbeta", buffer.Text);
        Assert.Equal(new MGTextRange(4, 14), insertedRange);
        Assert.Equal(2, buffer.LineCount);
        Assert.Equal("one alpha", buffer.GetLineText(0));
        Assert.Equal("beta", buffer.GetLineText(1));
    }

    [Fact]
    public void Delete_AcceptsInvertedRanges()
    {
        MGTextBuffer buffer = new("alpha beta gamma");

        buffer.Delete(new MGTextRange(10, 5));

        Assert.Equal("alpha gamma", buffer.Text);
    }

    [Fact]
    public void SplitRangeByLine_ConvertsMultiLineRangesIntoLineSpans()
    {
        MGTextBuffer buffer = new("ab\ncde\nf");

        IReadOnlyList<MGTextLineSpan> spans = buffer.SplitRangeByLine(new MGTextRange(1, 7));

        Assert.Collection(spans,
            first =>
            {
                Assert.Equal(0, first.LineIndex);
                Assert.Equal(1, first.StartColumn);
                Assert.Equal(2, first.EndColumn);
                Assert.Equal(new MGTextLineSpan(0, 1, 2, 1, 2), first);
            },
            second =>
            {
                Assert.Equal(1, second.LineIndex);
                Assert.Equal(0, second.StartColumn);
                Assert.Equal(3, second.EndColumn);
                Assert.Equal(new MGTextLineSpan(1, 3, 6, 0, 3), second);
            });
    }

    [Fact]
    public void SplitRangeByLine_PreservesEmptyRangesAtCaretPosition()
    {
        MGTextBuffer buffer = new("ab\ncd");

        IReadOnlyList<MGTextLineSpan> spans = buffer.SplitRangeByLine(MGTextRange.EmptyAt(4));

        MGTextLineSpan span = Assert.Single(spans);
        Assert.Equal(new MGTextLineSpan(1, 4, 4, 1, 1), span);
    }

    [Fact]
    public void GetVisualColumn_ExpandsTabsUsingConfiguredTabSize()
    {
        MGTextBuffer buffer = new("a\tbc");

        Assert.Equal(1, buffer.GetVisualColumn(new MGTextPosition(0, 1), 4));
        Assert.Equal(4, buffer.GetVisualColumn(new MGTextPosition(0, 2), 4));
        Assert.Equal(6, buffer.GetVisualColumn(new MGTextPosition(0, 4), 4));
    }
}