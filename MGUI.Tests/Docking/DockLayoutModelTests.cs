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

    // ── Panel placements (Task T1) ────────────────────────────────────────

    [Fact]
    public void SetPlacement_ThenTryGetPlacement_ReturnsIt()
    {
        var model = new DockLayoutModel();
        var placement = new DockPanelPlacement("group-1", 2);

        model.SetPlacement("panel-1", placement);

        Assert.True(model.TryGetPlacement("panel-1", out var found));
        Assert.Same(placement, found);
    }

    [Fact]
    public void TryGetPlacement_ReturnsFalse_WhenNotSet()
    {
        var model = new DockLayoutModel();
        Assert.False(model.TryGetPlacement("missing", out var placement));
        Assert.Null(placement);
    }

    [Fact]
    public void SetPlacement_Overwrites_PreviousPlacement()
    {
        var model = new DockLayoutModel();
        model.SetPlacement("p", new DockPanelPlacement("g1", 0));
        model.SetPlacement("p", new DockPanelPlacement("g2", 5));

        Assert.True(model.TryGetPlacement("p", out var found));
        Assert.Equal("g2", found.GroupId);
        Assert.Equal(5, found.TabIndex);
    }

    [Fact]
    public void SetPlacement_NullOrEmptyId_Throws()
    {
        var model = new DockLayoutModel();
        var placement = new DockPanelPlacement("g", 0);

        Assert.Throws<ArgumentException>(() => model.SetPlacement(null!, placement));
        Assert.Throws<ArgumentException>(() => model.SetPlacement(string.Empty, placement));
    }

    [Fact]
    public void SetPlacement_NullPlacement_Throws()
    {
        var model = new DockLayoutModel();
        Assert.Throws<ArgumentNullException>(() => model.SetPlacement("p", null!));
    }

    [Fact]
    public void RemovePlacement_ReturnsTrue_WhenFound()
    {
        var model = new DockLayoutModel();
        model.SetPlacement("p", new DockPanelPlacement("g", 0));

        Assert.True(model.RemovePlacement("p"));
        Assert.False(model.TryGetPlacement("p", out _));
    }

    [Fact]
    public void RemovePlacement_ReturnsFalse_WhenNotFound()
    {
        var model = new DockLayoutModel();
        Assert.False(model.RemovePlacement("missing"));
    }

    [Fact]
    public void IsPlaceholderReferenced_True_WhenGroupIsReferenced()
    {
        var model = new DockLayoutModel();
        var g = new DockTabGroupNode();
        model.SetPlacement("p", new DockPanelPlacement(g.Id, 0));

        Assert.True(model.IsPlaceholderReferenced(g));
    }

    [Fact]
    public void IsPlaceholderReferenced_False_WhenGroupIsNotReferenced()
    {
        var model = new DockLayoutModel();
        var g = new DockTabGroupNode();

        Assert.False(model.IsPlaceholderReferenced(g));
    }

    [Fact]
    public void IsPlaceholderReferenced_False_ForNullGroup()
    {
        var model = new DockLayoutModel();
        Assert.False(model.IsPlaceholderReferenced(null!));
    }

    // ── Floating store (Task T1) ────────────────────────────────────────

    [Fact]
    public void DockFloatingGroup_NullGroup_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DockFloatingGroup(null!, 0, 0, 100, 100));
    }

    [Fact]
    public void AddFloatingGroup_AddsToFloatingGroups()
    {
        var model = new DockLayoutModel();
        var fg = new DockFloatingGroup(Group(Panel()), 10, 20, 300, 400);

        model.AddFloatingGroup(fg);

        Assert.Single(model.FloatingGroups, fg);
    }

    [Fact]
    public void AddFloatingGroup_RaisesLayoutChanged()
    {
        var model = new DockLayoutModel();
        var fg = new DockFloatingGroup(Group(Panel()), 0, 0, 10, 10);

        int count = 0;
        model.LayoutChanged += (_, _) => count++;

        model.AddFloatingGroup(fg);

        Assert.True(count > 0);
    }

    [Fact]
    public void AddFloatingGroup_NullGroup_Throws()
    {
        var model = new DockLayoutModel();
        Assert.Throws<ArgumentNullException>(() => model.AddFloatingGroup(null!));
    }

    [Fact]
    public void AddFloatingGroup_Duplicate_Throws()
    {
        var model = new DockLayoutModel();
        var fg = new DockFloatingGroup(Group(Panel()), 0, 0, 10, 10);
        model.AddFloatingGroup(fg);

        Assert.Throws<InvalidOperationException>(() => model.AddFloatingGroup(fg));
    }

    [Fact]
    public void RemoveFloatingGroup_ReturnsTrue_WhenRemoved()
    {
        var model = new DockLayoutModel();
        var fg = new DockFloatingGroup(Group(Panel()), 0, 0, 10, 10);
        model.AddFloatingGroup(fg);

        Assert.True(model.RemoveFloatingGroup(fg));
        Assert.Empty(model.FloatingGroups);
    }

    [Fact]
    public void RemoveFloatingGroup_ReturnsFalse_AndDoesNotRaiseLayoutChanged_WhenNotFound()
    {
        var model = new DockLayoutModel();
        var fg = new DockFloatingGroup(Group(Panel()), 0, 0, 10, 10);

        int count = 0;
        model.LayoutChanged += (_, _) => count++;

        Assert.False(model.RemoveFloatingGroup(fg));
        Assert.Equal(0, count);
    }

    [Fact]
    public void RemoveFloatingGroup_RaisesLayoutChanged_WhenRemoved()
    {
        var model = new DockLayoutModel();
        var fg = new DockFloatingGroup(Group(Panel()), 0, 0, 10, 10);
        model.AddFloatingGroup(fg);

        int count = 0;
        model.LayoutChanged += (_, _) => count++;

        model.RemoveFloatingGroup(fg);

        Assert.True(count > 0);
    }

    [Fact]
    public void FindFloatingGroupOf_ReturnsGroup_HoldingPanel()
    {
        var model = new DockLayoutModel();
        var p = Panel();
        var fg = new DockFloatingGroup(Group(p), 0, 0, 10, 10);
        model.AddFloatingGroup(fg);

        var found = model.FindFloatingGroupOf(p.Id);

        Assert.Same(fg, found);
    }

    [Fact]
    public void FindFloatingGroupOf_ReturnsNull_WhenPanelNotFloating()
    {
        var model = new DockLayoutModel();
        Assert.Null(model.FindFloatingGroupOf("missing"));
    }

    [Fact]
    public void FloatingGroups_PreservesAddOrder()
    {
        var model = new DockLayoutModel();
        var fg1 = new DockFloatingGroup(Group(Panel()), 0, 0, 10, 10);
        var fg2 = new DockFloatingGroup(Group(Panel()), 0, 0, 10, 10);

        model.AddFloatingGroup(fg1);
        model.AddFloatingGroup(fg2);

        Assert.Equal(new[] { fg1, fg2 }, model.FloatingGroups);
    }

    [Fact]
    public void Clear_EmptiesFloatingStore_AndPlacements()
    {
        var model = new DockLayoutModel(Group(new DockPanelNode { Title = "Root" }));
        var fg = new DockFloatingGroup(Group(Panel()), 0, 0, 10, 10);
        model.AddFloatingGroup(fg);
        model.SetPlacement("p", new DockPanelPlacement("g", 0));

        model.Clear();

        Assert.Empty(model.FloatingGroups);
        Assert.False(model.TryGetPlacement("p", out _));
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

    // ── Format 2.0 round trips (Task T5) ────────────────────────────────────

    [Fact]
    public void Json_RoundTrip_Version_Is_2_0()
    {
        var model = new DockLayoutModel(Group(Panel()));
        string json = DockLayoutSerializer.ToJson(model);

        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("2.0", document.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public void Json_RoundTrip_PlaceholderGroupReferencedByPlacement_IsKept()
    {
        var stays = new DockPanelNode { Title = "Stays" };
        var floated = new DockPanelNode { Title = "Floated" };
        var g = Group(stays);
        var model = new DockLayoutModel(g);

        // Simulate "Floated" having left g's sibling group, leaving it empty but referenced.
        var ghostGroup = new DockTabGroupNode();
        var split = new DockSplitNode { Orientation = Orientation.Horizontal, SplitRatio = 0.5f, FirstChild = g, SecondChild = ghostGroup };
        model.RootNode = split;
        model.SetPlacement(floated.Id, new DockPanelPlacement(ghostGroup.Id, 0));
        model.AddFloatingGroup(new DockFloatingGroup(Group(floated), 0, 0, 100, 100));

        string json = DockLayoutSerializer.ToJson(model);
        var restored = DockLayoutSerializer.FromJson(json);

        Assert.True(restored.TryGetPlacement(floated.Id, out var placement));
        var restoredGhost = restored.RootNode.FindNodeById(placement.GroupId) as DockTabGroupNode;
        Assert.NotNull(restoredGhost);
        Assert.True(restoredGhost.IsEmpty);
        Assert.True(restored.IsPlaceholderReferenced(restoredGhost));
    }

    [Fact]
    public void Json_RoundTrip_FloatingStore_PreservesGroupPanelsActiveTabAndBounds()
    {
        var p1 = new DockPanelNode { Title = "F1" };
        var p2 = new DockPanelNode { Title = "F2" };
        var fgGroup = Group(p1, p2);
        fgGroup.SetActivePanel(p2.Id);
        var model = new DockLayoutModel(Group(Panel()));
        model.AddFloatingGroup(new DockFloatingGroup(fgGroup, 12, 34, 320, 240));

        string json = DockLayoutSerializer.ToJson(model);
        var restored = DockLayoutSerializer.FromJson(json);

        var restoredFg = Assert.Single(restored.FloatingGroups);
        Assert.Equal(12, restoredFg.Left);
        Assert.Equal(34, restoredFg.Top);
        Assert.Equal(320, restoredFg.Width);
        Assert.Equal(240, restoredFg.Height);
        Assert.Equal(new[] { "F1", "F2" }, restoredFg.Group.Panels.Select(p => p.Title));
        Assert.Equal(restoredFg.Group.Panels[1].Id, restoredFg.Group.ActivePanelId);
    }

    [Fact]
    public void Json_RoundTrip_AutoHideSections_PreservesSideAndOrder()
    {
        var left1 = new DockPanelNode { Title = "L1" };
        var left2 = new DockPanelNode { Title = "L2" };
        var right1 = new DockPanelNode { Title = "R1" };
        var model = new DockLayoutModel(Group(Panel()));
        model.AddToAutoHide(left1, AutoHideSide.Left);
        model.AddToAutoHide(left2, AutoHideSide.Left);
        model.AddToAutoHide(right1, AutoHideSide.Right);

        string json = DockLayoutSerializer.ToJson(model);
        var restored = DockLayoutSerializer.FromJson(json);

        Assert.Equal(new[] { "L1", "L2" }, restored.GetAutoHidePanels(AutoHideSide.Left).Select(p => p.Title));
        Assert.Equal(new[] { "R1" }, restored.GetAutoHidePanels(AutoHideSide.Right).Select(p => p.Title));
        Assert.Empty(restored.GetAutoHidePanels(AutoHideSide.Top));
        Assert.Empty(restored.GetAutoHidePanels(AutoHideSide.Bottom));
        Assert.All(restored.GetAllAutoHidePanels(), p => Assert.False(p.IsPinned));
    }

    [Fact]
    public void Json_RoundTrip_Placements_IncludingOneWhosePanelExistsNowhere()
    {
        var docked = new DockPanelNode { Title = "Docked" };
        var group = Group(docked);
        var model = new DockLayoutModel(group);
        model.SetPlacement("closed-panel-id", new DockPanelPlacement(group.Id, 3));

        string json = DockLayoutSerializer.ToJson(model);
        var restored = DockLayoutSerializer.FromJson(json);

        Assert.True(restored.TryGetPlacement("closed-panel-id", out var placement));
        Assert.Equal(group.Id, placement.GroupId);
        Assert.Equal(3, placement.TabIndex);
        Assert.Null(restored.FindPanelById("closed-panel-id"));
    }

    [Fact]
    public void FromJson_VersionOther_Than_2_0_Throws()
    {
        string json = "{\"version\":\"1.0\",\"rootNode\":{\"type\":\"TabGroup\",\"id\":\"g1\",\"panels\":[]}}";
        Assert.Throws<InvalidOperationException>(() => DockLayoutSerializer.FromJson(json));
    }

    [Fact]
    public void TryFromJson_VersionOther_Than_2_0_ReturnsFalse_WithDiagnostic()
    {
        string json = "{\"version\":\"1.0\",\"rootNode\":{\"type\":\"TabGroup\",\"id\":\"g1\",\"panels\":[]}}";
        bool success = DockLayoutSerializer.TryFromJson(json, null, out var model, out var diagnostics);

        Assert.False(success);
        Assert.Null(model);
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void FromJson_MalformedJson_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => DockLayoutSerializer.FromJson("{ not valid json"));
    }

    [Fact]
    public void TryFromJson_MalformedJson_ReturnsFalse_WithDiagnostic()
    {
        bool success = DockLayoutSerializer.TryFromJson("{ not valid json", null, out var model, out var diagnostics);

        Assert.False(success);
        Assert.Null(model);
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void TryFromJson_DuplicatePanelId_AcrossTreeAndFloating_ReturnsFalse()
    {
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

        bool success = DockLayoutSerializer.TryFromJson(json, null, out var model, out var diagnostics);

        Assert.False(success);
        Assert.Null(model);
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void TryFromJson_PlacementForADockedPanel_ReturnsFalse()
    {
        string json = """
        {
          "version": "2.0",
          "rootNode": { "type": "TabGroup", "id": "g1", "panels": [ { "id": "docked", "title": "A" } ] },
          "floatingGroups": [],
          "autoHide": [],
          "placements": [ { "panelId": "docked", "groupId": "g1", "tabIndex": 0 } ]
        }
        """;

        bool success = DockLayoutSerializer.TryFromJson(json, null, out var model, out var diagnostics);

        Assert.False(success);
        Assert.Null(model);
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void FromJson_PanelFactoryReturnsNull_SkipsPanel_AndDropsItsPlacement()
    {
        string json = """
        {
          "version": "2.0",
          "rootNode": { "type": "TabGroup", "id": "g1", "panels": [ { "id": "known", "title": "A" }, { "id": "unknown", "title": "B" } ] },
          "floatingGroups": [],
          "autoHide": [],
          "placements": [ { "panelId": "unknown", "groupId": "g2", "tabIndex": 0 } ]
        }
        """;

        var restored = DockLayoutSerializer.FromJson(json, panelId => panelId == "known" ? (() => null) : null);

        Assert.NotNull(restored.FindPanelById("known"));
        Assert.Null(restored.FindPanelById("unknown"));
        Assert.False(restored.TryGetPlacement("unknown", out _));
    }

    [Fact]
    public void TryFromJson_TabGroupWithMissingId_ReturnsFalse_WithDiagnostic_DoesNotThrow()
    {
        string json = """
        {
          "version": "2.0",
          "rootNode": { "type": "TabGroup", "panels": [] },
          "floatingGroups": [],
          "autoHide": [],
          "placements": []
        }
        """;

        bool success = DockLayoutSerializer.TryFromJson(json, null, out var model, out var diagnostics);

        Assert.False(success);
        Assert.Null(model);
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void TryFromJson_PanelWithMissingId_ReturnsFalse_WithDiagnostic_DoesNotThrow()
    {
        string json = """
        {
          "version": "2.0",
          "rootNode": { "type": "TabGroup", "id": "g1", "panels": [ { "title": "No id" } ] },
          "floatingGroups": [],
          "autoHide": [],
          "placements": []
        }
        """;

        bool success = DockLayoutSerializer.TryFromJson(json, null, out var model, out var diagnostics);

        Assert.False(success);
        Assert.Null(model);
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void FromJson_NodeWithMissingId_ThrowsInvalidOperationException_NotArgumentException()
    {
        string json = """
        {
          "version": "2.0",
          "rootNode": { "type": "TabGroup", "panels": [] },
          "floatingGroups": [],
          "autoHide": [],
          "placements": []
        }
        """;

        // Must fail through the documented diagnostic path (InvalidOperationException), never
        // through DockNode's constructor guard leaking out as an ArgumentException.
        Assert.Throws<InvalidOperationException>(() => DockLayoutSerializer.FromJson(json));
    }

    [Fact]
    public void FromJson_FloatingGroupWhosePanelIsUnknownToTheFactory_IsDropped()
    {
        string json = """
        {
          "version": "2.0",
          "rootNode": { "type": "TabGroup", "id": "g1", "panels": [ { "id": "known", "title": "A" } ] },
          "floatingGroups": [
            { "group": { "type": "TabGroup", "id": "fg1", "panels": [ { "id": "unknown", "title": "B" } ] }, "left": 0, "top": 0, "width": 100, "height": 100 }
          ],
          "autoHide": [],
          "placements": []
        }
        """;

        var restored = DockLayoutSerializer.FromJson(json, panelId => panelId == "known" ? (() => null) : null);

        Assert.Empty(restored.FloatingGroups);
        Assert.NotNull(restored.FindPanelById("known"));
    }
}
