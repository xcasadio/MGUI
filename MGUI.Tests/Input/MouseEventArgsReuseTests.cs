using MGUI.Core.UI;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace MGUI.Tests.Input;

/// <summary>
/// <see cref="MouseTracker"/> refills one <see cref="BaseMouseMovedEventArgs"/> from one move to the next, and one
/// <see cref="BaseMouseDraggedEventArgs"/> per drag start condition and button from one drag frame to the next, instead of creating them
/// every frame. Their values are those of the current frame; <see cref="MouseTracker.CurrentMoveEvent"/> stays null when the mouse is still.
/// </summary>
public class MouseEventArgsReuseTests
{
    private const int WarmupIterations = 2000;
    private const int MeasuredIterations = 1000;

    [Fact]
    public void TheMoveEvent_IsRefilledAtEachMove_AndNullWhileTheMouseIsStill()
    {
        InputTracker tracker = new();

        tracker.Update(CreateUpdateArgs(0, new Point(10, 10), ButtonState.Released));
        BaseMouseMovedEventArgs first = tracker.Mouse.CurrentMoveEvent;
        Assert.NotNull(first);

        tracker.Update(CreateUpdateArgs(16, new Point(20, 15), ButtonState.Released));
        Assert.Same(first, tracker.Mouse.CurrentMoveEvent);
        Assert.Equal(new Point(10, 10), first.PreviousPosition);
        Assert.Equal(new Point(20, 15), first.CurrentPosition);

        tracker.Update(CreateUpdateArgs(32, new Point(20, 15), ButtonState.Released));
        Assert.Null(tracker.Mouse.CurrentMoveEvent);

        tracker.Update(CreateUpdateArgs(48, new Point(5, 5), ButtonState.Released));
        Assert.Same(first, tracker.Mouse.CurrentMoveEvent);
        Assert.Equal(new Point(20, 15), first.PreviousPosition);
        Assert.Equal(new Point(5, 5), first.CurrentPosition);
    }

    [Fact]
    public void TheDraggedEvent_IsRefilledAtEachDragFrame_WithItsPositionStartAndTime()
    {
        InputTracker tracker = new();
        tracker.Update(CreateUpdateArgs(0, new Point(10, 10), ButtonState.Released));
        tracker.Update(CreateUpdateArgs(16, new Point(10, 10), ButtonState.Pressed));
        //  MouseMovedAfterPress starts the drag at the first move after the press; MousePressed started it at the press.
        tracker.Update(CreateUpdateArgs(32, new Point(30, 10), ButtonState.Pressed));

        tracker.Update(CreateUpdateArgs(48, new Point(40, 10), ButtonState.Pressed));
        BaseMouseDraggedEventArgs afterMove = tracker.Mouse.CurrentDraggedEvents[DragStartCondition.MouseMovedAfterPress][MouseButton.Left];
        BaseMouseDraggedEventArgs afterPress = tracker.Mouse.CurrentDraggedEvents[DragStartCondition.MousePressed][MouseButton.Left];
        Assert.NotNull(afterMove);
        Assert.NotNull(afterPress);
        Assert.NotSame(afterMove, afterPress);
        Assert.Equal(MouseButton.Left, afterMove.Button);
        Assert.Equal(new Point(40, 10), afterMove.Position);
        Assert.Equal(new Point(10, 10), afterMove.StartPosition);
        Assert.Equal(TimeSpan.FromMilliseconds(48), afterMove.Timestamp);

        tracker.Update(CreateUpdateArgs(64, new Point(50, 12), ButtonState.Pressed));
        Assert.Same(afterMove, tracker.Mouse.CurrentDraggedEvents[DragStartCondition.MouseMovedAfterPress][MouseButton.Left]);
        Assert.Same(afterPress, tracker.Mouse.CurrentDraggedEvents[DragStartCondition.MousePressed][MouseButton.Left]);
        Assert.Equal(new Point(50, 12), afterMove.Position);
        Assert.Equal(new Point(10, 10), afterMove.StartPosition);
        Assert.Equal(TimeSpan.FromMilliseconds(64), afterMove.Timestamp);
        Assert.Equal(new Point(50, 12), afterPress.Position);

        tracker.Update(CreateUpdateArgs(80, new Point(50, 12), ButtonState.Pressed));
        Assert.Null(tracker.Mouse.CurrentDraggedEvents[DragStartCondition.MouseMovedAfterPress][MouseButton.Left]);
    }

