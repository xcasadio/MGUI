using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Focus;

/// <summary>
/// Regression coverage for the navigation fallback front-to-back ordering fix: with no focused
/// or hovered element, auto-focus must resolve to the window on top of the visual stack (the
/// last window in <see cref="MGDesktop.Windows"/>, which the update loop processes first after
/// <c>Reverse()</c>), not the first one added.
/// </summary>
public class NavigationFrontToBackFallbackTests
{
    [Fact]
    public void MoveFocusNext_WithNoFocusOrHover_ResolvesToTopWindow_NotFirstAddedWindow()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow backWindow = new(desktop, 0, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton backButton = new(backWindow);
        backWindow.SetContent(backButton);

        MGWindow frontWindow = new(desktop, 250, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton frontButton = new(frontWindow);
        frontWindow.SetContent(frontButton);

        // Neither window is IsTopmost; frontWindow is added last, so it is on top of the visual
        // stack (matches the update loop's Windows.Reverse().OrderByDescending(IsTopmost)).
        desktop.Windows.Add(backWindow);
        desktop.Windows.Add(frontWindow);
        desktop.Update();
        desktop.Update();

        Assert.Null(desktop.FocusedKeyboardHandler);

        bool handled = desktop.NavigationService.MoveFocusNext(KeyboardFocusSource.Keyboard);
        desktop.Update();

        Assert.True(handled);
        Assert.Same(frontButton, desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void GetFocusableElements_WithNoFocusOrHover_ScopesToTopWindow()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow backWindow = new(desktop, 0, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton backButton = new(backWindow);
        backWindow.SetContent(backButton);

        MGWindow frontWindow = new(desktop, 250, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton frontButton = new(frontWindow);
        frontWindow.SetContent(frontButton);

        desktop.Windows.Add(backWindow);
        desktop.Windows.Add(frontWindow);
        desktop.Update();
        desktop.Update();

        var focusable = desktop.NavigationService.GetFocusableElements();

        Assert.Contains(frontButton, focusable);
        Assert.DoesNotContain(backButton, focusable);
    }

    /// <summary>Pins the <c>IsTopmost</c> dimension of the no-focus/no-hover fallback (task 11): a window flagged
    /// <see cref="MGWindow.IsTopmost"/> must resolve as the navigation root even though it is added FIRST to
    /// <see cref="MGDesktop.Windows"/> (so it would be last in <see cref="MGDesktop.Windows"/>'s own insertion
    /// order). Pins the <c>.OrderByDescending(x => x.IsTopmost)</c> clause at
    /// <c>UIFocusNavigationService.cs:404</c> (<c>GetNavigationRoot</c>'s <c>topWindowRoot</c>).</summary>
    [Fact]
    public void MoveFocusNext_WithIsTopmostWindow_AddedFirst_ResolvesToTopmostWindow_NotFirstAddedWindow()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        // topmostWindow is placed away from backWindow/frontWindow so no hover interferes with this fallback.
        MGWindow topmostWindow = new(desktop, 500, 300, 200, 200) { WindowStyle = WindowStyle.None, IsTopmost = true };
        MGButton topmostButton = new(topmostWindow);
        topmostWindow.SetContent(topmostButton);

        MGWindow backWindow = new(desktop, 0, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton backButton = new(backWindow);
        backWindow.SetContent(backButton);

        MGWindow frontWindow = new(desktop, 250, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton frontButton = new(frontWindow);
        frontWindow.SetContent(frontButton);

        // topmostWindow is added FIRST: without the IsTopmost sort, Windows.Reverse<MGWindow>() alone would put
        // it LAST, so this only passes because of the .OrderByDescending(x => x.IsTopmost) clause at :404.
        desktop.Windows.Add(topmostWindow);
        desktop.Windows.Add(backWindow);
        desktop.Windows.Add(frontWindow);
        // Unlike the two tests above, this uses the Frame helper (an explicit ApplyFrame with a real elapsed
        // time) rather than bare desktop.Update() calls: without ever calling ApplyFrame, GraphTestRuntime's
        // own default UpdateArgs (TimeSpan.Zero elapsed) leaves HoveredElement in a pre-layout state on the
        // window that sorts first, which the sanity asserts below would otherwise catch as a false positive.
        // (798, 598) is inside the 800x600 surface but outside all three windows.
        Frame(runtime, desktop, 0, new Point(798, 598));
        Frame(runtime, desktop, 16, new Point(798, 598));

        Assert.Null(desktop.FocusedKeyboardHandler);
        // Sanity: none of the three windows is hovered, so the resolution below exercises only the no-hover
        // fallback (topWindowRoot at :404), not the hovered-target path (:387) - if a future regression makes
        // one of them report a HoveredElement, this fails loudly instead of silently changing what this test
        // measures.
        Assert.Null(topmostWindow.HoveredElement);
        Assert.Null(backWindow.HoveredElement);
        Assert.Null(frontWindow.HoveredElement);

        var focusable = desktop.NavigationService.GetFocusableElements();
        Assert.Contains(topmostButton, focusable);
        Assert.DoesNotContain(frontButton, focusable);
        Assert.DoesNotContain(backButton, focusable);

        bool handled = desktop.NavigationService.MoveFocusNext(KeyboardFocusSource.Keyboard);
        desktop.Update();

        Assert.True(handled);
        Assert.Same(topmostButton, desktop.FocusedKeyboardHandler);
    }

    /// <summary>Pins the <c>IsTopmost</c> dimension of the hovered-target navigation fallback (task 11):
    /// <c>UIFocusNavigationService.cs:387</c> (<c>GetHoveredNavigationTarget</c>), reached only when some window
    /// actually has a real <see cref="MGWindow.HoveredElement"/>. To isolate this from the cross-window hover
    /// occlusion mechanism (task 4, <c>MGDesktop.cs:1417</c> / <c>:1433-1444</c>), <c>topmostWindow</c> is
    /// <see cref="MGWindow.AllowsClickThrough"/> and its own button is drawn below full opacity: the occlusion
    /// check's <c>FindFirstOpaqueParent</c> then walks past the (non-opaque) button up to the window itself,
    /// which is excluded from occlusion ("<c>OpaqueHoveredElement != Window</c>" at <c>MGDesktop.cs:1441</c>), so
    /// BOTH windows genuinely compute a non-null <see cref="MGWindow.HoveredElement"/> at the same overlap point
    /// this tick. The resolution below is then attributable only to the ordering at :387, never to one window's
    /// hover being suppressed by occlusion.</summary>
    [Fact]
    public void MoveFocusNext_WithRealHoverOverOverlappingWindows_ResolvesToIsTopmostWindow()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow topmostWindow = new(desktop, 100, 100, 200, 200)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
            IsTopmost = true,
            AllowsClickThrough = true
        };
        MGButton topmostButton = new(topmostWindow) { Opacity = 0.5f };
        topmostWindow.SetContent(topmostButton);

        MGWindow overlappingWindow = new(desktop, 150, 150, 200, 200) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        MGButton overlappingButton = new(overlappingWindow);
        overlappingWindow.SetContent(overlappingButton);

        // topmostWindow is added first: without the IsTopmost sort, Windows.Reverse<MGWindow>() alone would put
        // it LAST, so it would be checked after overlappingWindow at :387.
        desktop.Windows.Add(topmostWindow);
        desktop.Windows.Add(overlappingWindow);

        Frame(runtime, desktop, 0, new Point(1, 1));
        Frame(runtime, desktop, 16, new Point(1, 1));

        Rectangle overlap = Rectangle.Intersect(topmostButton.ActualLayoutBounds, overlappingButton.ActualLayoutBounds);
        Assert.True(overlap.Width > 0 && overlap.Height > 0, "test setup: the two windows' buttons must overlap");
        Point overlapPoint = overlap.Center;

        Frame(runtime, desktop, 32, overlapPoint);
        Frame(runtime, desktop, 48, overlapPoint);

        Assert.Null(desktop.FocusedKeyboardHandler);
        // Sanity: both windows genuinely have a live HoveredElement and neither is occluded - the outcome below
        // is not explainable by occlusion suppressing overlappingWindow's hover.
        Assert.Same(topmostButton, topmostWindow.HoveredElement);
        Assert.Same(overlappingButton, overlappingWindow.HoveredElement);
        Assert.False(topmostWindow.IsOccludedAtMousePos);
        Assert.False(overlappingWindow.IsOccludedAtMousePos);

        bool handled = desktop.NavigationService.MoveFocusNext(KeyboardFocusSource.Keyboard);
        desktop.Update();

        Assert.True(handled);
        Assert.Same(topmostButton, desktop.FocusedKeyboardHandler);
    }

    private static void Frame(GraphTestRuntime runtime, MGDesktop desktop, int totalElapsedMs, Point position)
    {
        MouseState mouse = new(position.X, position.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(totalElapsedMs), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
        desktop.Update();
    }
}
