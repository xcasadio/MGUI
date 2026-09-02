using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Input;

/// <summary>
/// Regression for the crash reproduced in the FocusInputReview sample: with native text input wired
/// (GameWindow.TextInput -> KeyboardTracker.QueueTextInput), pressing Tab in a focused <see cref="MGTextBox"/> delivered a raw
/// 1-character '\t' while the textbox assumes a Tab inserts MGTextRun.TabSpacesCount characters and moves the caret accordingly;
/// the next Backspace/Delete then sliced the backing text out of range (ArgumentOutOfRangeException in HandleKeyPress).
/// The harness reproduces the real interaction: a mouse click inside the textbox (pointer focus + caret placement), then keys.
/// </summary>
public class TextBoxNativeTabInputRegressionTests
{
    [Fact]
    public void NativeTab_ThenBackspace_InClickedTextBox_DoesNotThrow_AndEditsFourSpaceTab()
    {
        Harness h = Harness.Create(textBox => { });
        h.ClickInsideTextBox();

        // Tab press: the host forwards the native '\t' character for Keys.Tab on the tick the key goes down.
        h.Runtime.Input.Keyboard.QueueTextInput('\t', Keys.Tab);
        h.KeyFrame(Keys.Tab);
        Assert.Equal("    ", h.TextBox.Text);
        h.KeyFrame();

        // Backspace used to throw ArgumentOutOfRangeException here (caret desynchronized from the backing text).
        Exception? exception = Record.Exception(() => h.KeyFrame(Keys.Back));

        Assert.Null(exception);
        Assert.Equal("   ", h.TextBox.Text);
    }

    [Fact]
    public void NativeCarriageReturn_InClickedMultilineTextBox_InsertsLineFeed()
    {
        Harness h = Harness.Create(textBox => textBox.AcceptsReturn = true);
        h.ClickInsideTextBox();

        // Windows reports Enter as '\r' through GameWindow.TextInput; the text model uses '\n' (KeyboardTracker.DefaultEnterValue).
        h.Runtime.Input.Keyboard.QueueTextInput('\r', Keys.Enter);
        h.KeyFrame(Keys.Enter);

        Assert.Equal("\n", h.TextBox.Text);
    }

    [Fact]
    public void ProgrammaticTabCharacter_ThenBackspaceAtEnd_DoesNotThrow()
    {
        // Defensive clamp in the Backspace/Delete path: the displayed text expands '\t' (MGTextRun.TabReplacement), so the caret
        // index can exceed the backing field; slicing must clamp like the insertion path already does instead of throwing.
        Harness h = Harness.Create(textBox => textBox.Text = "ab\tcd");
        h.ClickInsideTextBox(nearRightEdge: true);
        h.KeyFrame(Keys.End);
        h.KeyFrame();

        Exception? exception = Record.Exception(() => h.KeyFrame(Keys.Back));

        Assert.Null(exception);
        Assert.Equal("ab\tcd".Length - 1, h.TextBox.Text.Length);
    }

    private sealed class Harness
    {
        public GraphTestRuntime Runtime { get; }
        public MGDesktop Desktop { get; }
        public MGTextBox TextBox { get; }
        private int _frame;

        private Harness(GraphTestRuntime runtime, MGDesktop desktop, MGTextBox textBox)
        {
            Runtime = runtime;
            Desktop = desktop;
            TextBox = textBox;
        }

        public static Harness Create(Action<MGTextBox> configure)
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, 400, 200) { WindowStyle = WindowStyle.None };
            MGTextBox textBox = new(window);
            configure(textBox);
            window.SetContent(textBox);
            desktop.Windows.Add(window);

            Harness h = new(runtime, desktop, textBox);
            h.KeyFrame();
            h.KeyFrame();
            Assert.False(textBox.LayoutBounds.IsEmpty);
            return h;
        }

        /// <summary>Real pointer interaction: press + release inside the textbox, which queues pointer focus and places the caret.</summary>
        public void ClickInsideTextBox(bool nearRightEdge = false)
        {
            Rectangle bounds = TextBox.LayoutBounds;
            int x = nearRightEdge ? bounds.Right - 2 : bounds.Center.X;
            int y = bounds.Center.Y;
            Frame(MouseAt(x, y, ButtonState.Pressed), new KeyboardState());
            Frame(MouseAt(x, y, ButtonState.Released), new KeyboardState());
            Frame(MouseAt(x, y, ButtonState.Released), new KeyboardState());

            Assert.Same(TextBox, Desktop.FocusedKeyboardHandler);
            Assert.True(TextBox.Caret.HasPosition);
        }

        public void KeyFrame(params Keys[] pressedKeys)
            => Frame(MouseAt(0, 0, ButtonState.Released), new KeyboardState(pressedKeys));

        private void Frame(MouseState mouse, KeyboardState keyboard)
        {
            _frame++;
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * _frame), TimeSpan.FromMilliseconds(16), mouse, keyboard));
            Desktop.Update();
        }

        private static MouseState MouseAt(int x, int y, ButtonState left)
            => new(x, y, 0, left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
    }
}
