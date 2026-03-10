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
        Assert.NotNull(secondClick.Sequence);
        Assert.Same(secondClick.Sequence, secondClick.PreviousClickInSequence.Sequence);
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
        Assert.NotNull(secondClick.Sequence);
        Assert.Same(secondClick.PreviousClickInSequence.Sequence, secondClick.Sequence);
        Assert.Equal(0, leftDoubleClicks);
        Assert.Equal(0, rightDoubleClicks);
        Assert.Equal(1, rightClicks);
    }

    [Fact]
    public void MouseTracker_DistinctMultiClickSequences_GetDistinctSequenceIds()
    {
        InputTracker tracker = new();
        tracker.Mouse.MultiClickPositionThreshold = 2;

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(12, 12)), new KeyboardState()));
        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(12, 12), MouseButton.Left), new KeyboardState()));
        tracker.Update(CreateUpdateArgs(25, CreateMouseState(new Point(12, 12)), new KeyboardState()));
        BaseMouseClickedEventArgs firstSequenceClick = tracker.Mouse.CurrentButtonClickedEvents[MouseButton.Left];

        tracker.Update(CreateUpdateArgs(50, CreateMouseState(new Point(30, 30), MouseButton.Left), new KeyboardState()));
        tracker.Update(CreateUpdateArgs(70, CreateMouseState(new Point(30, 30)), new KeyboardState()));
        BaseMouseClickedEventArgs secondSequenceClick = tracker.Mouse.CurrentButtonClickedEvents[MouseButton.Left];

        Assert.NotNull(firstSequenceClick);
        Assert.NotNull(secondSequenceClick);
        Assert.Equal(1, firstSequenceClick.ClickCount);
        Assert.Equal(1, secondSequenceClick.ClickCount);
        Assert.NotNull(firstSequenceClick.Sequence);
        Assert.NotNull(secondSequenceClick.Sequence);
        Assert.NotSame(firstSequenceClick.Sequence, secondSequenceClick.Sequence);
        Assert.NotEqual(firstSequenceClick.Sequence.Id, secondSequenceClick.Sequence.Id);
        Assert.Null(secondSequenceClick.PreviousClickInSequence);
    }

    [Fact]
    public void MouseTracker_UsesLogicalUpdateTimeForMouseEventTimestamps()
    {
        InputTracker tracker = new();

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(5, 5)), new KeyboardState()));
        tracker.Update(CreateUpdateArgs(120, CreateMouseState(new Point(5, 5), MouseButton.Left), new KeyboardState()));
        BaseMousePressedEventArgs pressed = tracker.Mouse.CurrentButtonPressedEvents[MouseButton.Left];
        tracker.Update(CreateUpdateArgs(255, CreateMouseState(new Point(5, 5)), new KeyboardState()));
        BaseMouseReleasedEventArgs released = tracker.Mouse.CurrentButtonReleasedEvents[MouseButton.Left];

        Assert.NotNull(pressed);
        Assert.NotNull(released);
        Assert.Equal(TimeSpan.FromMilliseconds(120), pressed.PressedAt);
        Assert.Equal(TimeSpan.FromMilliseconds(255), released.ReleasedAt);
        Assert.Equal(TimeSpan.FromMilliseconds(135), released.HeldDuration);
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
    public void KeyboardTracker_UsesLogicalUpdateTimeForKeyboardEventTimestamps()
    {
        InputTracker tracker = new();

        tracker.Update(CreateUpdateArgs(150, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        BaseKeyPressedEventArgs pressed = tracker.Keyboard.CurrentKeyPressedEvents[Keys.A];
        tracker.Update(CreateUpdateArgs(410, CreateMouseState(Point.Zero), new KeyboardState()));
        BaseKeyReleasedEventArgs released = tracker.Keyboard.CurrentKeyReleasedEvents[Keys.A];

        Assert.NotNull(pressed);
        Assert.NotNull(released);
        Assert.Equal(TimeSpan.FromMilliseconds(150), pressed.PressedAt);
        Assert.Equal(TimeSpan.FromMilliseconds(410), released.ReleasedAt);
        Assert.Equal(TimeSpan.FromMilliseconds(260), released.HeldDuration);
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
    public void KeyboardHandler_KeyEvents_ShareExplicitStreamIdentity()
    {
        InputTracker tracker = new();
        tracker.Keyboard.ClickTimeThreshold = TimeSpan.FromMilliseconds(500);

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        BaseKeyPressedEventArgs pressed = tracker.Keyboard.CurrentKeyPressedEvents[Keys.A];
        tracker.Update(CreateUpdateArgs(320, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        KeyboardHost host = new();
        KeyboardHandler handler = tracker.Keyboard.CreateHandler(host, 10);
        handler.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(300);
        handler.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(100);
        BaseKeyRepeatedEventArgs repeated = null;
        handler.KeyRepeat += (_, e) => repeated = e;
        tracker.Keyboard.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(350, CreateMouseState(Point.Zero), new KeyboardState()));
        BaseKeyReleasedEventArgs released = tracker.Keyboard.CurrentKeyReleasedEvents[Keys.A];
        BaseKeyClickedEventArgs clicked = tracker.Keyboard.CurrentKeyClickedEvents[Keys.A];

        Assert.NotNull(pressed);
        Assert.NotNull(pressed.Stream);
        Assert.NotNull(repeated);
        Assert.NotNull(released);
        Assert.NotNull(clicked);
        Assert.Same(pressed.Stream, repeated.Stream);
        Assert.Same(pressed.Stream, released.Stream);
        Assert.Same(pressed.Stream, clicked.Stream);
        Assert.Equal(pressed.Stream.Id, clicked.StreamId);
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

    [Fact]
    public void KeyboardHandler_HandledKeyStream_IsOwnedByHandlerNotOwnerInstance()
    {
        InputTracker tracker = new();
        KeyboardHost sharedHost = new() { HasFocus = true };
        KeyboardHandler owningHandler = tracker.Keyboard.CreateHandler(sharedHost, 20);
        KeyboardHandler secondaryHandler = tracker.Keyboard.CreateHandler(sharedHost, 10, false, true);

        owningHandler.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(300);
        owningHandler.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(100);
        secondaryHandler.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(300);
        secondaryHandler.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(100);

        int owningKeyDown = 0;
        int owningRepeats = 0;
        int owningKeyUp = 0;
        int secondaryRepeats = 0;
        int secondaryKeyUp = 0;

        owningHandler.KeyDown += (_, e) =>
        {
            owningKeyDown++;
            e.SetHandledBy(sharedHost, false);
        };
        owningHandler.KeyRepeat += (_, _) => owningRepeats++;
        owningHandler.KeyUp += (_, _) => owningKeyUp++;
        secondaryHandler.KeyRepeat += (_, _) => secondaryRepeats++;
        secondaryHandler.KeyUp += (_, _) => secondaryKeyUp++;

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        BaseKeyPressedEventArgs pressed = tracker.Keyboard.CurrentKeyPressedEvents[Keys.A];
        tracker.Keyboard.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(320, CreateMouseState(Point.Zero), new KeyboardState(Keys.A)));
        tracker.Keyboard.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(470, CreateMouseState(Point.Zero), new KeyboardState()));
        tracker.Keyboard.UpdateHandlers();

        Assert.NotNull(pressed);
        Assert.True(pressed.IsHandled);
        Assert.Same(sharedHost, pressed.HandledBy);
        Assert.NotNull(pressed.Stream);
        Assert.Equal(1, owningKeyDown);
        Assert.Equal(1, owningRepeats);
        Assert.Equal(1, owningKeyUp);
        Assert.Equal(0, secondaryRepeats);
        Assert.Equal(0, secondaryKeyUp);
    }

    [Fact]
    public void KeyboardHandler_UnhandledKeyStream_CanFollowFocusChanges()
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
        int newFocusedRepeats = 0;
        int newFocusedKeyUp = 0;

        originalHandler.KeyDown += (_, _) => originalKeyDown++;
        originalHandler.KeyRepeat += (_, _) => originalRepeats++;
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
        Assert.Equal(0, originalRepeats);
        Assert.Equal(1, newFocusedRepeats);
        Assert.Equal(1, newFocusedKeyUp);
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
    [InlineData(true, 2, true, false, true)]
    [InlineData(true, 2, false, false, false)]
    [InlineData(true, 2, true, true, false)]
    [InlineData(false, 2, true, false, false)]
    public void TreeViewItem_ShouldRaiseItemDoubleClicked_WithSequenceContext_ReturnsExpectedValue(bool hasItems, int clickCount,
        bool sequenceStartedOnHeaderBody, bool clickedExpander, bool expected)
    {
        bool actual = MGTreeViewItem.ShouldRaiseItemDoubleClicked(hasItems, clickCount, sequenceStartedOnHeaderBody, clickedExpander);

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

    [Theory]
    [InlineData(true, true, false, true, Keys.A, true)]
    [InlineData(false, true, false, true, Keys.A, false)]
    [InlineData(true, true, true, true, Keys.V, false)]
    [InlineData(true, false, false, false, Keys.Back, false)]
    public void TextBox_ShouldHandleRepeatedKey_ReturnsExpectedValue(bool hasKeyboardFocus, bool isHeldKeyRepeated, bool isControlDown, bool isPrintableKey, Keys key, bool expected)
    {
        bool actual = MGTextBox.ShouldHandleRepeatedKey(hasKeyboardFocus, isHeldKeyRepeated, isControlDown, isPrintableKey, key);

        Assert.Equal(expected, actual);
    }
}