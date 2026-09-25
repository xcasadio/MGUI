using System;
using System.ComponentModel;
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
/// Bound-screens program, phase 8, T4.5 (D17): <see cref="MGDockHost.PanelClosing"/>, raised before a USER close
/// action removes a panel, so a host subscriber (the editor's "unsaved changes?" prompt) can veto it. Exercised
/// against the real <see cref="MGDockHost"/> and its real visuals (headless, <see cref="GraphTestRuntime"/>), never
/// by simulating the host's own close logic.
/// </summary>
public class PanelClosingVetoTests
{
    private sealed class Harness
    {
        public GraphTestRuntime Runtime;
        public MGDesktop Desktop;
        public MGWindow MainWindow;
        public MGDockHost Host;

        private int _elapsedMs;

        public void Frame(Point mousePosition = default, MouseButton? pressedButton = null)
        {
            Runtime.ApplyFrame(new UpdateBaseArgs(
                TimeSpan.FromMilliseconds(_elapsedMs),
                TimeSpan.FromMilliseconds(16),
                CreateMouseState(mousePosition, pressedButton),
                new KeyboardState()));
            Desktop.Update();
            _elapsedMs += 16;
        }
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

    private static DockPanelNode Panel(MGWindow window, string title = "P")
        => new() { Title = title, CanClose = true, CanAutoHide = true, ContentFactory = () => new MGBorder(window) };

    private static Harness CreateHarness(Func<MGWindow, DockNode> buildRoot)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None };

