using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Tests.Docking;

/// <summary>
/// Task 17.1 — Unit tests for DockLayoutModel:
///   AddToAutoHide / RemoveFromAutoHide / HasAutoHidePanels, GetAllTabGroups,
///   JSON serialization round-trip.
/// </summary>
public class DockLayoutModelTests
{
    // ── Helpers ──────────────────────────────────────────────────────────
    private static DockPanelNode Panel(string id = "p1", string title = "Panel")
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

    // ── AddToAutoHide ─────────────────────────────────────────────────────

    [Fact]
    public void AddToAutoHide_SetsIsPinnedFalse()
    {
        var model = new DockLayoutModel();
        var p = Panel();
        Assert.True(p.IsPinned);

        model.AddToAutoHide(p, AutoHideSide.Left);

        Assert.False(p.IsPinned);
    }

    [Fact]
    public void AddToAutoHide_AppearsOnCorrectSide()
    {
        var model = new DockLayoutModel();
        var p = Panel();

        model.AddToAutoHide(p, AutoHideSide.Right);

        Assert.Contains(p, model.GetAutoHidePanels(AutoHideSide.Right));
        Assert.DoesNotContain(p, model.GetAutoHidePanels(AutoHideSide.Left));
        Assert.DoesNotContain(p, model.GetAutoHidePanels(AutoHideSide.Top));
        Assert.DoesNotContain(p, model.GetAutoHidePanels(AutoHideSide.Bottom));
    }

    [Fact]
    public void AddToAutoHide_SetsAutoHideSideProperty()
    {
        var model = new DockLayoutModel();
        var p = Panel();

        model.AddToAutoHide(p, AutoHideSide.Top);

        Assert.Equal(AutoHideSide.Top, p.AutoHideSide);
    }

    [Fact]
    public void AddToAutoHide_MovesFromPreviousSide()
    {
        // Adding to a second side should remove from the first.
        var model = new DockLayoutModel();
        var p = Panel();

        model.AddToAutoHide(p, AutoHideSide.Left);
        model.AddToAutoHide(p, AutoHideSide.Bottom);

        Assert.DoesNotContain(p, model.GetAutoHidePanels(AutoHideSide.Left));
        Assert.Contains(p, model.GetAutoHidePanels(AutoHideSide.Bottom));
    }

    [Fact]
    public void AddToAutoHide_NullPanel_DoesNotThrow()
    {
        var model = new DockLayoutModel();
        // Should silently return without throwing
        model.AddToAutoHide(null!, AutoHideSide.Left);
    }

    // ── RemoveFromAutoHide ────────────────────────────────────────────────

    [Fact]
    public void RemoveFromAutoHide_ReturnsTrueWhenFound()
    {
        var model = new DockLayoutModel();
        var p = Panel();
        model.AddToAutoHide(p, AutoHideSide.Left);

        bool result = model.RemoveFromAutoHide(p);

        Assert.True(result);
    }

    [Fact]
    public void RemoveFromAutoHide_ReturnsFalseWhenNotFound()
    {
        var model = new DockLayoutModel();
        var p = Panel();

        bool result = model.RemoveFromAutoHide(p);

        Assert.False(result);
    }

    [Fact]
    public void RemoveFromAutoHide_SetsIsPinnedTrue()
    {
        var model = new DockLayoutModel();
        var p = Panel();
        model.AddToAutoHide(p, AutoHideSide.Left);
        Assert.False(p.IsPinned);

        model.RemoveFromAutoHide(p);

        Assert.True(p.IsPinned);
    }

    [Fact]
    public void RemoveFromAutoHide_PanelNoLongerOnSide()
    {
        var model = new DockLayoutModel();
        var p = Panel();
        model.AddToAutoHide(p, AutoHideSide.Right);

        model.RemoveFromAutoHide(p);

        Assert.DoesNotContain(p, model.GetAutoHidePanels(AutoHideSide.Right));
    }

    [Fact]
    public void RemoveFromAutoHide_NullPanel_ReturnsFalse()
    {
        var model = new DockLayoutModel();
        Assert.False(model.RemoveFromAutoHide(null!));
    }

    // ── HasAutoHidePanels ─────────────────────────────────────────────────

    [Fact]
    public void HasAutoHidePanels_FalseWhenEmpty()
    {
        var model = new DockLayoutModel();
        foreach (AutoHideSide side in Enum.GetValues<AutoHideSide>())
        {
            Assert.False(model.HasAutoHidePanels(side));
        }
    }

