using Microsoft.Xna.Framework;
using MGUI.Core.UI.Adorners;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// Semi-transparent overlay that shows a preview of where a docked panel will be placed.
/// Displayed during drag and drop operations.
/// </summary>
public class MGDockPreviewOverlay : MGBoundsAdorner
{
    public const string SurfacePartName = "PART_Surface";
    public const string BorderPartName = "PART_Border";

    private bool _isPreviewVisible;
    /// <summary>
    /// Whether the preview overlay is currently visible.
    /// </summary>
    public bool IsPreviewVisible
    {
        get => _isPreviewVisible;
        set
        {
            if (_isPreviewVisible != value)
            {
                _isPreviewVisible = value;
                SyncPreviewVisibility();
                NPC(nameof(IsPreviewVisible));
            }
        }
    }

    /// <summary>
    /// The screen-space bounds where the preview rectangle should be drawn.
    /// </summary>
    public Rectangle PreviewBounds
    {
        get => TargetBoundsOverride ?? Rectangle.Empty;
        set
        {
            if (PreviewBounds != value)
            {
                TargetBoundsOverride = value;
                SyncPreviewVisibility();
                NPC(nameof(PreviewBounds));
            }
        }
    }

    /// <summary>
    /// The color of the preview rectangle (including alpha for transparency).
    /// Default is semi-transparent blue.
    /// </summary>
    public Color PreviewColor
    {
        get => FillColor;
        set
        {
            if (FillColor != value)
            {
                FillColor = value;
                NPC(nameof(PreviewColor));
            }
        }
    }

    /// <summary>
    /// Creates a new MGDockPreviewOverlay.
    /// </summary>
    /// <param name="window">The parent window.</param>
    public MGDockPreviewOverlay(MGWindow window)
        : base(window)
    {
        PreviewColor = new Color(0, 122, 204, 100);
        BorderColor = new Color(0, 122, 204, 200);
        BorderThickness = 2;
        IsPreviewVisible = false;
        TargetBoundsOverride = Rectangle.Empty;
        SyncPreviewVisibility();
    }

    private void SyncPreviewVisibility()
    {
        bool isVisible = IsPreviewVisible && PreviewBounds.Width > 0 && PreviewBounds.Height > 0;
        Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
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
}