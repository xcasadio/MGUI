using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering.Clipping;

namespace MGUI.Core.UI.Docking.Controls;

internal sealed class MGDockDropZoneIndicator : MGElement
{
    public DockZone Zone { get; set; }
    public bool IsHostEdge { get; set; }
    public bool IsDisabled { get; set; }
    public bool IsActive { get; set; }
    public Color InactiveColor { get; set; }
    public Color ActiveColor { get; set; }
    public Color BorderColor { get; set; }
    public Color HostInactiveColor { get; set; }
    public Color HostActiveColor { get; set; }
    public Color DisabledColor { get; set; }
    public Color DisabledBorderColor { get; set; }
    public Color SymbolColor { get; set; }
    public Color DisabledSymbolColor { get; set; }
    public int BorderWidth { get; set; }

    public MGDockDropZoneIndicator(MGWindow parentWindow) : base(parentWindow, MGElementType.Custom)
    {
        using (BeginInitializing())
        {
            IsHitTestVisible = false;
        }
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        if (layoutBounds.Width <= 0 || layoutBounds.Height <= 0)
        {
            return;
        }

        Color fillColor = IsDisabled
            ? DisabledColor
            : (IsHostEdge
                ? (IsActive ? HostActiveColor : HostInactiveColor)
                : (IsActive ? ActiveColor : InactiveColor));

        Color borderColor = IsDisabled ? DisabledBorderColor : BorderColor;

        DA.DT.FillRectangle(Vector2.Zero, new RectangleF(layoutBounds.X, layoutBounds.Y, layoutBounds.Width, layoutBounds.Height), fillColor);
        DrawBorder(DA, layoutBounds, borderColor, BorderWidth);

        DrawSymbol(DA, layoutBounds, IsDisabled ? DisabledSymbolColor : SymbolColor);
    }

