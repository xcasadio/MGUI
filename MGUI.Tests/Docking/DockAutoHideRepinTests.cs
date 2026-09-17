using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Docking;

/// <summary>
/// Task T3 of <c>Docs/Tasks/docking-ghost-groups-tasks.md</c>: <see cref="MGDockHost.UnpinPanel"/>
/// and <see cref="MGDockHost.RepinPanel"/> go through the same remembered places as floating
/// (D4) — a panel that auto-hides leaves its emptied group as a hidden placeholder, and comes
/// back to that exact group, at its tab index, whatever the order several panels return in.
/// Exercised against the real <see cref="MGDockHost"/> (headless, <see cref="GraphTestRuntime"/>),
/// never by simulating the host's logic.
/// </summary>
public class DockAutoHideRepinTests
{
    private sealed class Harness
    {
        public GraphTestRuntime Runtime;
        public MGDesktop Desktop;
        public MGWindow MainWindow;
        public MGDockHost Host;

        private int _elapsedMs;

        public void Frame()
        {
            Runtime.ApplyFrame(new UpdateBaseArgs(
                TimeSpan.FromMilliseconds(_elapsedMs),
                TimeSpan.FromMilliseconds(16),
                new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
                new KeyboardState()));
            Desktop.Update();
            _elapsedMs += 16;
        }
    }

    /// <summary>Builds a headless host whose layout tree comes from <paramref name="buildRoot"/>, which
    /// receives the host's own <see cref="MGWindow"/> so panels can wire a real <c>ContentFactory</c>
    /// from the start (needed before the first visual build).</summary>
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

