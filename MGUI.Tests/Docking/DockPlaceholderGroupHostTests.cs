using System;
using System.Collections.Generic;
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
/// Task T2 of <c>Docs/Tasks/docking-ghost-groups-tasks.md</c>: the host's visual tree skips hidden
/// placeholder groups (P1), and floating a panel then "Dock"-ing it (menu, drag, or
/// <see cref="MGDockHost.RedockPanel"/>) returns it to its exact original place (D2/D3), whatever
/// the order several panels return in. Layout reproduced from <c>MGUI.Editor/XamlEditorView.cs:126-166</c>
/// on branch <c>xaml-editor</c> (read-only reference, not built here): five groups, one panel each,
/// XAML/Document/Properties/Diagnostics floatable, Preview not.
/// </summary>
public class DockPlaceholderGroupHostTests
{
    private sealed class Harness
    {
        public GraphTestRuntime Runtime;
        public MGDesktop Desktop;
        public MGWindow MainWindow;
        public MGDockHost Host;
        public DockableRegistry Registry;

        public DockPanelNode Xaml;
        public DockPanelNode Preview;
        public DockPanelNode Document;
        public DockPanelNode Properties;
        public DockPanelNode Diagnostics;

        public DockTabGroupNode XamlGroup;
        public DockTabGroupNode PreviewGroup;
        public DockTabGroupNode DocumentGroup;
        public DockTabGroupNode PropertiesGroup;
        public DockTabGroupNode DiagnosticsGroup;

        public List<DockPanelNode> FloatablePanels => new() { Xaml, Document, Properties, Diagnostics };

        public List<DockPanelNode> AllPanels => new() { Xaml, Preview, Document, Properties, Diagnostics };

        private int _elapsedMs;

        public void Frame()
        {
            AdvanceFrame(Runtime, Desktop, _elapsedMs, Point.Zero);
            _elapsedMs += 16;
        }

        public void Frame(Point position, MouseButton? pressedButton = null)
        {
            AdvanceFrame(Runtime, Desktop, _elapsedMs, position, pressedButton);
            _elapsedMs += 16;
        }
    }

