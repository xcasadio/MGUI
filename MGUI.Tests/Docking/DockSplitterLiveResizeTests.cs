using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Rendering;
using MGUI.Shared.Input.Mouse;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Docking;

public class DockSplitterLiveResizeTests
{
    [Fact]
    public void SetSplitRatioWithoutSync_UpdatesChildBounds_OnNextFrame()
    {
        var harness = CreateHarness();

        AdvanceFrame(harness.Runtime, harness.Desktop, 0, Point.Zero);
        AdvanceFrame(harness.Runtime, harness.Desktop, 16, Point.Zero);

        int initialFirstWidth = harness.FirstChild.LayoutBounds.Width;

        harness.SplitContainer.SetSplitRatioWithoutSync(0.75f);

        AdvanceFrame(harness.Runtime, harness.Desktop, 32, Point.Zero);

        Assert.True(harness.FirstChild.LayoutBounds.Width > initialFirstWidth);
    }

    [Fact]
    public void SplitterDrag_UpdatesChildBounds_BeforeMouseRelease()
    {
        var harness = CreateHarness();

        AdvanceFrame(harness.Runtime, harness.Desktop, 0, Point.Zero);
        AdvanceFrame(harness.Runtime, harness.Desktop, 16, Point.Zero);

        Rectangle splitterBounds = harness.SplitContainer.SplitterBarLayoutBounds;
        Assert.False(splitterBounds.IsEmpty);

        Point pressPoint = splitterBounds.Center;
        Point dragPoint = new(pressPoint.X + 96, pressPoint.Y);
        int initialFirstWidth = harness.FirstChild.LayoutBounds.Width;

        AdvanceFrame(harness.Runtime, harness.Desktop, 32, pressPoint, MouseButton.Left);
        AdvanceFrame(harness.Runtime, harness.Desktop, 48, dragPoint, MouseButton.Left);
        AdvanceFrame(harness.Runtime, harness.Desktop, 64, dragPoint, MouseButton.Left);

        Assert.True(harness.FirstChild.LayoutBounds.Width > initialFirstWidth);

        AdvanceFrame(harness.Runtime, harness.Desktop, 80, dragPoint);
    }

    [Fact]
    public void SplitterDrag_SuppressesUnrelatedMouseMoveHandlers_UntilRelease()
    {
        var harness = CreateHarness();

        AdvanceFrame(harness.Runtime, harness.Desktop, 0, Point.Zero);
        AdvanceFrame(harness.Runtime, harness.Desktop, 16, Point.Zero);

        int enteredCount = 0;
        int movedInsideCount = 0;
        harness.SecondChild.MouseHandler.Entered += (_, _) => enteredCount++;
        harness.SecondChild.MouseHandler.MovedInside += (_, _) => movedInsideCount++;

        Rectangle splitterBounds = harness.SplitContainer.SplitterBarLayoutBounds;
        Assert.False(splitterBounds.IsEmpty);

        Point pressPoint = splitterBounds.Center;
        Point secondChildPoint = new(harness.SecondChild.LayoutBounds.Center.X, harness.SecondChild.LayoutBounds.Center.Y);

        AdvanceFrame(harness.Runtime, harness.Desktop, 32, pressPoint, MouseButton.Left);
        AdvanceFrame(harness.Runtime, harness.Desktop, 48, secondChildPoint, MouseButton.Left);
        AdvanceFrame(harness.Runtime, harness.Desktop, 64, secondChildPoint, MouseButton.Left);

        Assert.Equal(0, enteredCount);
        Assert.Equal(0, movedInsideCount);

        AdvanceFrame(harness.Runtime, harness.Desktop, 80, secondChildPoint);
    }

