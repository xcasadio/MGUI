using MGUI.Core.UI.Docking;
using MGUI.Tests.Architecture;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit.Abstractions;

namespace MGUI.Tests.Docking;

/// <summary>
/// A tab drag allocates nothing, over every kind of drop target (ADR-0036): the mouse alternates between two positions of the same target, ten pixels apart (more than the
/// 5 that make the host compute the target again), and the update and the draw of the desktop are measured apart.
/// </summary>
[Collection(AllocationMeasurementsCollection.Name)]
public class DockDropTargetAllocationTests
{
    private readonly ITestOutputHelper Output;

    public DockDropTargetAllocationTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private static readonly Point OutsideTheHost = new(630, 350);

    private static Point Shift(Point p) => new(p.X + 8, p.Y + 6);

    /// <summary>The two positions of a host edge indicator: both on its square and in the same group (a move from a group to another makes the host show the joystick of the
    /// new group, which is not what is measured here).</summary>
    private static (Point First, Point Second, Func<DockDropTarget, bool> Is) HostEdgeCase(DockDropTargetScene s, DockZone zone)
    {
        Point c = s.HostEdge(zone);
        bool alongY = zone is DockZone.Left or DockZone.Right;
        Point first = alongY ? new Point(c.X, c.Y - 6) : new Point(c.X - 12, c.Y);
        Point second = alongY ? new Point(c.X + 6, c.Y - 14) : new Point(c.X - 18, c.Y + 6);
        return (first, second, t => t.Zone == zone && t.IsHostEdge && t.TargetNode == null);
    }

    /// <summary>The two positions of a kind of target, and what the target must be at both.</summary>
    private static (Point First, Point Second, Func<DockDropTarget, bool> Is) Case(DockDropTargetScene s, string name)
    {
        Rectangle b = s.Bounds(s.GroupB);
        switch (name)
        {
            case "centre of another group":
                return (s.Joystick(s.GroupB, DockZone.Center), Shift(s.Joystick(s.GroupB, DockZone.Center)), t => t.Zone == DockZone.Center && t.TabIndex == -1 && ReferenceEquals(t.TargetNode, s.GroupB));
            case "split zone Left":
                return (s.Joystick(s.GroupB, DockZone.Left), Shift(s.Joystick(s.GroupB, DockZone.Left)), t => t.Zone == DockZone.Left && !t.IsHostEdge && !t.IsSplitterDrop);
            case "split zone Right":
                return (s.Joystick(s.GroupB, DockZone.Right), Shift(s.Joystick(s.GroupB, DockZone.Right)), t => t.Zone == DockZone.Right && !t.IsHostEdge && !t.IsSplitterDrop);
            case "split zone Top":
                return (s.Joystick(s.GroupB, DockZone.Top), Shift(s.Joystick(s.GroupB, DockZone.Top)), t => t.Zone == DockZone.Top && !t.IsHostEdge && !t.IsSplitterDrop);
            case "split zone Bottom":
                return (s.Joystick(s.GroupB, DockZone.Bottom), Shift(s.Joystick(s.GroupB, DockZone.Bottom)), t => t.Zone == DockZone.Bottom && !t.IsHostEdge && !t.IsSplitterDrop);
            case "host edge Left":
                return HostEdgeCase(s, DockZone.Left);
            case "host edge Right":
                return HostEdgeCase(s, DockZone.Right);
            case "host edge Top":
                return HostEdgeCase(s, DockZone.Top);
            case "host edge Bottom":
                return HostEdgeCase(s, DockZone.Bottom);
            case "splitter bar between A and B":
            {
                //  Along the bar: the position stays on it.
                Point first = s.RootBar(true);
                return (first, new Point(first.X, first.Y + 10), t => t.IsSplitterDrop && t.Zone == DockZone.Right && ReferenceEquals(t.TargetNode, s.GroupA) && t.SplitterNode == s.Host.LayoutModel.RootNode);
            }
            case "splitter bar between B and C":
            {
                Point first = s.InnerBar(false);
                return (first, new Point(first.X + 10, first.Y), t => t.IsSplitterDrop && t.Zone == DockZone.Top && ReferenceEquals(t.TargetNode, s.GroupC));
            }
            case "centre of the group of the dragged tab (reorder)":
                return (s.Joystick(s.GroupA, DockZone.Center), Shift(s.Joystick(s.GroupA, DockZone.Center)), t => t.Zone == DockZone.Center && t.TabIndex >= 0 && ReferenceEquals(t.TargetNode, s.GroupA));
            case "proximity band":
            {
                Point first = new(b.X + 12, b.Y + 70);
                return (first, Shift(first), t => t.Zone == DockZone.Left && !t.IsSplitterDrop && ReferenceEquals(t.TargetNode, s.GroupB));
            }
            case "no target":
                return (OutsideTheHost, new Point(OutsideTheHost.X - 8, OutsideTheHost.Y - 6), t => false);
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, null);
        }
    }

    [Theory]
    [InlineData("centre of another group")]
    [InlineData("split zone Left")]
    [InlineData("split zone Right")]
    [InlineData("split zone Top")]
    [InlineData("split zone Bottom")]
    [InlineData("host edge Left")]
    [InlineData("host edge Right")]
    [InlineData("host edge Top")]
    [InlineData("host edge Bottom")]
    [InlineData("splitter bar between A and B")]
    [InlineData("splitter bar between B and C")]
    [InlineData("centre of the group of the dragged tab (reorder)")]
    [InlineData("proximity band")]
    [InlineData("no target")]
    public void TabDrag_OverEachKindOfTarget_UpdateAndDraw_AllocateNothing(string kind)
    {
        DockDropTargetScene scene = DockDropTargetScene.Create();
        (Point first, Point second, Func<DockDropTarget, bool> isTheTarget) = Case(scene, kind);
        scene.PressAndStartTheDrag();

        void CheckTheTarget()
        {
            Assert.NotNull(scene.Host.CurrentDrag);
            if (kind == "no target")
            {
                Assert.Null(scene.Host.CurrentDropTarget);
            }
            else
            {
                Assert.NotNull(scene.Host.CurrentDropTarget);
                Assert.True(isTheTarget(scene.Host.CurrentDropTarget), $"the target at the mouse is not the {kind}: {DockDropTargetRecording.Values(scene.Host.CurrentDropTarget)}");
            }
        }

        scene.Bench.Mouse = first;
        scene.Bench.Frames(3);
        CheckTheTarget();
        scene.Bench.Mouse = second;
        scene.Bench.Frames(3);
        CheckTheTarget();
        scene.Bench.Mouse = first;
        scene.Bench.Frames(3);

        FrameBytes bytes = FrameAllocationMeter.Measure(frame => scene.Bench.Mouse = (frame & 1) == 0 ? first : second, scene.Bench.Update, scene.Bench.Draw);
        Output.WriteLine($"tab drag over {kind}: Update {bytes.Update} B/frame, Draw {bytes.Draw} B/frame (totals {bytes.UpdateTotal} and {bytes.DrawTotal} B over {bytes.Iterations} frames)");
        CheckTheTarget();
        scene.Bench.Left = ButtonState.Released;
        scene.Bench.Frames(2);

        Assert.Equal(0, bytes.UpdateTotal);
        Assert.Equal(0, bytes.DrawTotal);
    }
}