    private void DrawBorder(ElementDrawArgs DA, Rectangle bounds, Color color, int thickness)
    {
        DA.DT.FillRectangle(Vector2.Zero, new RectangleF(bounds.X, bounds.Y, bounds.Width, thickness), color);
        DA.DT.FillRectangle(Vector2.Zero, new RectangleF(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        DA.DT.FillRectangle(Vector2.Zero, new RectangleF(bounds.X, bounds.Y, thickness, bounds.Height), color);
        DA.DT.FillRectangle(Vector2.Zero, new RectangleF(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }

    private void DrawSymbol(ElementDrawArgs DA, Rectangle bounds, Color color)
    {
        const int iconSize = 24;
        Rectangle iconRect = new Rectangle(
            bounds.X + (bounds.Width - iconSize) / 2,
            bounds.Y + (bounds.Height - iconSize) / 2,
            iconSize,
            iconSize);

        string textureKey = Zone switch
        {
            DockZone.Left => IsHostEdge ? "DockPanelLeft" : "DockPanelLeftDashed",
            DockZone.Right => IsHostEdge ? "DockPanelRight" : "DockPanelRightDashed",
            DockZone.Top => IsHostEdge ? "DockPanelTop" : "DockPanelTopDashed",
            DockZone.Bottom => IsHostEdge ? "DockPanelBottom" : "DockPanelBottomDashed",
            DockZone.Center => IsHostEdge ? "DockPanelCenter" : "DockPanelCenterDashed",
            _ => null
        };

        if (textureKey != null && GetResources().TryDrawTexture(DA.DT, textureKey, iconRect, 1f, color))
        {
            return;
        }

        switch (Zone)
        {
            case DockZone.Left:
                UISymbolDrawing.DrawFilledTriangleArrow(DA.DT, DA.Offset.ToVector2(), iconRect, UITriangleArrowDirection.Left, color * DA.Opacity);
                break;
            case DockZone.Right:
                UISymbolDrawing.DrawFilledTriangleArrow(DA.DT, DA.Offset.ToVector2(), iconRect, UITriangleArrowDirection.Right, color * DA.Opacity);
                break;
            case DockZone.Top:
                UISymbolDrawing.DrawFilledTriangleArrow(DA.DT, DA.Offset.ToVector2(), iconRect, UITriangleArrowDirection.Up, color * DA.Opacity);
                break;
            case DockZone.Bottom:
                UISymbolDrawing.DrawFilledTriangleArrow(DA.DT, DA.Offset.ToVector2(), iconRect, UITriangleArrowDirection.Down, color * DA.Opacity);
                break;
            case DockZone.Center:
                DrawBorder(DA, new Rectangle(iconRect.X + 5, iconRect.Y + 5, iconRect.Width - 10, iconRect.Height - 10), color, 2);
                break;
        }
    }
}

/// <summary>
/// Overlay that displays a central visual indicator showing available drop zones
/// during drag and drop operations. Shows a single indicator with Left/Right/Top/Bottom/Center zones
/// arranged in a cross layout (like Visual Studio).
/// Also supports host-edge indicators: four small arrow buttons pinned to the edges of the
/// docking host that are always visible while a drag is active.
/// </summary>
public class MGDockDropIndicators : MGElement
{
    public const string LeftDropZonePartName = "PART_LeftDropZone";
    public const string RightDropZonePartName = "PART_RightDropZone";
    public const string TopDropZonePartName = "PART_TopDropZone";
    public const string BottomDropZonePartName = "PART_BottomDropZone";
    public const string CenterDropZonePartName = "PART_CenterDropZone";
    public const string HostLeftDropZonePartName = "PART_HostLeftDropZone";
    public const string HostRightDropZonePartName = "PART_HostRightDropZone";
    public const string HostTopDropZonePartName = "PART_HostTopDropZone";
    public const string HostBottomDropZonePartName = "PART_HostBottomDropZone";

    private const int ZoneSpacing = 4;    // Spacing between zones
    private const int BorderWidth = 2;

    private int _zoneSize = 40;
    /// <summary>Size of each square zone, in pixels: the hit-test rectangles and the zone elements follow it. Default: the theme's
    /// <see cref="MGThemeDockingSettings.DropIndicatorZoneSize"/>, applied by the <c>Dock.DropIndicators.Default</c> template (backlog task 14).</summary>
    public int ZoneSize
    {
        get => _zoneSize;
        set
        {
            if (_zoneSize != value)
            {
                _zoneSize = value;
                //  Both geometries derive from the size, not only from the bounds their setters watch.
                CalculateIndicatorPositions();
                CalculateHostEdgePositions();
                LayoutChanged(this, true);
                NPC(nameof(ZoneSize));
            }
        }
    }

    public Color InactiveColor { get; set; } = new Color(100, 100, 100, 180);
    public Color ActiveColor { get; set; } = new Color(0, 122, 204, 230);
    public Color BorderColor { get; set; } = new Color(255, 255, 255, 200);
    public Color HostInactiveColor { get; set; } = new Color(80, 80, 120, 180);
    public Color HostActiveColor { get; set; } = new Color(0, 160, 80, 230);
    public Color DisabledColor { get; set; } = new Color(40, 40, 40, 100);
    public Color DisabledBorderColor { get; set; } = new Color(70, 70, 70, 120);
    public Color SymbolColor { get; set; } = Color.White;
    public Color DisabledSymbolColor { get; set; } = new Color(100, 100, 100, 150);

    /// <summary>Zones that are currently forbidden by docking rules and should be drawn grayed out.</summary>
    private readonly HashSet<DockZone> _disabledZones = new HashSet<DockZone>();
    private MGDockDropZoneIndicator LeftZoneElement { get; set; }
    private MGDockDropZoneIndicator RightZoneElement { get; set; }
    private MGDockDropZoneIndicator TopZoneElement { get; set; }
    private MGDockDropZoneIndicator BottomZoneElement { get; set; }
    private MGDockDropZoneIndicator CenterZoneElement { get; set; }
    private MGDockDropZoneIndicator HostLeftZoneElement { get; set; }
    private MGDockDropZoneIndicator HostRightZoneElement { get; set; }
    private MGDockDropZoneIndicator HostTopZoneElement { get; set; }
    private MGDockDropZoneIndicator HostBottomZoneElement { get; set; }

    // ── Per-panel joystick state ─────────────────────────────────────────────
    private Rectangle _leftZoneRect;
    private Rectangle _rightZoneRect;
    private Rectangle _topZoneRect;
    private Rectangle _bottomZoneRect;
    private Rectangle _centerZoneRect;
    private Rectangle _indicatorBounds; // Bounding box of entire indicator

    private bool _isVisible;
    /// <summary>
    /// Whether the per-panel indicators are currently visible.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                NPC(nameof(IsVisible));
            }
        }
    }

    private Rectangle _targetBounds;
    /// <summary>
    /// The screen-space bounds of the target group where indicators should be displayed.
    /// </summary>
    public Rectangle TargetBounds
    {
        get => _targetBounds;
        set
        {
            if (_targetBounds != value)
            {
                _targetBounds = value;
                CalculateIndicatorPositions();
                NPC(nameof(TargetBounds));
            }
        }
    }

    private DockZone _activeZone = DockZone.None;
    /// <summary>
    /// The currently active (hovered) per-panel zone.
    /// </summary>
    public DockZone ActiveZone
    {
        get => _activeZone;
        private set
        {
            if (_activeZone != value)
            {
                _activeZone = value;
                NPC(nameof(ActiveZone));
            }
        }
    }

    // ── Host-edge indicator state ────────────────────────────────────────────

    private bool       _hostEdgeVisible;
    private Rectangle  _hostBounds;

    // The four small indicator squares pinned to each edge of the host
    private Rectangle _hostLeftZoneRect;
    private Rectangle _hostRightZoneRect;
    private Rectangle _hostTopZoneRect;
    private Rectangle _hostBottomZoneRect;

    private DockZone _hostEdgeActiveZone = DockZone.None;

    /// <summary>
    /// Whether the host-edge indicators are currently visible.
    /// </summary>
    public bool HostEdgeVisible => _hostEdgeVisible;

    public MGDockDropIndicators(MGWindow parentWindow) : base(parentWindow, MGElementType.Custom)
    {
        using (BeginInitializing())
        {
            // Don't intercept mouse input - this is a visual overlay
            IsHitTestVisible = false;
            
            // Allow indicators to be drawn outside parent bounds if needed
            ClipToBounds = false;
            
            // Start hidden
            IsVisible = false;
            
            // Full stretch to cover entire parent area
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            // The nine zone parts come from the control template, see AttachControlTemplateStructure.
            DefaultControlTemplateName = MGControlTemplateCatalog.DockDropIndicatorsTemplateName;
            SyncZoneVisuals();
        }
    }

    protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
    {
        yield return new(LeftDropZonePartName, typeof(MGDockDropZoneIndicator));
        yield return new(RightDropZonePartName, typeof(MGDockDropZoneIndicator));
        yield return new(TopDropZonePartName, typeof(MGDockDropZoneIndicator));
        yield return new(BottomDropZonePartName, typeof(MGDockDropZoneIndicator));
        yield return new(CenterDropZonePartName, typeof(MGDockDropZoneIndicator));
        yield return new(HostLeftDropZonePartName, typeof(MGDockDropZoneIndicator));
        yield return new(HostRightDropZonePartName, typeof(MGDockDropZoneIndicator));
        yield return new(HostTopDropZonePartName, typeof(MGDockDropZoneIndicator));
        yield return new(HostBottomDropZonePartName, typeof(MGDockDropZoneIndicator));
    }

    /// <summary>Binds the nine zone parts created by the control template (<c>Dock.DropIndicators.Default</c>) as children of this overlay. The zone of
    /// each part follows from its part name; positions, colors, active and disabled states stay computed here (<see cref="SyncZoneVisuals"/>).</summary>
    protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure Structure)
    {
        LeftZoneElement = BindZoneElement(LeftZoneElement, Structure, LeftDropZonePartName, DockZone.Left, false);
        RightZoneElement = BindZoneElement(RightZoneElement, Structure, RightDropZonePartName, DockZone.Right, false);
        TopZoneElement = BindZoneElement(TopZoneElement, Structure, TopDropZonePartName, DockZone.Top, false);
        BottomZoneElement = BindZoneElement(BottomZoneElement, Structure, BottomDropZonePartName, DockZone.Bottom, false);
        CenterZoneElement = BindZoneElement(CenterZoneElement, Structure, CenterDropZonePartName, DockZone.Center, false);
        HostLeftZoneElement = BindZoneElement(HostLeftZoneElement, Structure, HostLeftDropZonePartName, DockZone.Left, true);
        HostRightZoneElement = BindZoneElement(HostRightZoneElement, Structure, HostRightDropZonePartName, DockZone.Right, true);
        HostTopZoneElement = BindZoneElement(HostTopZoneElement, Structure, HostTopDropZonePartName, DockZone.Top, true);
        HostBottomZoneElement = BindZoneElement(HostBottomZoneElement, Structure, HostBottomDropZonePartName, DockZone.Bottom, true);
        SyncZoneVisuals();
    }

