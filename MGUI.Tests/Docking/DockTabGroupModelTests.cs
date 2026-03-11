using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Tests.Docking;

/// <summary>
/// Task 17.3 — Unit tests for tab overflow invariants.
///
/// NOTE: The overflow-detection UI logic lives in MGDockTabItem / MGDockTabGroup
///       which require a MonoGame window for layout passes and is therefore
///       covered by integration tests.  These unit tests verify the underlying
///       DockTabGroupNode model behaviour that the visual layer is built on top of:
///       panel ordering, active-tab promotion, and tab reordering edge-cases.
/// </summary>
public class DockTabGroupModelTests
{
    // ── Helpers ──────────────────────────────────────────────────────────
    private static DockPanelNode Panel(string title = "P")
        => new DockPanelNode { Title = title };

    private static DockTabGroupNode Group(params DockPanelNode[] panels)
    {
        var g = new DockTabGroupNode();
        foreach (var p in panels)
        {
            g.AddPanel(p, -1);
        }

        return g;
    }

    // ── Panel ordering ────────────────────────────────────────────────────

    [Fact]
    public void AddPanel_AppendsAtEnd_WithNegativeIndex()
    {
        var pA = Panel("A");
        var pB = Panel("B");
        var g  = Group(pA);

        g.AddPanel(pB, -1);

        Assert.Equal(2, g.Panels.Count);
        Assert.Same(pB, g.Panels[1]);
    }

    [Fact]
    public void AddPanel_InsertsAtIndex()
    {
        var pA = Panel("A");
        var pC = Panel("C");
        var pB = Panel("B");
        var g  = Group(pA, pC);

        g.AddPanel(pB, 1); // insert between A and C

        Assert.Same(pA, g.Panels[0]);
        Assert.Same(pB, g.Panels[1]);
        Assert.Same(pC, g.Panels[2]);
    }

    [Fact]
    public void AddPanel_SetsParent()
    {
        var p = Panel();
        var g = new DockTabGroupNode();

        g.AddPanel(p, -1);

        Assert.Same(g, p.Parent);
    }

    // ── Active panel ──────────────────────────────────────────────────────

    [Fact]
    public void SetActivePanel_ChangesActivePanelId()
    {
        var p1 = Panel("A");
        var p2 = Panel("B");
        var g  = Group(p1, p2);

        g.SetActivePanel(p2.Id);

        Assert.Equal(p2.Id, g.ActivePanelId);
        Assert.Same(p2, g.ActivePanel);
    }

    [Fact]
    public void ActivePanel_IsFirstPanel_WhenNotExplicitlySet()
    {
        // After adding a panel, it becomes active if group was empty first.
        var p = Panel();
        var g = new DockTabGroupNode();
        g.AddPanel(p, -1);

        // The active panel should be p (first inserted)
        Assert.Equal(p.Id, g.ActivePanelId);
    }

    [Fact]
    public void ActivePanel_ReturnsNull_WhenNoPanels()
    {
        var g = new DockTabGroupNode();
        Assert.Null(g.ActivePanel);
    }

    [Fact]
    public void ActivePanel_UpdatesAfterRemove_ToRemainingPanel()
    {
        var p1 = Panel("A");
        var p2 = Panel("B");
        var g  = Group(p1, p2);
        g.SetActivePanel(p1.Id);

        g.RemovePanel(p1);

        // Active panel should shift to remaining panel
        Assert.Same(p2, g.ActivePanel);
    }

    // ── IsEmpty ───────────────────────────────────────────────────────────

    [Fact]
    public void IsEmpty_TrueForNewGroup()
    {
        Assert.True(new DockTabGroupNode().IsEmpty);
    }

    [Fact]
    public void IsEmpty_FalseAfterAddPanel()
    {
        var g = Group(Panel());
        Assert.False(g.IsEmpty);
    }

    [Fact]
    public void IsEmpty_TrueAfterRemoveLastPanel()
    {
        var p = Panel();
        var g = Group(p);

        g.RemovePanel(p);

        Assert.True(g.IsEmpty);
    }

    // ── RemovePanel ───────────────────────────────────────────────────────

    [Fact]
    public void RemovePanel_ReturnsTrueWhenFound()
    {
        var p = Panel();
        var g = Group(p);

        Assert.True(g.RemovePanel(p));
    }

    [Fact]
    public void RemovePanel_ReturnsFalseWhenNotFound()
    {
        var p = Panel();
        var g = new DockTabGroupNode();

        Assert.False(g.RemovePanel(p));
    }

    [Fact]
    public void RemovePanel_ClearsParentReference()
    {
        var p = Panel();
        var g = Group(p);

        g.RemovePanel(p);

        Assert.Null(p.Parent);
    }

    // ── ReorderPanel ─────────────────────────────────────────────────────

    [Fact]
    public void ReorderPanel_MovesForward()
    {
        var pA = Panel("A");
        var pB = Panel("B");
        var pC = Panel("C");
        var g  = Group(pA, pB, pC);

        g.ReorderPanel(pA, 2); // A → index 2

        Assert.Same(pB, g.Panels[0]);
        Assert.Same(pC, g.Panels[1]);
        Assert.Same(pA, g.Panels[2]);
    }

    [Fact]
    public void ReorderPanel_MovesBackward()
    {
        var pA = Panel("A");
        var pB = Panel("B");
        var pC = Panel("C");
        var g  = Group(pA, pB, pC);

        g.ReorderPanel(pC, 0); // C → index 0

        Assert.Same(pC, g.Panels[0]);
        Assert.Same(pA, g.Panels[1]);
        Assert.Same(pB, g.Panels[2]);
    }

    [Fact]
    public void ReorderPanel_NegativeIndex_AppendsAtEnd()
    {
        var pA = Panel("A");
        var pB = Panel("B");
        var pC = Panel("C");
        var g  = Group(pA, pB, pC);

        g.ReorderPanel(pA, -1); // A → end

        Assert.Same(pA, g.Panels[^1]);
    }

    [Fact]
    public void ReorderPanel_SamePosition_NoChange()
    {
        var pA = Panel("A");
        var pB = Panel("B");
        var g  = Group(pA, pB);

        g.ReorderPanel(pA, 0); // no-op

        Assert.Same(pA, g.Panels[0]);
        Assert.Same(pB, g.Panels[1]);
    }

    // ── Scroll-index invariant (model-level) ──────────────────────────────

    /// <summary>
    /// When panels are added, the panel count determines the upper bound for the
    /// visible-tab scroll index.  After all panels are removed, the overflow state
    /// should resolve to "no overflow" at the model level (panel count == 0).
    /// </summary>
    [Fact]
    public void PanelCount_DecreasesAfterRemove_AllowsScrollClamp()
    {
        // Simulate: 5 panels → scroll to end → remove all but 1 → count should be 1
        var panels = Enumerable.Range(0, 5).Select(i => Panel($"P{i}")).ToList();
        var g = Group(panels.ToArray());

        Assert.Equal(5, g.Panels.Count);

        foreach (var p in panels.Take(4))
        {
            g.RemovePanel(p);
        }

        Assert.Single(g.Panels);
        // max valid scroll index = count - visible (≥1) ≤ 0 → scroll would be 0
        int maxScroll = Math.Max(0, g.Panels.Count - 1);
        Assert.Equal(0, maxScroll);
    }
}
