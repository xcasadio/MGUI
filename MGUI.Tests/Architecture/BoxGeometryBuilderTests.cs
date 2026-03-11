using MGUI.Core.UI;
using MGUI.Core.UI.Shapes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

public class BoxGeometryBuilderTests
{
    [Fact]
    public void RectangleShape_UsesFastPathAndFourOuterPoints()
    {
        MGBoxShape shape = new(new Rectangle(0, 0, 20, 10), new Thickness(0), MGCornerRadius.Zero);

        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, 6);

        Assert.True(geometry.UsesRectangleFastPath);
        Assert.Equal(4, geometry.OuterContour.Count);
        Assert.False(geometry.HasInnerContour);
        Assert.Equal(4, geometry.Vertices.Count);
        Assert.Equal(6, geometry.FillIndices.Count);
        Assert.False(geometry.HasBorderRingMesh);
    }

    [Fact]
    public void ZeroCornerRadius_DoesNotUseRoundedTessellation()
    {
        MGBoxShape shape = new(new Rectangle(0, 0, 20, 10), new Thickness(1), MGCornerRadius.Zero);

        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, 32);

        Assert.True(geometry.UsesRectangleFastPath);
        Assert.Equal(4, geometry.OuterContour.Count);
        Assert.Equal(4, geometry.InnerContour.Count);
    }

    [Fact]
    public void RoundedShape_BuildsRoundedOuterAndInnerContours()
    {
        MGBoxShape shape = new(new Rectangle(0, 0, 20, 20), new Thickness(2), new MGCornerRadius(6));

        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, 4);

        Assert.False(geometry.UsesRectangleFastPath);
        Assert.True(geometry.OuterContour.Count > 4);
        Assert.True(geometry.InnerContour.Count > 4);
        Assert.Equal(4, geometry.CornerSegmentCount);
        Assert.True(geometry.HasFillMesh);
        Assert.True(geometry.HasBorderRingMesh);
        Assert.Equal(geometry.OuterContour.Count + geometry.InnerContour.Count, geometry.Vertices.Count);
    }

    [Fact]
    public void InvalidSegmentCount_IsClampedToAtLeastOne()
    {
        MGBoxShape shape = new(new Rectangle(0, 0, 20, 20), new Thickness(0), new MGCornerRadius(4));

        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, 0);

        Assert.Equal(1, geometry.CornerSegmentCount);
        Assert.True(geometry.OuterContour.Count >= 8);
    }
}