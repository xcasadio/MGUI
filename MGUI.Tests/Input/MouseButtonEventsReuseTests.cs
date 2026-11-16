using MGUI.Shared.Input;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace MGUI.Tests.Input;

/// <summary>
/// <see cref="MouseTracker"/> keeps one press, one release and one click per button, one drag start and one drag end per drag start condition and button, and one click sequence per button
/// (ADR-0034): each is created at its first use and refilled afterwards, with its handled state reset and, for a sequence, a new identity and no adopted target. The events delivered are the
/// ones that <see cref="MouseButtonEventsReplayTests"/> freezes; these tests pin the reuse itself, the validity of a press while its button is held, and the zero allocation of the frames.
/// </summary>
public class MouseButtonEventsReuseTests
{
    private const int WarmupIterations = 2000;
    private const int MeasuredIterations = 1000;

    private static UpdateBaseArgs Args(int elapsedMilliseconds, Point position, MouseButton? down = null)
        => new(TimeSpan.FromMilliseconds(elapsedMilliseconds), TimeSpan.FromMilliseconds(16),
            new MouseState(position.X, position.Y, 0,
                down == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
                down == MouseButton.Middle ? ButtonState.Pressed : ButtonState.Released,
                down == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released, ButtonState.Released),
            new KeyboardState());

    private sealed class Session
    {
        public InputTracker Tracker { get; } = new();
        public int Elapsed { get; private set; }

        public void Frame(Point position, MouseButton? down = null, int dt = 16)
        {
            Elapsed += dt;
            Tracker.Update(Args(Elapsed, position, down));
        }
    }

    public static IEnumerable<object[]> Buttons() => new[]
    {
        new object[] { MouseButton.Left },
        new object[] { MouseButton.Middle },
        new object[] { MouseButton.Right },
    };

    [Theory]
    [MemberData(nameof(Buttons))]
    public void ThePressReleaseAndClick_AreTheSameInstancesAtEachClick_WithTheValuesOfEachClick(MouseButton button)
    {
        Session session = new();
        MouseTracker mouse = session.Tracker.Mouse;
        session.Frame(new Point(10, 10));

        session.Frame(new Point(10, 10), button);
        BaseMousePressedEventArgs press = mouse.CurrentButtonPressedEvents[button];
        Assert.NotNull(press);
        Assert.Equal(button, press.Button);
        Assert.Equal(new Point(10, 10), press.Position);
        int pressedAt = session.Elapsed;
        Assert.Equal(TimeSpan.FromMilliseconds(pressedAt), press.PressedAt);
        session.Frame(new Point(10, 10));
        BaseMouseReleasedEventArgs release = mouse.CurrentButtonReleasedEvents[button];
        BaseMouseClickedEventArgs click = mouse.CurrentButtonClickedEvents[button];
        Assert.NotNull(release);
        Assert.NotNull(click);
        Assert.Same(press, release.PressedArgs);
        Assert.Same(release, click.ReleasedArgs);
        Assert.Equal(TimeSpan.FromMilliseconds(16), release.HeldDuration);
        Assert.Equal(1, click.ClickCount);
        Assert.NotNull(click.Sequence);

        session.Frame(new Point(30, 20), button, 700);
        Assert.Same(press, mouse.CurrentButtonPressedEvents[button]);
        Assert.Equal(new Point(30, 20), press.Position);
        Assert.Equal(TimeSpan.FromMilliseconds(session.Elapsed), press.PressedAt);
        Assert.Null(mouse.CurrentButtonReleasedEvents[button]);
        Assert.Null(mouse.CurrentButtonClickedEvents[button]);
        session.Frame(new Point(30, 20), null, 32);
        Assert.Same(release, mouse.CurrentButtonReleasedEvents[button]);
        Assert.Same(click, mouse.CurrentButtonClickedEvents[button]);
        Assert.Equal(new Point(30, 20), release.Position);
        Assert.Equal(TimeSpan.FromMilliseconds(32), release.HeldDuration);
        Assert.Equal(new Point(30, 20), click.Position);
        Assert.Equal(1, click.ClickCount);

        //  Another button has its own instances.
        MouseButton other = button == MouseButton.Left ? MouseButton.Right : MouseButton.Left;
        session.Frame(new Point(30, 20), other);
        Assert.NotSame(press, mouse.CurrentButtonPressedEvents[other]);
        Assert.Equal(other, mouse.CurrentButtonPressedEvents[other].Button);
    }

