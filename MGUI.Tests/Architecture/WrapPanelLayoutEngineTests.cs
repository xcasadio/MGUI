using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;

namespace MGUI.Tests.Architecture;

public class WrapPanelLayoutEngineTests
{
    [Fact]
    public void HorizontalMeasure_Wraps_WhenWidthIsExceeded()
    {
        WrapPanelChildMeasurement[] children =
        {
            new(30, 10),
            new(40, 20),
            new(50, 30),
        };

        Size result = MGWrapPanelLayoutEngine.Measure(children, new Size(100, 200), Orientation.Horizontal, 5);

        Assert.Equal(new Size(75, 55), result);
    }

    [Fact]
    public void VerticalMeasure_Wraps_WhenHeightIsExceeded()
    {
        WrapPanelChildMeasurement[] children =
        {
            new(30, 20),
            new(40, 30),
            new(50, 40),
        };

        Size result = MGWrapPanelLayoutEngine.Measure(children, new Size(200, 60), Orientation.Vertical, 4);

        Assert.Equal(new Size(94, 54), result);
    }

    [Fact]
    public void Measure_IgnoresCollapsedChildren_ForSpacingAndDesiredSize()
    {
        WrapPanelChildMeasurement[] children =
        {
            new(20, 10),
            new(50, 50, true),
            new(30, 15),
        };

        Size result = MGWrapPanelLayoutEngine.Measure(children, new Size(200, 200), Orientation.Horizontal, 7);

        Assert.Equal(new Size(57, 15), result);
    }

    [Fact]
    public void Measure_DoesNotWrap_WhenPrimaryAxisIsUnbounded()
    {
        WrapPanelChildMeasurement[] children =
        {
            new(30, 10),
            new(40, 20),
            new(50, 30),
        };

        Size result = MGWrapPanelLayoutEngine.Measure(children, new Size(int.MaxValue, 100), Orientation.Horizontal, 3);

        Assert.Equal(new Size(126, 30), result);
    }

    [Fact]
    public void Arrange_PreservesChildOrder_AcrossWrappedRows()
    {
        WrapPanelChildMeasurement[] children =
        {
            new(40, 10),
            new(50, 20),
            new(30, 15),
            new(25, 12),
        };

        WrapPanelLayoutResult result = MGWrapPanelLayoutEngine.Arrange(children, new Rectangle(10, 20, 100, 100), Orientation.Horizontal, 5);

        Assert.Equal(new Rectangle(10, 20, 40, 10), result.ChildBounds[0]);
        Assert.Equal(new Rectangle(55, 20, 50, 20), result.ChildBounds[1]);
        Assert.Equal(new Rectangle(10, 45, 30, 15), result.ChildBounds[2]);
        Assert.Equal(new Rectangle(45, 45, 25, 12), result.ChildBounds[3]);
    }

    [Fact]
    public void Arrange_HandlesChildrenLargerThanAvailableWidth_ByKeepingTheItemOnItsOwnLine()
    {
        WrapPanelChildMeasurement[] children =
        {
            new(120, 25),
            new(30, 10),
        };

        WrapPanelLayoutResult result = MGWrapPanelLayoutEngine.Arrange(children, new Rectangle(0, 0, 100, 200), Orientation.Horizontal, 4);

        Assert.Equal(new Rectangle(0, 0, 120, 25), result.ChildBounds[0]);
        Assert.Equal(new Rectangle(0, 29, 30, 10), result.ChildBounds[1]);
        Assert.Equal(new Size(120, 39), result.DesiredSize);
    }
}