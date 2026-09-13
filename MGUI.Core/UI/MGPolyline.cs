using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using System.Diagnostics;

namespace MGUI.Core.UI;

public class MGPolyline : MGVertexShapeElementBase
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Vector2[] _Points = Array.Empty<Vector2>();
    public IReadOnlyList<Vector2> Points
    {
        get => _Points;
        set
        {
            _Points = value == null ? Array.Empty<Vector2>() : new List<Vector2>(value).ToArray();
            RebuildGeometry();
            NPC(nameof(Points));
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    protected Vector2[] NormalizedPoints = Array.Empty<Vector2>();

    public MGPolyline(MGWindow window)
        : this(window, Array.Empty<Vector2>(), Color.White, 1f)
    {
    }

    public MGPolyline(MGWindow window, IReadOnlyList<Vector2> points, Color stroke, float strokeThickness)
        : this(window, MGElementType.Polyline, points, stroke, strokeThickness)
    {
    }

    protected MGPolyline(MGWindow window, MGElementType elementType, IReadOnlyList<Vector2> points, Color stroke, float strokeThickness)
        : base(window, elementType)
    {
        using (BeginInitializing())
        {
            Points = points ?? Array.Empty<Vector2>();
            Stroke = stroke;
            StrokeThickness = strokeThickness;
        }
    }

    public override void DrawSelf(ElementDrawArgs DA, Microsoft.Xna.Framework.Rectangle layoutBounds)
    {
        if (!HasVisibleStroke || NormalizedPoints.Length < 2)
        {
            return;
        }

        MGPointShapePlacement placement = GetPlacement(layoutBounds);
        Vector2 origin = DA.Offset.ToVector2() + placement.GeometryOrigin;
        Color strokeColor = Stroke * DA.Opacity;

        for (int i = 0; i < NormalizedPoints.Length - 1; i++)
        {
            DA.Context.StrokeLineSegment(origin, NormalizedPoints[i], NormalizedPoints[i + 1], strokeColor, StrokeThickness);
        }
    }

    protected internal override bool ContainsUnscaledInputPoint(Vector2 unscaledScreenPosition)
    {
        if (!ActualLayoutBounds.ContainsInclusive(unscaledScreenPosition) || !HasVisibleStroke)
        {
            return false;
        }

        MGPointShapePlacement placement = GetPlacement(LayoutBounds);
        Vector2 layoutPoint = ConvertCoordinateSpace(CoordinateSpace.UnscaledScreen, CoordinateSpace.Layout, unscaledScreenPosition);
        Vector2 localPoint = layoutPoint - placement.GeometryOrigin;
        return MGVectorShapeHelper.IsPointNearPolyline(NormalizedPoints, localPoint, StrokeThickness, false);
    }

    protected void RebuildGeometry()
    {
        NormalizedPoints = MGVectorShapeHelper.NormalizePoints(_Points, out MonoGame.Extended.RectangleF bounds);
        GeometrySize = new MonoGame.Extended.Size(Math.Max(0, (int)Math.Ceiling(bounds.Width)), Math.Max(0, (int)Math.Ceiling(bounds.Height)));
        LayoutChanged(this, true);
    }
}