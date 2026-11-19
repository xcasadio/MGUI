using System.ComponentModel;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Docking;

/// <summary>
/// The drop target of <c>MGDockHost</c> is one instance written again at each computation (ADR-0036): no field written for a target survives into the next one, the
/// instance is the one that <c>GetDropTarget</c> returns, and <c>PropertyChanged</c> is raised when the target appears, disappears or changes a value, and only then.
/// </summary>
public class DockDropTargetReuseTests
{
    private static readonly Point OutsideTheHost = new(630, 350);

    private static DockTabGroupNode Group(DockDropTargetScene s, string name) => name switch
    {
        "A" => s.GroupA,
        "B" => s.GroupB,
        _ => s.GroupC
    };

    /// <summary>A splitter bar next to a group G on side S, then the zone S of the same group G, then the drop: the target of the zone is not a splitter drop, and the layout is
    /// the one that the code that made a new target at each computation gave (the frozen layouts of <see cref="DockDropTargetReplayTests"/>).</summary>
    [Theory]
    [InlineData(true, false, "B", DockZone.Left, "S(Horizontal,G[A2],S(Vertical,S(Horizontal,G[A1],G[B1]),G[C1])) floating=0")]
    [InlineData(true, true, "A", DockZone.Right, "S(Horizontal,S(Horizontal,G[A2],G[A1]),S(Vertical,G[B1],G[C1])) floating=0")]
    [InlineData(false, true, "B", DockZone.Bottom, "S(Horizontal,G[A2],S(Vertical,S(Vertical,G[B1],G[A1]),G[C1])) floating=0")]
    [InlineData(false, false, "C", DockZone.Top, "S(Horizontal,G[A2],S(Vertical,G[B1],S(Vertical,G[A1],G[C1]))) floating=0")]
    public void SplitterThenTheZoneOfTheSameGroup_Drop_HasNoSplitterFieldAndTheFrozenLayout(bool rootBar, bool firstHalf, string groupName, DockZone zone, string layout)
    {
        DockDropTargetScene scene = DockDropTargetScene.Create();
        DockTabGroupNode group = Group(scene, groupName);
        DockNode splitNode = rootBar ? scene.Host.LayoutModel.RootNode : ((DockSplitNode)scene.Host.LayoutModel.RootNode).SecondChild;
        scene.PressAndStartTheDrag();

        scene.Bench.Mouse = rootBar ? scene.RootBar(firstHalf) : scene.InnerBar(firstHalf);
        scene.Bench.Frame();
        DockDropTarget onTheBar = scene.Host.CurrentDropTarget;
        Assert.NotNull(onTheBar);
        Assert.True(onTheBar.IsSplitterDrop);
        Assert.Same(splitNode, onTheBar.SplitterNode);
        Assert.Equal(zone, onTheBar.Zone);
        Assert.Same(group, onTheBar.TargetNode);

        scene.Bench.Mouse = scene.Joystick(group, zone);
        scene.Bench.Frame();
        DockDropTarget onTheZone = scene.Host.CurrentDropTarget;
        Assert.NotNull(onTheZone);
        Assert.Equal(zone, onTheZone.Zone);
        Assert.Same(group, onTheZone.TargetNode);
        Assert.False(onTheZone.IsSplitterDrop);
        Assert.Null(onTheZone.SplitterNode);
        Assert.False(onTheZone.IsHostEdge);
        Assert.Equal(-1, onTheZone.TabIndex);

        scene.Bench.Left = ButtonState.Released;
        scene.Bench.Frames(2);
        Assert.Null(scene.Host.CurrentDropTarget);
        Assert.Equal(layout, scene.DescribeLayout());
    }

    /// <summary>The same, with the zone of the group reached by its position (the proximity band) and by <c>GetDropTarget</c>, whose targets come from the same zones.</summary>
    [Fact]
    public void SplitterThenTheSameZoneByProximityAndByPosition_HasNoSplitterField()
    {
        DockDropTargetScene scene = DockDropTargetScene.Create();
        scene.PressAndStartTheDrag();
        scene.Bench.Mouse = scene.InnerBar(false);
        scene.Bench.Frame();
        Assert.True(scene.Host.CurrentDropTarget.IsSplitterDrop);

        Rectangle c = scene.Bounds(scene.GroupC);
        scene.Bench.Mouse = new Point(c.X + 60, c.Y + 8);
        scene.Bench.Frame();
        Assert.Equal(DockZone.Top, scene.Host.CurrentDropTarget.Zone);
        Assert.False(scene.Host.CurrentDropTarget.IsSplitterDrop);
        Assert.Null(scene.Host.CurrentDropTarget.SplitterNode);

        //  The splitter again, then the same zone through GetDropTarget (the zones of the calculator are shared by both ways).
        scene.Bench.Mouse = scene.InnerBar(false);
        scene.Bench.Frame();
        Assert.True(scene.Host.CurrentDropTarget.IsSplitterDrop);
        DockDropTarget byPosition = scene.Host.GetDropTarget(new Point(c.X + 60, c.Y + 8));
        Assert.NotNull(byPosition);
        Assert.Equal(DockZone.Top, byPosition.Zone);
        Assert.False(byPosition.IsSplitterDrop);
        Assert.Null(byPosition.SplitterNode);
    }

