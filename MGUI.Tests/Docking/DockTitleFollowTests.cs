using System;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Docking;

/// <summary>
/// Reported by the author: the "*" a document editor puts in <see cref="DockPanelNode.Title"/> to mark unsaved
/// changes only showed up once its tab was redrawn for some other reason (hover, activation) — the editor updates
/// <see cref="DockPanelNode.Title"/> (which raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>),
/// but each view that shows a title had copied it once and never followed later changes.
///
/// Covers the four views that display a <see cref="DockPanelNode.Title"/>, each of which now subscribes to exactly
/// the panel(s) it currently displays and unsubscribes when it stops displaying them:
/// <list type="bullet">
/// <item><see cref="MGDockTabItem"/> (docked tab, and a discarded tab rebuilt by <see cref="MGDockTabGroup"/>'s
/// <c>RebuildTabHeaders</c> no longer following, the new tab following instead).</item>
/// <item><see cref="MGFloatingDockWindow"/> (follows the active panel, switches on activation change).</item>
/// <item><see cref="MGDockAutoHideStrip"/> (one button per auto-hidden panel).</item>
/// <item><see cref="MGDockAutoHideDrawer"/> (follows its active panel, stops following a previous one, and a drawer
/// replaced by a host template rebuild lets go of its panel).</item>
/// </list>
/// Every assertion below reads the displayed text immediately after mutating <see cref="DockPanelNode.Title"/>, with
/// no hover, activation or explicit relayout call in between — exactly the "redrawn for another reason" gap the bug
/// report describes.
/// </summary>
public class DockTitleFollowTests
{
    // ── Shared headless harness ───────────────────────────────────────────

    private sealed class Harness
    {
        public GraphTestRuntime Runtime;
        public MGDesktop Desktop;
        public MGWindow MainWindow;
        public MGDockHost Host;

        private int _elapsedMs;

        public void Frame(Point mousePosition = default)
        {
            Runtime.ApplyFrame(new UpdateBaseArgs(
                TimeSpan.FromMilliseconds(_elapsedMs),
                TimeSpan.FromMilliseconds(16),
                new MouseState(mousePosition.X, mousePosition.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
                new KeyboardState()));
            Desktop.Update();
            _elapsedMs += 16;
        }
    }

    private static DockPanelNode Panel(MGWindow window, string title)
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

        foreach (var panel in host.LayoutModel.GetAllPanels())
        {
            host.RegisterPanel(panel);
        }

        Harness harness = new() { Runtime = runtime, Desktop = desktop, MainWindow = mainWindow, Host = host };
        // Far away, well away from any tab or button, so nothing is hovered by the warm-up frames.
        Point farAway = new(1400, 1000);
        harness.Frame(farAway);
        harness.Frame(farAway);
        return harness;
    }

    private static MGDockTabItem GetTabItem(MGDockTabGroup group, DockPanelNode panel)
        => group.TraverseVisualTree<MGDockTabItem>(IncludeSelf: false).Single(t => t.Panel == panel);

    private static string TabTitleText(MGDockTabItem tab)
        => ((MGTextBlock)tab.TemplateParts[MGDockTabItem.TitleTextPartName]).Text;