    /// <summary>Takes ownership of one zone part; the zone it replaces, if a new structure brings another element, is detached from this overlay.</summary>
    private MGDockDropZoneIndicator BindZoneElement(MGDockDropZoneIndicator previous, MGControlTemplateStructure structure, string partName, DockZone zone, bool isHostEdge)
    {
        MGDockDropZoneIndicator element = (MGDockDropZoneIndicator)structure.Parts[partName];
        if (previous != null && !ReferenceEquals(previous, element))
        {
            previous.SetParent(null);
        }

        element.ManagedParent = this;
        element.Zone = zone;
        element.IsHostEdge = isHostEdge;
        element.IsHitTestVisible = false;
        element.Visibility = Visibility.Collapsed;
        element.SetParent(this);
        return element;
    }

    /// <summary>
    /// Shows the indicators centered on the given target bounds.
    /// </summary>
    public void Show(Rectangle targetBounds)
    {
        TargetBounds = targetBounds;
        IsVisible = true;
        ActiveZone = DockZone.None;
        SyncZoneVisuals();
    }

    /// <summary>
    /// Hides the indicators.
    /// </summary>
    public void Hide()
    {
        IsVisible = false;
        ActiveZone = DockZone.None;
        _disabledZones.Clear();
        SyncZoneVisuals();
    }

