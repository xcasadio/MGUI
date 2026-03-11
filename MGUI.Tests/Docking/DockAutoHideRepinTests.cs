using MGUI.Core.UI;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Tests.Docking;

/// <summary>
/// Task 17.5 — Unit tests for auto-hide / repin behaviour.
///
/// Full MGDockHost.UnpinPanel / RepinPanel (which call RebuildVisualTree) require an
/// MGWindow and are integration-test concerns.  These tests verify:
///   - The DockLayoutModel auto-hide store operations that underpin those methods.
///   - The DockPanelNode snapshot fields (AutoHideReturnGroup, AutoHideReturnZone,
///     AutoHideReturnSplitRatio) that the host reads back when repinning.
///   - Manual simulation of the unpin → repin round-trip using only model-layer APIs,
///     confirming that the restore logic returns the panel to the correct group.
/// </summary>
public class DockAutoHideRepinTests
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

    // ─────────────────────────────────────────────────────────────────────
    // UnpinPanel model-layer simulation
    // (mirrors what MGDockHost.UnpinPanel does, without needing a window)
    // ─────────────────────────────────────────────────────────────────────

    private static void SimulateUnpin(DockLayoutModel model, DockPanelNode panel, AutoHideSide side)
    {
        // Snapshot return group (as MGDockHost.UnpinPanel does)
        panel.AutoHideReturnGroup = panel.Parent as DockTabGroupNode;

        // Snapshot split position
        if (panel.AutoHideReturnGroup?.Parent is DockSplitNode splitParent)
        {
            bool isFirst = splitParent.FirstChild == panel.AutoHideReturnGroup;
            panel.AutoHideReturnZone = splitParent.Orientation == Orientation.Horizontal
                ? (isFirst ? DockZone.Left  : DockZone.Right)
                : (isFirst ? DockZone.Top   : DockZone.Bottom);
            panel.AutoHideReturnSplitRatio = isFirst ? splitParent.SplitRatio : 1f - splitParent.SplitRatio;
        }
        else
        {
            panel.AutoHideReturnZone       = DockZone.None;
            panel.AutoHideReturnSplitRatio = null;
        }

        DockOperation.RemovePanel(model, panel);
        model.AddToAutoHide(panel, side);
    }

    // ─────────────────────────────────────────────────────────────────────
    // RepinPanel model-layer simulation
    // ─────────────────────────────────────────────────────────────────────

    private static void SimulateRepin(DockLayoutModel model, DockPanelNode panel)
    {
        model.RemoveFromAutoHide(panel);

        var returnGroup = panel.AutoHideReturnGroup;
        panel.AutoHideReturnGroup = null;

        if (returnGroup != null && model.GetAllTabGroups().Contains(returnGroup))
        {
            DockOperation.DockAsTab(model, panel, returnGroup);
        }
        else
        {
            DockZone fallbackZone = panel.AutoHideReturnZone != DockZone.None
                ? panel.AutoHideReturnZone
                : panel.AutoHideSide switch
                {
                    AutoHideSide.Left   => DockZone.Left,
                    AutoHideSide.Right  => DockZone.Right,
                    AutoHideSide.Top    => DockZone.Top,
                    AutoHideSide.Bottom => DockZone.Bottom,
                    _                   => DockZone.Right
                };
            float fallbackRatio = panel.AutoHideReturnSplitRatio ?? 0.25f;

            panel.AutoHideReturnZone       = DockZone.None;
            panel.AutoHideReturnSplitRatio = null;

            if (model.RootNode == null)
            {
                var newGroup = new DockTabGroupNode();
                newGroup.AddPanel(panel, -1);
                model.RootNode = newGroup;
            }
            else
            {
                DockOperation.SplitDockAtRoot(model, panel, fallbackZone, fallbackRatio);
            }
        }
    }

    // ── AddToAutoHide / RemoveFromAutoHide store tests ────────────────────

    [Fact]
    public void AutoHideStore_Multipleпанели_CorrectSides()
    {
        var model = new DockLayoutModel();
        var pLeft  = Panel("L");
        var pRight = Panel("R");
        var pTop   = Panel("T");

        model.AddToAutoHide(pLeft,  AutoHideSide.Left);
        model.AddToAutoHide(pRight, AutoHideSide.Right);
        model.AddToAutoHide(pTop,   AutoHideSide.Top);

        Assert.Contains(pLeft,  model.GetAutoHidePanels(AutoHideSide.Left));
        Assert.Contains(pRight, model.GetAutoHidePanels(AutoHideSide.Right));
        Assert.Contains(pTop,   model.GetAutoHidePanels(AutoHideSide.Top));
        Assert.Empty(model.GetAutoHidePanels(AutoHideSide.Bottom));
    }

    [Fact]
    public void GetAllAutoHidePanels_ReturnsAllSides()
    {
        var model = new DockLayoutModel();
        var panels = new[]
        {
            Panel("L"), Panel("R"), Panel("T"), Panel("B")
        };

        model.AddToAutoHide(panels[0], AutoHideSide.Left);
        model.AddToAutoHide(panels[1], AutoHideSide.Right);
        model.AddToAutoHide(panels[2], AutoHideSide.Top);
        model.AddToAutoHide(panels[3], AutoHideSide.Bottom);

        Assert.Equal(4, model.GetAllAutoHidePanels().Count());
    }

    // ── AutoHideReturnGroup snapshot ──────────────────────────────────────

    [Fact]
    public void SimulateUnpin_SnapshotsReturnGroup()
    {
        var p = Panel();
        var g = Group(p);
        var model = new DockLayoutModel(g);

        SimulateUnpin(model, p, AutoHideSide.Left);

        Assert.Same(g, p.AutoHideReturnGroup);
    }

    [Fact]
    public void SimulateUnpin_SetsIsPinnedFalse()
    {
        var p = Panel();
        var g = Group(p);
        var model = new DockLayoutModel(g);

        SimulateUnpin(model, p, AutoHideSide.Left);

        Assert.False(p.IsPinned);
    }

    [Fact]
    public void SimulateUnpin_RemovesPanelFromLayout()
    {
        var p = Panel();
        var g = Group(p);
        var model = new DockLayoutModel(g);

        SimulateUnpin(model, p, AutoHideSide.Left);

        Assert.True(model.HasAutoHidePanels(AutoHideSide.Left));
        // panel is no longer in the visible layout tree
        Assert.DoesNotContain(p, model.GetAllTabGroups().SelectMany(grp => grp.Panels));
    }

    // ── AutoHideReturnZone snapshot (horizontal split) ────────────────────

    [Fact]
    public void SimulateUnpin_SnapshotsReturnZone_ForHorizontalSplit_FirstChild()
    {
        // panel is in g1 which is the LEFT child of a horizontal split
        var p  = Panel();
        var g1 = Group(p, Panel("other")); // keep g1 alive after unpin
        var g2 = Group(Panel("R"));
        var split = new DockSplitNode
        {
            Orientation = Orientation.Horizontal,
            SplitRatio  = 0.35f,
            FirstChild  = g1,
            SecondChild = g2
        };
        var model = new DockLayoutModel(split);

        SimulateUnpin(model, p, AutoHideSide.Left);

        Assert.Equal(DockZone.Left, p.AutoHideReturnZone);
        Assert.NotNull(p.AutoHideReturnSplitRatio);
        Assert.InRange(p.AutoHideReturnSplitRatio!.Value, 0.34f, 0.36f);
    }

    [Fact]
    public void SimulateUnpin_SnapshotsReturnZone_ForHorizontalSplit_SecondChild()
    {
        var p  = Panel();
        var g1 = Group(Panel("L"));
        var g2 = Group(p, Panel("other")); // p in second child
        var split = new DockSplitNode
        {
            Orientation = Orientation.Horizontal,
            SplitRatio  = 0.4f,
            FirstChild  = g1,
            SecondChild = g2
        };
        var model = new DockLayoutModel(split);

        SimulateUnpin(model, p, AutoHideSide.Right);

        Assert.Equal(DockZone.Right, p.AutoHideReturnZone);
        // Second child gets 1 - splitRatio = 0.6
        Assert.NotNull(p.AutoHideReturnSplitRatio);
        Assert.InRange(p.AutoHideReturnSplitRatio!.Value, 0.59f, 0.61f);
    }

    [Fact]
    public void SimulateUnpin_RootPanel_ReturnZoneIsNone()
    {
        // Panel is in a root group (no parent split) → no return zone
        var p = Panel();
        var g = Group(p, Panel("other"));
        var model = new DockLayoutModel(g);

        SimulateUnpin(model, p, AutoHideSide.Top);

        Assert.Equal(DockZone.None, p.AutoHideReturnZone);
        Assert.Null(p.AutoHideReturnSplitRatio);
    }

    // ── RepinPanel: restore to original group ─────────────────────────────

    [Fact]
    public void SimulateRepin_RestoresToOriginalGroup_WhenStillExists()
    {
        var p = Panel();
        var g = Group(p, Panel("other")); // keep g alive after unpin
        var model = new DockLayoutModel(g);

        SimulateUnpin(model, p, AutoHideSide.Left);
        SimulateRepin(model, p);

        // Panel should be back in g
        Assert.Contains(p, g.Panels);
        Assert.True(p.IsPinned);
    }

    [Fact]
    public void SimulateRepin_ClearsAutoHideReturnGroup()
    {
        var p = Panel();
        var g = Group(p, Panel("other"));
        var model = new DockLayoutModel(g);

        SimulateUnpin(model, p, AutoHideSide.Left);
        SimulateRepin(model, p);

        Assert.Null(p.AutoHideReturnGroup);
    }

    [Fact]
    public void SimulateRepin_FallbackSplit_WhenOriginalGroupGone()
    {
        // p is the ONLY panel in g; when unpinned g is destroyed
        var p = Panel();
        var g = Group(p);
        var gOther = Group(Panel("Other"));
        var split = new DockSplitNode
        {
            Orientation = Orientation.Horizontal,
            SplitRatio  = 0.5f,
            FirstChild  = g,
            SecondChild = gOther
        };
        var model = new DockLayoutModel(split);

        SimulateUnpin(model, p, AutoHideSide.Left);
        // At this point g is gone (was emptied), model.RootNode = gOther
        Assert.DoesNotContain(g, model.GetAllTabGroups());

        SimulateRepin(model, p);

        // Panel must be back in the layout (in some group)
        var allPanels = model.GetAllTabGroups().SelectMany(grp => grp.Panels).ToList();
        Assert.Contains(p, allPanels);
        Assert.True(p.IsPinned);
    }

    [Fact]
    public void SimulateRepin_EmptyLayout_RepinsIntoExistingEmptyRoot()
    {
        // Layout had only one panel; unpin empties it
        var p = Panel();
        var g = Group(p);
        var model = new DockLayoutModel(g);

        SimulateUnpin(model, p, AutoHideSide.Bottom);
        // Root group is preserved as an empty placeholder (not null)
        var emptyRoot = Assert.IsType<DockTabGroupNode>(model.RootNode);
        Assert.True(emptyRoot.IsEmpty);

        SimulateRepin(model, p);

        Assert.NotNull(model.RootNode);
        var allPanels = model.GetAllTabGroups().SelectMany(grp => grp.Panels).ToList();
        Assert.Contains(p, allPanels);
    }
}
