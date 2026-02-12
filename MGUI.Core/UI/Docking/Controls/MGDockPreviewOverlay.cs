using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Brushes.Fill_Brushes;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// Semi-transparent overlay that shows a preview of where a docked panel will be placed.
/// Displayed during drag and drop operations.
/// </summary>
public class MGDockPreviewOverlay : MGElement
{
    private bool _isVisible;
    /// <summary>
    /// Whether the preview overlay is currently visible.
    /// </summary>
    public bool IsPreviewVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                NPC(nameof(IsPreviewVisible));
            }
        }
    }

    private Rectangle _previewBounds;
    /// <summary>
    /// The screen-space bounds where the preview rectangle should be drawn.
    /// </summary>
    public Rectangle PreviewBounds
    {
        get => _previewBounds;
        set
        {
            if (_previewBounds != value)
            {
                _previewBounds = value;
                NPC(nameof(PreviewBounds));
            }
        }
    }

    private Color _previewColor = new Color(0, 122, 204, 100); // Blue semi-transparent
    /// <summary>
    /// The color of the preview rectangle (including alpha for transparency).
    /// Default is semi-transparent blue.
    /// </summary>
    public Color PreviewColor
    {
        get => _previewColor;
        set
        {
            if (_previewColor != value)
            {
                _previewColor = value;
                NPC(nameof(PreviewColor));
            }
        }
    }

    private Color _borderColor = new Color(0, 122, 204, 200); // Blue more opaque
    /// <summary>
    /// The color of the preview border.
    /// Default is blue with higher opacity than the fill.
    /// </summary>
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            if (_borderColor != value)
            {
                _borderColor = value;
                NPC(nameof(BorderColor));
            }
        }
    }

    private int _borderThickness = 2;
    /// <summary>
    /// The thickness of the preview border in pixels.
    /// </summary>
    public int BorderThickness
    {
        get => _borderThickness;
        set
        {
            if (_borderThickness != value)
            {
                _borderThickness = value;
                NPC(nameof(BorderThickness));
            }
        }
    }

    /// <summary>
    /// Creates a new MGDockPreviewOverlay.
    /// </summary>
    /// <param name="window">The parent window.</param>
    public MGDockPreviewOverlay(MGWindow window) : base(window, MGElementType.Custom)
    {
        using (BeginInitializing())
        {
            // Don't intercept any mouse input
            IsHitTestVisible = false;

            // Allow the preview to be drawn outside parent bounds if needed
            ClipToBounds = false;

            // Start hidden
            IsPreviewVisible = false;

            // Full stretch to cover entire parent area
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
        }
    }

    /// <summary>
    /// Shows the preview at the specified bounds.
    /// </summary>
    /// <param name="bounds">The screen-space bounds for the preview.</param>
    public void Show(Rectangle bounds)
    {
        PreviewBounds = bounds;
        IsPreviewVisible = true;
    }

    /// <summary>
    /// Hides the preview overlay.
    /// </summary>
    public void Hide()
    {
        IsPreviewVisible = false;
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
    {
        if (!IsPreviewVisible || PreviewBounds.Width <= 0 || PreviewBounds.Height <= 0)
        {
            DrawSelfBaseImplementation(DA, LayoutBounds);
            return;
        }

        // Draw the semi-transparent fill
        DA.DT.FillRectangle(
            Vector2.Zero,
            new RectangleF(PreviewBounds.X, PreviewBounds.Y, PreviewBounds.Width, PreviewBounds.Height),
            PreviewColor
        );

        // Draw a border for better visibility
        if (BorderThickness > 0)
        {
            // Top border
            DA.DT.FillRectangle(
                Vector2.Zero,
                new RectangleF(PreviewBounds.X, PreviewBounds.Y, PreviewBounds.Width, BorderThickness),
                BorderColor
            );

            // Bottom border
            DA.DT.FillRectangle(
                Vector2.Zero,
                new RectangleF(PreviewBounds.X, PreviewBounds.Bottom - BorderThickness, PreviewBounds.Width, BorderThickness),
                BorderColor
            );

            // Left border
            DA.DT.FillRectangle(
                Vector2.Zero,
                new RectangleF(PreviewBounds.X, PreviewBounds.Y, BorderThickness, PreviewBounds.Height),
                BorderColor
            );

            // Right border
            DA.DT.FillRectangle(
                Vector2.Zero,
                new RectangleF(PreviewBounds.Right - BorderThickness, PreviewBounds.Y, BorderThickness, PreviewBounds.Height),
                BorderColor
            );
        }

        DrawSelfBaseImplementation(DA, LayoutBounds);
    }
}