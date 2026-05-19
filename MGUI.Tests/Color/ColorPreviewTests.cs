using MGUI.Core.UI;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.ColorPicker;

public class ColorPreviewTests
{
    [Fact]
    public void GetContentBounds_CompressesByBorderThickness()
    {
        Rectangle bounds = new(10, 20, 50, 30);

        Rectangle content = MGColorPreview.GetContentBounds(bounds, 2);

        Assert.Equal(new Rectangle(12, 22, 46, 26), content);
    }

    [Fact]
    public void ValueBounds_SplitPreviousAndCurrentWithoutLosingOddPixel()
    {
        Rectangle bounds = new(0, 0, 11, 5);

        Rectangle previous = MGColorPreview.GetPreviousValueBounds(bounds, showPrevious: true);
        Rectangle current = MGColorPreview.GetCurrentValueBounds(bounds, showPrevious: true);

        Assert.Equal(new Rectangle(0, 0, 5, 5), previous);
        Assert.Equal(new Rectangle(5, 0, 6, 5), current);
    }

    [Fact]
    public void OpaqueComparison_SplitsRowsWithoutLosingOddPixel()
    {
        Rectangle bounds = new(0, 0, 10, 7);

        Rectangle transparent = MGColorPreview.GetTransparentComparisonBounds(bounds, showOpaqueComparison: true);
        Rectangle opaque = MGColorPreview.GetOpaqueComparisonBounds(bounds, showOpaqueComparison: true);

        Assert.Equal(new Rectangle(0, 0, 10, 3), transparent);
        Assert.Equal(new Rectangle(0, 3, 10, 4), opaque);
    }

    [Fact]
    public void DisabledSplits_ReturnFullCurrentAndNoOpaqueBounds()
    {
        Rectangle bounds = new(2, 4, 12, 8);

        Assert.Equal(Rectangle.Empty, MGColorPreview.GetPreviousValueBounds(bounds, showPrevious: false));
        Assert.Equal(bounds, MGColorPreview.GetCurrentValueBounds(bounds, showPrevious: false));
        Assert.Equal(bounds, MGColorPreview.GetTransparentComparisonBounds(bounds, showOpaqueComparison: false));
        Assert.Equal(Rectangle.Empty, MGColorPreview.GetOpaqueComparisonBounds(bounds, showOpaqueComparison: false));
    }
}