    /// <summary>
    /// Sets the zones that are currently forbidden by docking rules.
    /// Forbidden zones are drawn grayed out and are not returned by
    /// <see cref="GetZoneAtPosition"/>. Pass null or an empty sequence to clear.
    /// </summary>
    public void SetDisabledZones(IEnumerable<DockZone> disabledZones)
    {
        _disabledZones.Clear();
        if (disabledZones != null)
        {
            foreach (var z in disabledZones)
            {
                _disabledZones.Add(z);
            }
        }

        SyncZoneVisuals();
    }

    // ── Host-edge indicator API ──────────────────────────────────────────────

    /// <summary>
    /// Shows the four host-edge indicators pinned to the edges of <paramref name="hostBounds"/>.
    /// Call once when a drag begins (threshold exceeded) and update via
    /// <see cref="UpdateHostEdgeActiveZone"/> each frame.
    /// </summary>
    public void ShowHostEdge(Rectangle hostBounds)
    {
        if (_hostBounds != hostBounds)
        {
            _hostBounds = hostBounds;
            CalculateHostEdgePositions();
        }
        _hostEdgeVisible = true;
        _hostEdgeActiveZone = DockZone.None;
        SyncZoneVisuals();
    }

    /// <summary>
    /// Hides the host-edge indicators and resets the active zone.
    /// Call when a drag ends or is cancelled.
    /// </summary>
    public void HideHostEdge()
    {
        _hostEdgeVisible = false;
        _hostEdgeActiveZone = DockZone.None;
        SyncZoneVisuals();
    }

    internal override ClipDefinition GetSelfClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
        => null;

    internal override ClipDefinition GetContentsClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
        => null;

    /// <summary>
    /// Returns the host-edge zone whose indicator square contains <paramref name="screenPosition"/>,
    /// or <see cref="DockZone.None"/> if none.
    /// </summary>
    public DockZone GetHostEdgeZoneAtPosition(Point screenPosition)
    {
        if (!_hostEdgeVisible)
        {
            return DockZone.None;
        }

        if (_hostLeftZoneRect.Contains(screenPosition))
        {
            return DockZone.Left;
        }

        if (_hostRightZoneRect.Contains(screenPosition))
        {
            return DockZone.Right;
        }

        if (_hostTopZoneRect.Contains(screenPosition))
        {
            return DockZone.Top;
        }

        if (_hostBottomZoneRect.Contains(screenPosition))
        {
            return DockZone.Bottom;
        }

        return DockZone.None;
    }

    /// <summary>
    /// Updates the highlighted host-edge zone based on the current mouse position.
    /// Pass a point outside all indicator squares (or <c>new Point(-1,-1)</c>) to clear
    /// the highlight when the panel joystick takes priority.
    /// </summary>
    public void UpdateHostEdgeActiveZone(Point screenPosition)
    {
        _hostEdgeActiveZone = GetHostEdgeZoneAtPosition(screenPosition);
        SyncZoneVisuals();
    }