    [Fact]
    public void ThePressReleaseClickAndDragStart_StartEachEventNotHandled_EvenIfThePreviousOneWasHandled()
    {
        Session session = new();
        MouseTracker mouse = session.Tracker.Mouse;
        MouseHandlerHost host = new(() => new Rectangle(0, 0, 100, 100));
        session.Frame(new Point(10, 10));

        session.Frame(new Point(10, 10), MouseButton.Left);
        BaseMousePressedEventArgs press = mouse.CurrentButtonPressedEvents[MouseButton.Left];
        BaseMouseDragStartEventArgs dragStart = mouse.CurrentDragStartEvents[DragStartCondition.MousePressed][MouseButton.Left];
        press.SetHandledBy(host);
        dragStart.SetHandledBy(host);
        Assert.True(press.IsHandled);
        session.Frame(new Point(10, 10));
        BaseMouseReleasedEventArgs release = mouse.CurrentButtonReleasedEvents[MouseButton.Left];
        BaseMouseClickedEventArgs click = mouse.CurrentButtonClickedEvents[MouseButton.Left];
        release.SetHandledBy(host);
        click.SetHandledBy(host);
        Assert.True(release.IsHandled);
        Assert.True(click.IsHandled);

        session.Frame(new Point(10, 10), MouseButton.Left);
        Assert.Same(press, mouse.CurrentButtonPressedEvents[MouseButton.Left]);
        Assert.False(press.IsHandled);
        Assert.Null(press.HandledBy);
        Assert.Same(dragStart, mouse.CurrentDragStartEvents[DragStartCondition.MousePressed][MouseButton.Left]);
        Assert.False(dragStart.IsHandled);
        session.Frame(new Point(10, 10));
        Assert.Same(release, mouse.CurrentButtonReleasedEvents[MouseButton.Left]);
        Assert.Same(click, mouse.CurrentButtonClickedEvents[MouseButton.Left]);
        Assert.False(release.IsHandled);
        Assert.Null(release.HandledBy);
        Assert.False(click.IsHandled);
        Assert.Null(click.HandledBy);
    }

    [Fact]
    public void APress_StaysValidWithItsHandledState_WhileTheButtonIsHeldAndDuringItsRelease()
    {
        Session session = new();
        MouseTracker mouse = session.Tracker.Mouse;
        MouseHandlerHost host = new(() => new Rectangle(0, 0, 100, 100));
        session.Frame(new Point(10, 10));

        session.Frame(new Point(10, 10), MouseButton.Left);
        BaseMousePressedEventArgs press = mouse.CurrentButtonPressedEvents[MouseButton.Left];
        press.SetHandledBy(host);
        TimeSpan pressedAt = press.PressedAt;

        //  Held for several frames.
        for (int i = 0; i < 6; i++)
        {
            session.Frame(new Point(10, 10), MouseButton.Left);
            Assert.Same(press, mouse.RecentButtonPressedEvents[MouseButton.Left]);
            Assert.Equal(pressedAt, press.PressedAt);
            Assert.Equal(new Point(10, 10), press.Position);
            Assert.Same(host, press.HandledBy);
        }

        session.Frame(new Point(10, 10), MouseButton.Left, 16);
        session.Frame(new Point(10, 10));
        Assert.Null(mouse.RecentButtonPressedEvents[MouseButton.Left]);
        BaseMouseReleasedEventArgs release = mouse.CurrentButtonReleasedEvents[MouseButton.Left];
        Assert.Same(press, release.PressedArgs);
        Assert.Equal(pressedAt, release.PressedArgs.PressedAt);
        Assert.Same(host, release.PressedArgs.HandledBy);
        Assert.Same(press, mouse.CurrentButtonClickedEvents[MouseButton.Left].PressedArgs);

        //  Only the next press of the button refills it.
        session.Frame(new Point(40, 40), MouseButton.Left, 900);
        Assert.Same(press, mouse.CurrentButtonPressedEvents[MouseButton.Left]);
        Assert.NotEqual(pressedAt, press.PressedAt);
        Assert.False(press.IsHandled);
    }

