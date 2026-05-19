using Microsoft.Xna.Framework;
using MGUI.Core.UI;

namespace MGUI.Tests.ColorPicker;

public class ColorPickerPopupTests
{
    [Fact]
    public void GetFittedPopupBounds_KeepsPopupInsideRightEdge()
    {
        Rectangle viewport = new(0, 0, 300, 200);

        Rectangle bounds = MGColorPickerPopup.GetFittedPopupBounds(new Point(260, 20), 100, 80, viewport, 1f);

        Assert.Equal(200, bounds.Left);
        Assert.Equal(20, bounds.Top);
    }

    [Fact]
    public void GetFittedPopupBounds_FlipsAboveWhenBottomWouldOverflow()
    {
        Rectangle viewport = new(0, 0, 300, 200);

        Rectangle bounds = MGColorPickerPopup.GetFittedPopupBounds(new Point(20, 180), 100, 80, viewport, 1f);

        Assert.Equal(20, bounds.Left);
        Assert.Equal(100, bounds.Top);
    }

    [Fact]
    public void GetFittedPopupBounds_ClampsToViewportForLargePopup()
    {
        Rectangle viewport = new(10, 10, 100, 80);

        Rectangle bounds = MGColorPickerPopup.GetFittedPopupBounds(new Point(-20, -20), 200, 200, viewport, 1f);

        Assert.Equal(10, bounds.Left);
        Assert.Equal(10, bounds.Top);
    }
}