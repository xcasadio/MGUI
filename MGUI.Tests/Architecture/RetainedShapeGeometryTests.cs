using MGUI.Core.UI;
using MGUI.Core.UI.Shapes;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Architecture;

public class RetainedShapeGeometryTests
{
    [Fact]
    public void EllipseHitTest_RejectsBoundingBoxCorner()
    {
        Assert.True(MGVectorShapeHelper.ContainsEllipse(120, 72, new Vector2(60, 36), hasFill: true, hasStroke: false, strokeThickness: 0f));
        Assert.False(MGVectorShapeHelper.ContainsEllipse(120, 72, Vector2.Zero, hasFill: true, hasStroke: false, strokeThickness: 0f));
    }

    [Fact]
    public void PolylineHitTest_UsesStrokeThickness()
    {
        Vector2[] points = new[] { new Vector2(0, 0), new Vector2(100, 0) };

        Assert.True(MGVectorShapeHelper.IsPointNearPolyline(points, new Vector2(50, 2), 6f, closed: false));
        Assert.False(MGVectorShapeHelper.IsPointNearPolyline(points, new Vector2(50, 8), 6f, closed: false));
    }

    [Fact]
    public void ConvexPolygonDetection_RejectsConcaveShapes()
    {
        Vector2[] convex = new[] { new Vector2(0, 50), new Vector2(30, 0), new Vector2(90, 10), new Vector2(70, 70) };
        Vector2[] concave = new[] { new Vector2(0, 0), new Vector2(80, 0), new Vector2(40, 24), new Vector2(80, 80), new Vector2(0, 80) };

        Assert.True(MGVectorShapeHelper.IsConvexPolygon(convex));
        Assert.False(MGVectorShapeHelper.IsConvexPolygon(concave));
    }

    [Fact]
    public void PathLiteParser_SupportsMoveLineAndCloseCommands()
    {
        MGPathLiteCommand[] commands = ShapeXamlParser.ParsePathLiteCommands("M 0,20 L 40,0 L 80,20 Z");

        Assert.Collection(commands,
            item => Assert.Equal(MGPathLiteCommandType.MoveTo, item.Type),
            item => Assert.Equal(MGPathLiteCommandType.LineTo, item.Type),
            item => Assert.Equal(MGPathLiteCommandType.LineTo, item.Type),
            item => Assert.Equal(MGPathLiteCommandType.Close, item.Type));
    }
}