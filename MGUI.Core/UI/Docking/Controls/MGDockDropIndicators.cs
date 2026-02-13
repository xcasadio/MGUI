using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// Overlay that displays a central visual indicator showing available drop zones
/// during drag and drop operations. Shows a single indicator with Left/Right/Top/Bottom/Center zones
/// arranged in a cross layout (like Visual Studio).
/// </summary>
public class MGDockDropIndicators : MGElement
{
    private const int ZoneSize = 40;      // Size of each zone square
    private const int ZoneSpacing = 4;    // Spacing between zones
    private const int BorderWidth = 2;

    private static readonly Color InactiveColor = new Color(100, 100, 100, 180);
    private static readonly Color ActiveColor = new Color(0, 122, 204, 230);
    private static readonly Color BorderColor = new Color(255, 255, 255, 200);

    private Rectangle _leftZoneRect;
    private Rectangle _rightZoneRect;
    private Rectangle _topZoneRect;
    private Rectangle _bottomZoneRect;
    private Rectangle _centerZoneRect;
    private Rectangle _indicatorBounds; // Bounding box of entire indicator

    private bool _isVisible;
    /// <summary>
    /// Whether the indicators are currently visible.
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
    /// The currently active (hovered) zone.
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
        }
    }

    /// <summary>
    /// Shows the indicators centered on the given target bounds.
    /// </summary>
    public void Show(Rectangle targetBounds)
    {
        TargetBounds = targetBounds;
        IsVisible = true;
        ActiveZone = DockZone.None;
    }

    /// <summary>
    /// Hides the indicators.
    /// </summary>
    public void Hide()
    {
        IsVisible = false;
        ActiveZone = DockZone.None;
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

        if (_leftZoneRect.Contains(screenPosition))
        {
            return DockZone.Left;
        }

        if (_rightZoneRect.Contains(screenPosition))
        {
            return DockZone.Right;
        }

        if (_topZoneRect.Contains(screenPosition))
        {
            return DockZone.Top;
        }

        if (_bottomZoneRect.Contains(screenPosition))
        {
            return DockZone.Bottom;
        }

        if (_centerZoneRect.Contains(screenPosition))
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
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
    {
        if (!IsVisible)
        {
            DrawSelfBaseImplementation(DA, LayoutBounds);
            return;
        }

        DrawZoneIndicator(DA, _leftZoneRect, DockZone.Left);
        DrawZoneIndicator(DA, _rightZoneRect, DockZone.Right);
        DrawZoneIndicator(DA, _topZoneRect, DockZone.Top);
        DrawZoneIndicator(DA, _bottomZoneRect, DockZone.Bottom);
        DrawZoneIndicator(DA, _centerZoneRect, DockZone.Center);

        DrawSelfBaseImplementation(DA, LayoutBounds);
    }

    /// <summary>
    /// Draws a single zone indicator with a symbol.
    /// </summary>
    private void DrawZoneIndicator(ElementDrawArgs DA, Rectangle rect, DockZone zone)
    {
        bool isActive = (zone == ActiveZone);
        Color fillColor = isActive ? ActiveColor : InactiveColor;

        DA.DT.FillRectangle(
            Vector2.Zero,
            new RectangleF(rect.X, rect.Y, rect.Width, rect.Height),
            fillColor
        );

        DrawBorder(DA, rect, BorderColor, BorderWidth);
        DrawZoneSymbol(DA, rect, zone, Color.White);
    }

    /// <summary>
    /// Draws a border around a rectangle.
    /// </summary>
    private void DrawBorder(ElementDrawArgs DA, Rectangle rect, Color color, int thickness)
    {
        // Top
        DA.DT.FillRectangle(
            Vector2.Zero,
            new RectangleF(rect.X, rect.Y, rect.Width, thickness),
            color
        );

        // Bottom
        DA.DT.FillRectangle(
            Vector2.Zero,
            new RectangleF(rect.X, rect.Bottom - thickness, rect.Width, thickness),
            color
        );

        // Left
        DA.DT.FillRectangle(
            Vector2.Zero,
            new RectangleF(rect.X, rect.Y, thickness, rect.Height),
            color
        );

        // Right
        DA.DT.FillRectangle(
            Vector2.Zero,
            new RectangleF(rect.Right - thickness, rect.Y, thickness, rect.Height),
            color
        );
    }

    /// <summary>
    /// Draws the directional symbol for a zone.
    /// </summary>
    private void DrawZoneSymbol(ElementDrawArgs DA, Rectangle rect, DockZone zone, Color color)
    {
        int centerX = rect.X + rect.Width / 2;
        int centerY = rect.Y + rect.Height / 2;
        int arrowSize = 10; // Size of directional arrows

        switch (zone)
        {
            case DockZone.Left:
                // Left-pointing arrow (triangle)
                DrawArrow(DA, centerX, centerY, arrowSize, 180, color);
                break;

            case DockZone.Right:
                // Right-pointing arrow
                DrawArrow(DA, centerX, centerY, arrowSize, 0, color);
                break;

            case DockZone.Top:
                // Up-pointing arrow
                DrawArrow(DA, centerX, centerY, arrowSize, 270, color);
                break;

            case DockZone.Bottom:
                // Down-pointing arrow
                DrawArrow(DA, centerX, centerY, arrowSize, 90, color);
                break;

            case DockZone.Center:
                // Small square for center zone
                int squareSize = 14;
                var centerSquare = new Rectangle(
                    centerX - squareSize / 2,
                    centerY - squareSize / 2,
                    squareSize,
                    squareSize);
                DrawBorder(DA, centerSquare, color, 2);
                break;
        }
    }

    /// <summary>
    /// Draws a simple arrow (as rectangles forming an arrow shape).
    /// </summary>
    private void DrawArrow(ElementDrawArgs DA, int centerX, int centerY, int size, float angleDegrees, Color color)
    {
        // Simple arrow using rectangles
        // Draw a stem and a triangular head using filled rectangles
        int stemLength = size;
        int stemWidth = 3;
        int headSize = size / 2;

        switch ((int)angleDegrees)
        {
            case 0: // Right
                // Stem
                DA.DT.FillRectangle(Vector2.Zero,
                    new RectangleF(centerX - stemLength/2, centerY - stemWidth/2, stemLength, stemWidth),
                    color);
                // Arrow head (triangle approximation)
                DA.DT.FillRectangle(Vector2.Zero,
                    new RectangleF(centerX + stemLength/2, centerY - headSize/2, headSize/2, headSize),
                    color);
                break;

            case 180: // Left
                // Stem
                DA.DT.FillRectangle(Vector2.Zero,
                    new RectangleF(centerX - stemLength/2, centerY - stemWidth/2, stemLength, stemWidth),
                    color);
                // Arrow head
                DA.DT.FillRectangle(Vector2.Zero,
                    new RectangleF(centerX - stemLength/2 - headSize/2, centerY - headSize/2, headSize/2, headSize),
                    color);
                break;

            case 90: // Down
                // Stem
                DA.DT.FillRectangle(Vector2.Zero,
                    new RectangleF(centerX - stemWidth/2, centerY - stemLength/2, stemWidth, stemLength),
                    color);
                // Arrow head
                DA.DT.FillRectangle(Vector2.Zero,
                    new RectangleF(centerX - headSize/2, centerY + stemLength/2, headSize, headSize/2),
                    color);
                break;

            case 270: // Up
                // Stem
                DA.DT.FillRectangle(Vector2.Zero,
                    new RectangleF(centerX - stemWidth/2, centerY - stemLength/2, stemWidth, stemLength),
                    color);
                // Arrow head
                DA.DT.FillRectangle(Vector2.Zero,
                    new RectangleF(centerX - headSize/2, centerY - stemLength/2 - headSize/2, headSize, headSize/2),
                    color);
                break;
        }
    }
}
