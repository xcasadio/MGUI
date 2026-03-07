using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI.Docking;

namespace MGUI.Core.UI.Docking.DockLayout;

/// <summary>
/// Represents a leaf node in the docking layout - an individual panel (tool window or document).
/// </summary>
public class DockPanelNode : DockNode
{
    private string _title;
    /// <summary>
    /// Display title of the panel (shown in tab header).
    /// </summary>
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    private object _icon;
    /// <summary>
    /// Icon for the panel. Can be a Texture2D, string path, or any other representation.
    /// Interpretation is left to the view layer.
    /// </summary>
    public object Icon
    {
        get => _icon;
        set
        {
            if (_icon != value)
            {
                _icon = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Factory function to create the panel's content on-demand.
    /// Allows lazy initialization of panel content.
    /// </summary>
    public Func<MGElement> ContentFactory { get; set; }

    private bool _canClose;
    /// <summary>
    /// Indicates whether the panel can be closed by the user.
    /// Default: true.
    /// </summary>
    public bool CanClose
    {
        get => _canClose;
        set
        {
            if (_canClose != value)
            {
                _canClose = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _canFloat;
    /// <summary>
    /// Indicates whether the panel can be detached into a floating window.
    /// Phase 2 feature. Default: true.
    /// </summary>
    public bool CanFloat
    {
        get => _canFloat;
        set
        {
            if (_canFloat != value)
            {
                _canFloat = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _canAutoHide = true;
    /// <summary>
    /// Whether the user is allowed to pin/unpin this panel.
    /// When false, the pin button is hidden. Default: true.
    /// </summary>
    public bool CanAutoHide
    {
        get => _canAutoHide;
        set
        {
            if (_canAutoHide != value)
            {
                _canAutoHide = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isPinned = true;
    /// <summary>
    /// Indicates whether the panel is pinned (always visible) or auto-hidden (in a strip).
    /// Default: true (pinned).
    /// </summary>
    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned != value)
            {
                _isPinned = value;
                OnPropertyChanged();
            }
        }
    }

    private AutoHideSide _autoHideSide = AutoHideSide.Left;
    /// <summary>
    /// The edge of the host where this panel's languette appears when it is auto-hidden.
    /// Inferred from the panel's position when the user unpins it.
    /// </summary>
    public AutoHideSide AutoHideSide
    {
        get => _autoHideSide;
        set
        {
            if (_autoHideSide != value)
            {
                _autoHideSide = value;
                OnPropertyChanged();
            }
        }
    }

    private int _drawerSize = 200;
    /// <summary>
    /// Width (for Left/Right sides) or height (for Top/Bottom sides) of the auto-hide drawer in pixels.
    /// Persisted so the drawer reopens at the same size as the user left it.
    /// </summary>
    public int DrawerSize
    {
        get => _drawerSize;
        set
        {
            if (_drawerSize != value)
            {
                _drawerSize = Math.Max(60, value);
                OnPropertyChanged();
            }
        }
    }

    private DockableType _dockableType;
    /// <summary>
    /// The type of this dockable (Tool or Document).
    /// Influences docking rules (e.g., Documents go to the center area, Tools to the sides).
    /// Default: <see cref="DockableType.Tool"/>.
    /// </summary>
    public DockableType DockableType
    {
        get => _dockableType;
        set
        {
            if (_dockableType != value)
            {
                _dockableType = value;
                OnPropertyChanged();
            }
        }
    }

    private string _family;
    /// <summary>
    /// Optional family tag inherited from <see cref="DockableDefinition.Family"/>.
    /// When set, this panel can only tab-dock (Center zone) with panels that share the same family.
    /// Null = no family restriction.
    /// </summary>
    public string Family
    {
        get => _family;
        set
        {
            if (_family != value)
            {
                _family = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The tab group this panel belonged to just before it was sent to the auto-hide store.
    /// Set by the host when the panel is unpinned; cleared when it is repinned or removed.
    /// Allows the host to restore the panel to its original position without relying on
    /// any external side-dictionary.
    /// </summary>
    internal DockTabGroupNode AutoHideReturnGroup { get; set; }

    /// <summary>
    /// The zone (Left/Right/Top/Bottom) that the panel's group occupied within its parent
    /// <see cref="DockSplitNode"/> when the panel was unpinned.
    /// <see cref="DockZone.None"/> when the group had no split parent (was the root, or shared).
    /// Used by the fallback restore path in RepinPanel when the original group no longer exists.
    /// </summary>
    internal DockZone AutoHideReturnZone { get; set; } = DockZone.None;

    /// <summary>
    /// The fraction of the split that the panel's group occupied when unpinned.
    /// Null when the group had no split parent.  Paired with <see cref="AutoHideReturnZone"/>.
    /// </summary>
    internal float? AutoHideReturnSplitRatio { get; set; }

    private IReadOnlyList<DockZone> _allowedZones;
    /// <summary>
    /// Optional allow-list of drag zones inherited from <see cref="DockableDefinition.AllowedZones"/>.
    /// When non-null, the panel may only be dropped into one of the listed zones.
    /// Null = all zones permitted.
    /// </summary>
    public IReadOnlyList<DockZone> AllowedZones
    {
        get => _allowedZones;
        set
        {
            if (_allowedZones != value)
            {
                _allowedZones = value;
                OnPropertyChanged();
            }
        }
    }

    // Cache for content instance
    private MGElement _cachedContent;

    /// <summary>
    /// Creates a new DockPanelNode with default settings.
    /// </summary>
    public DockPanelNode() : base()
    {
        _title = "Untitled";
        _canClose = true;
        _canFloat = true;
        _isPinned = true;
    }

    /// <summary>
    /// Creates a new DockPanelNode with specified ID and default settings.
    /// </summary>
    /// <param name="id">Unique identifier for this node.</param>
    public DockPanelNode(string id) : base(id)
    {
        _title = "Untitled";
        _canClose = true;
        _canFloat = true;
        _isPinned = true;
    }

    /// <summary>
    /// Gets or creates the content for this panel.
    /// Content is created on first access using ContentFactory and then cached.
    /// </summary>
    /// <returns>The MGElement content, or null if ContentFactory is not set.</returns>
    public MGElement GetOrCreateContent()
    {
        if (_cachedContent == null && ContentFactory != null)
        {
            _cachedContent = ContentFactory();
        }
        return _cachedContent;
    }

    /// <summary>
    /// Clears the cached content, forcing recreation on next GetOrCreateContent() call.
    /// Useful for refreshing panel content or freeing resources.
    /// Callers that need to clean up data bindings should do so on the returned content
    /// BEFORE calling this method.
    /// </summary>
    public MGElement ClearCachedContent()
    {
        var previous = _cachedContent;
        _cachedContent = null;
        return previous;
    }

    /// <summary>
    /// Gets the currently cached content without creating it if it doesn't exist.
    /// </summary>
    /// <returns>The cached content, or null if not yet created.</returns>
    public MGElement GetCachedContent()
    {
        return _cachedContent;
    }

    /// <summary>
    /// Checks if the content has been created and cached.
    /// </summary>
    public bool IsContentCreated => _cachedContent != null;

    /// <summary>
    /// Panel nodes are leaf nodes and have no children.
    /// </summary>
    public override IEnumerable<DockNode> GetChildren()
    {
        return Enumerable.Empty<DockNode>();
    }

    /// <summary>
    /// Panel nodes have no children, so this is a no-op.
    /// </summary>
    public override void RemoveChild(DockNode child)
    {
        // Panel nodes don't have children
    }

    /// <summary>
    /// Calculates the effective minimum width for a panel.
    /// </summary>
    /// <returns>The minimum width in pixels for a panel (default: 150).</returns>
    public override int CalculateEffectiveMinWidth()
    {
        // Minimum width for a single panel (enough for a tab and some content)
        return 150;
    }

    /// <summary>
    /// Calculates the effective minimum height for a panel.
    /// </summary>
    /// <returns>The minimum height in pixels for a panel (default: 100).</returns>
    public override int CalculateEffectiveMinHeight()
    {
        // Minimum height for a single panel (tab header + some content space)
        return 100;
    }

    public override string ToString()
    {
        return $"Panel (Id: {Id}, Title: '{Title}', Content: {(IsContentCreated ? "Created" : "Not Created")})";
    }
}