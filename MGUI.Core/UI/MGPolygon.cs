using MGUI.Core.UI.Clipping;
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

    //  The clip geometry and definition of each use, kept while the points, the translation and the target bounds are the same (T1.2): one for the fill brush, one for the shape itself.
    private MGTriangulatedClipEntry _fillClip;
    private MGTriangulatedClipEntry _selfClip;

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
            _fillClip ??= new MGTriangulatedClipEntry($"{ElementType}.Fill", true);
            DrawClippedFillBrush(DA, placement.Bounds, _fillClip.GetDefinition(NormalizedPoints, origin, TransformClipBounds(DA, placement.Bounds)));
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

    internal override ElementClipRequest GetSelfClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
    {
        if (!ClipToBounds)
        {
            return ElementClipRequest.None;
        }

        var placement = GetPlacement(layoutBounds);
        if (NormalizedPoints.Length < 3)
        {
            return ElementClipRequest.Rectangle(TransformClipBounds(DA, placement.Bounds));
        }

        _selfClip ??= new MGTriangulatedClipEntry($"{ElementType}.Self", true);
        return ElementClipRequest.FromDefinition(_selfClip.GetDefinition(NormalizedPoints, DA.Offset.ToVector2() + placement.GeometryOrigin, TransformClipBounds(DA, placement.Bounds)));
    }
}