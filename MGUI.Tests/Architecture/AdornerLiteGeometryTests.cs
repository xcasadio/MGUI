using MGUI.Core.UI.Adorners;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

public class AdornerLiteGeometryTests
{
    [Fact]
    public void ResolveTargetBounds_AppliesTargetMargin()
    {
        Rectangle actual = MGAdornerGeometryHelper.ResolveTargetBounds(new Rectangle(40, 30, 120, 60), new Thickness(4, 6, 8, 10));

        Assert.Equal(new Rectangle(36, 24, 132, 76), actual);
    }

    [Fact]
    public void FillResizeHandleBounds_ReturnsEightAnchoredHandles()
    {
        Rectangle[] handles = new Rectangle[MGAdornerGeometryHelper.ResizeHandleCount];

        MGAdornerGeometryHelper.FillResizeHandleBounds(new Rectangle(100, 50, 80, 40), 10, handles);

        Assert.Equal(new Rectangle(95, 45, 10, 10), handles[0]);
        Assert.Equal(new Rectangle(135, 45, 10, 10), handles[1]);
        Assert.Equal(new Rectangle(175, 45, 10, 10), handles[2]);
        Assert.Equal(new Rectangle(95, 65, 10, 10), handles[3]);
        Assert.Equal(new Rectangle(175, 65, 10, 10), handles[4]);
        Assert.Equal(new Rectangle(95, 85, 10, 10), handles[5]);
        Assert.Equal(new Rectangle(135, 85, 10, 10), handles[6]);
        Assert.Equal(new Rectangle(175, 85, 10, 10), handles[7]);
    }

    [Fact]
    public void ResolveGuideCoordinate_UsesRequestedAxisAndAlignment()
    {
        Rectangle targetBounds = new(12, 24, 90, 42);

        Assert.Equal(12, MGAdornerGeometryHelper.ResolveGuideCoordinate(targetBounds, MGGuideAxis.Vertical, MGGuideAlignment.Start));
        Assert.Equal(57, MGAdornerGeometryHelper.ResolveGuideCoordinate(targetBounds, MGGuideAxis.Vertical, MGGuideAlignment.Center));
        Assert.Equal(102, MGAdornerGeometryHelper.ResolveGuideCoordinate(targetBounds, MGGuideAxis.Vertical, MGGuideAlignment.End));
        Assert.Equal(24, MGAdornerGeometryHelper.ResolveGuideCoordinate(targetBounds, MGGuideAxis.Horizontal, MGGuideAlignment.Start));
        Assert.Equal(45, MGAdornerGeometryHelper.ResolveGuideCoordinate(targetBounds, MGGuideAxis.Horizontal, MGGuideAlignment.Center));
        Assert.Equal(66, MGAdornerGeometryHelper.ResolveGuideCoordinate(targetBounds, MGGuideAxis.Horizontal, MGGuideAlignment.End));
    }
}