    /// <summary>A bare window, with no <see cref="MGDesktop.Windows"/> registration and no <see cref="MGDockHost"/>:
    /// tests (1) and (1b) build the <see cref="MGDockTabGroup"/> directly against this window (the same pattern
    /// <c>DockPartVocabularyTests</c> uses), so the panel's <see cref="DockPanelNode.PropertyChanged"/> reaches
    /// nothing but the tab item under test. With a host-managed layout, a <see cref="DockLayoutModel"/> rebuilds the
    /// visual tree when a node it subscribed changes, which would mask a missing subscription; it subscribes only the
    /// nodes present when its root is assigned or a floating group is added, not a panel docked later through
    /// <see cref="DockOperation.DockAsTab"/> — the editor's document tabs, hence the missing "*".</summary>
    private static MGWindow CreateStandaloneWindow()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 480, 320));
        MGDesktop desktop = new(runtime);
        return new MGWindow(desktop, 0, 0, 400, 240) { WindowStyle = WindowStyle.None };
    }

    // ── (1) MGDockTabItem: docked tab follows its panel's title ───────────

    [Fact]
    public void DockedTab_FollowsPanelTitleChange_WithNoHoverOrRelayout()
    {
        MGWindow window = CreateStandaloneWindow();
        DockPanelNode a = new() { Title = "A" };
        DockPanelNode b = new() { Title = "B" };
        DockTabGroupNode g = new();
        g.AddPanel(a, -1);
        g.AddPanel(b, -1);

        MGDockTabGroup tabGroup = new(window) { GroupNode = g };
        MGDockTabItem tabA = GetTabItem(tabGroup, a);
        Assert.Equal("A", TabTitleText(tabA));

        // Mutate the title only — no frame, no hover, no click, no explicit layout call.
        a.Title = "A*";

        Assert.Equal("A*", TabTitleText(tabA));
    }

    // ── (1b) A tab rebuilt by RebuildTabHeaders lets go of its old panel ──

    [Fact]
    public void RebuildingTabHeaders_DetachesTheDiscardedTab_TheNewTabFollowsInstead()
    {
        MGWindow window = CreateStandaloneWindow();
        DockPanelNode a = new() { Title = "A" };
        DockPanelNode b = new() { Title = "B" };
        DockTabGroupNode g = new();
        g.AddPanel(a, -1);
        g.AddPanel(b, -1);

        MGDockTabGroup tabGroup = new(window) { GroupNode = g };
        MGDockTabItem oldTabA = GetTabItem(tabGroup, a);
        Assert.Equal("A", TabTitleText(oldTabA));

        // Adding a panel to the group's Panels collection triggers MGDockTabGroup.RebuildTabHeaders,
        // which discards every existing MGDockTabItem (including oldTabA) and builds fresh ones.
        DockPanelNode c = new() { Title = "C" };
        g.AddPanel(c, -1);

        MGDockTabItem newTabA = GetTabItem(tabGroup, a);
        Assert.NotSame(oldTabA, newTabA);

        a.Title = "A*";

        // The discarded tab item no longer mirrors the panel — MGDockTabGroup.RebuildTabHeaders
        // detached it (MGDockTabItem.Detach -> Panel = null) before dropping it.
        Assert.Equal("A", TabTitleText(oldTabA));
        // The tab item that replaced it does follow.
        Assert.Equal("A*", TabTitleText(newTabA));
    }

    // ── (2) MGFloatingDockWindow: follows the active panel, switches on activation ─

    [Fact]
    public void FloatingWindow_FollowsActivePanelTitle_AndSwitchesWhenActivePanelChanges()
    {
        DockPanelNode a = null, b = null;
        Harness h = CreateHarness(window =>
        {
            a = Panel(window, "A");
            b = Panel(window, "B");
            DockTabGroupNode g = new();
            g.AddPanel(a, -1);
            return g;
        });

        MGFloatingDockWindow floating = h.Host.DetachToFloating(a, new Point(500, 400));
        h.Frame();
        h.Frame();

        Assert.Equal("A", floating.TitleText);

        a.Title = "A*";
        Assert.Equal("A*", floating.TitleText);

        // Add a second panel and activate it: the window must now follow B instead of A.
        floating.AddPanel(b, -1);
        floating.GroupNode.SetActivePanel(b.Id);
        h.Frame();
        Assert.Equal("B", floating.TitleText);

        b.Title = "B*";
        Assert.Equal("B*", floating.TitleText);

        // A no longer drives the title: a further change to it must not reach TitleText.
        a.Title = "A**";
        Assert.Equal("B*", floating.TitleText);
    }

    // ── (3) MGDockAutoHideStrip: one button per auto-hidden panel ──────────

    [Fact]
    public void AutoHideStripButton_FollowsPanelTitleChange_WithNoHoverOrRelayout()
    {
        DockPanelNode p = null;
        Harness h = CreateHarness(window =>
        {
            p = Panel(window, "Hidden");
            DockTabGroupNode g = new();
            g.AddPanel(p, -1);
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

        h.Host.UnpinPanel(p);
        h.Frame();

        MGDockAutoHideStrip strip = (MGDockAutoHideStrip)h.Host.TemplateParts[GetAutoHideStripPartName(p.AutoHideSide)];
        // The strip shows exactly one panel on this side in this harness.
        MGBorder button = Assert.Single(strip.GetChildren().OfType<MGBorder>());
        Assert.Equal("Hidden", GetButtonLabelText(button));

        p.Title = "Hidden*";

        Assert.Equal("Hidden*", GetButtonLabelText(button));
    }

    private static string GetAutoHideStripPartName(AutoHideSide side) => side switch
    {
        AutoHideSide.Left => MGDockHost.LeftAutoHideStripPartName,
        AutoHideSide.Right => MGDockHost.RightAutoHideStripPartName,
        AutoHideSide.Top => MGDockHost.TopAutoHideStripPartName,
        AutoHideSide.Bottom => MGDockHost.BottomAutoHideStripPartName,
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    private static string GetButtonLabelText(MGBorder button) => button.Content switch
    {
        MGTextBlock tb => tb.Text,
        MGRotatedTextLabel rtl => rtl.Text,
        _ => null,
    };

    // ── (4) MGDockAutoHideDrawer: follows its active panel ─────────────────

    [Fact]
    public void AutoHideDrawer_FollowsActivePanelTitle_AndStopsFollowingThePreviousOne()
    {
        DockPanelNode p = null, q = null;
        Harness h = CreateHarness(window =>
        {
            p = Panel(window, "P");
            q = Panel(window, "Q");
            DockTabGroupNode g = new();
            g.AddPanel(p, -1);
            DockTabGroupNode gq = new();
            gq.AddPanel(q, -1);
            DockTabGroupNode other = new();
            other.AddPanel(Panel(window, "Other"), -1);
            return new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                SplitRatio = 0.5f,
                FirstChild = new DockSplitNode
                {
                    Orientation = Orientation.Vertical,
                    SplitRatio = 0.5f,
                    FirstChild = g,
                    SecondChild = gq,
                },
                SecondChild = other,
            };
        });

        h.Host.UnpinPanel(p);
        h.Frame();
        h.Host.UnpinPanel(q);
        h.Frame();

        MGDockAutoHideDrawer drawer = (MGDockAutoHideDrawer)h.Host.TemplateParts[MGDockHost.AutoHideDrawerPartName];
        MGTextBlock titleLabel = (MGTextBlock)drawer.TemplateParts[MGDockAutoHideDrawer.TitleBarTextPartName];

        h.Host.ShowAutoHideDrawer(p);
        h.Frame();
        Assert.Equal("P", titleLabel.Text);

        p.Title = "P*";
        Assert.Equal("P*", titleLabel.Text);

        // Switch the drawer to another panel: it must now follow Q, and no longer follow P.
        h.Host.ShowAutoHideDrawer(q);
        h.Frame();
        Assert.Equal("Q", titleLabel.Text);

        q.Title = "Q*";
        Assert.Equal("Q*", titleLabel.Text);

        p.Title = "P**";
        Assert.Equal("Q*", titleLabel.Text);
    }

    // ── (4b) A drawer replaced by a host template rebuild lets go of its panel ─

    [Fact]
    public void ReplacingTheHostStructure_DetachesTheOpenDrawer_TheNewDrawerFollowsInstead()
    {
        DockPanelNode p = null;
        Harness h = CreateHarness(window =>
        {
            p = Panel(window, "P");
            DockTabGroupNode g = new();
            g.AddPanel(p, -1);
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

        h.Host.UnpinPanel(p);
        h.Frame();
        h.Host.ShowAutoHideDrawer(p);
        h.Frame();

        MGDockAutoHideDrawer oldDrawer = (MGDockAutoHideDrawer)h.Host.TemplateParts[MGDockHost.AutoHideDrawerPartName];
        MGTextBlock oldTitleLabel = (MGTextBlock)oldDrawer.TemplateParts[MGDockAutoHideDrawer.TitleBarTextPartName];
        Assert.Equal("P", oldTitleLabel.Text);

        // Same replacement structure as DockCompositeStructuralTemplateTests: every surface is a new instance.
        h.Desktop.Resources.AddControlTemplate(new MGControlTemplate("Test.DockHost", context =>
        {
            MGControlTemplateStructure structure = new(null);
            structure.AddPart(MGDockHost.PreviewOverlayPartName, new MGDockPreviewOverlay(h.MainWindow));
            structure.AddPart(MGDockHost.DropIndicatorsPartName, new MGDockDropIndicators(h.MainWindow));
            structure.AddPart(MGDockHost.LeftAutoHideStripPartName, new MGDockAutoHideStrip(h.MainWindow, AutoHideSide.Left));
            structure.AddPart(MGDockHost.RightAutoHideStripPartName, new MGDockAutoHideStrip(h.MainWindow, AutoHideSide.Right));
            structure.AddPart(MGDockHost.TopAutoHideStripPartName, new MGDockAutoHideStrip(h.MainWindow, AutoHideSide.Top));
            structure.AddPart(MGDockHost.BottomAutoHideStripPartName, new MGDockAutoHideStrip(h.MainWindow, AutoHideSide.Bottom));
            structure.AddPart(MGDockHost.AutoHideDrawerPartName, new MGDockAutoHideDrawer(h.MainWindow));
            return structure;
        }, null, _ => { }));
        h.Host.ControlTemplateName = "Test.DockHost";
        Assert.Null(h.Host.LastControlTemplateError);

        MGDockAutoHideDrawer newDrawer = (MGDockAutoHideDrawer)h.Host.TemplateParts[MGDockHost.AutoHideDrawerPartName];
        Assert.NotSame(oldDrawer, newDrawer);
        // The discarded drawer released its panel when the host dropped it, and no longer mirrors its title.
        Assert.Null(oldDrawer.ActivePanel);
        string oldTitleBeforeChange = oldTitleLabel.Text;

        p.Title = "P*";

        Assert.Equal(oldTitleBeforeChange, oldTitleLabel.Text);

        // The drawer that replaced it shows the panel and follows it.
        h.Host.ShowAutoHideDrawer(p);
        h.Frame();
        MGTextBlock newTitleLabel = (MGTextBlock)newDrawer.TemplateParts[MGDockAutoHideDrawer.TitleBarTextPartName];
        Assert.Equal("P*", newTitleLabel.Text);

        p.Title = "P**";

        Assert.Equal("P**", newTitleLabel.Text);
        Assert.Equal(oldTitleBeforeChange, oldTitleLabel.Text);
    }
}
