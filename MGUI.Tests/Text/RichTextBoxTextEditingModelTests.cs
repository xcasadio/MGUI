using MGUI.Core.UI.TextEditing;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Text;

public class RichTextBoxTextEditingModelTests
{
    [Fact]
    public void ApplyEdit_ReturnsRemovedInsertedRangesAndVersion()
    {
        MGTextBuffer buffer = new("alpha beta");

        MGTextEditResult result = buffer.ApplyEdit(new MGTextRange(6, 10), "gamma");

        Assert.Equal("alpha gamma", buffer.Text);
        Assert.Equal(new MGTextRange(6, 10), result.RemovedRange);
        Assert.Equal(new MGTextRange(6, 11), result.InsertedRange);
        Assert.Equal("beta", result.RemovedText);
        Assert.Equal("gamma", result.InsertedText);
        Assert.Equal(1, result.Delta);
        Assert.Equal(buffer.Version, result.Version);
        Assert.Equal(11, result.CaretIndexAfterEdit);
    }

    [Fact]
    public void CreateSnapshot_CapturesImmutableTextAndLineStarts()
    {
        MGTextBuffer buffer = new("one\ntwo");
        MGTextBufferSnapshot snapshot = buffer.CreateSnapshot();

        buffer.Insert(buffer.Length, "\nthree");

        Assert.Equal("one\ntwo", snapshot.Text);
        Assert.Equal(2, snapshot.LineCount);
        Assert.Equal(0, snapshot.LineStarts[0]);
        Assert.Equal(4, snapshot.LineStarts[1]);
    }

    [Fact]
    public void RestoreSnapshot_RestoresTextThroughNormalEditPath()
    {
        MGTextBuffer buffer = new("one\ntwo");
        MGTextBufferSnapshot snapshot = buffer.CreateSnapshot();

        buffer.SetText("changed");
        bool restored = buffer.RestoreSnapshot(snapshot);

        Assert.True(restored);
        Assert.Equal("one\ntwo", buffer.Text);
        Assert.Equal(2, buffer.LineCount);
    }

    [Fact]
    public void SelectionState_TracksAnchorActiveAndClampsToDocument()
    {
        MGTextSelectionState selection = MGTextSelectionState.EmptyAt(2).ExtendTo(99, 8);

        Assert.True(selection.HasSelection);
        Assert.Equal(2, selection.AnchorIndex);
        Assert.Equal(8, selection.ActiveIndex);
        Assert.Equal(new MGTextRange(2, 8), selection.Range);

        MGTextSelectionState moved = selection.MoveCaret(4, 8);
        Assert.False(moved.HasSelection);
        Assert.Equal(4, moved.CaretIndex);
    }

    [Fact]
    public void StyledTextSpan_ClampsRangeWithoutChangingStyleClassification()
    {
        MGRichTextStyle keywordStyle = new(Color.CornflowerBlue, null, IsBold: true);
        MGStyledTextSpan span = new(new MGTextRange(-5, 99), keywordStyle, "keyword");

        MGStyledTextSpan clamped = span.Clamp(12);

        Assert.Equal(new MGTextRange(0, 12), clamped.Range);
        Assert.Equal(keywordStyle, clamped.Style);
        Assert.Equal("keyword", clamped.Classification);
    }

    [Fact]
    public void RichTextStyle_MergeOver_UsesOverlayValuesForNullableColorsAndFlags()
    {
        MGRichTextStyle baseStyle = new(Color.White, Color.Black, IsBold: false, IsItalic: true);
        MGRichTextStyle overlay = new(Foreground: Color.Yellow, IsBold: true);

        MGRichTextStyle merged = overlay.MergeOver(baseStyle);

        Assert.Equal(Color.Yellow, merged.Foreground);
        Assert.Equal(Color.Black, merged.Background);
        Assert.True(merged.IsBold);
        Assert.True(merged.IsItalic);
    }
}