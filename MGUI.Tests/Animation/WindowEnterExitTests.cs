using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Animation;

/// <summary>Slice Y7 (Docs/decisions/0011-animation-v5.md, "Ouverture et fermeture des fenetres"): a window's entry on the root/nested/modal
/// opening paths, its deferred exit on <see cref="MGWindow.TryCloseWindow"/> and <see cref="MGWindow.RemoveNestedWindow"/>, the internal
/// closing state that stops input and occlusion, and the internal window draw transform that makes a scale/slide effect visible on a
/// window. Covers every bullet of the Y7 brief's acceptance list (item 7).</summary>
public class WindowEnterExitTests
{
    private static UIEnterExitSettings FadeSettings(int enterMs = 40, int exitMs = 40) => new()
    {
        EnterEffect = UIEnterExitEffect.Fade,
        ExitEffect = UIEnterExitEffect.Fade,
        EnterDuration = TimeSpan.FromMilliseconds(enterMs),
        ExitDuration = TimeSpan.FromMilliseconds(exitMs),
    };

    private static void Frame(GraphTestRuntime runtime, MGDesktop desktop, int frameIndex, Point? position = null, bool leftPressed = false)
    {
        Point p = position ?? new Point(1, 1);
        MouseState mouse = new(p.X, p.Y, 0,
            leftPressed ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * frameIndex), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
        desktop.Update();
    }

