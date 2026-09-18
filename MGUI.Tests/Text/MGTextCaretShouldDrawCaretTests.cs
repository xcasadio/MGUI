using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Text;

/// <summary>The pure draw decision <see cref="MGTextCaret.ShouldDrawCaret"/>: one test per row of the truth table in
/// this slice's brief (Docs/Tasks/xaml-editor-tasks.md, section X4), plus the blink/no-blink behaviour over time and
/// the non-positive <c>blinkRate</c> guard. <see cref="MGTextCaret.Draw"/> and <see cref="MGTextCaret.IsCurrentlyVisible"/>
/// both call this same method, so these tests exercise the one decision both callers share.</summary>
public class MGTextCaretShouldDrawCaretTests
{
    private static readonly TimeSpan DefaultBlinkRate = TimeSpan.FromSeconds(0.5);

    // -- Row 1: no position -> never drawn, whatever the rest --

    [Theory]
    [InlineData(false, false, 0.0)]
    [InlineData(false, true, 0.0)]
    [InlineData(true, false, 0.0)]
    [InlineData(true, true, 0.4)]
    public void NoPosition_IsNeverDrawn(bool hasKeyboardFocus, bool showWhenUnfocused, double secondsShown)
    {
        Assert.False(MGTextCaret.ShouldDrawCaret(false, hasKeyboardFocus, showWhenUnfocused, secondsShown, DefaultBlinkRate));
    }

    // -- Row 2: with keyboard focus, the blink gate exactly as before --

    [Fact]
    public void HasPositionAndFocus_WithinTheFirstBlinkWindow_IsShown()
    {
        Assert.True(MGTextCaret.ShouldDrawCaret(true, true, false, 0.0, DefaultBlinkRate));
        Assert.True(MGTextCaret.ShouldDrawCaret(true, true, false, 0.49, DefaultBlinkRate));
    }

    [Fact]
    public void HasPositionAndFocus_InTheSecondBlinkWindow_IsHidden()
    {
        Assert.False(MGTextCaret.ShouldDrawCaret(true, true, false, 0.5, DefaultBlinkRate));
        Assert.False(MGTextCaret.ShouldDrawCaret(true, true, false, 0.99, DefaultBlinkRate));
    }

    [Fact]
    public void HasPositionAndFocus_TheBlinkGateAlternatesOverTime()
    {
        //  Drive secondsShown across several blink boundaries: shown, hidden, shown, hidden, shown.
        Assert.True(MGTextCaret.ShouldDrawCaret(true, true, false, 0.0, DefaultBlinkRate));
        Assert.False(MGTextCaret.ShouldDrawCaret(true, true, false, 0.5, DefaultBlinkRate));
        Assert.True(MGTextCaret.ShouldDrawCaret(true, true, false, 1.0, DefaultBlinkRate));
        Assert.False(MGTextCaret.ShouldDrawCaret(true, true, false, 1.5, DefaultBlinkRate));
        Assert.True(MGTextCaret.ShouldDrawCaret(true, true, false, 2.0, DefaultBlinkRate));
    }

    // -- Row 3: without focus and ShowWhenUnfocused == false -> false (today's behaviour) --

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.3)]
    public void HasPositionNoFocus_NotShowingWhenUnfocused_IsNeverDrawn(double secondsShown)
    {
        Assert.False(MGTextCaret.ShouldDrawCaret(true, false, false, secondsShown, DefaultBlinkRate));
    }

    // -- Row 4: without focus and ShowWhenUnfocused == true -> always drawn, never blinking --

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.99)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(3.7)]
    public void HasPositionNoFocus_ShowingWhenUnfocused_IsAlwaysDrawn_AndDoesNotBlink(double secondsShown)
    {
        Assert.True(MGTextCaret.ShouldDrawCaret(true, false, true, secondsShown, DefaultBlinkRate));
    }

    // -- A non-positive blinkRate does not throw, and is treated as "always shown" while focused --

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void NonPositiveBlinkRate_WhileFocused_DoesNotThrow_AndIsAlwaysShown(double blinkRateSeconds)
    {
        TimeSpan blinkRate = TimeSpan.FromSeconds(blinkRateSeconds);

        var exception = Record.Exception(() => MGTextCaret.ShouldDrawCaret(true, true, false, 1.234, blinkRate));
        Assert.Null(exception);
        Assert.True(MGTextCaret.ShouldDrawCaret(true, true, false, 1.234, blinkRate));
    }
}

/// <summary><see cref="MGTextCaret.IsCurrentlyVisible"/> through a real headless desktop, on a real
/// <see cref="MGRichTextBox"/> with a caret position (same harness pattern as <c>RichTextBoxProgrammaticEditUndoTests</c>).
/// A long <see cref="MGTextCaret.BlinkRate"/> keeps the focused sample deterministic.</summary>
public class MGTextCaretIsCurrentlyVisibleTests
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

    private static void Frame(GraphTestRuntime runtime, MGDesktop desktop, int frameIndex)
    {
        MouseState mouse = new(1, 1, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * frameIndex), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
        desktop.Update();
    }

    [Fact]
    public void Focused_IsCurrentlyVisible()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGRichTextBox editor) = CreateEditor();
        editor.SetText("Hello world");
        Frame(runtime, desktop, 2);
        editor.CaretIndex = 5;
        Frame(runtime, desktop, 3);
        editor.Caret.BlinkRate = TimeSpan.FromSeconds(1000); // deterministic: never crosses a blink boundary within the test.
        editor.Focus();
        Frame(runtime, desktop, 4);

        Assert.Same(editor, desktop.FocusedKeyboardHandler);
        Assert.True(editor.Caret.HasPosition);
        Assert.True(editor.Caret.IsCurrentlyVisible);
    }

    [Fact]
    public void NotFocused_DefaultShowWhenUnfocusedFalse_IsNotCurrentlyVisible()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGRichTextBox editor) = CreateEditor();
        editor.SetText("Hello world");
        Frame(runtime, desktop, 2);
        editor.CaretIndex = 5;
        Frame(runtime, desktop, 3);

        Assert.False(editor.Caret.ShowWhenUnfocused);
        Assert.NotSame(editor, desktop.FocusedKeyboardHandler);
        Assert.True(editor.Caret.HasPosition);
        Assert.False(editor.Caret.IsCurrentlyVisible);
    }

    [Fact]
    public void NotFocused_ShowWhenUnfocusedTrue_IsCurrentlyVisible()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGRichTextBox editor) = CreateEditor();
        editor.SetText("Hello world");
        Frame(runtime, desktop, 2);
        editor.CaretIndex = 5;
        Frame(runtime, desktop, 3);
        editor.Caret.ShowWhenUnfocused = true;

        Assert.NotSame(editor, desktop.FocusedKeyboardHandler);
        Assert.True(editor.Caret.HasPosition);
        Assert.True(editor.Caret.IsCurrentlyVisible);
    }
}