    [Fact]
    public void TheDragStart_StaysValidWithItsHandledState_UntilTheNextDragStartOfItsConditionAndButton()
    {
        Session session = new();
        MouseTracker mouse = session.Tracker.Mouse;
        MouseHandlerHost host = new(() => new Rectangle(0, 0, 100, 100));
        session.Frame(new Point(10, 10));
        session.Frame(new Point(10, 10), MouseButton.Left);
        session.Frame(new Point(20, 10), MouseButton.Left);
        BaseMouseDragStartEventArgs start = mouse.CurrentDragStartEvents[DragStartCondition.MouseMovedAfterPress][MouseButton.Left];
        Assert.NotNull(start);
        start.SetHandledBy(host);

        session.Frame(new Point(30, 10), MouseButton.Left);
        BaseMouseDraggedEventArgs dragged = mouse.CurrentDraggedEvents[DragStartCondition.MouseMovedAfterPress][MouseButton.Left];
        Assert.Same(start, dragged.DragStartArgs);
        Assert.Same(host, dragged.DragStartArgs.HandledBy);
        session.Frame(new Point(40, 10));
        BaseMouseDragEndEventArgs end = mouse.CurrentDragEndEvents[DragStartCondition.MouseMovedAfterPress][MouseButton.Left];
        Assert.NotNull(end);
        Assert.Same(start, end.DragStartArgs);
        Assert.Same(host, end.DragStartArgs.HandledBy);
        Assert.Equal(new Point(10, 10), end.StartPosition);
        Assert.Equal(new Point(40, 10), end.EndPosition);

        session.Frame(new Point(60, 60), MouseButton.Left, 900);
        session.Frame(new Point(70, 60), MouseButton.Left);
        Assert.Same(start, mouse.CurrentDragStartEvents[DragStartCondition.MouseMovedAfterPress][MouseButton.Left]);
        Assert.False(start.IsHandled);
        Assert.Equal(new Point(60, 60), start.Position);
        session.Frame(new Point(80, 60));
        Assert.Same(end, mouse.CurrentDragEndEvents[DragStartCondition.MouseMovedAfterPress][MouseButton.Left]);
        Assert.Equal(new Point(60, 60), end.StartPosition);
        Assert.Equal(new Point(80, 60), end.EndPosition);
    }

    [Fact]
    public void TheClickSequence_IsOneInstancePerButton_RestartedWithANewIdentityAtEachNewSequence()
    {
        Session session = new();
        MouseTracker mouse = session.Tracker.Mouse;
        session.Frame(new Point(10, 10));

        void Click(MouseButton button, int gap)
        {
            session.Frame(new Point(10, 10), button, gap);
            session.Frame(new Point(10, 10));
        }

        Click(MouseButton.Left, 16);
        MouseClickSequence sequence = mouse.CurrentButtonClickedEvents[MouseButton.Left].Sequence;
        long firstId = sequence.Id;
        TimeSpan firstStart = sequence.StartedAt;
        Assert.Equal(MouseButton.Left, sequence.Button);
        Assert.Equal(1, sequence.ClickCount);
        Assert.True(sequence.CanBeAdoptedBy(7));
        Assert.True(sequence.TryAdoptTarget(7, null));
        Assert.True(sequence.IsOwnedBy(7));
        Assert.False(sequence.CanBeAdoptedBy(8));

        //  A click that continues the sequence keeps its identity and its adopted target.
        Click(MouseButton.Left, 16);
        Assert.Same(sequence, mouse.CurrentButtonClickedEvents[MouseButton.Left].Sequence);
        Assert.Equal(firstId, sequence.Id);
        Assert.Equal(firstStart, sequence.StartedAt);
        Assert.Equal(2, sequence.ClickCount);
        Assert.True(sequence.IsOwnedBy(7));

        //  Another button has its own sequence, and does not disturb this one.
        Click(MouseButton.Right, 16);
        MouseClickSequence rightSequence = mouse.CurrentButtonClickedEvents[MouseButton.Right].Sequence;
        Assert.NotSame(sequence, rightSequence);
        Assert.Equal(MouseButton.Right, rightSequence.Button);
        Assert.NotEqual(firstId, rightSequence.Id);
        Assert.True(sequence.IsOwnedBy(7));

        //  A click that does not continue it (too late) restarts it: a new id, a new start time, a new button state, and no adopted target.
        Click(MouseButton.Left, 900);
        BaseMouseClickedEventArgs restarted = mouse.CurrentButtonClickedEvents[MouseButton.Left];
        Assert.Same(sequence, restarted.Sequence);
        Assert.Equal(1, restarted.ClickCount);
        Assert.NotEqual(firstId, sequence.Id);
        Assert.Equal(MouseButton.Left, sequence.Button);
        Assert.True(sequence.StartedAt > firstStart);
        Assert.Equal(1, sequence.ClickCount);
        Assert.False(sequence.IsOwnedBy(7));
        Assert.True(sequence.CanBeAdoptedBy(8));
        Assert.False(sequence.TryGetValidatedTargetHost(out _));
    }

