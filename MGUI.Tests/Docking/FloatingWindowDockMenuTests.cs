using System;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Docking;

/// <summary>
/// Covers task 4 of <c>Docs/Tasks/docking-bugs-tasks.md</c>: a "Dock" entry in a floated tab's
/// context menu (<see cref="MGDockTabItem.DockRequested"/>) that lets the user re-dock a floated
/// panel without dragging. <see cref="MGDockHost"/> remembers, in memory only, which tab group a
/// panel was floated out of (recorded by <see cref="MGDockHost.DetachToFloating"/>) so
/// <see cref="MGDockHost.RedockPanel"/> can send it back there, falling back to the first visible
/// tab group when the source group no longer exists.
/// </summary>
public class FloatingWindowDockMenuTests
{
    private sealed class Harness
    {
        public GraphTestRuntime Runtime;
        public MGDesktop Desktop;
        public MGWindow MainWindow;
        public MGDockHost Host;

        /// <summary>Alone in its own group - floating it removes the group from the model.</summary>
        public DockPanelNode PanelC;
        public DockTabGroupNode GroupC;

        /// <summary>Shares <see cref="GroupAX"/> with a second panel so the group survives floating.</summary>
        public DockPanelNode PanelA;
        public DockTabGroupNode GroupAX;

        /// <summary>Shares <see cref="GroupBY"/> with a second panel so the group survives floating.</summary>
        public DockPanelNode PanelB;
        public DockTabGroupNode GroupBY;
    }

