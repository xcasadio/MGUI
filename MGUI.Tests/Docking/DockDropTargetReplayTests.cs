using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Docking;

/// <summary>
/// The scripted drags of the drop target of <c>MGDockHost</c> (ADR-0036), recorded frame by frame as the eight fields of <c>CurrentDropTarget</c> (copied as values at
/// the frame, see <see cref="DockDropTargetRecording"/>) and as the layout after each drop. The expectation is the recording of the code that made a new
/// <see cref="DockDropTarget"/> at each computation: the host that keeps one instance must say the same thing at every frame.
/// </summary>
public class DockDropTargetReplayTests
{
    private static readonly Point OutsideTheHost = new(630, 350);

    private static DockDropTargetRecording Start(out DockDropTargetScene scene)
    {
        scene = DockDropTargetScene.Create();
        return new DockDropTargetRecording(scene);
    }

    /// <summary>Every kind of target: the centre of another group, the four split zones of a group, the four host edges, the two splitter bars on both halves, the tab headers
    /// of the group of the dragged tab, the centre of that group, the zones next to the edge of a group, no target. Then a splitter bar next to a group G on side S, then
    /// the zone S of the same group G, then the drop.</summary>
    private static void Tour(DockDropTargetRecording r, DockDropTargetScene s)
    {
        r.PressAndStartTheDrag();
        r.Note("centre of another group");
        r.Move("B centre", s.Joystick(s.GroupB, DockZone.Center));
        r.Hold("B centre held");
        r.Note("four split zones");
        foreach (DockZone zone in new[] { DockZone.Left, DockZone.Right, DockZone.Top, DockZone.Bottom })
        {
            r.Move($"B {zone}", s.Joystick(s.GroupB, zone));
        }

        r.Note("four host edges");
        foreach (DockZone zone in new[] { DockZone.Left, DockZone.Right, DockZone.Top, DockZone.Bottom })
        {
            r.Move($"host {zone}", s.HostEdge(zone));
        }

        r.Note("splitter bars, both halves");
        //  The mouse leaves the bar between two of them: a move of less than 5 pixels does not compute the target again.
        Point away = s.Joystick(s.GroupB, DockZone.Center);
        r.Move("root bar first half", s.RootBar(true));
        r.Move("away", away);
        r.Move("root bar second half", s.RootBar(false));
        r.Move("away", away);
        r.Move("inner bar first half", s.InnerBar(true));
        r.Move("away", away);
        r.Move("inner bar second half", s.InnerBar(false));
        r.Note("reorder: tab headers of the group of the dragged tab, then its centre");
        r.Move("A tab headers", s.TabA2);
        r.Public("A tab headers public", s.TabA2);
        r.Move("A centre", s.Joystick(s.GroupA, DockZone.Center));
        r.Hold("A centre held");
        r.Note("centre without index right after a centre with a computed index");
        r.Move("C centre", s.Joystick(s.GroupC, DockZone.Center));
        r.Note("proximity bands");
        Rectangle b = s.Bounds(s.GroupB);
        r.Move("B proximity left", new Point(b.X + 12, b.Y + 70));
        r.Move("B proximity right", new Point(b.Right - 12, b.Y + 70));
        r.Move("B proximity top", new Point(b.X + 70, b.Y + 8));
        r.Move("B proximity bottom", new Point(b.X + 70, b.Bottom - 8));
        r.Note("no target");
        r.Move("outside", OutsideTheHost);
        r.Hold("outside held");
        r.Note("splitter next to B on the side bottom, then the zone bottom of B, then the drop");
        r.Move("inner bar first half again", s.InnerBar(true));
        r.Move("B bottom again", s.Joystick(s.GroupB, DockZone.Bottom));
        r.Release("tour");
    }

    private static void SplitterThenTheZoneOfTheSameGroup(DockDropTargetRecording r, DockDropTargetScene s, bool rootBar, bool firstHalf, DockTabGroupNode group, DockZone zone)
    {
        r.PressAndStartTheDrag();
        r.Move("bar", rootBar ? s.RootBar(firstHalf) : s.InnerBar(firstHalf));
        r.Hold("bar held");
        r.Public("the same zone by position", InZone(s.Bounds(group), zone));
        r.Move($"{zone} of the same group", s.Joystick(group, zone));
        r.Hold("zone held");
        r.Release($"{zone}");
    }

    /// <summary>A position in the band of a split zone of a group, away from the corners and from the centre.</summary>
    private static Point InZone(Rectangle b, DockZone zone) => zone switch
    {
        DockZone.Left => new Point(b.X + 8, b.Center.Y + 40),
        DockZone.Right => new Point(b.Right - 8, b.Center.Y + 40),
        DockZone.Top => new Point(b.Center.X + 60, b.Y + 8),
        _ => new Point(b.Center.X + 60, b.Bottom - 8)
    };