    /// <summary>
    /// Reproduces <c>XamlEditorView.CreateDockHost</c>'s default layout: root (0.72, horizontal)
    /// = leftBlock | rightColumn; leftBlock (0.78, vertical) = topSplit | Diagnostics; topSplit
    /// (0.45, horizontal) = XAML | Preview; rightColumn (0.45, vertical) = Document | Properties.
    /// </summary>
    private static Harness CreateHarness()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 1280, 720));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, 1280, 720) { WindowStyle = WindowStyle.None };

        DockableRegistry registry = new();
        DockableDefinition xamlDef = new("xaml", "XAML") { ContentFactory = () => new MGBorder(mainWindow) };
        DockableDefinition previewDef = new("preview", "Preview") { CanFloat = false, ContentFactory = () => new MGBorder(mainWindow) };
        DockableDefinition documentDef = new("document", "Document") { ContentFactory = () => new MGBorder(mainWindow) };
        DockableDefinition propertiesDef = new("properties", "Properties") { ContentFactory = () => new MGBorder(mainWindow) };
        DockableDefinition diagnosticsDef = new("diagnostics", "Diagnostics") { ContentFactory = () => new MGBorder(mainWindow) };
        foreach (var def in new[] { xamlDef, previewDef, documentDef, propertiesDef, diagnosticsDef })
        {
            registry.Register(def);
        }

        DockPanelNode xaml = xamlDef.CreatePanelNode();
        DockPanelNode preview = previewDef.CreatePanelNode();
        DockPanelNode document = documentDef.CreatePanelNode();
        DockPanelNode properties = propertiesDef.CreatePanelNode();
        DockPanelNode diagnostics = diagnosticsDef.CreatePanelNode();

        DockTabGroupNode xamlGroup = new();
        xamlGroup.AddPanel(xaml, -1);
        DockTabGroupNode previewGroup = new();
        previewGroup.AddPanel(preview, -1);
        DockTabGroupNode documentGroup = new();
        documentGroup.AddPanel(document, -1);
        DockTabGroupNode propertiesGroup = new();
        propertiesGroup.AddPanel(properties, -1);
        DockTabGroupNode diagnosticsGroup = new();
        diagnosticsGroup.AddPanel(diagnostics, -1);

        DockSplitNode topSplit = new()
        {
            Orientation = Orientation.Horizontal,
            FirstChild = xamlGroup,
            SecondChild = previewGroup,
            SplitRatio = 0.45f,
        };

        DockSplitNode leftBlock = new()
        {
            Orientation = Orientation.Vertical,
            FirstChild = topSplit,
            SecondChild = diagnosticsGroup,
            SplitRatio = 0.78f,
        };

        DockSplitNode rightColumn = new()
        {
            Orientation = Orientation.Vertical,
            FirstChild = documentGroup,
            SecondChild = propertiesGroup,
            SplitRatio = 0.45f,
        };

        DockSplitNode root = new()
        {
            Orientation = Orientation.Horizontal,
            FirstChild = leftBlock,
            SecondChild = rightColumn,
            SplitRatio = 0.72f,
        };

        MGDockHost host = new(mainWindow)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LayoutModel = new DockLayoutModel(root),
        };
        host.DockableRegistry = registry;

        // Register the five panels already sitting in the tree (the "LayoutModel then
        // RegisterPanel" pattern, P10), so ShowDockable's _panelRegistry lookup (a) and the
        // DockableRegistry's TryGetById (P4/T4) see them as any real host would.
        foreach (var panel in new[] { xaml, preview, document, properties, diagnostics })
        {
            host.RegisterPanel(panel);
        }

        mainWindow.SetContent(host);
        desktop.Windows.Add(mainWindow);

        Harness harness = new()
        {
            Runtime = runtime,
            Desktop = desktop,
            MainWindow = mainWindow,
            Host = host,
            Registry = registry,
            Xaml = xaml,
            Preview = preview,
            Document = document,
            Properties = properties,
            Diagnostics = diagnostics,
            XamlGroup = xamlGroup,
            PreviewGroup = previewGroup,
            DocumentGroup = documentGroup,
            PropertiesGroup = propertiesGroup,
            DiagnosticsGroup = diagnosticsGroup,
        };
        harness.Frame();
        harness.Frame();
        return harness;
    }

    private static string Json(Harness h) => DockLayoutSerializer.ToJson(h.Host.LayoutModel);

    /// <summary>A8 (common display check, P12): no host tab-group visual is bound to a hidden non-root
    /// group, and no split-container visual was built for a split with a hidden child.</summary>
    private static void AssertA8(Harness h)
    {
        foreach (var visual in h.Host.GetAllVisibleTabGroups())
        {
            Assert.False(visual.GroupNode.IsHiddenInLayout,
                $"MGDockTabGroup visual is bound to hidden group {visual.GroupNode.Id}.");
        }

        foreach (var splitVisual in h.Host.TraverseVisualTree<MGDockSplitContainer>(IncludeSelf: false))
        {
            var first = splitVisual.ModelNode?.FirstChild;
            var second = splitVisual.ModelNode?.SecondChild;
            var firstHidden = first == null || first.IsHiddenInLayout;
            var secondHidden = second == null || second.IsHiddenInLayout;
            Assert.False(firstHidden || secondHidden,
                "MGDockSplitContainer was built for a split with a hidden child.");
        }
    }

    private static void AssertPanelNotInAnyHostVisual(Harness h, DockPanelNode panel)
        => Assert.DoesNotContain(h.Host.GetAllVisibleTabGroups(), g => g.GroupNode.Panels.Any(p => p.Id == panel.Id));

    private static void AssertPanelInGroupVisual(Harness h, DockPanelNode panel, DockTabGroupNode group)
    {
        var visual = Assert.Single(h.Host.GetAllVisibleTabGroups(), g => g.GroupNode == group);
        Assert.Contains(visual.GroupNode.Panels, p => p.Id == panel.Id);
    }

    private static MGFloatingDockWindow Float(Harness h, DockPanelNode panel, Point dropPosition)
    {
        MGFloatingDockWindow floatingWindow = h.Host.DetachToFloating(panel, dropPosition);
        h.Frame();
        h.Frame();
        return floatingWindow;
    }

    private static void Dock(Harness h, DockPanelNode panel, MGFloatingDockWindow source)
    {
        h.Host.RedockPanel(panel, source);
        h.Frame();
        h.Frame();
    }

    // ── A1: each floatable panel floated then docked, one at a time ─────────

    [Fact]
    public void A1_EachFloatablePanel_FloatedThenDocked_OneAtATime_RestoresOriginalJson()
    {
        Harness harness = CreateHarness();
        string originalJson = Json(harness);

        var position = new Point(1200, 650);
        foreach (var panel in harness.FloatablePanels)
        {
            MGFloatingDockWindow window = Float(harness, panel, position);
            Assert.NotEqual(originalJson, Json(harness));

            Dock(harness, panel, window);

            Assert.Empty(harness.Host.FloatingWindows);
            Assert.Equal(originalJson, Json(harness));
            Assert.Empty(harness.Host.LayoutModel.Placements);
        }
    }

    // ── A2: the four floatable panels floated, then docked in each of the 24 orders ─

    [Fact]
    public void A2_AllFourFloatablePanels_DockedInEveryOrder_RestoresOriginalJson()
    {
        var panelTitles = new[] { "XAML", "Document", "Properties", "Diagnostics" };

        foreach (var order in Permutations(panelTitles))
        {
            Harness harness = CreateHarness();
            string originalJson = Json(harness);

            var byTitle = new Dictionary<string, DockPanelNode>
            {
                ["XAML"] = harness.Xaml,
                ["Document"] = harness.Document,
                ["Properties"] = harness.Properties,
                ["Diagnostics"] = harness.Diagnostics,
            };

            var windows = new Dictionary<string, MGFloatingDockWindow>();
            var position = new Point(1200, 650);
            foreach (var title in panelTitles)
            {
                windows[title] = Float(harness, byTitle[title], position);
                position.X -= 40; // keep floating windows from exactly overlapping
            }

            foreach (var title in order)
            {
                Dock(harness, byTitle[title], windows[title]);
            }

            Assert.Empty(harness.Host.FloatingWindows);
            Assert.Equal(originalJson, Json(harness));
            Assert.Empty(harness.Host.LayoutModel.Placements);
        }
    }

    private static IEnumerable<T[]> Permutations<T>(IReadOnlyList<T> items)
    {
        if (items.Count == 0)
        {
            yield return Array.Empty<T>();
            yield break;
        }

        for (var i = 0; i < items.Count; i++)
        {
            var rest = items.Where((_, idx) => idx != i).ToList();
            foreach (var restPermutation in Permutations(rest))
            {
                var result = new T[items.Count];
                result[0] = items[i];
                Array.Copy(restPermutation, 0, result, 1, restPermutation.Length);
                yield return result;
            }
        }
    }

    // ── T3 helpers: Unpin / Repin through the real host, one frame settle each ──────

    private static void Unpin(Harness h, DockPanelNode panel)
    {
        h.Host.UnpinPanel(panel);
        h.Frame();
        h.Frame();
    }

    private static void Repin(Harness h, DockPanelNode panel)
    {
        h.Host.RepinPanel(panel);
        h.Frame();
        h.Frame();
    }

    // ── A3: all five panes unpinned, then re-pinned in forward, reverse and a crossed order ──

    [Fact]
    public void A3_AllFivePanes_RepinnedInForwardOrder_RestoresOriginalJson()
        => RunA3(new[] { "XAML", "Preview", "Document", "Properties", "Diagnostics" });

    [Fact]
    public void A3_AllFivePanes_RepinnedInReverseOrder_RestoresOriginalJson()
        => RunA3(new[] { "Diagnostics", "Properties", "Document", "Preview", "XAML" });

    [Fact]
    public void A3_AllFivePanes_RepinnedInACrossedOrder_RestoresOriginalJson()
        => RunA3(new[] { "Properties", "XAML", "Diagnostics", "Preview", "Document" });

    private static void RunA3(string[] repinOrder)
    {
        Harness harness = CreateHarness();
        string originalJson = Json(harness);

        var byTitle = new Dictionary<string, DockPanelNode>
        {
            ["XAML"] = harness.Xaml,
            ["Preview"] = harness.Preview,
            ["Document"] = harness.Document,
            ["Properties"] = harness.Properties,
            ["Diagnostics"] = harness.Diagnostics,
        };

        var panelTitles = new[] { "XAML", "Preview", "Document", "Properties", "Diagnostics" };
        foreach (var title in panelTitles)
        {
            Unpin(harness, byTitle[title]);
        }

        foreach (var title in repinOrder)
        {
            Repin(harness, byTitle[title]);
        }

        Assert.Equal(originalJson, Json(harness));
        Assert.Empty(harness.Host.LayoutModel.GetAllAutoHidePanels());
        Assert.Empty(harness.Host.LayoutModel.Placements);
    }

    // ── mixed: one pane floated, one pane unpinned, returned in a crossed order ──────

    [Fact]
    public void Mixed_OneFloatedOnePinnedAway_ReturnedDockThenPin_RestoresOriginalJson()
        => RunMixed(dockFirst: true);

    [Fact]
    public void Mixed_OneFloatedOnePinnedAway_ReturnedPinThenDock_RestoresOriginalJson()
        => RunMixed(dockFirst: false);

    private static void RunMixed(bool dockFirst)
    {
        Harness harness = CreateHarness();
        string originalJson = Json(harness);

        MGFloatingDockWindow floatingWindow = Float(harness, harness.Document, new Point(1200, 650));
        Unpin(harness, harness.Properties);

        if (dockFirst)
        {
            Dock(harness, harness.Document, floatingWindow);
            Repin(harness, harness.Properties);
        }
        else
        {
            Repin(harness, harness.Properties);
            Dock(harness, harness.Document, floatingWindow);
        }

        Assert.Equal(originalJson, Json(harness));
        Assert.Empty(harness.Host.LayoutModel.GetAllAutoHidePanels());
        Assert.Empty(harness.Host.FloatingWindows);
        Assert.Empty(harness.Host.LayoutModel.Placements);
    }

    // ── A8 for the auto-hide paths (P12) ──────────────────────────────────

    [Fact]
    public void A8_UnpinPanel()
    {
        Harness harness = CreateHarness();

        harness.Host.UnpinPanel(harness.Document);
        harness.Frame();
        harness.Frame();

        Assert.True(harness.DocumentGroup.IsHiddenInLayout);
        AssertPanelNotInAnyHostVisual(harness, harness.Document);
        AssertA8(harness);
    }

    [Fact]
    public void A8_RepinPanel()
    {
        Harness harness = CreateHarness();
        Unpin(harness, harness.Document);

        harness.Host.RepinPanel(harness.Document);
        harness.Frame();
        harness.Frame();

        Assert.False(harness.DocumentGroup.IsHiddenInLayout);
        AssertPanelInGroupVisual(harness, harness.Document, harness.DocumentGroup);
        AssertA8(harness);
    }

    [Fact]
    public void A8_CloseAutoHidePanel_FromTheDrawerPath()
    {
        Harness harness = CreateHarness();
        Unpin(harness, harness.Document);
        List<DockPanelNode> removed = new();
        harness.Host.PanelRemoved += (_, panel) => removed.Add(panel);

        harness.Host.ShowAutoHideDrawer(harness.Document);
        harness.Frame();
        harness.Host.CloseAutoHidePanel(harness.Document);
        harness.Frame();
        harness.Frame();

        Assert.Equal(new[] { harness.Document }, removed);
        Assert.Empty(harness.Host.LayoutModel.GetAllAutoHidePanels());
        AssertPanelNotInAnyHostVisual(harness, harness.Document);
        AssertA8(harness);
    }

    // ── A7: while a placeholder exists, no visual for it, sibling fills the area ────

    [Fact]
    public void A7_WhilePlaceholderExists_NoHiddenGroupVisual_AndSiblingFillsTheFormerSplitArea()
    {
        Harness harness = CreateHarness();

        // topSplit's area, before floating XAML.
        var topSplitVisual = harness.Host.TraverseVisualTree<MGDockSplitContainer>(IncludeSelf: false)
            .Single(sc => sc.ModelNode == harness.XamlGroup.Parent);
        var topSplitArea = topSplitVisual.LayoutBounds;

        MGFloatingDockWindow floatingWindow = Float(harness, harness.Xaml, new Point(1200, 650));

        Assert.True(harness.XamlGroup.IsHiddenInLayout);
        AssertA8(harness);

        // XAML's group is gone from the host's visible groups; Preview (its sibling in topSplit)
        // now fills the entire area topSplit used to occupy - no split container remains there.
        Assert.DoesNotContain(harness.Host.GetAllVisibleTabGroups(), g => g.GroupNode == harness.XamlGroup);
        var previewVisual = Assert.Single(harness.Host.GetAllVisibleTabGroups(), g => g.GroupNode == harness.PreviewGroup);
        Assert.Equal(topSplitArea, previewVisual.LayoutBounds);
        Assert.DoesNotContain(harness.Host.TraverseVisualTree<MGDockSplitContainer>(IncludeSelf: false),
            sc => sc.ModelNode == harness.XamlGroup.Parent);

        Dock(harness, harness.Xaml, floatingWindow);
        AssertA8(harness);
    }

    // ── A8: display check across every path T2 touches ──────────────────────

    [Fact]
    public void A8_Float_FromTabContextMenu_Menu()
    {
        Harness harness = CreateHarness();
        MGDockTabItem tab = GetSoleDockedTabItem(harness, harness.DocumentGroup);

        OpenContextMenu(harness, tab);
        MGContextMenuButton floatButton = FindMenuButton(harness.Desktop.ActiveContextMenu, "Float");
        Assert.NotNull(floatButton);
        floatButton.Action.Invoke(floatButton);
        harness.Frame();
        harness.Frame();

        Assert.Single(harness.Host.FloatingWindows);
        AssertPanelNotInAnyHostVisual(harness, harness.Document);
        AssertA8(harness);
    }

    [Fact]
    public void A8_Float_FromDragReleasedOutsideHost()
    {
        Harness harness = CreateHarness();
        MGDockTabItem tab = GetSoleDockedTabItem(harness, harness.DocumentGroup);

        Point pressPoint = tab.LayoutBounds.Center;
        Point outsideHost = new(harness.Host.LayoutBounds.Right + 200, harness.Host.LayoutBounds.Bottom + 200);
        Point midway = new((pressPoint.X + outsideHost.X) / 2, (pressPoint.Y + outsideHost.Y) / 2);

        harness.Frame(pressPoint, MouseButton.Left);
        harness.Frame(midway, MouseButton.Left);
        harness.Frame(outsideHost, MouseButton.Left);
        harness.Frame(outsideHost, MouseButton.Left);
        Assert.NotNull(harness.Host.CurrentDrag);
        Assert.True(harness.Host.CurrentDrag.HasExceededThreshold);

        harness.Frame(outsideHost); // release: no drop target, outside host -> DetachToFloating
        harness.Frame(outsideHost);

        Assert.Single(harness.Host.FloatingWindows);
        AssertPanelNotInAnyHostVisual(harness, harness.Document);
        AssertA8(harness);
    }

    [Fact]
    public void A8_Dock_FromTabContextMenu_Menu()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = Float(harness, harness.Document, new Point(1200, 650));
        MGDockTabItem tab = GetSoleFloatingTabItem(floatingWindow, harness.Document);

        OpenContextMenu(harness, tab);
        MGContextMenuButton dockButton = FindMenuButton(harness.Desktop.ActiveContextMenu, "Dock");
        Assert.NotNull(dockButton);
        dockButton.Action.Invoke(dockButton);
        harness.Frame();
        harness.Frame();

        Assert.Empty(harness.Host.FloatingWindows);
        AssertPanelInGroupVisual(harness, harness.Document, harness.DocumentGroup);
        AssertA8(harness);
    }

    [Fact]
    public void A8_Dock_FromDraggingFloatedTabOntoDockedGroupCenter()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = Float(harness, harness.Document, new Point(1200, 650));
        MGDockTabItem tab = GetSoleFloatingTabItem(floatingWindow, harness.Document);

        var propertiesVisual = Assert.Single(harness.Host.GetAllVisibleTabGroups(), g => g.GroupNode == harness.PropertiesGroup);
        Point pressPoint = tab.LayoutBounds.Center;
        Point overTarget = propertiesVisual.LayoutBounds.Center;
        Point midway = new((pressPoint.X + overTarget.X) / 2, (pressPoint.Y + overTarget.Y) / 2);

        harness.Frame(pressPoint, MouseButton.Left);
        harness.Frame(midway, MouseButton.Left);
        harness.Frame(overTarget, MouseButton.Left);
        harness.Frame(overTarget, MouseButton.Left);
        Assert.NotNull(harness.Host.CurrentDropTarget);
        Assert.Equal(DockZone.Center, harness.Host.CurrentDropTarget.Zone);

        harness.Frame(overTarget); // release over Properties' center -> ExecuteDrop floating branch
        harness.Frame(overTarget);

        Assert.Empty(harness.Host.FloatingWindows);
        AssertPanelInGroupVisual(harness, harness.Document, harness.PropertiesGroup);
        AssertA8(harness);
    }

    [Fact]
    public void A8_CloseFloatingPanel_FromTabContextMenu_Menu()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = Float(harness, harness.Document, new Point(1200, 650));
        MGDockTabItem tab = GetSoleFloatingTabItem(floatingWindow, harness.Document);
        List<DockPanelNode> removed = new();
        harness.Host.PanelRemoved += (_, panel) => removed.Add(panel);

        OpenContextMenu(harness, tab);
        MGContextMenuButton closeButton = FindMenuButton(harness.Desktop.ActiveContextMenu, "Close");
        Assert.NotNull(closeButton);
        closeButton.Action.Invoke(closeButton);
        harness.Frame();
        harness.Frame();

        Assert.Equal(new[] { harness.Document }, removed);
        Assert.Empty(harness.Host.FloatingWindows);
        AssertPanelNotInAnyHostVisual(harness, harness.Document);
        AssertA8(harness);
    }

    [Fact]
    public void A8_CloseFloatingWindow_ViaTryCloseWindow()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = Float(harness, harness.Document, new Point(1200, 650));
        List<DockPanelNode> removed = new();
        harness.Host.PanelRemoved += (_, panel) => removed.Add(panel);

        Assert.True(floatingWindow.TryCloseWindow());
        harness.Frame();
        harness.Frame();

        Assert.Equal(new[] { harness.Document }, removed);
        Assert.Empty(harness.Host.FloatingWindows);
        Assert.DoesNotContain(floatingWindow, harness.MainWindow.NestedWindows);
        AssertPanelNotInAnyHostVisual(harness, harness.Document);
        AssertA8(harness);
    }

    // ── dropped elsewhere: the place is forgotten (explicit drop replaces it) ───

    [Fact]
    public void FloatedPanel_DroppedIntoAnotherGroup_ThenFloatedAndDocked_ReturnsToTheNewGroup()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = Float(harness, harness.Document, new Point(1200, 650));
        MGDockTabItem tab = GetSoleFloatingTabItem(floatingWindow, harness.Document);

        var propertiesVisual = Assert.Single(harness.Host.GetAllVisibleTabGroups(), g => g.GroupNode == harness.PropertiesGroup);
        Point pressPoint = tab.LayoutBounds.Center;
        Point overTarget = propertiesVisual.LayoutBounds.Center;
        Point midway = new((pressPoint.X + overTarget.X) / 2, (pressPoint.Y + overTarget.Y) / 2);

        harness.Frame(pressPoint, MouseButton.Left);
        harness.Frame(midway, MouseButton.Left);
        harness.Frame(overTarget, MouseButton.Left);
        harness.Frame(overTarget, MouseButton.Left);
        harness.Frame(overTarget);
        harness.Frame(overTarget);

        Assert.Contains(harness.Document, harness.PropertiesGroup.Panels);

        // Float again from its new home, then Dock - it must go back to Properties, not DocumentGroup.
        MGFloatingDockWindow secondFloat = Float(harness, harness.Document, new Point(1200, 650));
        Dock(harness, harness.Document, secondFloat);

        AssertPanelInGroupVisual(harness, harness.Document, harness.PropertiesGroup);
        Assert.DoesNotContain(harness.Document, harness.DocumentGroup.Panels);
    }

    // ── all panels floated: empty placeholder host content, host-edge drop still works ────

    [Fact]
    public void AllPanelsFloated_HostShowsEmptyPlaceholder_AndHostEdgeDropStillDocks()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None };

        DockPanelNode panelA = new() { Title = "A", ContentFactory = () => new MGBorder(mainWindow) };
        DockPanelNode panelB = new() { Title = "B", ContentFactory = () => new MGBorder(mainWindow) };
        DockTabGroupNode groupA = new();
        groupA.AddPanel(panelA, -1);
        DockTabGroupNode groupB = new();
        groupB.AddPanel(panelB, -1);
        DockSplitNode rootSplit = new()
        {
            Orientation = Orientation.Horizontal,
            SplitRatio = 0.5f,
            MinFirstSize = 0,
            MinSecondSize = 0,
            FirstChild = groupA,
            SecondChild = groupB,
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

        MGFloatingDockWindow floatA = host.DetachToFloating(panelA, new Point(600, 500));
        AdvanceFrame(runtime, desktop, 32, Point.Zero);
        AdvanceFrame(runtime, desktop, 48, Point.Zero);
        MGFloatingDockWindow floatB = host.DetachToFloating(panelB, new Point(600, 100));
        AdvanceFrame(runtime, desktop, 64, Point.Zero);
        AdvanceFrame(runtime, desktop, 80, Point.Zero);

        Assert.Empty(host.GetAllVisibleTabGroups());
        Assert.True(host.LayoutModel.RootNode.IsHiddenInLayout);

        var hostEdgeZones = DockDropCalculator.CalculateHostEdgeZones(host.LayoutBounds);
        var rightZone = hostEdgeZones.Single(z => z.Zone == DockZone.Right);
        Point aim = rightZone.HitRect.Center;

        MGDockTabItem tabA = GetSoleFloatingTabItem(floatA, panelA);
        Point pressPoint = tabA.LayoutBounds.Center;
        Point midway = new((pressPoint.X + aim.X) / 2, (pressPoint.Y + aim.Y) / 2);

        AdvanceFrame(runtime, desktop, 96, pressPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 112, midway, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 128, aim, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 144, aim, MouseButton.Left);
        Assert.NotNull(host.CurrentDropTarget);
        Assert.Equal(DockZone.Right, host.CurrentDropTarget.Zone);

        AdvanceFrame(runtime, desktop, 160, aim);
        AdvanceFrame(runtime, desktop, 176, aim);

        Assert.Contains(host.GetAllVisibleTabGroups(), g => g.GroupNode.Panels.Any(p => p.Id == panelA.Id));
        // floatB is untouched by this drop.
        Assert.Single(host.FloatingWindows);
    }

    // ── floating window bounds mirror the model (P9), and model replacement syncs windows (P10) ──

    [Fact]
    public void FloatingWindowBounds_MovingOrResizing_UpdatesTheFloatingGroup()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = Float(harness, harness.Document, new Point(1200, 650));
        DockFloatingGroup floatingGroup = harness.Host.LayoutModel.FindFloatingGroupOf(harness.Document.Id);
        Assert.NotNull(floatingGroup);

        floatingWindow.Left = floatingWindow.Left + 37;
        floatingWindow.Top = floatingWindow.Top + 11;
        floatingWindow.WindowWidth = floatingWindow.WindowWidth + 20;
        floatingWindow.WindowHeight = floatingWindow.WindowHeight + 15;
        harness.Frame();
        harness.Frame();

        Assert.Equal(floatingWindow.Left, floatingGroup.Left);
        Assert.Equal(floatingWindow.Top, floatingGroup.Top);
        Assert.Equal(floatingWindow.WindowWidth, floatingGroup.Width);
        Assert.Equal(floatingWindow.WindowHeight, floatingGroup.Height);
    }

    [Fact]
    public void ReplacingLayoutModel_ClosesOldModelWindows_WithoutPanelRemoved_AndOpensNewModelWindows()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = Float(harness, harness.Document, new Point(1200, 650));
        List<DockPanelNode> removed = new();
        harness.Host.PanelRemoved += (_, panel) => removed.Add(panel);

        // A new model whose own floating store already has one entry.
        DockPanelNode freshPanel = new() { Title = "Fresh", ContentFactory = () => new MGBorder(harness.MainWindow) };
        DockTabGroupNode freshGroup = new();
        freshGroup.AddPanel(freshPanel, -1);
        DockLayoutModel newModel = new(new DockTabGroupNode());
        DockFloatingGroup newFloatingGroup = new(freshGroup, 50, 50, 300, 200);
        newModel.AddFloatingGroup(newFloatingGroup);

        harness.Host.LayoutModel = newModel;
        harness.Frame();
        harness.Frame();

        Assert.Empty(removed); // no panel close reported for the old model's windows (P10)
        Assert.DoesNotContain(floatingWindow, harness.MainWindow.NestedWindows);
        Assert.Single(harness.Host.FloatingWindows);
        Assert.Same(newFloatingGroup, harness.Host.FloatingWindows[0].FloatingGroup);
        Assert.Contains(harness.Host.FloatingWindows[0], harness.MainWindow.NestedWindows);
    }

    // ── fix round: CloseFloatingWindow on a live model-backed window ────────

    [Fact]
    public void CloseFloatingWindow_OnALiveModelBackedWindow_DetachesItFromTheModel_AndDoesNotResurrect()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = Float(harness, harness.Document, new Point(1200, 650));
        DockFloatingGroup floatingGroup = floatingWindow.FloatingGroup;
        Assert.NotNull(floatingGroup);
        Assert.Contains(floatingGroup, harness.Host.LayoutModel.FloatingGroups);
        List<DockPanelNode> removed = new();
        harness.Host.PanelRemoved += (_, panel) => removed.Add(panel);

        // Calling the public API directly (not through TryCloseWindow) must detach the group from
        // the model, or the next commit's SyncFloatingWindows would resurrect it (P1).
        harness.Host.CloseFloatingWindow(floatingWindow);

        Assert.Equal(new[] { harness.Document }, removed);
        Assert.Empty(harness.Host.FloatingWindows);
        Assert.DoesNotContain(floatingGroup, harness.Host.LayoutModel.FloatingGroups);
        Assert.DoesNotContain(floatingWindow, harness.MainWindow.NestedWindows);

        // Any later commit must not bring the panel back as a new floating window.
        MGFloatingDockWindow otherFloat = Float(harness, harness.Properties, new Point(1200, 650));
        Dock(harness, harness.Properties, otherFloat);

        Assert.Empty(harness.Host.FloatingWindows);
        AssertPanelNotInAnyHostVisual(harness, harness.Document);
    }

    // ── fix round: CreateFloatingWindow on an already-docked panel ──────────

    [Fact]
    public void CreateFloatingWindow_OnAnAlreadyDockedPanel_UnregistersIt_AndRemovePanelBecomesANoOp()
    {
        Harness harness = CreateHarness();

        MGFloatingDockWindow floatingWindow = harness.Host.CreateFloatingWindow(harness.Document, 1200, 650);
        harness.Frame();
        harness.Frame();

        Assert.NotNull(floatingWindow.FloatingGroup);
        Assert.Contains(floatingWindow.FloatingGroup, harness.Host.LayoutModel.FloatingGroups);
        Assert.Contains(harness.Document, floatingWindow.GroupNode.Panels);

        // The panel left the host's panel registry when it moved into the model's floating store
        // (mirroring DetachToFloating); RemovePanel must therefore be a no-op instead of tearing the
        // panel out of a floating group it does not know about and leaving an orphaned window.
        bool removedByPublicApi = harness.Host.RemovePanel(harness.Document.Id);

        Assert.False(removedByPublicApi);
        Assert.Contains(floatingWindow, harness.Host.FloatingWindows);
        Assert.Contains(harness.Document, floatingWindow.GroupNode.Panels);
        Assert.Contains(floatingWindow.FloatingGroup, harness.Host.LayoutModel.FloatingGroups);
    }

    // ── fix round: RemovePanel on a currently auto-hidden panel ─────────────

    [Fact]
    public void RemovePanel_OnAnAutoHiddenPanel_RefreshesItsStripButton_AndClosesItsDrawer()
    {
        Harness harness = CreateHarness();
        Unpin(harness, harness.Properties);

        harness.Host.ShowAutoHideDrawer(harness.Properties);
        harness.Frame();

        MGDockAutoHideStrip strip = harness.Host.TraverseVisualTree<MGDockAutoHideStrip>(IncludeSelf: true)
            .Single(s => s.GetChildren().Any());
        Assert.Single(strip.GetChildren());

        MGDockAutoHideDrawer drawer = harness.Host.TraverseVisualTree<MGDockAutoHideDrawer>(IncludeSelf: true).Single();
        Assert.Equal(Visibility.Visible, drawer.Visibility);

        Assert.True(harness.Host.RemovePanel(harness.Properties.Id));
        harness.Frame();
        harness.Frame();

        // The stale button must be gone, not just the model entry, or clicking it would reopen
        // the drawer on a panel that was just told it is closed.
        Assert.Empty(strip.GetChildren());
        Assert.Equal(Visibility.Collapsed, drawer.Visibility);
    }

    // ── T4: close then reopen at the original place ─────────────────────────

    /// <summary>Uniqueness invariant (T4): each panel id appears at most once across the layout
    /// tree, the floating store and the auto-hide store.</summary>
    private static void AssertUniquePanelIds(Harness h)
    {
        var ids = new List<string>();
        ids.AddRange(CollectTreeIds(h.Host.LayoutModel.RootNode));
        ids.AddRange(h.Host.LayoutModel.FloatingGroups.SelectMany(g => g.Group.Panels.Select(p => p.Id)));
        ids.AddRange(h.Host.LayoutModel.GetAllAutoHidePanels().Select(p => p.Id));

        var duplicates = ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(duplicates.Count == 0, $"Duplicate panel id(s) found: {string.Join(", ", duplicates)}.");
    }

    private static IEnumerable<string> CollectTreeIds(DockNode node)
    {
        switch (node)
        {
            case null:
                yield break;
            case DockTabGroupNode group:
                foreach (var p in group.Panels)
                {
                    yield return p.Id;
                }
                yield break;
            case DockSplitNode split:
                foreach (var id in CollectTreeIds(split.FirstChild))
                {
                    yield return id;
                }
                foreach (var id in CollectTreeIds(split.SecondChild))
                {
                    yield return id;
                }
                yield break;
        }
    }

    // ── A4: each panel closed then reopened with ShowDockable ────────────────

    [Fact]
    public void A4_EachPanel_ClosedThenReopened_OneAtATime_RestoresOriginalJson()
    {
        Harness harness = CreateHarness();
        string originalJson = Json(harness);

        foreach (var panel in harness.AllPanels)
        {
            string id = panel.Id;

            Assert.True(harness.Host.RemovePanel(id));
            harness.Frame();
            harness.Frame();
            AssertUniquePanelIds(harness);

            Assert.True(harness.Host.ShowDockable(id));
            harness.Frame();
            harness.Frame();
            AssertUniquePanelIds(harness);

            Assert.Equal(originalJson, Json(harness));
        }

        Assert.Empty(harness.Host.LayoutModel.Placements);
    }

    [Fact]
    public void A4_AllPanels_ClosedThenReopened_InReverseOrder_RestoresOriginalJson()
    {
        var ids = new[] { "xaml", "preview", "document", "properties", "diagnostics" };
        RunA4(CreateHarness(), ids, ids.Reverse().ToArray());
    }

    [Fact]
    public void A4_AllPanels_ClosedThenReopened_InACrossedOrder_RestoresOriginalJson()
    {
        var closeOrder = new[] { "xaml", "preview", "document", "properties", "diagnostics" };
        var reopenOrder = new[] { "properties", "xaml", "diagnostics", "preview", "document" };
        RunA4(CreateHarness(), closeOrder, reopenOrder);
    }

    private static void RunA4(Harness harness, string[] closeOrder, string[] reopenOrder)
    {
        string originalJson = Json(harness);

        foreach (var id in closeOrder)
        {
            Assert.True(harness.Host.RemovePanel(id));
            harness.Frame();
            harness.Frame();
            AssertUniquePanelIds(harness);
        }

        foreach (var id in reopenOrder)
        {
            Assert.True(harness.Host.ShowDockable(id));
            harness.Frame();
            harness.Frame();
            AssertUniquePanelIds(harness);
        }

        Assert.Equal(originalJson, Json(harness));
        Assert.Empty(harness.Host.LayoutModel.Placements);
    }

    // ── closed from a floating window / the auto-hide drawer, reopened at the original place ──

    [Fact]
    public void ClosedFromFloatingWindow_ReopensAtItsOriginalDockedPlace()
    {
        Harness harness = CreateHarness();
        string originalJson = Json(harness);

        MGFloatingDockWindow floatingWindow = Float(harness, harness.Document, new Point(1200, 650));
        MGDockTabItem tab = GetSoleFloatingTabItem(floatingWindow, harness.Document);
        List<DockPanelNode> removed = new();
        harness.Host.PanelRemoved += (_, panel) => removed.Add(panel);

        OpenContextMenu(harness, tab);
        MGContextMenuButton closeButton = FindMenuButton(harness.Desktop.ActiveContextMenu, "Close");
        Assert.NotNull(closeButton);
        closeButton.Action.Invoke(closeButton);
        harness.Frame();
        harness.Frame();

        Assert.Equal(new[] { harness.Document }, removed);
        Assert.Empty(harness.Host.FloatingWindows);

        Assert.True(harness.Host.ShowDockable(harness.Document.Id));
        harness.Frame();
        harness.Frame();

        var reopened = harness.Host.FindPanel(harness.Document.Id);
        Assert.NotNull(reopened);
        AssertPanelInGroupVisual(harness, reopened, harness.DocumentGroup);
        Assert.Equal(originalJson, Json(harness));
    }

    [Fact]
    public void ClosedFromAutoHideDrawer_ReopensAtItsOriginalDockedPlace()
    {
        Harness harness = CreateHarness();
        string originalJson = Json(harness);

        Unpin(harness, harness.Properties);
        List<DockPanelNode> removed = new();
        harness.Host.PanelRemoved += (_, panel) => removed.Add(panel);

        harness.Host.ShowAutoHideDrawer(harness.Properties);
        harness.Frame();
        harness.Host.CloseAutoHidePanel(harness.Properties);
        harness.Frame();
        harness.Frame();

        Assert.Equal(new[] { harness.Properties }, removed);
        Assert.Empty(harness.Host.LayoutModel.GetAllAutoHidePanels());

        Assert.True(harness.Host.ShowDockable(harness.Properties.Id));
        harness.Frame();
        harness.Frame();

        var reopened = harness.Host.FindPanel(harness.Properties.Id);
        Assert.NotNull(reopened);
        AssertPanelInGroupVisual(harness, reopened, harness.PropertiesGroup);
        Assert.Equal(originalJson, Json(harness));
    }

    [Fact]
    public void ClosedByClosingTheFloatingWindow_ViaTryCloseWindow_ReopensAtItsOriginalDockedPlace()
    {
        Harness harness = CreateHarness();
        string originalJson = Json(harness);

        MGFloatingDockWindow floatingWindow = Float(harness, harness.Document, new Point(1200, 650));

        Assert.True(floatingWindow.TryCloseWindow());
        harness.Frame();
        harness.Frame();

        Assert.Empty(harness.Host.FloatingWindows);
        Assert.True(harness.Host.LayoutModel.TryGetPlacement(harness.Document.Id, out _));

        Assert.True(harness.Host.ShowDockable(harness.Document.Id));
        harness.Frame();
        harness.Frame();

        var reopened = harness.Host.FindPanel(harness.Document.Id);
        Assert.NotNull(reopened);
        AssertPanelInGroupVisual(harness, reopened, harness.DocumentGroup);
        Assert.Equal(originalJson, Json(harness));
    }

    [Fact]
    public void ClosedByClosingTheFloatingWindow_ViaCloseFloatingWindow_ReopensAtItsOriginalDockedPlace()
    {
        Harness harness = CreateHarness();
        string originalJson = Json(harness);

        MGFloatingDockWindow floatingWindow = Float(harness, harness.Document, new Point(1200, 650));

        harness.Host.CloseFloatingWindow(floatingWindow);
        harness.Frame();
        harness.Frame();

        Assert.Empty(harness.Host.FloatingWindows);
        Assert.True(harness.Host.LayoutModel.TryGetPlacement(harness.Document.Id, out _));

        Assert.True(harness.Host.ShowDockable(harness.Document.Id));
        harness.Frame();
        harness.Frame();

        var reopened = harness.Host.FindPanel(harness.Document.Id);
        Assert.NotNull(reopened);
        AssertPanelInGroupVisual(harness, reopened, harness.DocumentGroup);
        Assert.Equal(originalJson, Json(harness));
    }

    // ── ShowDockable on an already-floating panel: activate, no duplicate ────

    [Fact]
    public void ShowDockable_OnFloatingPanel_ActivatesItWithoutDuplicate()
    {
        Harness harness = CreateHarness();
        string originalJson = Json(harness);

        MGFloatingDockWindow floatingWindow = Float(harness, harness.Document, new Point(1200, 650));

        bool result = harness.Host.ShowDockable(harness.Document.Id);
        harness.Frame();
        harness.Frame();

        Assert.True(result);
        AssertUniquePanelIds(harness);
        Assert.Contains(floatingWindow, harness.Host.FloatingWindows);
        Assert.Equal(harness.Document.Id, floatingWindow.GroupNode.ActivePanel?.Id);
        Assert.True(harness.Host.LayoutModel.TryGetPlacement(harness.Document.Id, out _));

        Dock(harness, harness.Document, floatingWindow);

        Assert.Empty(harness.Host.FloatingWindows);
        Assert.Equal(originalJson, Json(harness));
    }

    [Fact]
    public void ShowDockable_OnFloatingPanel_WithMultipleTabs_ActivatesTheRequestedTab()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = Float(harness, harness.Document, new Point(1200, 650));

        // Give the floating group a second tab directly at the model level (equivalent to
        // dragging Properties onto the floating window's own tab strip), then make the other tab
        // (Properties) the active one, so ShowDockable(Document) has something to switch away from.
        harness.PropertiesGroup.RemovePanel(harness.Properties);
        floatingWindow.GroupNode.AddPanel(harness.Properties, -1);
        floatingWindow.GroupNode.SetActivePanel(harness.Properties.Id);
        harness.Frame();
        harness.Frame();

        Assert.Equal(harness.Properties.Id, floatingWindow.GroupNode.ActivePanel?.Id);

        bool result = harness.Host.ShowDockable(harness.Document.Id);
        harness.Frame();
        harness.Frame();

        Assert.True(result);
        Assert.Equal(harness.Document.Id, floatingWindow.GroupNode.ActivePanel?.Id);
    }

    // ── a panel with no definition in the registry, closed: no placeholder is left ──

    [Fact]
    public void ClosingAPanelWithNoRegistryDefinition_LeavesNoPlaceholder_AndCollapsesLikeAPlainClose()
    {
        Harness withUnknownId = CreateHarness();
        withUnknownId.Registry.Unregister(withUnknownId.Diagnostics.Id);
        DockNode topSplitOfUnknown = withUnknownId.XamlGroup.Parent;
        DockSplitNode rootOfUnknown = Assert.IsType<DockSplitNode>(withUnknownId.Host.LayoutModel.RootNode);

        Harness withNoRegistry = CreateHarness();
        withNoRegistry.Host.DockableRegistry = null;
        DockNode topSplitOfPlain = withNoRegistry.XamlGroup.Parent;
        DockSplitNode rootOfPlain = Assert.IsType<DockSplitNode>(withNoRegistry.Host.LayoutModel.RootNode);

        Assert.True(withUnknownId.Host.RemovePanel(withUnknownId.Diagnostics.Id));
        withUnknownId.Frame();
        withUnknownId.Frame();

        Assert.True(withNoRegistry.Host.RemovePanel(withNoRegistry.Diagnostics.Id));
        withNoRegistry.Frame();
        withNoRegistry.Frame();

        // No placement is kept, so the placeholder is unreferenced and collapses immediately.
        Assert.False(withUnknownId.Host.LayoutModel.TryGetPlacement(withUnknownId.Diagnostics.Id, out _));
        Assert.DoesNotContain(withUnknownId.DiagnosticsGroup, withUnknownId.Host.LayoutModel.GetAllTabGroups());
        Assert.Same(topSplitOfUnknown, rootOfUnknown.FirstChild);

        // Same collapse when there is no DockableRegistry at all (today's behaviour, unchanged).
        Assert.DoesNotContain(withNoRegistry.DiagnosticsGroup, withNoRegistry.Host.LayoutModel.GetAllTabGroups());
        Assert.Same(topSplitOfPlain, rootOfPlain.FirstChild);
    }

    // ── event counts: exactly one PanelRemoved/NotifyClosed on close, one PanelAdded/OnShown on reopen ──

    [Fact]
    public void ClosingAndReopeningAPanel_RaisesLifecycleEventsExactlyOnce()
    {
        Harness harness = CreateHarness();
        List<DockPanelNode> removed = new();
        List<DockPanelNode> added = new();
        List<DockableDefinition> closedNotifications = new();
        List<DockableDefinition> shownNotifications = new();
        harness.Host.PanelRemoved += (_, panel) => removed.Add(panel);
        harness.Host.PanelAdded += (_, panel) => added.Add(panel);
        harness.Registry.OnClosed += (_, def) => closedNotifications.Add(def);
        harness.Registry.OnShown += (_, def) => shownNotifications.Add(def);

        Assert.True(harness.Host.RemovePanel(harness.Properties.Id));
        harness.Frame();
        harness.Frame();

        Assert.Single(removed);
        Assert.Equal("properties", removed[0].Id);
        Assert.Single(closedNotifications);
        Assert.Equal("properties", closedNotifications[0].DockableId);
        Assert.Empty(added);
        Assert.Empty(shownNotifications);

        Assert.True(harness.Host.ShowDockable("properties"));
        harness.Frame();
        harness.Frame();

        Assert.Single(added);
        Assert.Equal("properties", added[0].Id);
        Assert.Single(shownNotifications);
        Assert.Equal("properties", shownNotifications[0].DockableId);
        Assert.Single(removed);
        Assert.Single(closedNotifications);
    }

    // ── A8: display check across every path T4 touches ──────────────────────

    [Fact]
    public void A8_CloseDockedTab_FromTabContextMenu_Menu()
    {
        Harness harness = CreateHarness();
        MGDockTabItem tab = GetSoleDockedTabItem(harness, harness.DocumentGroup);
        List<DockPanelNode> removed = new();
        harness.Host.PanelRemoved += (_, panel) => removed.Add(panel);

        OpenContextMenu(harness, tab);
        MGContextMenuButton closeButton = FindMenuButton(harness.Desktop.ActiveContextMenu, "Close");
        Assert.NotNull(closeButton);
        closeButton.Action.Invoke(closeButton);
        harness.Frame();
        harness.Frame();

        Assert.Equal(new[] { harness.Document }, removed);
        AssertPanelNotInAnyHostVisual(harness, harness.Document);
        AssertA8(harness);
    }

    [Fact]
    public void A8_CloseOthers_FromTabContextMenu_Menu()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = Float(harness, harness.Properties, new Point(1200, 650));
        DockOntoGroupCenter(harness, harness.Properties, floatingWindow, harness.DocumentGroup);

        MGDockTabItem keepTab = GetDockedTabItemForPanel(harness, harness.DocumentGroup, harness.Document);
        List<DockPanelNode> removed = new();
        harness.Host.PanelRemoved += (_, panel) => removed.Add(panel);

        OpenContextMenu(harness, keepTab);
        MGContextMenuButton closeOthersButton = FindMenuButton(harness.Desktop.ActiveContextMenu, "Close Others");
        Assert.NotNull(closeOthersButton);
        closeOthersButton.Action.Invoke(closeOthersButton);
        harness.Frame();
        harness.Frame();

        Assert.Equal(new[] { harness.Properties }, removed);
        AssertPanelNotInAnyHostVisual(harness, harness.Properties);
        AssertPanelInGroupVisual(harness, harness.Document, harness.DocumentGroup);
        AssertA8(harness);
    }

    [Fact]
    public void A8_CloseAll_FromTabContextMenu_Menu()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = Float(harness, harness.Properties, new Point(1200, 650));
        DockOntoGroupCenter(harness, harness.Properties, floatingWindow, harness.DocumentGroup);

        MGDockTabItem tab = GetDockedTabItemForPanel(harness, harness.DocumentGroup, harness.Document);
        List<DockPanelNode> removed = new();
        harness.Host.PanelRemoved += (_, panel) => removed.Add(panel);

        OpenContextMenu(harness, tab);
        MGContextMenuButton closeAllButton = FindMenuButton(harness.Desktop.ActiveContextMenu, "Close All");
        Assert.NotNull(closeAllButton);
        closeAllButton.Action.Invoke(closeAllButton);
        harness.Frame();
        harness.Frame();

        Assert.Equal(2, removed.Count);
        Assert.Contains(harness.Document, removed);
        Assert.Contains(harness.Properties, removed);
        AssertPanelNotInAnyHostVisual(harness, harness.Document);
        AssertPanelNotInAnyHostVisual(harness, harness.Properties);
        AssertA8(harness);
    }

    [Fact]
    public void A8_RemovePanel_ByPublicApi()
    {
        Harness harness = CreateHarness();
        List<DockPanelNode> removed = new();
        harness.Host.PanelRemoved += (_, panel) => removed.Add(panel);

        Assert.True(harness.Host.RemovePanel(harness.Document.Id));
        harness.Frame();
        harness.Frame();

        Assert.Equal(new[] { harness.Document }, removed);
        AssertPanelNotInAnyHostVisual(harness, harness.Document);
        AssertA8(harness);
    }

    [Fact]
    public void A8_ShowDockable_ReopensIntoAPlaceholder()
    {
        Harness harness = CreateHarness();
        Assert.True(harness.Host.RemovePanel(harness.Document.Id));
        harness.Frame();
        harness.Frame();

        Assert.True(harness.Host.ShowDockable(harness.Document.Id));
        harness.Frame();
        harness.Frame();

        var reopened = harness.Host.FindPanel(harness.Document.Id);
        Assert.NotNull(reopened);
        AssertPanelInGroupVisual(harness, reopened, harness.DocumentGroup);
        AssertA8(harness);
    }

    [Fact]
    public void A8_ShowDockable_FallsBackToFirstVisibleGroup_WhenPlacementIsGone()
    {
        Harness harness = CreateHarness();
        Assert.True(harness.Host.RemovePanel(harness.Document.Id));
        harness.Frame();
        harness.Frame();

        // The application replaces the root entirely while the remembered placement still points
        // at the old tree (P5): the placement no longer resolves, so ShowDockable falls back.
        DockPanelNode freshPanel = new() { Title = "Fresh", ContentFactory = () => new MGBorder(harness.MainWindow) };
        DockTabGroupNode freshGroup = new();
        freshGroup.AddPanel(freshPanel, -1);
        harness.Host.LayoutModel.RootNode = freshGroup;
        harness.Frame();
        harness.Frame();

        Assert.True(harness.Host.ShowDockable(harness.Document.Id));
        harness.Frame();
        harness.Frame();

        var reopened = harness.Host.FindPanel(harness.Document.Id);
        Assert.NotNull(reopened);
        AssertPanelInGroupVisual(harness, reopened, freshGroup);
        AssertA8(harness);

        // The stale placement — pointing at a group no longer in the tree, while the panel is
        // now anchored elsewhere — must not survive the fallback (P5), or a later save/load would
        // see both an anchored panel and a placement for it (P8's own rejection rule).
        Assert.False(harness.Host.LayoutModel.TryGetPlacement(harness.Document.Id, out _));
    }

    /// <summary>Drags <paramref name="panel"/>'s tab from its floating window onto the center of
    /// <paramref name="targetGroup"/>'s visual, docking it there as a second tab (used to build a
    /// multi-tab group for the "Close Others" / "Close All" checks).</summary>
    private static void DockOntoGroupCenter(Harness h, DockPanelNode panel, MGFloatingDockWindow floatingWindow, DockTabGroupNode targetGroup)
    {
        MGDockTabItem tab = GetSoleFloatingTabItem(floatingWindow, panel);
        var targetVisual = Assert.Single(h.Host.GetAllVisibleTabGroups(), g => g.GroupNode == targetGroup);
        Point pressPoint = tab.LayoutBounds.Center;
        Point overTarget = targetVisual.LayoutBounds.Center;
        Point midway = new((pressPoint.X + overTarget.X) / 2, (pressPoint.Y + overTarget.Y) / 2);

        h.Frame(pressPoint, MouseButton.Left);
        h.Frame(midway, MouseButton.Left);
        h.Frame(overTarget, MouseButton.Left);
        h.Frame(overTarget, MouseButton.Left);
        h.Frame(overTarget);
        h.Frame(overTarget);
    }

    private static MGDockTabItem GetDockedTabItemForPanel(Harness h, DockTabGroupNode group, DockPanelNode panel)
    {
        var visual = Assert.Single(h.Host.GetAllVisibleTabGroups(), g => g.GroupNode == group);
        return visual.TraverseVisualTree<MGDockTabItem>(IncludeSelf: false).Single(t => t.Panel == panel);
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static MGDockTabItem GetSoleDockedTabItem(Harness h, DockTabGroupNode group)
    {
        var visual = Assert.Single(h.Host.GetAllVisibleTabGroups(), g => g.GroupNode == group);
        return Assert.Single(visual.TraverseVisualTree<MGDockTabItem>(IncludeSelf: false));
    }

    private static MGDockTabItem GetSoleFloatingTabItem(MGFloatingDockWindow floatingWindow, DockPanelNode panel)
        => floatingWindow.TabGroup.TraverseVisualTree<MGDockTabItem>(IncludeSelf: false).Single(t => t.Panel == panel);

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
