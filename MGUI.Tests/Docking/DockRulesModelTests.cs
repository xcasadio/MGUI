using MGUI.Core.UI;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Tests.Docking;

/// <summary>
/// Task 17.4 — Unit tests for docking rules.
///
/// The full CanDockTo / GetForbiddenZones logic resides in MGDockHost
/// (which requires a MonoGame window) and is covered by integration tests.
/// These unit tests verify the pure-model properties that feed into those rules:
///   • AllowedZones allow-list on DockPanelNode
///   • Family same-family restriction for tab-docking
///   • DockableType + IsDocumentArea flags
///
/// A local helper (CanDock) mirrors the AllowedZones and Family portions of
/// MGDockHost.CanDockTo so the rule logic is exercised without a host window.
/// </summary>
public class DockRulesModelTests
{
    // ── Local mirror of CanDockTo (AllowedZones + Family parts) ──────────
    private static bool CanDock(DockPanelNode panel, DockTabGroupNode target, DockZone zone)
    {
        if (panel == null || target == null)
            return true;

        // AllowedZones restriction
        if (panel.AllowedZones != null && !panel.AllowedZones.Contains(zone))
            return false;

        // Family restriction — only for tab-docking (Center)
        if (zone == DockZone.Center && panel.Family != null)
        {
            foreach (var p in target.Panels)
            {
                if (p.Id == panel.Id) continue;
                if (p.Family != null && p.Family != panel.Family)
                    return false;
            }
        }

        return true;
    }

    private static DockPanelNode Panel(string? family = null, IReadOnlyList<DockZone>? allowedZones = null)
    {
        var p = new DockPanelNode { Title = "P" };
        if (family != null) p.Family = family;
        if (allowedZones != null) p.AllowedZones = allowedZones;
        return p;
    }

    private static DockTabGroupNode Group(params DockPanelNode[] panels)
    {
        var g = new DockTabGroupNode();
        foreach (var p in panels) g.AddPanel(p, -1);
        return g;
    }

    // ── AllowedZones restriction ──────────────────────────────────────────

    [Fact]
    public void AllowedZones_Null_AllZonesPermitted()
    {
        var p = Panel();
        var g = new DockTabGroupNode();

        foreach (DockZone z in Enum.GetValues<DockZone>())
        {
            if (z == DockZone.None) continue;
            Assert.True(CanDock(p, g, z), $"Zone {z} should be allowed when AllowedZones is null");
        }
    }

    [Fact]
    public void AllowedZones_RestrictsToListedZones()
    {
        var allowed = new[] { DockZone.Left, DockZone.Right };
        var p = Panel(allowedZones: allowed);
        var g = new DockTabGroupNode();

        Assert.True(CanDock(p, g, DockZone.Left));
        Assert.True(CanDock(p, g, DockZone.Right));
        Assert.False(CanDock(p, g, DockZone.Top));
        Assert.False(CanDock(p, g, DockZone.Bottom));
        Assert.False(CanDock(p, g, DockZone.Center));
    }

    [Fact]
    public void AllowedZones_CenterOnly_RejectsAllSplitZones()
    {
        var p = Panel(allowedZones: new[] { DockZone.Center });
        var g = new DockTabGroupNode();

        Assert.True (CanDock(p, g, DockZone.Center));
        Assert.False(CanDock(p, g, DockZone.Left));
        Assert.False(CanDock(p, g, DockZone.Right));
        Assert.False(CanDock(p, g, DockZone.Top));
        Assert.False(CanDock(p, g, DockZone.Bottom));
    }

    [Fact]
    public void AllowedZones_Property_IsReadWriteable()
    {
        var p = Panel();
        Assert.Null(p.AllowedZones);

        p.AllowedZones = new[] { DockZone.Left };

        Assert.NotNull(p.AllowedZones);
        Assert.Contains(DockZone.Left, p.AllowedZones);
    }

    // ── Family restriction ────────────────────────────────────────────────

    [Fact]
    public void Family_NullPanel_NullGroup_AllZonesPermitted()
    {
        var p = Panel(family: null);
        var g = Group(Panel(family: null));

        // No family restrictions on either side
        Assert.True(CanDock(p, g, DockZone.Center));
    }