        MGDockHost host = new(mainWindow)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LayoutModel = new DockLayoutModel(buildRoot(mainWindow)),
        };
        mainWindow.SetContent(host);
        desktop.Windows.Add(mainWindow);

        // Registering every panel with the host's registry is the caller's job after assigning
        // LayoutModel directly (MGDockHost.ApplyLoadedLayoutModel's XML doc); without it, FindPanel,
        // RemovePanel and UnpinPanel would not see these panels.
        foreach (var panel in host.LayoutModel.GetAllPanels())
        {
            host.RegisterPanel(panel);
        }

        Harness harness = new() { Runtime = runtime, Desktop = desktop, MainWindow = mainWindow, Host = host };
        Point farAway = new(1400, 1000);
        harness.Frame(farAway);
        harness.Frame(farAway);
        return harness;
    }

    private static MGDockTabItem GetTabItem(MGDockTabGroup group, DockPanelNode panel)
        => group.TraverseVisualTree<MGDockTabItem>(IncludeSelf: false).Single(t => t.Panel == panel);

    /// <summary>Full user click path: press, press (still down), release — exactly the pattern
    /// <c>DockTabItemVisualsTests</c> uses to raise a real <c>CloseRequested</c> from the docked tab strip.</summary>
    private static void ClickCloseButton(Harness harness, MGDockTabItem tab)
    {
        MGElement closeButton = tab.TemplateParts[MGDockTabItem.CloseButtonPartName];
        Point onCloseButton = closeButton.LayoutBounds.Center;
        harness.Frame(onCloseButton, MouseButton.Left);
        harness.Frame(onCloseButton, MouseButton.Left);
        harness.Frame(onCloseButton);
    }

    private static void OpenContextMenu(Harness harness, MGDockTabItem tab)
    {
        Point onTab = tab.LayoutBounds.Center;
        harness.Frame(onTab, MouseButton.Right);
        harness.Frame(onTab, MouseButton.Right);
        harness.Frame(onTab);
    }

    private static MGContextMenuButton FindMenuButton(MGContextMenu menu, string text)
        => menu.Items.OfType<MGContextMenuButton>()
            .FirstOrDefault(b => (b.MenuItemContent as MGTextBlock)?.Text == text);

    // ── (a) docked tab close: cancelled then accepted ────────────────────────

    [Fact]
    public void TabClose_Cancelled_KeepsThePanel_ThenAccepted_ClosesIt()
    {
        DockPanelNode panel = null;
        Harness h = CreateHarness(window =>
        {
            panel = Panel(window, "A");
            DockTabGroupNode g = new();
            g.AddPanel(panel, -1);
            return g;
        });

        MGDockTabGroup groupControl = h.Host.TraverseVisualTree<MGDockTabGroup>(IncludeSelf: false).Single();
        MGDockTabItem tab = GetTabItem(groupControl, panel);

        bool cancelNext = true;
        int raisedCount = 0;
        h.Host.PanelClosing += (_, args) =>
        {
            raisedCount++;
            args.Cancel = cancelNext;
        };

        ClickCloseButton(h, tab);
        Assert.Equal(1, raisedCount);
        Assert.NotNull(h.Host.FindPanel(panel.Id)); // still there, cancelled

        cancelNext = false;
        ClickCloseButton(h, tab);
        Assert.Equal(2, raisedCount);
        Assert.Null(h.Host.FindPanel(panel.Id)); // now closed
    }

    // ── (a) Close Others: one refused panel stays, the others go ────────────

    [Fact]
    public void CloseOthers_OneRefused_ThatPanelStays_OthersClose()
    {
        DockPanelNode kept = null, refused = null, closes = null;
        Harness h = CreateHarness(window =>
        {
            kept = Panel(window, "Kept");
            refused = Panel(window, "Refused");
            closes = Panel(window, "Closes");
            DockTabGroupNode g = new();
            g.AddPanel(kept, -1);
            g.AddPanel(refused, -1);
            g.AddPanel(closes, -1);
            return g;
        });

        MGDockTabGroup groupControl = h.Host.TraverseVisualTree<MGDockTabGroup>(IncludeSelf: false).Single();
        MGDockTabItem keptTab = GetTabItem(groupControl, kept);

        h.Host.PanelClosing += (_, args) => args.Cancel = args.Data == refused;

        OpenContextMenu(h, keptTab);
        MGContextMenuButton closeOthers = FindMenuButton(h.Desktop.ActiveContextMenu, "Close Others");
        Assert.NotNull(closeOthers);
        closeOthers.Action.Invoke(closeOthers);

        Assert.NotNull(h.Host.FindPanel(kept.Id));
        Assert.NotNull(h.Host.FindPanel(refused.Id)); // refused close: stays
        Assert.Null(h.Host.FindPanel(closes.Id));      // not refused: closed
    }

    // ── (a) Close All: one refused ───────────────────────────────────────────

    [Fact]
    public void CloseAll_OneRefused_ThatPanelStays_OthersClose()
    {
        DockPanelNode refused = null, a = null, b = null;
        Harness h = CreateHarness(window =>
        {
            refused = Panel(window, "Refused");
            a = Panel(window, "A");
            b = Panel(window, "B");
            DockTabGroupNode g = new();
            g.AddPanel(refused, -1);
            g.AddPanel(a, -1);
            g.AddPanel(b, -1);
            return g;
        });

        MGDockTabGroup groupControl = h.Host.TraverseVisualTree<MGDockTabGroup>(IncludeSelf: false).Single();
        MGDockTabItem anyTab = GetTabItem(groupControl, a);

        h.Host.PanelClosing += (_, args) => args.Cancel = args.Data == refused;

        OpenContextMenu(h, anyTab);
        MGContextMenuButton closeAll = FindMenuButton(h.Desktop.ActiveContextMenu, "Close All");
        Assert.NotNull(closeAll);
        closeAll.Action.Invoke(closeAll);

        Assert.NotNull(h.Host.FindPanel(refused.Id)); // refused close: stays
        Assert.Null(h.Host.FindPanel(a.Id));
        Assert.Null(h.Host.FindPanel(b.Id));
    }

    // ── (b) floating window tab close: model-backed ──────────────────────────

    [Fact]
    public void FloatingWindow_ModelBacked_TabClose_Refused_KeepsThePanel()
    {
        DockPanelNode floated = null, other = null;
        Harness h = CreateHarness(window =>
        {
            floated = Panel(window, "Floated");
            other = Panel(window, "Other");
            DockTabGroupNode g = new();
            g.AddPanel(floated, -1);
            g.AddPanel(other, -1);
            return g;
        });

        MGFloatingDockWindow floatingWindow = h.Host.DetachToFloating(floated, new Point(600, 400));
        h.Frame();
        h.Frame();

        MGDockTabItem tab = floatingWindow.TabGroup.TraverseVisualTree<MGDockTabItem>(IncludeSelf: false)
            .Single(t => t.Panel == floated);

        bool raised = false;
        h.Host.PanelClosing += (_, args) =>
        {
            raised = true;
            args.Cancel = true;
        };

        ClickCloseButton(h, tab);

        Assert.True(raised);
        Assert.Contains(floatingWindow, h.Host.FloatingWindows);
        Assert.Contains(floated, floatingWindow.GroupNode.Panels);
    }

    // ── (b) floating window tab close: standalone (non model-backed) ────────

    [Fact]
    public void FloatingWindow_Standalone_TabClose_Refused_KeepsThePanel()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None };
        MGDockHost host = new(mainWindow);
        desktop.Windows.Add(mainWindow);

        DockPanelNode panel = Panel(mainWindow, "Standalone");
        MGFloatingDockWindow floatingWindow = new(host, panel, 0, 0, 200, 150);
        mainWindow.AddNestedWindow(floatingWindow);

        Harness h = new() { Runtime = runtime, Desktop = desktop, MainWindow = mainWindow, Host = host };
        h.Frame();
        h.Frame();

        MGDockTabItem tab = floatingWindow.TabGroup.TraverseVisualTree<MGDockTabItem>(IncludeSelf: false)
            .Single(t => t.Panel == panel);

        bool raised = false;
        host.PanelClosing += (_, args) =>
        {
            raised = true;
            args.Cancel = true;
        };

        ClickCloseButton(h, tab);

        Assert.True(raised);
        Assert.Contains(panel, floatingWindow.GroupNode.Panels);
        Assert.Contains(floatingWindow, mainWindow.NestedWindows);
    }

    // ── (d) whole floating window close: second panel refuses ───────────────

    [Fact]
    public void WholeFloatingWindowClose_SecondPanelRefuses_WindowAndAllItsPanelsStay()
    {
        DockPanelNode first = null, second = null, dockedOther = null;
        Harness h = CreateHarness(window =>
        {
            first = Panel(window, "First");
            second = Panel(window, "Second");
            dockedOther = Panel(window, "DockedOther");
            DockTabGroupNode floatable = new();
            floatable.AddPanel(first, -1);
            DockTabGroupNode dockedGroup = new();
            dockedGroup.AddPanel(dockedOther, -1);
            dockedGroup.AddPanel(second, -1);
            return new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                SplitRatio = 0.5f,
                FirstChild = floatable,
                SecondChild = dockedGroup,
            };
        });

        MGFloatingDockWindow floatingWindow = h.Host.DetachToFloating(first, new Point(600, 400));
        h.Frame();
        h.Frame();

        // Give the floating window a second panel directly at the model level (equivalent to
        // dragging "second" onto the floating window's own tab strip): closing the WHOLE window
        // must now ask about both panels, in order.
        DockTabGroupNode dockedGroupNode = h.Host.GetAllTabGroups().Single(g => g.Panels.Contains(second));
        dockedGroupNode.RemovePanel(second);
        floatingWindow.GroupNode.AddPanel(second, -1);
        h.Frame();
        h.Frame();

        var asked = new System.Collections.Generic.List<DockPanelNode>();
        h.Host.PanelClosing += (_, args) =>
        {
            asked.Add(args.Data);
            args.Cancel = args.Data == second; // first is fine, second refuses
        };

        bool closed = floatingWindow.TryCloseWindow();

        Assert.False(closed);
        Assert.Equal(new[] { first, second }, asked); // asked in order, both because "first" alone did not cancel
        Assert.Contains(floatingWindow, h.Host.FloatingWindows);
        Assert.Contains(floatingWindow, h.MainWindow.NestedWindows);
        Assert.Contains(first, floatingWindow.GroupNode.Panels);
        Assert.Contains(second, floatingWindow.GroupNode.Panels);
    }

    [Fact]
    public void WholeFloatingWindowClose_NoRefusal_ClosesTheWindowAndAllItsPanels()
    {
        DockPanelNode panel = null;
        Harness h = CreateHarness(window =>
        {
            panel = Panel(window, "Solo");
            DockTabGroupNode g = new();
            g.AddPanel(panel, -1);
            DockTabGroupNode other = new();
            other.AddPanel(Panel(window, "Other"), -1);
            return new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                SplitRatio = 0.5f,
                FirstChild = g,
                SecondChild = other,
            };
        });

        MGFloatingDockWindow floatingWindow = h.Host.DetachToFloating(panel, new Point(600, 400));
        h.Frame();
        h.Frame();

        h.Host.PanelClosing += (_, args) => args.Cancel = false;

        bool closed = floatingWindow.TryCloseWindow();

        Assert.True(closed);
        Assert.DoesNotContain(floatingWindow, h.Host.FloatingWindows);
        Assert.Null(h.Host.FindPanel(panel.Id));
    }

    // ── (c) auto-hide drawer close refused ───────────────────────────────────

    [Fact]
    public void AutoHideDrawerClose_Refused_KeepsThePanelAutoHidden()
    {
        DockPanelNode panel = null;
        Harness h = CreateHarness(window =>
        {
            panel = Panel(window, "AutoHidden");
            DockTabGroupNode g = new();
            g.AddPanel(panel, -1);
            DockTabGroupNode other = new();
            other.AddPanel(Panel(window, "Other"), -1);
            return new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                SplitRatio = 0.5f,
                FirstChild = g,
                SecondChild = other,
            };
        });

        h.Host.UnpinPanel(panel);
        h.Frame();
        h.Host.ShowAutoHideDrawer(panel);
        h.Frame();
        h.Frame();

        MGDockAutoHideDrawer drawer = (MGDockAutoHideDrawer)h.Host.TemplateParts[MGDockHost.AutoHideDrawerPartName];
        MGElement closeButton = drawer.TemplateParts[MGDockAutoHideDrawer.CloseButtonPartName];

        bool raised = false;
        h.Host.PanelClosing += (_, args) =>
        {
            raised = true;
            args.Cancel = true;
        };

        Point onCloseButton = closeButton.LayoutBounds.Center;
        h.Frame(onCloseButton, MouseButton.Left);
        h.Frame(onCloseButton, MouseButton.Left);
        h.Frame(onCloseButton);

        Assert.True(raised);
        Assert.NotNull(h.Host.FindPanel(panel.Id));
        Assert.True(h.Host.LayoutModel.HasAutoHidePanels(panel.AutoHideSide));
    }

    // ── programmatic removal: never raised ───────────────────────────────────

    [Fact]
    public void RemovePanel_DoesNotRaisePanelClosing()
    {
        DockPanelNode panel = null;
        Harness h = CreateHarness(window =>
        {
            panel = Panel(window, "Programmatic");
            DockTabGroupNode g = new();
            g.AddPanel(panel, -1);
            return g;
        });

        bool raised = false;
        h.Host.PanelClosing += (_, _) => raised = true;

        bool removed = h.Host.RemovePanel(panel.Id);

        Assert.True(removed);
        Assert.False(raised);
        Assert.Null(h.Host.FindPanel(panel.Id));
    }

    // ── no subscriber: behaviour unchanged ───────────────────────────────────

    [Fact]
    public void TabClose_WithoutAnySubscriber_ClosesThePanel_AsBefore()
    {
        DockPanelNode panel = null;
        Harness h = CreateHarness(window =>
        {
            panel = Panel(window, "Unwatched");
            DockTabGroupNode g = new();
            g.AddPanel(panel, -1);
            return g;
        });

        MGDockTabGroup groupControl = h.Host.TraverseVisualTree<MGDockTabGroup>(IncludeSelf: false).Single();
        MGDockTabItem tab = GetTabItem(groupControl, panel);

        ClickCloseButton(h, tab);

        Assert.Null(h.Host.FindPanel(panel.Id));
    }

    // ── ADR-0018: the closing cause (DockPanelClosingEventArgs.ClosingFloatingWindow) ──

    /// <summary>Subscribes a recorder that keeps every argument the host raises <see cref="MGDockHost.PanelClosing"/>
    /// with, refusing the close of every panel in <paramref name="refused"/>.</summary>
    private static System.Collections.Generic.List<CancelEventArgs<DockPanelNode>> RecordClosingArgs(
        MGDockHost host, System.Collections.Generic.ISet<DockPanelNode> refused = null)
    {
        var recorded = new System.Collections.Generic.List<CancelEventArgs<DockPanelNode>>();
        host.PanelClosing += (_, args) =>
        {
            recorded.Add(args);
            args.Cancel = refused?.Contains(args.Data) == true;
        };
        return recorded;
    }

    private static void AssertNoClosingFloatingWindow(System.Collections.Generic.IReadOnlyList<CancelEventArgs<DockPanelNode>> recorded)
    {
        Assert.NotEmpty(recorded);
        foreach (var args in recorded)
        {
            DockPanelClosingEventArgs closingArgs = Assert.IsType<DockPanelClosingEventArgs>(args);
            Assert.Null(closingArgs.ClosingFloatingWindow);
        }
    }

    [Fact]
    public void ClosingCause_TabCloseButton_HasNoClosingFloatingWindow()
    {
        DockPanelNode panel = null;
        Harness h = CreateHarness(window =>
        {
            panel = Panel(window, "A");
            DockTabGroupNode g = new();
            g.AddPanel(panel, -1);
            return g;
        });
        MGDockTabGroup groupControl = h.Host.TraverseVisualTree<MGDockTabGroup>(IncludeSelf: false).Single();
        var recorded = RecordClosingArgs(h.Host);

        ClickCloseButton(h, GetTabItem(groupControl, panel));

        AssertNoClosingFloatingWindow(recorded);
    }

    [Theory]
    [InlineData("Close Others")]
    [InlineData("Close All")]
    public void ClosingCause_CloseOthersAndCloseAll_HaveNoClosingFloatingWindow(string menuText)
    {
        DockPanelNode a = null, b = null, c = null;
        Harness h = CreateHarness(window =>
        {
            a = Panel(window, "A");
            b = Panel(window, "B");
            c = Panel(window, "C");
            DockTabGroupNode g = new();
            g.AddPanel(a, -1);
            g.AddPanel(b, -1);
            g.AddPanel(c, -1);
            return g;
        });
        MGDockTabGroup groupControl = h.Host.TraverseVisualTree<MGDockTabGroup>(IncludeSelf: false).Single();
        var recorded = RecordClosingArgs(h.Host);

        OpenContextMenu(h, GetTabItem(groupControl, a));
        MGContextMenuButton button = FindMenuButton(h.Desktop.ActiveContextMenu, menuText);
        Assert.NotNull(button);
        button.Action.Invoke(button);

        AssertNoClosingFloatingWindow(recorded);
    }

    [Fact]
    public void ClosingCause_FloatingWindowTabClose_HasNoClosingFloatingWindow()
    {
        DockPanelNode floated = null;
        Harness h = CreateHarness(window =>
        {
            floated = Panel(window, "Floated");
            DockTabGroupNode g = new();
            g.AddPanel(floated, -1);
            g.AddPanel(Panel(window, "Other"), -1);
            return g;
        });
        MGFloatingDockWindow floatingWindow = h.Host.DetachToFloating(floated, new Point(600, 400));
        h.Frame();
        h.Frame();
        MGDockTabItem tab = floatingWindow.TabGroup.TraverseVisualTree<MGDockTabItem>(IncludeSelf: false)
            .Single(t => t.Panel == floated);
        var recorded = RecordClosingArgs(h.Host, new System.Collections.Generic.HashSet<DockPanelNode> { floated });

        ClickCloseButton(h, tab);

        AssertNoClosingFloatingWindow(recorded);
    }

    [Fact]
    public void ClosingCause_StandaloneFloatingWindowTabClose_HasNoClosingFloatingWindow()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None };
        MGDockHost host = new(mainWindow);
        desktop.Windows.Add(mainWindow);
        DockPanelNode panel = Panel(mainWindow, "Standalone");
        MGFloatingDockWindow floatingWindow = new(host, panel, 0, 0, 200, 150);
        mainWindow.AddNestedWindow(floatingWindow);
        Harness h = new() { Runtime = runtime, Desktop = desktop, MainWindow = mainWindow, Host = host };
        h.Frame();
        h.Frame();
        MGDockTabItem tab = floatingWindow.TabGroup.TraverseVisualTree<MGDockTabItem>(IncludeSelf: false)
            .Single(t => t.Panel == panel);
        var recorded = RecordClosingArgs(host, new System.Collections.Generic.HashSet<DockPanelNode> { panel });

        ClickCloseButton(h, tab);

        AssertNoClosingFloatingWindow(recorded);
    }

    [Fact]
    public void ClosingCause_AutoHideDrawerClose_HasNoClosingFloatingWindow()
    {
        DockPanelNode panel = null;
        Harness h = CreateHarness(window =>
        {
            panel = Panel(window, "AutoHidden");
            DockTabGroupNode g = new();
            g.AddPanel(panel, -1);
            DockTabGroupNode other = new();
            other.AddPanel(Panel(window, "Other"), -1);
            return new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                SplitRatio = 0.5f,
                FirstChild = g,
                SecondChild = other,
            };
        });
        h.Host.UnpinPanel(panel);
        h.Frame();
        h.Host.ShowAutoHideDrawer(panel);
        h.Frame();
        h.Frame();
        MGDockAutoHideDrawer drawer = (MGDockAutoHideDrawer)h.Host.TemplateParts[MGDockHost.AutoHideDrawerPartName];
        MGElement closeButton = drawer.TemplateParts[MGDockAutoHideDrawer.CloseButtonPartName];
        var recorded = RecordClosingArgs(h.Host, new System.Collections.Generic.HashSet<DockPanelNode> { panel });

        Point onCloseButton = closeButton.LayoutBounds.Center;
        h.Frame(onCloseButton, MouseButton.Left);
        h.Frame(onCloseButton, MouseButton.Left);
        h.Frame(onCloseButton);

        AssertNoClosingFloatingWindow(recorded);
    }

    /// <summary>Builds a harness whose floating window holds <c>first</c> then <c>second</c>, as
    /// <see cref="WholeFloatingWindowClose_SecondPanelRefuses_WindowAndAllItsPanelsStay"/> does.</summary>
    private static Harness CreateFloatingWindowWithTwoPanels(out MGFloatingDockWindow floatingWindow, out DockPanelNode first, out DockPanelNode second)
    {
        DockPanelNode firstPanel = null, secondPanel = null;
        Harness h = CreateHarness(window =>
        {
            firstPanel = Panel(window, "First");
            secondPanel = Panel(window, "Second");
            DockTabGroupNode floatable = new();
            floatable.AddPanel(firstPanel, -1);
            DockTabGroupNode dockedGroup = new();
            dockedGroup.AddPanel(Panel(window, "DockedOther"), -1);
            dockedGroup.AddPanel(secondPanel, -1);
            return new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                SplitRatio = 0.5f,
                FirstChild = floatable,
                SecondChild = dockedGroup,
            };
        });

        floatingWindow = h.Host.DetachToFloating(firstPanel, new Point(600, 400));
        h.Frame();
        h.Frame();
        DockTabGroupNode dockedGroupNode = h.Host.GetAllTabGroups().Single(g => g.Panels.Contains(secondPanel));
        dockedGroupNode.RemovePanel(secondPanel);
        floatingWindow.GroupNode.AddPanel(secondPanel, -1);
        h.Frame();
        h.Frame();

        first = firstPanel;
        second = secondPanel;
        return h;
    }

    [Fact]
    public void ClosingCause_WholeFloatingWindowClose_NamesTheWindow_ForEveryPanelAsked()
    {
        Harness h = CreateFloatingWindowWithTwoPanels(out MGFloatingDockWindow floatingWindow, out DockPanelNode first, out DockPanelNode second);
        var recorded = RecordClosingArgs(h.Host, new System.Collections.Generic.HashSet<DockPanelNode> { second });

        bool closed = floatingWindow.TryCloseWindow();

        Assert.False(closed);
        Assert.Equal(new[] { first, second }, recorded.Select(args => args.Data));
        foreach (var args in recorded)
        {
            DockPanelClosingEventArgs closingArgs = Assert.IsType<DockPanelClosingEventArgs>(args);
            Assert.Same(floatingWindow, closingArgs.ClosingFloatingWindow);
        }
    }

    /// <summary>
    /// The chain an asynchronous subscriber runs (CasaEngine's editor, ADR-0039): it refuses every modified panel, asks its
    /// question, then on an answer that lets the panel close closes it with <see cref="MGDockHost.ClosePanel"/> and retries
    /// the whole-window close through <see cref="DockPanelClosingEventArgs.ClosingFloatingWindow"/>, which asks the next
    /// panel. Also pins that closing the last panel of a floating window by code closes the window.
    /// </summary>
    [Fact]
    public void ClosingCause_RetryingTheWindowClose_AsksTheRemainingPanels_UntilTheWindowCloses()
    {
        Harness h = CreateFloatingWindowWithTwoPanels(out MGFloatingDockWindow floatingWindow, out DockPanelNode first, out DockPanelNode second);
        var modified = new System.Collections.Generic.HashSet<DockPanelNode> { first, second };
        var recorded = RecordClosingArgs(h.Host, modified);
        var removed = new System.Collections.Generic.List<DockPanelNode>();
        h.Host.PanelRemoved += (_, removedPanel) => removed.Add(removedPanel);

        //  1st close: "first" is modified, refuses; the window stays with both panels.
        Assert.False(floatingWindow.TryCloseWindow());
        Assert.Equal(new[] { first }, recorded.Select(args => args.Data));
        MGFloatingDockWindow toRetry = ((DockPanelClosingEventArgs)recorded[^1]).ClosingFloatingWindow;
        Assert.Same(floatingWindow, toRetry);

        //  Answer for "first" lets it close: close it by code, then retry the window close - "second" is asked and refuses.
        modified.Remove(first);
        Assert.True(h.Host.ClosePanel(first));
        h.Frame();
        Assert.Contains(floatingWindow, h.Host.FloatingWindows);
        Assert.False(toRetry.TryCloseWindow());
        Assert.Equal(new[] { first, second }, recorded.Select(args => args.Data));
        Assert.Same(floatingWindow, ((DockPanelClosingEventArgs)recorded[^1]).ClosingFloatingWindow);

        //  Answer for "second" lets it close: closing the window's last panel by code closes the window itself.
        modified.Remove(second);
        Assert.True(h.Host.ClosePanel(second));
        h.Frame();

        Assert.DoesNotContain(floatingWindow, h.Host.FloatingWindows);
        Assert.DoesNotContain(floatingWindow, h.MainWindow.NestedWindows);
        Assert.Equal(new[] { first, second }, removed);
        Assert.Equal(2, recorded.Count); // ClosePanel never raises PanelClosing
    }

    // ── ADR-0018: MGDockHost.ClosePanel, the programmatic close an asynchronous subscriber uses ──

    /// <summary>The reason <see cref="MGDockHost.ClosePanel"/> exists: a panel detached to a floating window leaves the
    /// host's registry, so <see cref="MGDockHost.RemovePanel"/> cannot close it.</summary>
    [Fact]
    public void RemovePanel_DoesNotSeeAFloatingPanel()
    {
        DockPanelNode floated = null;
        Harness h = CreateHarness(window =>
        {
            floated = Panel(window, "Floated");
            DockTabGroupNode g = new();
            g.AddPanel(floated, -1);
            g.AddPanel(Panel(window, "Other"), -1);
            return g;
        });
        MGFloatingDockWindow floatingWindow = h.Host.DetachToFloating(floated, new Point(600, 400));
        h.Frame();

        Assert.False(h.Host.RemovePanel(floated.Id));
        Assert.Contains(floated, floatingWindow.GroupNode.Panels);
    }

    [Fact]
    public void ClosePanel_Docked_ClosesIt_RaisingPanelRemovedButNotPanelClosing()
    {
        DockPanelNode panel = null, other = null;
        Harness h = CreateHarness(window =>
        {
            panel = Panel(window, "A");
            other = Panel(window, "B");
            DockTabGroupNode g = new();
            g.AddPanel(panel, -1);
            g.AddPanel(other, -1);
            return g;
        });
        int closingCount = 0;
        var removed = new System.Collections.Generic.List<DockPanelNode>();
        h.Host.PanelClosing += (_, _) => closingCount++;
        h.Host.PanelRemoved += (_, removedPanel) => removed.Add(removedPanel);

        bool closed = h.Host.ClosePanel(panel);
        h.Frame();

        Assert.True(closed);
        Assert.Null(h.Host.FindPanel(panel.Id));
        Assert.NotNull(h.Host.FindPanel(other.Id));
        Assert.Equal(new[] { panel }, removed);
        Assert.Equal(0, closingCount);
    }

    [Fact]
    public void ClosePanel_AutoHidden_ClosesIt()
    {
        DockPanelNode panel = null;
        Harness h = CreateHarness(window =>
        {
            panel = Panel(window, "AutoHidden");
            DockTabGroupNode g = new();
            g.AddPanel(panel, -1);
            DockTabGroupNode other = new();
            other.AddPanel(Panel(window, "Other"), -1);
            return new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                SplitRatio = 0.5f,
                FirstChild = g,
                SecondChild = other,
            };
        });
        h.Host.UnpinPanel(panel);
        h.Frame();
        int closingCount = 0;
        h.Host.PanelClosing += (_, _) => closingCount++;

        bool closed = h.Host.ClosePanel(panel);
        h.Frame();

        Assert.True(closed);
        Assert.Null(h.Host.FindPanel(panel.Id));
        Assert.False(h.Host.LayoutModel.HasAutoHidePanels(panel.AutoHideSide));
        Assert.Equal(0, closingCount);
    }

    [Fact]
    public void ClosePanel_InAFloatingWindowWithOtherPanels_ClosesOnlyThatPanel_AndKeepsTheWindow()
    {
        Harness h = CreateFloatingWindowWithTwoPanels(out MGFloatingDockWindow floatingWindow, out DockPanelNode first, out DockPanelNode second);
        int closingCount = 0;
        var removed = new System.Collections.Generic.List<DockPanelNode>();
        h.Host.PanelClosing += (_, _) => closingCount++;
        h.Host.PanelRemoved += (_, removedPanel) => removed.Add(removedPanel);

        bool closed = h.Host.ClosePanel(first);
        h.Frame();

        Assert.True(closed);
        Assert.Contains(floatingWindow, h.Host.FloatingWindows);
        Assert.DoesNotContain(first, floatingWindow.GroupNode.Panels);
        Assert.Contains(second, floatingWindow.GroupNode.Panels);
        Assert.Equal(new[] { first }, removed);
        Assert.Equal(0, closingCount);
    }

    [Fact]
    public void ClosePanel_ReturnsFalse_ForAPanelTheHostDoesNotHold()
    {
        DockPanelNode held = null;
        Harness h = CreateHarness(window =>
        {
            held = Panel(window, "Held");
            DockTabGroupNode g = new();
            g.AddPanel(held, -1);
            return g;
        });
        DockPanelNode stranger = Panel(h.MainWindow, "Stranger");

        Assert.False(h.Host.ClosePanel(stranger));
        Assert.False(h.Host.ClosePanel(null));
        Assert.NotNull(h.Host.FindPanel(held.Id));
    }
}
