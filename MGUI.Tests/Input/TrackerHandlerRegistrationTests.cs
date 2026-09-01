using MGUI.Shared.Input;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Input;

/// <summary>Pins the D1 contract: <see cref="MouseTracker.CreateHandler{T}(T, double?, bool, bool, bool)"/> and
/// <see cref="KeyboardTracker.CreateHandler{T}(T, double?, bool, bool)"/> must not add a manual handler (null <c>UpdatePriority</c>)
/// to their tracker's <c>Handlers</c> list, while auto handlers (non-null <c>UpdatePriority</c>) keep being registered/unregistered as before.</summary>
public class TrackerHandlerRegistrationTests
{
    private sealed class KeyboardHost : IKeyboardHandlerHost
    {
        public bool CanReceiveKeyboardInput() => true;
        public bool HasKeyboardFocus() => true;
    }

    private static UpdateBaseArgs CreateUpdateArgs(int elapsedMs, MouseState mouseState, KeyboardState keyboardState)
        => new(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(16), mouseState, keyboardState);

    private static MouseState CreateMouseState(Point position, ButtonState left = ButtonState.Released)
        => new(position.X, position.Y, 0, left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

    [Fact]
    public void MouseTracker_CreateHandler_Manual_DoesNotRegisterInHandlers()
    {
        InputTracker tracker = new();
        MouseHandlerHost host = new(() => new Rectangle(0, 0, 100, 100));

        MouseHandler handler = tracker.Mouse.CreateHandler(host, (double?)null);

        Assert.True(handler.IsManualUpdate);
        Assert.Empty(tracker.Mouse.Handlers);
    }

    [Fact]
    public void MouseTracker_CreateHandler_Manual_StillDeliversEventsViaManualUpdate()
    {
        InputTracker tracker = new();
        MouseHandlerHost host = new(() => new Rectangle(0, 0, 100, 100));
        MouseHandler handler = tracker.Mouse.CreateHandler(host, (double?)null);

        int pressCount = 0;
        int releaseCount = 0;
        handler.PressedInside += (_, _) => pressCount++;
        handler.ReleasedInside += (_, _) => releaseCount++;

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(10, 10)), new KeyboardState()));
        handler.ManualUpdate();

        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(10, 10), ButtonState.Pressed), new KeyboardState()));
        handler.ManualUpdate();

        tracker.Update(CreateUpdateArgs(20, CreateMouseState(new Point(10, 10)), new KeyboardState()));
        handler.ManualUpdate();

        Assert.Equal(1, pressCount);
        Assert.Equal(1, releaseCount);
        Assert.Empty(tracker.Mouse.Handlers);
    }

    [Fact]
    public void MouseTracker_CreateHandler_Auto_RegistersAndUnsubscribeClearsIt()
    {
        InputTracker tracker = new();
        MouseHandlerHost host = new(() => new Rectangle(0, 0, 100, 100));

        MouseHandler handler = tracker.Mouse.CreateHandler(host, 10);

        Assert.False(handler.IsManualUpdate);
        Assert.Contains(handler, tracker.Mouse.Handlers);

        int pressCount = 0;
        handler.PressedInside += (_, _) => pressCount++;

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(10, 10)), new KeyboardState()));
        tracker.Mouse.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(10, 10), ButtonState.Pressed), new KeyboardState()));
        tracker.Mouse.UpdateHandlers();

        Assert.Equal(1, pressCount);

        handler.Unsubscribe();
        Assert.Empty(tracker.Mouse.Handlers);
    }

    [Fact]
    public void MouseTracker_Unsubscribe_OnNeverRegisteredManualHandler_DoesNotThrowAndDisablesHandler()
    {
        InputTracker tracker = new();
        MouseHandlerHost host = new(() => new Rectangle(0, 0, 100, 100));
        MouseHandler handler = tracker.Mouse.CreateHandler(host, (double?)null);

        int pressCount = 0;
        handler.PressedInside += (_, _) => pressCount++;

        Exception exception = Record.Exception(() => handler.Unsubscribe());
        Assert.Null(exception);
        Assert.Empty(tracker.Mouse.Handlers);

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(10, 10)), new KeyboardState()));
        handler.ManualUpdate();
        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(10, 10), ButtonState.Pressed), new KeyboardState()));
        handler.ManualUpdate();

        Assert.Equal(0, pressCount);
    }

    [Fact]
    public void KeyboardTracker_CreateHandler_Manual_DoesNotRegisterInHandlers()
    {
        InputTracker tracker = new();
        KeyboardHost host = new();

        KeyboardHandler handler = tracker.Keyboard.CreateHandler(host, (double?)null);

        Assert.True(handler.IsManualUpdate);
        Assert.Empty(tracker.Keyboard.Handlers);
    }

    [Fact]
    public void KeyboardTracker_CreateHandler_Manual_StillDeliversEventsViaManualUpdate()
    {
        InputTracker tracker = new();
        KeyboardHost host = new();
        KeyboardHandler handler = tracker.Keyboard.CreateHandler(host, (double?)null);

        int pressCount = 0;
        int releaseCount = 0;
        handler.Pressed += (_, _) => pressCount++;
        handler.Released += (_, _) => releaseCount++;

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(0, 0)), new KeyboardState()));
        handler.ManualUpdate();

        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(0, 0)), new KeyboardState(Keys.A)));
        handler.ManualUpdate();

        tracker.Update(CreateUpdateArgs(20, CreateMouseState(new Point(0, 0)), new KeyboardState()));
        handler.ManualUpdate();

        Assert.Equal(1, pressCount);
        Assert.Equal(1, releaseCount);
        Assert.Empty(tracker.Keyboard.Handlers);
    }

    [Fact]
    public void KeyboardTracker_CreateHandler_Auto_RegistersAndUnsubscribeClearsIt()
    {
        InputTracker tracker = new();
        KeyboardHost host = new();

        KeyboardHandler handler = tracker.Keyboard.CreateHandler(host, 10);

        Assert.False(handler.IsManualUpdate);
        Assert.Contains(handler, tracker.Keyboard.Handlers);

        int pressCount = 0;
        handler.Pressed += (_, _) => pressCount++;

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(0, 0)), new KeyboardState()));
        tracker.Keyboard.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(0, 0)), new KeyboardState(Keys.A)));
        tracker.Keyboard.UpdateHandlers();

        Assert.Equal(1, pressCount);

        handler.Unsubscribe();
        Assert.Empty(tracker.Keyboard.Handlers);
    }

    [Fact]
    public void KeyboardTracker_Unsubscribe_OnNeverRegisteredManualHandler_DoesNotThrowAndDisablesHandler()
    {
        InputTracker tracker = new();
        KeyboardHost host = new();
        KeyboardHandler handler = tracker.Keyboard.CreateHandler(host, (double?)null);

        int pressCount = 0;
        handler.Pressed += (_, _) => pressCount++;

        Exception exception = Record.Exception(() => handler.Unsubscribe());
        Assert.Null(exception);
        Assert.Empty(tracker.Keyboard.Handlers);

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(0, 0)), new KeyboardState()));
        handler.ManualUpdate();
        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(0, 0)), new KeyboardState(Keys.A)));
        handler.ManualUpdate();

        Assert.Equal(0, pressCount);
    }
}