    /// <summary>
    /// Layout, left to right: GroupBY (B, Y) | GroupAX (A, X) | GroupC (C alone).
    /// <see cref="MGDockHost.GetAllVisibleTabGroups"/> visits FirstChild before SecondChild at every
    /// split, so the visible order is [GroupBY, GroupAX, GroupC] - GroupBY is deliberately NOT the
    /// group any of A/B/C are floated from, so a mutant that always falls back to the first visible
    /// group instead of the remembered one is caught by tests (c)/(e).
    /// </summary>
    private static Harness CreateHarness()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 900, 600));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, 900, 600) { WindowStyle = WindowStyle.None };

        DockPanelNode panelA = new() { Title = "A", ContentFactory = () => new MGBorder(mainWindow) };
        DockPanelNode panelX = new() { Title = "X", ContentFactory = () => new MGBorder(mainWindow) };
        DockPanelNode panelB = new() { Title = "B", ContentFactory = () => new MGBorder(mainWindow) };
        DockPanelNode panelY = new() { Title = "Y", ContentFactory = () => new MGBorder(mainWindow) };
        DockPanelNode panelC = new() { Title = "C", ContentFactory = () => new MGBorder(mainWindow) };

        DockTabGroupNode groupAX = new();
        groupAX.AddPanel(panelA, -1);
        groupAX.AddPanel(panelX, -1);

        DockTabGroupNode groupBY = new();
        groupBY.AddPanel(panelB, -1);
        groupBY.AddPanel(panelY, -1);

        DockTabGroupNode groupC = new();
        groupC.AddPanel(panelC, -1);

        DockSplitNode innerSplit = new()
        {
            Orientation = Orientation.Horizontal,
            SplitRatio = 0.5f,
            MinFirstSize = 0,
            MinSecondSize = 0,
            FirstChild = groupAX,
            SecondChild = groupC,
        };

        DockSplitNode rootSplit = new()
        {
            Orientation = Orientation.Horizontal,
            SplitRatio = 0.34f,
            MinFirstSize = 0,
            MinSecondSize = 0,
            FirstChild = groupBY,
            SecondChild = innerSplit,
        };

        MGDockHost host = new(mainWindow)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LayoutModel = new DockLayoutModel(rootSplit),
        };

        mainWindow.SetContent(host);
        desktop.Windows.Add(mainWindow);

        AdvanceFrame(runtime, desktop, 0, Point.Zero);
        AdvanceFrame(runtime, desktop, 16, Point.Zero);

        return new Harness
        {
            Runtime = runtime,
            Desktop = desktop,
            MainWindow = mainWindow,
            Host = host,
            PanelC = panelC,
            GroupC = groupC,
            PanelA = panelA,
            GroupAX = groupAX,
            PanelB = panelB,
            GroupBY = groupBY,
        };
    }

    private static MGFloatingDockWindow FloatPanel(Harness harness, DockPanelNode panel)
        => FloatPanel(harness, panel, new Point(700, 500));

    /// <summary>
    /// Floats <paramref name="panel"/> at <paramref name="dropPosition"/>. Callers that float more
    /// than one panel in the same test must use non-overlapping positions - otherwise the resulting
    /// floating windows occupy identical screen bounds and a mouse-driven right-click aimed at one
    /// tab actually lands on whichever window is topmost.
    /// </summary>
    private static MGFloatingDockWindow FloatPanel(Harness harness, DockPanelNode panel, Point dropPosition)
    {
        MGFloatingDockWindow floatingWindow = harness.Host.DetachToFloating(panel, dropPosition);
        AdvanceFrame(harness.Runtime, harness.Desktop, 32, Point.Zero);
        AdvanceFrame(harness.Runtime, harness.Desktop, 48, Point.Zero);
        return floatingWindow;
    }

    private static MGDockTabItem GetTabItem(MGFloatingDockWindow floatingWindow, DockPanelNode panel)
        => floatingWindow.TabGroup.TraverseVisualTree<MGDockTabItem>(IncludeSelf: false)
            .Single(t => t.Panel == panel);

    private static MGDockTabItem GetSoleDockedTabItem(MGDockTabGroup group)
        => Assert.Single(group.TraverseVisualTree<MGDockTabItem>(IncludeSelf: false));

    private static void OpenContextMenu(Harness harness, MGDockTabItem tab)
    {
        Point onTab = tab.LayoutBounds.Center;
        AdvanceFrame(harness.Runtime, harness.Desktop, 64, onTab, MouseButton.Right);
        AdvanceFrame(harness.Runtime, harness.Desktop, 80, onTab, MouseButton.Right);
        AdvanceFrame(harness.Runtime, harness.Desktop, 96, onTab);
    }

    private static MGContextMenuButton FindMenuButton(MGContextMenu menu, string text)
        => menu.Items.OfType<MGContextMenuButton>()
            .FirstOrDefault(b => (b.MenuItemContent as MGTextBlock)?.Text == text);

    // ── (a)/(b): menu contents ───────────────────────────────────────────────

    [Fact]
    public void DockedTabContextMenu_HasFloat_NotDock()
    {
        Harness harness = CreateHarness();
        MGDockTabItem tab = GetSoleDockedTabItem(
            Assert.Single(harness.Host.GetAllVisibleTabGroups(), g => g.GroupNode == harness.GroupC));

        OpenContextMenu(harness, tab);
        MGContextMenu menu = harness.Desktop.ActiveContextMenu;
        Assert.NotNull(menu);

        Assert.NotNull(FindMenuButton(menu, "Float"));
        Assert.Null(FindMenuButton(menu, "Dock"));
    }

    [Fact]
    public void FloatedTabContextMenu_HasDock_NotFloat()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = FloatPanel(harness, harness.PanelC);
        MGDockTabItem tab = GetTabItem(floatingWindow, harness.PanelC);

        OpenContextMenu(harness, tab);
        MGContextMenu menu = harness.Desktop.ActiveContextMenu;
        Assert.NotNull(menu);

        Assert.NotNull(FindMenuButton(menu, "Dock"));
        Assert.Null(FindMenuButton(menu, "Float"));
    }

    // ── (c): "Dock" re-docks into the remembered source group ────────────────

    [Fact]
    public void Dock_RedocksIntoRememberedSourceGroup_AndClosesFloatingWindow()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = FloatPanel(harness, harness.PanelA);
        MGDockTabItem tab = GetTabItem(floatingWindow, harness.PanelA);

        OpenContextMenu(harness, tab);
        MGContextMenu menu = harness.Desktop.ActiveContextMenu;
        MGContextMenuButton dockButton = FindMenuButton(menu, "Dock");
        Assert.NotNull(dockButton);

        dockButton.Action.Invoke(dockButton);

        Assert.Empty(harness.Host.FloatingWindows);
        Assert.Empty(harness.MainWindow.NestedWindows);
        Assert.Contains(harness.PanelA, harness.GroupAX.Panels);
        Assert.Equal(harness.PanelA.Id, harness.GroupAX.ActivePanelId);

        // Not sent to the first visible group (GroupBY) - proves the remembered group won, not a fallback.
        Assert.DoesNotContain(harness.PanelA, harness.GroupBY.Panels);
        Assert.Same(harness.GroupBY, harness.Host.GetAllVisibleTabGroups().First().GroupNode);
    }

    // ── (d): source group gone -> falls back to the first visible group ─────

    [Fact]
    public void Dock_WhenSourceGroupNoLongerExists_FallsBackToFirstVisibleGroup()
    {
        Harness harness = CreateHarness();

        // GroupC contains only C: floating it removes GroupC from the model entirely.
        MGFloatingDockWindow floatingWindow = FloatPanel(harness, harness.PanelC);
        Assert.DoesNotContain(harness.Host.LayoutModel.GetAllTabGroups(), g => g == harness.GroupC);

        MGDockTabItem tab = GetTabItem(floatingWindow, harness.PanelC);
        OpenContextMenu(harness, tab);
        MGContextMenu menu = harness.Desktop.ActiveContextMenu;
        MGContextMenuButton dockButton = FindMenuButton(menu, "Dock");
        Assert.NotNull(dockButton);

        dockButton.Action.Invoke(dockButton);

        Assert.Empty(harness.Host.FloatingWindows);

        MGDockTabGroup firstVisible = harness.Host.GetAllVisibleTabGroups().First();
        Assert.Same(harness.GroupBY, firstVisible.GroupNode); // GroupBY is first: FirstChild-before-SecondChild
        Assert.Contains(harness.PanelC, harness.GroupBY.Panels);
    }

    // ── (e): two panels floated separately each return to their own group ───

    [Fact]
    public void Dock_TwoPanelsFloatedSeparately_EachReturnsToItsOwnGroup()
    {
        Harness harness = CreateHarness();

        MGFloatingDockWindow floatA = FloatPanel(harness, harness.PanelA, new Point(700, 500));
        MGFloatingDockWindow floatB = FloatPanel(harness, harness.PanelB, new Point(200, 150));

        MGDockTabItem tabA = GetTabItem(floatA, harness.PanelA);
        OpenContextMenu(harness, tabA);
        MGContextMenuButton dockA = FindMenuButton(harness.Desktop.ActiveContextMenu, "Dock");
        Assert.NotNull(dockA);
        dockA.Action.Invoke(dockA);

        MGDockTabItem tabB = GetTabItem(floatB, harness.PanelB);
        OpenContextMenu(harness, tabB);
        MGContextMenuButton dockB = FindMenuButton(harness.Desktop.ActiveContextMenu, "Dock");
        Assert.NotNull(dockB);
        dockB.Action.Invoke(dockB);

        Assert.Empty(harness.Host.FloatingWindows);
        Assert.Contains(harness.PanelA, harness.GroupAX.Panels);
        Assert.Contains(harness.PanelB, harness.GroupBY.Panels);
        Assert.DoesNotContain(harness.PanelA, harness.GroupBY.Panels);
        Assert.DoesNotContain(harness.PanelB, harness.GroupAX.Panels);
    }

    // ── (f): saved layout format is unaffected by the in-memory float memory ─

    [Fact]
    public void LayoutSerialization_IsUnaffectedByFloatMemory_WhileAPanelIsFloating()
    {
        Harness harness = CreateHarness();
        FloatPanel(harness, harness.PanelA);

        string json = DockLayoutSerializer.ToJson(harness.Host.LayoutModel);

        // The remembered float-source mapping must never leak into the saved format.
        Assert.DoesNotContain("floatedFrom", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sourceGroup", json, StringComparison.OrdinalIgnoreCase);

        DockLayoutModel reloaded = DockLayoutSerializer.FromJson(json);
        var reloadedIds = reloaded.GetAllPanels().Select(p => p.Id).ToList();

        // A is floating (not in the docked layout being serialized); X, B, Y, C remain docked.
        Assert.DoesNotContain(harness.PanelA.Id, reloadedIds);
        Assert.Contains(harness.PanelB.Id, reloadedIds);
        Assert.Contains(harness.PanelC.Id, reloadedIds);
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

    private static MouseState CreateMouseState(Point position, MouseButton? pressedButton)
        => new(
            position.X,
            position.Y,
            0,
            pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            pressedButton == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
}
