using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System.Diagnostics;

namespace MGUI.Core.UI;

public class MGEllipse : MGShapeElementBase
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _width;
    public int Width
    {
        get => _width;
        set
        {
            int actualValue = Math.Max(0, value);
            if (_width != actualValue)
            {
                _width = actualValue;
                _geometryDirty = true;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(Width));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _height;
    public int Height
    {
        get => _height;
        set
        {
            int actualValue = Math.Max(0, value);
            if (_height != actualValue)
            {
                _height = actualValue;
                _geometryDirty = true;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(Height));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _segmentCount;
    public int SegmentCount
    {
        get => _segmentCount;
        set
        {
            int actualValue = Math.Max(3, value);
            if (_segmentCount != actualValue)
            {
                _segmentCount = actualValue;
                _geometryDirty = true;
                NotifyPropertyChanged(nameof(SegmentCount));
            }
        }
    }

    private bool _geometryDirty = true;
    private Vector2[] _cachedVertices = Array.Empty<Vector2>();

    public MGEllipse(MGWindow window, int width, int height)
        : this(window, width, height, Color.White, 1f, Color.Transparent)
    {
    }

    public MGEllipse(MGWindow window, int width, int height, Color stroke, float strokeThickness, Color fill)
        : base(window, MGElementType.Ellipse)
    {
        using (BeginInitializing())
        {
            Width = width;
            Height = height;
            Stroke = stroke;
            StrokeThickness = strokeThickness;
            Fill = fill;
            SegmentCount = 32;
        }
    }

    public override Thickness MeasureSelfOverride(Size availableSize, out Thickness sharedSize)
    {
        sharedSize = new Thickness(0);
        return new Thickness(Width, Height, 0, 0);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        Rectangle bounds = GetEllipseBounds(layoutBounds);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        EnsureGeometry();
        if (_cachedVertices.Length < 3)
        {
            return;
        }

        Vector2 origin = DA.Offset.ToVector2() + bounds.Location.ToVector2();
        Color strokeColor = Stroke * DA.Opacity;
        bool hasSolidFill = TryGetSolidFillColor(DA.Opacity, out Color fillColor);
        bool hasBrushFill = HasVisibleFill && !hasSolidFill;

        if (hasBrushFill)
        {
            MGUI.Shared.Rendering.Clipping.ClipGeometry clipGeometry = MGVectorShapeHelper.CreateTriangulatedClipGeometry(_cachedVertices, origin);
            DrawClippedFillBrush(DA, bounds,
                CreateGeometryClipDefinition(TransformClipBounds(DA, bounds), clipGeometry, $"{ElementType}.Fill"));
        }

        if (hasSolidFill && HasVisibleStroke)
        {
            DA.Context.StrokeAndFillPolygon(origin, _cachedVertices, strokeColor, fillColor, StrokeThickness);
        }
        else if (hasSolidFill)
        {
            DA.Context.FillPolygon(origin, _cachedVertices, fillColor);
        }
        else if (HasVisibleStroke)
        {
            for (int i = 0; i < _cachedVertices.Length; i++)
            {
                DA.Context.StrokeLineSegment(origin, _cachedVertices[i], _cachedVertices[(i + 1) % _cachedVertices.Length], strokeColor, StrokeThickness);
            }
        }
    }

    protected internal override bool ContainsUnscaledInputPoint(Vector2 unscaledScreenPosition)
    {
        if (!ActualLayoutBounds.ContainsInclusive(unscaledScreenPosition))
        {
            return false;
        }

        Rectangle bounds = GetEllipseBounds(LayoutBounds);
        Vector2 layoutPoint = ConvertCoordinateSpace(CoordinateSpace.UnscaledScreen, CoordinateSpace.Layout, unscaledScreenPosition);
        Vector2 localPoint = layoutPoint - bounds.Location.ToVector2();
        return MGVectorShapeHelper.ContainsEllipse(bounds.Width, bounds.Height, localPoint, HasVisibleFill, HasVisibleStroke, StrokeThickness);
    }

    internal override MGUI.Shared.Rendering.Clipping.ClipDefinition GetSelfClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
    {
        if (!ClipToBounds)
        {
            return null;
        }

        Rectangle bounds = GetEllipseBounds(layoutBounds);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        EnsureGeometry();
        if (_cachedVertices.Length < 3)
        {
            return CreateRectangleClipDefinition(TransformClipBounds(DA, bounds), $"{ElementType}.Self");
        }

        Vector2 translation = DA.Offset.ToVector2() + bounds.Location.ToVector2();
        MGUI.Shared.Rendering.Clipping.ClipGeometry geometry = MGVectorShapeHelper.CreateTriangulatedClipGeometry(_cachedVertices, translation);
        return CreateGeometryClipDefinition(TransformClipBounds(DA, bounds), geometry, $"{ElementType}.Self");
    }

    protected override bool StrokeThicknessAffectsLayout => false;

    private Rectangle GetEllipseBounds(Rectangle layoutBounds)
        => ApplyAlignment(layoutBounds, HorizontalAlignment, VerticalAlignment, new Size(Width, Height));

    private void EnsureGeometry()
    {
        if (_geometryDirty)
        {
            _cachedVertices = MGVectorShapeHelper.CreateEllipseVertices(Width, Height, HasVisibleStroke ? StrokeThickness : 0f, SegmentCount);
            _geometryDirty = false;
        }
    }
}