using MGUI.Core.UI;
using MonoGame.Extended;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Architecture;

public class BoxShapeTests
{
    [Fact]
    public void Normalize_ClampsBorderThicknessToBounds()
    {
        MGBoxShape shape = new(new Rectangle(10, 20, 8, 6), new Thickness(10, 4, 10, 4), MGCornerRadius.Zero);

        MGBoxShape normalized = shape.Normalize();

        Assert.Equal(8, normalized.BorderThickness.Left);
        Assert.Equal(4, normalized.BorderThickness.Top);
        Assert.Equal(0, normalized.BorderThickness.Right);
        Assert.Equal(2, normalized.BorderThickness.Bottom);
        Assert.Equal(new Rectangle(18, 24, 0, 0), normalized.InnerBounds);
    }

    [Fact]
    public void Normalize_ClampsCornerRadiusToFitBounds()
    {
        MGBoxShape shape = new(new Rectangle(0, 0, 10, 8), new Thickness(0), new MGCornerRadius(8, 8, 8, 8));

        MGBoxShape normalized = shape.Normalize();

        Assert.Equal(new MGCornerRadius(4, 4, 4, 4), normalized.CornerRadius);
        Assert.True(normalized.HasRoundedCorners);
    }

    [Fact]
    public void InnerCornerRadius_SubtractsAdjacentBorderThickness()
    {
        MGBoxShape shape = new(
            new Rectangle(0, 0, 20, 20),
            new Thickness(2, 4, 6, 8),
            new MGCornerRadius(10, 11, 12, 13));

        Assert.Equal(new MGCornerRadius(4, 2, 1, 2), shape.InnerCornerRadius);
    }

    [Fact]
    public void ZeroThicknessAndZeroRadius_RepresentsSimpleRectangleFill()
    {
        MGBoxShape shape = new(new Rectangle(1, 2, 30, 40), new Thickness(0), MGCornerRadius.Zero);

        Assert.False(shape.HasBorder);
        Assert.False(shape.HasRoundedCorners);
        Assert.Equal(shape.OuterBounds, shape.InnerBounds);
        Assert.Equal(MGCornerRadius.Zero, shape.InnerCornerRadius);
    }

    [Fact]
    public void Normalize_ClampsNegativeValuesToZero()
    {
        MGBoxShape shape = new(new Rectangle(0, 0, 10, 10), new Thickness(-1, -2, 3, 4), new MGCornerRadius(-5, 4, -3, 2));

        MGBoxShape normalized = shape.Normalize();

        Assert.Equal(new Thickness(0, 0, 3, 4), normalized.BorderThickness);
        Assert.Equal(new MGCornerRadius(0, 4, 0, 2), normalized.CornerRadius);
    }

    [Fact]
    public void InnerCornerRadius_IsClampedWhenBorderConsumesTooMuchSpace()
    {
        MGBoxShape shape = new(new Rectangle(0, 0, 6, 6), new Thickness(3), new MGCornerRadius(5));

        MGBoxShape normalized = shape.Normalize();

        Assert.Equal(new Rectangle(3, 3, 0, 0), normalized.InnerBounds);
        Assert.Equal(MGCornerRadius.Zero, normalized.InnerCornerRadius);
    }
}