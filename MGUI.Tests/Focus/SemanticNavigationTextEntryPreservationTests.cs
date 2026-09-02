using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Input.Semantic;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Focus;

/// <summary>
/// Regression coverage for the fix aligning <see cref="MGDesktop.TryHandleInputAction"/> (semantic input
/// path) with the text-entry key preservation guard that the raw keyboard path already applies via
/// <c>MGTextBox.ShouldPreserveTextEntryKey</c>. A key that a focused, editable <see cref="MGTextBox"/>
/// reserves for editing (e.g. arrow keys, or Tab when <see cref="MGTextBox.AcceptsTab"/> is true) must
/// never be reinterpreted as UI navigation on either path, and must not leak to gameplay either.
/// </summary>
public class SemanticNavigationTextEntryPreservationTests
{
    [Fact]
    public void NavigateLeft_WithFocusedEditableTextBox_PreservesArrowKey_DoesNotDispatchNavigation()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGTextBox textBox = new(window);
        window.SetContent(textBox);
        desktop.Update();

        SetFocusedKeyboardHandler(desktop, textBox);

        InputActionEvent actionEvent = new(InputAction.NavigateLeft,
            new InputActionContext(InputActionSource.Keyboard, InputActionPhase.Pressed, System.TimeSpan.Zero, Key: Keys.Left));

        bool handled = desktop.TryHandleInputAction(actionEvent);

        Assert.False(handled);
        Assert.Same(textBox, desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void NavigateNext_WithFocusedEditableTextBox_AcceptsTab_PreservesTabKey_DoesNotMoveFocus()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGTextBox textBox = new(window) { AcceptsTab = true };
        MGButton button = new(window);
        MGStackPanel panel = new(window, Orientation.Vertical);
        panel.TryAddChild(textBox);
        panel.TryAddChild(button);
        window.SetContent(panel);
        desktop.Update();

        SetFocusedKeyboardHandler(desktop, textBox);

        InputActionEvent actionEvent = new(InputAction.NavigateNext,
            new InputActionContext(InputActionSource.Keyboard, InputActionPhase.Pressed, System.TimeSpan.Zero, Key: Keys.Tab));

        bool handled = desktop.TryHandleInputAction(actionEvent);

        Assert.False(handled);
        Assert.Same(textBox, desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void NavigateNext_WithFocusedEditableTextBox_AcceptsTabFalse_MovesFocusToNextElement()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGTextBox textBox = new(window) { AcceptsTab = false };
        MGButton button = new(window);
        MGStackPanel panel = new(window, Orientation.Vertical);
        panel.TryAddChild(textBox);
        panel.TryAddChild(button);
        window.SetContent(panel);
        desktop.Update();

        SetFocusedKeyboardHandler(desktop, textBox);

        InputActionEvent actionEvent = new(InputAction.NavigateNext,
            new InputActionContext(InputActionSource.Keyboard, InputActionPhase.Pressed, System.TimeSpan.Zero, Key: Keys.Tab));

        bool handled = desktop.TryHandleInputAction(actionEvent);
        desktop.Update();

        Assert.True(handled);
        Assert.Same(button, desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void NavigateNext_WithoutFocusedTextBox_NavigatesNormally()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGButton first = new(window);
        MGButton second = new(window);
        MGStackPanel panel = new(window, Orientation.Vertical);
        panel.TryAddChild(first);
        panel.TryAddChild(second);
        window.SetContent(panel);
        desktop.Update();

        Assert.Null(desktop.FocusedKeyboardHandler);

        InputActionEvent actionEvent = new(InputAction.NavigateNext,
            new InputActionContext(InputActionSource.Keyboard, InputActionPhase.Pressed, System.TimeSpan.Zero, Key: Keys.Tab));

        bool handled = desktop.TryHandleInputAction(actionEvent);
        desktop.Update();

        Assert.True(handled);
        Assert.NotNull(desktop.FocusedKeyboardHandler);
    }

    private static MGDesktop CreateDesktopWithSingleWindow(out MGWindow window)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        window = new MGWindow(desktop, 0, 0, 400, 400) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();
        return desktop;
    }

    private static void SetFocusedKeyboardHandler(MGDesktop desktop, MGElement element)
    {
        MethodInfo setter = typeof(MGDesktop).GetProperty(nameof(MGDesktop.FocusedKeyboardHandler), BindingFlags.Public | BindingFlags.Instance)!.GetSetMethod(true)!;
        setter.Invoke(desktop, new object[] { element });
    }
}