    private static List<string> Run()
    {
        List<string> lines = new();

        {
            DockDropTargetRecording r = Start(out DockDropTargetScene s);
            r.Note("script: tour");
            Tour(r, s);
            lines.AddRange(r.Result);
        }

        foreach ((string name, bool rootBar, bool firstHalf, Func<DockDropTargetScene, DockTabGroupNode> group, DockZone zone) in new (string, bool, bool, Func<DockDropTargetScene, DockTabGroupNode>, DockZone)[]
        {
            ("root bar second half then B left", true, false, s => s.GroupB, DockZone.Left),
            ("root bar first half then A right", true, true, s => s.GroupA, DockZone.Right),
            ("inner bar first half then B bottom", false, true, s => s.GroupB, DockZone.Bottom),
            ("inner bar second half then C top", false, false, s => s.GroupC, DockZone.Top)
        })
        {
            DockDropTargetRecording r = Start(out DockDropTargetScene s);
            r.Note("script: " + name);
            SplitterThenTheZoneOfTheSameGroup(r, s, rootBar, firstHalf, group(s), zone);
            lines.AddRange(r.Result);
        }

        {
            DockDropTargetRecording r = Start(out DockDropTargetScene s);
            r.Note("script: splitter next to C, then the proximity band top of C");
            r.PressAndStartTheDrag();
            r.Move("inner bar second half", s.InnerBar(false));
            Rectangle c = s.Bounds(s.GroupC);
            r.Move("C proximity top", new Point(c.X + 60, c.Y + 8));
            r.Hold("C proximity top held");
            r.Release("proximity");
            lines.AddRange(r.Result);
        }

        {
            DockDropTargetRecording r = Start(out DockDropTargetScene s);
            r.Note("script: public GetDropTarget outside a drag");
            r.Public("before any drag, B centre", s.Joystick(s.GroupB, DockZone.Center));
            r.Public("before any drag, A tab headers", s.TabA2);
            r.Public("before any drag, A left", new Point(s.Bounds(s.GroupA).X + 5, s.Bounds(s.GroupA).Center.Y));
            r.Public("before any drag, outside", OutsideTheHost);
            lines.AddRange(r.Result);
        }

        {
            DockDropTargetRecording r = Start(out DockDropTargetScene s);
            r.Note("script: a computed tab index, then the centre of another group, then the drop");
            r.PressAndStartTheDrag();
            r.Move("A centre", s.Joystick(s.GroupA, DockZone.Center));
            r.Public("A tab headers public", s.TabA2);
            r.Public("A centre by position, in the drag", s.Joystick(s.GroupA, DockZone.Center));
            r.Move("C centre", s.Joystick(s.GroupC, DockZone.Center));
            r.Hold("C centre held");
            r.Release("tab index then no index");
            r.Public("after the drop, C centre", s.Joystick(s.GroupC, DockZone.Center));
            r.Public("after the drop, B centre", s.Joystick(s.GroupB, DockZone.Center));
            lines.AddRange(r.Result);
        }

        {
            DockDropTargetRecording r = Start(out DockDropTargetScene s);
            r.Note("script: reorder in the group of the dragged tab by its centre");
            r.PressAndStartTheDrag();
            r.Move("A centre", s.Joystick(s.GroupA, DockZone.Center));
            r.Release("reorder");
            lines.AddRange(r.Result);
        }

        {
            DockDropTargetRecording r = Start(out DockDropTargetScene s);
            r.Note("script: the target of the tab headers, dropped as it is");
            r.PressAndStartTheDrag();
            r.Move("A tab headers", s.TabA2);
            r.Public("A tab headers public", s.TabA2);
            r.Hold("held");
            r.Release("headers");
            lines.AddRange(r.Result);
        }

        {
            DockDropTargetRecording r = Start(out DockDropTargetScene s);
            r.Note("script: the target of the tab headers, on the empty part of the headers, dropped as it is");
            r.PressAndStartTheDrag();
            Point emptyHeaders = new(s.Bounds(s.GroupA).Right - 20, s.TabA2.Y);
            r.Move("A empty headers", emptyHeaders);
            r.Public("A empty headers public", emptyHeaders);
            r.Hold("held");
            r.Release("empty headers");
            lines.AddRange(r.Result);
        }

        foreach (DockZone zone in new[] { DockZone.Left, DockZone.Right, DockZone.Top, DockZone.Bottom })
        {
            DockDropTargetRecording r = Start(out DockDropTargetScene s);
            r.Note($"script: host edge {zone}, dropped");
            r.PressAndStartTheDrag();
            r.Move($"host {zone}", s.HostEdge(zone));
            r.Release($"host {zone}");
            lines.AddRange(r.Result);
        }

        foreach (DockZone zone in new[] { DockZone.Left, DockZone.Right, DockZone.Top, DockZone.Bottom })
        {
            DockDropTargetRecording r = Start(out DockDropTargetScene s);
            r.Note($"script: split zone {zone} of C, dropped");
            r.PressAndStartTheDrag();
            r.Move($"C {zone}", s.Joystick(s.GroupC, zone));
            r.Release($"C {zone}");
            lines.AddRange(r.Result);
        }

        {
            DockDropTargetRecording r = Start(out DockDropTargetScene s);
            r.Note("script: released outside the host with no target");
            r.PressAndStartTheDrag();
            r.Move("outside", OutsideTheHost);
            r.Release("outside");
            lines.AddRange(r.Result);
        }

        {
            DockDropTargetRecording r = Start(out DockDropTargetScene s);
            r.Note("script: cancelled drag");
            r.PressAndStartTheDrag();
            r.Move("B centre", s.Joystick(s.GroupB, DockZone.Center));
            s.Host.CancelDrag();
            r.Note("cancelled: " + DockDropTargetRecording.Values(s.Host.CurrentDropTarget));
            r.Hold("after the cancel");
            lines.AddRange(r.Result);
        }

        return lines;
    }

    [Fact]
    public void ScriptedDrags_RecordTheEightFieldsFrameByFrameAndTheLayoutAfterEachDrop()
        => FrozenJournal.Assert("dock drop target replay", 230, "DD1696A26133C00F", Run());
}
