using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MGUI.Core.UI.Docking.DockLayout;

/// <summary>
/// Centralized registry of all <see cref="DockableDefinition"/>s known to the docking system.
/// Tracks which dockables are visible in the current layout and which are hidden.
/// </summary>
public class DockableRegistry
{
    private readonly Dictionary<string, DockableDefinition> _definitions = new();

    /// <summary>Read-only snapshot of all registered definitions.</summary>
    public IReadOnlyCollection<DockableDefinition> All => _definitions.Values.ToList().AsReadOnly();

    // ──────────────────────────────────────────────────────
    // Lifecycle events
    // ──────────────────────────────────────────────────────

    /// <summary>Raised when a dockable is shown (added to the layout).</summary>
    public event EventHandler<DockableDefinition> OnShown;

    /// <summary>Raised when a dockable is hidden (removed from the layout but still registered).</summary>
    public event EventHandler<DockableDefinition> OnHidden;

    /// <summary>Raised when a dockable is closed by the user (CanClose = true, removed permanently from layout).</summary>
    public event EventHandler<DockableDefinition> OnClosed;

    /// <summary>Raised when a dockable tab becomes the active selection in its group.</summary>
    public event EventHandler<DockableDefinition> OnActivated;

    // ──────────────────────────────────────────────────────
    // Visibility tracking (set from MGDockHost)
    // ──────────────────────────────────────────────────────
    private readonly HashSet<string> _visibleIds = new();

    // ──────────────────────────────────────────────────────
    // Registration
    // ──────────────────────────────────────────────────────

    /// <summary>
    /// Registers a <see cref="DockableDefinition"/> in the registry.
    /// If a definition with the same <see cref="DockableDefinition.DockableId"/> already exists it will be replaced.
    /// </summary>
    /// <param name="definition">The definition to register.</param>
    public void Register(DockableDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        _definitions[definition.DockableId] = definition;
    }

    /// <summary>
    /// Removes a definition from the registry.
    /// </summary>
    /// <param name="dockableId">The ID to unregister.</param>
    /// <returns>True if the definition was removed, false if not found.</returns>
    public bool Unregister(string dockableId)
    {
        if (string.IsNullOrWhiteSpace(dockableId))
            return false;

        _visibleIds.Remove(dockableId);
        return _definitions.Remove(dockableId);
    }

    // ──────────────────────────────────────────────────────
    // Lookups
    // ──────────────────────────────────────────────────────

    /// <summary>Returns the definition with the given ID, or null if not found.</summary>
    public bool TryGetById(string dockableId, out DockableDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(dockableId))
        {
            definition = null;
            return false;
        }
        return _definitions.TryGetValue(dockableId, out definition);
    }

    /// <summary>Returns the definition with the given ID, or null if not found.</summary>
    public DockableDefinition GetById(string dockableId)
    {
        if (string.IsNullOrWhiteSpace(dockableId))
            return null;
        _definitions.TryGetValue(dockableId, out var def);
        return def;
    }

    /// <summary>Returns all registered definitions.</summary>
    public IReadOnlyCollection<DockableDefinition> GetAll() =>
        _definitions.Values.ToList().AsReadOnly();

    /// <summary>Returns only definitions whose panels are currently visible in the layout.</summary>
    public IReadOnlyCollection<DockableDefinition> GetVisible() =>
        _definitions.Values.Where(d => _visibleIds.Contains(d.DockableId)).ToList().AsReadOnly();

    /// <summary>Returns only definitions whose panels are NOT currently visible in the layout.</summary>
    public IReadOnlyCollection<DockableDefinition> GetHidden() =>
        _definitions.Values.Where(d => !_visibleIds.Contains(d.DockableId)).ToList().AsReadOnly();

    /// <summary>Returns whether a dockable is currently visible in the layout.</summary>
    public bool IsVisible(string dockableId) => _visibleIds.Contains(dockableId);

    // ──────────────────────────────────────────────────────
    // Internal visibility tracking (called from MGDockHost)
    // ──────────────────────────────────────────────────────

    /// <summary>
    /// Marks the dockable with the given ID as visible and fires <see cref="OnShown"/>.
    /// Called by the host when a panel is added to the layout.
    /// </summary>
    internal void NotifyShown(string dockableId)
    {
        if (!_definitions.TryGetValue(dockableId, out var def)) return;

        bool wasHidden = _visibleIds.Add(dockableId); // returns true if added
        if (wasHidden)
        {
            OnShown?.Invoke(this, def);
        }
    }

    /// <summary>
    /// Marks the dockable as hidden and fires <see cref="OnHidden"/>.
    /// Called by the host when a panel is removed from the layout (without closing it).
    /// </summary>
    internal void NotifyHidden(string dockableId)
    {
        if (!_definitions.TryGetValue(dockableId, out var def)) return;

        bool wasVisible = _visibleIds.Remove(dockableId);
        if (wasVisible)
        {
            OnHidden?.Invoke(this, def);
        }
    }

    /// <summary>
    /// Marks the dockable as hidden and fires <see cref="OnClosed"/>.
    /// Called by the host when a panel is closed by the user.
    /// </summary>
    internal void NotifyClosed(string dockableId)
    {
        if (!_definitions.TryGetValue(dockableId, out var def)) return;

        _visibleIds.Remove(dockableId);
        OnClosed?.Invoke(this, def);
    }

    /// <summary>
    /// Fires <see cref="OnActivated"/> for the given dockable.
    /// Called by the host when a panel tab is selected.
    /// </summary>
    internal void NotifyActivated(string dockableId)
    {
        if (!_definitions.TryGetValue(dockableId, out var def)) return;
        OnActivated?.Invoke(this, def);
    }

    /// <summary>
    /// Synchronizes the visibility set from a full layout scan.
    /// Called by the host after rebuilding the visual tree.
    /// </summary>
    /// <param name="visibleIds">IDs of panels currently present in the layout.</param>
    internal void SyncVisibility(IEnumerable<string> visibleIds)
    {
        _visibleIds.Clear();
        foreach (var id in visibleIds)
        {
            _visibleIds.Add(id);
        }
    }
}