    /// <summary>
    /// Calculates the zone at the given screen position.
    /// Returns DockZone.None if the position is not over any zone indicator.
    /// </summary>
    public DockZone GetZoneAtPosition(Point screenPosition)
    {
        if (!IsVisible)
        {
            return DockZone.None;
        }

        if (!_indicatorBounds.Contains(screenPosition))
        {
            return DockZone.None;
        }

        if (_leftZoneRect.Contains(screenPosition)   && !_disabledZones.Contains(DockZone.Left))
        {
            return DockZone.Left;
        }

        if (_rightZoneRect.Contains(screenPosition)  && !_disabledZones.Contains(DockZone.Right))
        {
            return DockZone.Right;
        }

        if (_topZoneRect.Contains(screenPosition)    && !_disabledZones.Contains(DockZone.Top))
        {
            return DockZone.Top;
        }

        if (_bottomZoneRect.Contains(screenPosition) && !_disabledZones.Contains(DockZone.Bottom))
        {
            return DockZone.Bottom;
        }

        if (_centerZoneRect.Contains(screenPosition) && !_disabledZones.Contains(DockZone.Center))
        {
            return DockZone.Center;
        }

        return DockZone.None;
    }

    /// <summary>
    /// Updates the active zone based on mouse position.
    /// Should be called during drag preview updates.
    /// </summary>
    public void UpdateActiveZone(Point screenPosition)
    {
        ActiveZone = GetZoneAtPosition(screenPosition);
        SyncZoneVisuals();
    }

    /// <summary>
    /// Calculates the screen-space positions of all zone indicators
    /// based on the current target bounds.
    /// </summary>
    private void CalculateIndicatorPositions()
    {
        if (_targetBounds.IsEmpty || _targetBounds.Width <= 0 || _targetBounds.Height <= 0)
        {
            return;
        }

        int centerX = _targetBounds.X + _targetBounds.Width / 2;
        int centerY = _targetBounds.Y + _targetBounds.Height / 2;

        // Layout zones in a cross pattern:
        //       [Top]
        // [Left][Center][Right]
        //      [Bottom]

        _centerZoneRect = new Rectangle(
            centerX - ZoneSize / 2,
            centerY - ZoneSize / 2,
            ZoneSize,
            ZoneSize);

        _leftZoneRect = new Rectangle(
            _centerZoneRect.Left - ZoneSize - ZoneSpacing,
            centerY - ZoneSize / 2,
            ZoneSize,
            ZoneSize);

        _rightZoneRect = new Rectangle(
            _centerZoneRect.Right + ZoneSpacing,
            centerY - ZoneSize / 2,
            ZoneSize,
            ZoneSize);

        _topZoneRect = new Rectangle(
            centerX - ZoneSize / 2,
            _centerZoneRect.Top - ZoneSize - ZoneSpacing,
            ZoneSize,
            ZoneSize);

        _bottomZoneRect = new Rectangle(
            centerX - ZoneSize / 2,
            _centerZoneRect.Bottom + ZoneSpacing,
            ZoneSize,
            ZoneSize);

        // Calculate overall indicator bounds (for hit testing)
        int minX = Math.Min(_leftZoneRect.Left, Math.Min(_topZoneRect.Left, _centerZoneRect.Left));
        int minY = Math.Min(_topZoneRect.Top, Math.Min(_leftZoneRect.Top, _centerZoneRect.Top));
        int maxX = Math.Max(_rightZoneRect.Right, Math.Max(_bottomZoneRect.Right, _centerZoneRect.Right));
        int maxY = Math.Max(_bottomZoneRect.Bottom, Math.Max(_leftZoneRect.Bottom, _centerZoneRect.Bottom));
        
        _indicatorBounds = new Rectangle(minX, minY, maxX - minX, maxY - minY);
        SyncZoneVisuals();
    }