    /// <summary>The index of a tab that a target computed never shows in the next one: not in the target of another group, not in a split zone, not in a target that
    /// <c>GetDropTarget</c> computes outside a drag.</summary>
    [Fact]
    public void TabIndexOfATarget_NeverAppearsInTheNextOne()
    {
        DockDropTargetScene scene = DockDropTargetScene.Create();
        MGDockHost host = scene.Host;
        scene.PressAndStartTheDrag();

        scene.Bench.Mouse = scene.Joystick(scene.GroupA, DockZone.Center);
        scene.Bench.Frame();
        Assert.True(host.CurrentDropTarget.TabIndex >= 0);
        Assert.Same(scene.GroupA, host.CurrentDropTarget.TargetNode);

        scene.Bench.Mouse = scene.Joystick(scene.GroupC, DockZone.Center);
        scene.Bench.Frame();
        Assert.Same(scene.GroupC, host.CurrentDropTarget.TargetNode);
        Assert.Equal(-1, host.CurrentDropTarget.TabIndex);

        scene.Bench.Mouse = scene.Joystick(scene.GroupA, DockZone.Center);
        scene.Bench.Frame();
        Assert.True(host.CurrentDropTarget.TabIndex >= 0);
        scene.Bench.Mouse = scene.Joystick(scene.GroupB, DockZone.Left);
        scene.Bench.Frame();
        Assert.Equal(DockZone.Left, host.CurrentDropTarget.Zone);
        Assert.Equal(-1, host.CurrentDropTarget.TabIndex);

        //  The reorder target of the tab headers, then a split zone.
        DockDropTarget reorder = host.GetDropTarget(scene.TabA2);
        Assert.True(reorder.TabIndex >= 0);
        Assert.Equal(DockZone.Center, reorder.Zone);
        scene.Bench.Mouse = scene.Joystick(scene.GroupC, DockZone.Right);
        scene.Bench.Frame();
        Assert.Equal(DockZone.Right, host.CurrentDropTarget.Zone);
        Assert.Equal(-1, host.CurrentDropTarget.TabIndex);

        //  A target of the centre of the group of the dragged tab by position (the index is written after the zone is computed), then the drag ends and GetDropTarget
        //  finds the centre of another group outside a drag: no index.
        DockDropTarget centreOfA = host.GetDropTarget(scene.Joystick(scene.GroupA, DockZone.Center));
        Assert.True(centreOfA.TabIndex >= 0);
        host.CancelDrag();
        Assert.Null(host.CurrentDropTarget);
        DockDropTarget centreOfC = host.GetDropTarget(scene.Joystick(scene.GroupC, DockZone.Center));
        Assert.NotNull(centreOfC);
        Assert.Same(scene.GroupC, centreOfC.TargetNode);
        Assert.Equal(DockZone.Center, centreOfC.Zone);
        Assert.Equal(-1, centreOfC.TabIndex);
        DockDropTarget centreOfAAgain = host.GetDropTarget(scene.Joystick(scene.GroupA, DockZone.Center));
        Assert.Same(scene.GroupA, centreOfAAgain.TargetNode);
        Assert.Equal(-1, centreOfAAgain.TabIndex);
    }

    /// <summary>The target is one instance, whatever its kind, and <c>GetDropTarget</c> returns it.</summary>
    [Fact]
    public void CurrentDropTarget_IsOneInstance_AndGetDropTargetReturnsIt()
    {
        DockDropTargetScene scene = DockDropTargetScene.Create();
        MGDockHost host = scene.Host;
        Assert.Null(host.CurrentDropTarget);
        scene.PressAndStartTheDrag();

        List<Point> positions = new()
        {
            scene.Joystick(scene.GroupB, DockZone.Center),
            scene.Joystick(scene.GroupB, DockZone.Left),
            scene.HostEdge(DockZone.Right),
            scene.RootBar(false),
            scene.Joystick(scene.GroupA, DockZone.Center),
            new Point(scene.Bounds(scene.GroupB).X + 12, scene.Bounds(scene.GroupB).Y + 70)
        };
        DockDropTarget first = null;
        foreach (Point position in positions)
        {
            scene.Bench.Mouse = position;
            scene.Bench.Frame();
            Assert.NotNull(host.CurrentDropTarget);
            first ??= host.CurrentDropTarget;
            Assert.Same(first, host.CurrentDropTarget);
        }

        scene.Bench.Mouse = OutsideTheHost;
        scene.Bench.Frame();
        Assert.Null(host.CurrentDropTarget);
        scene.Bench.Mouse = scene.Joystick(scene.GroupC, DockZone.Center);
        scene.Bench.Frame();
        Assert.Same(first, host.CurrentDropTarget);

        DockDropTarget returned = host.GetDropTarget(scene.Joystick(scene.GroupB, DockZone.Center));
        Assert.Same(first, returned);
        Assert.Same(returned, host.CurrentDropTarget);
        Assert.Same(scene.GroupB, returned.TargetNode);
        Assert.Null(host.GetDropTarget(OutsideTheHost));
        Assert.Null(host.CurrentDropTarget);
        DockDropTarget reorder = host.GetDropTarget(scene.TabA2);
        Assert.Same(first, reorder);
    }