    [Fact]
    public void TheTargetAdoptedByASequence_DoesNotSurviveIntoTheNextSequenceOfTheSameButton()
    {
        Session session = new();
        MouseTracker mouse = session.Tracker.Mouse;
        MouseHandlerHost hostX = new(() => new Rectangle(0, 0, 100, 100));
        MouseHandlerHost hostY = new(() => new Rectangle(200, 0, 100, 100));
        MouseHandler x = mouse.CreateHandler(hostX, 20);
        MouseHandler y = mouse.CreateHandler(hostY, 10);
        int xClicks = 0, xDoubles = 0, yClicks = 0, yDoubles = 0;
        x.LMBClickedInside += (_, _) => xClicks++;
        x.LMBDoubleClickedInside += (_, _) => xDoubles++;
        y.LMBClickedInside += (_, _) => yClicks++;
        y.LMBDoubleClickedInside += (_, _) => yDoubles++;
        session.Frame(new Point(50, 50));
        mouse.UpdateHandlers();

        void Click(Point at, int gap)
        {
            session.Frame(at, MouseButton.Left, gap);
            mouse.UpdateHandlers();
            session.Frame(at);
            mouse.UpdateHandlers();
        }

        Click(new Point(50, 50), 16);
        long firstId = mouse.CurrentButtonClickedEvents[MouseButton.Left].Sequence.Id;
        Click(new Point(50, 50), 16);
        Assert.Equal(firstId, mouse.CurrentButtonClickedEvents[MouseButton.Left].Sequence.Id);
        Assert.Equal(2, xClicks);
        Assert.Equal(1, xDoubles);
        Assert.Equal(0, yClicks);
        Assert.Equal(0, yDoubles);

        //  The next sequence of the button starts on Y: before any handler sees its first click it has no adopted target, and Y receives its double click.
        session.Frame(new Point(250, 50), MouseButton.Left, 900);
        session.Frame(new Point(250, 50));
        MouseClickSequence second = mouse.CurrentButtonClickedEvents[MouseButton.Left].Sequence;
        Assert.NotEqual(firstId, second.Id);
        Assert.False(second.TryGetValidatedTargetHost(out _));
        Assert.True(second.CanBeAdoptedBy(long.MaxValue));
        mouse.UpdateHandlers();
        Click(new Point(250, 50), 16);
        Assert.Equal(2, mouse.CurrentButtonClickedEvents[MouseButton.Left].ClickCount);
        Assert.Equal(2, yClicks);
        Assert.Equal(1, yDoubles);
        Assert.Equal(1, xDoubles);
        Assert.Equal(2, xClicks);

        //  And back on X: again a new sequence, again no target kept from Y.
        Click(new Point(50, 50), 900);
        Click(new Point(50, 50), 16);
        Assert.Equal(2, xDoubles);
        Assert.Equal(1, yDoubles);
    }

    [Fact]
    public void TheMultiClickCount_IsDetectedOnTheValuesOfThePreviousClick()
    {
        UpdateBaseArgs release = Args(500, new Point(10, 10));
        InputTracker tracker = new();
        tracker.Update(Args(0, new Point(10, 10), MouseButton.Left));
        tracker.Update(release);
        BaseMouseReleasedEventArgs released = tracker.Mouse.CurrentButtonReleasedEvents[MouseButton.Left];
        TimeSpan within = TimeSpan.FromMilliseconds(500);

        Assert.Equal(1, MouseTracker.GetMultiClickCount(0, TimeSpan.Zero, Point.Zero, released, within, 4));
        Assert.Equal(1, MouseTracker.GetMultiClickCount(3, TimeSpan.FromMilliseconds(500), new Point(10, 10), null, within, 4));
        Assert.Equal(4, MouseTracker.GetMultiClickCount(3, TimeSpan.FromMilliseconds(400), new Point(12, 8), released, within, 4));
        Assert.Equal(1, MouseTracker.GetMultiClickCount(3, TimeSpan.FromMilliseconds(400), new Point(15, 10), released, within, 4));
        Assert.Equal(1, MouseTracker.GetMultiClickCount(3, TimeSpan.FromMilliseconds(-100), new Point(10, 10), released, within, 4));
        Assert.Equal(2, MouseTracker.GetMultiClickCount(1, TimeSpan.FromMilliseconds(0), new Point(10, 10), released, within, 4));
    }

