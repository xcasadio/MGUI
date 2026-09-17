using MGUI.Core.UI;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Tests.Docking;

/// <summary>
/// Task 17.2 — Unit tests for DockOperation:
///   DockAsTab, ReorderTab, SplitDock, RemovePanel, SplitDockAtRoot.
/// </summary>
public class DockOperationTests
{
    // ── Helpers ──────────────────────────────────────────────────────────
    private static DockPanelNode Panel(string? title = null)
    {
        var p = new DockPanelNode { Title = title ?? "Panel" };
        return p;
    }

    private static DockTabGroupNode Group(params DockPanelNode[] panels)
    {
        var g = new DockTabGroupNode();
        foreach (var p in panels)
        {
            g.AddPanel(p, -1);
        }

        return g;
    }

    /// Creates a model with a single tab group as root.
    private static (DockLayoutModel model, DockTabGroupNode group) ModelWithGroup(params DockPanelNode[] panels)
    {
        var g = Group(panels);
        return (new DockLayoutModel(g), g);
    }

    // ── DockAsTab ─────────────────────────────────────────────────────────

    [Fact]
    public void DockAsTab_AddsPanel_ToEmptyGroup()
    {
        var p = Panel("P");
        var g = new DockTabGroupNode();
        var model = new DockLayoutModel(g);

        DockOperation.DockAsTab(model, p, g);

        Assert.Single(g.Panels);
        Assert.Same(p, g.Panels[0]);
    }

    [Fact]
    public void DockAsTab_SetsPanelParent()
    {
        var p = Panel();
        var g = new DockTabGroupNode();
        var model = new DockLayoutModel(g);

        DockOperation.DockAsTab(model, p, g);

        Assert.Same(g, p.Parent);
    }

    [Fact]
    public void DockAsTab_SetsActivePanel()
    {
        var p = Panel();
        var g = new DockTabGroupNode();
        var model = new DockLayoutModel(g);

        DockOperation.DockAsTab(model, p, g);

        Assert.Equal(p.Id, g.ActivePanelId);
    }

    [Fact]
    public void DockAsTab_InsertsAtSpecifiedIndex()
    {
        var pA = Panel("A");
        var pB = Panel("B");
        var pNew = Panel("New");
        var g = Group(pA, pB);
        var model = new DockLayoutModel(g);

        DockOperation.DockAsTab(model, pNew, g, 1); // insert between A and B

        Assert.Equal(3, g.Panels.Count);
        Assert.Same(pA,  g.Panels[0]);
        Assert.Same(pNew,g.Panels[1]);
        Assert.Same(pB,  g.Panels[2]);
    }

    [Fact]
    public void DockAsTab_MovesPanel_FromSourceGroup_CleansUpEmptySource()
    {
        var p = Panel();
        var source = Group(p);
        var target = Group(Panel("Other"));
        var split  = new DockSplitNode { Orientation = Orientation.Horizontal, SplitRatio = 0.5f, FirstChild = source, SecondChild = target };
        var model  = new DockLayoutModel(split);

        DockOperation.DockAsTab(model, p, target);

        // Panel now in target
        Assert.Contains(p, target.Panels);
        // Source was emptied → split should be collapsed, root = target
        Assert.Same(target, model.RootNode);
    }

