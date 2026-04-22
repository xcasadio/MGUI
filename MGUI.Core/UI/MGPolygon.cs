using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MGUI.Shared.Rendering.Clipping;
using System.Collections.Generic;

namespace MGUI.Core.UI
{
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

            MGPointShapePlacement placement = GetPlacement(layoutBounds);
            Vector2 origin = DA.Offset.ToVector2() + placement.GeometryOrigin;
            Color fillColor = Fill * DA.Opacity;
            Color strokeColor = Stroke * DA.Opacity;

            if (HasVisibleFill && HasVisibleStroke)
            {
                DA.Context.StrokeAndFillPolygon(origin, NormalizedPoints, strokeColor, fillColor, StrokeThickness);
            }
            else if (HasVisibleFill)
            {
                DA.Context.FillPolygon(origin, NormalizedPoints, fillColor);
            }
            else if (HasVisibleStroke)
            {
                for (int i = 0; i < NormalizedPoints.Length; i++)
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

            MGPointShapePlacement placement = GetPlacement(LayoutBounds);
            Vector2 layoutPoint = ConvertCoordinateSpace(CoordinateSpace.UnscaledScreen, CoordinateSpace.Layout, unscaledScreenPosition);
            Vector2 localPoint = layoutPoint - placement.GeometryOrigin;

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

            MGPointShapePlacement placement = GetPlacement(layoutBounds);
            if (NormalizedPoints.Length < 3 || !MGVectorShapeHelper.IsConvexPolygon(NormalizedPoints))
            {
                return CreateRectangleClipDefinition(TransformClipBounds(DA, placement.Bounds), $"{ElementType}.Self");
            }

            ClipGeometry geometry = MGVectorShapeHelper.CreateTriangleFanClipGeometry(NormalizedPoints, DA.Offset.ToVector2() + placement.GeometryOrigin);
            return CreateGeometryClipDefinition(TransformClipBounds(DA, placement.Bounds), geometry, $"{ElementType}.Self", allowRectangleFallback: true);
        }
    }
}