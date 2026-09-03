using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System.Reflection;
using Xunit;

namespace MGUI.Tests.Input;

/// <summary>
/// Covers task 2 of the input-activation slice: <see cref="MGWindow.ActivatesOnClick"/> (default <see langword="true"/>),
/// documented in Docs/input-window-activation-design.md section 3.a. A press inside a window brings it to front
/// (<see cref="MGDesktop.BringToFront(MGWindow)"/>/<see cref="MGWindow.BringToFront(MGWindow)"/>) and moves keyboard
/// focus into it via <see cref="MGDesktop.ResolveAutoFocusTarget(MGElement, bool)"/> with <c>preferWindowDefault: false</c>,
/// guarded by the existing modal guard (<see cref="MGDesktop.IsBlockedByModalOrOverlay(MGElement)"/>), and never overrides
/// a focus the click itself already set on a focusable descendant.
/// </summary>
public class WindowActivationOnClickTests
{
    [Fact]
    public void ClickOnBackgroundWindow_BringsToFront_ResolvesFocusToFirstFocusable_UpdatesActiveWindow()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow windowA = new(desktop, 0, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton buttonA = new(windowA);
        windowA.SetContent(buttonA);

        MGWindow windowB = new(desktop, 250, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton buttonB = new(windowB)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            PreferredWidth = 40,
            PreferredHeight = 30,
        };
        windowB.SetContent(buttonB);

        desktop.Windows.Add(windowA);
        desktop.Windows.Add(windowB);
        desktop.Update();
        desktop.Update();

        // windowB is added last (front); bring windowA to front instead, pushing windowB to the back so the
        // click below has to bring it forward again. Put keyboard focus on windowA to observe the resolution and ActiveWindow.
        desktop.BringToFront(windowA);
        SetFocusedKeyboardHandler(desktop, buttonA);
        Assert.Same(windowA, desktop.ActiveWindow);
        Assert.Equal(new List<MGWindow> { windowB, windowA }, desktop.Windows);

        int activeWindowChangedCount = 0;
        MGWindow eventPreviousValue = null;
        MGWindow eventNewValue = null;
        desktop.ActiveWindowChanged += (sender, e) =>
        {
            activeWindowChangedCount++;
            eventPreviousValue = e.PreviousValue;
            eventNewValue = e.NewValue;
        };

        // Click windowB's empty body (not on buttonB), so the resolved auto-focus target - not a click-driven
        // focus on the clicked element itself - is what ends up focused.
        Point emptyBodyPoint = new(windowB.Left + 150, windowB.Top + 150);
        Assert.False(buttonB.ActualLayoutBounds.Contains(emptyBodyPoint));

        AdvanceFrame(runtime, desktop, 32, emptyBodyPoint);
        AdvanceFrame(runtime, desktop, 48, emptyBodyPoint, MouseButton.Left);

        // BringToFront happens synchronously on the press tick.
        Assert.Equal(new List<MGWindow> { windowA, windowB }, desktop.Windows);

        AdvanceFrame(runtime, desktop, 64, emptyBodyPoint); // release; ApplyQueuedFocusChange() runs at the start of this Update()

        Assert.Same(buttonB, desktop.FocusedKeyboardHandler);
        Assert.Same(windowB, desktop.ActiveWindow);
        Assert.Equal(1, activeWindowChangedCount);
        Assert.Same(windowA, eventPreviousValue);
        Assert.Same(windowB, eventNewValue);
    }