    [Fact]
    public void Family_SameFamily_CenterAllowed()
    {
        var existing = Panel(family: "A");
        var newPanel = Panel(family: "A");
        var g = Group(existing);

        Assert.True(CanDock(newPanel, g, DockZone.Center));
    }

    [Fact]
    public void Family_DifferentFamily_CenterForbidden()
    {
        var existing = Panel(family: "A");
        var newPanel = Panel(family: "B");
        var g = Group(existing);

        Assert.False(CanDock(newPanel, g, DockZone.Center));
    }

    [Fact]
    public void Family_DifferentFamily_SplitZones_StillAllowed()
    {
        // Family restriction only applies to Center (tab-docking)
        var existing = Panel(family: "A");
        var newPanel = Panel(family: "B");
        var g = Group(existing);

        Assert.True(CanDock(newPanel, g, DockZone.Left));
        Assert.True(CanDock(newPanel, g, DockZone.Right));
        Assert.True(CanDock(newPanel, g, DockZone.Top));
        Assert.True(CanDock(newPanel, g, DockZone.Bottom));
    }

    [Fact]
    public void Family_NewPanelNullFamily_EmptyGroup_CenterAllowed()
    {
        var p = Panel(family: null);
        var g = new DockTabGroupNode(); // no existing panels

        Assert.True(CanDock(p, g, DockZone.Center));
    }

    [Fact]
    public void Family_NewPanelWithFamily_ExistingHasNoFamily_CenterAllowed()
    {
        // Existing panel has no family restriction → new panel with family can join
        var existing = Panel(family: null);
        var newPanel = Panel(family: "A");
        var g = Group(existing);

        Assert.True(CanDock(newPanel, g, DockZone.Center));
    }

    [Fact]
    public void Family_Property_IsReadWriteable()
    {
        var p = Panel();
        Assert.Null(p.Family);

        p.Family = "ToolGroup";

        Assert.Equal("ToolGroup", p.Family);
    }

    // ── DockableType & IsDocumentArea flags (model-level) ─────────────────

    [Fact]
    public void DockableType_DefaultIsTool()
    {
        var p = new DockPanelNode();
        Assert.Equal(DockableType.Tool, p.DockableType);
    }

    [Fact]
    public void DockableType_CanBeSetToDocument()
    {
        var p = new DockPanelNode { DockableType = DockableType.Document };
        Assert.Equal(DockableType.Document, p.DockableType);
    }

    [Fact]
    public void IsDocumentArea_DefaultIsFalse()
    {
        Assert.False(new DockTabGroupNode().IsDocumentArea);
    }

    [Fact]
    public void IsDocumentArea_CanBeSetTrue()
    {
        var g = new DockTabGroupNode { IsDocumentArea = true };
        Assert.True(g.IsDocumentArea);
    }

    [Fact]
    public void GetAllTabGroups_FindsDocumentAreaGroup()
    {
        var docGroup  = new DockTabGroupNode { IsDocumentArea = true };
        var toolGroup = new DockTabGroupNode { IsDocumentArea = false };
        docGroup .AddPanel(new DockPanelNode { Title = "Doc" },   -1);
        toolGroup.AddPanel(new DockPanelNode { Title = "Tool" }, -1);

        var split = new DockSplitNode { Orientation = Orientation.Horizontal, SplitRatio = 0.3f, FirstChild = toolGroup, SecondChild = docGroup };
        var model = new DockLayoutModel(split);

        var documentArea = model.GetAllTabGroups().FirstOrDefault(g => g.IsDocumentArea);
        Assert.NotNull(documentArea);
        Assert.Same(docGroup, documentArea);
    }

    // ── AllowedZones + Family combined ────────────────────────────────────

    [Fact]
    public void AllowedZonesCenterOnly_DifferentFamily_Forbidden()
    {
        var existing = Panel(family: "A");
        var newPanel = Panel(family: "B", allowedZones: new[] { DockZone.Center });
        var g = Group(existing);

        // Center is in AllowedZones but family differs → forbidden
        Assert.False(CanDock(newPanel, g, DockZone.Center));
        // Other zones also forbidden by AllowedZones
        Assert.False(CanDock(newPanel, g, DockZone.Left));
    }
}
