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
        foreach (var p in panels) g.AddPanel(p, -1);
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
}