    [Fact]
    public void HasAutoHidePanels_TrueAfterAdd()
    {
        var model = new DockLayoutModel();
        model.AddToAutoHide(Panel(), AutoHideSide.Top);

        Assert.True(model.HasAutoHidePanels(AutoHideSide.Top));
    }

    [Fact]
    public void HasAutoHidePanels_FalseOnOtherSidesAfterAdd()
    {
        var model = new DockLayoutModel();
        model.AddToAutoHide(Panel(), AutoHideSide.Bottom);

        Assert.False(model.HasAutoHidePanels(AutoHideSide.Left));
        Assert.False(model.HasAutoHidePanels(AutoHideSide.Right));
        Assert.False(model.HasAutoHidePanels(AutoHideSide.Top));
    }

    [Fact]
    public void HasAnyAutoHidePanels_FalseWhenAllEmpty()
    {
        var model = new DockLayoutModel();
        Assert.False(model.HasAnyAutoHidePanels());
    }

    [Fact]
    public void HasAnyAutoHidePanels_TrueWithOnePanel()
    {
        var model = new DockLayoutModel();
        model.AddToAutoHide(Panel(), AutoHideSide.Right);

        Assert.True(model.HasAnyAutoHidePanels());
    }

    [Fact]
    public void HasAutoHidePanels_FalseAfterRemove()
    {
        var model = new DockLayoutModel();
        var p = Panel();
        model.AddToAutoHide(p, AutoHideSide.Left);
        model.RemoveFromAutoHide(p);

        Assert.False(model.HasAutoHidePanels(AutoHideSide.Left));
    }

    // ── GetAllTabGroups ───────────────────────────────────────────────────

    [Fact]
    public void GetAllTabGroups_EmptyWhenRootNull()
    {
        var model = new DockLayoutModel();
        Assert.Empty(model.GetAllTabGroups());
    }

    [Fact]
    public void GetAllTabGroups_ReturnsRootGroupDirectly()
    {
        var g = Group(Panel());
        var model = new DockLayoutModel(g);

        Assert.Single(model.GetAllTabGroups(), g);
    }

    [Fact]
    public void GetAllTabGroups_ReturnsBothSidesOfHorizontalSplit()
    {
        var left   = Group(Panel("p1", "L"));
        var right  = Group(Panel("p2", "R"));
        var split  = new DockSplitNode { Orientation = Orientation.Horizontal, SplitRatio = 0.5f, FirstChild = left, SecondChild = right };
        var model  = new DockLayoutModel(split);

        var groups = model.GetAllTabGroups().ToList();
        Assert.Equal(2, groups.Count);
        Assert.Contains(left,  groups);
        Assert.Contains(right, groups);
    }

    [Fact]
    public void GetAllTabGroups_ReturnsAllGroupsInNestedTree()
    {
        // Build: root[H] → left=G, right[V] → topRight=G, bottomRight=G
        var g1       = Group(Panel("p1"));
        var g2       = Group(Panel("p2"));
        var g3       = Group(Panel("p3"));
        var rightSplit = new DockSplitNode { Orientation = Orientation.Vertical, SplitRatio = 0.5f, FirstChild = g2, SecondChild = g3 };
        var root       = new DockSplitNode { Orientation = Orientation.Horizontal, SplitRatio = 0.3f, FirstChild = g1, SecondChild = rightSplit };
        var model      = new DockLayoutModel(root);

        var groups = model.GetAllTabGroups().ToList();
        Assert.Equal(3, groups.Count);
        Assert.Contains(g1, groups);
        Assert.Contains(g2, groups);
        Assert.Contains(g3, groups);
    }

    // ── JSON Serialization round-trip ─────────────────────────────────────

    [Fact]
    public void Json_RoundTrip_EmptyModel_ReturnsEmptyRootGroup()
    {
        var model = new DockLayoutModel();
        string json = DockLayoutSerializer.ToJson(model);
        var restored = DockLayoutSerializer.FromJson(json);

        Assert.NotNull(restored);
        // Serializer guarantees a non-null root — creates an empty placeholder TabGroup if needed
        var root = Assert.IsType<DockTabGroupNode>(restored!.RootNode);
        Assert.True(root.IsEmpty);
    }

    [Fact]
    public void Json_RoundTrip_SingleTabGroup_PreservesPanelTitleAndId()
    {
        var p      = new DockPanelNode { Title = "MyPanel" };
        var g      = Group(p);
        var model  = new DockLayoutModel(g);

        string json    = DockLayoutSerializer.ToJson(model);
        var restored   = DockLayoutSerializer.FromJson(json);

        var restoredGroup = restored!.GetAllTabGroups().Single();
        Assert.Single(restoredGroup.Panels);
        Assert.Equal("MyPanel", restoredGroup.Panels[0].Title);
    }

