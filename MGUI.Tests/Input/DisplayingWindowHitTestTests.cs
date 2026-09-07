using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Input;

/// <summary>
/// Covers <see cref="MGElement.DisplayingWindow"/> (see <c>Docs/decisions/0004-hit-test-occlusion-from-displaying-window.md</c>):
/// an element resolves hit-test occlusion, <see cref="MGWindow.HasModalWindow"/>, <see cref="MGWindow.HoveredElement"/> and
/// <see cref="MGWindow.PressedElement"/> from the window that actually displays it (first <see cref="MGWindow"/> found walking
/// its visual <see cref="MGElement.Parent"/> chain), not from <see cref="MGElement.SelfOrParentWindow"/> (the window it was
/// constructed with). Before this fix, an element built with one window but re-parented into another (e.g. application content
/// re-parented into a floating dock window) self-occluded, because that other window is registered as a nested window of the
/// construction window.
/// </summary>
public class DisplayingWindowHitTestTests
{
    [Fact]
    public void ElementReparentedIntoNestedWindow_IsHoveredAndPressed_ThroughTheNestedWindow()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };

        // Deep child: panel -> border -> button, all constructed with mainWindow.
        MGButton button = new(mainWindow);
        MGBorder border = new(mainWindow);
        border.SetContent(button);
        MGStackPanel panel = new(mainWindow, Orientation.Vertical);
        panel.TryAddChild(border);
        mainWindow.SetContent(panel);
        desktop.Windows.Add(mainWindow);

        Frame(runtime, desktop, 1, new Point(1, 1));
        Frame(runtime, desktop, 2, new Point(1, 1));

        // Step 1: hover the button in its construction window, priming the DisplayingWindow cache.
        Point onButtonInMain = button.LayoutBounds.Center;
        Frame(runtime, desktop, 3, onButtonInMain);
        Frame(runtime, desktop, 4, onButtonInMain);
        Assert.True(button.IsHovered);

        // Step 2: re-parent the panel root into a nested window positioned away from the original spot.
        MGWindow nested = new(mainWindow, 500, 400, 250, 150) { WindowStyle = WindowStyle.None };
        mainWindow.AddNestedWindow(nested);
        Frame(runtime, desktop, 5, new Point(1, 1));
        Frame(runtime, desktop, 6, new Point(1, 1));

        mainWindow.SetContent(null);
        nested.SetContent(panel);
        Frame(runtime, desktop, 7, new Point(1, 1));
        Frame(runtime, desktop, 8, new Point(1, 1));

        Point onButtonInNested = button.LayoutBounds.Center;
        Assert.False(mainWindow.LayoutBounds.Contains(onButtonInNested));
        Assert.True(nested.LayoutBounds.Contains(onButtonInNested));

        Frame(runtime, desktop, 9, onButtonInNested);
        Frame(runtime, desktop, 10, onButtonInNested);
        Assert.True(button.IsHovered);
        Assert.True(panel.IsHovered);

        Frame(runtime, desktop, 11, onButtonInNested, leftPressed: true);
        Assert.True(button.IsLMBPressed);
        Assert.NotNull(nested.PressedElement);
        Assert.True(IsSelfOrAncestorOf(nested.PressedElement, button));
        Assert.Null(mainWindow.PressedElement);

        Frame(runtime, desktop, 12, onButtonInNested);
    }

    // NAMED MUTATION (apply by hand, rebuild, run this test, then revert):
    // In MGElement.SetParent, stop incrementing _TreeTopologyGeneration and instead reset only the re-parented
    // instance's own _DisplayingWindowGeneration to -1 (i.e. invalidate the single element being re-parented,
    // not the whole process-wide generation). Expected effect: the deep button's DisplayingWindow cache (primed
    // in step 1, on a different instance than the one being re-parented) stays stale, so
    // ElementReparentedIntoNestedWindow_IsHoveredAndPressed_ThroughTheNestedWindow fails its "button.IsHovered"
    // assertion after step 2, while a root-panel-only assertion (panel.IsHovered) still passes because the panel
    // IS the re-parented instance and its own cache was reset directly.

    [Fact]
    public void ElementCoveredByAnotherNestedWindow_InItsOwnConstructionWindow_StaysOccluded()
    {
        // Regression pin of a331639 (z-order-aware hit-test): an element displayed in its own construction window
        // must still be occluded by an unrelated nested window drawn above it at the same position.
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        MGButton button = new(mainWindow);
        MGBorder border = new(mainWindow);
        border.SetContent(button);
        MGStackPanel panel = new(mainWindow, Orientation.Vertical);
        panel.TryAddChild(border);
        mainWindow.SetContent(panel);
        desktop.Windows.Add(mainWindow);
        Frame(runtime, desktop, 1, new Point(1, 1));
        Frame(runtime, desktop, 2, new Point(1, 1));

        Point onButton = button.LayoutBounds.Center;

        MGWindow covering = new(mainWindow, onButton.X - 20, onButton.Y - 20, 100, 100) { WindowStyle = WindowStyle.None };
        covering.SetContent(new MGButton(covering));
        mainWindow.AddNestedWindow(covering);
        Frame(runtime, desktop, 3, new Point(1, 1));
        Frame(runtime, desktop, 4, new Point(1, 1));
        Assert.True(covering.LayoutBounds.Contains(onButton));

        Frame(runtime, desktop, 5, onButton);
        Frame(runtime, desktop, 6, onButton);

        Assert.False(button.IsHovered);
        Assert.Null(mainWindow.HoveredElement);
    }

    [Fact]
    public void VisualStateOfReparentedElement_ReflectsTheNestedWindowsHoveredAndPressedElement()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        MGButton button = new(mainWindow);
        MGBorder border = new(mainWindow);
        border.SetContent(button);
        MGStackPanel panel = new(mainWindow, Orientation.Vertical);
        panel.TryAddChild(border);
        mainWindow.SetContent(panel);
        desktop.Windows.Add(mainWindow);
        Frame(runtime, desktop, 1, new Point(1, 1));
        Frame(runtime, desktop, 2, new Point(1, 1));

        Point onButtonInMain = button.LayoutBounds.Center;
        Frame(runtime, desktop, 3, onButtonInMain);
        Frame(runtime, desktop, 4, onButtonInMain);
        Assert.True(button.IsHovered);

        MGWindow nested = new(mainWindow, 500, 400, 250, 150) { WindowStyle = WindowStyle.None };
        mainWindow.AddNestedWindow(nested);
        Frame(runtime, desktop, 5, new Point(1, 1));
        Frame(runtime, desktop, 6, new Point(1, 1));

        mainWindow.SetContent(null);
        nested.SetContent(panel);
        Frame(runtime, desktop, 7, new Point(1, 1));
        Frame(runtime, desktop, 8, new Point(1, 1));

        Point onButtonInNested = button.LayoutBounds.Center;

        Frame(runtime, desktop, 9, onButtonInNested);
        Frame(runtime, desktop, 10, onButtonInNested);
        Assert.Equal(SecondaryVisualState.Hovered, button.VisualState.Secondary);

        Frame(runtime, desktop, 11, onButtonInNested, leftPressed: true);
        Assert.Equal(SecondaryVisualState.Pressed, button.VisualState.Secondary);

        Frame(runtime, desktop, 12, onButtonInNested);
    }

    // NAMED MUTATION (apply by hand, rebuild, run this test, then revert):
    // In MGElement.Update's VisualState computation, use SelfOrParentWindow again (instead of the resolved
    // DisplayingWindow local) for HasModalWindow / PressedElement / HoveredElement. Expected effect:
    // VisualStateOfReparentedElement_ReflectsTheNestedWindowsHoveredAndPressedElement fails - the button's
    // VisualState.Secondary never reports Hovered/Pressed after the re-parent, because mainWindow (the
    // construction window) never assigns HoveredElement/PressedElement to an element it no longer displays.

    [Fact]
    public void DetachedElement_NeverParented_ResolvesDisplayingWindowToParentWindow_AndDoesNotThrow()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(mainWindow);
        Frame(runtime, desktop, 1, new Point(1, 1));

        MGButton detached = new(mainWindow);
        // Never added to any content host / window content - Parent stays null.

        Assert.Null(detached.Parent);
        Assert.Same(mainWindow, detached.DisplayingWindow);
        bool isHovered = detached.IsHovered;
        Assert.False(isHovered);
    }

    private static bool IsSelfOrAncestorOf(MGElement ancestorCandidate, MGElement element)
    {
        for (MGElement current = element; current != null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestorCandidate))
            {
                return true;
            }
        }
        return false;
    }

    private static void Frame(GraphTestRuntime runtime, MGDesktop desktop, int frameIndex, Point position, bool leftPressed = false)
    {
        MouseState mouse = new(
            position.X,
            position.Y,
            0,
            leftPressed ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * frameIndex), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
        desktop.Update();
    }
}
