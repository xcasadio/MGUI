using MGUI.Shared.Input;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Input;

/// <summary>Pins the fast-path drag flags on <see cref="MouseTracker"/>: <c>HasCurrentDragStartEvents</c>,
/// <c>HasCurrentDraggedEvents</c> and <c>HasCurrentDragEndEvents</c> must reflect whether any button actually has a
/// drag event this tick, not just whether the per-condition inner dictionary itself is non-null (which it always is).</summary>
public class MouseTrackerDragFastPathTests
{
    private static UpdateBaseArgs CreateUpdateArgs(int elapsedMs, MouseState mouseState)
        => new(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(16), mouseState, new KeyboardState());

    private static MouseState CreateMouseState(Point position, ButtonState left = ButtonState.Released)
        => new(position.X, position.Y, 0, left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

    [Fact]
    public void MouseTracker_DragFlags_AreFalse_WhenNoDragActivity()
    {
        InputTracker tracker = new();

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(10, 10))));

        Assert.False(tracker.Mouse.HasCurrentDragStartEvents);
        Assert.False(tracker.Mouse.HasCurrentDraggedEvents);
        Assert.False(tracker.Mouse.HasCurrentDragEndEvents);
        Assert.False(tracker.Mouse.HasCurrentDragEvents);
    }

    [Fact]
    public void MouseTracker_DragFlags_AreFalse_WhenMouseMovesWithNoButtonPressed()
    {
        InputTracker tracker = new();

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(10, 10))));
        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(50, 50))));

        Assert.False(tracker.Mouse.HasCurrentDragStartEvents);
        Assert.False(tracker.Mouse.HasCurrentDraggedEvents);
        Assert.False(tracker.Mouse.HasCurrentDragEndEvents);
        Assert.False(tracker.Mouse.HasCurrentDragEvents);
    }

    [Fact]
    public void MouseTracker_DragStartFlag_IsTrue_OnTheTickTheButtonIsPressed()
    {
        // DragStartCondition.MousePressed starts a drag on the exact tick the button transitions to Pressed,
        // with no movement-threshold requirement.
        InputTracker tracker = new();

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(10, 10))));
        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(10, 10), ButtonState.Pressed)));

        Assert.True(tracker.Mouse.HasCurrentDragStartEvents);
        Assert.True(tracker.Mouse.HasCurrentDragEvents);
        Assert.False(tracker.Mouse.HasCurrentDraggedEvents);
        Assert.False(tracker.Mouse.HasCurrentDragEndEvents);
    }

    [Fact]
    public void MouseTracker_DraggedFlag_IsTrue_WhileDragInProgress()
    {
        InputTracker tracker = new();
        int dragThreshold = tracker.Mouse.DragThreshold;

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(10, 10))));
        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(10, 10), ButtonState.Pressed)));
        tracker.Update(CreateUpdateArgs(20, CreateMouseState(new Point(10 + dragThreshold + 5, 10), ButtonState.Pressed)));
        tracker.Update(CreateUpdateArgs(30, CreateMouseState(new Point(10 + dragThreshold + 15, 10), ButtonState.Pressed)));

        Assert.True(tracker.Mouse.HasCurrentDraggedEvents);
        Assert.True(tracker.Mouse.HasCurrentDragEvents);
        Assert.False(tracker.Mouse.HasCurrentDragEndEvents);
    }

    [Fact]
    public void MouseTracker_DragEndFlag_IsTrue_WhenButtonReleasedDuringDrag_ThenAllFlagsGoFalseAgain()
    {
        InputTracker tracker = new();
        int dragThreshold = tracker.Mouse.DragThreshold;

        tracker.Update(CreateUpdateArgs(0, CreateMouseState(new Point(10, 10))));
        tracker.Update(CreateUpdateArgs(10, CreateMouseState(new Point(10, 10), ButtonState.Pressed)));
        tracker.Update(CreateUpdateArgs(20, CreateMouseState(new Point(10 + dragThreshold + 5, 10), ButtonState.Pressed)));
        tracker.Update(CreateUpdateArgs(30, CreateMouseState(new Point(10 + dragThreshold + 15, 10))));

        Assert.True(tracker.Mouse.HasCurrentDragEndEvents);
        Assert.True(tracker.Mouse.HasCurrentDragEvents);

        tracker.Update(CreateUpdateArgs(40, CreateMouseState(new Point(10 + dragThreshold + 15, 10))));

        Assert.False(tracker.Mouse.HasCurrentDragStartEvents);
        Assert.False(tracker.Mouse.HasCurrentDraggedEvents);
        Assert.False(tracker.Mouse.HasCurrentDragEndEvents);
        Assert.False(tracker.Mouse.HasCurrentDragEvents);
    }
}
