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
/// Task T5 of <c>Docs/Tasks/docking-ghost-groups-tasks.md</c>: format 2.0 persists the whole
/// layout (tree with placeholder groups, floating windows with their bounds, auto-hidden panels
/// and remembered placements, D1/D6), and a load that fails (old version, malformed JSON,
/// inconsistent content) reports the reason without throwing and leaves the host exactly as it
/// was (D7/D8). Layout reproduced from <c>MGUI.Editor/XamlEditorView.cs:126-166</c> (read-only
/// reference, not built here), as in <c>DockPlaceholderGroupHostTests</c>.
/// </summary>
public class DockLayoutPersistenceTests
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
        public DockTabGroupNode DocumentGroup;
        public DockTabGroupNode PropertiesGroup;

        private int _elapsedMs;

        public void Frame()
        {
            AdvanceFrame(Runtime, Desktop, _elapsedMs, Point.Zero);
            _elapsedMs += 16;
        }
    }

    /// <summary>Same default layout as <c>DockPlaceholderGroupHostTests.CreateHarness</c>.</summary>
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

        DockSplitNode topSplit = new() { Orientation = Orientation.Horizontal, FirstChild = xamlGroup, SecondChild = previewGroup, SplitRatio = 0.45f };
        DockSplitNode leftBlock = new() { Orientation = Orientation.Vertical, FirstChild = topSplit, SecondChild = diagnosticsGroup, SplitRatio = 0.78f };
        DockSplitNode rightColumn = new() { Orientation = Orientation.Vertical, FirstChild = documentGroup, SecondChild = propertiesGroup, SplitRatio = 0.45f };
        DockSplitNode root = new() { Orientation = Orientation.Horizontal, FirstChild = leftBlock, SecondChild = rightColumn, SplitRatio = 0.72f };

        MGDockHost host = new(mainWindow)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LayoutModel = new DockLayoutModel(root),
        };
        host.DockableRegistry = registry;

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
            DocumentGroup = documentGroup,
            PropertiesGroup = propertiesGroup,
        };
        harness.Frame();
        harness.Frame();
        return harness;
    }

    /// <summary>Factory that recreates the five known panels' content by id; unknown ids get null.</summary>
    private static Func<string, Func<MGElement>> PanelFactory(MGWindow window)
        => id => id switch
        {
            "xaml" or "preview" or "document" or "properties" or "diagnostics" => () => new MGBorder(window),
            _ => null,
        };

    /// <summary>Registers the same five <see cref="DockableDefinition"/>s (id and title) as
    /// <see cref="CreateHarness"/>'s registry, so a JSON round trip reproduces identical titles.</summary>
    private static void RegisterKnownDockables(DockableRegistry registry, MGWindow window)
    {
        var titles = new (string Id, string Title)[]
        {
            ("xaml", "XAML"), ("preview", "Preview"), ("document", "Document"),
            ("properties", "Properties"), ("diagnostics", "Diagnostics"),
        };
        foreach (var (id, title) in titles)
        {
            registry.Register(new DockableDefinition(id, title) { ContentFactory = () => new MGBorder(window) });
        }
    }

    private static string Json(MGDockHost host) => DockLayoutSerializer.ToJson(host.LayoutModel);

    /// <summary>A8 (common display check, P12).</summary>
    private static void AssertA8(MGDockHost host)
    {
        foreach (var visual in host.GetAllVisibleTabGroups())
        {
            Assert.False(visual.GroupNode.IsHiddenInLayout,
                $"MGDockTabGroup visual is bound to hidden group {visual.GroupNode.Id}.");
        }

        foreach (var splitVisual in host.TraverseVisualTree<MGDockSplitContainer>(IncludeSelf: false))
        {
            var first = splitVisual.ModelNode?.FirstChild;
            var second = splitVisual.ModelNode?.SecondChild;
            var firstHidden = first == null || first.IsHiddenInLayout;
            var secondHidden = second == null || second.IsHiddenInLayout;
            Assert.False(firstHidden || secondHidden,
                "MGDockSplitContainer was built for a split with a hidden child.");
        }
    }

    /// <summary>Uniqueness invariant (T4/T5): each panel id appears at most once across the
    /// layout tree, the floating store and the auto-hide store.</summary>
    private static void AssertUniquePanelIds(MGDockHost host)
    {
        var ids = new List<string>();
        ids.AddRange(CollectTreeIds(host.LayoutModel.RootNode));
        ids.AddRange(host.LayoutModel.FloatingGroups.SelectMany(g => g.Group.Panels.Select(p => p.Id)));
        ids.AddRange(host.LayoutModel.GetAllAutoHidePanels().Select(p => p.Id));

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

    // ── A5: float + auto-hide + close, save, load into a fresh host, restore via Dock/Pin/ShowDockable ──

    [Fact]
    public void A5_SaveThenLoadIntoAFreshHost_ThenDockPinShowDockable_RestoresOriginalJson()
    {
        Harness source = CreateHarness();
        string originalJson = Json(source.Host);

        MGFloatingDockWindow floatingWindow = source.Host.DetachToFloating(source.Document, new Point(1200, 650));
        source.Frame();
        source.Frame();
        // Known bounds, away from (0,0) so a bounds round trip is meaningful.
        floatingWindow.Left = 300;
        floatingWindow.Top = 150;
        floatingWindow.WindowWidth = 340;
        floatingWindow.WindowHeight = 280;
        source.Frame();
        source.Frame();

        source.Host.UnpinPanel(source.Properties);
        source.Frame();
        source.Frame();

        Assert.True(source.Host.RemovePanel(source.Diagnostics.Id));
        source.Frame();
        source.Frame();

        string savedJson = source.Host.SaveLayoutToJson(indented: true);

        // Fresh host, fresh registry, fresh desktop.
        GraphTestRuntime runtime2 = new(new Rectangle(0, 0, 1280, 720));
        MGDesktop desktop2 = new(runtime2);
        MGWindow mainWindow2 = new(desktop2, 0, 0, 1280, 720) { WindowStyle = WindowStyle.None };
        DockableRegistry registry2 = new();
        RegisterKnownDockables(registry2, mainWindow2);

        MGDockHost freshHost = new(mainWindow2)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        freshHost.DockableRegistry = registry2;
        mainWindow2.SetContent(freshHost);
        desktop2.Windows.Add(mainWindow2);
        AdvanceFrame(runtime2, desktop2, 0, Point.Zero);
        AdvanceFrame(runtime2, desktop2, 16, Point.Zero);

        bool loaded = freshHost.TryLoadLayoutFromJson(savedJson, PanelFactory(mainWindow2), out var diagnostics);
        AdvanceFrame(runtime2, desktop2, 32, Point.Zero);
        AdvanceFrame(runtime2, desktop2, 48, Point.Zero);

        Assert.True(loaded);
        Assert.Empty(diagnostics);

        // Floating window recreated with its saved bounds.
        MGFloatingDockWindow reloadedFloat = Assert.Single(freshHost.FloatingWindows);
        Assert.Equal(300, reloadedFloat.Left);
        Assert.Equal(150, reloadedFloat.Top);
        Assert.Equal(340, reloadedFloat.WindowWidth);
        Assert.Equal(280, reloadedFloat.WindowHeight);
        Assert.Contains("document", reloadedFloat.GroupNode.Panels.Select(p => p.Id));

        // Auto-hide strip shows the hidden pane.
        Assert.True(freshHost.LayoutModel.GetAllAutoHidePanels().Any(p => p.Id == "properties"));

        // Closed pane is absent from the tree.
        Assert.DoesNotContain(freshHost.GetAllVisibleTabGroups(), g => g.GroupNode.Panels.Any(p => p.Id == "diagnostics"));

        AssertA8(freshHost);
        AssertUniquePanelIds(freshHost);

        // "Dock": redock the floated Document panel.
        DockPanelNode reloadedDocument = reloadedFloat.GroupNode.Panels.Single(p => p.Id == "document");
        freshHost.RedockPanel(reloadedDocument, reloadedFloat);
        AdvanceFrame(runtime2, desktop2, 64, Point.Zero);
        AdvanceFrame(runtime2, desktop2, 80, Point.Zero);
        AssertA8(freshHost);
        AssertUniquePanelIds(freshHost);

        // "Pin": re-pin the auto-hidden Properties panel.
        DockPanelNode reloadedProperties = freshHost.FindPanel("properties");
        Assert.NotNull(reloadedProperties);
        freshHost.RepinPanel(reloadedProperties);
        AdvanceFrame(runtime2, desktop2, 96, Point.Zero);
        AdvanceFrame(runtime2, desktop2, 112, Point.Zero);
        AssertA8(freshHost);
        AssertUniquePanelIds(freshHost);

        // ShowDockable: reopen the closed Diagnostics panel.
        Assert.True(freshHost.ShowDockable("diagnostics"));
        AdvanceFrame(runtime2, desktop2, 128, Point.Zero);
        AdvanceFrame(runtime2, desktop2, 144, Point.Zero);
        AssertA8(freshHost);
        AssertUniquePanelIds(freshHost);

        Assert.Empty(freshHost.FloatingWindows);
        Assert.Empty(freshHost.LayoutModel.GetAllAutoHidePanels());
        Assert.Equal(originalJson, Json(freshHost));
    }

    // ── A6: an old-version and a malformed document both fail cleanly ────────

    [Fact]
    public void A6_TryLoadLayoutFromJson_OldVersionDocument_ReturnsFalse_HostUnchanged()
    {
        Harness harness = CreateHarness();
        string jsonBefore = Json(harness.Host);
        string oldVersionJson = "{\"version\":\"1.0\",\"rootNode\":{\"type\":\"TabGroup\",\"id\":\"g1\",\"panels\":[]}}";

        bool result = harness.Host.TryLoadLayoutFromJson(oldVersionJson, PanelFactory(harness.MainWindow), out var diagnostics);

        Assert.False(result);
        Assert.NotEmpty(diagnostics);
        Assert.Equal(jsonBefore, Json(harness.Host));
        Assert.Empty(harness.Host.FloatingWindows);
    }

    [Fact]
    public void A6_TryLoadLayoutFromJson_MalformedDocument_ReturnsFalse_HostUnchanged()
    {
        Harness harness = CreateHarness();
        string jsonBefore = Json(harness.Host);

        bool result = harness.Host.TryLoadLayoutFromJson("{ not valid json", PanelFactory(harness.MainWindow), out var diagnostics);

        Assert.False(result);
        Assert.NotEmpty(diagnostics);
        Assert.Equal(jsonBefore, Json(harness.Host));
        Assert.Empty(harness.Host.FloatingWindows);
    }

    [Fact]
    public void A6_TryLoadLayoutFromJson_OldVersionDocument_WithAnOpenFloatingWindow_LeavesItUntouched()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = harness.Host.DetachToFloating(harness.Document, new Point(1200, 650));
        harness.Frame();
        harness.Frame();
        string jsonBefore = Json(harness.Host);

        bool result = harness.Host.TryLoadLayoutFromJson(
            "{\"version\":\"1.0\",\"rootNode\":{\"type\":\"TabGroup\",\"id\":\"g1\",\"panels\":[]}}",
            PanelFactory(harness.MainWindow), out var diagnostics);

        Assert.False(result);
        Assert.NotEmpty(diagnostics);
        Assert.Same(floatingWindow, Assert.Single(harness.Host.FloatingWindows));
        Assert.Equal(jsonBefore, Json(harness.Host));
    }

    [Fact]
    public void LoadLayoutFromJson_OldVersionDocument_Throws()
    {
        Harness harness = CreateHarness();
        Assert.Throws<InvalidOperationException>(() =>
            harness.Host.LoadLayoutFromJson("{\"version\":\"1.0\",\"rootNode\":{\"type\":\"TabGroup\",\"id\":\"g1\",\"panels\":[]}}", PanelFactory(harness.MainWindow)));
    }

    [Fact]
    public void LoadLayoutFromJson_MalformedDocument_Throws()
    {
        Harness harness = CreateHarness();
        Assert.Throws<InvalidOperationException>(() =>
            harness.Host.LoadLayoutFromJson("{ not valid json", PanelFactory(harness.MainWindow)));
    }

    // ── floating bounds out of the screen are clamped on load ────────────────

    [Fact]
    public void TryLoadLayoutFromJson_FloatingBoundsOutOfScreen_AreClampedIntoValidScreenBounds()
    {
        Harness harness = CreateHarness();

        var offscreenPanel = new DockPanelNode("offscreen") { Title = "Offscreen" };
        var offscreenGroup = new DockTabGroupNode();
        offscreenGroup.AddPanel(offscreenPanel, -1);
        var offscreenModel = new DockLayoutModel(new DockTabGroupNode());
        offscreenModel.AddFloatingGroup(new DockFloatingGroup(offscreenGroup, 100000, -50000, 300, 200));
        string json = DockLayoutSerializer.ToJson(offscreenModel);

        bool loaded = harness.Host.TryLoadLayoutFromJson(json, id => id == "offscreen" ? (() => new MGBorder(harness.MainWindow)) : null, out var diagnostics);
        harness.Frame();
        harness.Frame();

        Assert.True(loaded);
        Assert.Empty(diagnostics);
        MGFloatingDockWindow window = Assert.Single(harness.Host.FloatingWindows);
        var screen = harness.Desktop.ValidScreenBounds;
        Assert.InRange(window.Left, screen.X, screen.Right);
        Assert.InRange(window.Top, screen.Y, screen.Bottom);
    }

    // ── a panel unknown to the factory is skipped on load without breaking the layout ──

    [Fact]
    public void TryLoadLayoutFromJson_PanelUnknownToTheFactory_IsSkipped_LayoutStaysValid()
    {
        Harness harness = CreateHarness();

        var known = new DockPanelNode("known") { Title = "Known" };
        var unknown = new DockPanelNode("unknown") { Title = "Unknown" };
        var group = new DockTabGroupNode();
        group.AddPanel(known, -1);
        group.AddPanel(unknown, -1);
        var model = new DockLayoutModel(group);
        string json = DockLayoutSerializer.ToJson(model);

        bool loaded = harness.Host.TryLoadLayoutFromJson(
            json,
            id => id == "known" ? (() => new MGBorder(harness.MainWindow)) : null,
            out var diagnostics);
        harness.Frame();
        harness.Frame();

        Assert.True(loaded);
        Assert.Empty(diagnostics);
        Assert.NotNull(harness.Host.FindPanel("known"));
        Assert.Null(harness.Host.FindPanel("unknown"));
        AssertA8(harness.Host);
    }

    // ── a maximized floating window keeps its pre-maximize bounds on save (P9) ──

    [Fact]
    public void SaveLayoutToJson_MaximizedFloatingWindow_KeepsItsPreMaximizeBounds()
    {
        Harness source = CreateHarness();
        MGFloatingDockWindow floatingWindow = source.Host.DetachToFloating(source.Document, new Point(1200, 650));
        source.Frame();
        source.Frame();
        floatingWindow.Left = 300;
        floatingWindow.Top = 150;
        floatingWindow.WindowWidth = 340;
        floatingWindow.WindowHeight = 280;
        source.Frame();
        source.Frame();

        // Maximize: bounds change on screen, but the pre-maximize bounds are what must be saved.
        floatingWindow.TabGroup.IsMaximized = true;
        floatingWindow.Left = 0;
        floatingWindow.Top = 0;
        floatingWindow.WindowWidth = 1280;
        floatingWindow.WindowHeight = 720;
        source.Frame();
        source.Frame();

        string json = source.Host.SaveLayoutToJson();
        var model = DockLayoutSerializer.FromJson(json, PanelFactory(source.MainWindow));

        DockFloatingGroup savedGroup = Assert.Single(model.FloatingGroups);
        Assert.Equal(300, savedGroup.Left);
        Assert.Equal(150, savedGroup.Top);
        Assert.Equal(340, savedGroup.Width);
        Assert.Equal(280, savedGroup.Height);
    }

    // ── SetLayoutModel then RegisterPanels keeps the editor pattern (P10, R3) ──

    [Fact]
    public void SetLayoutModelThenRegisterPanels_KeepsTheEditorPattern()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 1280, 720));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, 1280, 720) { WindowStyle = WindowStyle.None };

        DockableRegistry registry = new();
        var ids = new[] { "p1", "p2", "p3", "p4", "p5" };
        foreach (var id in ids)
        {
            registry.Register(new DockableDefinition(id, id) { ContentFactory = () => new MGBorder(mainWindow) });
        }

        var panels = ids.Select(id => new DockPanelNode(id) { Title = id }).ToArray();
        var group1 = new DockTabGroupNode();
        group1.AddPanel(panels[0], -1);
        group1.AddPanel(panels[1], -1);
        var group2 = new DockTabGroupNode();
        group2.AddPanel(panels[2], -1);
        var group3 = new DockTabGroupNode();
        group3.AddPanel(panels[3], -1);
        group3.AddPanel(panels[4], -1);
        var split = new DockSplitNode { Orientation = Orientation.Horizontal, FirstChild = group1, SecondChild = new DockSplitNode { Orientation = Orientation.Vertical, FirstChild = group2, SecondChild = group3 } };

        MGDockHost host = new(mainWindow)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        host.DockableRegistry = registry;

        var added = new List<DockPanelNode>();
        var shown = new List<DockableDefinition>();
        host.PanelAdded += (_, p) => added.Add(p);
        registry.OnShown += (_, d) => shown.Add(d);

        // The pattern: assign LayoutModel first, then RegisterPanel each panel already in the tree.
        host.LayoutModel = new DockLayoutModel(split);
        var exception = Record.Exception(() =>
        {
            foreach (var panel in panels)
            {
                host.RegisterPanel(panel);
            }
        });

        Assert.Null(exception);
        Assert.Equal(5, added.Count);
        Assert.Equal(5, shown.Count);
        foreach (var id in ids)
        {
            Assert.NotNull(host.FindPanel(id));
        }
    }

    // ── after TryLoadLayoutFromJson, FindPanel resolves loaded panels and ShowDockable is safe ──

    [Fact]
    public void AfterTryLoadLayoutFromJson_FindPanelResolvesLoadedPanels_AndShowDockableCreatesNoDuplicate()
    {
        Harness source = CreateHarness();
        source.Host.UnpinPanel(source.Properties);
        source.Frame();
        source.Frame();
        string json = source.Host.SaveLayoutToJson();

        GraphTestRuntime runtime2 = new(new Rectangle(0, 0, 1280, 720));
        MGDesktop desktop2 = new(runtime2);
        MGWindow mainWindow2 = new(desktop2, 0, 0, 1280, 720) { WindowStyle = WindowStyle.None };
        DockableRegistry registry2 = new();
        RegisterKnownDockables(registry2, mainWindow2);

        MGDockHost freshHost = new(mainWindow2)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        freshHost.DockableRegistry = registry2;
        mainWindow2.SetContent(freshHost);
        desktop2.Windows.Add(mainWindow2);
        AdvanceFrame(runtime2, desktop2, 0, Point.Zero);
        AdvanceFrame(runtime2, desktop2, 16, Point.Zero);

        bool loaded = freshHost.TryLoadLayoutFromJson(json, PanelFactory(mainWindow2), out var diagnostics);
        AdvanceFrame(runtime2, desktop2, 32, Point.Zero);
        AdvanceFrame(runtime2, desktop2, 48, Point.Zero);
        Assert.True(loaded);
        Assert.Empty(diagnostics);

        Assert.NotNull(freshHost.FindPanel("xaml"));
        Assert.NotNull(freshHost.FindPanel("preview"));
        Assert.NotNull(freshHost.FindPanel("document"));
        Assert.NotNull(freshHost.FindPanel("properties")); // auto-hidden, still registered
        Assert.NotNull(freshHost.FindPanel("diagnostics"));

        Assert.True(freshHost.ShowDockable("properties"));
        AdvanceFrame(runtime2, desktop2, 64, Point.Zero);
        AdvanceFrame(runtime2, desktop2, 80, Point.Zero);
        AssertUniquePanelIds(freshHost);
    }

    // ── a JSON with a duplicate panel id, or a placement whose panel is docked, is refused ──

    [Fact]
    public void TryLoadLayoutFromJson_DuplicatePanelId_IsRefused_WithDiagnostic()
    {
        Harness harness = CreateHarness();
        string jsonBefore = Json(harness.Host);
        string json = """
        {
          "version": "2.0",
          "rootNode": { "type": "TabGroup", "id": "g1", "panels": [ { "id": "dup", "title": "A" } ] },
          "floatingGroups": [
            { "group": { "type": "TabGroup", "id": "fg1", "panels": [ { "id": "dup", "title": "A2" } ] }, "left": 0, "top": 0, "width": 100, "height": 100 }
          ],
          "autoHide": [],
          "placements": []
        }
        """;

        bool loaded = harness.Host.TryLoadLayoutFromJson(json, id => () => new MGBorder(harness.MainWindow), out var diagnostics);

        Assert.False(loaded);
        Assert.NotEmpty(diagnostics);
        Assert.Equal(jsonBefore, Json(harness.Host));
    }

    [Fact]
    public void TryLoadLayoutFromJson_PlacementForADockedPanel_IsRefused_WithDiagnostic()
    {
        Harness harness = CreateHarness();
        string jsonBefore = Json(harness.Host);
        string json = """
        {
          "version": "2.0",
          "rootNode": { "type": "TabGroup", "id": "g1", "panels": [ { "id": "docked", "title": "A" } ] },
          "floatingGroups": [],
          "autoHide": [],
          "placements": [ { "panelId": "docked", "groupId": "g1", "tabIndex": 0 } ]
        }
        """;

        bool loaded = harness.Host.TryLoadLayoutFromJson(json, id => () => new MGBorder(harness.MainWindow), out var diagnostics);

        Assert.False(loaded);
        Assert.NotEmpty(diagnostics);
        Assert.Equal(jsonBefore, Json(harness.Host));
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
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
}