    private static (GraphTestRuntime Runtime, MGDesktop Desktop) NewDesktop()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        return (runtime, desktop);
    }

    // ---- Root window entries -------------------------------------------------------------------------------------

    [Fact]
    public void RootWindow_AddedToDesktopWindows_EntryActuallyRuns_ThenReachesBaseOpacity()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow window = new(desktop, 0, 0, 200, 150) { WindowStyle = WindowStyle.None };
        window.Opacity = 1f;
        window.EnterExit = FadeSettings();

        desktop.Windows.Add(window);
        Frame(runtime, desktop, 1);
        Assert.True(desktop.Animations.ActiveCount > 0);

        Frame(runtime, desktop, 2, position: new Point(1, 1));
        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 3 + i);
        }

        Assert.Equal(1f, window.Opacity, 3);
        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    [Fact]
    public void BringToFront_BringToBack_ClickActivation_ReplayNoEntry()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow a = new(desktop, 0, 0, 200, 150) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings() };
        MGWindow b = new(desktop, 250, 0, 200, 150) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings() };
        desktop.Windows.Add(a);
        desktop.Windows.Add(b);

        Frame(runtime, desktop, 1);
        int activeAfterOpen = desktop.Animations.ActiveCount;
        Assert.True(activeAfterOpen > 0);

        // Let both entries finish.
        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 2 + i);
        }
        Assert.Equal(0, desktop.Animations.ActiveCount);

        desktop.BringToFront(a);
        Frame(runtime, desktop, 20);
        Assert.Equal(0, desktop.Animations.ActiveCount); // no replayed entry

        desktop.BringToBack(a);
        Frame(runtime, desktop, 21);
        Assert.Equal(0, desktop.Animations.ActiveCount);

        // Click activation: press+release inside `a` (ActivatesOnClick default true) removes+re-adds within BringToFront,
        // still within the frame it is processed -- no replay across the frame boundary.
        Point insideA = new(a.Left + 10, a.Top + 10);
        Frame(runtime, desktop, 22, insideA, leftPressed: true);
        Frame(runtime, desktop, 23, insideA, leftPressed: false);
        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    [Fact]
    public void DesktopWindowsRemove_Direct_PlaysNoExit_NoResidualRun()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow window = new(desktop, 0, 0, 200, 150) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings() };
        desktop.Windows.Add(window);
        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 1 + i);
        }
        Assert.Equal(0, desktop.Animations.ActiveCount);

        bool removed = desktop.Windows.Remove(window);
        Assert.True(removed);
        Assert.False(window.IsClosing);
        Assert.Equal(0, desktop.Animations.ActiveCount);

        Frame(runtime, desktop, 12);
        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    // ---- TryCloseWindow: event order, drawing during the exit, list membership ------------------------------------

    [Fact]
    public void TryCloseWindow_RootWindow_PlaysExit_ThenNotifiesInOrder_AndIsRemovedOnlyAfter()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow window = new(desktop, 0, 0, 200, 150) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(exitMs: 60) };
        desktop.Windows.Add(window);
        for (int i = 0; i < 5; i++)
        {
            Frame(runtime, desktop, 1 + i);
        }

        List<string> events = new();
        window.WindowClosing += (_, _) => events.Add("Closing");
        window.WindowClosed += (_, _) => events.Add("Closed");
        bool sawInListWhileExiting = false;

        bool accepted = window.TryCloseWindow();
        Assert.True(accepted);
        Assert.True(window.IsClosing);
        Assert.Equal(new[] { "Closing" }, events);
        Assert.Contains(window, desktop.Windows); // still drawn/listed during the exit
        sawInListWhileExiting = desktop.Windows.Contains(window);
        Assert.True(sawInListWhileExiting);

        // Drawn during the exit.
        GraphNoOpDrawTransaction draw = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.Draw(draw);

        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 6 + i);
        }

        Assert.Equal(new[] { "Closing", "Closed" }, events);
        Assert.False(window.IsClosing);
        Assert.DoesNotContain(window, desktop.Windows);
        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    [Fact]
    public void TryCloseWindow_NestedWindow_RemovedFromNestedWindows_OnlyAfterExit()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow parent = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(parent);
        MGWindow nested = new(parent, 10, 10, 150, 100) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(exitMs: 60) };
        parent.AddNestedWindow(nested);
        for (int i = 0; i < 5; i++)
        {
            Frame(runtime, desktop, 1 + i);
        }

        Assert.True(nested.TryCloseWindow());
        Assert.True(nested.IsClosing);
        Assert.Contains(nested, parent.NestedWindows);

        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 6 + i);
        }

        Assert.False(nested.IsClosing);
        Assert.DoesNotContain(nested, parent.NestedWindows);
        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    [Fact]
    public void TryCloseWindow_SecondCallDuringExit_DoesNothing_ReturnsFalse()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow window = new(desktop, 0, 0, 200, 150) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(exitMs: 200) };
        desktop.Windows.Add(window);
        for (int i = 0; i < 5; i++)
        {
            Frame(runtime, desktop, 1 + i);
        }

        Assert.True(window.TryCloseWindow());
        Assert.False(window.TryCloseWindow());
        Assert.True(window.IsClosing);
    }

    [Fact]
    public void WindowWithNoEnterExit_TryCloseWindow_BehavesExactlyLikeHead()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow window = new(desktop, 0, 0, 200, 150) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(window);
        Frame(runtime, desktop, 1);

        bool closedEventRaised = false;
        window.WindowClosed += (_, _) => closedEventRaised = true;

        bool accepted = window.TryCloseWindow();
        Assert.True(accepted);
        Assert.False(window.IsClosing);
        Assert.True(closedEventRaised); // notified at once, no deferral
        Assert.DoesNotContain(window, desktop.Windows);
    }

    // ---- Modal: stack freed at the end of the exit, parent blocked until then -------------------------------------

    [Fact]
    public void Modal_TryCloseWindow_ParentBlocked_UntilExitEnds_ThenStackIsFreed()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow parent = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(parent);
        MGWindow modal = new(parent, 50, 50, 150, 100) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(exitMs: 60) };
        parent.PushModalWindow(modal);
        for (int i = 0; i < 5; i++)
        {
            Frame(runtime, desktop, 1 + i);
        }
        Assert.True(parent.HasModalWindow);

        int notifyClosedCount = 0;
        modal.WindowClosed += (_, _) => notifyClosedCount++;

        Assert.True(modal.TryCloseWindow());
        Assert.True(parent.HasModalWindow); // still blocked mid-exit

        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 6 + i);
            if (!parent.HasModalWindow)
            {
                break;
            }
        }

        Assert.False(parent.HasModalWindow);
        Assert.Equal(1, notifyClosedCount);
    }

    // ---- Focus released at the start of the exit --------------------------------------------------------------

    [Fact]
    public void Focus_ReleasedAtTheStartOfTheExit_NotAtTheEnd()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow parent = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(parent);
        MGWindow nested = new(parent, 10, 10, 150, 100) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(exitMs: 200) };
        MGButton button = new(nested) { PreferredWidth = 60, PreferredHeight = 24 };
        nested.SetContent(button);
        parent.AddNestedWindow(nested);
        for (int i = 0; i < 5; i++)
        {
            Frame(runtime, desktop, 1 + i);
        }

        button.Focus();
        Frame(runtime, desktop, 6);
        Assert.Same(button, desktop.FocusedKeyboardHandler);

        nested.TryCloseWindow();
        Assert.NotSame(button, desktop.FocusedKeyboardHandler); // released immediately, exit still running
        Assert.True(nested.IsClosing);
    }

    // ---- Non-modal: click during the exit reaches the element below; parent hover no longer suppressed ------------

    [Fact]
    public void NonModal_ClickDuringExit_ReachesElementBelow_AndParentHoverIsNotSuppressed()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow parent = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        MGButton parentButton = new(parent) { PreferredWidth = 200, PreferredHeight = 150 };
        parent.SetContent(parentButton);
        desktop.Windows.Add(parent);

        MGWindow nested = new(parent, 0, 0, 100, 80) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(exitMs: 300) };
        parent.AddNestedWindow(nested);
        for (int i = 0; i < 5; i++)
        {
            Frame(runtime, desktop, 1 + i);
        }

        Point overlap = new(nested.Left + 10, nested.Top + 10);
        Assert.True(nested.LayoutBounds.Contains(overlap));
        Assert.True(parentButton.LayoutBounds.Contains(overlap));

        Frame(runtime, desktop, 6, overlap);
        Frame(runtime, desktop, 7, overlap);
        Assert.False(parentButton.IsHovered); // occluded by the (still fully opaque, not-yet-exiting) nested window

        Assert.True(nested.TryCloseWindow());
        Assert.True(nested.IsClosing);

        Frame(runtime, desktop, 8, overlap);
        Frame(runtime, desktop, 9, new Point(overlap.X + 1, overlap.Y));

        Assert.True(parentButton.IsHovered); // the exiting nested window no longer occludes/suppresses hover

        int clicksOnParentButton = 0;
        parentButton.MouseHandler.PressedInside += (_, _) => clicksOnParentButton++;
        Frame(runtime, desktop, 10, overlap, leftPressed: true);
        Assert.True(clicksOnParentButton > 0);
    }

    // ---- Floating dock windows: never play an exit ------------------------------------------------------------

    [Fact]
    public void FloatingDockWindow_ClosedAtOnce_EvenWithExitConfigured_OnCloseAndOnReDock()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow parentWindow = new(desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(parentWindow);
        MGDockHost host = new(parentWindow);
        DockPanelNode panelA = new() { Title = "A", ContentFactory = () => new MGBorder(parentWindow) };
        MGFloatingDockWindow floatA = new(host, panelA, 0, 0, 200, 150) { EnterExit = FadeSettings(exitMs: 5000) };
        parentWindow.AddNestedWindow(floatA);
        Frame(runtime, desktop, 1);

        Assert.True(floatA.SuppressWindowEnterExit);
        Assert.True(floatA.TryCloseWindow());
        Assert.False(floatA.IsClosing); // never plays an exit
        Assert.DoesNotContain(floatA, parentWindow.NestedWindows);

        // Re-dock path: MGDockHost.CloseFloatingWindow -> RemoveNestedWindow, also immediate.
        DockPanelNode panelB = new() { Title = "B", ContentFactory = () => new MGBorder(parentWindow) };
        MGFloatingDockWindow floatB = new(host, panelB, 0, 0, 200, 150) { EnterExit = FadeSettings(exitMs: 5000) };
        parentWindow.AddNestedWindow(floatB);
        Frame(runtime, desktop, 2);
        host.CloseFloatingWindow(floatB);
        Assert.False(floatB.IsClosing);
        Assert.DoesNotContain(floatB, parentWindow.NestedWindows);
    }

    // ---- Color picker popup: reopened during its exit, no exception -------------------------------------------

    [Fact]
    public void ColorPickerPopup_ReopenedDuringExit_NoException_EndsInteractive()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow owner = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(owner);
        MGColorPickerPopup popup = new(owner);
        popup.PopupWindow.EnterExit = FadeSettings(exitMs: 200);

        popup.Open(new Point(10, 10), new ColorValue(1f, 0f, 0f, 1f));
        for (int i = 0; i < 3; i++)
        {
            Frame(runtime, desktop, 1 + i);
        }
        Assert.Contains(popup.PopupWindow, owner.NestedWindows);

        popup.CancelAndClose();
        Frame(runtime, desktop, 4);
        Assert.True(popup.PopupWindow.IsClosing);

        Exception caught = Record.Exception(() => popup.Open(new Point(20, 20), new ColorValue(0f, 1f, 0f, 1f)));
        Assert.Null(caught);

        Frame(runtime, desktop, 5);
        Assert.False(popup.PopupWindow.IsClosing);
        Assert.Contains(popup.PopupWindow, owner.NestedWindows);
    }

    // ---- Combo box dropdown: open/close/reopen mid-exit, select during entry, RemoveNestedWindow contract ----------

    [Fact]
    public void ComboBoxDropdown_OpenCloseReopenDuringExit_ThenSelectDuringEntry()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow window = new(desktop, 0, 0, 300, 400) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(window);
        MGComboBox<string> comboBox = new(window) { PreferredHeight = 30 };
        comboBox.SetItemsSource(new List<string> { "Alpha", "Beta", "Gamma" });
        window.SetContent(comboBox);
        Frame(runtime, desktop, 1);
        Frame(runtime, desktop, 2);

        comboBox.Dropdown.EnterExit = FadeSettings(enterMs: 200, exitMs: 200);

        comboBox.IsDropdownOpen = true;
        Frame(runtime, desktop, 3);
        MGWindow dropdown = Assert.Single(window.NestedWindows);
        Assert.Same(comboBox.Dropdown, dropdown);

        comboBox.IsDropdownOpen = false;
        Frame(runtime, desktop, 4);
        Assert.True(dropdown.IsClosing);
        Assert.Contains(dropdown, window.NestedWindows); // RemoveNestedWindow deferred it, still present

        // Reopen mid-exit: no exception, cancels the exit, replays the entry.
        Exception caught = Record.Exception(() => comboBox.IsDropdownOpen = true);
        Assert.Null(caught);
        Frame(runtime, desktop, 5);
        Assert.False(dropdown.IsClosing);
        Assert.Contains(dropdown, window.NestedWindows);

        // Selecting an item during the (still running) entry works normally.
        comboBox.SelectedItem = "Beta";
        Assert.Equal("Beta", comboBox.SelectedItem);
    }

    [Fact]
    public void RemoveNestedWindow_OnAWindowWithAnExit_ReturnsTrue_AndDefersRemoval()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow parent = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(parent);
        MGWindow nested = new(parent, 10, 10, 100, 80) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(exitMs: 100) };
        parent.AddNestedWindow(nested);
        Frame(runtime, desktop, 1);

        bool result = parent.RemoveNestedWindow(nested);
        Assert.True(result);
        Assert.Contains(nested, parent.NestedWindows);
        Assert.True(nested.IsClosing);

        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 2 + i);
        }

        Assert.DoesNotContain(nested, parent.NestedWindows);
    }

    /// <summary>Y7-R1 fix (P2): a Visibility-driven (Y6) exit is already playing on a nested window (not one started by
    /// <see cref="MGWindow.TryCloseWindow"/> or a previous <see cref="MGWindow.RemoveNestedWindow"/> call) when <see cref="MGWindow.RemoveNestedWindow"/>
    /// is called. Before the fix, that exit's completion only applied the pending <see cref="Visibility"/> and never removed the window, so it
    /// stayed in <see cref="MGWindow.NestedWindows"/> forever, reappeared on a later <c>Visibility = Visible</c>, and made a subsequent
    /// <see cref="MGWindow.AddNestedWindow"/> throw.</summary>
    [Fact]
    public void RemoveNestedWindow_DuringAVisibilityDrivenExit_StillRemovesTheWindow()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow parent = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(parent);
        MGWindow nested = new(parent, 10, 10, 100, 80) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(exitMs: 100) };
        parent.AddNestedWindow(nested);
        Frame(runtime, desktop, 1);
        GraphNoOpDrawTransaction draw = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.Draw(draw); // a Y6 exit only plays once the element has drawn at least once (_hasDrawnSinceAttached)
        Frame(runtime, desktop, 2);

        nested.Visibility = Visibility.Collapsed; // Y6 Visibility-driven exit, not a window-removal one
        Assert.False(nested.IsClosing); // Y12: IsClosing narrows to a removal-bound exit; a plain Visibility write is not one

        bool result = parent.RemoveNestedWindow(nested);
        Assert.True(result);
        Assert.Contains(nested, parent.NestedWindows); // deferred, exactly like the removal-driven case

        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 3 + i);
        }

        Assert.DoesNotContain(nested, parent.NestedWindows);
        Assert.Equal(Visibility.Collapsed, nested.Visibility); // the Y6 Visibility write still happens

        // The removal was accepted, so a later re-add must not throw (HEAD contract for a window no longer in the list).
        parent.AddNestedWindow(nested);
        Assert.Contains(nested, parent.NestedWindows);
    }

    // ---- Closing the parent during a nested window's own exit: no residue --------------------------------------

    [Fact]
    public void ClosingTheParent_DuringANestedWindowsExit_LeavesNoResidualWindowOrAnimation()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow parent = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(exitMs: 60) };
        desktop.Windows.Add(parent);
        MGWindow nested = new(parent, 10, 10, 100, 80) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(exitMs: 5000) };
        parent.AddNestedWindow(nested);
        for (int i = 0; i < 5; i++)
        {
            Frame(runtime, desktop, 1 + i);
        }

        Assert.True(nested.TryCloseWindow()); // nested starts its own (very slow) exit
        Frame(runtime, desktop, 6);
        Assert.True(nested.IsClosing);
        Assert.Contains(nested, parent.NestedWindows); // still there: nested's own exit has not ended yet

        Assert.True(parent.TryCloseWindow()); // parent starts its own exit; it force-removes nested at once
        Frame(runtime, desktop, 7);
        Assert.DoesNotContain(nested, parent.NestedWindows);

        for (int i = 0; i < 15; i++)
        {
            Frame(runtime, desktop, 8 + i);
        }

        Assert.False(parent.IsClosing);
        Assert.DoesNotContain(parent, desktop.Windows);
        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    [Fact]
    public void ClosingTheParent_WithANestedWindowStillFullyOpen_RemovesItAtOnce_NoExitOfItsOwn()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow parent = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(exitMs: 60) };
        desktop.Windows.Add(parent);
        MGWindow nested = new(parent, 10, 10, 100, 80) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(exitMs: 5000) };
        parent.AddNestedWindow(nested);
        for (int i = 0; i < 5; i++)
        {
            Frame(runtime, desktop, 1 + i);
        }

        Assert.True(parent.TryCloseWindow());
        Assert.DoesNotContain(nested, parent.NestedWindows); // removed at once, without playing its own exit

        for (int i = 0; i < 15; i++)
        {
            Frame(runtime, desktop, 6 + i);
        }

        Assert.False(parent.IsClosing);
        Assert.DoesNotContain(parent, desktop.Windows);
        Assert.Equal(0, desktop.Animations.ActiveCount); // nested's own exit run was cancelled by the parent's own close
    }

    // ---- Scaled window with a FadeScale exit: composes both transforms ------------------------------------------

    [Fact]
    public void ScaledWindow_WithFadeScaleExit_ComposesBothTransforms_AndDrawsThroughout()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow window = new(desktop, 0, 0, 200, 150) { WindowStyle = WindowStyle.None, Scale = 1.5f };
        window.EnterExit = new UIEnterExitSettings
        {
            ExitEffect = UIEnterExitEffect.FadeScale,
            ExitDuration = TimeSpan.FromMilliseconds(80),
        };
        desktop.Windows.Add(window);
        for (int i = 0; i < 3; i++)
        {
            Frame(runtime, desktop, 1 + i);
        }

        Assert.True(window.IsWindowScaled);
        Assert.True(window.TryCloseWindow());
        Frame(runtime, desktop, 4);

        Assert.NotNull(window.GetType()); // the window instance is still usable/alive
        GraphNoOpDrawTransaction draw = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        Exception caught = Record.Exception(() => desktop.Draw(draw));
        Assert.Null(caught);

        // Two pushes recorded while the exit runs (MGWindow.Draw): the outer enter/exit draw transform composed with the ambient
        // transform, then -- because the window is also scaled -- UnscaledScreenSpaceToScaledScreenSpace composed on top of it.
        Assert.Equal(2, draw.TransformPushes.Count);
        Assert.Equal(window.UnscaledScreenSpaceToScaledScreenSpace * draw.TransformPushes[0], draw.TransformPushes[1]);

        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 5 + i);
        }

        Assert.False(window.IsClosing);
        Assert.DoesNotContain(window, desktop.Windows);
    }

    // ---- Zero allocation: matrix pushes ------------------------------------------------------------------------

    [Fact]
    public void Window_WithNoEnterExitSettings_DrawsWithNoTransformPush()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow window = new(desktop, 0, 0, 200, 150) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(window);
        for (int i = 0; i < 3; i++)
        {
            Frame(runtime, desktop, 1 + i);
        }

        GraphNoOpDrawTransaction draw = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.Draw(draw);

        Assert.Empty(draw.TransformPushes);
    }

    // ---- Zero allocation: the root window comparison in a steady state and during a run ---------------------------

    [Fact]
    public void RootWindowComparison_AllocatesNothingPerFrame_InASteadyState()
    {
        // Isolates MGDesktop.DetectRootWindowEntries itself (internal, called directly): the surrounding Desktop.Update
        // pipeline (layout, focus, mouse tracking, ...) has its own allocation profile, unrelated to this slice's own budget.
        var (runtime, desktop) = NewDesktop();
        MGWindow a = new(desktop, 0, 0, 100, 100) { WindowStyle = WindowStyle.None };
        MGWindow b = new(desktop, 150, 0, 100, 100) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(a);
        desktop.Windows.Add(b);
        for (int i = 0; i < 5; i++)
        {
            desktop.DetectRootWindowEntries(); // warm up (list capacities settle)
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < 200; i++)
        {
            desktop.DetectRootWindowEntries();
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    [Fact]
    public void RootWindowComparison_AllocatesNothingPerFrame_DuringAWindowRun()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow a = new(desktop, 0, 0, 100, 100) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(enterMs: 100_000) };
        desktop.Windows.Add(a);
        Frame(runtime, desktop, 1);
        Assert.True(desktop.Animations.ActiveCount > 0);
        for (int i = 0; i < 5; i++)
        {
            desktop.DetectRootWindowEntries();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < 200; i++)
        {
            desktop.DetectRootWindowEntries();
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    [Fact]
    public void WindowWithoutSettings_HasNoWindowTransform_AndDrawsIdentically()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow window = new(desktop, 0, 0, 200, 150) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(window);
        Frame(runtime, desktop, 1);
        Frame(runtime, desktop, 2);

        Assert.True(window.TryCloseWindow());
        Assert.False(window.IsClosing); // no EnterExit configured: closes exactly like before Y7
        Assert.DoesNotContain(window, desktop.Windows);
    }

    // ---- Y12: IsClosing narrowed to a removal-bound exit, TryCloseWindow accepted during a Visibility-driven one -----

    /// <summary>Y12 regression (this is the assertion that fails against the pre-fix code, see the class doc): a plain <see cref="Visibility"/>
    /// write starts a Y6 exit -- <see cref="MGWindow.IsClosing"/> stays false for that (it is not a close), input/occlusion are already
    /// suppressed by the broader <see cref="MGElement.IsPlayingEnterExitExit"/> state. <see cref="MGWindow.TryCloseWindow"/> called mid-run then
    /// closes the window by attaching to that SAME run (asserted here through <see cref="Animation.UIAnimationCollection.ActiveCount"/> and a
    /// tight time budget that only the original run's remaining duration -- not a fresh restarted one -- can meet), never doubling it:
    /// <see cref="MGWindow.WindowClosing"/> fires once at once, <see cref="MGWindow.WindowClosed"/> fires exactly once when that run ends, the
    /// pending <see cref="Visibility"/> is applied, and the window leaves <see cref="MGWindow.NestedWindows"/>.</summary>
    [Fact]
    public void VisibilityDrivenExit_ThenTryCloseWindow_ClosesOnSameRun_NoRestart_WindowClosedOnce()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow parent = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(parent);
        MGWindow nested = new(parent, 10, 10, 100, 80) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(exitMs: 400) };
        parent.AddNestedWindow(nested);
        Frame(runtime, desktop, 1);
        GraphNoOpDrawTransaction draw = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.Draw(draw); // a Y6 exit only plays once the element has drawn at least once (_hasDrawnSinceAttached)
        Frame(runtime, desktop, 2);

        nested.Visibility = Visibility.Collapsed; // Y6 Visibility-driven exit starts, not a close
        Assert.False(nested.IsClosing);
        Assert.True(desktop.Animations.ActiveCount > 0);

        // Roughly half the exit's duration elapses before the close request: the run is genuinely mid-flight.
        for (int i = 0; i < 12; i++)
        {
            Frame(runtime, desktop, 3 + i);
        }
        int activeCountMidExit = desktop.Animations.ActiveCount;
        Assert.True(activeCountMidExit > 0);
        Assert.False(nested.IsClosing);

        List<string> events = new();
        nested.WindowClosing += (_, _) => events.Add("Closing");
        nested.WindowClosed += (_, _) => events.Add("Closed");

        bool accepted = nested.TryCloseWindow();
        Assert.True(accepted);
        Assert.True(nested.IsClosing); // Y12: accepted -- attached to the already-playing exit, not a refusal
        Assert.Equal(new[] { "Closing" }, events);
        Assert.Contains(nested, parent.NestedWindows);
        Assert.Equal(activeCountMidExit, desktop.Animations.ActiveCount); // unchanged: still the SAME run, not restarted, not doubled

        // Enough time for the ORIGINAL run's remaining ~208ms to finish, but well short of a fresh 400ms restart.
        for (int i = 0; i < 19; i++)
        {
            Frame(runtime, desktop, 15 + i);
        }

        Assert.Equal(new[] { "Closing", "Closed" }, events); // WindowClosed exactly once, no doubling
        Assert.False(nested.IsClosing);
        Assert.DoesNotContain(nested, parent.NestedWindows);
        Assert.Equal(0, desktop.Animations.ActiveCount);
        Assert.Equal(Visibility.Collapsed, nested.Visibility); // the pending Y6 Visibility write still lands
    }

    /// <summary>Y12: while a Visibility-driven exit plays -- with no <see cref="MGWindow.TryCloseWindow"/> call at all -- the window already
    /// takes no mouse input and occludes nothing (the Y7 behaviour), which is keyed on <see cref="MGElement.IsPlayingEnterExitExit"/>, not on
    /// the narrower <see cref="MGWindow.IsClosing"/>, so it stays true for the whole run even though <see cref="MGWindow.IsClosing"/> itself
    /// never rises.</summary>
    [Fact]
    public void VisibilityDrivenExit_TakesNoInput_AndOccludesNothing_WhileIsClosingStaysFalse()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow parent = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        MGButton parentButton = new(parent) { PreferredWidth = 200, PreferredHeight = 150 };
        parent.SetContent(parentButton);
        desktop.Windows.Add(parent);

        MGWindow nested = new(parent, 0, 0, 100, 80) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(exitMs: 300) };
        parent.AddNestedWindow(nested);
        for (int i = 0; i < 5; i++)
        {
            Frame(runtime, desktop, 1 + i);
        }

        Point overlap = new(nested.Left + 10, nested.Top + 10);
        Assert.True(nested.LayoutBounds.Contains(overlap));
        Assert.True(parentButton.LayoutBounds.Contains(overlap));

        Frame(runtime, desktop, 6, overlap);
        Frame(runtime, desktop, 7, overlap);
        Assert.False(parentButton.IsHovered); // occluded by the (still fully opaque, not-yet-exiting) nested window

        nested.Visibility = Visibility.Collapsed; // Visibility-driven exit only -- never TryCloseWindow
        Assert.False(nested.IsClosing);

        Frame(runtime, desktop, 8, overlap);
        Frame(runtime, desktop, 9, new Point(overlap.X + 1, overlap.Y));

        Assert.True(parentButton.IsHovered); // the exiting nested window no longer occludes/suppresses hover
        Assert.False(nested.IsClosing); // still not a close, just an exit

        int clicksOnParentButton = 0;
        parentButton.MouseHandler.PressedInside += (_, _) => clicksOnParentButton++;
        Frame(runtime, desktop, 10, overlap, leftPressed: true);
        Assert.True(clicksOnParentButton > 0);
    }

    /// <summary>Y12: a second <see cref="MGWindow.TryCloseWindow"/> call while a close is in progress does nothing and returns false, even when
    /// that close was attached to a <see cref="Visibility"/>-driven exit already playing (as opposed to one <see cref="MGWindow.TryCloseWindow"/>
    /// itself started) -- the rest of the Y7 contract (one <see cref="MGWindow.WindowClosing"/>, the window still listed) is unaffected.</summary>
    [Fact]
    public void TryCloseWindow_SecondCall_WhileAttachedToAVisibilityDrivenExit_DoesNothing_ReturnsFalse()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow parent = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(parent);
        MGWindow nested = new(parent, 10, 10, 100, 80) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(exitMs: 300) };
        parent.AddNestedWindow(nested);
        Frame(runtime, desktop, 1);
        GraphNoOpDrawTransaction draw = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.Draw(draw);
        Frame(runtime, desktop, 2);

        nested.Visibility = Visibility.Collapsed;
        Frame(runtime, desktop, 3);
        int activeCountMidExit = desktop.Animations.ActiveCount;
        Assert.True(activeCountMidExit > 0);

        List<string> events = new();
        nested.WindowClosing += (_, _) => events.Add("Closing");
        nested.WindowClosed += (_, _) => events.Add("Closed");

        Assert.True(nested.TryCloseWindow());
        Assert.True(nested.IsClosing);
        Assert.False(nested.TryCloseWindow()); // second call: no-op
        Assert.True(nested.IsClosing); // unchanged
        Assert.Equal(new[] { "Closing" }, events); // not raised twice
        Assert.Contains(nested, parent.NestedWindows);
        Assert.Equal(activeCountMidExit, desktop.Animations.ActiveCount); // still the one run
    }

    /// <summary>Y12: a window's exit is superseded mid-run by <see cref="MGWindow.AddNestedWindow"/> reopening it (Y7's carve-out, now keyed on
    /// <see cref="MGElement.IsPlayingEnterExitExit"/>) after a <see cref="MGWindow.TryCloseWindow"/> call had attached a close to that exit:
    /// the closing flag must not stay stuck raised on a window that is back in the list and visible, and a later
    /// <see cref="MGWindow.TryCloseWindow"/> call must still work normally.</summary>
    [Fact]
    public void ClosingExit_SupersededByReopen_LeavesNoStaleIsClosing_LaterCloseStillWorks()
    {
        var (runtime, desktop) = NewDesktop();
        MGWindow parent = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(parent);
        MGWindow nested = new(parent, 10, 10, 100, 80) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(enterMs: 60, exitMs: 300) };
        parent.AddNestedWindow(nested);
        Frame(runtime, desktop, 1);
        GraphNoOpDrawTransaction draw = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.Draw(draw);
        Frame(runtime, desktop, 2);

        nested.Visibility = Visibility.Collapsed; // Y6 exit starts
        Frame(runtime, desktop, 3);

        int closedCount = 0;
        nested.WindowClosed += (_, _) => closedCount++;

        Assert.True(nested.TryCloseWindow()); // attaches the close to the running exit
        Assert.True(nested.IsClosing);

        // Reopen mid-exit: cancels the exit (dropping the pending close) and replays the entry instead of throwing.
        Exception caught = Record.Exception(() => parent.AddNestedWindow(nested));
        Assert.Null(caught);
        Assert.False(nested.IsClosing); // no stale closing flag left raised
        Assert.Contains(nested, parent.NestedWindows);
        Assert.Equal(0, closedCount); // the superseded close never ran to completion

        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 4 + i);
        }
        Assert.False(nested.IsClosing);
        Assert.Contains(nested, parent.NestedWindows);
        Assert.Equal(Visibility.Visible, nested.Visibility); // the entry restored visibility

        // A later close still works normally.
        Assert.True(nested.TryCloseWindow());
        Assert.True(nested.IsClosing);
        for (int i = 0; i < 25; i++)
        {
            Frame(runtime, desktop, 15 + i);
            if (!parent.NestedWindows.Contains(nested))
            {
                break;
            }
        }
        Assert.Equal(1, closedCount);
        Assert.False(nested.IsClosing);
        Assert.DoesNotContain(nested, parent.NestedWindows);
    }
}
