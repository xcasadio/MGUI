using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Tests.Architecture;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit.Abstractions;

namespace MGUI.Tests.Docking;

/// <summary>
/// T0.1 of the plan "end of the zero allocation of the drawing", for T1.5: a tab drag whose mouse changes the hovered group at every frame. <see cref="DockDropTargetAllocationTests"/> moves the
/// mouse between two positions of one target (in one group) and asserts 0; here the two positions of each case are in two different groups of <see cref="DockDropTargetScene"/> (A left, B above C
/// on the right), so that the host shows the drop indicators of another group at every frame (the cause the previous plan guessed at and did not look for). The kinds of target are the ones of the
/// previous tests, taken in two groups: centre, split zone, proximity band, the reorder centre of the group of the dragged tab, and the host edge indicator that straddles two groups (the top edge of
/// the host sits on the boundary between A and B, the bottom edge between A and C: a probe of the previous plan measured 32 B per frame of update at the top edge).<para/>
/// Each case checks, before the measure and after it, that the drag is running, that the target at each position is the one the case names, and that the two positions are inside two different
/// groups. The numbers asserted are the totals over <see cref="FrameAllocationMeter.MeasuredFrames"/> frames as measured before T1.5 (the update and the draw told apart); T1.5 replaces them by 0
/// or by the remainder that it finds.
/// </summary>
[Collection(AllocationMeasurementsCollection.Name)]
public class DockGroupChangeAllocationTests
{
    private readonly ITestOutputHelper Output;

    public DockGroupChangeAllocationTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private const string CentreOfBThenC = "centre of B, centre of C";
    private const string ReorderCentreOfAThenCentreOfB = "reorder centre of A, centre of B";
    private const string SplitLeftOfBThenC = "split zone Left of B, split zone Left of C";
    private const string SplitTopOfBThenC = "split zone Top of B, split zone Top of C";
    private const string ProximityBandOfBThenC = "proximity band of B, proximity band of C";
    private const string HostEdgeTopAcrossAAndB = "host edge Top straddling A and B";
    private const string HostEdgeBottomAcrossAAndC = "host edge Bottom straddling A and C";

    private sealed record Setup(Point First, Point Second, DockTabGroupNode FirstGroup, DockTabGroupNode SecondGroup, Func<DockDropTarget, bool> IsFirst, Func<DockDropTarget, bool> IsSecond);

    private static Setup CreateSetup(DockDropTargetScene s, string name)
    {
        Rectangle b = s.Bounds(s.GroupB);
        Rectangle c = s.Bounds(s.GroupC);
        switch (name)
        {
            case CentreOfBThenC:
                return new Setup(s.Joystick(s.GroupB, DockZone.Center), s.Joystick(s.GroupC, DockZone.Center), s.GroupB, s.GroupC,
                    t => t.Zone == DockZone.Center && t.TabIndex == -1 && ReferenceEquals(t.TargetNode, s.GroupB),
                    t => t.Zone == DockZone.Center && t.TabIndex == -1 && ReferenceEquals(t.TargetNode, s.GroupC));
            case ReorderCentreOfAThenCentreOfB:
                return new Setup(s.Joystick(s.GroupA, DockZone.Center), s.Joystick(s.GroupB, DockZone.Center), s.GroupA, s.GroupB,
                    t => t.Zone == DockZone.Center && t.TabIndex >= 0 && ReferenceEquals(t.TargetNode, s.GroupA),
                    t => t.Zone == DockZone.Center && t.TabIndex == -1 && ReferenceEquals(t.TargetNode, s.GroupB));
            case SplitLeftOfBThenC:
                return new Setup(s.Joystick(s.GroupB, DockZone.Left), s.Joystick(s.GroupC, DockZone.Left), s.GroupB, s.GroupC,
                    t => t.Zone == DockZone.Left && !t.IsHostEdge && !t.IsSplitterDrop && ReferenceEquals(t.TargetNode, s.GroupB),
                    t => t.Zone == DockZone.Left && !t.IsHostEdge && !t.IsSplitterDrop && ReferenceEquals(t.TargetNode, s.GroupC));
            case SplitTopOfBThenC:
                return new Setup(s.Joystick(s.GroupB, DockZone.Top), s.Joystick(s.GroupC, DockZone.Top), s.GroupB, s.GroupC,
                    t => t.Zone == DockZone.Top && !t.IsHostEdge && !t.IsSplitterDrop && ReferenceEquals(t.TargetNode, s.GroupB),
                    t => t.Zone == DockZone.Top && !t.IsHostEdge && !t.IsSplitterDrop && ReferenceEquals(t.TargetNode, s.GroupC));
            case ProximityBandOfBThenC:
                return new Setup(new Point(b.X + 12, b.Y + 70), new Point(c.X + 12, c.Y + 70), s.GroupB, s.GroupC,
                    t => t.Zone == DockZone.Left && !t.IsSplitterDrop && ReferenceEquals(t.TargetNode, s.GroupB),
                    t => t.Zone == DockZone.Left && !t.IsSplitterDrop && ReferenceEquals(t.TargetNode, s.GroupC));
            case HostEdgeTopAcrossAAndB:
            {
                Point e = s.HostEdge(DockZone.Top);
                return new Setup(new Point(e.X - 12, e.Y), new Point(e.X + 12, e.Y), s.GroupA, s.GroupB,
                    t => t.Zone == DockZone.Top && t.IsHostEdge && t.TargetNode == null,
                    t => t.Zone == DockZone.Top && t.IsHostEdge && t.TargetNode == null);
            }
            case HostEdgeBottomAcrossAAndC:
            {
                Point e = s.HostEdge(DockZone.Bottom);
                return new Setup(new Point(e.X - 12, e.Y), new Point(e.X + 12, e.Y), s.GroupA, s.GroupC,
                    t => t.Zone == DockZone.Bottom && t.IsHostEdge && t.TargetNode == null,
                    t => t.Zone == DockZone.Bottom && t.IsHostEdge && t.TargetNode == null);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, null);
        }
    }

