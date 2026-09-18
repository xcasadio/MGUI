using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Editor;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Xunit;

namespace MGUI.Tests.Editor;

public class XamlEditorViewTests
{
    private static (MGDesktop Desktop, MGWindow Window, XamlEditorView View) CreateUnhostedView(int width, int height)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, width, height));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, width, height)
        {
            WindowStyle = WindowStyle.None,
        };

        XamlEditorSession session = new();
        XamlEditorView view = new(window, session);

        return (desktop, window, view);
    }

    private static (MGDesktop Desktop, MGWindow Window, XamlEditorView View, MGDockHost Host) CreateHostedView(int width, int height)
    {
        (MGDesktop desktop, MGWindow window, XamlEditorView view) = CreateUnhostedView(width, height);

        MGDockHost host = view.CreateDockHost();
        window.SetContent(host);
        desktop.Windows.Add(window);

        desktop.Update();
        desktop.Update();

        return (desktop, window, view, host);
    }

    // -- 1. Dockables: ids, titles, flags, order; content factories; panes start unparented and unnamed --

    [Fact]
    public void Dockables_HaveTheExpectedIdsTitlesAndFlags_AndPanesStartUnparentedAndUnnamed()
    {
        (_, _, XamlEditorView view) = CreateUnhostedView(1280, 720);

        Assert.Equal(5, view.Dockables.Count);

        (string Id, string Title, MGElement Pane, bool CanFloat)[] expected =
        {
            (XamlEditorView.TextDockableId, "XAML", view.TextPane, true),
            (XamlEditorView.PreviewDockableId, "Preview", view.PreviewPane, false),
            (XamlEditorView.TreeDockableId, "Document", view.TreePane, true),
            (XamlEditorView.PropertiesDockableId, "Properties", view.PropertyPane, true),
            (XamlEditorView.DiagnosticsDockableId, "Diagnostics", view.DiagnosticsPane, true),
        };

        for (int i = 0; i < expected.Length; i++)
        {
            DockableDefinition dockable = view.Dockables[i];
            Assert.Equal(expected[i].Id, dockable.DockableId);
            Assert.Equal(expected[i].Title, dockable.Title);
            Assert.False(dockable.CanClose);
            Assert.True(dockable.CanAutoHide);
            Assert.Equal(expected[i].CanFloat, dockable.CanFloat);
            Assert.Same(expected[i].Pane, dockable.ContentFactory());

            Assert.Null(expected[i].Pane.Parent);
            Assert.Null(expected[i].Pane.Name);
        }
    }

    // -- 2. Registry sees the five dockables as visible right after CreateDockHost() + two frames --

    [Fact]
    public void DockableRegistry_SeesAllFiveDockablesAsVisible_AfterCreateDockHost()
    {
        (_, _, _, MGDockHost host) = CreateHostedView(1280, 720);

        Assert.True(host.DockableRegistry.IsVisible(XamlEditorView.TextDockableId));
        Assert.True(host.DockableRegistry.IsVisible(XamlEditorView.PreviewDockableId));
        Assert.True(host.DockableRegistry.IsVisible(XamlEditorView.TreeDockableId));
        Assert.True(host.DockableRegistry.IsVisible(XamlEditorView.PropertiesDockableId));
        Assert.True(host.DockableRegistry.IsVisible(XamlEditorView.DiagnosticsDockableId));
    }

    // -- 3. Default layout: parents, non-empty bounds, relative positions, presenter still inside the preview pane --

    [Fact]
    public void DefaultLayout_OrdersThePanesAndSizesThemNonEmpty_InA1280x720Window()
    {
        (_, _, XamlEditorView view, MGDockHost host) = CreateHostedView(1280, 720);

        Rectangle textBounds = view.TextPane.LayoutBounds;
        Rectangle previewBounds = view.PreviewPane.LayoutBounds;
        Rectangle treeBounds = view.TreePane.LayoutBounds;
        Rectangle propertyBounds = view.PropertyPane.LayoutBounds;
        Rectangle diagnosticsBounds = view.DiagnosticsPane.LayoutBounds;

        Assert.NotNull(view.TextPane.Parent);
        Assert.NotNull(view.PreviewPane.Parent);
        Assert.NotNull(view.TreePane.Parent);
        Assert.NotNull(view.PropertyPane.Parent);
        Assert.NotNull(view.DiagnosticsPane.Parent);

        Assert.True(textBounds.Width > 0 && textBounds.Height > 0);
        Assert.True(previewBounds.Width > 0 && previewBounds.Height > 0);
        Assert.True(treeBounds.Width > 0 && treeBounds.Height > 0);
        Assert.True(propertyBounds.Width > 0 && propertyBounds.Height > 0);
        Assert.True(diagnosticsBounds.Width > 0 && diagnosticsBounds.Height > 0);

        // "XAML" left of "Preview", itself left of "Document".
        Assert.True(textBounds.Right <= previewBounds.Left);
        Assert.True(previewBounds.Right <= treeBounds.Left);

        // "Document" above "Properties" (same column).
        Assert.True(treeBounds.Bottom <= propertyBounds.Top);
        Assert.Equal(treeBounds.Left, propertyBounds.Left);
        Assert.Equal(treeBounds.Width, propertyBounds.Width);

        // "Diagnostics" below "XAML" and "Preview".
        Assert.True(textBounds.Bottom <= diagnosticsBounds.Top);
        Assert.True(previewBounds.Bottom <= diagnosticsBounds.Top);

        Assert.Contains(view.PreviewPresenter, view.PreviewPane.TraverseVisualTree(false, false, false, false));

        Assert.DoesNotContain(host.TraverseVisualTree(true, false, false, false),
            element => element is MGTextBlock textBlock && textBlock.Text.StartsWith("Error building docking layout"));
    }

    // -- 4. Inactive tab contract: Parent == null, not reached from the host, OnParentChanged raised at both transitions --

    [Fact]
    public void InactiveTab_DetachesItsContent_AndReattachesItWhenActivatedAgain()
    {
        (MGDesktop desktop, MGWindow window, XamlEditorView view, MGDockHost host) = CreateHostedView(1280, 720);

        DockPanelNode propertiesPanel = host.FindPanel(XamlEditorView.PropertiesDockableId);
        DockTabGroupNode documentGroup = host.GetAllTabGroups()
            .Single(group => group.Panels.Any(panel => panel.Id == XamlEditorView.TreeDockableId));

        DockOperation.DockAsTab(host.LayoutModel, propertiesPanel, documentGroup);
        Assert.Equal(XamlEditorView.PropertiesDockableId, documentGroup.ActivePanelId);

        desktop.Update();
        desktop.Update();
        Rectangle boundsWhileActive = view.PropertyPane.LayoutBounds;
        Assert.True(boundsWhileActive.Width > 0 && boundsWhileActive.Height > 0);

        int parentChangedCount = 0;
        view.PropertyPane.OnParentChanged += (_, _) => parentChangedCount++;

        // Switching the active tab back to "Document" detaches the (now inactive) Properties content.
        documentGroup.SetActivePanel(XamlEditorView.TreeDockableId);

        Assert.Null(view.PropertyPane.Parent);
        Assert.DoesNotContain(view.PropertyPane, host.TraverseVisualTree(false, false, false, false));
        Assert.Same(propertiesPanel, host.FindPanel(XamlEditorView.PropertiesDockableId));
        // DockTabGroupNode.ActivePanelId raises PropertyChanged twice per change (once as itself, once as
        // ActivePanel), and MGDockTabGroup reacts to both: the outgoing panel is detached once on the first
        // notification (the second notification only re-touches the panel that is already active, i.e. the
        // tree pane here). One event is the real, observable transition.
        Assert.Equal(1, parentChangedCount);

        // A detached pane is not laid out any more: it keeps its last bounds even when the window is resized.
        // "Hidden" is therefore read on Parent == null, never on empty bounds.
        window.WindowWidth = 1000;
        window.WindowHeight = 600;
        desktop.Update();
        desktop.Update();
        Assert.Null(view.PropertyPane.Parent);
        Assert.Equal(boundsWhileActive, view.PropertyPane.LayoutBounds);

        // Activating "Properties" again re-attaches it and recomputes its bounds. As the panel becoming
        // active, it goes through the same double notification as above: an attach on the first, then a
        // redundant detach and reattach on the second, so it ends up parented again but with 3 additional
        // OnParentChanged events (4 total) rather than 1 - the real behaviour of the docking code, not a
        // forced expectation.
        documentGroup.SetActivePanel(XamlEditorView.PropertiesDockableId);

        Assert.Equal(4, parentChangedCount);
        Assert.IsType<MGDockTabGroup>(view.PropertyPane.Parent);

        desktop.Update();
        desktop.Update();
        Rectangle boundsAfterReactivation = view.PropertyPane.LayoutBounds;
        Assert.True(boundsAfterReactivation.Width > 0 && boundsAfterReactivation.Height > 0);
        Assert.NotEqual(boundsWhileActive, boundsAfterReactivation);
        Assert.True(boundsAfterReactivation.Right <= 1000 && boundsAfterReactivation.Bottom <= 600);
    }

    // -- 5. Floating: displayed by the floating window, ParentWindow unchanged, RedockPanel puts it back --

    [Fact]
    public void DetachToFloating_DisplaysThePanelInTheFloatingWindow_AndRedockPanelReturnsItToTheLayout()
    {
        (_, MGWindow window, XamlEditorView view, MGDockHost host) = CreateHostedView(1280, 720);

        DockPanelNode propertiesPanel = host.FindPanel(XamlEditorView.PropertiesDockableId);

        MGFloatingDockWindow floatingWindow = host.DetachToFloating(propertiesPanel, new Point(600, 400));

        Assert.Same(floatingWindow, view.PropertyPane.DisplayingWindow);
        Assert.Same(window, floatingWindow.ParentWindow);
        Assert.Contains(floatingWindow, host.FloatingWindows);

        host.RedockPanel(propertiesPanel, floatingWindow);

        Assert.Empty(host.FloatingWindows);
        Assert.Contains(host.GetAllTabGroups(), group => group.Panels.Contains(propertiesPanel));
    }

    // -- 6. Robustness: tab switch + float/redock cycle raise no exception; no error placeholder; every pane accounted for --

    [Fact]
    public void TabSwitchAndFloatRedockCycle_RaisesNoException_AndLeavesNoPaneUnaccountedFor()
    {
        (_, _, XamlEditorView view, MGDockHost host) = CreateHostedView(1280, 720);

        // The default layout has no multi-panel group (every leaf is a single-panel tab group), so a tab
        // switch first needs two panels sharing a group: merge "Properties" into the "XAML" group.
        DockPanelNode propertiesPanel = host.FindPanel(XamlEditorView.PropertiesDockableId);
        DockPanelNode treePanel = host.FindPanel(XamlEditorView.TreeDockableId);
        DockTabGroupNode textGroup = host.GetAllTabGroups()
            .Single(group => group.Panels.Any(panel => panel.Id == XamlEditorView.TextDockableId));

        Exception exception = Record.Exception(() =>
        {
            DockOperation.DockAsTab(host.LayoutModel, propertiesPanel, textGroup);

            // Tab switch within the merged group.
            textGroup.SetActivePanel(XamlEditorView.TextDockableId);
            textGroup.SetActivePanel(XamlEditorView.PropertiesDockableId);

            // Float + re-dock cycle on a floatable pane.
            MGFloatingDockWindow floatingWindow = host.DetachToFloating(treePanel, new Point(500, 300));
            host.RedockPanel(treePanel, floatingWindow);
        });

        Assert.Null(exception);

        Assert.DoesNotContain(host.TraverseVisualTree(true, false, false, false),
            element => element is MGTextBlock textBlock && textBlock.Text.StartsWith("Error building docking layout"));

        MGElement[] panes = { view.TextPane, view.PreviewPane, view.TreePane, view.PropertyPane, view.DiagnosticsPane };
        var reachableFromHost = host.TraverseVisualTree(true, false, false, false).ToHashSet();
        var reachableFromFloatingWindows = host.FloatingWindows
            .SelectMany(floatingWindow => floatingWindow.TraverseVisualTree(true, false, false, false))
            .ToHashSet();

        foreach (MGElement pane in panes)
        {
            bool reached = reachableFromHost.Contains(pane) || reachableFromFloatingWindows.Contains(pane);
            bool detachedAsInactiveTab = pane.Parent == null
                && host.GetAllPanels().Any(panel => ReferenceEquals(panel.GetCachedContent(), pane));

            Assert.True(reached || detachedAsInactiveTab);
        }
    }

    // -- 7. No MGGridSplitter; constructor null checks; a second CreateDockHost() throws --

    [Fact]
    public void Host_HasNoGridSplitter()
    {
        (_, _, _, MGDockHost host) = CreateHostedView(1280, 720);

        Assert.DoesNotContain(host.TraverseVisualTree(true, false, false, false), element => element is MGGridSplitter);
    }

    [Fact]
    public void Constructor_ThrowsOnNullArguments()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 320, 240));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 320, 240)
        {
            WindowStyle = WindowStyle.None,
        };
        XamlEditorSession session = new();

        Assert.Throws<ArgumentNullException>(() => new XamlEditorView(null, session));
        Assert.Throws<ArgumentNullException>(() => new XamlEditorView(window, null));
    }

    [Fact]
    public void CreateDockHost_CalledTwice_Throws()
    {
        (_, _, XamlEditorView view) = CreateUnhostedView(1280, 720);

        view.CreateDockHost();

        Assert.Throws<InvalidOperationException>(() => view.CreateDockHost());
    }
}
