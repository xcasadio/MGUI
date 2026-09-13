using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MGUI.Shared.Rendering.Clipping;

namespace MGUI.Core.UI;

public class MGPolygon : MGPolyline
{
    public MGPolygon(MGWindow window)
        : this(window, System.Array.Empty<Vector2>(), Color.White, 1f, Color.Transparent)
    {
    }

    public MGPolygon(MGWindow window, IReadOnlyList<Vector2> points, Color stroke, float strokeThickness, Color fill)
        : base(window, MGElementType.Polygon, points, stroke, strokeThickness)
    {
        Fill = fill;
    }

    protected override bool StrokeThicknessAffectsLayout => true;

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        if (NormalizedPoints.Length < 3)
        {
            return;
        }

        var placement = GetPlacement(layoutBounds);
        var origin = DA.Offset.ToVector2() + placement.GeometryOrigin;
        var strokeColor = Stroke * DA.Opacity;
        var hasSolidFill = TryGetSolidFillColor(DA.Opacity, out var fillColor);
        var hasBrushFill = HasVisibleFill && !hasSolidFill;

        if (hasBrushFill)
        {
            var clipGeometry = MGVectorShapeHelper.CreateTriangulatedClipGeometry(NormalizedPoints, origin);
            DrawClippedFillBrush(DA, placement.Bounds,
                CreateGeometryClipDefinition(TransformClipBounds(DA, placement.Bounds), clipGeometry, $"{ElementType}.Fill", allowRectangleFallback: true));
        }

        if (hasSolidFill && HasVisibleStroke)
        {
            DA.Context.StrokeAndFillPolygon(origin, NormalizedPoints, strokeColor, fillColor, StrokeThickness);
        }
        else if (hasSolidFill)
        {
            DA.Context.FillPolygon(origin, NormalizedPoints, fillColor);
        }
        else if (HasVisibleStroke)
        {
            for (var i = 0; i < NormalizedPoints.Length; i++)
            {
                DA.Context.StrokeLineSegment(origin, NormalizedPoints[i], NormalizedPoints[(i + 1) % NormalizedPoints.Length], strokeColor, StrokeThickness);
            }
        }
    }

    protected internal override bool ContainsUnscaledInputPoint(Vector2 unscaledScreenPosition)
    {
        if (!ActualLayoutBounds.ContainsInclusive(unscaledScreenPosition))
        {
            return false;
        }

        var placement = GetPlacement(LayoutBounds);
        var layoutPoint = ConvertCoordinateSpace(CoordinateSpace.UnscaledScreen, CoordinateSpace.Layout, unscaledScreenPosition);
        var localPoint = layoutPoint - placement.GeometryOrigin;

        if (HasVisibleFill && MGVectorShapeHelper.IsPointInPolygon(NormalizedPoints, localPoint))
        {
            return true;
        }

        return HasVisibleStroke && MGVectorShapeHelper.IsPointNearPolyline(NormalizedPoints, localPoint, StrokeThickness, true);
    }

    internal override ClipDefinition GetSelfClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
    {
        if (!ClipToBounds)
        {
            return null;
        }

        var placement = GetPlacement(layoutBounds);
        if (NormalizedPoints.Length < 3)
        {
            return CreateRectangleClipDefinition(TransformClipBounds(DA, placement.Bounds), $"{ElementType}.Self");
        }

        var geometry = MGVectorShapeHelper.CreateTriangulatedClipGeometry(NormalizedPoints, DA.Offset.ToVector2() + placement.GeometryOrigin);
        return CreateGeometryClipDefinition(TransformClipBounds(DA, placement.Bounds), geometry, $"{ElementType}.Self", allowRectangleFallback: true);
    }
}