    [Fact]
    public void Json_RoundTrip_HorizontalSplit_PreservesOrientationAndRatio()
    {
        var left  = Group(Panel("p1", "Left"));
        var right = Group(Panel("p2", "Right"));
        var split = new DockSplitNode { Orientation = Orientation.Horizontal, SplitRatio = 0.4f, FirstChild = left, SecondChild = right };
        var model = new DockLayoutModel(split);

        string json  = DockLayoutSerializer.ToJson(model);
        var restored = DockLayoutSerializer.FromJson(json);

        var restoredSplit = Assert.IsType<DockSplitNode>(restored!.RootNode);
        Assert.Equal(Orientation.Horizontal, restoredSplit.Orientation);
        Assert.InRange(restoredSplit.SplitRatio, 0.39f, 0.41f);
    }

    [Fact]
    public void Json_RoundTrip_ActivePanel_IsPreserved()
    {
        var p1 = new DockPanelNode { Title = "A" };
        var p2 = new DockPanelNode { Title = "B" };
        var g  = Group(p1, p2);
        g.SetActivePanel(p2.Id);
        var model = new DockLayoutModel(g);

        string json  = DockLayoutSerializer.ToJson(model);
        var restored = DockLayoutSerializer.FromJson(json);

        var rg = restored!.GetAllTabGroups().Single();
        Assert.Equal(2, rg.Panels.Count);
        Assert.Equal(p2.Id, rg.ActivePanelId);
    }

    [Fact]
    public void Json_RoundTrip_IsDocumentArea_IsPreserved()
    {
        var g = Group(Panel());
        g.IsDocumentArea = true;
        var model = new DockLayoutModel(g);

        string json  = DockLayoutSerializer.ToJson(model);
        var restored = DockLayoutSerializer.FromJson(json);

        var rg = restored!.GetAllTabGroups().Single();
        Assert.True(rg.IsDocumentArea);
    }

    [Fact]
    public void Json_RoundTrip_ThreePanelNestedSplit()
    {
        var p1 = new DockPanelNode { Title = "Left" };
        var p2 = new DockPanelNode { Title = "TopRight" };
        var p3 = new DockPanelNode { Title = "BottomRight" };
        var g1 = Group(p1);
        var g2 = Group(p2);
        var g3 = Group(p3);
        var inner = new DockSplitNode { Orientation = Orientation.Vertical,   SplitRatio = 0.5f, FirstChild = g2, SecondChild = g3 };
        var root  = new DockSplitNode { Orientation = Orientation.Horizontal, SplitRatio = 0.3f, FirstChild = g1, SecondChild = inner };
        var model = new DockLayoutModel(root);

        string json  = DockLayoutSerializer.ToJson(model);
        var restored = DockLayoutSerializer.FromJson(json);

        var titles = restored!.GetAllTabGroups()
            .SelectMany(g => g.Panels)
            .Select(p => p.Title)
            .OrderBy(t => t)
            .ToList();

        Assert.Equal(new[] { "BottomRight", "Left", "TopRight" }, titles);
    }

    // ── JSON Round-trip: new properties (Phase 4.4) ───────────────────────

    [Fact]
    public void Json_RoundTrip_Family_IsPreserved()
    {
        var p = new DockPanelNode { Title = "Toolbox", Family = "editors" };
        var model = new DockLayoutModel(Group(p));

        string json  = DockLayoutSerializer.ToJson(model);
        var restored = DockLayoutSerializer.FromJson(json);

        var rp = restored!.GetAllTabGroups().Single().Panels[0];
        Assert.Equal("editors", rp.Family);
    }

    [Fact]
    public void Json_RoundTrip_CanAutoHide_False_IsPreserved()
    {
        var p = new DockPanelNode { Title = "Fixed", CanAutoHide = false };
        var model = new DockLayoutModel(Group(p));

        string json  = DockLayoutSerializer.ToJson(model);
        var restored = DockLayoutSerializer.FromJson(json);

        var rp = restored!.GetAllTabGroups().Single().Panels[0];
        Assert.False(rp.CanAutoHide);
    }

