using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Focus;

/// <summary>
/// Covers the single keyboard-focus navigation rule: <see cref="MGDesktop.IsNavigationTarget(MGElement)"/> keys off
/// <see cref="MGElement.CanHandleKeyboardInput"/> alone, so a control that overrides it - a text box above all - is a
/// navigation target instead of being silently excluded by an <see cref="MGElement.IsFocusable"/> that it never set.
/// Also covers the two behaviours that make that rule usable: text entry hosts are deprioritised when auto-focus
/// resolution falls back on "first focusable", and <see cref="MGTextBox.AcceptsTab"/> now defaults to false so Tab can
/// leave a text box. See Docs/decisions/0014-keyboard-focus-navigation-single-rule.md.
/// </summary>
public class FocusNavigationTextEntryTests
{
    // ---- The rule itself -----------------------------------------------------------------------------------------

    [Fact]
    public void DefaultTextBox_IsNavigationTarget_AndAppearsInFocusableElements()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGTextBox textBox = new(window);
        window.SetContent(textBox);
        Settle(desktop);

        Assert.True(desktop.IsNavigationTarget(textBox));
        Assert.Contains(textBox, desktop.GetFocusableElements());
    }

    /// <summary>Guards the MGTextBox half of the fix on its own: the public flag must now say what is true.</summary>
    [Fact]
    public void DefaultTextBox_IsFocusable_IsTrue()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGTextBox textBox = new(window);

        Assert.True(textBox.IsFocusable);
        Assert.True(textBox.CanHandleKeyboardInput);
        GC.KeepAlive(desktop);
    }

    /// <summary>Guards the IsNavigationTarget half of the fix on its own. A text box cannot guard it, because the
    /// other half sets IsFocusable = true on it: only a control that overrides CanHandleKeyboardInput while leaving
    /// IsFocusable false can tell the two apart.</summary>
    [Fact]
    public void ControlOverridingCanHandleKeyboardInput_WithIsFocusableFalse_IsNavigationTarget()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        KeyboardActiveProbe probe = new(window);
        window.SetContent(probe);
        Settle(desktop);

        Assert.False(probe.IsFocusable);
        Assert.True(probe.CanHandleKeyboardInput);
        Assert.True(desktop.IsNavigationTarget(probe));
        Assert.Contains(probe, desktop.GetFocusableElements());
    }

    [Fact]
    public void TextBoxSubclasses_AreNavigationTargets()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGPasswordBox passwordBox = new(window);
        MGRichTextBox richTextBox = new(window);
        MGNumericUpDown numericUpDown = new(window);
        MGStackPanel panel = new(window, Orientation.Vertical);
        panel.TryAddChild(passwordBox);
        panel.TryAddChild(richTextBox);
        panel.TryAddChild(numericUpDown);
        window.SetContent(panel);
        Settle(desktop);

        Assert.True(desktop.IsNavigationTarget(passwordBox));
        Assert.True(desktop.IsNavigationTarget(richTextBox));
        Assert.True(desktop.IsNavigationTarget(numericUpDown));
    }

    [Fact]
    public void FocusableElements_ContainsTextBoxes_InDocumentOrder()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGButton button = new(window);
        MGTextBox textBox = new(window);
        MGStackPanel panel = new(window, Orientation.Vertical);
        panel.TryAddChild(button);
        panel.TryAddChild(textBox);
        window.SetContent(panel);
        Settle(desktop);

        //  The Tab order itself is NOT filtered by the text-entry deprioritisation - only auto-focus resolution is.
        List<MGElement> focusable = desktop.GetFocusableElements().ToList();
        Assert.True(focusable.IndexOf(button) >= 0);
        Assert.True(focusable.IndexOf(textBox) > focusable.IndexOf(button));
    }

    /// <summary>The accepted consequence of the single rule: a composite's internal editors become tab stops too.</summary>
    [Fact]
    public void PropertyGridRowEditors_AreNavigationTargets()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window, 640, 480);
        MGPropertyGrid grid = new(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LabelColumnWidth = 160,
            SelectedObject = new InspectableEntity { PositionX = 7, Name = "Player" },
        };
        window.SetContent(grid);
        Settle(desktop);

        Assert.Contains(desktop.GetFocusableElements(), x => x is MGTextBox);
    }

    // ---- Tab in, and Tab back out --------------------------------------------------------------------------------

    [Fact]
    public void Tab_ReachesTextBox_AndTabFromTextBox_ReachesTheNextElement()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGButton first = new(window);
        MGTextBox textBox = new(window);
        MGButton last = new(window);
        MGStackPanel panel = new(window, Orientation.Vertical);
        panel.TryAddChild(first);
        panel.TryAddChild(textBox);
        panel.TryAddChild(last);
        window.SetContent(panel);
        Settle(desktop);

        SetFocusedKeyboardHandler(desktop, first);

        //  Tab INTO the text box: impossible before the single rule, since it was not a navigation target at all.
        Assert.True(desktop.NavigationService.TryDispatchNavigationAction(UINavigationAction.MoveNext, KeyboardFocusSource.Keyboard));
        desktop.Update();
        Assert.Same(textBox, desktop.FocusedKeyboardHandler);

        //  Tab back OUT of it: only possible because AcceptsTab now defaults to false.
        Assert.True(desktop.NavigationService.TryDispatchNavigationAction(UINavigationAction.MoveNext, KeyboardFocusSource.Keyboard, Keys.Tab));
        desktop.Update();
        Assert.Same(last, desktop.FocusedKeyboardHandler);
    }

    /// <summary>The same exit, driven through the REAL keyboard pipeline rather than the navigation-dispatch entry
    /// point, because only this path can insert characters: it is what proves a default text box no longer swallows Tab
    /// as four spaces. <see cref="Tab_ReachesTextBox_AndTabFromTextBox_ReachesTheNextElement"/> alone cannot show that -
    /// the semantic dispatch it calls never reaches MGTextBox.HandleKeyPress, so any assertion on Text there would hold
    /// whatever AcceptsTab said.</summary>
    [Fact]
    public void RealTabKeyPress_InDefaultTextBox_MovesFocus_AndInsertsNothing()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        MGTextBox textBox = new(window) { PreferredWidth = 120, PreferredHeight = 24 };
        MGButton next = new(window) { PreferredWidth = 120, PreferredHeight = 24 };
        MGStackPanel panel = new(window, Orientation.Vertical)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        panel.TryAddChild(textBox);
        panel.TryAddChild(next);
        window.SetContent(panel);
        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();

        //  Real pointer focus, so the text box owns the keyboard exactly as it would in the app.
        Point caretPoint = textBox.LayoutBounds.Center;
        AdvanceFrame(runtime, desktop, 16, caretPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 32, caretPoint);
        AdvanceFrame(runtime, desktop, 48, caretPoint);
        Assert.Same(textBox, desktop.FocusedKeyboardHandler);

        KeyFrame(runtime, desktop, 64, Keys.Tab);
        KeyFrame(runtime, desktop, 80);

        Assert.Equal(string.Empty, textBox.Text);
        Assert.Same(next, desktop.FocusedKeyboardHandler);
    }

    /// <summary>The mirror of the test above: with AcceptsTab opted back in, the very same real key press inserts and
    /// keeps focus. Together the two prove the assertions discriminate the default rather than the code path.</summary>
    [Fact]
    public void RealTabKeyPress_InTextBoxWithAcceptsTabTrue_InsertsSpaces_AndKeepsFocus()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        MGTextBox textBox = new(window) { PreferredWidth = 120, PreferredHeight = 24, AcceptsTab = true };
        MGButton next = new(window) { PreferredWidth = 120, PreferredHeight = 24 };
        MGStackPanel panel = new(window, Orientation.Vertical)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        panel.TryAddChild(textBox);
        panel.TryAddChild(next);
        window.SetContent(panel);
        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();

        Point caretPoint = textBox.LayoutBounds.Center;
        AdvanceFrame(runtime, desktop, 16, caretPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 32, caretPoint);
        AdvanceFrame(runtime, desktop, 48, caretPoint);
        Assert.Same(textBox, desktop.FocusedKeyboardHandler);

        KeyFrame(runtime, desktop, 64, Keys.Tab);
        KeyFrame(runtime, desktop, 80);

        Assert.NotEqual(string.Empty, textBox.Text);
        Assert.Same(textBox, desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void TextBox_WithAcceptsTabTrue_KeepsTabForItself()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGTextBox textBox = new(window) { AcceptsTab = true };
        MGButton button = new(window);
        MGStackPanel panel = new(window, Orientation.Vertical);
        panel.TryAddChild(textBox);
        panel.TryAddChild(button);
        window.SetContent(panel);
        Settle(desktop);

        SetFocusedKeyboardHandler(desktop, textBox);

        Assert.False(desktop.NavigationService.TryDispatchNavigationAction(UINavigationAction.MoveNext, KeyboardFocusSource.Keyboard, Keys.Tab));
        desktop.Update();
        Assert.Same(textBox, desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void RichTextBox_StillAcceptsTab_ByDefault()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGRichTextBox richTextBox = new(window);

        Assert.True(richTextBox.AcceptsTab);
        Assert.False(new MGTextBox(window).AcceptsTab);
        GC.KeepAlive(desktop);
    }

    /// <summary>Pins the one in-repo consumer that relied on the old <c>AcceptsTab = true</c> default instead of setting
    /// it: the hand-edited markup box of <see cref="MGXAMLDesigner"/>. It states the property nowhere, so an inventory of
    /// <c>AcceptsTab</c> occurrences cannot find it, and the loss of Tab-to-indent there would be silent.</summary>
    [Fact]
    public void XamlDesigner_EditableMarkupBox_StillAcceptsTab_AndTheReadonlyOneNeedsNoOptIn()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window, 640, 480);
        MGXAMLDesigner designer = new(window);

        Assert.True(designer.FromStringTextBoxComponent.AcceptsTab);
        Assert.True(designer.FromFileTextBoxComponent.IsReadonly);
        GC.KeepAlive(desktop);
    }

    // ---- Ctrl+Tab: the keyboard way out of a control that reserves Tab -------------------------------------------

    /// <summary>Without this, making text entry hosts navigation targets would turn every control that legitimately
    /// sets <c>AcceptsTab = true</c> into a keyboard trap: Tab is reserved, arrows and Home/End are reserved
    /// unconditionally, and Escape maps to Cancel, which fallback navigation does not handle.</summary>
    [Fact]
    public void CtrlTab_LeavesARichTextBox_WithoutInsertingAnything()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGRichTextBox editor, MGButton next) = CreateTabReservingPair();

        KeyFrame(runtime, desktop, 64, Keys.LeftControl, Keys.Tab);
        KeyFrame(runtime, desktop, 80);

        Assert.Equal(string.Empty, editor.Text);
        Assert.Same(next, desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void CtrlShiftTab_LeavesARichTextBox_Backwards()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGRichTextBox editor, MGButton next) = CreateTabReservingPair(out MGButton previous);

        KeyFrame(runtime, desktop, 64, Keys.LeftControl, Keys.LeftShift, Keys.Tab);
        KeyFrame(runtime, desktop, 80);

        Assert.Equal(string.Empty, editor.Text);
        Assert.Same(previous, desktop.FocusedKeyboardHandler);
        Assert.NotSame(next, desktop.FocusedKeyboardHandler);
    }

    /// <summary>The escape must not eat the plain key: Tab alone still indents in a control that reserves it.</summary>
    [Fact]
    public void PlainTab_InARichTextBox_StillIndents_AndKeepsFocus()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGRichTextBox editor, MGButton next) = CreateTabReservingPair();

        KeyFrame(runtime, desktop, 64, Keys.Tab);
        KeyFrame(runtime, desktop, 80);

        Assert.NotEqual(string.Empty, editor.Text);
        Assert.Same(editor, desktop.FocusedKeyboardHandler);
        Assert.NotSame(next, desktop.FocusedKeyboardHandler);
    }

    /// <summary>The escape is Tab-only. Ctrl+Left keeps its word-wise caret meaning rather than moving focus, which is
    /// why the rule is expressed as one key and not as "Ctrl suspends text entry preservation".<para/>
    /// The layout is HORIZONTAL on purpose: a button whose centre is strictly left of the editor's is what gives
    /// <see cref="UINavigationAction.MoveLeft"/> somewhere to land. In a vertical stack every element shares a left
    /// edge, directional navigation finds no candidate, and the test would pass whether or not the escape were still
    /// restricted to Tab.</summary>
    [Fact]
    public void CtrlLeft_InARichTextBox_DoesNotMoveFocus()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 200) { WindowStyle = WindowStyle.None };
        MGButton leftNeighbour = new(window) { PreferredWidth = 80, PreferredHeight = 40 };
        MGRichTextBox editor = new(window) { PreferredWidth = 120, PreferredHeight = 40 };
        MGStackPanel panel = new(window, Orientation.Horizontal)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        panel.TryAddChild(leftNeighbour);
        panel.TryAddChild(editor);
        window.SetContent(panel);
        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();

        Assert.True(editor.AcceptsTab);
        //  Without this, MoveLeft would have nowhere to go and the test could not fail.
        Assert.True(leftNeighbour.ActualLayoutBounds.Center.X < editor.ActualLayoutBounds.Center.X);
        Assert.True(desktop.IsNavigationTarget(leftNeighbour));

        Point caretPoint = editor.LayoutBounds.Center;
        AdvanceFrame(runtime, desktop, 16, caretPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 32, caretPoint);
        AdvanceFrame(runtime, desktop, 48, caretPoint);
        Assert.Same(editor, desktop.FocusedKeyboardHandler);

        KeyFrame(runtime, desktop, 64, Keys.LeftControl, Keys.Left);
        KeyFrame(runtime, desktop, 80);

        Assert.Same(editor, desktop.FocusedKeyboardHandler);
        Assert.NotSame(leftNeighbour, desktop.FocusedKeyboardHandler);
    }

    // ---- The reported bug ----------------------------------------------------------------------------------------

    /// <summary>
    /// The bug this whole change exists for. MGWindow.ActivatesOnClick resolves a new auto-focus target on any click
    /// that queued no focus of its own; because the text box was not a navigation target, it could not be restored as
    /// the window's last-focused element, so such a click moved focus AWAY from it and it could never come back.
    /// <para/>
    /// The second focusable (the button) is what makes this criterion able to fail: without it, the pre-fix resolution
    /// would return null and the null-conditional Focus call would be a no-op, so focus would stay on the text box for
    /// the wrong reason and the test would pass against the unfixed tree.
    /// </summary>
    [Fact]
    public void ClickOnInertArea_WithTextBoxLastFocused_LeavesFocusOnTheTextBox()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };

        MGButton button = new(window) { PreferredWidth = 60, PreferredHeight = 24 };
        MGTextBox textBox = new(window) { PreferredWidth = 60, PreferredHeight = 24 };
        MGStackPanel panel = new(window, Orientation.Vertical)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        panel.TryAddChild(button);
        panel.TryAddChild(textBox);
        window.SetContent(panel);
        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();

        Assert.True(desktop.IsNavigationTarget(button));
        SetFocusedKeyboardHandler(desktop, textBox);
        Assert.Same(textBox, desktop.FocusedKeyboardHandler);

        //  An inert spot: inside the window, outside both controls, so the click queues no focus of its own and
        //  ActivatesOnClick has to resolve a target.
        Point inertPoint = new(window.Left + 300, window.Top + 250);
        Assert.False(button.ActualLayoutBounds.Contains(inertPoint));
        Assert.False(textBox.ActualLayoutBounds.Contains(inertPoint));

        AdvanceFrame(runtime, desktop, 32, inertPoint);
        AdvanceFrame(runtime, desktop, 48, inertPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, inertPoint);

        //  Before the fix this was the button (the pre-fix firstFocusable), never the text box.
        Assert.Same(textBox, desktop.FocusedKeyboardHandler);
    }

    // ---- Text entry deprioritised as the "first focusable" fallback ----------------------------------------------

    [Fact]
    public void AutoFocusResolution_PrefersANonTextEntryTarget_OverATextBox()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGTextBox textBox = new(window);
        MGButton button = new(window);
        MGStackPanel panel = new(window, Orientation.Vertical);
        //  Text box FIRST in document order, so only the deprioritisation can make the button win.
        panel.TryAddChild(textBox);
        panel.TryAddChild(button);
        window.SetContent(panel);
        Settle(desktop);

        Assert.Equal(textBox, desktop.GetFocusableElements().First());
        Assert.Same(button, desktop.ResolveAutoFocusTarget(window, false));
    }

    [Fact]
    public void AutoFocusResolution_FallsBackToTheTextBox_WhenItIsTheOnlyTarget()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGTextBox textBox = new(window);
        window.SetContent(textBox);
        Settle(desktop);

        Assert.Same(textBox, desktop.ResolveAutoFocusTarget(window, false));
    }

    [Fact]
    public void AutoFocusResolution_HonoursDefaultFocusElement_EvenWhenItIsATextBox()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGTextBox textBox = new(window);
        MGButton button = new(window);
        MGStackPanel panel = new(window, Orientation.Vertical);
        panel.TryAddChild(textBox);
        panel.TryAddChild(button);
        window.SetContent(panel);
        window.DefaultFocusElement = textBox;
        Settle(desktop);

        //  Explicit intent beats the deprioritisation, which only ever governs the "first focusable" fallback.
        Assert.Same(textBox, desktop.ResolveAutoFocusTarget(window, true));
    }

    [Fact]
    public void AutoFocusResolution_RestoresALastFocusedTextBox_OverTheFirstFocusable()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGButton button = new(window);
        MGTextBox textBox = new(window);
        MGStackPanel panel = new(window, Orientation.Vertical);
        panel.TryAddChild(button);
        panel.TryAddChild(textBox);
        window.SetContent(panel);
        Settle(desktop);

        SetFocusedKeyboardHandler(desktop, textBox);

        Assert.Same(textBox, desktop.ResolveAutoFocusTarget(window, false));
    }

    // ---- Helpers -------------------------------------------------------------------------------------------------

    /// <summary>An element that is keyboard-active without ever setting <see cref="MGElement.IsFocusable"/> - the shape
    /// that the removed <c>IsFocusable</c> term in <see cref="MGDesktop.IsNavigationTarget(MGElement)"/> used to
    /// exclude from navigation, and the only shape that can detect its return.</summary>
    private sealed class KeyboardActiveProbe : MGElement
    {
        public KeyboardActiveProbe(MGWindow window)
            : base(window, MGElementType.Custom)
        {
            PreferredWidth = 40;
            PreferredHeight = 20;
        }

        public override bool CanHandleKeyboardInput => true;
    }

    private sealed class InspectableEntity
    {
        [Category("Transform")]
        public int PositionX { get; set; }

        [Category("Identity")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>A focused <see cref="MGRichTextBox"/> (which sets <c>AcceptsTab = true</c> of its own accord) with a
    /// button before it and one after it, so both navigation directions have somewhere to land. Focus is taken through
    /// a real mouse press, so the whole keyboard pipeline behaves as it does in the app.</summary>
    private static (GraphTestRuntime Runtime, MGDesktop Desktop, MGRichTextBox Editor, MGButton Next) CreateTabReservingPair()
        => CreateTabReservingPair(out _);

    private static (GraphTestRuntime Runtime, MGDesktop Desktop, MGRichTextBox Editor, MGButton Next) CreateTabReservingPair(out MGButton previous)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        MGButton first = new(window) { PreferredWidth = 120, PreferredHeight = 24 };
        MGRichTextBox editor = new(window) { PreferredWidth = 120, PreferredHeight = 40 };
        MGButton last = new(window) { PreferredWidth = 120, PreferredHeight = 24 };
        MGStackPanel panel = new(window, Orientation.Vertical)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        panel.TryAddChild(first);
        panel.TryAddChild(editor);
        panel.TryAddChild(last);
        window.SetContent(panel);
        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();

        Assert.True(editor.AcceptsTab);

        Point caretPoint = editor.LayoutBounds.Center;
        AdvanceFrame(runtime, desktop, 16, caretPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 32, caretPoint);
        AdvanceFrame(runtime, desktop, 48, caretPoint);
        Assert.Same(editor, desktop.FocusedKeyboardHandler);

        previous = first;
        return (runtime, desktop, editor, last);
    }

    private static MGDesktop CreateDesktopWithSingleWindow(out MGWindow window, int width = 400, int height = 400)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        window = new MGWindow(desktop, 0, 0, width, height) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();
        return desktop;
    }

    private static void Settle(MGDesktop desktop)
    {
        desktop.Update();
        desktop.Update();
    }

    private static void AdvanceFrame(GraphTestRuntime runtime, MGDesktop desktop, int totalElapsedMs, Point position, MouseButton? pressedButton = null)
    {
        runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(totalElapsedMs),
            TimeSpan.FromMilliseconds(16),
            CreateMouseState(position, pressedButton),
            new KeyboardState()));
        desktop.Update();
    }

    private static void KeyFrame(GraphTestRuntime runtime, MGDesktop desktop, int totalElapsedMs, params Keys[] pressedKeys)
    {
        runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(totalElapsedMs),
            TimeSpan.FromMilliseconds(16),
            CreateMouseState(new Point(0, 0), null),
            new KeyboardState(pressedKeys)));
        desktop.Update();
    }

    private static MouseState CreateMouseState(Point position, MouseButton? pressedButton)
        => new(
            position.X,
            position.Y,
            0,
            pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);

    private static void SetFocusedKeyboardHandler(MGDesktop desktop, MGElement element)
    {
        MethodInfo setter = typeof(MGDesktop).GetProperty(nameof(MGDesktop.FocusedKeyboardHandler), BindingFlags.Public | BindingFlags.Instance)!.GetSetMethod(true)!;
        setter.Invoke(desktop, new object[] { element });
    }
}
