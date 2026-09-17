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

        /// <summary>Alone in its own group - floating it leaves GroupC as a hidden placeholder (D2).</summary>
        public DockPanelNode PanelC;
        public DockTabGroupNode GroupC;

        /// <summary>Shares <see cref="GroupAX"/> with a second panel so the group survives floating.</summary>
        public DockPanelNode PanelA;
        public DockPanelNode PanelX;
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
            PanelX = panelX,
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
        Assert.Equal(0, harness.GroupAX.IndexOf(harness.PanelA)); // D3: back at its original tab index

        // Not sent to the first visible group (GroupBY) - proves the remembered group won, not a fallback.
        Assert.DoesNotContain(harness.PanelA, harness.GroupBY.Panels);
        Assert.Same(harness.GroupBY, harness.Host.GetAllVisibleTabGroups().First().GroupNode);
    }

    // ── (d): source group still exists -> the placeholder group returns identically ─────

    [Fact]
    public void Dock_WhenSourceGroupStillExists_RestoresTheExactPlaceholderGroup_AndJsonMatchesOriginal()
    {
        Harness harness = CreateHarness();
        string originalJson = DockLayoutSerializer.ToJson(harness.Host.LayoutModel);

        // GroupC contains only C: floating it leaves GroupC in the tree, hidden, as a placeholder (D2)
        // - it is not removed, only its visual disappears.
        MGFloatingDockWindow floatingWindow = FloatPanel(harness, harness.PanelC);
        Assert.Contains(harness.Host.LayoutModel.GetAllTabGroups(), g => g == harness.GroupC);
        Assert.True(harness.GroupC.IsHiddenInLayout);
        Assert.DoesNotContain(harness.Host.GetAllVisibleTabGroups(), g => g.GroupNode == harness.GroupC);

        var parentSplit = Assert.IsType<DockSplitNode>(harness.GroupC.Parent);
        var ratioBeforeDock = parentSplit.SplitRatio;

        MGDockTabItem tab = GetTabItem(floatingWindow, harness.PanelC);
        OpenContextMenu(harness, tab);
        MGContextMenu menu = harness.Desktop.ActiveContextMenu;
        MGContextMenuButton dockButton = FindMenuButton(menu, "Dock");
        Assert.NotNull(dockButton);

        dockButton.Action.Invoke(dockButton);

        Assert.Empty(harness.Host.FloatingWindows);
        Assert.Empty(harness.MainWindow.NestedWindows);

        // Same node, same parent split, same ratio.
        Assert.Same(parentSplit, harness.GroupC.Parent);
        Assert.Equal(ratioBeforeDock, parentSplit.SplitRatio);
        Assert.Contains(harness.PanelC, harness.GroupC.Panels);
        Assert.False(harness.GroupC.IsHiddenInLayout);

        Assert.Equal(originalJson, DockLayoutSerializer.ToJson(harness.Host.LayoutModel));
    }

    // ── source group gone (root replaced) -> falls back to the first visible group ─────

    [Fact]
    public void Dock_WhenApplicationReplacedTheRoot_FallsBackToFirstVisibleGroup()
    {
        Harness harness = CreateHarness();

        MGFloatingDockWindow floatingWindow = FloatPanel(harness, harness.PanelC);

        // The application swaps in a brand-new tree (new ids) - GroupC's placement now points
        // to a group that no longer exists anywhere in the model.
        DockPanelNode panelZ = new() { Title = "Z", ContentFactory = () => new MGBorder(harness.MainWindow) };
        DockTabGroupNode newGroup = new();
        newGroup.AddPanel(panelZ, -1);
        harness.Host.LayoutModel.RootNode = newGroup;
        AdvanceFrame(harness.Runtime, harness.Desktop, 160, Point.Zero);
        AdvanceFrame(harness.Runtime, harness.Desktop, 176, Point.Zero);

        MGDockTabItem tab = GetTabItem(floatingWindow, harness.PanelC);
        OpenContextMenu(harness, tab);
        MGContextMenuButton dockButton = FindMenuButton(harness.Desktop.ActiveContextMenu, "Dock");
        Assert.NotNull(dockButton);

        dockButton.Action.Invoke(dockButton);

        Assert.Empty(harness.Host.FloatingWindows);

        MGDockTabGroup firstVisible = Assert.Single(harness.Host.GetAllVisibleTabGroups());
        Assert.Same(newGroup, firstVisible.GroupNode);
        Assert.Contains(harness.PanelC, newGroup.Panels);
    }

    // ── multi-tab group: order/active tab after both panels return (P11) ─────

    [Fact]
    public void Dock_MultiTabGroup_BothPanelsFloatedThenDockedInOrder_FollowsP11()
    {
        Harness harness = CreateHarness();

        MGFloatingDockWindow floatA = FloatPanel(harness, harness.PanelA, new Point(700, 500));
        MGFloatingDockWindow floatX = FloatPanel(harness, harness.PanelX, new Point(200, 150));

        // GroupAX is now empty - a hidden placeholder referenced by both A's and X's placements.
        Assert.True(harness.GroupAX.IsEmpty);
        Assert.True(harness.GroupAX.IsHiddenInLayout);
        var parentSplit = Assert.IsType<DockSplitNode>(harness.GroupAX.Parent);
        var ratioBeforeDock = parentSplit.SplitRatio;

        // Dock A first.
        MGDockTabItem tabA = GetTabItem(floatA, harness.PanelA);
        OpenContextMenu(harness, tabA);
        MGContextMenuButton dockA = FindMenuButton(harness.Desktop.ActiveContextMenu, "Dock");
        Assert.NotNull(dockA);
        dockA.Action.Invoke(dockA);

        // Then X.
        MGDockTabItem tabX = GetTabItem(floatX, harness.PanelX);
        OpenContextMenu(harness, tabX);
        MGContextMenuButton dockX = FindMenuButton(harness.Desktop.ActiveContextMenu, "Dock");
        Assert.NotNull(dockX);
        dockX.Action.Invoke(dockX);

        Assert.Empty(harness.Host.FloatingWindows);

        // Same node, same parent split, same ratio.
        Assert.Same(parentSplit, harness.GroupAX.Parent);
        Assert.Equal(ratioBeforeDock, parentSplit.SplitRatio);

        // P11: X was docked last (at its remembered index 0, ahead of A), so tabs are [X, A], active X.
        Assert.Equal(new[] { harness.PanelX, harness.PanelA }, harness.GroupAX.Panels);
        Assert.Equal(harness.PanelX.Id, harness.GroupAX.ActivePanelId);
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

    // ── (f): saved layout format (2.0) persists the floating group and its placement ─

    [Fact]
    public void LayoutSerialization_PersistsTheFloatingGroupAndItsPlacement_WhileAPanelIsFloating()
    {
        Harness harness = CreateHarness();
        FloatPanel(harness, harness.PanelA);

        string json = DockLayoutSerializer.ToJson(harness.Host.LayoutModel);

        DockLayoutModel reloaded = DockLayoutSerializer.FromJson(json);

        // A is floating (not docked): its floating group is persisted, and so is its remembered
        // placement (GroupAX, at its original tab index) so a later "Dock" can send it back.
        var reloadedFloatingGroup = reloaded.FindFloatingGroupOf(harness.PanelA.Id);
        Assert.NotNull(reloadedFloatingGroup);
        Assert.Contains(harness.PanelA.Id, reloadedFloatingGroup.Group.Panels.Select(p => p.Id));
        Assert.True(reloaded.TryGetPlacement(harness.PanelA.Id, out var placement));
        Assert.Equal(harness.GroupAX.Id, placement.GroupId);

        // The docked panels (X, B, C) are still there, in the tree.
        var reloadedDockedIds = reloaded.GetAllPanels().Select(p => p.Id).ToList();
        Assert.DoesNotContain(harness.PanelA.Id, reloadedDockedIds);
        Assert.Contains(harness.PanelX.Id, reloadedDockedIds);
        Assert.Contains(harness.PanelB.Id, reloadedDockedIds);
        Assert.Contains(harness.PanelC.Id, reloadedDockedIds);
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
