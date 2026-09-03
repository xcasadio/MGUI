using System;
using System.Collections.Generic;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Input.Semantic;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Input;

/// <summary>
/// Locks down the central routing guarantee for overlapping desktop windows (documented in Docs/input-architecture.md "Fenetres superposees"):
/// press/release/scroll/drag-start in the overlap region are consumed by the topmost window and never delivered
/// to the window below, hover follows the cross-window occlusion decision from tache 4, keyboard is routed by
/// focus rather than z-order, <see cref="MGWindow.AllowsClickThrough"/> lets unconsumed events fall through, and
/// the current (not-fixed-here) behavior of <see cref="MGComboBox{TItemType}"/> dropdown light-dismiss when a
/// higher window's own closure consumes the outside release is documented as a known limitation.<para/>
/// Deliberately reviewed for task 2 of the input-activation slice (<see cref="MGWindow.ActivatesOnClick"/>, default
/// <see langword="true"/>): none of the tests below needed adjustment, since none assert on <see cref="MGDesktop.Windows"/>
/// ordering (the thing click-activation changes) - they assert press/release/scroll/drag consumption counts,
/// <see cref="MGElement.HoveredElement"/>/<see cref="MGWindow.PressedElement"/>, keyboard routing by focus, and
/// <see cref="MGComboBox{TItemType}.IsDropdownOpen"/>, none of which click-activation touches. The dedicated
/// bidirectional z-order coverage for the new default lives in <c>WindowActivationOnClickTests</c>
/// (<see cref="MGUI.Tests.Input.WindowActivationOnClickTests"/>) and the docking migration regression in
/// <see cref="MGUI.Tests.Input.FloatingDockWindowActivationTests"/>.
/// </summary>
public class OverlappingWindowsInputRoutingTests
{
    [Fact]
    public void PressReleaseScroll_AtOverlapPoint_ConsumedByFrontWindow_NeverReachesBackWindow()
    {
        Harness harness = CreateHarness();

        int frontPressed = 0, frontReleased = 0, frontScrolled = 0;
        int backPressed = 0, backReleased = 0, backScrolled = 0;
        harness.FrontWindow.MouseHandler.PressedInside += (_, __) => frontPressed++;
        harness.FrontWindow.MouseHandler.ReleasedInside += (_, __) => frontReleased++;
        harness.FrontWindow.MouseHandler.Scrolled += (_, __) => frontScrolled++;
        harness.BackWindow.MouseHandler.PressedInside += (_, __) => backPressed++;
        harness.BackWindow.MouseHandler.ReleasedInside += (_, __) => backReleased++;
        harness.BackWindow.MouseHandler.Scrolled += (_, __) => backScrolled++;

        Point overlapPoint = OverlapPoint(harness);
        AdvanceFrame(harness, 32, overlapPoint);
        AdvanceFrame(harness, 48, overlapPoint, MouseButton.Left);
        AdvanceFrame(harness, 64, overlapPoint);
        AdvanceFrame(harness, 80, overlapPoint, scrollWheelValue: 120);

        Assert.True(frontPressed > 0);
        Assert.True(frontReleased > 0);
        Assert.True(frontScrolled > 0);
        Assert.Equal(0, backPressed);
        Assert.Equal(0, backReleased);
        Assert.Equal(0, backScrolled);
    }

    [Fact]
    public void DragStart_AtOverlapPoint_ConsumedByFrontWindow_NeverReachesBackWindow()
    {
        Harness harness = CreateHarness();

        int frontDragStart = 0, backDragStart = 0;
        harness.FrontWindow.MouseHandler.DragStart += (_, __) => frontDragStart++;
        harness.BackWindow.MouseHandler.DragStart += (_, __) => backDragStart++;

        Point overlapPoint = OverlapPoint(harness);
        AdvanceFrame(harness, 32, overlapPoint);
        // DragStartCondition.MousePressed fires on the exact press tick (see MouseTrackerDragFastPathTests, tache 1).
        AdvanceFrame(harness, 48, overlapPoint, MouseButton.Left);

        Assert.True(frontDragStart > 0);
        Assert.Equal(0, backDragStart);
    }

    [Fact]
    public void Hover_AtOverlapPoint_OnlyFrontWindowLightsUp_PinsTache4Decision()
    {
        // Pins the cross-window hover occlusion decision from tache 4 (option (b)) as an overlapping-windows
        // scenario in its own right, distinct from the press/release/scroll/drag consumption guarantee above.
        Harness harness = CreateHarness();

        Point overlapPoint = OverlapPoint(harness);
        AdvanceFrame(harness, 32, overlapPoint);
        AdvanceFrame(harness, 48, overlapPoint);

        Assert.Same(harness.FrontButton, harness.FrontWindow.HoveredElement);
        Assert.Null(harness.BackWindow.HoveredElement);
    }