    [Fact]
    public void Json_RoundTrip_DrawerSize_IsPreserved()
    {
        var p = new DockPanelNode { Title = "Side", DrawerSize = 350 };
        var model = new DockLayoutModel(Group(p));

        string json  = DockLayoutSerializer.ToJson(model);
        var restored = DockLayoutSerializer.FromJson(json);

        var rp = restored!.GetAllTabGroups().Single().Panels[0];
        Assert.Equal(350, rp.DrawerSize);
    }

    [Fact]
    public void Json_RoundTrip_AllowedZones_IsPreserved()
    {
        var p = new DockPanelNode
        {
            Title = "Restricted",
            AllowedZones = new List<DockZone> { DockZone.Left, DockZone.Right }.AsReadOnly()
        };
        var model = new DockLayoutModel(Group(p));

        string json  = DockLayoutSerializer.ToJson(model);
        var restored = DockLayoutSerializer.FromJson(json);

        var rp = restored!.GetAllTabGroups().Single().Panels[0];
        Assert.NotNull(rp.AllowedZones);
        Assert.Equal(2, rp.AllowedZones!.Count);
        Assert.Contains(DockZone.Left,  rp.AllowedZones);
        Assert.Contains(DockZone.Right, rp.AllowedZones);
    }

    [Fact]
    public void Json_RoundTrip_AllowedZones_Null_IsPreserved()
    {
        var p = new DockPanelNode { Title = "Unrestricted", AllowedZones = null };
        var model = new DockLayoutModel(Group(p));

        string json  = DockLayoutSerializer.ToJson(model);
        var restored = DockLayoutSerializer.FromJson(json);

        var rp = restored!.GetAllTabGroups().Single().Panels[0];
        Assert.Null(rp.AllowedZones);
    }

    // ── Non-regression: LayoutChanged NOT fired for ActivePanelId (Phase 4.4) ─

    [Fact]
    public void LayoutChanged_NotFired_WhenActivePanelIdChanges()
    {
        // Regression guard: DockLayoutModel.OnNodePropertyChanged must NOT propagate
        // LayoutChanged for ActivePanelId/ActivePanel changes (would cause full visual
        // rebuild on every Ctrl+Tab press).
        var p1 = new DockPanelNode { Title = "A" };
        var p2 = new DockPanelNode { Title = "B" };
        var g  = Group(p1, p2);
        var model = new DockLayoutModel(g);

        int layoutChangedCount = 0;
        model.LayoutChanged += (_, _) => layoutChangedCount++;

        // Switch tab — should NOT fire LayoutChanged
        g.SetActivePanel(p2.Id);
        g.SetActivePanel(p1.Id);

        Assert.Equal(0, layoutChangedCount);
    }

    [Fact]
    public void LayoutChanged_Fired_WhenTitleChanges()
    {
        // Structural/visual property changes SHOULD still propagate LayoutChanged.
        var p = new DockPanelNode { Title = "Before" };
        var model = new DockLayoutModel(Group(p));

        int layoutChangedCount = 0;
        model.LayoutChanged += (_, _) => layoutChangedCount++;

        p.Title = "After"; // non-ActivePanel property

        Assert.True(layoutChangedCount > 0);
    }

    // ── Non-regression: Clear() clears auto-hide store (Phase 4.4) ──────────

    [Fact]
    public void Clear_RemovesAllPanels_FromAutoHideStore()
    {
        var p1 = new DockPanelNode { Title = "A" };
        var p2 = new DockPanelNode { Title = "B" };
        var model = new DockLayoutModel(Group(new DockPanelNode { Title = "Dummy" }));
        model.AddToAutoHide(p1, AutoHideSide.Left);
        model.AddToAutoHide(p2, AutoHideSide.Right);

        model.Clear();

        Assert.False(model.HasAnyAutoHidePanels());
        Assert.Empty(model.GetAutoHidePanels(AutoHideSide.Left));
        Assert.Empty(model.GetAutoHidePanels(AutoHideSide.Right));
    }

    [Fact]
    public void Clear_AutoHidePanelPropertyChanges_DoNotFireLayoutChanged_After_Clear()
    {
        // After Clear(), auto-hide panels should be fully unsubscribed.
        // Any property change on them must NOT fire LayoutChanged any more.
        var p = new DockPanelNode { Title = "AH" };
        var model = new DockLayoutModel(Group(new DockPanelNode { Title = "Root" }));
        model.AddToAutoHide(p, AutoHideSide.Bottom);

        model.Clear();

        int count = 0;
        model.LayoutChanged += (_, _) => count++;

        p.Title = "Changed after clear"; // should not trigger LayoutChanged
        Assert.Equal(0, count);
    }
}
