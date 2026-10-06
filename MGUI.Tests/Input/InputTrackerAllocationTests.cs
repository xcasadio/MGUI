using MGUI.Core.UI;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace MGUI.Tests.Input;

/// <summary>
/// The input trackers and handlers walk buttons, drag conditions and keys without allocating each frame: <c>foreach</c> over static arrays
/// instead of read-only collections, dictionary struct enumerators instead of LINQ <c>Any</c>, and the pressed keys read into reused lists
/// through <see cref="KeyboardState.GetPressedKeys(Keys[])"/>. The events delivered are unchanged.
/// </summary>
public class InputTrackerAllocationTests
{
    private const int WarmupIterations = 2000;
    private const int MeasuredIterations = 1000;

    private sealed class KeyboardHost : IKeyboardHandlerHost
    {
        public bool CanReceiveKeyboardInput() => true;
        public bool HasKeyboardFocus() => true;
    }

    [Fact]
    public void AMouseAndKeyboardSession_DeliversTheSameEvents()
    {
        InputTracker tracker = new();
        List<string> log = new();
        MouseHandler mouse = tracker.Mouse.CreateHandler(new MouseHandlerHost(() => new Rectangle(0, 0, 100, 100)), 10);
        mouse.PressedInside += (_, e) => log.Add($"mouse pressed {e.Button} {e.Position}");
        mouse.ReleasedInside += (_, e) => log.Add($"mouse released {e.Button} {e.Position}");
        mouse.DragStart += (_, e) => log.Add($"drag start {e.Button} {e.Position}");
        mouse.Dragged += (_, e) => log.Add($"dragged {e.Button} {e.Position}");
        mouse.DragEnd += (_, e) => log.Add($"drag end {e.Button} {e.EndPosition}");
        KeyboardHandler keyboard = tracker.Keyboard.CreateHandler(new KeyboardHost(), 10);
        keyboard.Pressed += (_, e) => log.Add($"key pressed {e.Key}");
        keyboard.Released += (_, e) => log.Add($"key released {e.Key}");
        keyboard.Clicked += (_, e) => log.Add($"key clicked {e.Key}");

        int elapsed = 0;
        void Frame(Point position, ButtonState left, params Keys[] keys)
        {
            tracker.Update(CreateUpdateArgs(elapsed, position, left, new KeyboardState(keys)));
            tracker.Mouse.UpdateHandlers();
            tracker.Keyboard.UpdateHandlers();
            log.Add($"frame {elapsed}");
            elapsed += 16;
        }

        Frame(new Point(10, 10), ButtonState.Released);
        Frame(new Point(10, 10), ButtonState.Pressed);
        Frame(new Point(30, 10), ButtonState.Pressed);
        Frame(new Point(40, 10), ButtonState.Pressed);
        Frame(new Point(40, 10), ButtonState.Released);
        Frame(new Point(40, 10), ButtonState.Released, Keys.B, Keys.A);
        Frame(new Point(40, 10), ButtonState.Released, Keys.A);
        Frame(new Point(40, 10), ButtonState.Released);

        //  Recorded on the code before this change (same log, line for line): the default drag start condition starts the drag on the first
        //  move after the press, at the press position.
        Assert.Equal(new[]
        {
            "frame 0",
            "mouse pressed Left {X:10 Y:10}", "frame 16",
            "drag start Left {X:10 Y:10}", "frame 32",
            "dragged Left {X:40 Y:10}", "frame 48",
            "mouse released Left {X:40 Y:10}", "drag end Left {X:40 Y:10}", "frame 64",
            "key pressed A", "key pressed B", "frame 80",
            "key released B", "key clicked B", "frame 96",
            "key released A", "key clicked A", "frame 112",
        }, log);
    }

    [Fact]
    public void InputTrackerUpdate_WithTheMouseStillAndNoKey_AllocatesNothing()
    {
        InputTracker tracker = new();
        UpdateBaseArgs idle = CreateUpdateArgs(0, new Point(10, 10), ButtonState.Released, new KeyboardState());

        Assert.Equal(0, Measure(() => tracker.Update(idle)));
    }

    [Fact]
    public void InputTrackerUpdate_WithTwoKeysHeld_AllocatesNothing()
    {
        InputTracker tracker = new();
        UpdateBaseArgs held = CreateUpdateArgs(0, new Point(10, 10), ButtonState.Released, new KeyboardState(Keys.LeftShift, Keys.A));
        tracker.Update(held);

        Assert.Equal(0, Measure(() => tracker.Update(held)));
    }

    [Fact]
    public void KeyboardHandlers_WithASubscriberAndNoKey_AllocateNothing()
    {
        InputTracker tracker = new();
        int pressed = 0;
        tracker.Keyboard.CreateHandler(new KeyboardHost(), 10).Pressed += (_, _) => pressed++;
        KeyboardHandler manual = tracker.Keyboard.CreateHandler(new KeyboardHost(), (double?)null);
        manual.Pressed += (_, _) => pressed++;
        tracker.Update(CreateUpdateArgs(0, new Point(10, 10), ButtonState.Released, new KeyboardState()));

        Assert.Equal(0, Measure(() =>
        {
            tracker.Keyboard.UpdateHandlers();
            manual.ManualUpdate();
        }));
        Assert.Equal(0, pressed);
    }

    [Fact]
    public void KeyboardAndGamePadActivityChecks_AllocateNothing()
    {
        InputTracker tracker = new();
        tracker.Update(CreateUpdateArgs(0, new Point(10, 10), ButtonState.Released, new KeyboardState()));
        bool activity = false;

        Assert.Equal(0, Measure(() => activity |= MGDesktop.HasKeyboardActivity(tracker.Keyboard) || tracker.GamePad.HasActivity()));
        Assert.False(activity);
    }

    private static long Measure(Action Action)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            Action();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            Action();
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static UpdateBaseArgs CreateUpdateArgs(int ElapsedMilliseconds, Point Position, ButtonState Left, KeyboardState Keyboard)
        => new(TimeSpan.FromMilliseconds(ElapsedMilliseconds), TimeSpan.FromMilliseconds(16),
            new MouseState(Position.X, Position.Y, 0, Left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
            Keyboard);
}
