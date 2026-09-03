using MGUI.Core.UI;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Input;

/// <summary>
/// Covers the docking migration piece of task 2 of the input-activation slice: <see cref="MGFloatingDockWindow"/>'s
/// former local click-to-front wiring (<c>MouseHandler.LMBPressedInside += (_, _) => BringToFront();</c>) was removed
/// in favor of the generic <see cref="MGWindow.ActivatesOnClick"/> mechanism (default <see langword="true"/>), so this
/// pins the pre-existing behavior still holds after the migration: clicking a floating dock window brings it to front.
/// </summary>
public class FloatingDockWindowActivationTests
{
    [Fact]
    public void ClickInsideFloatingDockWindow_BringsItToFront_ViaActivatesOnClick()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow parentWindow = new(desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(parentWindow);

        MGDockHost host = new(parentWindow);

        DockPanelNode panelA = new() { Title = "A", ContentFactory = () => new MGBorder(parentWindow) };
        DockPanelNode panelB = new() { Title = "B", ContentFactory = () => new MGBorder(parentWindow) };

        MGFloatingDockWindow floatA = new(host, panelA, 0, 0, 200, 150);
        MGFloatingDockWindow floatB = new(host, panelB, 300, 0, 200, 150);

        parentWindow.AddNestedWindow(floatA);
        parentWindow.AddNestedWindow(floatB); // added last => front-most

        desktop.Update();
        desktop.Update();

        Assert.True(floatA.ActivatesOnClick);
        Assert.Equal(new[] { floatA, floatB }, parentWindow.NestedWindows);

        // Click inside floatA, which does not overlap floatB, so the press unambiguously reaches floatA.
        Point clickInsideFloatA = new(floatA.Left + 20, floatA.Top + 20);
        Assert.False(floatB.LayoutBounds.Contains(clickInsideFloatA));
        Assert.True(floatA.LayoutBounds.Contains(clickInsideFloatA));

        AdvanceFrame(runtime, desktop, 32, clickInsideFloatA);
        AdvanceFrame(runtime, desktop, 48, clickInsideFloatA, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, clickInsideFloatA);

        Assert.Equal(new[] { floatB, floatA }, parentWindow.NestedWindows);
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

    private static MouseState CreateMouseState(Point position, MouseButton? pressedButton)
        => new(
            position.X,
            position.Y,
            0,
            pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
}