    [Fact]
    public void ClickOnWindowWithNoFocusableDescendant_BringsToFront_LeavesFocusUnchanged()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow windowA = new(desktop, 0, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton buttonA = new(windowA);
        windowA.SetContent(buttonA);

        MGWindow windowB = new(desktop, 250, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGBorder borderB = new(windowB);
        windowB.SetContent(borderB); // no focusable descendant

        desktop.Windows.Add(windowB);
        desktop.Windows.Add(windowA); // windowA in front, windowB behind
        desktop.Update();
        desktop.Update();

        SetFocusedKeyboardHandler(desktop, buttonA);
        Assert.Same(buttonA, desktop.FocusedKeyboardHandler);

        Point windowBBodyPoint = new(windowB.Left + 100, windowB.Top + 100);
        AdvanceFrame(runtime, desktop, 32, windowBBodyPoint);
        AdvanceFrame(runtime, desktop, 48, windowBBodyPoint, MouseButton.Left);

        // Brought to front despite having no valid auto-focus target.
        Assert.Same(windowB, desktop.Windows[^1]);

        AdvanceFrame(runtime, desktop, 64, windowBBodyPoint);

        // No valid resolution target: the pre-existing focus (and therefore ActiveWindow) is left untouched.
        Assert.Same(buttonA, desktop.FocusedKeyboardHandler);
        Assert.Same(windowA, desktop.ActiveWindow);
    }

    [Fact]
    public void ClickOnFocusableElement_FocusesThatElement_NotTheResolvedAutoFocusTarget()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow window = new(desktop, 0, 0, 200, 300) { WindowStyle = WindowStyle.None };
        MGButton defaultButton = new(window) { PreferredWidth = 150, PreferredHeight = 30 };
        MGButton clickedButton = new(window) { PreferredWidth = 150, PreferredHeight = 30 };
        MGStackPanel panel = new(window, Orientation.Vertical);
        panel.TryAddChild(defaultButton);
        panel.TryAddChild(clickedButton);
        window.SetContent(panel);
        window.DefaultFocusElement = defaultButton;

        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();

        Assert.Null(desktop.FocusedKeyboardHandler);

        Point clickedButtonCenter = clickedButton.ActualLayoutBounds.Center;
        Assert.False(defaultButton.ActualLayoutBounds.Contains(clickedButtonCenter));

        AdvanceFrame(runtime, desktop, 32, clickedButtonCenter);
        AdvanceFrame(runtime, desktop, 48, clickedButtonCenter, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, clickedButtonCenter);

        // The click itself focused clickedButton (MGElement.IsFocusable's own auto-focus-on-click); the
        // window's click-activation must not override it with DefaultFocusElement.
        Assert.Same(clickedButton, desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void ClickActivation_PrefersWindowFocusHistoryOverDefaultFocusElement()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow otherWindow = new(desktop, 0, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton otherButton = new(otherWindow);
        otherWindow.SetContent(otherButton);

        MGWindow window = new(desktop, 250, 0, 200, 300) { WindowStyle = WindowStyle.None };
        MGButton defaultButton = new(window) { PreferredWidth = 150, PreferredHeight = 30 };
        MGButton historyButton = new(window) { PreferredWidth = 150, PreferredHeight = 30 };
        MGStackPanel panel = new(window, Orientation.Vertical);
        panel.TryAddChild(defaultButton);
        panel.TryAddChild(historyButton);
        window.SetContent(panel);
        window.DefaultFocusElement = defaultButton;

        desktop.Windows.Add(otherWindow);
        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();

        // Focus historyButton (records it in WindowFocusHistory[window]) then move focus away to otherWindow,
        // leaving window with DefaultFocusElement=defaultButton AND a WindowFocusHistory entry=historyButton
        // that differs from it.
        SetFocusedKeyboardHandler(desktop, historyButton);
        SetFocusedKeyboardHandler(desktop, otherButton);
        Assert.Same(otherWindow, desktop.ActiveWindow);

        // Click window's empty body so resolution (not a direct click-on-focusable) decides the target.
        Point emptyBodyPoint = new(window.Left + 175, window.Top + 250);
        Assert.False(defaultButton.ActualLayoutBounds.Contains(emptyBodyPoint));
        Assert.False(historyButton.ActualLayoutBounds.Contains(emptyBodyPoint));

        AdvanceFrame(runtime, desktop, 32, emptyBodyPoint);
        AdvanceFrame(runtime, desktop, 48, emptyBodyPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, emptyBodyPoint);

        // preferWindowDefault: false => lastFocused (WindowFocusHistory) wins over DefaultFocusElement.
        Assert.Same(historyButton, desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void ClickActivation_HasNoEffect_WhileBlockedByActiveModalWindow()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow windowA = new(desktop, 0, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton buttonA = new(windowA);
        windowA.SetContent(buttonA);

        MGWindow windowB = new(desktop, 250, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton buttonB = new(windowB);
        windowB.SetContent(buttonB);

        // A modal child, positioned away from the click point below - HasModalWindow is a pure per-window flag,
        // not geometry-dependent, so this is a clean test of the modal guard rather than of occlusion.
        MGWindow modalChild = new(windowB, 900, 900, 10, 10) { WindowStyle = WindowStyle.None };
        windowB.PushModalWindow(modalChild);

        desktop.Windows.Add(windowB); // windowB behind
        desktop.Windows.Add(windowA); // windowA in front
        desktop.Update();
        desktop.Update();

        Assert.True(windowB.HasModalWindow);
        SetFocusedKeyboardHandler(desktop, buttonA);

        List<MGWindow> windowOrderBeforeClick = new(desktop.Windows);
        Point windowBBodyPoint = new(windowB.Left + 100, windowB.Top + 100);

        AdvanceFrame(runtime, desktop, 32, windowBBodyPoint);
        AdvanceFrame(runtime, desktop, 48, windowBBodyPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, windowBBodyPoint);

        Assert.Equal(windowOrderBeforeClick, desktop.Windows);
        Assert.Same(buttonA, desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void OverlappingWindows_OccludedPress_DoesNotRaiseBackWindow_ExposedPress_DoesRaiseIt()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow backWindow = new(desktop, 0, 0, 300, 200) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        MGButton backButton = new(backWindow);
        backWindow.SetContent(backButton);

        MGWindow frontWindow = new(desktop, 150, 0, 300, 200) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        MGButton frontButton = new(frontWindow);
        frontWindow.SetContent(frontButton);

        // frontWindow added last => on top of the visual stack (matches the pattern established by
        // NavigationFrontToBackFallbackTests / OverlappingWindowsInputRoutingTests).
        desktop.Windows.Add(backWindow);
        desktop.Windows.Add(frontWindow);
        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        Rectangle overlap = Rectangle.Intersect(backButton.ActualLayoutBounds, frontButton.ActualLayoutBounds);
        Assert.True(overlap.Width > 0 && overlap.Height > 0);
        Point overlapPoint = overlap.Center;

        // Direction 1: a press in the occluded overlap region must not raise backWindow to front.
        AdvanceFrame(runtime, desktop, 32, overlapPoint);
        AdvanceFrame(runtime, desktop, 48, overlapPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, overlapPoint);

        Assert.Equal(new List<MGWindow> { backWindow, frontWindow }, desktop.Windows);

        // Direction 2: a press on backWindow's exposed (non-overlapping) area must raise it to front.
        Point exposedBackPoint = new(backWindow.Left + 10, backWindow.Top + 10);
        Assert.False(frontWindow.LayoutBounds.Contains(exposedBackPoint));
        Assert.True(backWindow.LayoutBounds.Contains(exposedBackPoint));

        AdvanceFrame(runtime, desktop, 80, exposedBackPoint);
        AdvanceFrame(runtime, desktop, 96, exposedBackPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 112, exposedBackPoint);

        Assert.Equal(new List<MGWindow> { frontWindow, backWindow }, desktop.Windows);
    }

    [Fact]
    public void ContextMenu_ToolTip_ComboBoxDropdown_ColorPickerPopup_DefaultToActivatesOnClickFalse()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 400) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(window);

        MGContextMenu contextMenu = new(window);
        Assert.False(contextMenu.ActivatesOnClick);

        MGButton toolTipHost = new(window);
        MGToolTip toolTip = new(window, toolTipHost, 100, 50);
        Assert.False(toolTip.ActivatesOnClick);

        MGComboBox<string> comboBox = new(window);
        comboBox.SetItemsSource(new List<string> { "Alpha", "Bravo" });
        window.SetContent(comboBox);
        desktop.Update();
        desktop.Update();
        Assert.False(comboBox.Dropdown.ActivatesOnClick);

        MGColorPickerPopup colorPickerPopup = new(window);
        Assert.False(colorPickerPopup.PopupWindow.ActivatesOnClick);
    }

    [Fact]
    public void ClickOnItemInsideOpenComboBoxDropdown_DoesNotReorderNestedWindows()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 300, 300) { WindowStyle = WindowStyle.None };
        MGComboBox<string> comboBox = new(window)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            PreferredWidth = 100,
            PreferredHeight = 30,
        };
        comboBox.SetItemsSource(new List<string> { "Alpha", "Bravo", "Charlie" });
        window.SetContent(comboBox);
        desktop.Windows.Add(window);
        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        // A second nested window on the SAME parent, positioned far from the dropdown, added AFTER the
        // dropdown opens - so it starts as the front-most nested window. If the dropdown's own
        // ActivatesOnClick were (wrongly) true, clicking inside it would call ParentWindow.BringToFront(Dropdown)
        // and move it past sibling, changing the NestedWindows order asserted below.
        comboBox.IsDropdownOpen = true;
        AdvanceFrame(runtime, desktop, 32, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 48, new Point(1, 1));
        MGWindow dropdown = comboBox.Dropdown;
        Assert.Contains(dropdown, window.NestedWindows);

        MGWindow sibling = new(window, 250, 250, 20, 20) { WindowStyle = WindowStyle.None };
        window.AddNestedWindow(sibling);

        List<MGWindow> nestedOrderBeforeClick = new(window.NestedWindows);
        Assert.Same(dropdown, nestedOrderBeforeClick[0]);
        Assert.Same(sibling, nestedOrderBeforeClick[^1]);

        Point itemPoint = dropdown.LayoutBounds.Center;
        Assert.False(sibling.LayoutBounds.Contains(itemPoint));

        // BringToFront (if the exclusion were missing) happens synchronously on the press tick, so check right
        // after the press - selecting the item on release closes the dropdown, which would otherwise remove it
        // from NestedWindows entirely and make this assertion moot.
        AdvanceFrame(runtime, desktop, 64, itemPoint);
        AdvanceFrame(runtime, desktop, 80, itemPoint, MouseButton.Left);

        Assert.Contains(dropdown, window.NestedWindows);
        Assert.Equal(nestedOrderBeforeClick, window.NestedWindows);

        AdvanceFrame(runtime, desktop, 96, itemPoint); // release
    }

    [Fact]
    public void ClickInsideOpenContextMenu_DoesNotBringWindowsForward_DoesNotDisturbFocus()
    {
        // MGContextMenu is managed through a single-slot ActiveContextMenu reference (MGDesktop.ActiveContextMenu /
        // MGContextMenu.ActiveContextMenu for submenus), not through the Windows/NestedWindows z-order lists, so
        // clicking inside one cannot literally "reorder nested windows" the way the ComboBox dropdown or color
        // picker popup can. What ActivatesOnClick=false actually guards here is the same click-driven side effects
        // the other three popups are excluded from: no BringToFront call and no auto-focus-resolution churn from
        // clicking around inside an already-open menu.
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow otherWindow = new(desktop, 400, 0, 200, 200) { WindowStyle = WindowStyle.None };
        MGButton otherButton = new(otherWindow);
        otherWindow.SetContent(otherButton);

        MGWindow ownerWindow = new(desktop, 0, 0, 300, 300) { WindowStyle = WindowStyle.None };

        desktop.Windows.Add(ownerWindow);
        desktop.Windows.Add(otherWindow);
        desktop.Update();
        desktop.Update();

        MGContextMenu contextMenu = new(ownerWindow, "Menu");
        contextMenu.AddButton("Item 1", null);
        MGContextMenuSeparator separator = contextMenu.AddSeparator();
        contextMenu.AddButton("Item 2", null);
        Assert.True(contextMenu.TryOpenContextMenu(new Point(20, 20)));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 32, new Point(1, 1));
        Assert.True(contextMenu.IsContextMenuOpen);
        Assert.False(separator.ActualLayoutBounds.IsEmpty);

        SetFocusedKeyboardHandler(desktop, otherButton);
        List<MGWindow> windowOrderBeforeClick = new(desktop.Windows);

        // Click the separator (part of the open MGContextMenu itself, but not a focusable menu item) so this test
        // actually discriminates ActivatesOnClick: a focusable item's own click would focus itself regardless.
        Point menuBodyPoint = separator.ActualLayoutBounds.Center;
        AdvanceFrame(runtime, desktop, 48, menuBodyPoint);
        AdvanceFrame(runtime, desktop, 64, menuBodyPoint, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 80, menuBodyPoint);

        Assert.Equal(windowOrderBeforeClick, desktop.Windows);
        Assert.Same(otherButton, desktop.FocusedKeyboardHandler);
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

    private static void SetFocusedKeyboardHandler(MGDesktop desktop, MGElement element)
    {
        MethodInfo setter = typeof(MGDesktop).GetProperty(nameof(MGDesktop.FocusedKeyboardHandler), BindingFlags.Public | BindingFlags.Instance)!.GetSetMethod(true)!;
        setter.Invoke(desktop, new object[] { element });
    }
}