    /// <summary>
    /// Calculates the screen-space positions of the four host-edge indicator squares
    /// based on the current host bounds.  Each square is centred on the corresponding edge.
    /// </summary>
    private void CalculateHostEdgePositions()
    {
        if (_hostBounds.Width <= 0 || _hostBounds.Height <= 0)
        {
            return;
        }

        int midX = _hostBounds.X + _hostBounds.Width  / 2;
        int midY = _hostBounds.Y + _hostBounds.Height / 2;
        int half = ZoneSize / 2;

        // Left  — centred vertically on the left edge
        _hostLeftZoneRect = new Rectangle(
            _hostBounds.X,
            midY - half,
            ZoneSize, ZoneSize);

        // Right — centred vertically on the right edge
        _hostRightZoneRect = new Rectangle(
            _hostBounds.Right - ZoneSize,
            midY - half,
            ZoneSize, ZoneSize);

        // Top   — centred horizontally on the top edge
        _hostTopZoneRect = new Rectangle(
            midX - half,
            _hostBounds.Y,
            ZoneSize, ZoneSize);

        // Bottom — centred horizontally on the bottom edge
        _hostBottomZoneRect = new Rectangle(
            midX - half,
            _hostBounds.Bottom - ZoneSize,
            ZoneSize, ZoneSize);

        SyncZoneVisuals();
    }

    public override IEnumerable<MGElement> GetChildren()
    {
        // The zones are attached together; none exists until a control template supplies them.
        if (LeftZoneElement == null)
        {
            yield break;
        }

        yield return LeftZoneElement;
        yield return RightZoneElement;
        yield return TopZoneElement;
        yield return BottomZoneElement;
        yield return CenterZoneElement;
        yield return HostLeftZoneElement;
        yield return HostRightZoneElement;
        yield return HostTopZoneElement;
        yield return HostBottomZoneElement;
    }

    private void SyncZoneVisuals()
    {
        SyncZoneElement(LeftZoneElement, _leftZoneRect, IsVisible, ActiveZone == DockZone.Left, _disabledZones.Contains(DockZone.Left));
        SyncZoneElement(RightZoneElement, _rightZoneRect, IsVisible, ActiveZone == DockZone.Right, _disabledZones.Contains(DockZone.Right));
        SyncZoneElement(TopZoneElement, _topZoneRect, IsVisible, ActiveZone == DockZone.Top, _disabledZones.Contains(DockZone.Top));
        SyncZoneElement(BottomZoneElement, _bottomZoneRect, IsVisible, ActiveZone == DockZone.Bottom, _disabledZones.Contains(DockZone.Bottom));
        SyncZoneElement(CenterZoneElement, _centerZoneRect, IsVisible, ActiveZone == DockZone.Center, _disabledZones.Contains(DockZone.Center));

        SyncZoneElement(HostLeftZoneElement, _hostLeftZoneRect, _hostEdgeVisible, _hostEdgeActiveZone == DockZone.Left, false);
        SyncZoneElement(HostRightZoneElement, _hostRightZoneRect, _hostEdgeVisible, _hostEdgeActiveZone == DockZone.Right, false);
        SyncZoneElement(HostTopZoneElement, _hostTopZoneRect, _hostEdgeVisible, _hostEdgeActiveZone == DockZone.Top, false);
        SyncZoneElement(HostBottomZoneElement, _hostBottomZoneRect, _hostEdgeVisible, _hostEdgeActiveZone == DockZone.Bottom, false);
    }

    private void SyncZoneElement(MGDockDropZoneIndicator element, Rectangle bounds, bool isVisible, bool isActive, bool isDisabled)
    {
        if (element == null)
        {
            return;
        }

        element.Visibility = isVisible && bounds.Width > 0 && bounds.Height > 0 ? Visibility.Visible : Visibility.Collapsed;
        element.InactiveColor = InactiveColor;
        element.ActiveColor = ActiveColor;
        element.BorderColor = BorderColor;
        element.HostInactiveColor = HostInactiveColor;
        element.HostActiveColor = HostActiveColor;
        element.DisabledColor = DisabledColor;
        element.DisabledBorderColor = DisabledBorderColor;
        element.SymbolColor = SymbolColor;
        element.DisabledSymbolColor = DisabledSymbolColor;
        element.BorderWidth = BorderWidth;
        element.IsActive = isActive;
        element.IsDisabled = isDisabled;
        if (element.Visibility == Visibility.Visible)
        {
            element.UpdateLayout(bounds);
        }
    }

    protected override void DrawContents(ElementDrawArgs DA)
    {
        foreach (var child in GetChildren())
        {
            child?.Draw(DA);
        }
    }

}