    private sealed class Counter
    {
        public int Count;

        public Counter(MGDockHost host)
        {
            host.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MGDockHost.CurrentDropTarget))
            {
                Count++;
            }
        }
    }

    /// <summary>PropertyChanged for CurrentDropTarget: raised when the target appears, when a value changes, when it disappears; not when a computation finds the same values.</summary>
    [Fact]
    public void CurrentDropTarget_RaisesPropertyChanged_OnAppearanceValueChangeAndDisappearance_NotOnAnIdenticalComputation()
    {
        DockDropTargetScene scene = DockDropTargetScene.Create();
        MGDockHost host = scene.Host;
        Counter counter = new(host);
        scene.PressAndStartTheDrag();
        Assert.Equal(0, counter.Count);

        //  Appearance.
        Point centre = scene.Joystick(scene.GroupB, DockZone.Center);
        scene.Bench.Mouse = centre;
        scene.Bench.Frame();
        Assert.Equal(1, counter.Count);

        //  An identical computation: the mouse moves 10 pixels inside the square of the same joystick zone, the target has the same eight values.
        string before = DockDropTargetRecording.Values(host.CurrentDropTarget);
        scene.Bench.Mouse = new Point(centre.X + 8, centre.Y + 6);
        scene.Bench.Frame();
        scene.Bench.Mouse = centre;
        scene.Bench.Frame();
        Assert.Equal(before, DockDropTargetRecording.Values(host.CurrentDropTarget));
        Assert.Equal(1, counter.Count);

        //  Holding the mouse still computes nothing.
        scene.Bench.Frames(3);
        Assert.Equal(1, counter.Count);

        //  A change of value: another zone, another group, a host edge, a splitter bar.
        scene.Bench.Mouse = scene.Joystick(scene.GroupB, DockZone.Left);
        scene.Bench.Frame();
        Assert.Equal(2, counter.Count);
        scene.Bench.Mouse = scene.Joystick(scene.GroupC, DockZone.Left);
        scene.Bench.Frame();
        Assert.Equal(3, counter.Count);
        scene.Bench.Mouse = scene.HostEdge(DockZone.Right);
        scene.Bench.Frame();
        Assert.Equal(4, counter.Count);
        scene.Bench.Mouse = scene.RootBar(false);
        scene.Bench.Frame();
        Assert.Equal(5, counter.Count);
        //  The same group and zone as a split zone (B left): only IsSplitterDrop and SplitterNode differ from the target of the joystick.
        scene.Bench.Mouse = scene.Joystick(scene.GroupB, DockZone.Left);
        scene.Bench.Frame();
        Assert.Equal(6, counter.Count);
        Assert.False(host.CurrentDropTarget.IsSplitterDrop);

        //  Disappearance, and no notification for no target again.
        scene.Bench.Mouse = OutsideTheHost;
        scene.Bench.Frame();
        Assert.Null(host.CurrentDropTarget);
        Assert.Equal(7, counter.Count);
        scene.Bench.Mouse = new Point(OutsideTheHost.X - 8, OutsideTheHost.Y - 6);
        scene.Bench.Frame();
        Assert.Null(host.CurrentDropTarget);
        Assert.Equal(7, counter.Count);

        //  Appearance again with values equal to the ones the kept instance still holds from before it disappeared.
        scene.Bench.Mouse = scene.Joystick(scene.GroupB, DockZone.Left);
        scene.Bench.Frame();
        Assert.Equal(8, counter.Count);

        //  GetDropTarget: no target outside the drag area, then the same target twice.
        int count = counter.Count;
        host.GetDropTarget(scene.Joystick(scene.GroupB, DockZone.Center));
        Assert.Equal(count + 1, counter.Count);
        host.GetDropTarget(scene.Joystick(scene.GroupB, DockZone.Center));
        Assert.Equal(count + 1, counter.Count);
        host.GetDropTarget(OutsideTheHost);
        Assert.Equal(count + 2, counter.Count);

        //  The end of the drag: a target that is already null raises nothing.
        host.CancelDrag();
        Assert.Equal(count + 2, counter.Count);
    }

    /// <summary>The drop applies what the target was when the mouse was released, and the target is gone afterwards (the host sets it to none; the kept instance is not what is read next).</summary>
    [Fact]
    public void Drop_ClearsTheCurrentDropTarget_AfterApplyingIt()
    {
        DockDropTargetScene scene = DockDropTargetScene.Create();
        MGDockHost host = scene.Host;
        Counter counter = new(host);
        scene.PressAndStartTheDrag();
        scene.Bench.Mouse = scene.Joystick(scene.GroupC, DockZone.Center);
        scene.Bench.Frame();
        DockDropTarget kept = host.CurrentDropTarget;
        Assert.Equal(1, counter.Count);

        scene.Bench.Left = ButtonState.Released;
        scene.Bench.Frames(2);
        Assert.Null(host.CurrentDropTarget);
        Assert.Equal(2, counter.Count);
        Assert.Equal("S(Horizontal,G[A2],S(Vertical,G[B1],G[C1,A1])) floating=0", scene.DescribeLayout());

        //  A caller that kept the reference reads the values of the last target: the host does not clear them.
        Assert.Equal(DockZone.Center, kept.Zone);
        Assert.Same(scene.GroupC, kept.TargetNode);
    }

    /// <summary>The kept lists of the calculator hold the same targets from one call to the next, and each call fills all eight fields of every target it gives.</summary>
    [Fact]
    public void CalculatorLists_AreFilledAgain_AndGiveTheValuesOfThePublicMethods()
    {
        DockDropTargetScene scene = DockDropTargetScene.Create();
        Rectangle host = scene.Host.LayoutBounds;

        List<DockDropTarget> edges = new();
        DockDropCalculator.CalculateHostEdgeZonesInto(edges, host);
        Assert.Equal(4, edges.Count);
        DockDropTarget[] edgeSlots = edges.ToArray();
        edgeSlots[1].TabIndex = 7;
        edgeSlots[1].IsSplitterDrop = true;
        edgeSlots[1].SplitterNode = (DockSplitNode)scene.Host.LayoutModel.RootNode;
        edgeSlots[1].TargetNode = scene.GroupA;
        DockDropCalculator.CalculateHostEdgeZonesInto(edges, host);
        Assert.Equal(4, edges.Count);
        for (int i = 0; i < 4; i++)
        {
            Assert.Same(edgeSlots[i], edges[i]);
        }

        List<DockDropTarget> expectedEdges = DockDropCalculator.CalculateHostEdgeZones(host);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(DockDropTargetRecording.Values(expectedEdges[i]), DockDropTargetRecording.Values(edges[i]));
        }

        DockDropCalculator.CalculateHostEdgeZonesInto(edges, Rectangle.Empty);
        Assert.Empty(edges);

        List<DockDropTarget> zones = new();
        Rectangle bounds = scene.Bounds(scene.GroupB);
        DockDropCalculator.CalculateDropZonesInto(zones, scene.VisualGroup(scene.GroupB), bounds);
        Assert.Equal(5, zones.Count);
        DockDropTarget[] zoneSlots = zones.ToArray();
        foreach (DockDropTarget slot in zoneSlots)
        {
            slot.TabIndex = 3;
            slot.IsHostEdge = true;
            slot.IsSplitterDrop = true;
            slot.SplitterNode = (DockSplitNode)scene.Host.LayoutModel.RootNode;
        }

        DockDropCalculator.CalculateDropZonesInto(zones, scene.VisualGroup(scene.GroupC), scene.Bounds(scene.GroupC));
        Assert.Equal(5, zones.Count);
        List<DockDropTarget> expectedZones = DockDropCalculator.CalculateDropZones(scene.VisualGroup(scene.GroupC), scene.Bounds(scene.GroupC));
        for (int i = 0; i < 5; i++)
        {
            Assert.Same(zoneSlots[i], zones[i]);
            Assert.Equal(DockDropTargetRecording.Values(expectedZones[i]), DockDropTargetRecording.Values(zones[i]));
            Assert.Equal(-1, zones[i].TabIndex);
            Assert.False(zones[i].IsHostEdge);
            Assert.False(zones[i].IsSplitterDrop);
            Assert.Null(zones[i].SplitterNode);
        }

        DockDropCalculator.CalculateDropZonesInto(zones, scene.VisualGroup(scene.GroupC), Rectangle.Empty);
        Assert.Empty(zones);
    }
}