        Harness harness = new() { Runtime = runtime, Desktop = desktop, MainWindow = mainWindow, Host = host };
        harness.Frame();
        harness.Frame();
        return harness;
    }

    private static DockPanelNode Panel(MGWindow window, string title = "P")
        => new() { Title = title, ContentFactory = () => new MGBorder(window) };

    private static DockTabGroupNode Group(params DockPanelNode[] panels)
    {
        DockTabGroupNode g = new();
        foreach (var p in panels)
        {
            g.AddPanel(p, -1);
        }

        return g;
    }

    private static bool AnySideAutoHidden(MGDockHost host)
        => host.LayoutModel.HasAutoHidePanels(AutoHideSide.Left)
            || host.LayoutModel.HasAutoHidePanels(AutoHideSide.Right)
            || host.LayoutModel.HasAutoHidePanels(AutoHideSide.Top)
            || host.LayoutModel.HasAutoHidePanels(AutoHideSide.Bottom);

    // ── returns to the same group, at its tab index ──────────────────────

    [Fact]
    public void UnpinThenRepin_ReturnsPanelToItsOriginalGroup_AndPins()
    {
        DockPanelNode p = null;
        DockPanelNode other = null;
        DockTabGroupNode g = null;
        Harness h = CreateHarness(window =>
        {
            p = Panel(window, "P");
            other = Panel(window, "Other");
            g = Group(p, other);
            DockTabGroupNode gOther = Group(Panel(window, "Elsewhere"));
            return new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                SplitRatio = 0.5f,
                FirstChild = g,
                SecondChild = gOther,
            };
        });
        DockSplitNode split = Assert.IsType<DockSplitNode>(h.Host.LayoutModel.RootNode);

        h.Host.UnpinPanel(p);
        h.Frame();

        Assert.True(h.Host.LayoutModel.HasAutoHidePanels(p.AutoHideSide));
        Assert.False(p.IsPinned);
        Assert.Same(g, split.FirstChild); // placeholder kept in place

        h.Host.RepinPanel(p);
        h.Frame();

        Assert.Contains(p, g.Panels);
        Assert.True(p.IsPinned);
        Assert.False(AnySideAutoHidden(h.Host));
    }

    [Fact]
    public void UnpinPanel_CalledTwice_IsIdempotent_DoesNotThrow()
    {
        DockPanelNode p = null;
        Harness h = CreateHarness(window =>
        {
            p = Panel(window, "P");
            return Group(p, Panel(window, "Other"));
        });

        h.Host.UnpinPanel(p);
        h.Frame();
        Assert.False(p.IsPinned);

        // A second call, e.g. from a duplicate dispatch of the same pin-toggle click, must be a
        // no-op rather than throw: DockOperation.AutoHidePanel requires the panel to still sit
        // in a tab group in the layout tree, which is no longer true here.
        var exception = Record.Exception(() => h.Host.UnpinPanel(p));
        h.Frame();

        Assert.Null(exception);
        Assert.False(p.IsPinned);
        Assert.True(h.Host.LayoutModel.HasAutoHidePanels(p.AutoHideSide));
        Assert.Single(h.Host.LayoutModel.GetAutoHidePanels(p.AutoHideSide));
    }

    [Fact]
    public void RepinPanel_CalledTwice_IsIdempotent_DoesNotThrow()
    {
        DockPanelNode p = null;
        DockTabGroupNode g = null;
        Harness h = CreateHarness(window =>
        {
            p = Panel(window, "P");
            g = Group(p, Panel(window, "Other"));
            return g;
        });

        h.Host.UnpinPanel(p);
        h.Frame();

        h.Host.RepinPanel(p);
        h.Frame();
        Assert.True(p.IsPinned);

        // A second call, e.g. from a duplicate dispatch of the same pin-toggle click, must be a
        // no-op rather than throw: DockOperation.RestoreToPlacement requires the panel to have
        // no parent, which is no longer true here.
        var exception = Record.Exception(() => h.Host.RepinPanel(p));
        h.Frame();

        Assert.Null(exception);
        Assert.True(p.IsPinned);
        Assert.Contains(p, g.Panels);
        Assert.Equal(1, g.Panels.Count(x => x.Id == p.Id));
    }

    [Fact]
    public void UnpinThenRepin_WhenGroupEmptiedInBetween_ReturnsToTheExactPlaceholderGroup()
    {
        // p is the ONLY panel in g; unpinning it leaves g as a hidden placeholder.
        DockPanelNode p = null;
        DockTabGroupNode g = null;
        Harness h = CreateHarness(window =>
        {
            p = Panel(window, "P");
            g = Group(p);
            DockTabGroupNode gOther = Group(Panel(window, "Other"));
            return new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                SplitRatio = 0.5f,
                FirstChild = g,
                SecondChild = gOther,
            };
        });
        DockSplitNode split = Assert.IsType<DockSplitNode>(h.Host.LayoutModel.RootNode);

        h.Host.UnpinPanel(p);
        h.Frame();

        Assert.True(g.IsHiddenInLayout);
        Assert.Same(g, split.FirstChild); // g survives as a placeholder, still the same node
        Assert.Empty(h.Host.GetAllVisibleTabGroups().Where(v => v.GroupNode == g));

        h.Host.RepinPanel(p);
        h.Frame();

        Assert.False(g.IsHiddenInLayout);
        Assert.Contains(p, g.Panels);
        Assert.Same(g, split.FirstChild); // same node, same split
    }

    [Fact]
    public void MultiTabGroup_OutOfOrderReturn_FollowsIndexRule()
    {
        // [A, B, C] active C, in a split — mirrors DockOperationTests' D3 group.
        DockPanelNode a = null;
        DockPanelNode b = null;
        DockPanelNode c = null;
        DockTabGroupNode g = null;
        Harness h = CreateHarness(window =>
        {
            a = Panel(window, "A");
            b = Panel(window, "B");
            c = Panel(window, "C");
            g = Group(a, b, c);
            g.SetActivePanel(c.Id);
            DockTabGroupNode gOther = Group(Panel(window, "Other"));
            return new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                SplitRatio = 0.5f,
                FirstChild = g,
                SecondChild = gOther,
            };
        });

        // Depart C, B, A (indices 2, 1, 0); return C, B, A.
        h.Host.UnpinPanel(c);
        h.Frame();
        h.Host.UnpinPanel(b);
        h.Frame();
        h.Host.UnpinPanel(a);
        h.Frame();

        h.Host.RepinPanel(c);
        h.Frame();
        h.Host.RepinPanel(b);
        h.Frame();
        h.Host.RepinPanel(a);
        h.Frame();

        Assert.Equal(new[] { a, c, b }, g.Panels);
        Assert.Equal(a.Id, g.ActivePanelId);
    }

    // ── empty layout still accepts a re-pin ──────────────────────────────

    [Fact]
    public void UnpinThenRepin_EmptyLayout_RepinsIntoTheSurvivingEmptyRoot()
    {
        DockPanelNode p = null;
        DockTabGroupNode g = null;
        Harness h = CreateHarness(window =>
        {
            p = Panel(window, "P");
            g = Group(p);
            return g;
        });

        h.Host.UnpinPanel(p);
        h.Frame();

        var emptyRoot = Assert.IsType<DockTabGroupNode>(h.Host.LayoutModel.RootNode);
        Assert.True(emptyRoot.IsEmpty);
        Assert.Same(g, emptyRoot);

        h.Host.RepinPanel(p);
        h.Frame();

        Assert.Contains(p, h.Host.LayoutModel.GetAllTabGroups().SelectMany(gr => gr.Panels));
    }

    // ── fallback: the place is gone because the application replaced the root ────

    [Fact]
    public void Repin_WhenPlacementGroupGone_FallsBackToRootEdgeMatchingAutoHideSide()
    {
        DockPanelNode p = null;
        Harness h = CreateHarness(window =>
        {
            p = Panel(window, "P");
            return Group(p);
        });

        h.Host.UnpinPanel(p);
        h.Frame();
        AutoHideSide side = p.AutoHideSide;

        // The application replaces the root entirely while p is auto-hidden.
        DockTabGroupNode freshRoot = Group(Panel(h.MainWindow, "Fresh"));
        h.Host.LayoutModel.RootNode = freshRoot;
        h.Frame();

        h.Host.RepinPanel(p);
        h.Frame();

        Assert.True(p.IsPinned);
        Assert.False(h.Host.LayoutModel.TryGetPlacement(p.Id, out _));
        DockSplitNode newRootSplit = Assert.IsType<DockSplitNode>(h.Host.LayoutModel.RootNode);
        DockNode edgeChild = side switch
        {
            AutoHideSide.Left or AutoHideSide.Top => newRootSplit.FirstChild,
            _ => newRootSplit.SecondChild,
        };
        DockTabGroupNode edgeGroup = Assert.IsType<DockTabGroupNode>(edgeChild);
        Assert.Contains(p, edgeGroup.Panels);
    }

    // ── closing an auto-hidden panel ──────────────────────────────────────

    [Fact]
    public void CloseAutoHidePanel_RemovesFromStore_RaisesPanelRemovedOnce_AndNotifiesRegistry()
    {
        DockPanelNode p = null;
        DockTabGroupNode g = null;
        DockTabGroupNode gOther = null;
        Harness h = CreateHarness(window =>
        {
            p = Panel(window, "P");
            g = Group(p);
            gOther = Group(Panel(window, "Other"));
            return new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                SplitRatio = 0.5f,
                FirstChild = g,
                SecondChild = gOther,
            };
        });
        List<DockPanelNode> removed = new();
        h.Host.PanelRemoved += (_, panel) => removed.Add(panel);

        h.Host.UnpinPanel(p);
        h.Frame();

        // Drive the drawer's open state through the public toggle API, then close as the
        // drawer's close button would (its own wiring is out of scope here).
        h.Host.ShowAutoHideDrawer(p);
        h.Frame();
        h.Host.CloseAutoHidePanel(p);
        h.Frame();

        Assert.Equal(new[] { p }, removed);
        Assert.False(AnySideAutoHidden(h.Host));

        // The unreferenced placeholder collapses exactly as a plain close would: g is gone, its
        // sibling takes over the split's area.
        Assert.DoesNotContain(g, h.Host.LayoutModel.GetAllTabGroups());
        Assert.Same(gOther, h.Host.LayoutModel.RootNode);
    }

    [Fact]
    public void CloseAutoHidePanel_WhenClosingTheLastPlaceholder_RootBecomesTheSurvivingEmptyGroup_AndIsShown()
    {
        // Both panels auto-hidden (root split fully hidden -> host shows its empty placeholder);
        // closing one collapses its solo group and its now-lone sibling becomes the new root,
        // exempt from the hidden rule (a root group is always shown, even empty) - the display
        // must reflect that surviving root, not keep showing the generic "all hidden" content.
        DockPanelNode p = null;
        DockPanelNode q = null;
        DockTabGroupNode g = null;
        DockTabGroupNode gOther = null;
        Harness h = CreateHarness(window =>
        {
            p = Panel(window, "P");
            q = Panel(window, "Q");
            g = Group(p);
            gOther = Group(q);
            return new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                SplitRatio = 0.5f,
                FirstChild = g,
                SecondChild = gOther,
            };
        });

        h.Host.UnpinPanel(p);
        h.Frame();
        h.Host.UnpinPanel(q);
        h.Frame();

        h.Host.CloseAutoHidePanel(p);
        h.Frame();

        Assert.Same(gOther, h.Host.LayoutModel.RootNode);
        Assert.False(gOther.IsHiddenInLayout); // root exemption (P1)
        Assert.Contains(h.Host.GetAllVisibleTabGroups(), v => v.GroupNode == gOther);
    }
}
