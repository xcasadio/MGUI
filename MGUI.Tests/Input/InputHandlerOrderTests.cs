using MGUI.Shared.Input;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Tests.Input;

/// <summary>
/// <see cref="MouseTracker.UpdateHandlers"/> and <see cref="KeyboardTracker.UpdateHandlers"/> walk an order cached since the last handler was
/// added or removed, instead of sorting the handlers with LINQ every frame. The order is the one LINQ gave: descending priority, then
/// registration order; a handler added or removed during the walk leaves the walk in progress unchanged; manual handlers are never walked.
/// </summary>
public class InputHandlerOrderTests
{
    private const int WarmupIterations = 2000;
    private const int MeasuredIterations = 1000;

    private static readonly double[] MixedPriorities = { 10, 50, 10, 90, 50, 0, 90 };

    private sealed class KeyboardHost : IKeyboardHandlerHost
    {
        public bool CanReceiveKeyboardInput() => true;
        public bool HasKeyboardFocus() => true;
    }

    private static int[] LinqOrder(double[] priorities)
        => Enumerable.Range(0, priorities.Length).OrderByDescending(i => priorities[i]).GroupBy(i => priorities[i]).OrderByDescending(g => g.Key)
            .SelectMany(g => g).ToArray();

    [Fact]
    public void MouseUpdateHandlers_WalksByDescendingPriorityThenRegistrationOrder()
    {
        InputTracker tracker = new();
        List<int> order = new();
        for (int i = 0; i < MixedPriorities.Length; i++)
        {
            int index = i;
            CreateMouseHandler(tracker, MixedPriorities[i]).MovedInside += (_, _) => order.Add(index);
        }

        MoveMouse(tracker, 0, new Point(10, 10));
        order.Clear();
        MoveMouse(tracker, 16, new Point(20, 20));

        Assert.Equal(LinqOrder(MixedPriorities), order);
    }

    [Fact]
    public void KeyboardUpdateHandlers_WalksByDescendingPriorityThenRegistrationOrder()
    {
        InputTracker tracker = new();
        List<int> order = new();
        for (int i = 0; i < MixedPriorities.Length; i++)
        {
            int index = i;
            tracker.Keyboard.CreateHandler(new KeyboardHost(), MixedPriorities[i]).Pressed += (_, _) => order.Add(index);
        }

        tracker.Update(CreateUpdateArgs(0, new Point(10, 10), new KeyboardState()));
        tracker.Keyboard.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(16, new Point(10, 10), new KeyboardState(Keys.A)));
        tracker.Keyboard.UpdateHandlers();

        Assert.Equal(LinqOrder(MixedPriorities), order);
    }

    [Fact]
    public void AHandlerAddedDuringTheWalk_IsWalkedFromTheNextFrame()
    {
        InputTracker tracker = new();
        List<string> calls = new();
        MouseHandler added = null;
        bool addOnNextMove = false;
        CreateMouseHandler(tracker, 50).MovedInside += (_, _) =>
        {
            calls.Add("existing");
            if (addOnNextMove && added == null)
            {
                added = CreateMouseHandler(tracker, 100);
                added.MovedInside += (_, _) => calls.Add("added");
            }
        };

        MoveMouse(tracker, 0, new Point(10, 10));
        calls.Clear();
        addOnNextMove = true;
        MoveMouse(tracker, 16, new Point(20, 20));
        Assert.Equal(new[] { "existing" }, calls);

        calls.Clear();
        MoveMouse(tracker, 32, new Point(30, 30));
        Assert.Equal(new[] { "added", "existing" }, calls);
    }

    [Fact]
    public void AHandlerUnsubscribedDuringTheWalk_LeavesTheOthersWalked()
    {
        InputTracker tracker = new();
        List<string> calls = new();
        MouseHandler first = CreateMouseHandler(tracker, 90);
        MouseHandler second = CreateMouseHandler(tracker, 50);
        MouseHandler third = CreateMouseHandler(tracker, 10);
        first.MovedInside += (_, _) => { calls.Add("first"); second.Unsubscribe(); };
        second.MovedInside += (_, _) => calls.Add("second");
        third.MovedInside += (_, _) => calls.Add("third");

        MoveMouse(tracker, 0, new Point(10, 10));
        calls.Clear();
        MoveMouse(tracker, 16, new Point(20, 20));

        Assert.Equal(new[] { "first", "third" }, calls);
        Assert.DoesNotContain(second, tracker.Mouse.Handlers);
    }

    [Fact]
    public void ManualHandlers_AreNotWalked()
    {
        InputTracker tracker = new();
        List<string> calls = new();
        CreateMouseHandler(tracker, 10).MovedInside += (_, _) => calls.Add("auto");
        tracker.Mouse.CreateHandler(new MouseHandlerHost(() => new Rectangle(0, 0, 100, 100)), (double?)null).MovedInside += (_, _) => calls.Add("manual");

        MoveMouse(tracker, 0, new Point(10, 10));
        calls.Clear();
        MoveMouse(tracker, 16, new Point(20, 20));

        Assert.Equal(new[] { "auto" }, calls);
    }

    [Fact]
    public void MouseUpdateHandlers_WithTheMouseStill_AllocatesNothing()
    {
        InputTracker tracker = new();
        int moves = 0;
        for (int i = 0; i < MixedPriorities.Length; i++)
        {
            CreateMouseHandler(tracker, MixedPriorities[i]).MovedInside += (_, _) => moves++;
        }

        MoveMouse(tracker, 0, new Point(10, 10));
        tracker.Update(CreateUpdateArgs(16, new Point(10, 10), new KeyboardState()));

        Assert.Equal(0, Measure(tracker.Mouse.UpdateHandlers));
    }

    [Fact]
    public void KeyboardUpdateHandlers_AllocatesNothing()
    {
        InputTracker tracker = new();
        for (int i = 0; i < MixedPriorities.Length; i++)
        {
            tracker.Keyboard.CreateHandler(new KeyboardHost(), MixedPriorities[i]);
        }

        tracker.Update(CreateUpdateArgs(0, new Point(10, 10), new KeyboardState()));

        Assert.Equal(0, Measure(tracker.Keyboard.UpdateHandlers));
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

    private static MouseHandler CreateMouseHandler(InputTracker Tracker, double Priority)
        => Tracker.Mouse.CreateHandler(new MouseHandlerHost(() => new Rectangle(0, 0, 100, 100)), Priority);

    private static void MoveMouse(InputTracker Tracker, int ElapsedMilliseconds, Point Position)
    {
        Tracker.Update(CreateUpdateArgs(ElapsedMilliseconds, Position, new KeyboardState()));
        Tracker.Mouse.UpdateHandlers();
    }

    private static UpdateBaseArgs CreateUpdateArgs(int ElapsedMilliseconds, Point Position, KeyboardState Keyboard)
        => new(TimeSpan.FromMilliseconds(ElapsedMilliseconds), TimeSpan.FromMilliseconds(16),
            new MouseState(Position.X, Position.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
            Keyboard);
}