    [Fact]
    public void DockHostSplitterDrag_UpdatesActivePanelContentBounds_BeforeMouseRelease()
    {
        var runtime = new GraphTestRuntime(new Rectangle(0, 0, 480, 320));
        var desktop = new MGDesktop(runtime);
        var window = new MGWindow(desktop, 0, 0, 400, 240)
        {
            WindowStyle = WindowStyle.None,
        };

        var leftContent = new MGBorder(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var rightContent = new MGBorder(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var leftPanel = new DockPanelNode
        {
            Title = "Left",
            ContentFactory = () => leftContent,
        };

        var rightPanel = new DockPanelNode
        {
            Title = "Right",
            ContentFactory = () => rightContent,
        };

        var leftGroup = new DockTabGroupNode();
        leftGroup.AddPanel(leftPanel, -1);

        var rightGroup = new DockTabGroupNode();
        rightGroup.AddPanel(rightPanel, -1);

        var rootSplit = new DockSplitNode
        {
            Orientation = Orientation.Horizontal,
            SplitRatio = 0.5f,
            MinFirstSize = 0,
            MinSecondSize = 0,
            FirstChild = leftGroup,
            SecondChild = rightGroup,
        };

        var host = new MGDockHost(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LayoutModel = new DockLayoutModel(rootSplit),
        };

        window.SetContent(host);
        desktop.Windows.Add(window);

        AdvanceFrame(runtime, desktop, 0, Point.Zero);
        AdvanceFrame(runtime, desktop, 16, Point.Zero);

        var splitContainer = Assert.IsType<MGDockSplitContainer>(host.Content);
        Rectangle splitterBounds = splitContainer.SplitterBarLayoutBounds;
        Assert.False(splitterBounds.IsEmpty);

        Point pressPoint = splitterBounds.Center;
        Point dragPoint = new(pressPoint.X + 96, pressPoint.Y);
        int initialLeftWidth = leftContent.LayoutBounds.Width;

        AdvanceFrame(runtime, desktop, 32, pressPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 48, dragPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, dragPoint, MouseButton.Left);

        Assert.True(leftContent.LayoutBounds.Width > initialLeftWidth);

        AdvanceFrame(runtime, desktop, 80, dragPoint);
    }

    [Fact]
    public void WrappedDockHostSplitterDrag_UpdatesActivePanelContentBounds_BeforeMouseRelease()
    {
        var runtime = new GraphTestRuntime(new Rectangle(0, 0, 480, 320));
        var desktop = new MGDesktop(runtime);
        var window = new MGWindow(desktop, 0, 0, 400, 240)
        {
            WindowStyle = WindowStyle.None,
        };

        var leftContent = new MGBorder(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var rightContent = new MGBorder(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var leftPanel = new DockPanelNode
        {
            Title = "Left",
            ContentFactory = () => leftContent,
        };

        var rightPanel = new DockPanelNode
        {
            Title = "Right",
            ContentFactory = () => rightContent,
        };

        var leftGroup = new DockTabGroupNode();
        leftGroup.AddPanel(leftPanel, -1);

        var rightGroup = new DockTabGroupNode();
        rightGroup.AddPanel(rightPanel, -1);

        var rootSplit = new DockSplitNode
        {
            Orientation = Orientation.Horizontal,
            SplitRatio = 0.5f,
            MinFirstSize = 0,
            MinSecondSize = 0,
            FirstChild = leftGroup,
            SecondChild = rightGroup,
        };

        var host = new MGDockHost(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LayoutModel = new DockLayoutModel(rootSplit),
        };

        var rootPanel = new MGDockPanel(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        rootPanel.TryAddChild(host, Dock.Top);

        window.SetContent(rootPanel);
        desktop.Windows.Add(window);

        AdvanceFrame(runtime, desktop, 0, Point.Zero);
        AdvanceFrame(runtime, desktop, 16, Point.Zero);

        var splitContainer = Assert.IsType<MGDockSplitContainer>(host.Content);
        Rectangle splitterBounds = splitContainer.SplitterBarLayoutBounds;
        Assert.False(splitterBounds.IsEmpty);

        Point pressPoint = splitterBounds.Center;
        Point dragPoint = new(pressPoint.X + 96, pressPoint.Y);
        int initialLeftWidth = leftContent.LayoutBounds.Width;

        AdvanceFrame(runtime, desktop, 32, pressPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 48, dragPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, dragPoint, MouseButton.Left);

        Assert.True(leftContent.LayoutBounds.Width > initialLeftWidth);

        AdvanceFrame(runtime, desktop, 80, dragPoint);
    }

    private static DockSplitterHarness CreateHarness()
    {
        var runtime = new GraphTestRuntime(new Rectangle(0, 0, 480, 320));
        var desktop = new MGDesktop(runtime);
        var window = new MGWindow(desktop, 0, 0, 400, 240)
        {
            WindowStyle = WindowStyle.None,
        };

        var splitContainer = new MGDockSplitContainer(window, Orientation.Horizontal)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            SplitRatio = 0.5f,
            SplitterThickness = 4,
            MinFirstSize = 0,
            MinSecondSize = 0,
        };

        var firstChild = new MGBorder(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var secondChild = new MGBorder(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        splitContainer.FirstChild = firstChild;
        splitContainer.SecondChild = secondChild;
        window.SetContent(splitContainer);
        desktop.Windows.Add(window);

        return new DockSplitterHarness(runtime, desktop, window, splitContainer, firstChild, secondChild);
    }

    private static void AdvanceFrame(GraphTestRuntime runtime, MGDesktop desktop, int totalElapsedMs, Point position, MouseButton? pressedButton = null)
    {
        runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(totalElapsedMs),
            TimeSpan.FromMilliseconds(16),
            CreateMouseState(position, pressedButton),
            new KeyboardState()));
        desktop.Update();
    }

    private static MouseState CreateMouseState(Point position, MouseButton? pressedButton = null, int scrollWheel = 0)
        => new(
            position.X,
            position.Y,
            scrollWheel,
            pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Middle ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);

    private readonly record struct DockSplitterHarness(
        GraphTestRuntime Runtime,
        MGDesktop Desktop,
        MGWindow Window,
        MGDockSplitContainer SplitContainer,
        MGBorder FirstChild,
        MGBorder SecondChild);
}