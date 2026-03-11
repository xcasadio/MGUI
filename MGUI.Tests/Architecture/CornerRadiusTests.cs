using MGUI.Core.UI;

namespace MGUI.Tests.Architecture;

public class CornerRadiusTests
{
    [Fact]
    public void Zero_IsZero()
    {
        Assert.True(MGCornerRadius.Zero.IsZero);
        Assert.Equal(new MGCornerRadius(0, 0, 0, 0), MGCornerRadius.Zero);
    }

    [Fact]
    public void UniformConstructor_SetsAllCorners()
    {
        MGCornerRadius radius = new(6);

        Assert.Equal(6, radius.TopLeft);
        Assert.Equal(6, radius.TopRight);
        Assert.Equal(6, radius.BottomRight);
        Assert.Equal(6, radius.BottomLeft);
        Assert.False(radius.IsZero);
    }

    [Fact]
    public void FullConstructor_SetsIndependentCorners()
    {
        MGCornerRadius radius = new(1, 2, 3, 4);

        Assert.Equal(1, radius.TopLeft);
        Assert.Equal(2, radius.TopRight);
        Assert.Equal(3, radius.BottomRight);
        Assert.Equal(4, radius.BottomLeft);
    }

    [Fact]
    public void Equality_UsesCornerValues()
    {
        MGCornerRadius left = new(2, 4, 6, 8);
        MGCornerRadius right = new(2, 4, 6, 8);
        MGCornerRadius different = new(2, 4, 6, 7);

        Assert.Equal(left, right);
        Assert.NotEqual(left, different);
    }
}