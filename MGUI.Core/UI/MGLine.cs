using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System.Diagnostics;

namespace MGUI.Core.UI;

public class MGLine : MGVertexShapeElementBase
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Vector2 _StartPoint;
    public Vector2 StartPoint
    {
        get => _StartPoint;
        set
        {
            if (_StartPoint != value)
            {
                _StartPoint = value;
                RebuildGeometry();
                NPC(nameof(StartPoint));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Vector2 _EndPoint;
    public Vector2 EndPoint
    {
        get => _EndPoint;
        set
        {
            if (_EndPoint != value)
            {
                _EndPoint = value;
                RebuildGeometry();
                NPC(nameof(EndPoint));
            }
        }
    }

    private Vector2 _NormalizedStartPoint;
    private Vector2 _NormalizedEndPoint;

    public MGLine(MGWindow window, Vector2 startPoint, Vector2 endPoint)
        : this(window, startPoint, endPoint, Color.White, 1f)
    {
    }

    public MGLine(MGWindow window, Vector2 startPoint, Vector2 endPoint, Color stroke, float strokeThickness)
        : base(window, MGElementType.Line)
    {
        using (BeginInitializing())
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            Stroke = stroke;
            StrokeThickness = strokeThickness;
        }
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        if (!HasVisibleStroke)
        {
            return;
        }

        MGPointShapePlacement placement = GetPlacement(layoutBounds);
        Vector2 origin = DA.Offset.ToVector2() + placement.GeometryOrigin;
        DA.Context.StrokeLineSegment(origin, _NormalizedStartPoint, _NormalizedEndPoint, Stroke * DA.Opacity, StrokeThickness);
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
        return MGVectorShapeHelper.IsPointNearPolyline(new[] { _NormalizedStartPoint, _NormalizedEndPoint }, localPoint, StrokeThickness, false);
    }

    private void RebuildGeometry()
    {
        Vector2[] normalized = MGVectorShapeHelper.NormalizePoints(new[] { StartPoint, EndPoint }, out RectangleF bounds);
        _NormalizedStartPoint = normalized.Length > 0 ? normalized[0] : Vector2.Zero;
        _NormalizedEndPoint = normalized.Length > 1 ? normalized[1] : Vector2.Zero;
        GeometrySize = new Size(Math.Max(0, (int)Math.Ceiling(bounds.Width)), Math.Max(0, (int)Math.Ceiling(bounds.Height)));
        LayoutChanged(this, true);
    }
}