    [Theory]
    [InlineData(CentreOfBThenC, 0L, 0L)]
    [InlineData(ReorderCentreOfAThenCentreOfB, 0L, 0L)]
    [InlineData(SplitLeftOfBThenC, 0L, 0L)]
    [InlineData(SplitTopOfBThenC, 0L, 0L)]
    [InlineData(ProximityBandOfBThenC, 0L, 0L)]
    [InlineData(HostEdgeTopAcrossAAndB, 0L, 0L)]
    [InlineData(HostEdgeBottomAcrossAAndC, 0L, 0L)]
    public void TabDrag_ThatChangesTheHoveredGroupAtEachFrame_UpdateAndDraw_AllocateTheMeasuredRemainder(string name, long expectedUpdateTotal, long expectedDrawTotal)
    {
        DockDropTargetScene scene = DockDropTargetScene.Create();
        Setup setup = CreateSetup(scene, name);

        //  The two positions are in two different groups.
        Assert.NotSame(setup.FirstGroup, setup.SecondGroup);
        Assert.True(scene.Bounds(setup.FirstGroup).Contains(setup.First), $"{name}: {setup.First} is not inside the first group {scene.Bounds(setup.FirstGroup)}");
        Assert.True(scene.Bounds(setup.SecondGroup).Contains(setup.Second), $"{name}: {setup.Second} is not inside the second group {scene.Bounds(setup.SecondGroup)}");
        Assert.False(scene.Bounds(setup.SecondGroup).Contains(setup.First), $"{name}: {setup.First} is inside the second group too");
        Assert.False(scene.Bounds(setup.FirstGroup).Contains(setup.Second), $"{name}: {setup.Second} is inside the first group too");

        scene.PressAndStartTheDrag();

        void CheckTheTarget(Point at, Func<DockDropTarget, bool> isTheTarget, string which)
        {
            Assert.NotNull(scene.Host.CurrentDrag);
            Assert.True(scene.Host.CurrentDrag.HasExceededThreshold);
            Assert.NotNull(scene.Host.CurrentDropTarget);
            Assert.True(isTheTarget(scene.Host.CurrentDropTarget), $"{name}: the target at the {which} position {at} is not the one the case names: {DockDropTargetRecording.Values(scene.Host.CurrentDropTarget)}");
        }

        scene.Bench.Mouse = setup.First;
        scene.Bench.Frames(3);
        CheckTheTarget(setup.First, setup.IsFirst, "first");
        scene.Bench.Mouse = setup.Second;
        scene.Bench.Frames(3);
        CheckTheTarget(setup.Second, setup.IsSecond, "second");
        scene.Bench.Mouse = setup.First;
        scene.Bench.Frames(3);
        CheckTheTarget(setup.First, setup.IsFirst, "first");

        //  The frames that are measured alternate: the first position at the even frames, the second at the odd ones. The prepare step of a frame is the mouse move, measured on its own.
        FrameBytes bytes = FrameAllocationMeter.Measure(frame => scene.Bench.Mouse = (frame & 1) == 0 ? setup.First : setup.Second, scene.Bench.Update, scene.Bench.Draw);
        Output.WriteLine($"tab drag, {name}: Update {bytes.Update} B/frame, Draw {bytes.Draw} B/frame (totals {bytes.UpdateTotal} and {bytes.DrawTotal} B over {bytes.Iterations} frames; prepare {bytes.PrepareTotal} B)");

        //  After 1000 frames (an even count), the last mouse move was to the second position; the target is the second one's after the update that follows it.
        CheckTheTarget(setup.Second, setup.IsSecond, "second");
        scene.Bench.Left = ButtonState.Released;
        scene.Bench.Frames(2);

        Assert.True(bytes.UpdateTotal == expectedUpdateTotal && bytes.DrawTotal == expectedDrawTotal,
            $"{name}: Update {bytes.UpdateTotal} B and Draw {bytes.DrawTotal} B over {bytes.Iterations} frames, expected {expectedUpdateTotal} and {expectedDrawTotal}");
    }
}
