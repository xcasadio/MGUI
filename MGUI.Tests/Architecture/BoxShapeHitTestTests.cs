using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>Analytic hit test of <see cref="MGBoxShape.Contains(Vector2)"/>: inner rectangle, corner arcs, zero radius, clamped radii.</summary>
public class BoxShapeHitTestTests
{
    //  100x60 box at (10,10) with a uniform 20px radius: corner squares are [10,30]x[10,30], [90,110]x[10,30], [90,110]x[50,70], [10,30]x[50,70].
    private static readonly Rectangle Bounds = new(10, 10, 100, 60);
    private static MGBoxShape RoundedShape(int radius) => new(Bounds, new Thickness(0), new MGCornerRadius(radius));

    [Fact]
    public void Contains_PointInsideInnerRectangle_ReturnsTrue()
    {
        MGBoxShape shape = RoundedShape(20);

        Assert.True(shape.Contains(new Vector2(60, 40)));
        //  Inside the top strip but outside every corner square.
        Assert.True(shape.Contains(new Vector2(60, 12)));
        //  Inside the left strip but outside every corner square.
        Assert.True(shape.Contains(new Vector2(12, 40)));
    }

    [Theory]
    [InlineData(20f, 20f)]   // top-left: (30,30) centre, distance sqrt(200) < 20
    [InlineData(100f, 20f)]  // top-right: (90,30) centre
    [InlineData(100f, 60f)]  // bottom-right: (90,50) centre
    [InlineData(20f, 60f)]   // bottom-left: (30,50) centre
    public void Contains_PointInsideCornerArc_ReturnsTrue(float x, float y)
    {
        Assert.True(RoundedShape(20).Contains(new Vector2(x, y)));
    }

    [Theory]
    [InlineData(12f, 12f)]   // top-left: distance sqrt(648) > 20 although inside the rectangle
    [InlineData(108f, 12f)]  // top-right
    [InlineData(108f, 68f)]  // bottom-right
    [InlineData(12f, 68f)]   // bottom-left
    public void Contains_PointInsideRectangleButOutsideCornerArc_ReturnsFalse(float x, float y)
    {
        MGBoxShape shape = RoundedShape(20);

        Assert.True(Bounds.ContainsInclusive(new Vector2(x, y)));
        Assert.False(shape.Contains(new Vector2(x, y)));
    }

    [Fact]
    public void Contains_PointOnArcBoundary_IsInclusive()
    {
        //  Top-left arc centre (30,30), radius 20: (30 - 20, 30) lies exactly on the arc and on the left edge.
        Assert.True(RoundedShape(20).Contains(new Vector2(10, 30)));
        //  Exactly on the arc, inside the corner square: (30 - 12, 30 - 16) -> 144 + 256 = 400.
        Assert.True(RoundedShape(20).Contains(new Vector2(18, 14)));
    }

    [Fact]
    public void Contains_ZeroRadius_MatchesInclusiveRectangleTest()
    {
        MGBoxShape shape = new(Bounds, new Thickness(3), MGCornerRadius.Zero);

        for (int y = Bounds.Top - 1; y <= Bounds.Bottom + 1; y++)
        {
            for (int x = Bounds.Left - 1; x <= Bounds.Right + 1; x++)
            {
                Vector2 point = new(x, y);
                Assert.Equal(Bounds.ContainsInclusive(point), shape.Contains(point));
            }
        }
    }

    [Fact]
    public void Contains_PointOutsideBounds_ReturnsFalse()
    {
        MGBoxShape shape = RoundedShape(20);

        Assert.False(shape.Contains(new Vector2(Bounds.Left - 0.5f, 40)));
        Assert.False(shape.Contains(new Vector2(Bounds.Right + 0.5f, 40)));
        Assert.False(shape.Contains(new Vector2(60, Bounds.Top - 0.5f)));
        Assert.False(shape.Contains(new Vector2(60, Bounds.Bottom + 0.5f)));
    }

    [Fact]
    public void Contains_ClampedRadius_BehavesLikeNormalizedRadius()
    {
        //  40x20 bounds with a requested radius of 100: NormalizeCornerRadius scales it down to 10 (height constraint).
        Rectangle smallBounds = new(0, 0, 40, 20);
        MGBoxShape oversized = new(smallBounds, new Thickness(0), new MGCornerRadius(100));
        MGBoxShape normalized = new(smallBounds, new Thickness(0), new MGCornerRadius(10));

        Assert.Equal(new MGCornerRadius(10), oversized.NormalizedCornerRadius);
        for (int y = -1; y <= 21; y++)
        {
            for (int x = -1; x <= 41; x++)
            {
                Vector2 point = new(x, y);
                Assert.Equal(normalized.Contains(point), oversized.Contains(point));
            }
        }

        //  Spot checks against the clamped radius: (1,1) is outside the arc centred at (10,10); (3,3) is inside.
        Assert.False(oversized.Contains(new Vector2(1, 1)));
        Assert.True(oversized.Contains(new Vector2(3, 3)));
    }

    [Fact]
    public void Contains_AsymmetricRadii_TestEachCornerWithItsOwnRadius()
    {
        MGBoxShape shape = new(Bounds, new Thickness(0), new MGCornerRadius(20, 0, 10, 0));

        //  Top-left has a 20px arc: (12,12) is cut off.
        Assert.False(shape.Contains(new Vector2(12, 12)));
        //  Top-right is square: the extreme corner point is inside.
        Assert.True(shape.Contains(new Vector2(110, 10)));
        //  Bottom-right has a 10px arc centred at (100,60): (109,69) is cut off, (104,64) is inside.
        Assert.False(shape.Contains(new Vector2(109, 69)));
        Assert.True(shape.Contains(new Vector2(104, 64)));
        //  Bottom-left is square.
        Assert.True(shape.Contains(new Vector2(10, 70)));
    }

    [Fact]
    public void Contains_IgnoresBorderThickness()
    {
        MGBoxShape thin = new(Bounds, new Thickness(0), new MGCornerRadius(20));
        MGBoxShape thick = new(Bounds, new Thickness(12), new MGCornerRadius(20));

        for (int y = Bounds.Top; y <= Bounds.Bottom; y += 2)
        {
            for (int x = Bounds.Left; x <= Bounds.Right; x += 2)
            {
                Vector2 point = new(x, y);
                Assert.Equal(thin.Contains(point), thick.Contains(point));
            }
        }
    }
}