    [Fact]
    public void Keyboard_FocusedElementInBackWindow_ReceivesAction_EvenWhileVisuallyOccluded()
    {
        // Keyboard is routed by focus (MGDesktop.FocusedKeyboardHandler), not by window z-order/occlusion:
        // the semantic input path (MGDesktop.TryHandleInputAction) dispatches straight to the focused element
        // regardless of which window is drawn on top at the current mouse position.
        Harness harness = CreateHarness();

        int frontClicks = 0, backClicks = 0;
        harness.FrontButton.OnLeftClicked += (_, __) => frontClicks++;
        harness.BackButton.OnLeftClicked += (_, __) => backClicks++;

        SetFocusedKeyboardHandler(harness.Desktop, harness.BackButton);

        // Move the mouse to the overlap point so backWindow is actually occluded at this tick (sanity check).
        Point overlapPoint = OverlapPoint(harness);
        AdvanceFrame(harness, 32, overlapPoint);
        AdvanceFrame(harness, 48, overlapPoint);
        Assert.Null(harness.BackWindow.HoveredElement);

        InputActionEvent actionEvent = new(InputAction.Submit,
            new InputActionContext(InputActionSource.Keyboard, InputActionPhase.Pressed, TimeSpan.Zero, Key: Keys.Enter));
        bool handled = harness.Desktop.TryHandleInputAction(actionEvent);

        Assert.True(handled);
        Assert.Equal(1, backClicks);
        Assert.Equal(0, frontClicks);
        Assert.Same(harness.BackButton, harness.Desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void AllowsClickThrough_True_LetsUnconsumedPressFallThroughToWindowBelow()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow backWindow = new(desktop, 0, 0, 300, 200) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        MGButton backButton = new(backWindow);
        backWindow.SetContent(backButton);

        // frontWindow overlaps backWindow but its own button only covers a small corner, so a click over the
        // rest of frontWindow's bounds hits no child of frontWindow - only frontWindow's own (click-through) body.
        MGWindow frontWindow = new(desktop, 150, 0, 300, 200)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
            AllowsClickThrough = true
        };
        MGButton frontCornerButton = new(frontWindow)
        {
            HorizontalAlignment = Core.UI.HorizontalAlignment.Left,
            VerticalAlignment = Core.UI.VerticalAlignment.Top,
            PreferredWidth = 50,
            PreferredHeight = 50
        };
        frontWindow.SetContent(frontCornerButton);

        desktop.Windows.Add(backWindow);
        desktop.Windows.Add(frontWindow);
        AdvanceFrame(desktop, runtime, 0, new Point(1, 1));
        AdvanceFrame(desktop, runtime, 16, new Point(1, 1));

        // Inside both windows' bounds, inside the overlap, but outside frontCornerButton (which only covers
        // the (150,0)-(200,50) corner) and outside backButton's own layout position check below.
        Point clickPoint = new(250, 100);
        Assert.True(new Rectangle(150, 0, 300, 200).Contains(clickPoint));
        Assert.True(new Rectangle(0, 0, 300, 200).Contains(clickPoint));
        Assert.False(new Rectangle(150, 0, 50, 50).Contains(clickPoint));

        AdvanceFrame(desktop, runtime, 32, clickPoint);
        AdvanceFrame(desktop, runtime, 48, clickPoint, MouseButton.Left);

        Assert.Same(backButton, backWindow.PressedElement);
    }

    [Fact]
    public void ComboBoxDropdown_ReleasedOutsideClick_ClosesDropdown_WhenNoHigherWindowBlocksIt()
    {
        // Control case: with nothing drawn above it, the dropdown's own light-dismiss (Dropdown.WindowMouseHandler.ReleasedOutside)
        // works normally, so the blocked case below can be attributed specifically to the higher window's closure.
        ComboBoxHarness harness = CreateComboBoxHarness(withCoveringWindow: false);

        harness.ComboBox.IsDropdownOpen = true;
        harness.Desktop.Update();

        Point farOutsidePoint = new(700, 550);
        AdvanceFrame(harness.Desktop, harness.Runtime, 32, farOutsidePoint);
        AdvanceFrame(harness.Desktop, harness.Runtime, 48, farOutsidePoint, MouseButton.Left);
        AdvanceFrame(harness.Desktop, harness.Runtime, 64, farOutsidePoint);

        Assert.False(harness.ComboBox.IsDropdownOpen);
    }

