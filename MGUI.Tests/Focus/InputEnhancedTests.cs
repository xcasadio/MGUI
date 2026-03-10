using MGUI.Core.UI;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Focus;

public class InputEnhancedTests
{
    private sealed class KeyboardHost : IKeyboardHandlerHost
    {
        public bool CanReceive { get; set; } = true;
        public bool HasFocus { get; set; } = true;

        public bool CanReceiveKeyboardInput() => CanReceive;
        public bool HasKeyboardFocus() => HasFocus;
    }

    private static UpdateBaseArgs CreateUpdateArgs(int elapsedMs, MouseState mouseState, KeyboardState keyboardState)
        => new(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(16), mouseState, keyboardState);

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

    [Theory]
    [InlineData(MouseButton.Left)]
    [InlineData(MouseButton.Right)]
    public void MouseTracker_DoubleClick_CountsLeftAndRightButtons(MouseButton button)
    {
        InputTracker tracker = new();

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(12, 12)), new KeyboardState()));
        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(12, 12), button), new KeyboardState()));
        tracker.Update(CreateUpdateArgs(25, CreateMouseState(new Point(12, 12)), new KeyboardState()));

        BaseMouseClickedEventArgs firstClick = tracker.Mouse.CurrentButtonClickedEvents[button];
        Assert.NotNull(firstClick);
        Assert.Equal(1, firstClick.ClickCount);
        Assert.False(firstClick.IsDoubleClick);
        Assert.False(firstClick.IsMultiClick);
        Assert.Null(tracker.Mouse.CurrentButtonDoubleClickedEvents[button]);

        tracker.Update(CreateUpdateArgs(50, CreateMouseState(new Point(12, 12), button), new KeyboardState()));
        tracker.Update(CreateUpdateArgs(80, CreateMouseState(new Point(12, 12)), new KeyboardState()));

        BaseMouseClickedEventArgs secondClick = tracker.Mouse.CurrentButtonClickedEvents[button];
        Assert.NotNull(secondClick);
        Assert.Equal(2, secondClick.ClickCount);
        Assert.True(secondClick.IsDoubleClick);
        Assert.True(secondClick.IsMultiClick);
        Assert.Same(secondClick, tracker.Mouse.CurrentButtonDoubleClickedEvents[button]);
    }

    [Fact]
    public void MouseTracker_DoubleClick_RespectsPositionThreshold()
    {
        InputTracker tracker = new();
        tracker.Mouse.MultiClickPositionThreshold = 3;

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(10, 10)), new KeyboardState()));
        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(10, 10), MouseButton.Left), new KeyboardState()));
        tracker.Update(CreateUpdateArgs(25, CreateMouseState(new Point(10, 10)), new KeyboardState()));
        tracker.Update(CreateUpdateArgs(50, CreateMouseState(new Point(30, 30), MouseButton.Left), new KeyboardState()));
        tracker.Update(CreateUpdateArgs(80, CreateMouseState(new Point(30, 30)), new KeyboardState()));

        BaseMouseClickedEventArgs secondClick = tracker.Mouse.CurrentButtonClickedEvents[MouseButton.Left];
        Assert.NotNull(secondClick);
        Assert.Equal(1, secondClick.ClickCount);
        Assert.Null(tracker.Mouse.CurrentButtonDoubleClickedEvents[MouseButton.Left]);
    }

    [Fact]
    public void MouseHandler_DoubleClickedInside_RaisesSpecializedEvent()
    {
        InputTracker tracker = new();
        MouseHandlerHost host = new(() => new Rectangle(0, 0, 100, 100));
        MouseHandler handler = tracker.Mouse.CreateHandler(host, 10);

        int callCount = 0;
        int lastClickCount = 0;
        handler.LMBDoubleClickedInside += (_, e) =>
        {
            callCount++;
            lastClickCount = e.ClickCount;
        };

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(10, 10)), new KeyboardState()));
        tracker.Mouse.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(10, 10), MouseButton.Left), new KeyboardState()));
        tracker.Mouse.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(25, CreateMouseState(new Point(10, 10)), new KeyboardState()));
        tracker.Mouse.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(50, CreateMouseState(new Point(10, 10), MouseButton.Left), new KeyboardState()));
        tracker.Mouse.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(80, CreateMouseState(new Point(10, 10)), new KeyboardState()));
        tracker.Mouse.UpdateHandlers();

        Assert.Equal(1, callCount);
        Assert.Equal(2, lastClickCount);
    }

    [Fact]
    public void MouseTracker_DoubleClick_IsOnlyEmittedOnSecondClickTransition()
    {
        InputTracker tracker = new();

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(12, 12)), new KeyboardState()));
        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(12, 12), MouseButton.Left), new KeyboardState()));
        tracker.Update(CreateUpdateArgs(25, CreateMouseState(new Point(12, 12)), new KeyboardState()));
        Assert.Null(tracker.Mouse.CurrentButtonDoubleClickedEvents[MouseButton.Left]);

        tracker.Update(CreateUpdateArgs(50, CreateMouseState(new Point(12, 12), MouseButton.Left), new KeyboardState()));
        tracker.Update(CreateUpdateArgs(80, CreateMouseState(new Point(12, 12)), new KeyboardState()));
        BaseMouseClickedEventArgs secondClick = tracker.Mouse.CurrentButtonClickedEvents[MouseButton.Left];
        Assert.NotNull(secondClick);
        Assert.Equal(2, secondClick.ClickCount);
        Assert.Same(secondClick, tracker.Mouse.CurrentButtonDoubleClickedEvents[MouseButton.Left]);

        tracker.Update(CreateUpdateArgs(110, CreateMouseState(new Point(12, 12), MouseButton.Left), new KeyboardState()));
        tracker.Update(CreateUpdateArgs(135, CreateMouseState(new Point(12, 12)), new KeyboardState()));
        BaseMouseClickedEventArgs thirdClick = tracker.Mouse.CurrentButtonClickedEvents[MouseButton.Left];
        Assert.NotNull(thirdClick);
        Assert.Equal(3, thirdClick.ClickCount);
        Assert.False(thirdClick.IsDoubleClick);
        Assert.True(thirdClick.IsMultiClick);
        Assert.Null(tracker.Mouse.CurrentButtonDoubleClickedEvents[MouseButton.Left]);
    }

    [Fact]
    public void MouseHandler_DoubleClick_RequiresStableLogicalTarget()
    {
        InputTracker tracker = new();
        tracker.Mouse.MultiClickPositionThreshold = 4;

        MouseHandlerHost leftHost = new(() => new Rectangle(0, 0, 50, 100));
        MouseHandlerHost rightHost = new(() => new Rectangle(50, 0, 50, 100));
        MouseHandler leftHandler = tracker.Mouse.CreateHandler(leftHost, 20);
        MouseHandler rightHandler = tracker.Mouse.CreateHandler(rightHost, 10);

        int leftDoubleClicks = 0;
        int rightDoubleClicks = 0;
        int rightClicks = 0;

        leftHandler.LMBDoubleClickedInside += (_, _) => leftDoubleClicks++;
        rightHandler.LMBDoubleClickedInside += (_, _) => rightDoubleClicks++;
        rightHandler.LMBClickedInside += (_, _) => rightClicks++;

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(49, 10)), new KeyboardState()));
        tracker.Mouse.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(49, 10), MouseButton.Left), new KeyboardState()));
        tracker.Mouse.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(25, CreateMouseState(new Point(49, 10)), new KeyboardState()));
        tracker.Mouse.UpdateHandlers();

        tracker.Update(CreateUpdateArgs(50, CreateMouseState(new Point(51, 10), MouseButton.Left), new KeyboardState()));
        tracker.Mouse.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(80, CreateMouseState(new Point(51, 10)), new KeyboardState()));
        tracker.Mouse.UpdateHandlers();

        BaseMouseClickedEventArgs secondClick = tracker.Mouse.CurrentButtonClickedEvents[MouseButton.Left];
        Assert.NotNull(secondClick);
        Assert.Equal(2, secondClick.ClickCount);
        Assert.NotNull(secondClick.PreviousClickInSequence);
        Assert.Equal(secondClick.PreviousClickInSequence.MultiClickSequenceId, secondClick.MultiClickSequenceId);
        Assert.Equal(0, leftDoubleClicks);
        Assert.Equal(0, rightDoubleClicks);
        Assert.Equal(1, rightClicks);
    }

    [Fact]
    public void KeyboardHandler_KeyRepeat_UsesLocalPolicyTiming()
    {
        InputTracker tracker = new();
        KeyboardHost host = new();
        KeyboardHandler handler = tracker.Keyboard.CreateHandler(host, 10);
        handler.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(300);
        handler.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(100);

        int repeatCount = 0;
        handler.KeyRepeat += (_, _) => repeatCount++;

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        tracker.Keyboard.UpdateHandlers();
        Assert.NotNull(tracker.Keyboard.CurrentKeyPressedEvents[Keys.A]);
        Assert.Equal(0, repeatCount);

        tracker.Update(CreateUpdateArgs(200, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        tracker.Keyboard.UpdateHandlers();
        Assert.Equal(0, repeatCount);

        tracker.Update(CreateUpdateArgs(320, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        tracker.Keyboard.UpdateHandlers();
        Assert.Equal(1, repeatCount);

        tracker.Update(CreateUpdateArgs(370, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        tracker.Keyboard.UpdateHandlers();
        Assert.Equal(1, repeatCount);

        tracker.Update(CreateUpdateArgs(430, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        tracker.Keyboard.UpdateHandlers();
        Assert.Equal(2, repeatCount);

        tracker.Update(CreateUpdateArgs(470, CreateMouseState(Point.Zero), new KeyboardState()));
        Assert.NotNull(tracker.Keyboard.CurrentKeyReleasedEvents[Keys.A]);
        tracker.Keyboard.UpdateHandlers();
        Assert.Equal(2, repeatCount);
    }

    [Fact]
    public void KeyboardHandler_LocalRepeatPolicies_DoNotInterfere()
    {
        InputTracker tracker = new();
        KeyboardHost fastHost = new();
        KeyboardHost slowHost = new();
        KeyboardHandler fastHandler = tracker.Keyboard.CreateHandler(fastHost, 20, false, true);
        KeyboardHandler slowHandler = tracker.Keyboard.CreateHandler(slowHost, 10, false, true);

        fastHandler.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(300);
        fastHandler.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(100);
        slowHandler.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(500);
        slowHandler.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(200);

        int fastRepeats = 0;
        int slowRepeats = 0;
        fastHandler.KeyRepeat += (_, _) => fastRepeats++;
        slowHandler.KeyRepeat += (_, _) => slowRepeats++;

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        tracker.Keyboard.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(320, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        tracker.Keyboard.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(520, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        tracker.Keyboard.UpdateHandlers();

        Assert.Equal(2, fastRepeats);
        Assert.Equal(1, slowRepeats);
    }

    [Fact]
    public void KeyboardHandler_KeyDownKeyRepeatAndKeyUp_AreInvoked()
    {
        InputTracker tracker = new();

        KeyboardHost host = new();
        KeyboardHandler handler = tracker.Keyboard.CreateHandler(host, 10);
        handler.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(300);
        handler.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(100);

        int keyDownCount = 0;
        int keyRepeatCount = 0;
        int keyUpCount = 0;

        handler.KeyDown += (_, _) => keyDownCount++;
        handler.KeyRepeat += (_, e) =>
        {
            keyRepeatCount++;
            Assert.True(e.IsRepeat);
        };
        handler.KeyUp += (_, _) => keyUpCount++;

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        tracker.Keyboard.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(320, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        tracker.Keyboard.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(470, CreateMouseState(Point.Zero), new KeyboardState()));
        tracker.Keyboard.UpdateHandlers();

        Assert.Equal(1, keyDownCount);
        Assert.Equal(1, keyRepeatCount);
        Assert.Equal(1, keyUpCount);
    }

    [Fact]
    public void KeyboardHandler_HandledKeyStream_RemainsOwnedAfterFocusChanges()
    {
        InputTracker tracker = new();
        KeyboardHost originalHost = new() { HasFocus = true };
        KeyboardHost newFocusedHost = new() { HasFocus = false };
        KeyboardHandler originalHandler = tracker.Keyboard.CreateHandler(originalHost, 20);
        KeyboardHandler newFocusedHandler = tracker.Keyboard.CreateHandler(newFocusedHost, 10, false, true);

        originalHandler.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(300);
        originalHandler.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(100);
        newFocusedHandler.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(300);
        newFocusedHandler.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(100);

        int originalKeyDown = 0;
        int originalRepeats = 0;
        int originalKeyUp = 0;
        int newFocusedRepeats = 0;
        int newFocusedKeyUp = 0;

        originalHandler.KeyDown += (_, e) =>
        {
            originalKeyDown++;
            e.SetHandledBy(originalHost, false);
        };
        originalHandler.KeyRepeat += (_, _) => originalRepeats++;
        originalHandler.KeyUp += (_, _) => originalKeyUp++;
        newFocusedHandler.KeyRepeat += (_, _) => newFocusedRepeats++;
        newFocusedHandler.KeyUp += (_, _) => newFocusedKeyUp++;

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        tracker.Keyboard.UpdateHandlers();

        originalHost.HasFocus = false;
        newFocusedHost.HasFocus = true;

        tracker.Update(CreateUpdateArgs(320, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        tracker.Keyboard.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(470, CreateMouseState(Point.Zero), new KeyboardState()));
        tracker.Keyboard.UpdateHandlers();

        Assert.Equal(1, originalKeyDown);
        Assert.Equal(1, originalRepeats);
        Assert.Equal(1, originalKeyUp);
        Assert.Equal(0, newFocusedRepeats);
        Assert.Equal(0, newFocusedKeyUp);
    }

    [Theory]
    [InlineData(true, 1, true)]
    [InlineData(true, 2, false)]
    [InlineData(false, 1, false)]
    public void TreeViewItem_ShouldToggleExpansionOnHeaderClick_ReturnsExpectedValue(bool hasItems, int clickCount, bool expected)
    {
        bool actual = MGTreeViewItem.ShouldToggleExpansionOnHeaderClick(hasItems, clickCount);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(true, 1, false)]
    [InlineData(true, 2, true)]
    [InlineData(true, 3, false)]
    [InlineData(false, 2, false)]
    public void TreeViewItem_ShouldRaiseItemDoubleClicked_ReturnsExpectedValue(bool hasItems, int clickCount, bool expected)
    {
        bool actual = MGTreeViewItem.ShouldRaiseItemDoubleClicked(hasItems, clickCount);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(true, false, true, Keys.A, true)]
    [InlineData(true, true, true, Keys.A, false)]
    [InlineData(true, false, false, Keys.Back, true)]
    [InlineData(true, false, false, Keys.Enter, false)]
    [InlineData(false, false, true, Keys.A, false)]
    public void TextBox_ShouldProcessRepeatedKey_ReturnsExpectedValue(bool isHeldKeyRepeated, bool isControlDown, bool isPrintableKey, Keys key, bool expected)
    {
        bool actual = MGTextBox.ShouldProcessRepeatedKey(isHeldKeyRepeated, isControlDown, isPrintableKey, key);

        Assert.Equal(expected, actual);
    }
}