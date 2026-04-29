using MGUI.Core.UI.TextEditing;

namespace MGUI.Tests.Text;

public class RichTextBoxEditingControllerTests
{
    [Fact]
    public void InsertText_ReplacesSelectionAndMovesCaretToInsertedEnd()
    {
        MGRichTextEditController controller = new("alpha beta");
        controller.Select(new MGTextRange(6, 10));

        MGTextEditResult result = controller.InsertText("gamma");

        Assert.Equal("alpha gamma", controller.Buffer.Text);
        Assert.Equal(new MGTextRange(6, 11), result.InsertedRange);
        Assert.False(controller.Selection.HasSelection);
        Assert.Equal(11, controller.Selection.CaretIndex);
    }

    [Fact]
    public void Backspace_DeletesSelectedTextOrPreviousCharacter()
    {
        MGRichTextEditController controller = new("alpha beta");
        controller.Select(new MGTextRange(5, 10));

        Assert.True(controller.Backspace());
        Assert.Equal("alpha", controller.Buffer.Text);
        Assert.Equal(5, controller.Selection.CaretIndex);

        Assert.True(controller.Backspace());
        Assert.Equal("alph", controller.Buffer.Text);
        Assert.Equal(4, controller.Selection.CaretIndex);
    }

    [Fact]
    public void DeleteForward_DeletesSelectedTextOrNextCharacter()
    {
        MGRichTextEditController controller = new("abc");
        controller.MoveCaret(1);

        Assert.True(controller.DeleteForward());
        Assert.Equal("ac", controller.Buffer.Text);
        Assert.Equal(1, controller.Selection.CaretIndex);

        controller.SelectAll();
        Assert.True(controller.DeleteForward());
        Assert.Equal(string.Empty, controller.Buffer.Text);
        Assert.Equal(0, controller.Selection.CaretIndex);
    }

    [Fact]
    public void UndoRedo_RestoresTextAndSelection()
    {
        MGRichTextEditController controller = new("one");
        controller.MoveCaret(3);
        controller.InsertText(" two");

        Assert.Equal("one two", controller.Buffer.Text);
        Assert.True(controller.TryUndo());
        Assert.Equal("one", controller.Buffer.Text);
        Assert.Equal(3, controller.Selection.CaretIndex);

        Assert.True(controller.TryRedo());
        Assert.Equal("one two", controller.Buffer.Text);
        Assert.Equal(7, controller.Selection.CaretIndex);
    }
}