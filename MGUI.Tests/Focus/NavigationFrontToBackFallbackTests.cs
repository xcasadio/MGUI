using MGUI.Core.UI;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
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
}
