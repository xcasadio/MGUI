using MGUI.Core.UI;
using MGUI.Core.UI.TextEditing;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Text;

/// <summary>Docs/Tasks/xaml-editor-tasks.md adds an undo hook (<c>MGTextBox.PushUndoState</c>, private
/// protected) so a programmatic edit applied through <see cref="MGRichTextBox.ApplyTextEdit"/> -- which is also what
/// completion acceptance goes through -- becomes undoable. These tests fail without that hook: before it existed,
/// <see cref="MGTextBox.TryUndo"/> had nothing on the stack to pop after a programmatic edit.</summary>
public class RichTextBoxProgrammaticEditUndoTests
{
    private static (GraphTestRuntime Runtime, MGDesktop Desktop, MGRichTextBox Editor) CreateEditor()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None };
        MGRichTextBox editor = new(window);
        window.SetContent(editor);
        desktop.Windows.Add(window);
        Frame(runtime, desktop, 0);
        Frame(runtime, desktop, 1);
        return (runtime, desktop, editor);
    }

    /// <summary>Runs one desktop update, so a caret index set right before it is resolved against a built text
    /// layout (an <see cref="MGRichTextBox.CaretIndex"/> set before the first layout pass cannot be resolved to a
    /// visual position yet).</summary>
    private static void Frame(GraphTestRuntime runtime, MGDesktop desktop, int frameIndex)
    {
        MouseState mouse = new(1, 1, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * frameIndex), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
        desktop.Update();
    }

    [Fact]
    public void ApplyTextEdit_ThenTryUndo_RestoresTextAndCaret()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGRichTextBox editor) = CreateEditor();
        editor.SetText("Hello world");
        Frame(runtime, desktop, 2);
        editor.CaretIndex = 5;
        Frame(runtime, desktop, 3);
        Assert.Equal(5, editor.CaretIndex);

        editor.ApplyTextEdit(MGTextRange.EmptyAt(5), " there");
        Frame(runtime, desktop, 4);
        Assert.Equal("Hello there world", editor.Text);

        bool undone = editor.TryUndo();
        Frame(runtime, desktop, 5);

        Assert.True(undone);
        Assert.Equal("Hello world", editor.Text);
        Assert.Equal(5, editor.CaretIndex);
    }

    [Fact]
    public void ApplyTextEdit_Twice_ThenTwoUndos_RestoresEachPriorState_AndRedoReappliesBoth()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGRichTextBox editor) = CreateEditor();
        editor.SetText("abc");
        Frame(runtime, desktop, 2);

        editor.ApplyTextEdit(MGTextRange.EmptyAt(3), "d");
        Frame(runtime, desktop, 3);
        Assert.Equal("abcd", editor.Text);
        editor.ApplyTextEdit(MGTextRange.EmptyAt(4), "e");
        Frame(runtime, desktop, 4);
        Assert.Equal("abcde", editor.Text);

        Assert.True(editor.TryUndo());
        Frame(runtime, desktop, 5);
        Assert.Equal("abcd", editor.Text);
        Assert.True(editor.TryUndo());
        Frame(runtime, desktop, 6);
        Assert.Equal("abc", editor.Text);

        Assert.True(editor.TryRedo());
        Frame(runtime, desktop, 7);
        Assert.Equal("abcd", editor.Text);
        Assert.True(editor.TryRedo());
        Frame(runtime, desktop, 8);
        Assert.Equal("abcde", editor.Text);
    }

    [Fact]
    public void AcceptCompletion_GoesThroughApplyTextEdit_SoItIsUndoableToo()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGRichTextBox editor) = CreateEditor();
        editor.SetText("cla");
        Frame(runtime, desktop, 2);
        editor.CaretIndex = 3;
        Frame(runtime, desktop, 3);

        MGRichTextCompletionItem item = new("class");
        bool accepted = editor.AcceptCompletion(item, new MGTextRange(0, 3));
        Frame(runtime, desktop, 4);

        Assert.True(accepted);
        Assert.Equal("class", editor.Text);

        Assert.True(editor.TryUndo());
        Frame(runtime, desktop, 5);
        Assert.Equal("cla", editor.Text);
    }
}
