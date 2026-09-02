using MGUI.Core.UI;
using MGUI.Core.UI.Shapes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>These tests clear and count the process-wide <see cref="MGBoxGeometryBuilder"/> cache, so they must not run while other test classes
/// build geometry in parallel (xunit runs collections concurrently by default): the collection below is executed on its own.</summary>
[CollectionDefinition(BoxGeometryBuilderCacheCollection.Name, DisableParallelization = true)]
public sealed class BoxGeometryBuilderCacheCollection
{
    public const string Name = "MGBoxGeometryBuilder global cache";
}

[Collection(BoxGeometryBuilderCacheCollection.Name)]
public class BoxGeometryBuilderTests
{
    public BoxGeometryBuilderTests()
    {
        MGBoxGeometryBuilder.ClearCache();
    }

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

    [Fact]
    public void EquivalentNormalizedShapes_ReuseCachedGeometry()
    {
        MGBoxShape unclamped = new(new Rectangle(0, 0, 30, 20), new Thickness(50), new MGCornerRadius(40));
        MGBoxShape normalized = unclamped.Normalize();

        MGBoxGeometry first = MGBoxGeometryBuilder.Build(unclamped, 6);
        MGBoxGeometry second = MGBoxGeometryBuilder.Build(normalized, 6);
        MGBoxGeometry differentTessellation = MGBoxGeometryBuilder.Build(normalized, 7);

        Assert.Equal(2, MGBoxGeometryBuilder.CachedGeometryCount);
        Assert.Same(first.Vertices, second.Vertices);
        Assert.NotSame(first.Vertices, differentTessellation.Vertices);
    }

    /// <summary>Tache 4 item 7: a border thickness reaching the corner radius used to collapse the inner arc for that corner to a single point,
    /// leaving <see cref="MGBoxGeometryBuilder"/> with an outer contour longer than the inner one; <see cref="MGBoxGeometry.HasBorderRingMesh"/>
    /// requires matching counts, so the ring mesh was empty and border rendering fell back to the rectangle path. The inner contour now repeats
    /// the collapsed corner's point instead of shortening the contour, so the counts always match once a border and a non-empty InnerBounds exist.</summary>
    [Fact]
    public void ThicknessReachingCornerRadius_InnerContourMatchesOuterCountAndKeepsRingMesh()
    {
        MGBoxShape shape = new(new Rectangle(0, 0, 80, 40), new Thickness(4), new MGCornerRadius(4));

        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, 8);

        Assert.Equal(MGCornerRadius.Zero, shape.Normalize().InnerCornerRadius);
        Assert.True(shape.Normalize().InnerBounds.Width > 0 && shape.Normalize().InnerBounds.Height > 0);
        Assert.Equal(geometry.OuterContour.Count, geometry.InnerContour.Count);
        Assert.True(geometry.HasBorderRingMesh);
        Assert.Equal(geometry.OuterContour.Count * 6, geometry.BorderRingIndices.Count);

        //  Every corner's inner points collapse onto the same (repeated) location - the arc did not just shrink, it degenerated on purpose.
        Vector2 firstCornerPoint = geometry.InnerContour[0];
        for (int i = 1; i < 9; i++)
        {
            Assert.Equal(firstCornerPoint, geometry.InnerContour[i]);
        }
    }

    /// <summary>Only one corner's thickness reaches its radius; the other three stay rounded on the inner side. The collapsed corner still
    /// contributes as many (repeated) inner points as the outer contour emitted for it, so the total counts match overall.</summary>
    [Fact]
    public void AsymmetricCollapsedCorner_InnerContourStillMatchesOuterCount()
    {
        //  TopLeft: thickness 4 == radius 4 -> collapses. The other three corners: thickness 4 < radius 10 -> stay rounded (inner radius 6).
        MGBoxShape shape = new(new Rectangle(0, 0, 80, 40), new Thickness(4), new MGCornerRadius(4, 10, 10, 10));

        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, 8);
        MGCornerRadius innerRadius = shape.Normalize().InnerCornerRadius;

        Assert.Equal(0, innerRadius.TopLeft);
        Assert.True(innerRadius.TopRight > 0 && innerRadius.BottomRight > 0 && innerRadius.BottomLeft > 0);
        Assert.Equal(geometry.OuterContour.Count, geometry.InnerContour.Count);
        Assert.True(geometry.HasBorderRingMesh);

        //  The collapsed TopLeft corner (indices 0-8, 9 points at cornerSegmentCount 8) repeats a single point...
        Vector2 collapsedPoint = geometry.InnerContour[0];
        for (int i = 1; i < 9; i++)
        {
            Assert.Equal(collapsedPoint, geometry.InnerContour[i]);
        }

        //  ...while the next (rounded) corner samples a genuine arc, so consecutive points differ.
        Assert.NotEqual(geometry.InnerContour[9], geometry.InnerContour[10]);
    }

    /// <summary>When the outer contour itself loses points to coincident-point suppression (adjacent corners meeting on a small rectangle),
    /// the inner contour must still end up with exactly as many points as the outer one, per corner, using the outer's actual (suppressed)
    /// count rather than the unsuppressed cornerSegmentCount + 1.</summary>
    [Fact]
    public void TinyBounds_OuterContourSuppressionStillMatchesInnerContourCount()
    {
        //  Radius 3 on a 6px-wide box: TopLeft/TopRight radii (3+3=6=width) meet exactly on the top edge, and BottomLeft/BottomRight
        //  meet exactly on the bottom edge, so BuildContour's coincident-point suppression drops one point at each of those two seams.
        MGBoxShape shape = new(new Rectangle(0, 0, 6, 20), new Thickness(1), new MGCornerRadius(3));

        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, 8);

        Assert.True(geometry.OuterContour.Count < 4 * 9, "the scenario must actually trigger outer suppression, or it doesn't exercise this case");
        Assert.Equal(geometry.OuterContour.Count, geometry.InnerContour.Count);
        Assert.True(geometry.HasBorderRingMesh);
    }
}