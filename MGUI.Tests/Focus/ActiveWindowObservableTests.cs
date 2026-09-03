using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System.Reflection;

namespace MGUI.Tests.Focus;

/// <summary>
/// Covers task 1 of the input-activation slice: <see cref="MGDesktop.ActiveWindow"/> is a read-only value
/// derived from <see cref="MGDesktop.FocusedKeyboardHandler"/>, with no click-activation wiring in this slice.
/// </summary>
public class ActiveWindowObservableTests
{
    [Fact]
    public void FocusingElementInAnotherWindow_UpdatesActiveWindow_WithoutReorderingWindows()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow windowA = new(desktop, 0, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton buttonA = new(windowA);
        windowA.SetContent(buttonA);

        MGWindow windowB = new(desktop, 250, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton buttonB = new(windowB);
        windowB.SetContent(buttonB);

        desktop.Windows.Add(windowA);
        desktop.Windows.Add(windowB);
        desktop.Update();
        desktop.Update();

        SetFocusedKeyboardHandler(desktop, buttonA);
        Assert.Same(windowA, desktop.ActiveWindow);

        List<MGWindow> windowOrderBeforeFocusChange = new(desktop.Windows);

        int raisedCount = 0;
        MGWindow eventPreviousValue = null;
        MGWindow eventNewValue = null;
        desktop.ActiveWindowChanged += (sender, e) =>
        {
            raisedCount++;
            eventPreviousValue = e.PreviousValue;
            eventNewValue = e.NewValue;
        };

        SetFocusedKeyboardHandler(desktop, buttonB);

        Assert.Same(windowB, desktop.ActiveWindow);
        Assert.Equal(1, raisedCount);
        Assert.Same(windowA, eventPreviousValue);
        Assert.Same(windowB, eventNewValue);

        // No activation wiring exists in this slice: focusing an element must never call BringToFront,
        // so the window order (i.e. z-order in desktop.Windows) must be unchanged by the focus change.
        Assert.Equal(windowOrderBeforeFocusChange, desktop.Windows);
    }

    [Fact]
    public void ActiveWindow_WithNoFocusedKeyboardHandler_IsNull()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow window = new(desktop, 0, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton button = new(window);
        window.SetContent(button);

        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();

        Assert.Null(desktop.FocusedKeyboardHandler);
        Assert.Null(desktop.ActiveWindow);
    }

    [Fact]
    public void SettingSameWindowFocusTwice_DoesNotRaiseActiveWindowChangedAgain()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow window = new(desktop, 0, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton buttonOne = new(window);
        MGButton buttonTwo = new(window);
        MGStackPanel panel = new(window, Orientation.Vertical);
        panel.TryAddChild(buttonOne);
        panel.TryAddChild(buttonTwo);
        window.SetContent(panel);

        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();

        SetFocusedKeyboardHandler(desktop, buttonOne);
        Assert.Same(window, desktop.ActiveWindow);

        int raisedCount = 0;
        desktop.ActiveWindowChanged += (sender, e) => raisedCount++;

        // Moving keyboard focus between two elements that both belong to the same window must not
        // raise ActiveWindowChanged, since the derived ActiveWindow value does not change.
        SetFocusedKeyboardHandler(desktop, buttonTwo);

        Assert.Same(window, desktop.ActiveWindow);
        Assert.Equal(0, raisedCount);
    }

    private static void SetFocusedKeyboardHandler(MGDesktop desktop, MGElement element)
    {
        MethodInfo setter = typeof(MGDesktop).GetProperty(nameof(MGDesktop.FocusedKeyboardHandler), BindingFlags.Public | BindingFlags.Instance)!.GetSetMethod(true)!;
        setter.Invoke(desktop, new object[] { element });
    }
}
