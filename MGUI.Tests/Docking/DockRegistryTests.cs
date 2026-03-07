using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Tests.Docking;

/// <summary>
/// Phase 4.2 — Unit tests for DockableRegistry and DockableDefinition.
/// </summary>
public class DockRegistryTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static DockableDefinition Def(string id, string title = "Test")
        => new DockableDefinition(id, title)
        {
            ContentFactory = () => null!
        };

    // ══════════════════════════════════════════════════════════════════════
    // Register / Unregister / TryGetById
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Register_AddsDefinition()
    {
        var registry = new DockableRegistry();
        registry.Register(Def("tool1"));

        Assert.True(registry.TryGetById("tool1", out var def));
        Assert.Equal("tool1", def!.DockableId);
    }

    [Fact]
    public void Register_ReplacesExisting_WithSameId()
    {
        var registry = new DockableRegistry();
        registry.Register(Def("tool1", "First"));
        registry.Register(Def("tool1", "Second"));

        registry.TryGetById("tool1", out var def);
        Assert.Equal("Second", def!.Title);
    }

    [Fact]
    public void Register_NullDefinition_Throws()
    {
        var registry = new DockableRegistry();
        Assert.Throws<System.ArgumentNullException>(() => registry.Register(null!));
    }

    [Fact]
    public void Unregister_ReturnsFalse_ForUnknownId()
    {
        var registry = new DockableRegistry();
        Assert.False(registry.Unregister("not-there"));
    }

    [Fact]
    public void Unregister_ReturnsTrue_AndRemovesDefinition()
    {
        var registry = new DockableRegistry();
        registry.Register(Def("tool1"));

        bool removed = registry.Unregister("tool1");

        Assert.True(removed);
        Assert.False(registry.TryGetById("tool1", out _));
    }

    [Fact]
    public void Unregister_AlsoClearsVisibility()
    {
        var registry = new DockableRegistry();
        registry.Register(Def("tool1"));
        // Mark as visible
        registry.SyncVisibility(new[] { "tool1" });
        Assert.True(registry.IsVisible("tool1"));

        registry.Unregister("tool1");

        // After unregister, the id is gone from the visible set
        Assert.False(registry.IsVisible("tool1"));
    }

    [Fact]
    public void TryGetById_ReturnsFalse_ForEmptyId()
    {
        var registry = new DockableRegistry();
        Assert.False(registry.TryGetById("", out _));
        Assert.False(registry.TryGetById(null!, out _));
    }

    // ══════════════════════════════════════════════════════════════════════
    // SyncVisibility
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SyncVisibility_MarksSuppliedIds_AsVisible()
    {
        var registry = new DockableRegistry();
        registry.Register(Def("a"));
        registry.Register(Def("b"));

        registry.SyncVisibility(new[] { "a" });

        Assert.True(registry.IsVisible("a"));
        Assert.False(registry.IsVisible("b"));
    }

    [Fact]
    public void SyncVisibility_ClearsOldVisible_WhenCalledAgain()
    {
        var registry = new DockableRegistry();
        registry.Register(Def("a"));
        registry.Register(Def("b"));
        registry.SyncVisibility(new[] { "a", "b" });

        registry.SyncVisibility(new[] { "b" }); // only b now

        Assert.False(registry.IsVisible("a"));
        Assert.True(registry.IsVisible("b"));
    }

    // ══════════════════════════════════════════════════════════════════════
    // NotifyShown / NotifyHidden / NotifyClosed / NotifyActivated
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void NotifyShown_FiresOnShown_Event()
    {
        var registry = new DockableRegistry();
        registry.Register(Def("tool1"));
        DockableDefinition? fired = null;
        registry.OnShown += (_, d) => fired = d;

        registry.NotifyShown("tool1");

        Assert.NotNull(fired);
        Assert.Equal("tool1", fired!.DockableId);
    }

    [Fact]
    public void NotifyShown_DoesNotFireTwice_IfAlreadyVisible()
    {
        var registry = new DockableRegistry();
        registry.Register(Def("tool1"));
        int count = 0;
        registry.OnShown += (_, _) => count++;

        registry.NotifyShown("tool1");
        registry.NotifyShown("tool1"); // second call — already visible

        Assert.Equal(1, count);
    }

    [Fact]
    public void NotifyHidden_FiresOnHidden_AndMarksInvisible()
    {
        var registry = new DockableRegistry();
        registry.Register(Def("tool1"));
        registry.NotifyShown("tool1");
        DockableDefinition? fired = null;
        registry.OnHidden += (_, d) => fired = d;

        registry.NotifyHidden("tool1");

        Assert.NotNull(fired);
        Assert.False(registry.IsVisible("tool1"));
    }

    [Fact]
    public void NotifyClosed_FiresOnClosed_AndMarksInvisible()
    {
        var registry = new DockableRegistry();
        registry.Register(Def("tool1"));
        registry.NotifyShown("tool1");
        DockableDefinition? fired = null;
        registry.OnClosed += (_, d) => fired = d;

        registry.NotifyClosed("tool1");

        Assert.NotNull(fired);
        Assert.False(registry.IsVisible("tool1"));
    }

    [Fact]
    public void NotifyActivated_FiresOnActivated_Event()
    {
        var registry = new DockableRegistry();
        registry.Register(Def("tool1"));
        DockableDefinition? fired = null;
        registry.OnActivated += (_, d) => fired = d;

        registry.NotifyActivated("tool1");

        Assert.NotNull(fired);
    }

    [Fact]
    public void NotifyShown_ForUnknownId_DoesNotThrow()
    {
        var registry = new DockableRegistry();
        // Should silently do nothing for unregistered id
        registry.NotifyShown("unknown");
    }

    // ══════════════════════════════════════════════════════════════════════
    // DockableDefinition.CreatePanelNode
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void CreatePanelNode_HasCorrectId_AndTitle()
    {
        var def = new DockableDefinition("myTool", "My Tool")
        {
            ContentFactory = () => null!,
            CanClose    = false,
            CanFloat    = false,
            CanAutoHide = false
        };

        var panel = def.CreatePanelNode();

        Assert.Equal("myTool", panel.Id);
        Assert.Equal("My Tool", panel.Title);
        Assert.False(panel.CanClose);
        Assert.False(panel.CanFloat);
        Assert.False(panel.CanAutoHide);
    }

    [Fact]
    public void CreatePanelNode_CopiesCanAutoHide()
    {
        // Regression: CreatePanelNode was previously forgetting to copy CanAutoHide.
        var def = new DockableDefinition("t", "T")
        {
            CanAutoHide    = false,
            ContentFactory = () => null!
        };

        var panel = def.CreatePanelNode();

        Assert.False(panel.CanAutoHide);
    }

    [Fact]
    public void CreatePanelNode_CopiesFamily_AndAllowedZones()
    {
        var def = new DockableDefinition("t", "T")
        {
            Family         = "editors",
            AllowedZones   = new List<DockZone> { DockZone.Left }.AsReadOnly(),
            ContentFactory = () => null!
        };

        var panel = def.CreatePanelNode();

        Assert.Equal("editors", panel.Family);
        Assert.NotNull(panel.AllowedZones);
        Assert.Single(panel.AllowedZones!);
        Assert.Equal(DockZone.Left, panel.AllowedZones![0]);
    }
}

