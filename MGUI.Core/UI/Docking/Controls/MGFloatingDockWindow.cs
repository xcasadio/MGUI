using System;
using System.Linq;
using Microsoft.Xna.Framework;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// A floating (undocked) window that wraps a single <see cref="MGDockTabGroup"/>.
/// Created by <see cref="MGDockHost"/> when the user detaches a panel from the docked layout,
/// or when a drag is released outside the host bounds for a panel whose <see cref="DockPanelNode.CanFloat"/> is true.
/// The floating window is added as a <see cref="MGWindow.NestedWindows">NestedWindow</see>
/// of the host's parent window so it renders within the same viewport.
/// </summary>
public class MGFloatingDockWindow : MGWindow
{
    /// <summary>The <see cref="MGDockHost"/> that created and owns this floating window.</summary>
    public MGDockHost OwnerHost { get; }

    /// <summary>The tab group node model backing this floating window.</summary>
    public DockTabGroupNode GroupNode { get; }

    private MGDockTabGroup _tabGroup;
    /// <summary>The visual tab group control shown inside this window.</summary>
    public MGDockTabGroup TabGroup => _tabGroup;

    /// <summary>Saved window bounds captured just before a maximize operation so we can restore them later.</summary>
    private (int Left, int Top, int Width, int Height)? _preMaximizeBounds;

    // ──────────────────────────────────────────────────────────────────────
    // Constructor
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new floating dock window for <paramref name="initialPanel"/>.
    /// The caller is responsible for calling
    /// <see cref="MGWindow.AddNestedWindow"/> on the parent window afterwards.
    /// </summary>
    /// <param name="ownerHost">The docking host that owns this window.</param>
    /// <param name="initialPanel">The panel to show in this floating window.</param>
    /// <param name="left">Initial X position (screen-space).</param>
    /// <param name="top">Initial Y position (screen-space).</param>
    /// <param name="width">Initial width in pixels.</param>
    /// <param name="height">Initial height in pixels.</param>
    public MGFloatingDockWindow(
        MGDockHost ownerHost,
        DockPanelNode initialPanel,
        int left,
        int top,
        int width  = 320,
        int height = 260)
        : base(ownerHost.ParentWindow, left, top, width, height)
    {
        OwnerHost = ownerHost ?? throw new ArgumentNullException(nameof(ownerHost));
        if (initialPanel == null) throw new ArgumentNullException(nameof(initialPanel));

        // Window chrome
        IsDraggable      = true;
        IsUserResizable  = true;
        IsTitleBarVisible = true;

        // Create the model group and add the initial panel
        GroupNode = new DockTabGroupNode();
        GroupNode.AddPanel(initialPanel, -1);

        // Build the tab group visual
        _tabGroup = new MGDockTabGroup(ownerHost.ParentWindow, GroupNode)
        {
            HorizontalAlignment     = HorizontalAlignment.Stretch,
            VerticalAlignment       = VerticalAlignment.Stretch,
            // Maximize button is not meaningful in a floating window because
            // "filling the host" while floating does not make sense.
            // We leave to the user whether to hide the maximize button.
            OwnerDockHost           = ownerHost,
            OwnerFloatingWindow     = this
        };

        _tabGroup.PanelCloseRequested += OnPanelCloseRequested;

        // Maximize / restore the floating window to fill the desktop viewport
        _tabGroup.MaximizeRequested += (_, _) => MaximizeWindow();
        _tabGroup.RestoreRequested  += (_, _) => RestoreWindow();

        // Update title bar when the active tab changes
        UpdateTitle();
        GroupNode.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DockTabGroupNode.ActivePanel) ||
                args.PropertyName == nameof(DockTabGroupNode.ActivePanelId))
            {
                UpdateTitle();
            }
        };

        // Z-order: bring to front when the user clicks anywhere on this window
        MouseHandler.LMBPressedInside += (_, _) => BringToFront();

        SetContent(_tabGroup);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Public helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a panel to this floating window's tab group.
    /// </summary>
    public void AddPanel(DockPanelNode panel, int index = -1)
    {
        if (panel == null) throw new ArgumentNullException(nameof(panel));
        GroupNode.AddPanel(panel, index);
        UpdateTitle();
    }

    /// <summary>
    /// Removes a panel from this floating window's tab group by ID.
    /// Returns true if the panel was found and removed.
    /// </summary>
    public bool RemovePanel(string panelId)
    {
        if (string.IsNullOrEmpty(panelId)) return false;
        var panel = GroupNode.Panels.FirstOrDefault(p => p.Id == panelId);
        if (panel == null) return false;
        GroupNode.RemovePanelById(panelId);
        UpdateTitle();
        return true;
    }

    /// <summary>
    /// Brings this floating window to the top of the z-order relative to sibling nested windows.
    /// </summary>
    public void BringToFront()
    {
        ParentWindow?.BringToFront(this);
    }

    /// <summary>
    /// Maximises this floating window to cover the full desktop viewport, saving the
    /// previous bounds so they can be restored later.
    /// </summary>
    private void MaximizeWindow()
    {
        if (_tabGroup.IsMaximized) return;
        _preMaximizeBounds = (Left, Top, WindowWidth, WindowHeight);
        var screen = GetDesktop().ValidScreenBounds;
        Left          = screen.X;
        Top           = screen.Y;
        WindowWidth   = screen.Width;
        WindowHeight  = screen.Height;
        _tabGroup.IsMaximized = true;
        IsDraggable           = false;
        IsUserResizable       = false;
    }

    /// <summary>
    /// Restores this floating window to the bounds it had before <see cref="MaximizeWindow"/> was called.
    /// </summary>
    private void RestoreWindow()
    {
        if (!_tabGroup.IsMaximized) return;
        if (_preMaximizeBounds.HasValue)
        {
            var (l, t, w, h)  = _preMaximizeBounds.Value;
            Left              = l;
            Top               = t;
            WindowWidth       = w;
            WindowHeight      = h;
            _preMaximizeBounds = null;
        }
        _tabGroup.IsMaximized = false;
        IsDraggable           = true;
        IsUserResizable       = true;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Private
    // ──────────────────────────────────────────────────────────────────────

    private void OnPanelCloseRequested(object sender, DockPanelNode panel)
    {
        if (panel == null) return;

        GroupNode.RemovePanelById(panel.Id);
        OwnerHost.NotifyFloatingPanelClosed(panel);

        if (GroupNode.IsEmpty)
        {
            // Remove ourselves from the owner host's tracking and the nested window list
            OwnerHost.CloseFloatingWindow(this);
        }
        else
        {
            UpdateTitle();
        }
    }

    private void UpdateTitle()
    {
        var active = GroupNode.ActivePanel;
        if (active != null)
        {
            TitleText = active.Title;
        }
        else if (GroupNode.Panels.Count > 0)
        {
            TitleText = GroupNode.Panels[0].Title;
        }
        else
        {
            TitleText = string.Empty;
        }
    }
}