    [Theory]
    [MemberData(nameof(Buttons))]
    public void ClickFrames_WithPressedReleasedClickedAndDoubleClickedSubscribers_AllocateNothing(MouseButton button)
    {
        InputTracker tracker = new();
        MouseHandler handler = tracker.Mouse.CreateHandler(new MouseHandlerHost(() => new Rectangle(0, 0, 100, 100)), 10);
        int events = 0;
        handler.PressedInside += (_, _) => events++;
        handler.ReleasedInside += (_, _) => events++;
        handler.ClickedInside += (_, _) => events++;
        handler.DoubleClickedInside += (_, _) => events++;
        handler.LMBPressedInside += (_, _) => events++;
        handler.MMBPressedInside += (_, _) => events++;
        handler.RMBPressedInside += (_, _) => events++;
        handler.LMBClickedInside += (_, _) => events++;
        handler.MMBClickedInside += (_, _) => events++;
        handler.RMBClickedInside += (_, _) => events++;
        handler.PressedOutside += (_, _) => events++;
        handler.ReleasedOutside += (_, _) => events++;

        //  A click every four frames; a pause of 600 ms every ten clicks, which starts a new sequence.
        int total = WarmupIterations + MeasuredIterations + 8;
        UpdateBaseArgs[] frames = new UpdateBaseArgs[total];
        int clock = 0;
        for (int i = 0; i < total; i++)
        {
            clock += 16 + (i % 40 == 0 ? 600 : 0);
            Point at = (i / 40) % 2 == 0 ? new Point(10, 10) : new Point(300, 300);
            frames[i] = Args(clock, at, (i & 3) == 1 ? button : null);
        }

        int frame = 0;
        long bytes = Measure(() =>
        {
            tracker.Update(frames[frame++]);
            tracker.Mouse.UpdateHandlers();
        });

        Assert.Equal(0, bytes);
        Assert.True(events > 100, $"{events} events");
        Assert.NotNull(tracker.Mouse.RecentButtonClickedEvents[button]);
    }

    [Theory]
    [InlineData(DragStartCondition.MouseMovedAfterPress, MouseButton.Left)]
    [InlineData(DragStartCondition.MouseMovedAfterPress, MouseButton.Middle)]
    [InlineData(DragStartCondition.MouseMovedAfterPress, MouseButton.Right)]
    [InlineData(DragStartCondition.MousePressed, MouseButton.Left)]
    [InlineData(DragStartCondition.MousePressed, MouseButton.Middle)]
    [InlineData(DragStartCondition.MousePressed, MouseButton.Right)]
    [InlineData(DragStartCondition.Both, MouseButton.Left)]
    public void DragFrames_WithDragStartDraggedAndDragEndSubscribers_AllocateNothing(DragStartCondition condition, MouseButton button)
    {
        InputTracker tracker = new();
        MouseHandler handler = tracker.Mouse.CreateHandler(new MouseHandlerHost(() => new Rectangle(0, 0, 100, 100)), 10);
        handler.DragStartCondition = condition;
        int starts = 0, drags = 0, ends = 0;
        handler.DragStart += (_, _) => starts++;
        handler.Dragged += (_, _) => drags++;
        handler.DragEnd += (_, _) => ends++;

        //  A drag every six frames: press, three moves, release, rest.
        int total = WarmupIterations + MeasuredIterations + 8;
        UpdateBaseArgs[] frames = new UpdateBaseArgs[total];
        int clock = 0;
        for (int i = 0; i < total; i++)
        {
            clock += 16;
            int step = i % 6;
            Point at = step switch { 0 => new Point(10, 10), 1 => new Point(10, 10), 2 => new Point(20, 14), 3 => new Point(40, 20), 4 => new Point(60, 30), _ => new Point(60, 30) };
            frames[i] = Args(clock, at, step is >= 1 and <= 4 ? button : null);
        }

        int frame = 0;
        long bytes = Measure(() =>
        {
            tracker.Update(frames[frame++]);
            tracker.Mouse.UpdateHandlers();
        });

        Assert.Equal(0, bytes);
        Assert.True(starts > 100, $"{starts} drag starts");
        Assert.True(drags > 100, $"{drags} dragged");
        Assert.True(ends > 100, $"{ends} drag ends");
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
}
