using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI;

namespace MGUI.Tests.Architecture;

public class CanvasLayoutEngineTests
{
    [Fact]
    public void Arrange_UsesLeftAndTop_WhenProvided()
    {
        CanvasChildMeasurement[] children = { new(40, 20, Left: 15, Top: 25) };

        CanvasLayoutResult result = MGCanvasLayoutEngine.Arrange(children, new Rectangle(100, 200, 300, 150));

        Assert.Equal(new Rectangle(115, 225, 40, 20), result.ChildBounds[0]);
    }

    [Fact]
    public void Arrange_UsesRightAndBottom_WhenPrimaryCoordinatesAreMissing()
    {
        CanvasChildMeasurement[] children = { new(40, 20, Right: 15, Bottom: 10) };

        CanvasLayoutResult result = MGCanvasLayoutEngine.Arrange(children, new Rectangle(100, 200, 300, 150));

        Assert.Equal(new Rectangle(345, 320, 40, 20), result.ChildBounds[0]);
    }

    [Fact]
    public void Arrange_PrimaryCoordinatesWinOverCompetingSecondaryOnes()
    {
        CanvasChildMeasurement[] children = { new(40, 20, Left: 12, Top: 18, Right: 99, Bottom: 88) };

        CanvasLayoutResult result = MGCanvasLayoutEngine.Arrange(children, new Rectangle(0, 0, 200, 100));

        Assert.Equal(new Rectangle(12, 18, 40, 20), result.ChildBounds[0]);
    }

    [Fact]
    public void Arrange_DefaultsToOrigin_WhenNoCoordinatesAreProvided()
    {
        CanvasChildMeasurement[] children = { new(40, 20) };

        CanvasLayoutResult result = MGCanvasLayoutEngine.Arrange(children, new Rectangle(10, 20, 200, 100));

        Assert.Equal(new Rectangle(10, 20, 40, 20), result.ChildBounds[0]);
    }

    [Fact]
    public void Measure_AccountsForConfiguredOffsets()
    {
        CanvasChildMeasurement[] children =
        {
            new(40, 20, Left: 30, Top: 10),
            new(50, 60, Right: 25, Bottom: 5),
        };

        Size result = MGCanvasLayoutEngine.Measure(children);

        Assert.Equal(new Size(75, 65), result);
    }

    [Fact]
    public void Arrange_PreservesChildOrder()
    {
        CanvasChildMeasurement[] children =
        {
            new(10, 10, Left: 5, Top: 5),
            new(10, 10, Left: 25, Top: 15),
            new(10, 10, Left: 45, Top: 25),
        };

        CanvasLayoutResult result = MGCanvasLayoutEngine.Arrange(children, new Rectangle(0, 0, 100, 100));

        Assert.Equal(new Rectangle(5, 5, 10, 10), result.ChildBounds[0]);
        Assert.Equal(new Rectangle(25, 15, 10, 10), result.ChildBounds[1]);
        Assert.Equal(new Rectangle(45, 25, 10, 10), result.ChildBounds[2]);
    }

    [Fact]
    public void Canvas_AttachedCoordinateHelpers_StoreAndReadMetadata()
    {
        MGElement element = new MGTextBlock(null as MGWindow, string.Empty);

        MGCanvas.SetLeft(element, 12);
        MGCanvas.SetTop(element, 18);
        MGCanvas.SetRight(element, 24);
        MGCanvas.SetBottom(element, 30);

        Assert.Equal(12, MGCanvas.GetLeft(element));
        Assert.Equal(18, MGCanvas.GetTop(element));
        Assert.Equal(24, MGCanvas.GetRight(element));
        Assert.Equal(30, MGCanvas.GetBottom(element));

        MGCanvas.SetLeft(element, null);
        Assert.Null(MGCanvas.GetLeft(element));
    }
}