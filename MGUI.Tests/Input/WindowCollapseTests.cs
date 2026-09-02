using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using MGUI.Shared.Input.Mouse;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Input;

/// <summary>
/// <see cref="MGWindow.IsCollapsed"/> shades a window down to its title bar and restores it;
/// <see cref="MGWindow.CollapseOnTitleBarDoubleClick"/> toggles it from a double-click on the title bar.
/// </summary>
public class WindowCollapseTests
{
    private const int WindowHeight = 240;

    [Fact]
    public void IsCollapsed_ShrinksToTitleBar_AndRestoresHeightContentAndResizability()
    {
        var (runtime, desktop, window, content) = CreateWindow();
        AdvanceFrame(runtime, desktop, 16, new Point(300, 300));

        int titleBarHeight = window.TitleBarComponent.Element.LayoutBounds.Height;
        Assert.True(titleBarHeight > 0);

        window.IsCollapsed = true;
        AdvanceFrame(runtime, desktop, 32, new Point(300, 300));

        Assert.True(window.IsCollapsed);
        Assert.Equal(Visibility.Collapsed, content.Visibility);
        Assert.False(window.IsUserResizable);
        Assert.True(window.WindowHeight < WindowHeight / 2);
        Assert.True(window.WindowHeight >= titleBarHeight);

        window.IsCollapsed = false;
        AdvanceFrame(runtime, desktop, 48, new Point(300, 300));

        Assert.False(window.IsCollapsed);
        Assert.Equal(Visibility.Visible, content.Visibility);
        Assert.True(window.IsUserResizable);
        Assert.Equal(WindowHeight, window.WindowHeight);
    }

    [Fact]
    public void TitleBarDoubleClick_TogglesCollapse_OnlyWhenEnabled()
    {
        var (runtime, desktop, window, _) = CreateWindow();
        AdvanceFrame(runtime, desktop, 16, new Point(300, 300));

        var titleBar = window.TitleBarComponent.Element.LayoutBounds;
        var clickPoint = new Point(titleBar.Left + 20, titleBar.Top + titleBar.Height / 2);

        DoubleClick(runtime, desktop, 100, clickPoint);
        Assert.False(window.IsCollapsed);

        window.CollapseOnTitleBarDoubleClick = true;
        DoubleClick(runtime, desktop, 2000, clickPoint);
        Assert.True(window.IsCollapsed);

        DoubleClick(runtime, desktop, 4000, clickPoint);
        Assert.False(window.IsCollapsed);
        Assert.Equal(WindowHeight, window.WindowHeight);
    }

    [Fact]
    public void DoubleClick_OutsideTitleBar_DoesNotCollapse()
    {
        var (runtime, desktop, window, _) = CreateWindow();
        window.CollapseOnTitleBarDoubleClick = true;
        AdvanceFrame(runtime, desktop, 16, new Point(300, 300));

        DoubleClick(runtime, desktop, 100, new Point(200, 150));
        Assert.False(window.IsCollapsed);
    }

    private static (GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, MGBorder Content) CreateWindow()
    {
        var runtime = new GraphTestRuntime(new Rectangle(0, 0, 480, 320));
        var desktop = new MGDesktop(runtime);
        var window = new MGWindow(desktop, 0, 0, 400, WindowHeight)
        {
            TitleText = "Collapse me",
            IsUserResizable = true,
        };
        var content = new MGBorder(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        window.SetContent(content);
        desktop.Windows.Add(window);
        return (runtime, desktop, window, content);
    }

    private static void DoubleClick(GraphTestRuntime runtime, MGDesktop desktop, int startMs, Point position)
    {
        AdvanceFrame(runtime, desktop, startMs, position);
        AdvanceFrame(runtime, desktop, startMs + 16, position, MouseButton.Left);
        AdvanceFrame(runtime, desktop, startMs + 32, position);
        AdvanceFrame(runtime, desktop, startMs + 96, position, MouseButton.Left);
        AdvanceFrame(runtime, desktop, startMs + 112, position);
        AdvanceFrame(runtime, desktop, startMs + 128, position);
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

    private static MouseState CreateMouseState(Point position, MouseButton? pressedButton = null)
        => new(
            position.X,
            position.Y,
            0,
            pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Middle ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
}
