using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Rendering.Clipping;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// Semi-transparent overlay that shows a preview of where a docked panel will be placed.
/// Displayed during drag and drop operations.
/// </summary>
public class MGDockPreviewOverlay : MGElement
{
    public const string SurfacePartName = "PART_Surface";
    public const string BorderPartName = "PART_Border";

    private MGComponent<MGRectangle> PreviewSurfaceComponent { get; }
    private MGRectangle PreviewSurfaceElement { get; }
    private MGComponent<MGRectangle> PreviewBorderComponent { get; }
    private MGRectangle PreviewBorderElement { get; }

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
                SyncPreviewVisuals();
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
                SyncPreviewVisuals();
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
                if (PreviewSurfaceElement != null)
                {
                    PreviewSurfaceElement.Fill = value.AsFillBrush();
                }
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
                if (PreviewBorderElement != null)
                {
                    PreviewBorderElement.Stroke = value;
                }
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
                if (PreviewBorderElement != null)
                {
                    PreviewBorderElement.StrokeThickness = value;
                }
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

            PreviewSurfaceElement = new(window, 0, 0, Color.Transparent, 0, Color.Transparent)
            {
                ManagedParent = this,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            RegisterTemplatePart(SurfacePartName, PreviewSurfaceElement);
            PreviewSurfaceComponent = new(PreviewSurfaceElement, false, false, false, false, false, false, false,
                (availableBounds, componentSize) => PreviewBounds);
            AddComponent(PreviewSurfaceComponent);

            PreviewBorderElement = new(window, 0, 0, BorderColor, BorderThickness, Color.Transparent)
            {
                ManagedParent = this,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            RegisterTemplatePart(BorderPartName, PreviewBorderElement);
            PreviewBorderComponent = new(PreviewBorderElement, false, false, false, false, false, false, false,
                (availableBounds, componentSize) => PreviewBounds);
            AddComponent(PreviewBorderComponent);

            SyncPreviewVisuals();
        }
    }

    private void SyncPreviewVisuals()
    {
        if (PreviewSurfaceElement == null || PreviewBorderElement == null)
        {
            return;
        }

        bool isVisible = IsPreviewVisible && PreviewBounds.Width > 0 && PreviewBounds.Height > 0;
        Visibility visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        PreviewSurfaceElement.Visibility = visibility;
        PreviewSurfaceElement.Width = Math.Max(0, PreviewBounds.Width);
        PreviewSurfaceElement.Height = Math.Max(0, PreviewBounds.Height);
        PreviewSurfaceElement.Fill = PreviewColor.AsFillBrush();

        PreviewBorderElement.Visibility = visibility;
        PreviewBorderElement.Width = Math.Max(0, PreviewBounds.Width);
        PreviewBorderElement.Height = Math.Max(0, PreviewBounds.Height);
        PreviewBorderElement.Stroke = BorderColor;
        PreviewBorderElement.StrokeThickness = BorderThickness;
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

    internal override ClipDefinition GetSelfClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
        => null;

    internal override ClipDefinition GetContentsClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
        => null;

}