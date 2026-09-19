using System;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Docking;

/// <summary>ADR-0015, T1.1: the docking containers (<see cref="MGDockTabGroup"/>, <see cref="MGDockSplitContainer"/>,
/// <see cref="MGDockAutoHideDrawer"/>) are <see cref="MGContentHost"/>s that announce every reparent, so a window's
/// name index sees exactly the content currently present in the docked tree, a rebuild or not, a float/redock or not,
/// a tab switch or not. One test group per acceptance bullet of <c>Docs/Tasks/dock-name-index-tasks.md</c>, T1.1
/// step 6.</summary>
public class DockNameIndexTests
{
    private sealed class Harness
    {
        public GraphTestRuntime Runtime;
        public MGDesktop Desktop;
        public MGWindow MainWindow;
        public MGDockHost Host;

        public DockPanelNode PanelA;
        public DockPanelNode PanelB;
        public DockTabGroupNode GroupA;
        public DockTabGroupNode GroupB;

        public MGContentPresenter PresenterA;
        public MGContentPresenter PresenterB;

        private int _elapsedMs;

        public void Frame()
        {
            MouseState mouse = new(1, 1, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(_elapsedMs), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
            _elapsedMs += 16;
        }

        public void Settle()
        {
            Frame();
            Frame();
        }
    }

    /// <summary>Creates a named element wrapped in a presenter so its content can be swapped later without losing the
    /// presenter identity that <see cref="DockPanelNode.ContentFactory"/> caches.</summary>
    private static MGContentPresenter CreateNamedPresenter(MGWindow window, string name, out MGElement named)
    {
        MGContentPresenter presenter = new(window);
        MGTextBlock textBlock = new(window, name) { Name = name };
        presenter.SetContent(textBlock);
        named = textBlock;
        return presenter;
    }

    /// <summary>Two docked panels, "A" (left) and "B" (right), each holding a presenter whose content is a text block
    /// named after the panel. Not yet attached to <paramref name="attachHostBeforeReturning"/> -- callers that need
    /// the "before the host is attached" scenario (a) inspect the tree before calling <see cref="AttachAndSettle"/>.</summary>
    private static Harness CreateHarness(bool attachHostBeforeReturning = true)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None };

        MGContentPresenter presenterA = CreateNamedPresenter(mainWindow, "A", out _);
        MGContentPresenter presenterB = CreateNamedPresenter(mainWindow, "B", out _);

        DockPanelNode panelA = new() { Title = "A", ContentFactory = () => presenterA };
        DockPanelNode panelB = new() { Title = "B", ContentFactory = () => presenterB };

        DockTabGroupNode groupA = new();
        groupA.AddPanel(panelA, -1);
        DockTabGroupNode groupB = new();
        groupB.AddPanel(panelB, -1);

        DockSplitNode root = new()
        {
            Orientation = Orientation.Horizontal,
            FirstChild = groupA,
            SecondChild = groupB,
            SplitRatio = 0.5f,
        };