    [Fact]
    public void DraggingASliderThumb_MovesItsValueWithTheMouse()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 300, 200);
        desktop.Windows.Add(window);
        MGSlider slider = new(window, 0, 100, 50);
        window.SetContent(slider);
        List<float> values = new();
        slider.ValueChangedNonAlloc += (_, e) => values.Add(e.NewValue);

        int elapsed = 0;
        void Frame(Point position, ButtonState left)
        {
            runtime.ApplyFrame(CreateUpdateArgs(elapsed, position, left));
            desktop.Update();
            desktop.Draw();
            elapsed += 16;
        }

        Frame(new Point(1, 1), ButtonState.Released);
        Frame(new Point(1, 1), ButtonState.Released);
        Rectangle bounds = slider.LayoutBounds;
        Point thumb = slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, bounds.Center);
        Frame(thumb, ButtonState.Released);
        Frame(thumb, ButtonState.Released);
        Frame(thumb, ButtonState.Pressed);
        Frame(thumb + new Point(20, 0), ButtonState.Pressed);
        Frame(thumb + new Point(40, 0), ButtonState.Pressed);
        Frame(thumb + new Point(400, 0), ButtonState.Pressed);
        Frame(thumb + new Point(400, 0), ButtonState.Released);

        Assert.NotEmpty(values);
        for (int i = 1; i < values.Count; i++)
        {
            Assert.True(values[i] > values[i - 1], $"value {i} ({values[i]}) does not follow the mouse ({values[i - 1]} before)");
        }
        Assert.Equal(100f, slider.Value);
    }

    [Fact]
    public void MoveFrames_WithAMovedInsideSubscriber_AllocateNothing()
    {
        InputTracker tracker = new();
        int moves = 0;
        MouseHandler handler = tracker.Mouse.CreateHandler(new MouseHandlerHost(() => new Rectangle(0, 0, 100, 100)), 10);
        handler.MovedInside += (_, _) => moves++;
        UpdateBaseArgs first = CreateUpdateArgs(0, new Point(10, 10), ButtonState.Released);
        UpdateBaseArgs second = CreateUpdateArgs(16, new Point(20, 20), ButtonState.Released);
        int frame = 0;

        long bytes = Measure(() =>
        {
            tracker.Update(frame++ % 2 == 0 ? first : second);
            tracker.Mouse.UpdateHandlers();
        });

        Assert.Equal(0, bytes);
        Assert.True(moves > 0);
    }

    [Fact]
    public void DragFrames_WithADraggedSubscriber_AllocateNothing()
    {
        InputTracker tracker = new();
        int drags = 0;
        MouseHandler handler = tracker.Mouse.CreateHandler(new MouseHandlerHost(() => new Rectangle(0, 0, 100, 100)), 10);
        handler.Dragged += (_, _) => drags++;
        tracker.Update(CreateUpdateArgs(0, new Point(10, 10), ButtonState.Released));
        tracker.Mouse.UpdateHandlers();
        tracker.Update(CreateUpdateArgs(16, new Point(10, 10), ButtonState.Pressed));
        tracker.Mouse.UpdateHandlers();
        UpdateBaseArgs first = CreateUpdateArgs(32, new Point(30, 30), ButtonState.Pressed);
        UpdateBaseArgs second = CreateUpdateArgs(48, new Point(40, 40), ButtonState.Pressed);
        int frame = 0;

        long bytes = Measure(() =>
        {
            tracker.Update(frame++ % 2 == 0 ? first : second);
            tracker.Mouse.UpdateHandlers();
        });

        Assert.Equal(0, bytes);
        Assert.True(drags > 0);
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

    private static UpdateBaseArgs CreateUpdateArgs(int ElapsedMilliseconds, Point Position, ButtonState Left)
        => new(TimeSpan.FromMilliseconds(ElapsedMilliseconds), TimeSpan.FromMilliseconds(16),
            new MouseState(Position.X, Position.Y, 0, Left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
            new KeyboardState());
}
