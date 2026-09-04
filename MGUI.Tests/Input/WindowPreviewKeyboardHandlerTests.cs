using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Input;

/// <summary>
/// Task 5 of the input-activation slice (previously deferred, now implemented - see decision 6 turned over
/// in the task brief and Docs/input-window-activation-design.md section 3.c): <see cref="MGWindow.PreviewKeyboardHandler"/>
/// is pumped every tick BEFORE this window's content children are updated, so a subscriber sees the tick's keys
/// whether keyboard focus is on the window itself or on any descendant (e.g. a focused <see cref="MGTextBox"/>).
/// It replaces the inert <see cref="MGWindow.WindowKeyboardHandler"/> (now <see cref="System.ObsoleteAttribute"/>),
/// which is pinned here as still receiving nothing, unchanged.
/// </summary>
public class WindowPreviewKeyboardHandlerTests
{
    [Fact]
    public void PreviewKeyboardHandler_ReceivesPressedBeforeFocusedDescendant_AndCanConsumeItFirst()
    {
        Harness h = Harness.Create();
        h.ClickInsideTextBox();

        bool previewSawCtrlF = false;
        bool previewSawUnhandledAtEntry = false;
        h.Window.PreviewKeyboardHandler.Pressed += (sender, e) =>
        {
            //  This is only a meaningful ordering proof because MGTextBox.KeyboardHandler.Pressed unconditionally
            //  marks the event handled once it runs (MGTextBox.cs, e.SetHandledBy(this, false) after HandleKeyPress).
            //  If PreviewKeyboardHandler were pumped AFTER content children instead of before, the textbox would
            //  already have handled this key by the time this subscriber ran, and e.IsHandled would be true here.
            previewSawUnhandledAtEntry = !e.IsHandled;

            if (e.Tracker.IsControlDown && e.Key == Keys.F)
            {
                previewSawCtrlF = true;
                e.SetHandledBy(h.Window, false);
            }
        };

        h.KeyFrame(Keys.LeftControl, Keys.F);

        Assert.True(previewSawCtrlF);
        Assert.True(previewSawUnhandledAtEntry);
        //  Consumed by the window-level preview before MGTextBox's own KeyboardHandler.Pressed ever ran
        //  (KeyboardHandler.InvokeQueuedEvents only delivers Pressed when !IsHandled, see KeyboardHandler.cs:172),
        //  so the textbox never typed the 'f' character.
        Assert.Equal(string.Empty, h.TextBox.Text);
    }

    [Fact]
    public void PreviewKeyboardHandler_ReproducesMotivatingPattern_WindowShortcutWorksWhileTextBoxFocused_PlainTypingKeyIsNotStolen()
    {
        Harness h = Harness.Create();
        h.ClickInsideTextBox();

        int ctrlFShortcutInvocations = 0;
        h.Window.PreviewKeyboardHandler.Pressed += (sender, e) =>
        {
            if (e.IsHandled)
            {
                return;
            }

            //  Recognized window-level shortcuts are handled regardless of focus.
            if (e.Tracker.IsControlDown && e.Key == Keys.F)
            {
                ctrlFShortcutInvocations++;
                e.SetHandledBy(h.Window, true);
                return;
            }

            //  Anything else is left alone while an ITextEntryHost has focus, so plain typing keys are never stolen.
            if (h.Desktop.FocusedKeyboardHandler is MGTextBox)
            {
                return;
            }
        };

        //  Ctrl+F: handled at window level even though the textbox has keyboard focus.
        h.KeyFrame(Keys.LeftControl, Keys.F);
        Assert.Equal(1, ctrlFShortcutInvocations);
        Assert.Equal(string.Empty, h.TextBox.Text);

        //  A plain typing key on the next tick: not a recognized shortcut, and the subscriber returns early
        //  because the textbox has focus, so the key reaches the textbox exactly like it would without the preview.
        h.KeyFrame();
        h.KeyFrame(Keys.A);
        Assert.Equal(1, ctrlFShortcutInvocations);
        Assert.Equal("a", h.TextBox.Text);
    }

    [Fact]
    public void WindowKeyboardHandler_StillReceivesNothing_WhileDescendantTextBoxIsFocused()
    {
        Harness h = Harness.Create();
        h.ClickInsideTextBox();

        bool obsoleteHandlerSawAnything = false;
#pragma warning disable CS0618 // Pinning that the [Obsolete] WindowKeyboardHandler is unchanged: it still receives nothing.
        h.Window.WindowKeyboardHandler.Pressed += (sender, e) => obsoleteHandlerSawAnything = true;
#pragma warning restore CS0618

        h.KeyFrame(Keys.A);

        Assert.False(obsoleteHandlerSawAnything);
        Assert.Equal("a", h.TextBox.Text);
    }

    /// <summary>Mirrors the harness style used by <c>TextBoxNativeTabInputRegressionTests</c>: a real pointer click inside
    /// the textbox both focuses it and places its caret, which <see cref="MGTextBox"/>'s printable-key insertion path requires.</summary>
    private sealed class Harness
    {
        public GraphTestRuntime Runtime { get; }
        public MGDesktop Desktop { get; }
        public MGWindow Window { get; }
        public MGTextBox TextBox { get; }
        private int _frame;

        private Harness(GraphTestRuntime runtime, MGDesktop desktop, MGWindow window, MGTextBox textBox)
        {
            Runtime = runtime;
            Desktop = desktop;
            Window = window;
            TextBox = textBox;
        }

        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, 400, 200) { WindowStyle = WindowStyle.None };
            MGTextBox textBox = new(window);
            window.SetContent(textBox);
            desktop.Windows.Add(window);

            Harness h = new(runtime, desktop, window, textBox);
            h.KeyFrame();
            h.KeyFrame();
            Assert.False(textBox.LayoutBounds.IsEmpty);
            return h;
        }

        public void ClickInsideTextBox()
        {
            Rectangle bounds = TextBox.LayoutBounds;
            int x = bounds.Center.X;
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
