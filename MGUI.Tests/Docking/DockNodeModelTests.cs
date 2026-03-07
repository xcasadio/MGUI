using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Tests.Docking;

/// <summary>
/// Phase 4.2 — Unit tests that fill coverage gaps in the model layer:
///   DockNode, DockSplitNode, DockTabGroupNode edge cases.
/// </summary>
public class DockNodeModelTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static DockPanelNode Panel(string? title = null)
        => new DockPanelNode { Title = title ?? "Panel" };

    private static DockTabGroupNode Group(params DockPanelNode[] panels)
    {
        var g = new DockTabGroupNode();
        foreach (var p in panels) g.Panels.Add(p);
        return g;
    }

    // ══════════════════════════════════════════════════════════════════════
    // DockNode: FindNodeById
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void FindNodeById_ReturnsNull_WhenIdNotInTree()
    {
        var g = Group(Panel());
        Assert.Null(g.FindNodeById("does-not-exist"));
    }

    [Fact]
    public void FindNodeById_ReturnsSelf_WhenIdMatches()
    {
        var g = Group();
        Assert.Same(g, g.FindNodeById(g.Id));
    }

    [Fact]
    public void FindNodeById_ReturnsPanel_NestedInsideSplit()
    {
        var p = Panel("Target");
        var g1 = Group(p);
        var g2 = Group(Panel("Other"));
        var split = new DockSplitNode { FirstChild = g1, SecondChild = g2 };

        var result = split.FindNodeById(p.Id);

        Assert.Same(p, result);
    }

    [Fact]
    public void FindNodeById_ReturnsNull_ForNullId()
    {
        var g = Group(Panel());
        // DockNode.FindNodeById accepts null gracefully (no match)
        Assert.Null(g.FindNodeById(null!));
    }

    // ══════════════════════════════════════════════════════════════════════
    // DockNode: Parent management
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SplitNode_FirstChild_Setter_UpdatesChildParent()
    {
        var split = new DockSplitNode();
        var g = Group(Panel());

        split.FirstChild = g;

        Assert.Same(split, g.Parent);
    }

    [Fact]
    public void SplitNode_FirstChild_Setter_ClearsOldChildParent()
    {
        var split = new DockSplitNode();
        var old = Group(Panel());
        split.FirstChild = old;
        Assert.Same(split, old.Parent);

        var replacement = Group(Panel());
        split.FirstChild = replacement;

        Assert.Null(old.Parent);
        Assert.Same(split, replacement.Parent);
    }

    [Fact]
    public void SplitNode_SecondChild_Setter_UpdatesChildParent()
    {
        var split = new DockSplitNode();
        var g = Group(Panel());

        split.SecondChild = g;

        Assert.Same(split, g.Parent);
    }

    [Fact]
    public void SplitNode_SecondChild_Setter_ClearsOldChildParent()
    {
        var split = new DockSplitNode();
        var old = Group(Panel());
        split.SecondChild = old;

        var replacement = Group(Panel());
        split.SecondChild = replacement;

        Assert.Null(old.Parent);
        Assert.Same(split, replacement.Parent);
    }

    // ══════════════════════════════════════════════════════════════════════
    // DockSplitNode: SplitRatio clamping
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SplitRatio_ClampsTo_Zero_WhenNegative()
    {
        var split = new DockSplitNode { SplitRatio = -0.5f };
        Assert.Equal(0f, split.SplitRatio);
    }

    [Fact]
    public void SplitRatio_ClampsTo_One_WhenAboveOne()
    {
        var split = new DockSplitNode { SplitRatio = 2.0f };
        Assert.Equal(1f, split.SplitRatio);
    }

    [Fact]
    public void SplitRatio_AcceptsValidValue()
    {
        var split = new DockSplitNode { SplitRatio = 0.35f };
        Assert.InRange(split.SplitRatio, 0.34f, 0.36f);
    }

    // ══════════════════════════════════════════════════════════════════════
    // DockSplitNode: GetChildren / RemoveChild / GetSibling
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetChildren_ReturnsBothChildren_WhenBothSet()
    {
        var c1 = Group(Panel("a"));
        var c2 = Group(Panel("b"));
        var split = new DockSplitNode { FirstChild = c1, SecondChild = c2 };

        var children = split.GetChildren().ToList();
        Assert.Equal(2, children.Count);
        Assert.Contains(c1, children);
        Assert.Contains(c2, children);
    }

    [Fact]
    public void GetChildren_SkipsNull_WhenChildrenNotSet()
    {
        var split = new DockSplitNode();
        Assert.Empty(split.GetChildren());
    }

    [Fact]
    public void RemoveChild_NullsFirstChild_WhenFirstChildRemoved()
    {
        var c1 = Group(Panel());
        var c2 = Group(Panel());
        var split = new DockSplitNode { FirstChild = c1, SecondChild = c2 };

        split.RemoveChild(c1);

        Assert.Null(split.FirstChild);
        Assert.Same(c2, split.SecondChild);
    }

    [Fact]
    public void RemoveChild_NullsSecondChild_WhenSecondChildRemoved()
    {
        var c1 = Group(Panel());
        var c2 = Group(Panel());
        var split = new DockSplitNode { FirstChild = c1, SecondChild = c2 };

        split.RemoveChild(c2);

        Assert.Same(c1, split.FirstChild);
        Assert.Null(split.SecondChild);
    }

    [Fact]
    public void GetSibling_ReturnsSibling_ForFirstChild()
    {
        var c1 = Group(Panel());
        var c2 = Group(Panel());
        var split = new DockSplitNode { FirstChild = c1, SecondChild = c2 };

        Assert.Same(c2, split.GetSibling(c1));
    }

    [Fact]
    public void GetSibling_ReturnsSibling_ForSecondChild()
    {
        var c1 = Group(Panel());
        var c2 = Group(Panel());
        var split = new DockSplitNode { FirstChild = c1, SecondChild = c2 };

        Assert.Same(c1, split.GetSibling(c2));
    }

    [Fact]
    public void GetSibling_ReturnsNull_ForUnrelatedNode()
    {
        var c1 = Group(Panel());
        var c2 = Group(Panel());
        var unrelated = Group(Panel());
        var split = new DockSplitNode { FirstChild = c1, SecondChild = c2 };

        Assert.Null(split.GetSibling(unrelated));
    }

    // ══════════════════════════════════════════════════════════════════════
    // DockTabGroupNode: AddPanel / RemovePanel / SetActivePanel
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void AddPanel_SetsParent_ToPanelParent()
    {
        var g = new DockTabGroupNode();
        var p = Panel();

        g.AddPanel(p, -1);

        Assert.Same(g, p.Parent);
    }

    [Fact]
    public void RemovePanel_ClearsParent()
    {
        var p = Panel();
        var g = Group(p);

        g.RemovePanel(p);

        Assert.Null(p.Parent);
    }

    [Fact]
    public void AddPanel_Duplicate_Throws()
    {
        var p = Panel();
        var g = Group(p);

        Assert.Throws<System.InvalidOperationException>(() => g.AddPanel(p, -1));
    }

    [Fact]
    public void SetActivePanel_WithNonExistent_Throws()
    {
        var g = Group(Panel());

        Assert.Throws<System.ArgumentException>(() => g.SetActivePanel("non-existent-id"));
    }

    [Fact]
    public void SetActivePanel_WithNull_Succeeds()
    {
        var g = Group(Panel());
        // Clearing active panel should not throw
        g.SetActivePanel(null);
        Assert.Null(g.ActivePanelId);
    }

    [Fact]
    public void IsEmpty_True_WhenNoPanels()
    {
        var g = new DockTabGroupNode();
        Assert.True(g.IsEmpty);
    }

    [Fact]
    public void IsEmpty_False_WhenHasPanels()
    {
        var g = Group(Panel());
        Assert.False(g.IsEmpty);
    }

    [Fact]
    public void ActivePanel_AutoClearedWhen_ActivePanelRemoved()
    {
        var p = Panel();
        var g = Group(p);
        Assert.Equal(p.Id, g.ActivePanelId);

        g.RemovePanel(p);

        Assert.Null(g.ActivePanelId);
    }

    [Fact]
    public void ActivePanel_SelectsFirstPanel_WhenFirstAdded()
    {
        var p = Panel();
        var g = new DockTabGroupNode();

        g.AddPanel(p, -1);

        Assert.Equal(p.Id, g.ActivePanelId);
    }

    [Fact]
    public void OnPanelsCollectionChanged_Move_PreservesParent()
    {
        // Reordering via ObservableCollection.Move should not affect Parent
        var p1 = Panel("A");
        var p2 = Panel("B");
        var g = Group(p1, p2);

        g.Panels.Move(0, 1); // swap positions

        Assert.Same(g, p1.Parent);
        Assert.Same(g, p2.Parent);
    }

    // ══════════════════════════════════════════════════════════════════════
    // DockLayoutModel: ValidateTree
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateTree_ReturnsTrue_ForEmptyModel()
    {
        var model = new DockLayoutModel();
        Assert.True(model.ValidateTree());
    }

    [Fact]
    public void ValidateTree_ReturnsTrue_ForValidSingleGroup()
    {
        var g = Group(Panel());
        var model = new DockLayoutModel(g);
        Assert.True(model.ValidateTree());
    }

    [Fact]
    public void ValidateTree_ReturnsTrue_ForValidSplit()
    {
        var g1 = Group(Panel("a"));
        var g2 = Group(Panel("b"));
        var split = new DockSplitNode
        {
            Orientation = Orientation.Horizontal,
            SplitRatio = 0.5f,
            FirstChild = g1,
            SecondChild = g2
        };
        var model = new DockLayoutModel(split);
        Assert.True(model.ValidateTree());
    }

    [Fact]
    public void ValidateTree_ReturnsFalse_WhenParentReferenceIsWrong()
    {
        // Build a valid tree then corrupt it by mutating a parent reference directly.
        // The internal setter is accessible because MGUI.Tests is in InternalsVisibleTo.
        var p = Panel();
        var g1 = Group(p);
        var g2 = Group();
        var split = new DockSplitNode
        {
            Orientation = Orientation.Horizontal,
            SplitRatio = 0.5f,
            FirstChild = g1,
            SecondChild = g2
        };
        var model = new DockLayoutModel(split);
        Assert.True(model.ValidateTree()); // sanity check

        // Corrupt: panel's parent should be g1, force it to g2 via internal setter
        p.Parent = g2;

        Assert.False(model.ValidateTree());
    }

    // ══════════════════════════════════════════════════════════════════════
    // DockPanelNode: content caching
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetOrCreateContent_ReturnsNull_WhenNoFactory()
    {
        var p = Panel();
        Assert.Null(p.GetOrCreateContent());
        Assert.False(p.IsContentCreated);
    }

    [Fact]
    public void ClearCachedContent_ReturnsOldContent_AndSetsToNull()
    {
        // ClearCachedContent now returns the old content for caller cleanup
        var p = Panel();
        // Without a real MonoGame context we can't create an MGElement,
        // so verify the null-content path of Clear returns null correctly.
        var old = p.ClearCachedContent();
        Assert.Null(old);
        Assert.False(p.IsContentCreated);
    }
}