    [Fact]
    public void DockAsTab_ThrowsOnNullModel()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DockOperation.DockAsTab(null!, Panel(), new DockTabGroupNode()));
    }

    [Fact]
    public void DockAsTab_ThrowsOnNullPanel()
    {
        var model = new DockLayoutModel();
        Assert.Throws<ArgumentNullException>(() =>
            DockOperation.DockAsTab(model, null!, new DockTabGroupNode()));
    }

    // ── ReorderTab ────────────────────────────────────────────────────────

    [Fact]
    public void ReorderTab_MovesPanel_ToNewIndex()
    {
        var pA = Panel("A");
        var pB = Panel("B");
        var pC = Panel("C");
        var g  = Group(pA, pB, pC);
        var model = new DockLayoutModel(g);

        DockOperation.ReorderTab(model, pA, g, 2); // move A to end

        Assert.Equal(3, g.Panels.Count);
        Assert.Same(pB, g.Panels[0]);
        Assert.Same(pC, g.Panels[1]);
        Assert.Same(pA, g.Panels[2]);
    }

    [Fact]
    public void ReorderTab_ThrowsWhenPanelNotInGroup()
    {
        var p      = Panel();
        var other  = new DockTabGroupNode(); // p is NOT in this group
        var model  = new DockLayoutModel(other);

        Assert.Throws<InvalidOperationException>(() =>
            DockOperation.ReorderTab(model, p, other, 0));
    }

    [Fact]
    public void ReorderTab_MoveToEnd_WithNegativeIndex()
    {
        var pA = Panel("A");
        var pB = Panel("B");
        var pC = Panel("C");
        var g  = Group(pA, pB, pC);
        var model = new DockLayoutModel(g);

        DockOperation.ReorderTab(model, pA, g, -1); // append at end

        Assert.Same(pA, g.Panels[^1]);
    }

    // ── SplitDock ─────────────────────────────────────────────────────────

    [Fact]
    public void SplitDock_Left_CreatesHorizontalSplit_PanelAsFirstChild()
    {
        var existing = Panel("Existing");
        var root     = Group(existing);
        var newPanel = Panel("New");
        var model    = new DockLayoutModel(root);

        DockOperation.SplitDock(model, newPanel, root, DockZone.Left);

        var split = Assert.IsType<DockSplitNode>(model.RootNode);
        Assert.Equal(Orientation.Horizontal, split.Orientation);
        var firstGroup = Assert.IsType<DockTabGroupNode>(split.FirstChild);
        Assert.Contains(newPanel, firstGroup.Panels);
    }

    [Fact]
    public void SplitDock_Right_CreatesHorizontalSplit_PanelAsSecondChild()
    {
        var root     = Group(Panel("Existing"));
        var newPanel = Panel("New");
        var model    = new DockLayoutModel(root);

        DockOperation.SplitDock(model, newPanel, root, DockZone.Right);

        var split = Assert.IsType<DockSplitNode>(model.RootNode);
        Assert.Equal(Orientation.Horizontal, split.Orientation);
        var secondGroup = Assert.IsType<DockTabGroupNode>(split.SecondChild);
        Assert.Contains(newPanel, secondGroup.Panels);
    }

    [Fact]
    public void SplitDock_Top_CreatesVerticalSplit_PanelAsFirstChild()
    {
        var root     = Group(Panel("Existing"));
        var newPanel = Panel("New");
        var model    = new DockLayoutModel(root);

        DockOperation.SplitDock(model, newPanel, root, DockZone.Top);

        var split = Assert.IsType<DockSplitNode>(model.RootNode);
        Assert.Equal(Orientation.Vertical, split.Orientation);
        var firstGroup = Assert.IsType<DockTabGroupNode>(split.FirstChild);
        Assert.Contains(newPanel, firstGroup.Panels);
    }

    [Fact]
    public void SplitDock_Bottom_CreatesVerticalSplit_PanelAsSecondChild()
    {
        var root     = Group(Panel("Existing"));
        var newPanel = Panel("New");
        var model    = new DockLayoutModel(root);

        DockOperation.SplitDock(model, newPanel, root, DockZone.Bottom);

        var split = Assert.IsType<DockSplitNode>(model.RootNode);
        Assert.Equal(Orientation.Vertical, split.Orientation);
        var secondGroup = Assert.IsType<DockTabGroupNode>(split.SecondChild);
        Assert.Contains(newPanel, secondGroup.Panels);
    }

    [Fact]
    public void SplitDock_ReplacesRoot_WhenTargetIsRoot()
    {
        var root     = Group(Panel("Root"));
        var newPanel = Panel("New");
        var model    = new DockLayoutModel(root);

        DockOperation.SplitDock(model, newPanel, root, DockZone.Right);

        Assert.IsType<DockSplitNode>(model.RootNode);
        Assert.Null(model.RootNode!.Parent);
    }

    [Fact]
    public void SplitDock_InsertsNestedSplit_WhenTargetIsChildNode()
    {
        // Layout: root[H] → left=G0, right=G1. We split right (G1) further.
        var g0 = Group(Panel("G0"));
        var g1 = Group(Panel("G1"));
        var rootSplit = new DockSplitNode { Orientation = Orientation.Horizontal, SplitRatio = 0.5f, FirstChild = g0, SecondChild = g1 };
        var model     = new DockLayoutModel(rootSplit);

        var newPanel = Panel("New");
        DockOperation.SplitDock(model, newPanel, g1, DockZone.Bottom);

        // Root split should still be horizontal with g0 on the left, and a new V-split on the right
        Assert.Same(rootSplit, model.RootNode);
        var innerSplit = Assert.IsType<DockSplitNode>(rootSplit.SecondChild);
        Assert.Equal(Orientation.Vertical, innerSplit.Orientation);
        // g1 stays as top, new panel group goes to bottom
        Assert.Same(g1, innerSplit.FirstChild);
        var newGroup = Assert.IsType<DockTabGroupNode>(innerSplit.SecondChild);
        Assert.Contains(newPanel, newGroup.Panels);
    }

    [Fact]
    public void SplitDock_Center_DelegatesToDockAsTab()
    {
        var p1 = Panel("P1");
        var g  = Group(p1);
        var p2 = Panel("P2");
        var model = new DockLayoutModel(g);

        DockOperation.SplitDock(model, p2, g, DockZone.Center);

        Assert.Equal(2, g.Panels.Count);
        Assert.Contains(p2, g.Panels);
    }

    // ── RemovePanel ───────────────────────────────────────────────────────

    [Fact]
    public void RemovePanel_RemovesPanelFromGroup()
    {
        var p1 = Panel("P1");
        var p2 = Panel("P2");
        var g  = Group(p1, p2);
        var model = new DockLayoutModel(g);

        DockOperation.RemovePanel(model, p1);

        Assert.DoesNotContain(p1, g.Panels);
        Assert.Contains(p2, g.Panels);
    }

    [Fact]
    public void RemovePanel_CollapsesEmptyGroup_LeavesOtherChildAsRoot()
    {
        var pLeft  = Panel("L");
        var pRight = Panel("R");
        var gLeft  = Group(pLeft);
        var gRight = Group(pRight);
        var split  = new DockSplitNode { Orientation = Orientation.Horizontal, SplitRatio = 0.5f, FirstChild = gLeft, SecondChild = gRight };
        var model  = new DockLayoutModel(split);

        DockOperation.RemovePanel(model, pLeft); // gLeft becomes empty → collapse

        Assert.Same(gRight, model.RootNode);
    }

    [Fact]
    public void RemovePanel_LastPanelInRoot_LeavesEmptyRootGroup()
    {
        var p  = Panel();
        var g  = Group(p);
        var model = new DockLayoutModel(g);

        DockOperation.RemovePanel(model, p);

        // Root group is preserved as an empty placeholder (not set to null)
        var root = Assert.IsType<DockTabGroupNode>(model.RootNode);
        Assert.True(root.IsEmpty);
    }

    // ── SplitDockAtRoot ───────────────────────────────────────────────────

    [Fact]
    public void SplitDockAtRoot_InsertsNewPanelAtLeft_ExistingRootBecomesSecondChild()
    {
        var existing = Panel("Existing");
        var g        = Group(existing);
        var model    = new DockLayoutModel(g);
        var newPanel = Panel("New");

        DockOperation.SplitDockAtRoot(model, newPanel, DockZone.Left, 0.25f);

        var split = Assert.IsType<DockSplitNode>(model.RootNode);
        Assert.Equal(Orientation.Horizontal, split.Orientation);
        var newGroup = Assert.IsType<DockTabGroupNode>(split.FirstChild);
        Assert.Contains(newPanel, newGroup.Panels);
        // Original root is now the second child
        Assert.Same(g, split.SecondChild);
    }

    [Fact]
    public void SplitDockAtRoot_Right_NewGroupIsSecondChild()
    {
        var g        = Group(Panel("E"));
        var model    = new DockLayoutModel(g);
        var newPanel = Panel("New");

        DockOperation.SplitDockAtRoot(model, newPanel, DockZone.Right, 0.25f);

        var split    = Assert.IsType<DockSplitNode>(model.RootNode);
        var newGroup = Assert.IsType<DockTabGroupNode>(split.SecondChild);
        Assert.Contains(newPanel, newGroup.Panels);
        Assert.Same(g, split.FirstChild);
    }

    [Fact]
    public void SplitDockAtRoot_ThrowsForNoneZone()
    {
        var model = new DockLayoutModel(Group(Panel()));
        Assert.Throws<ArgumentException>(() =>
            DockOperation.SplitDockAtRoot(model, Panel(), DockZone.None));
    }

    [Fact]
    public void SplitDockAtRoot_ThrowsForCenterZone()
    {
        var model = new DockLayoutModel(Group(Panel()));
        Assert.Throws<ArgumentException>(() =>
            DockOperation.SplitDockAtRoot(model, Panel(), DockZone.Center));
    }

    [Fact]
    public void SplitDockAtRoot_NullRoot_CreatesNewRootGroup()
    {
        var model    = new DockLayoutModel(); // empty
        var newPanel = Panel("New");

        // SplitDockAtRoot on an empty model should not throw and create a valid layout
        DockOperation.SplitDockAtRoot(model, newPanel, DockZone.Right);

        Assert.NotNull(model.RootNode);
        var panels = model.GetAllTabGroups().SelectMany(g => g.Panels).ToList();
        Assert.Contains(newPanel, panels);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Placeholder groups (Task T1)
    // ══════════════════════════════════════════════════════════════════════

    #region Helpers

    /// <summary>
    /// Reproduces the XAML editor's default layout (XamlEditorView.cs:126-166, branch `xaml-editor`):
    /// one panel per group (text/preview/tree/properties/diagnostics), topSplit (text|preview) 0.45,
    /// leftBlock (topSplit/diagnostics) 0.78, rightColumn (tree/properties) 0.45,
    /// root (leftBlock|rightColumn) 0.72. Every node has an explicit id so JSON can be compared
    /// across two separately built model instances. Floatable panes: text, tree, properties, diagnostics.
    /// </summary>
    private sealed class EditorFixture
    {
        public DockLayoutModel Model = null!;
        public DockPanelNode Text = null!;
        public DockPanelNode Preview = null!;
        public DockPanelNode Tree = null!;
        public DockPanelNode Properties = null!;
        public DockPanelNode Diagnostics = null!;
        public DockTabGroupNode TextGroup = null!;
        public DockTabGroupNode PreviewGroup = null!;
        public DockTabGroupNode TreeGroup = null!;
        public DockTabGroupNode PropertiesGroup = null!;
        public DockTabGroupNode DiagnosticsGroup = null!;
        public DockSplitNode TopSplit = null!;
        public DockSplitNode LeftBlock = null!;
        public DockSplitNode RightColumn = null!;
        public DockSplitNode Root = null!;

        /// <summary>The four panes that can float, keyed by panel id, in departure-order-test order.</summary>
        public DockPanelNode[] Floatable => new[] { Text, Tree, Properties, Diagnostics };

        /// <summary>All five panes, keyed by panel id, in auto-hide-test order.</summary>
        public DockPanelNode[] All => new[] { Text, Preview, Tree, Properties, Diagnostics };
    }

    private static EditorFixture EditorLayout()
    {
        var text = new DockPanelNode("text") { Title = "XAML" };
        var preview = new DockPanelNode("preview") { Title = "Preview" };
        var tree = new DockPanelNode("tree") { Title = "Document" };
        var properties = new DockPanelNode("properties") { Title = "Properties" };
        var diagnostics = new DockPanelNode("diagnostics") { Title = "Diagnostics" };

        var textGroup = new DockTabGroupNode("textGroup");
        textGroup.AddPanel(text, -1);
        var previewGroup = new DockTabGroupNode("previewGroup");
        previewGroup.AddPanel(preview, -1);
        var treeGroup = new DockTabGroupNode("treeGroup");
        treeGroup.AddPanel(tree, -1);
        var propertiesGroup = new DockTabGroupNode("propertiesGroup");
        propertiesGroup.AddPanel(properties, -1);
        var diagnosticsGroup = new DockTabGroupNode("diagnosticsGroup");
        diagnosticsGroup.AddPanel(diagnostics, -1);

        var topSplit = new DockSplitNode("topSplit")
        {
            Orientation = Orientation.Horizontal,
            FirstChild = textGroup,
            SecondChild = previewGroup,
            SplitRatio = 0.45f,
        };

        var leftBlock = new DockSplitNode("leftBlock")
        {
            Orientation = Orientation.Vertical,
            FirstChild = topSplit,
            SecondChild = diagnosticsGroup,
            SplitRatio = 0.78f,
        };

        var rightColumn = new DockSplitNode("rightColumn")
        {
            Orientation = Orientation.Vertical,
            FirstChild = treeGroup,
            SecondChild = propertiesGroup,
            SplitRatio = 0.45f,
        };

        var root = new DockSplitNode("root")
        {
            Orientation = Orientation.Horizontal,
            FirstChild = leftBlock,
            SecondChild = rightColumn,
            SplitRatio = 0.72f,
        };

        return new EditorFixture
        {
            Model = new DockLayoutModel(root),
            Text = text,
            Preview = preview,
            Tree = tree,
            Properties = properties,
            Diagnostics = diagnostics,
            TextGroup = textGroup,
            PreviewGroup = previewGroup,
            TreeGroup = treeGroup,
            PropertiesGroup = propertiesGroup,
            DiagnosticsGroup = diagnosticsGroup,
            TopSplit = topSplit,
            LeftBlock = leftBlock,
            RightColumn = rightColumn,
            Root = root,
        };
    }

    /// <summary>Generates every permutation of <paramref name="items"/>.</summary>
    private static IEnumerable<List<T>> Permutations<T>(IReadOnlyList<T> items)
    {
        if (items.Count == 0)
        {
            yield return new List<T>();
            yield break;
        }

        for (int i = 0; i < items.Count; i++)
        {
            var rest = new List<T>(items);
            rest.RemoveAt(i);
            foreach (var permutation in Permutations(rest))
            {
                permutation.Insert(0, items[i]);
                yield return permutation;
            }
        }
    }

    private static string Label<T>(IEnumerable<T> panels) where T : DockPanelNode
        => string.Join(",", panels.Select(p => p.Id));

    #endregion Helpers

    // ── FloatPanel / DetachFromFloatingGroup / RestoreToPlacement: single-panel round trip ──

    [Fact]
    public void FloatPanel_RecordsPlacement_KeepsEmptiedGroupAsHiddenPlaceholder()
    {
        var e = EditorLayout();

        var floatingGroup = DockOperation.FloatPanel(e.Model, e.Text, 10, 20, 300, 200);

        Assert.True(e.Model.ValidateTree());
        Assert.Single(e.Model.FloatingGroups);
        Assert.Same(floatingGroup, e.Model.FloatingGroups[0]);
        Assert.Same(e.Text, floatingGroup.Group.Panels.Single());

        Assert.True(e.Model.TryGetPlacement(e.Text.Id, out var placement));
        Assert.Equal(e.TextGroup.Id, placement.GroupId);
        Assert.Equal(0, placement.TabIndex);

        // Source group survives, empty and hidden, as a placeholder.
        Assert.Same(e.TextGroup, e.Model.FindNodeById(e.TextGroup.Id));
        Assert.True(e.TextGroup.IsEmpty);
        Assert.True(e.TextGroup.IsHiddenInLayout);
        Assert.Same(e.TextGroup, e.TopSplit.FirstChild);
    }

    [Fact]
    public void FloatPanel_DoesNotDoubleSubscribe_PanelAlreadyInModelTree()
    {
        var e = EditorLayout();
        var layoutChangedCount = 0;
        e.Model.LayoutChanged += (_, _) => layoutChangedCount++;

        // e.Text was already subscribed once when RootNode was set. Floating it moves it into a
        // brand-new group that AddFloatingGroup subscribes again: without de-duplication this
        // registers the handler a second time on the same panel instance.
        var floatingGroup = DockOperation.FloatPanel(e.Model, e.Text, 0, 0, 100, 100);

        layoutChangedCount = 0;

        // Trigger a single structural change (Parent -> null) directly on the floating group,
        // bypassing DetachFromFloatingGroup so RemoveFloatingGroup's own explicit LayoutChanged
        // invocation does not confound the count: this isolates exactly how many times the
        // panel's PropertyChanged handler fires for one change, i.e. how many times it is
        // currently registered.
        floatingGroup.Group.RemovePanel(e.Text);

        Assert.Equal(1, layoutChangedCount);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("tree")]
    [InlineData("properties")]
    [InlineData("diagnostics")]
    public void FloatThenDetachThenRestore_SinglePane_ReproducesStartingJson(string panelId)
    {
        var e = EditorLayout();
        var beforeJson = DockLayoutSerializer.ToJson(e.Model);
        var panel = (DockPanelNode)e.Model.FindNodeById(panelId);

        DockOperation.FloatPanel(e.Model, panel, 0, 0, 100, 100);
        Assert.True(DockOperation.DetachFromFloatingGroup(e.Model, panel));
        Assert.True(DockOperation.RestoreToPlacement(e.Model, panel));

        var afterJson = DockLayoutSerializer.ToJson(e.Model);
        Assert.Equal(beforeJson, afterJson);
        Assert.Empty(e.Model.FloatingGroups);
        Assert.Empty(e.Model.Placements);
        Assert.True(e.Model.ValidateTree());
    }

    // ── All 24 departure orders x all 24 return orders of the four floatable panes ──

    [Fact]
    public void FloatAllFourPanes_AnyDepartureAndReturnOrder_ReproducesStartingJson()
    {
        var failures = new List<string>();
        var referenceFixture = EditorLayout();
        var floatableIds = referenceFixture.Floatable.Select(p => p.Id).ToList();

        foreach (var departureOrder in Permutations(floatableIds))
        {
            foreach (var returnOrder in Permutations(floatableIds))
            {
                var e = EditorLayout();
                var beforeJson = DockLayoutSerializer.ToJson(e.Model);

                foreach (var id in departureOrder)
                {
                    var panel = (DockPanelNode)e.Model.FindNodeById(id);
                    DockOperation.FloatPanel(e.Model, panel, 0, 0, 100, 100);
                }

                foreach (var id in returnOrder)
                {
                    var panel = e.All.Concat(Array.Empty<DockPanelNode>()).First(p => p.Id == id);
                    DockOperation.DetachFromFloatingGroup(e.Model, panel);
                    DockOperation.RestoreToPlacement(e.Model, panel);
                }

                var afterJson = DockLayoutSerializer.ToJson(e.Model);
                if (afterJson != beforeJson || !e.Model.ValidateTree())
                {
                    failures.Add($"depart=[{string.Join(",", departureOrder)}] return=[{string.Join(",", returnOrder)}]");
                }
            }
        }

        Assert.Empty(failures);
    }

    // ── Five panes auto-hidden forward and reverse, then all 120 return orders ──

    [Fact]
    public void AutoHideAllFivePanes_ForwardAndReverseDeparture_AnyReturnOrder_ReproducesStartingJson()
    {
        var failures = new List<string>();
        var reference = EditorLayout();
        var forwardIds = reference.All.Select(p => p.Id).ToList();
        var reverseIds = Enumerable.Reverse(forwardIds).ToList();

        foreach (var departureOrder in new[] { forwardIds, reverseIds })
        {
            foreach (var returnOrder in Permutations(forwardIds))
            {
                var e = EditorLayout();
                var beforeJson = DockLayoutSerializer.ToJson(e.Model);

                foreach (var id in departureOrder)
                {
                    var panel = (DockPanelNode)e.Model.FindNodeById(id);
                    DockOperation.AutoHidePanel(e.Model, panel, AutoHideSide.Left);
                }

                foreach (var id in returnOrder)
                {
                    var panel = e.All.First(p => p.Id == id);
                    e.Model.RemoveFromAutoHide(panel);
                    DockOperation.RestoreToPlacement(e.Model, panel);
                }

                var afterJson = DockLayoutSerializer.ToJson(e.Model);
                if (afterJson != beforeJson || !e.Model.ValidateTree())
                {
                    failures.Add($"depart=[{string.Join(",", departureOrder)}] return=[{string.Join(",", returnOrder)}]");
                }
            }
        }

        Assert.Empty(failures);
    }

    // ── Mixed: float / auto-hide / close(remember), all 6 return orders ──

    [Fact]
    public void MixedFloatAutoHideClose_AnyReturnOrder_ReproducesStartingJson()
    {
        var failures = new List<string>();
        var ids = new[] { "text", "preview", "tree" };

        foreach (var returnOrder in Permutations(ids))
        {
            var e = EditorLayout();
            var beforeJson = DockLayoutSerializer.ToJson(e.Model);

            DockOperation.FloatPanel(e.Model, e.Text, 0, 0, 100, 100);
            DockOperation.AutoHidePanel(e.Model, e.Preview, AutoHideSide.Left);
            DockOperation.ClosePanel(e.Model, e.Tree, rememberPlacement: true);

            foreach (var id in returnOrder)
            {
                var panel = e.All.First(p => p.Id == id);
                if (id == "text")
                {
                    DockOperation.DetachFromFloatingGroup(e.Model, panel);
                }
                else if (id == "preview")
                {
                    e.Model.RemoveFromAutoHide(panel);
                }
                // "tree" was closed: nothing to detach, panel.Parent is already null.

                DockOperation.RestoreToPlacement(e.Model, panel);
            }

            var afterJson = DockLayoutSerializer.ToJson(e.Model);
            if (afterJson != beforeJson || !e.Model.ValidateTree())
            {
                failures.Add($"return=[{string.Join(",", returnOrder)}]");
            }
        }

        Assert.Empty(failures);
    }

    // ── D3 / P11: index rule for out-of-order returns in a multi-tab group ──

    [Fact]
    public void MultiTabGroup_OutOfOrderReturn_FollowsIndexRule()
    {
        // Case 1: departures C, B, A then returns C, B, A -> [A, C, B], active A.
        {
            var a = new DockPanelNode("a") { Title = "A" };
            var b = new DockPanelNode("b") { Title = "B" };
            var c = new DockPanelNode("c") { Title = "C" };
            var group = new DockTabGroupNode("group");
            group.AddPanel(a, -1);
            group.AddPanel(b, -1);
            group.AddPanel(c, -1);
            group.SetActivePanel(c.Id);
            var other = new DockTabGroupNode("other");
            other.AddPanel(new DockPanelNode("x") { Title = "X" }, -1);
            var split = new DockSplitNode("split") { Orientation = Orientation.Horizontal, SplitRatio = 0.5f, FirstChild = group, SecondChild = other };
            var model = new DockLayoutModel(split);

            foreach (var p in new[] { c, b, a })
            {
                DockOperation.FloatPanel(model, p, 0, 0, 10, 10);
            }

            foreach (var p in new[] { c, b, a })
            {
                DockOperation.DetachFromFloatingGroup(model, p);
                DockOperation.RestoreToPlacement(model, p);
            }

            Assert.Same(group, split.FirstChild);
            Assert.Same(split, group.Parent);
            Assert.Equal(0.5f, split.SplitRatio, 3);
            Assert.Equal(new[] { a, c, b }, group.Panels);
            Assert.Equal(a.Id, group.ActivePanelId);
        }

        // Case 2: departures A, B, C then returns C, B, A (reverse order) -> [A, B, C], active A.
        {
            var a = new DockPanelNode("a2") { Title = "A" };
            var b = new DockPanelNode("b2") { Title = "B" };
            var c = new DockPanelNode("c2") { Title = "C" };
            var group = new DockTabGroupNode("group2");
            group.AddPanel(a, -1);
            group.AddPanel(b, -1);
            group.AddPanel(c, -1);
            group.SetActivePanel(c.Id);
            var other = new DockTabGroupNode("other2");
            other.AddPanel(new DockPanelNode("x2") { Title = "X" }, -1);
            var split = new DockSplitNode("split2") { Orientation = Orientation.Horizontal, SplitRatio = 0.5f, FirstChild = group, SecondChild = other };
            var model = new DockLayoutModel(split);

            foreach (var p in new[] { a, b, c })
            {
                DockOperation.FloatPanel(model, p, 0, 0, 10, 10);
            }

            foreach (var p in new[] { c, b, a })
            {
                DockOperation.DetachFromFloatingGroup(model, p);
                DockOperation.RestoreToPlacement(model, p);
            }

            Assert.Same(group, split.FirstChild);
            Assert.Equal(0.5f, split.SplitRatio, 3);
            Assert.Equal(new[] { a, b, c }, group.Panels);
            Assert.Equal(a.Id, group.ActivePanelId);
        }
    }

    [Fact]
    public void RestoreToPlacement_ClampsOutOfBoundsIndex_D3()
    {
        var a = new DockPanelNode("a") { Title = "A" };
        var b = new DockPanelNode("b") { Title = "B" };
        var c = new DockPanelNode("c") { Title = "C" };
        var group = new DockTabGroupNode("group");
        group.AddPanel(a, -1);
        group.AddPanel(b, -1);
        group.AddPanel(c, -1);
        var other = new DockTabGroupNode("other");
        other.AddPanel(new DockPanelNode("x") { Title = "X" }, -1);
        var split = new DockSplitNode("split") { Orientation = Orientation.Horizontal, SplitRatio = 0.5f, FirstChild = group, SecondChild = other };
        var model = new DockLayoutModel(split);

        DockOperation.FloatPanel(model, b, 0, 0, 10, 10); // group becomes [A, C]; remembered index for b = 1

        // In-bounds restore first: the group still holds two panels when b returns, so a
        // hard-coded append (e.g. always -1, ignoring the remembered index) is distinguishable
        // from honoring the remembered index 1 (b lands between a and c, not after c).
        DockOperation.DetachFromFloatingGroup(model, b);
        DockOperation.RestoreToPlacement(model, b);

        Assert.Equal(new[] { a, b, c }, group.Panels);
        Assert.Equal(b.Id, group.ActivePanelId);

        // Now shrink the group below the remembered index: the out-of-bounds case must still
        // clamp to an append rather than throwing or corrupting order.
        DockOperation.FloatPanel(model, b, 0, 0, 10, 10); // group becomes [A, C] again; remembered index = 1
        DockOperation.ClosePanel(model, c, rememberPlacement: false); // group becomes [A]

        DockOperation.DetachFromFloatingGroup(model, b);
        DockOperation.RestoreToPlacement(model, b); // remembered index 1, but group only has 1 tab now

        Assert.Equal(new[] { a, b }, group.Panels);
        Assert.Equal(b.Id, group.ActivePanelId);
    }

    // ── A placeholder survives the removal of the group's last docked panel ──

    [Fact]
    public void Placeholder_Survives_RemovalOfLastDockedPanel()
    {
        var a = new DockPanelNode("a") { Title = "A" };
        var b = new DockPanelNode("b") { Title = "B" };
        var group = new DockTabGroupNode("group");
        group.AddPanel(a, -1);
        group.AddPanel(b, -1);
        var other = new DockTabGroupNode("other");
        other.AddPanel(new DockPanelNode("x") { Title = "X" }, -1);
        var split = new DockSplitNode("split") { Orientation = Orientation.Horizontal, SplitRatio = 0.5f, FirstChild = group, SecondChild = other };
        var model = new DockLayoutModel(split);

        DockOperation.FloatPanel(model, a, 0, 0, 10, 10); // group becomes [B]
        DockOperation.RemovePanel(model, b); // group becomes empty

        Assert.Same(group, split.FirstChild);
        Assert.True(group.IsEmpty);
        Assert.True(group.IsHiddenInLayout);

        DockOperation.DetachFromFloatingGroup(model, a);
        DockOperation.RestoreToPlacement(model, a);

        Assert.Same(group, split.FirstChild);
        Assert.Equal(new[] { a }, group.Panels);
    }

    // ── ForgetPlacement / CleanupEmptyNodes collapse exactly like today ──

    [Fact]
    public void ForgetPlacement_Collapses_ExactlyLikeDirectRemovePanel()
    {
        var e1 = EditorLayout();
        DockOperation.FloatPanel(e1.Model, e1.Text, 0, 0, 10, 10);
        DockOperation.ForgetPlacement(e1.Model, e1.Text.Id);

        var e2 = EditorLayout();
        DockOperation.RemovePanel(e2.Model, e2.Text);

        Assert.Equal(DockLayoutSerializer.ToJson(e2.Model), DockLayoutSerializer.ToJson(e1.Model));
    }

    [Fact]
    public void CleanupEmptyNodes_Collapses_UnreferencedEmptyGroup_ExactlyLikeDirectRemovePanel()
    {
        var e1 = EditorLayout();
        DockOperation.FloatPanel(e1.Model, e1.Text, 0, 0, 10, 10);
        e1.Model.RemovePlacement(e1.Text.Id); // simulate an unreferenced placeholder directly
        DockOperation.CleanupEmptyNodes(e1.Model);

        var e2 = EditorLayout();
        DockOperation.RemovePanel(e2.Model, e2.Text);

        Assert.Equal(DockLayoutSerializer.ToJson(e2.Model), DockLayoutSerializer.ToJson(e1.Model));
    }

    [Fact]
    public void CleanupEmptyNodes_Keeps_ReferencedPlaceholder()
    {
        var e = EditorLayout();
        DockOperation.FloatPanel(e.Model, e.Text, 0, 0, 10, 10);

        DockOperation.CleanupEmptyNodes(e.Model);

        Assert.Same(e.TextGroup, e.Model.FindNodeById(e.TextGroup.Id));
        Assert.True(e.TextGroup.IsEmpty);
        Assert.Same(e.TextGroup, e.TopSplit.FirstChild);
    }

    // ── Dangling placement: the application replaced the tree under different ids ──

    [Fact]
    public void RestoreToPlacement_ReturnsFalse_AndKeepsPlacement_WhenGroupNoLongerInTree()
    {
        var e = EditorLayout();
        DockOperation.FloatPanel(e.Model, e.Text, 0, 0, 10, 10);
        DockOperation.DetachFromFloatingGroup(e.Model, e.Text);

        // Application replaces the tree wholesale, under different ids.
        var replacement = new DockTabGroupNode("replacement-group");
        replacement.AddPanel(new DockPanelNode("other-panel") { Title = "Other" }, -1);
        e.Model.RootNode = replacement;
        var jsonAfterReplace = DockLayoutSerializer.ToJson(e.Model);

        var restored = DockOperation.RestoreToPlacement(e.Model, e.Text);

        Assert.False(restored);
        Assert.Equal(jsonAfterReplace, DockLayoutSerializer.ToJson(e.Model));
        Assert.True(e.Model.TryGetPlacement(e.Text.Id, out _));
        Assert.Null(DockOperation.ResolvePlacementGroup(e.Model, e.Text.Id));
    }

    // ── Argument and state validation ─────────────────────────────────────

    [Fact]
    public void RestoreToPlacement_Throws_WhenPanelStillHasParent()
    {
        var e = EditorLayout();
        DockOperation.FloatPanel(e.Model, e.Text, 0, 0, 10, 10); // Text now belongs to a floating group

        Assert.Throws<InvalidOperationException>(() => DockOperation.RestoreToPlacement(e.Model, e.Text));
    }

    [Fact]
    public void FloatPanel_Throws_WhenPanelAlreadyInAFloatingGroup()
    {
        var e = EditorLayout();
        DockOperation.FloatPanel(e.Model, e.Text, 0, 0, 10, 10);

        Assert.Throws<InvalidOperationException>(() => DockOperation.FloatPanel(e.Model, e.Text, 0, 0, 10, 10));
    }

    [Fact]
    public void AutoHidePanel_Throws_WhenPanelIsInAFloatingGroup()
    {
        var e = EditorLayout();
        DockOperation.FloatPanel(e.Model, e.Text, 0, 0, 10, 10);

        Assert.Throws<InvalidOperationException>(() => DockOperation.AutoHidePanel(e.Model, e.Text, AutoHideSide.Left));
    }

    [Fact]
    public void DetachFromFloatingGroup_RemovesEmptiedFloatingGroup_KeepsGroupStillHoldingPanels()
    {
        var e = EditorLayout();
        var floatingGroup = DockOperation.FloatPanel(e.Model, e.Text, 0, 0, 10, 10);

        // Merge Properties into the same floating group to make it hold two panels.
        DockOperation.RemovePanel(e.Model, e.Properties);
        floatingGroup.Group.AddPanel(e.Properties, -1);

        Assert.True(DockOperation.DetachFromFloatingGroup(e.Model, e.Properties));
        Assert.Contains(floatingGroup, e.Model.FloatingGroups); // still holds Text

        Assert.True(DockOperation.DetachFromFloatingGroup(e.Model, e.Text));
        Assert.DoesNotContain(floatingGroup, e.Model.FloatingGroups); // now empty, removed
    }

    // ── ClosePanel: from the tree, from a floating group, from the auto-hide store ──

    [Fact]
    public void ClosePanel_FromTree_RememberPlacement_KeepsPlacementAndPlaceholder()
    {
        var e = EditorLayout();

        DockOperation.ClosePanel(e.Model, e.Text, rememberPlacement: true);

        Assert.True(e.Model.TryGetPlacement(e.Text.Id, out var placement));
        Assert.Equal(e.TextGroup.Id, placement.GroupId);
        Assert.Same(e.TextGroup, e.Model.FindNodeById(e.TextGroup.Id));
        Assert.True(e.TextGroup.IsEmpty);
    }

    [Fact]
    public void ClosePanel_FromTree_ForgetPlacement_CollectsPlaceholder()
    {
        var e = EditorLayout();

        DockOperation.ClosePanel(e.Model, e.Text, rememberPlacement: false);

        Assert.False(e.Model.TryGetPlacement(e.Text.Id, out _));
        Assert.Null(e.Model.FindNodeById(e.TextGroup.Id));
    }

    [Fact]
    public void ClosePanel_FromFloatingGroup_RememberPlacement_KeepsOriginalPlacement()
    {
        var e = EditorLayout();
        DockOperation.FloatPanel(e.Model, e.Text, 0, 0, 10, 10);

        DockOperation.ClosePanel(e.Model, e.Text, rememberPlacement: true);

        Assert.Empty(e.Model.FloatingGroups);
        Assert.True(e.Model.TryGetPlacement(e.Text.Id, out var placement));
        Assert.Equal(e.TextGroup.Id, placement.GroupId);
        Assert.Same(e.TextGroup, e.Model.FindNodeById(e.TextGroup.Id));
    }

    [Fact]
    public void ClosePanel_FromFloatingGroup_ForgetPlacement_CollectsPlaceholder()
    {
        var e = EditorLayout();
        DockOperation.FloatPanel(e.Model, e.Text, 0, 0, 10, 10);

        DockOperation.ClosePanel(e.Model, e.Text, rememberPlacement: false);

        Assert.Empty(e.Model.FloatingGroups);
        Assert.False(e.Model.TryGetPlacement(e.Text.Id, out _));
        Assert.Null(e.Model.FindNodeById(e.TextGroup.Id));
    }

    [Fact]
    public void ClosePanel_FromAutoHideStore_RememberPlacement_KeepsOriginalPlacement()
    {
        var e = EditorLayout();
        DockOperation.AutoHidePanel(e.Model, e.Text, AutoHideSide.Left);

        DockOperation.ClosePanel(e.Model, e.Text, rememberPlacement: true);

        Assert.False(e.Model.HasAnyAutoHidePanels());
        Assert.True(e.Model.TryGetPlacement(e.Text.Id, out var placement));
        Assert.Equal(e.TextGroup.Id, placement.GroupId);
        Assert.Same(e.TextGroup, e.Model.FindNodeById(e.TextGroup.Id));
    }

    [Fact]
    public void ClosePanel_FromAutoHideStore_ForgetPlacement_CollectsPlaceholder()
    {
        var e = EditorLayout();
        DockOperation.AutoHidePanel(e.Model, e.Text, AutoHideSide.Left);

        DockOperation.ClosePanel(e.Model, e.Text, rememberPlacement: false);

        Assert.False(e.Model.HasAnyAutoHidePanels());
        Assert.False(e.Model.TryGetPlacement(e.Text.Id, out _));
        Assert.Null(e.Model.FindNodeById(e.TextGroup.Id));
    }
}