        MGDockHost host = new(mainWindow)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LayoutModel = new DockLayoutModel(root),
        };
        host.RegisterPanel(panelA);
        host.RegisterPanel(panelB);

        Harness harness = new()
        {
            Runtime = runtime,
            Desktop = desktop,
            MainWindow = mainWindow,
            Host = host,
            PanelA = panelA,
            PanelB = panelB,
            GroupA = groupA,
            GroupB = groupB,
            PresenterA = presenterA,
            PresenterB = presenterB,
        };

        if (attachHostBeforeReturning)
        {
            mainWindow.SetContent(host);
            desktop.Windows.Add(mainWindow);
            harness.Settle();
        }

        return harness;
    }

    private static bool HasErrorPlaceholder(MGDockHost host)
        => host.TraverseVisualTree(true, false, false, false)
            .Any(element => element is MGTextBlock textBlock && textBlock.Text.StartsWith("Error building docking layout"));

    // ── a. named content created before the host is attached ────────────────

    [Fact]
    public void NamedContentCreatedBeforeAttachment_ResolvesAfterTheHostIsAttached()
    {
        Harness h = CreateHarness(attachHostBeforeReturning: false);

        // GetOrCreateContent already ran while building the model (the presenter and its named text
        // block exist), but nothing has been announced to any window yet.
        Assert.False(h.MainWindow.TryGetElementByName("A", out _));

        h.MainWindow.SetContent(h.Host);
        h.Desktop.Windows.Add(h.MainWindow);
        h.Settle();

        Assert.True(h.MainWindow.TryGetElementByName("A", out MGElement resolvedA));
        Assert.Same(h.PresenterA.Content, resolvedA);
        Assert.True(h.MainWindow.TryGetElementByName("B", out _));
    }

    // ── b. named content posed in a panel's presenter AFTER the host is attached, no rebuild ────

    [Fact]
    public void NamedContentPosedAfterAttachment_WithNoRebuild_ResolvesImmediately()
    {
        Harness h = CreateHarness();

        MGTextBlock later = new(h.MainWindow, "later") { Name = "Later" };
        h.PresenterA.SetContent(later);

        Assert.True(h.MainWindow.TryGetElementByName("Later", out MGElement resolved));
        Assert.Same(later, resolved);
    }

    // ── c. two RebuildVisualTree() in a row: no error placeholder, same instance resolved ────

    [Fact]
    public void TwoRebuildsInARow_ShowNoErrorPlaceholder_AndResolveTheSameInstance()
    {
        Harness h = CreateHarness();
        MGElement original = h.PresenterA.Content;

        h.Host.RebuildVisualTree();
        h.Host.RebuildVisualTree();

        Assert.False(HasErrorPlaceholder(h.Host));
        Assert.True(h.MainWindow.TryGetElementByName("A", out MGElement resolved));
        Assert.Same(original, resolved);
    }

    // ── d. content instance replaced between two rebuilds ("typed then dragged") ────

    [Fact]
    public void ContentInstanceReplacedBetweenTwoRebuilds_ResolvesTheNewInstance_NotTheOld()
    {
        Harness h = CreateHarness();
        h.Host.RebuildVisualTree();

        MGElement oldInstance = h.PresenterA.Content;
        Assert.True(h.MainWindow.TryGetElementByName("A", out MGElement resolvedOld));
        Assert.Same(oldInstance, resolvedOld);

        MGTextBlock newInstance = new(h.MainWindow, "A2") { Name = "A" };
        h.PresenterA.SetContent(newInstance);

        h.Host.RebuildVisualTree();

        Assert.False(HasErrorPlaceholder(h.Host));
        Assert.True(h.MainWindow.TryGetElementByName("A", out MGElement resolvedNew));
        Assert.Same(newInstance, resolvedNew);
        Assert.NotSame(oldInstance, resolvedNew);
    }

    // ── e. tab switch: inactive content not resolved, reactivated content resolved ────

    [Fact]
    public void TabSwitch_InactiveContentIsNotResolved_ReactivatedContentIsResolvedAgain()
    {
        Harness h = CreateHarness();

        DockOperation.DockAsTab(h.Host.LayoutModel, h.PanelB, h.GroupA);
        h.Settle();

        // Merging activates the newly added panel.
        Assert.Equal(h.PanelB.Id, h.GroupA.ActivePanelId);
        Assert.False(h.MainWindow.TryGetElementByName("A", out _));
        Assert.True(h.MainWindow.TryGetElementByName("B", out _));

        h.GroupA.SetActivePanel(h.PanelA.Id);
        h.Settle();

        Assert.True(h.MainWindow.TryGetElementByName("A", out MGElement resolvedA));
        Assert.Same(h.PresenterA.Content, resolvedA);
        Assert.False(h.MainWindow.TryGetElementByName("B", out _));

        h.GroupA.SetActivePanel(h.PanelB.Id);
        h.Settle();

        Assert.False(h.MainWindow.TryGetElementByName("A", out _));
        Assert.True(h.MainWindow.TryGetElementByName("B", out _));
    }

    // ── f. model float/redock: ordering proof of P4 ────

    [Fact]
    public void ModelBackedFloatThenRedock_MainWindowStopsThenResolvesAgain_NoPhantomEntry()
    {
        Harness h = CreateHarness();
        MGElement contentA = h.PresenterA.Content;

        MGFloatingDockWindow floatingWindow = h.Host.DetachToFloating(h.PanelA, new Point(500, 300));
        h.Settle();

        Assert.False(h.MainWindow.TryGetElementByName("A", out _));
        Assert.Single(h.Host.FloatingWindows);
        Assert.Same(floatingWindow, h.Host.FloatingWindows[0]);
        Assert.True(floatingWindow.TryGetElementByName("A", out MGElement resolvedInFloat));
        Assert.Same(contentA, resolvedInFloat);

        h.Host.RedockPanel(h.PanelA, floatingWindow);
        h.Settle();

        // Without the unconditional removal announcement (P4), this would either show the error
        // placeholder or leave a phantom entry in the main window.
        Assert.False(HasErrorPlaceholder(h.Host));
        Assert.True(h.MainWindow.TryGetElementByName("A", out MGElement resolvedAfterRedock));
        Assert.Same(contentA, resolvedAfterRedock);
        Assert.False(floatingWindow.TryGetElementByName("A", out _));
        Assert.IsType<MGDockTabGroup>(h.PresenterA.Parent);
        Assert.Contains(h.Host.GetAllTabGroups(), group => ReferenceEquals(group, h.PanelA.Parent));
    }

    // ── l. a group still listing content another host has taken announces its release: proof of P4 ────

    /// <summary>Unlike scenario f (a model-driven float always removes the panel from its source group's
    /// <c>Panels</c> collection live, which reacts through the guarded path with <c>Parent == this</c> still
    /// true), the public <see cref="MGFloatingDockWindow"/> constructor does not check that the panel is free:
    /// it steals the docked group's active content by a bare <c>SetParent</c> without touching the docked
    /// group's model at all, so the docked group's own <c>_activeContentContainer</c> field stays stale --
    /// <c>SetParent</c> never clears the previous holder's field. The only place anything notices is the next
    /// <see cref="MGDockHost.RebuildVisualTree"/>'s unconditional <c>Detach()</c> of every old tab-group visual,
    /// which is exactly the gap P4 covers.</summary>
    [Fact]
    public void StolenByAStandaloneFloatingWindow_TheStaleDockedGroupReleasesItOnRebuild()
    {
        Harness h = CreateHarness();
        MGElement contentA = h.PresenterA.Content; // the named "A" text block; PresenterA is the wrapper the tab groups reparent
        Assert.True(h.MainWindow.TryGetElementByName("A", out MGElement resolvedBefore));
        Assert.Same(contentA, resolvedBefore);

        // PanelA stays a member of GroupA.Panels the whole time: nothing ever fires GroupA's own
        // live Panels.CollectionChanged reaction, so this bypasses the path scenario f exercises.
        MGFloatingDockWindow stolen = new(h.Host, h.PanelA, 100, 100);
        h.Host.ParentWindow.AddNestedWindow(stolen);
        h.Settle();

        Assert.True(stolen.TryGetElementByName("A", out MGElement resolvedInStolen));
        Assert.Same(contentA, resolvedInStolen);

        h.Host.RebuildVisualTree();
        h.Settle();

        // Without the unconditional removal (P4), the stale docked group's Detach() (Parent != this,
        // since stolen's tab group holds it now) never announces the release, so the fresh docked
        // group's InvokeContentAdded hits IndexElementName with the still-indexed stale entry, and
        // RebuildVisualTree's catch turns that MGDuplicateElementNameException into the placeholder.
        Assert.False(HasErrorPlaceholder(h.Host));
        Assert.True(h.MainWindow.TryGetElementByName("A", out MGElement resolvedAfterRebuild));
        Assert.Same(contentA, resolvedAfterRebuild);
        MGElement dockedParentAfterRebuild = h.PresenterA.Parent;
        Assert.IsType<MGDockTabGroup>(dockedParentAfterRebuild);
        Assert.NotSame(stolen.TabGroup, dockedParentAfterRebuild);

        stolen.OwnerHost.CloseFloatingWindow(stolen);
        h.Settle();

        Assert.False(stolen.TryGetElementByName("A", out _));
        Assert.True(h.MainWindow.TryGetElementByName("A", out MGElement resolvedAfterClose));
        Assert.Same(contentA, resolvedAfterClose);
        // The guarded SetParent(null) in CloseFloatingWindow's Detach() must not deparent content the
        // docked group now holds: stolen's own tab group no longer owns it, so Detach() there only
        // announces the release from stolen's index, leaving the docked parent untouched.
        Assert.Same(dockedParentAfterRebuild, h.PresenterA.Parent);
    }

    // ── g. standalone floating window: proof of P5 ────

    [Fact]
    public void StandaloneFloatingWindow_ResolvesItsContent_AndReleasesItWhenClosed()
    {
        Harness h = CreateHarness();

        // A panel outside the host's model entirely -- the only path where Detach() is the sole release.
        MGContentPresenter presenterC = CreateNamedPresenter(h.MainWindow, "C", out MGElement contentC);
        DockPanelNode panelC = new() { Title = "C", ContentFactory = () => presenterC };

        MGFloatingDockWindow standaloneWindow = new(h.Host, panelC, 100, 100);
        h.Host.ParentWindow.AddNestedWindow(standaloneWindow);
        h.Settle();

        Assert.True(standaloneWindow.TryGetElementByName("C", out MGElement resolved));
        Assert.Same(contentC, resolved);

        h.Host.CloseFloatingWindow(standaloneWindow);
        h.Settle();

        Assert.False(standaloneWindow.TryGetElementByName("C", out _));
        // presenterC, not contentC, is what the tab group holds and reparents: contentC is presenterC's
        // own (unchanging) content, so it is presenterC's release that Detach() must have performed.
        Assert.DoesNotContain(presenterC, standaloneWindow.TabGroup.GetChildren());
        Assert.Null(presenterC.Parent);
    }

    // ── h. docked-to-docked drop path: double rebuild (mirrors ExecuteDrop) ────

    [Fact]
    public void DockedToDockedDrop_MirroringExecuteDropsDoubleRebuild_ShowsNoError()
    {
        Harness h = CreateHarness();

        DockOperation.DockAsTab(h.Host.LayoutModel, h.PanelB, h.GroupA);
        h.Host.RebuildVisualTree();
        h.Settle();

        Assert.False(HasErrorPlaceholder(h.Host));
        Assert.True(h.MainWindow.TryGetElementByName("B", out MGElement resolvedB));
        Assert.Same(h.PresenterB.Content, resolvedB);
    }

    // ── i. auto-hide: unpin, open drawer, hide drawer, repin ────

    [Fact]
    public void AutoHideCycle_TracksTheIndexAtEachStep()
    {
        Harness h = CreateHarness();

        h.Host.UnpinPanel(h.PanelA);
        h.Settle();
        Assert.False(h.MainWindow.TryGetElementByName("A", out _));

        h.Host.ShowAutoHideDrawer(h.PanelA);
        h.Settle();
        Assert.True(h.MainWindow.TryGetElementByName("A", out MGElement resolvedInDrawer));
        Assert.Same(h.PresenterA.Content, resolvedInDrawer);

        h.Host.HideAutoHideDrawer();
        h.Settle();
        Assert.False(h.MainWindow.TryGetElementByName("A", out _));

        h.Host.RepinPanel(h.PanelA);
        h.Settle();
        Assert.True(h.MainWindow.TryGetElementByName("A", out MGElement resolvedAfterRepin));
        Assert.Same(h.PresenterA.Content, resolvedAfterRepin);
    }

    // ── j. MGDockSplitContainer built directly under a presenter ────

    [Fact]
    public void SplitContainerBuiltDirectly_FirstChildReplacement_UnindexesTheOld_IndexesTheNew()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 800, 600);
        desktop.Windows.Add(window);
        MGContentPresenter presenter = new(window);
        window.SetContent(presenter);

        MGDockSplitContainer split = new(window);
        MGTextBlock a = new(window, "a") { Name = "A" };
        split.FirstChild = a;
        presenter.SetContent(split);
        desktop.Update();

        Assert.True(window.TryGetElementByName("A", out MGElement resolvedA));
        Assert.Same(a, resolvedA);

        MGTextBlock b = new(window, "b") { Name = "B" };
        split.FirstChild = b;
        desktop.Update();

        Assert.False(window.TryGetElementByName("A", out _));
        Assert.True(window.TryGetElementByName("B", out MGElement resolvedB));
        Assert.Same(b, resolvedB);
    }

    // ── k. closing a panel (RemovePanel) removes it from the index ────

    [Fact]
    public void RemovingAPanel_TakesItOutOfTheIndex()
    {
        Harness h = CreateHarness();

        Assert.True(h.Host.RemovePanel(h.PanelA.Id));
        h.Settle();

        Assert.False(h.MainWindow.TryGetElementByName("A", out _));
        Assert.True(h.MainWindow.TryGetElementByName("B", out _));
    }
}
