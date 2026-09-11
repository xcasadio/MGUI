using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Adorners;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// Semi-transparent overlay that shows a preview of where a docked panel will be placed.
/// Displayed during drag and drop operations.<para/>
/// The fill and the border are drawn by two template parts (<see cref="SurfacePartName"/>, <see cref="BorderPartName"/>) created by the
/// <c>Dock.PreviewOverlay.Default</c> control template, which also applies the theme's <see cref="MGThemeDockingSettings.PreviewOverlayFillColor"/>
/// and <see cref="MGThemeDockingSettings.PreviewOverlayBorderColor"/>.
/// </summary>
public class MGDockPreviewOverlay : MGBoundsAdorner
{
    public const string SurfacePartName = "PART_Surface";
    public const string BorderPartName = "PART_Border";

    private MGBorder SurfaceElement { get; set; }
    private MGBorder BorderElement { get; set; }

    /// <summary>The bounds the two parts were last laid out on.</summary>
    private Rectangle _partBounds;

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
    /// The color of the preview rectangle (including alpha for transparency).<para/>
    /// Default value: <see cref="MGThemeDockingSettings.PreviewOverlayFillColor"/>
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
    /// The color of the preview rectangle's border.<para/>
    /// Default value: <see cref="MGThemeDockingSettings.PreviewOverlayBorderColor"/>
    /// </summary>
    public Color PreviewBorderColor
    {
        get => BorderColor;
        set
        {
            if (BorderColor != value)
            {
                BorderColor = value;
                NPC(nameof(PreviewBorderColor));
            }
        }
    }

    /// <summary>
    /// The thickness, in pixels, of the preview rectangle's border.<para/>
    /// Default value: 2 (<c>Dock.PreviewOverlay.Default</c>)
    /// </summary>
    public int PreviewBorderThickness
    {
        get => BorderThickness;
        set
        {
            if (BorderThickness != value)
            {
                BorderThickness = value;
                NPC(nameof(PreviewBorderThickness));
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
        PropertyChanged += OnOwnPropertyChanged;
        IsPreviewVisible = false;
        TargetBoundsOverride = Rectangle.Empty;

        // The surface and border parts, and the preview colors, come from the control template, see AttachControlTemplateStructure.
        DefaultControlTemplateName = MGControlTemplateCatalog.DockPreviewOverlayTemplateName;
        SyncPreviewVisibility();
    }

    protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
    {
        yield return new(SurfacePartName, typeof(MGBorder));
        yield return new(BorderPartName, typeof(MGBorder));
    }

    /// <summary>Binds the fill and border parts created by the control template (<c>Dock.PreviewOverlay.Default</c>) as children laid out on the
    /// preview bounds. <see cref="PreviewColor"/>, <see cref="PreviewBorderColor"/> and <see cref="PreviewBorderThickness"/> stay this overlay's own
    /// state and are pushed to the parts; a part replaced by another structure is detached from this overlay.</summary>
    protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure Structure)
    {
        SurfaceElement = BindPart(SurfaceElement, (MGBorder)Structure.Parts[SurfacePartName]);
        BorderElement = BindPart(BorderElement, (MGBorder)Structure.Parts[BorderPartName]);
        _partBounds = Rectangle.Empty;
        SyncPreviewParts();
        SyncPartBounds();
    }

    private MGBorder BindPart(MGBorder previous, MGBorder part)
    {
        if (previous != null && !ReferenceEquals(previous, part))
        {
            previous.SetParent(null);
        }

        part.ManagedParent = this;
        part.IsHitTestVisible = false;
        part.SetParent(this);
        return part;
    }

    private void OnOwnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(FillColor):
            case nameof(BorderColor):
            case nameof(BorderThickness):
                SyncPreviewParts();
                break;
            case nameof(TargetBoundsOverride):
            case nameof(TargetMargin):
            case nameof(TargetElement):
                SyncPartBounds();
                break;
        }
    }

    private void SyncPreviewParts()
    {
        if (SurfaceElement == null || BorderElement == null)
        {
            return;
        }

        SurfaceElement.SetBackgroundSlot(UIValueSlot.Normal, FillColor.AsFillBrush(), UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));
        BorderElement.SetBackgroundSlot(UIValueSlot.Normal, null, UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));
        BorderElement.SetBorderBrush(BorderColor.AsFillBrush().AsUniformBorderBrush(), UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));
        BorderElement.SetBorderThickness(new Thickness(BorderThickness), UIValueResolutionSource.VisualState(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
    }

    /// <summary>Lays the parts out on the adorned bounds when they change: the preview moves every frame of a drag without invalidating the layout
    /// of its host.</summary>
    private void SyncPartBounds()
    {
        if (SurfaceElement == null || BorderElement == null)
        {
            return;
        }

        Rectangle bounds = TryGetAdornedBounds(out Rectangle adornedBounds) ? adornedBounds : Rectangle.Empty;
        if (bounds != _partBounds)
        {
            _partBounds = bounds;
            SurfaceElement.UpdateLayout(bounds);
            BorderElement.UpdateLayout(bounds);
        }
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

    public override void UpdateSelf(ElementUpdateArgs UA)
    {
        base.UpdateSelf(UA);
        SyncPartBounds();
    }

    public override IEnumerable<MGElement> GetChildren()
    {
        // The two parts are attached together; none exists until a control template supplies them.
        if (SurfaceElement == null)
        {
            yield break;
        }

        yield return SurfaceElement;
        yield return BorderElement;
    }

    /// <summary>The template parts draw the preview; without a template structure the adorner draws its own fill and border.</summary>
    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        if (SurfaceElement == null)
        {
            base.DrawSelf(DA, layoutBounds);
        }
    }

    protected override void DrawContents(ElementDrawArgs DA)
    {
        foreach (MGElement child in GetChildren())
        {
            child?.Draw(DA);
        }
    }

    internal override ClipDefinition GetSelfClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
        => null;

    internal override ClipDefinition GetContentsClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
        => null;
}
