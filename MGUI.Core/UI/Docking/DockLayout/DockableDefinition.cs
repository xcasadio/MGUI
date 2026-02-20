using System;
using MGUI.Core.UI.Docking;

namespace MGUI.Core.UI.Docking.DockLayout;

/// <summary>
/// Holds metadata for a dockable panel registered in the docking system.
/// Used by the DockableRegistry to manage visible/hidden states.
/// </summary>
public class DockableDefinition
{
    /// <summary>
    /// Unique identifier for this dockable. Must be stable across sessions (used in saved layouts).
    /// </summary>
    public string DockableId { get; }

    private string _title;
    /// <summary>
    /// Display title shown in tab headers and menus.
    /// </summary>
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value ?? throw new ArgumentNullException(nameof(value));
            }
        }
    }

    /// <summary>
    /// Optional icon identifier. Interpretation (texture path, icon name…) is left to the view layer.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Whether the user can close this panel. Default: true.
    /// </summary>
    public bool CanClose { get; set; } = true;

    /// <summary>
    /// Whether the panel can be detached into a floating window. Default: true.
    /// </summary>
    public bool CanFloat { get; set; } = true;

    /// <summary>
    /// Whether the panel can be auto-hidden (pinned/unpinned). Default: true.
    /// </summary>
    public bool CanAutoHide { get; set; } = true;

    /// <summary>
    /// Whether the panel can be docked alongside other panels of different types.
    /// Documents can only tab with other Documents; Tools can only tab with other Tools.
    /// Default: follows <see cref="DockableType"/> rules.
    /// </summary>
    public DockableType DockableType { get; set; } = DockableType.Tool;

    /// <summary>
    /// Factory function that creates the MGElement content for this panel.
    /// Called lazily when the panel is first shown.
    /// </summary>
    public Func<MGElement> ContentFactory { get; set; }

    /// <summary>
    /// Default dock position hint when first shown. Null = no preference.
    /// </summary>
    public DockZone? DefaultDockZone { get; set; }

    /// <summary>
    /// Creates a new DockableDefinition.
    /// </summary>
    /// <param name="dockableId">Unique, stable identifier for this dockable.</param>
    /// <param name="title">Display title.</param>
    public DockableDefinition(string dockableId, string title)
    {
        if (string.IsNullOrWhiteSpace(dockableId))
            throw new ArgumentException("DockableId cannot be null or empty.", nameof(dockableId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be null or empty.", nameof(title));

        DockableId = dockableId;
        _title = title;
    }

    /// <summary>
    /// Creates a <see cref="DockPanelNode"/> pre-configured from this definition.
    /// </summary>
    public DockPanelNode CreatePanelNode()
    {
        return new DockPanelNode(DockableId)
        {
            Title = Title,
            Icon = Icon,
            CanClose = CanClose,
            CanFloat = CanFloat,
            IsPinned = true,
            DockableType = DockableType,
            ContentFactory = ContentFactory
        };
    }

    public override string ToString() => $"DockableDefinition({DockableId}, \"{Title}\", {DockableType})";
}