    [Fact]
    public void ComboBoxDropdown_ReleasedOutsideClick_IsBlockedByHigherWindowClosure_DropdownStaysOpen_KnownLimitation()
    {
        // Documents the current (not fixed by tache 5, which is coverage-only) behavior described in the task text:
        // Dropdown.WindowMouseHandler.ReleasedOutside is a manually-updated handler (InvokeEvenIfHandled == false)
        // that runs AFTER the desktop's normal front-to-back window pass. A non-frontal window's popup can be
        // "closed" (light-dismissed) by an outside click only if nothing above it already marked that click's
        // shared ReleasedArgs as handled - here, a separate, fully covering, non-click-through window sitting on
        // top of the combobox's own window consumes the release first (its own ReleasedInside handler, since
        // AllowsClickThrough defaults to false), so the dropdown's ReleasedOutside never fires and it stays open.
        ComboBoxHarness harness = CreateComboBoxHarness(withCoveringWindow: true);

        harness.ComboBox.IsDropdownOpen = true;
        harness.Desktop.Update();

        Point farOutsidePoint = new(700, 550);
        AdvanceFrame(harness.Desktop, harness.Runtime, 32, farOutsidePoint);
        AdvanceFrame(harness.Desktop, harness.Runtime, 48, farOutsidePoint, MouseButton.Left);
        AdvanceFrame(harness.Desktop, harness.Runtime, 64, farOutsidePoint);

        Assert.True(harness.ComboBox.IsDropdownOpen);
    }

    private static Point OverlapPoint(Harness harness)
    {
        Rectangle back = harness.BackButton.ActualLayoutBounds;
        Rectangle front = harness.FrontButton.ActualLayoutBounds;
        Rectangle overlap = Rectangle.Intersect(back, front);
        Assert.True(overlap.Width > 0 && overlap.Height > 0);
        return overlap.Center;
    }

    private static Harness CreateHarness()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow backWindow = new(desktop, 0, 0, 300, 200) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        MGButton backButton = new(backWindow);
        backWindow.SetContent(backButton);

        MGWindow frontWindow = new(desktop, 150, 0, 300, 200) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        MGButton frontButton = new(frontWindow);
        frontWindow.SetContent(frontButton);

        // frontWindow is added last, so it is on top of the visual stack (Windows.Reverse().OrderByDescending(IsTopmost)),
        // matching the pattern already established by NavigationFrontToBackFallbackTests / CrossWindowHoverPressedOcclusionTests.
        desktop.Windows.Add(backWindow);
        desktop.Windows.Add(frontWindow);

        Harness harness = new(runtime, desktop, backWindow, backButton, frontWindow, frontButton);

        AdvanceFrame(harness, 0, new Point(1, 1));
        AdvanceFrame(harness, 16, new Point(1, 1));

        return harness;
    }

    private static ComboBoxHarness CreateComboBoxHarness(bool withCoveringWindow)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow comboWindow = new(desktop, 0, 0, 300, 200) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        MGComboBox<string> comboBox = new(comboWindow)
        {
            HorizontalAlignment = Core.UI.HorizontalAlignment.Left,
            VerticalAlignment = Core.UI.VerticalAlignment.Top,
            PreferredWidth = 100,
            PreferredHeight = 30
        };
        comboBox.SetItemsSource(new List<string> { "Alpha", "Bravo", "Charlie" });
        comboWindow.SetContent(comboBox);
        desktop.Windows.Add(comboWindow);

        if (withCoveringWindow)
        {
            // Added after comboWindow, so it sits on top of the visual stack and fully covers the desktop -
            // any click anywhere lands "inside" this window first.
            MGWindow coveringWindow = new(desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
            desktop.Windows.Add(coveringWindow);
        }

        AdvanceFrame(desktop, runtime, 0, new Point(1, 1));
        AdvanceFrame(desktop, runtime, 16, new Point(1, 1));

        return new(runtime, desktop, comboWindow, comboBox);
    }

    private static void AdvanceFrame(Harness harness, int totalElapsedMs, Point position, MouseButton? pressedButton = null, int scrollWheelValue = 0)
        => AdvanceFrame(harness.Desktop, harness.Runtime, totalElapsedMs, position, pressedButton, scrollWheelValue);

    private static void AdvanceFrame(MGDesktop desktop, GraphTestRuntime runtime, int totalElapsedMs, Point position, MouseButton? pressedButton = null, int scrollWheelValue = 0)
    {
        runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(totalElapsedMs),
            TimeSpan.FromMilliseconds(16),
            CreateMouseState(position, pressedButton, scrollWheelValue),
            new KeyboardState()));
        desktop.Update();
    }

    private static MouseState CreateMouseState(Point position, MouseButton? pressedButton = null, int scrollWheelValue = 0)
        => new(
            position.X,
            position.Y,
            scrollWheelValue,
            pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Middle ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);

    private static void SetFocusedKeyboardHandler(MGDesktop desktop, MGElement element)
    {
        MethodInfo setter = typeof(MGDesktop).GetProperty(nameof(MGDesktop.FocusedKeyboardHandler), BindingFlags.Public | BindingFlags.Instance)!.GetSetMethod(true)!;
        setter.Invoke(desktop, new object[] { element });
    }

    private sealed record Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow BackWindow, MGButton BackButton, MGWindow FrontWindow, MGButton FrontButton);

    private sealed record ComboBoxHarness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow ComboWindow, MGComboBox<string> ComboBox);
}
