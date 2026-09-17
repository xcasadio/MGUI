using System;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Animation;

/// <summary>Slice Y8 (Docs/decisions/0011-animation-v5.md, "Entree et sortie", tooltips/menus/theme): a tooltip's and a context menu's
/// (including one level of submenu) entry on show/open and exit kept drawn in a dedicated exiting slot until the run ends -- no input, no
/// occlusion, at most one occupant per slot -- and the theme's <c>Animation</c> popup group (<c>OpenDuration</c>/<c>CloseDuration</c>/
/// <c>OpenEasing</c>/<c>CloseEasing</c>/<c>PopupEffect</c>) resolved for a window/tooltip/context menu/dropdown with no explicit
/// <see cref="UIEnterExitSettings"/> of its own, only when <see cref="MGThemeAnimationSettings.Enabled"/> is true.</summary>
public class PopupEnterExitTests
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

    /// <summary>Runs enough frames for <see cref="MGElement.ActualLayoutBounds"/> (which hit-testing/hover reads, unlike <see cref="MGElement.LayoutBounds"/>)
    /// to settle after content is attached -- a single frame leaves it at its default zero size.</summary>
    private static void Settle(GraphTestRuntime runtime, MGDesktop desktop)
    {
        for (int i = 0; i < 5; i++)
        {
            Frame(runtime, desktop, i + 1);
        }
    }

    private static (GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, MGButton Host) NewSceneWithHost()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        MGButton host = new(window)
        {
            PreferredWidth = 100,
            PreferredHeight = 30,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        window.SetContent(host);
        desktop.Windows.Add(window);
        return (runtime, desktop, window, host);
    }

    private static MGToolTip NewToolTip(MGWindow window, MGButton host, UIEnterExitSettings enterExit = null)
    {
        MGToolTip toolTip = new(window, host, 80, 24) { ShowDelayOverride = TimeSpan.Zero };
        if (enterExit != null)
        {
            toolTip.EnterExit = enterExit;
        }

        host.ToolTip = toolTip;
        return toolTip;
    }

    // ---- ToolTip: entry ------------------------------------------------------------------------------------------

    [Fact]
    public void ToolTip_ShowingIt_PlaysEntry_ThenReachesBaseOpacity()
    {
        var (runtime, desktop, window, host) = NewSceneWithHost();
        MGToolTip toolTip = NewToolTip(window, host, FadeSettings());
        toolTip.Opacity = 1f;

        Settle(runtime, desktop);
        Point hoverPos = host.LayoutBounds.Center;

        Frame(runtime, desktop, 10, hoverPos);
        Assert.Same(toolTip, desktop.ActiveToolTip);
        Assert.True(desktop.Animations.ActiveCount > 0);

        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 11 + i, hoverPos);
        }

        Assert.Equal(1f, toolTip.Opacity, 3);
    }

    // ---- ToolTip: exit drawn at a frozen position ------------------------------------------------------------------

    [Fact]
    public void ToolTip_Exit_IsDrawnAtAFrozenPosition_MouseMovingDuringTheRun_DoesNotMoveIt()
    {
        var (runtime, desktop, window, host) = NewSceneWithHost();
        MGToolTip toolTip = NewToolTip(window, host, FadeSettings(exitMs: 300));
        toolTip.DrawOffset = new Point(10, 10);

        Settle(runtime, desktop);
        Point hoverPos = host.LayoutBounds.Center;

        Frame(runtime, desktop, 10, hoverPos);
        Assert.Same(toolTip, desktop.ActiveToolTip);

        // Move the mouse away: the tooltip stops being Active and its exit starts, frozen at THIS frame's mouse position.
        Point awayPos = new(390, 290);
        Frame(runtime, desktop, 11, awayPos);
        Assert.Null(desktop.ActiveToolTip);
        Assert.Same(toolTip, desktop.State.ExitingToolTip);
        Point frozen = desktop.State.ExitingToolTipDrawPosition;
        Assert.Equal(awayPos + toolTip.DrawOffset, frozen);

        // Move the mouse further during the exit (still well away from the host, whose bounds start at the window's own
        // origin): the recorded draw position must not follow it.
        Frame(runtime, desktop, 12, new Point(250, 250));
        Frame(runtime, desktop, 13, new Point(200, 200));
        Assert.Same(toolTip, desktop.State.ExitingToolTip);
        Assert.Equal(frozen, desktop.State.ExitingToolTipDrawPosition);

        for (int i = 0; i < 20; i++)
        {
            Frame(runtime, desktop, 14 + i, new Point(200, 200));
        }

        Assert.Null(desktop.State.ExitingToolTip);
    }

    // ---- ToolTip: replacement ends the previous exit at once, no residual run --------------------------------------

    [Fact]
    public void ToolTip_ReplacedByADifferentToolTip_EndsThePreviousExitAtOnce_NoResidualRun()
    {
        var (runtime, desktop, window, hostA) = NewSceneWithHost();
        MGButton hostB = new(window) { PreferredWidth = 100, PreferredHeight = 30, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        MGStackPanel panel = new(window, Orientation.Vertical) { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        window.SetContent(panel);
        panel.TryAddChild(hostA);
        panel.TryAddChild(hostB);

        MGToolTip toolTipA = NewToolTip(window, hostA, FadeSettings(exitMs: 500));
        MGToolTip toolTipB = NewToolTip(window, hostB, FadeSettings());

        Settle(runtime, desktop);
        Point hostACenter = hostA.LayoutBounds.Center;
        Point hostBCenter = hostB.LayoutBounds.Center;

        Frame(runtime, desktop, 10, hostACenter);
        Assert.Same(toolTipA, desktop.ActiveToolTip);

        Frame(runtime, desktop, 11, hostBCenter);
        Assert.Same(toolTipB, desktop.ActiveToolTip);
        // toolTipA's exit was cut short at once by toolTipB's arrival: no residual entry left tracked.
        Assert.NotSame(toolTipA, desktop.State.ExitingToolTip);
    }

    // ---- ToolTip: the same tooltip re-queued during its own exit cancels it (no second exit) --------------------

    [Fact]
    public void ToolTip_TheSameToolTip_QueuedAgainDuringItsOwnExit_CancelsTheExit_NoSecondExit()
    {
        var (runtime, desktop, window, host) = NewSceneWithHost();
        MGToolTip toolTip = NewToolTip(window, host, FadeSettings(exitMs: 500));
        toolTip.Opacity = 1f;

        Settle(runtime, desktop);
        Point hoverPos = host.LayoutBounds.Center;

        Frame(runtime, desktop, 10, hoverPos);
        Assert.Same(toolTip, desktop.ActiveToolTip);

        Frame(runtime, desktop, 11, new Point(390, 290));
        Assert.Null(desktop.ActiveToolTip);
        Assert.Same(toolTip, desktop.State.ExitingToolTip);
        Assert.True(toolTip.IsClosing);

        // Hover the host again while the exit is still running: cancels the exit and returns to Active (no second, independent exit).
        Frame(runtime, desktop, 12, hoverPos);
        Assert.Same(toolTip, desktop.ActiveToolTip);
        Assert.Null(desktop.State.ExitingToolTip);
        Assert.False(toolTip.IsClosing);

        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 13 + i, hoverPos);
        }

        Assert.Equal(1f, toolTip.Opacity, 3);
    }

    // ---- ToolTip: a click on an exiting tooltip's area still reaches the element below --------------------------

    [Fact]
    public void ToolTip_Exiting_NeverConsumesInput_HostClickStillWorks()
    {
        var (runtime, desktop, window, host) = NewSceneWithHost();
        MGToolTip toolTip = NewToolTip(window, host, FadeSettings(exitMs: 500));

        Settle(runtime, desktop);
        Point hoverPos = host.LayoutBounds.Center;

        Frame(runtime, desktop, 10, hoverPos);
        Assert.Same(toolTip, desktop.ActiveToolTip);

        Frame(runtime, desktop, 11, new Point(390, 290));
        Assert.Same(toolTip, desktop.State.ExitingToolTip);

        int clicks = 0;
        host.MouseHandler.PressedInside += (_, _) => clicks++;
        Frame(runtime, desktop, 12, hoverPos, leftPressed: true);
        Assert.True(clicks > 0);
    }

    // ---- ToolTip: no exit configured -> disappears at once, no slot used (3b) -----------------------------------

    [Fact]
    public void ToolTip_WithNoExitConfigured_DisappearsAtOnce_NoSlotUsed()
    {
        var (runtime, desktop, window, host) = NewSceneWithHost();
        MGToolTip toolTip = NewToolTip(window, host);

        Settle(runtime, desktop);
        Point hoverPos = host.LayoutBounds.Center;

        Frame(runtime, desktop, 10, hoverPos);
        Assert.Same(toolTip, desktop.ActiveToolTip);

        Frame(runtime, desktop, 11, new Point(390, 290));
        Assert.Null(desktop.ActiveToolTip);
        Assert.Null(desktop.State.ExitingToolTip);
    }

    // ---- Context menu: entry on open, exit kept drawn until the end, TryCloseActiveContextMenu contract --------

    [Fact]
    public void ContextMenu_Open_PlaysEntry_Close_KeepsItDrawnUntilTheExitEnds_ThenClears()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(window);
        MGContextMenu menu = new(desktop, "Menu") { EnterExit = FadeSettings(exitMs: 300) };
        menu.AddButton("Item", _ => { });

        Assert.True(desktop.TryOpenContextMenu(menu, Point.Zero));
        Assert.Same(menu, desktop.ActiveContextMenu);
        Frame(runtime, desktop, 1);
        Assert.True(desktop.Animations.ActiveCount > 0);

        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 2 + i);
        }

        // Close: the return contract is unchanged, the menu is logically closed at once (ActiveContextMenu null,
        // IsContextMenuOpen false), but it keeps being drawn in the exiting slot.
        bool closed = desktop.TryCloseActiveContextMenu();
        Assert.True(closed);
        Assert.Null(desktop.ActiveContextMenu);
        Assert.False(menu.IsContextMenuOpen);
        Assert.Same(menu, desktop.State.ExitingContextMenu);
        Assert.True(menu.IsClosing);

        // Closing again while there is nothing active still returns true (unchanged contract).
        Assert.True(desktop.TryCloseActiveContextMenu());

        for (int i = 0; i < 20; i++)
        {
            Frame(runtime, desktop, 12 + i);
        }

        Assert.Null(desktop.State.ExitingContextMenu);
    }

    // ---- Context menu: opening another menu ends the previous exit at once ---------------------------------------

    [Fact]
    public void ContextMenu_OpeningAnotherMenu_EndsThePreviousExitAtOnce()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(window);
        MGContextMenu menuA = new(desktop, "A") { EnterExit = FadeSettings(exitMs: 500) };
        menuA.AddButton("Item", _ => { });
        MGContextMenu menuB = new(desktop, "B") { EnterExit = FadeSettings() };
        menuB.AddButton("Item", _ => { });

        Assert.True(desktop.TryOpenContextMenu(menuA, Point.Zero));
        Frame(runtime, desktop, 1);
        Assert.True(desktop.TryCloseActiveContextMenu());
        Assert.Same(menuA, desktop.State.ExitingContextMenu);

        Assert.True(desktop.TryOpenContextMenu(menuB, new Point(50, 50)));
        Assert.Same(menuB, desktop.ActiveContextMenu);
        Assert.NotSame(menuA, desktop.State.ExitingContextMenu);
    }

    // ---- Submenu: entry/exit one level at a time -------------------------------------------------------------------

    [Fact]
    public void Submenu_EntryOnOpen_ExitKeptDrawnUntilTheEnd_OneLevelAtATime()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(window);
        MGContextMenu parent = new(desktop, "Parent");
        parent.AddButton("Item", _ => { });
        MGContextMenu submenu = new(parent) { EnterExit = FadeSettings(exitMs: 300) };
        submenu.AddButton("Sub item", _ => { });

        Assert.True(desktop.TryOpenContextMenu(parent, Point.Zero));
        Frame(runtime, desktop, 1);

        Assert.True(parent.TryOpenContextMenu(submenu, new Rectangle(10, 10, 1, 1)));
        Assert.Same(submenu, parent.ActiveContextMenu);
        Frame(runtime, desktop, 2);
        Assert.True(desktop.Animations.ActiveCount > 0);

        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 3 + i);
        }

        Assert.True(parent.TryCloseActiveContextMenu());
        Assert.Null(parent.ActiveContextMenu);
        Assert.True(submenu.IsClosing);

        for (int i = 0; i < 20; i++)
        {
            Frame(runtime, desktop, 13 + i);
        }

        Assert.False(submenu.IsClosing);
    }

    // ---- Theme: the popup group applies to window/tooltip/context menu/dropdown, only when Enabled ------------------

    private static MGTheme EnabledPopupTheme(MGTheme source, int openMs = 40, int closeMs = 40)
    {
        MGTheme theme = source.Copy();
        theme.Animation.Enabled = true;
        theme.Animation.OpenDuration = TimeSpan.FromMilliseconds(openMs);
        theme.Animation.CloseDuration = TimeSpan.FromMilliseconds(closeMs);
        theme.Animation.PopupEffect = UIEnterExitEffect.Fade;
        return theme;
    }

    [Fact]
    public void Theme_Enabled_GivesAWindowWithNoExplicitSettings_TheDefaultEntry()
    {
        var (runtime, desktop, window, _) = NewSceneWithHost();
        desktop.Resources.DefaultTheme = EnabledPopupTheme(desktop.Theme);

        MGWindow nested = new(window, 10, 10, 100, 80) { WindowStyle = WindowStyle.None };
        window.AddNestedWindow(nested);
        Frame(runtime, desktop, 1);

        Assert.True(desktop.Animations.ActiveCount > 0);
        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 2 + i);
        }

        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    [Fact]
    public void Theme_Enabled_ExplicitEnterExitOnTheElement_Wins()
    {
        var (runtime, desktop, window, _) = NewSceneWithHost();
        // Settle the scene's own root window entry BEFORE the theme is enabled, so only the nested window below is affected.
        Frame(runtime, desktop, 1);
        desktop.Resources.DefaultTheme = EnabledPopupTheme(desktop.Theme, openMs: 1000);

        MGWindow nested = new(window, 10, 10, 100, 80) { WindowStyle = WindowStyle.None, EnterExit = FadeSettings(enterMs: 20) };
        window.AddNestedWindow(nested);
        Frame(runtime, desktop, 2);

        for (int i = 0; i < 5; i++)
        {
            Frame(runtime, desktop, 3 + i);
        }

        // The explicit 20ms entry has already finished after 5*16=80ms; the theme's 1000ms one would not have.
        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    [Fact]
    public void Theme_Disabled_NoPopupRunEverStarts()
    {
        var (runtime, desktop, window, host) = NewSceneWithHost();
        Assert.False(desktop.Theme.Animation.Enabled);

        // Well away from the host button (0,0)-(100,30) so it does not occlude the later hover check.
        MGWindow nested = new(window, 200, 200, 100, 80) { WindowStyle = WindowStyle.None };
        window.AddNestedWindow(nested);
        Frame(runtime, desktop, 1);
        Assert.Equal(0, desktop.Animations.ActiveCount);

        MGToolTip toolTip = NewToolTip(window, host);
        Settle(runtime, desktop);
        Frame(runtime, desktop, 10, host.LayoutBounds.Center);
        Assert.Same(toolTip, desktop.ActiveToolTip);
        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    [Fact]
    public void Theme_Enabled_ToolTipWithNoExplicitSettings_PlaysTheThemeEffect()
    {
        var (runtime, desktop, window, host) = NewSceneWithHost();
        Settle(runtime, desktop);
        desktop.Resources.DefaultTheme = EnabledPopupTheme(desktop.Theme);

        MGToolTip toolTip = NewToolTip(window, host);
        Point hoverPos = host.LayoutBounds.Center;

        Frame(runtime, desktop, 10, hoverPos);
        Assert.Same(toolTip, desktop.ActiveToolTip);
        Assert.True(desktop.Animations.ActiveCount > 0);

        for (int i = 0; i < 10; i++)
        {
            Frame(runtime, desktop, 11 + i, hoverPos);
        }

        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    [Fact]
    public void Theme_Enabled_ContextMenuAndDropdown_PlayTheThemeEffect()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(window);
        desktop.Resources.DefaultTheme = EnabledPopupTheme(desktop.Theme);

        MGContextMenu menu = new(desktop, "Menu");
        menu.AddButton("Item", _ => { });
        Assert.True(desktop.TryOpenContextMenu(menu, Point.Zero));
        Frame(runtime, desktop, 1);
        Assert.True(desktop.Animations.ActiveCount > 0);

        MGComboBox<string> comboBox = new(window) { PreferredHeight = 30 };
        comboBox.SetItemsSource(new System.Collections.Generic.List<string> { "A", "B" });
        window.SetContent(comboBox);
        Frame(runtime, desktop, 2);
        comboBox.IsDropdownOpen = true;
        Frame(runtime, desktop, 3);
        Assert.True(desktop.Animations.ActiveCount > 0);
    }

    [Fact]
    public void Theme_SwapMidExit_TheRunFinishesWithItsOriginalDuration()
    {
        var (runtime, desktop, window, _) = NewSceneWithHost();
        MGTheme enabled = EnabledPopupTheme(desktop.Theme, closeMs: 300);
        desktop.Resources.DefaultTheme = enabled;

        MGWindow nested = new(window, 10, 10, 100, 80) { WindowStyle = WindowStyle.None };
        window.AddNestedWindow(nested);
        Frame(runtime, desktop, 1);
        for (int i = 0; i < 5; i++)
        {
            Frame(runtime, desktop, 2 + i);
        }

        Assert.True(nested.TryCloseWindow());
        Assert.True(nested.IsClosing);
        Frame(runtime, desktop, 7);

        // Swap the theme mid-exit: a much shorter CloseDuration must NOT retroactively shorten the already-resolved run.
        MGTheme swapped = EnabledPopupTheme(desktop.Theme, closeMs: 1);
        desktop.Resources.DefaultTheme = swapped;
        Frame(runtime, desktop, 8);

        Assert.True(nested.IsClosing); // still running past what the new 1ms duration would already have finished
    }

    [Fact]
    public void FloatingDockWindow_IgnoresTheThemePopupDefaults_EvenWhenEnabled()
    {
        var (runtime, desktop, window, _) = NewSceneWithHost();
        desktop.Resources.DefaultTheme = EnabledPopupTheme(desktop.Theme);

        MGUI.Core.UI.Docking.Controls.MGDockHost host = new(window);
        MGUI.Core.UI.Docking.DockLayout.DockPanelNode panel = new() { Title = "A", ContentFactory = () => new MGBorder(window) };
        MGUI.Core.UI.Docking.Controls.MGFloatingDockWindow floating = new(host, panel, 0, 0, 200, 150);
        window.AddNestedWindow(floating);
        Frame(runtime, desktop, 1);

        Assert.True(floating.SuppressWindowEnterExit);
        Assert.True(floating.TryCloseWindow());
        Assert.False(floating.IsClosing); // never plays a theme-driven exit either
    }
}
