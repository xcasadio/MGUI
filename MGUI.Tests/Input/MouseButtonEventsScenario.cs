using MGUI.Shared.Input;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MGUI.Tests.Input;

/// <summary>
/// A scripted scenario of the mouse button events, run frame by frame on one <see cref="InputTracker"/> and six manual <see cref="MouseHandler"/>s, that writes one journal:
/// for each frame the state of the mouse, the events that the tracker holds for the frame, then every event that each handler received, with its values written at the time of
/// the event (never read again from a kept reference). Its journal is frozen in <see cref="MouseButtonEventsGolden"/> (taken from the code before the mouse button events were reused:
/// a new object for each press, release, click, drag start and drag end, and for each click sequence). A difference in any frame is a change of behaviour.
/// <para/>
/// The handlers: <c>E</c> (the early handler of the parent host, updated before its children: its host only receives mouse input, and marks the events handled, while it is enabled), <c>X</c> and <c>Y</c> (two
/// children, which start a drag at the first move after the press for X and at the press for Y), <c>W</c> (a child that always handles the events it receives, and that reads both drag
/// start conditions), <c>P</c> (the late handler of the parent host, which also receives what a descendant handled) and <c>V</c> (the late handler of the parent host that receives everything).
/// The script covers: press, release and click of each button, double, triple and fourth clicks, a click of another button between two clicks of a sequence, a press while another button
/// is held, drags by the three drag start conditions with the dragged frames and the drag end, a release outside of the element, a release that is not a click, a click handled by
/// the parent before the child, a handler that always handles, and the double click of the same button on an element and then on another element.
/// </summary>
internal static class MouseButtonEventsScenario
{
    private static readonly string[] HandlerNames = { "E", "X", "Y", "W", "P", "V" };

    /// <summary>The host of the early handler: it covers the whole parent, and only receives mouse input while it is enabled (an early handler that always received it would adopt every click sequence before its children).</summary>
    private sealed class EarlyHost : IMouseHandlerHost
    {
        public bool Enabled { get; set; }
        public bool CanReceiveMouseInput() => Enabled;
        public bool IsInside(Vector2 Position) => new Rectangle(0, 0, 400, 300).Contains(Position);
        public Vector2 GetOffset() => Vector2.Zero;
    }

    public static List<string> Run()
    {
        List<string> journal = new();
        InputTracker tracker = new();
        MouseTracker mouse = tracker.Mouse;

        MouseHandlerHost hostParent = new(() => new Rectangle(0, 0, 400, 300));
        MouseHandlerHost hostX = new(() => new Rectangle(20, 20, 100, 80)) { MouseInputParent = hostParent };
        MouseHandlerHost hostY = new(() => new Rectangle(200, 20, 100, 80)) { MouseInputParent = hostParent };
        MouseHandlerHost hostW = new(() => new Rectangle(20, 150, 100, 60)) { MouseInputParent = hostParent };
        EarlyHost hostEarly = new();
        Dictionary<IMouseHandlerHost, string> hostNames = new(ReferenceEqualityComparer.Instance)
        {
            { hostParent, "Parent" }, { hostEarly, "Early" }, { hostX, "X" }, { hostY, "Y" }, { hostW, "W" },
        };
        string Who(IMouseHandlerHost host) => host == null ? "-" : hostNames[host];

        MouseHandler hEarly = mouse.CreateHandler(hostEarly, (double?)null, HandledInputPolicy.Self);
        MouseHandler hX = mouse.CreateHandler(hostX, (double?)null, HandledInputPolicy.Self);
        MouseHandler hY = mouse.CreateHandler(hostY, (double?)null, HandledInputPolicy.Self);
        hY.DragStartCondition = DragStartCondition.MousePressed;
        MouseHandler hW = mouse.CreateHandler(hostW, (double?)null, HandledInputPolicy.Self, AlwaysHandlesEvents: true);
        hW.DragStartCondition = DragStartCondition.Both;
        MouseHandler hLate = mouse.CreateHandler(hostParent, (double?)null, HandledInputPolicy.Descendant | HandledInputPolicy.Self);
        MouseHandler hAny = mouse.CreateHandler(hostParent, (double?)null, HandledInputPolicy.Any);
        MouseHandler[] handlers = { hEarly, hX, hY, hW, hLate, hAny };

        bool xHandlesDrag = false;
        bool xHandlesClicks = false;

        string P(Point p) => $"({p.X},{p.Y})";
        string Ms(TimeSpan t) => t.TotalMilliseconds.ToString("R", CultureInfo.InvariantCulture);
        string Handled(HandledByEventArgs<IMouseHandlerHost> e) => e.IsHandled ? "by " + Who(e.HandledBy) : "free";

        void Behave(string name, string evt, HandledByEventArgs<IMouseHandlerHost> e)
        {
            if (name == "E" && hostEarly.Enabled && (evt is "PressedInside" or "ReleasedInside" or "ClickedInside" or "DragStart"))
            {
                e.SetHandledBy(hostEarly, false);
            }

            if (name == "X" && xHandlesDrag && evt == "DragStart")
            {
                e.SetHandledBy(hostX, false);
            }

            if (name == "X" && xHandlesClicks && evt == "LMBClickedInside")
            {
                e.SetHandledBy(hostX, false);
            }
        }

        void Pressed(string name, string evt, BaseMousePressedEventArgs e)
        {
            journal.Add($"  {name}.{evt} {e.Button} at {P(e.Position)} pressedAt={Ms(e.PressedAt)} {Handled(e)}");
            Behave(name, evt, e);
        }

        void Released(string name, string evt, BaseMouseReleasedEventArgs e)
        {
            journal.Add($"  {name}.{evt} {e.Button} at {P(e.Position)} releasedAt={Ms(e.ReleasedAt)} held={Ms(e.HeldDuration)} pressedAt={Ms(e.PressedArgs.PressedAt)} at {P(e.PressedArgs.Position)} " +
                        $"{Handled(e)} press {Handled(e.PressedArgs)}");
            Behave(name, evt, e);
        }

        void Clicked(string name, string evt, BaseMouseClickedEventArgs e)
        {
            journal.Add($"  {name}.{evt} {e.Button} at {P(e.Position)} count={e.ClickCount} double={e.IsDoubleClick} multi={e.IsMultiClick} seq={e.MultiClickSequenceId} " +
                        $"seqButton={e.Sequence.Button} seqStart={Ms(e.Sequence.StartedAt)} seqCount={e.Sequence.ClickCount} releasedAt={Ms(e.ReleasedArgs.ReleasedAt)} pressedAt={Ms(e.PressedArgs.PressedAt)} " +
                        $"{Handled(e)} release {Handled(e.ReleasedArgs)} press {Handled(e.PressedArgs)}");
            Behave(name, evt, e);
        }

        void DragStarted(string name, string evt, BaseMouseDragStartEventArgs e)
        {
            journal.Add($"  {name}.{evt} {e.Button} at {P(e.Position)} startedAt={Ms(e.StartedAt)} condition={e.Condition} {Handled(e)}");
            Behave(name, evt, e);
        }

        void Dragged(string name, BaseMouseDraggedEventArgs e)
            => journal.Add($"  {name}.Dragged {e.Button} at {P(e.Position)} start={P(e.StartPosition)} delta={P(e.PositionDelta)} distance2={e.GetTotalDistanceMoved(true).ToString("R", CultureInfo.InvariantCulture)} " +
                           $"timestamp={Ms(e.Timestamp)} start {e.DragStartArgs.Condition} {Handled(e.DragStartArgs)}");

        void DragEnded(string name, BaseMouseDragEndEventArgs e)
            => journal.Add($"  {name}.DragEnd {e.Button} start={P(e.StartPosition)} end={P(e.EndPosition)} delta={P(e.PositionDelta)} timestamp={Ms(e.Timestamp)} start {e.DragStartArgs.Condition} {Handled(e.DragStartArgs)}");

        void Hook(string n, MouseHandler h, bool insideOnly = false)
        {
            h.PressedInside += (_, e) => Pressed(n, "PressedInside", e);
            h.LMBPressedInside += (_, e) => Pressed(n, "LMBPressedInside", e);
            h.MMBPressedInside += (_, e) => Pressed(n, "MMBPressedInside", e);
            h.RMBPressedInside += (_, e) => Pressed(n, "RMBPressedInside", e);
            if (!insideOnly) { h.PressedOutside += (_, e) => Pressed(n, "PressedOutside", e); }
            h.ReleasedInside += (_, e) => Released(n, "ReleasedInside", e);
            h.LMBReleasedInside += (_, e) => Released(n, "LMBReleasedInside", e);
            h.MMBReleasedInside += (_, e) => Released(n, "MMBReleasedInside", e);
            h.RMBReleasedInside += (_, e) => Released(n, "RMBReleasedInside", e);
            if (!insideOnly) { h.ReleasedOutside += (_, e) => Released(n, "ReleasedOutside", e); }
            h.ClickedInside += (_, e) => Clicked(n, "ClickedInside", e);
            h.LMBClickedInside += (_, e) => Clicked(n, "LMBClickedInside", e);
            h.MMBClickedInside += (_, e) => Clicked(n, "MMBClickedInside", e);
            h.RMBClickedInside += (_, e) => Clicked(n, "RMBClickedInside", e);
            if (!insideOnly) { h.ClickedOutside += (_, e) => Clicked(n, "ClickedOutside", e); }
            h.DoubleClickedInside += (_, e) => Clicked(n, "DoubleClickedInside", e);
            h.LMBDoubleClickedInside += (_, e) => Clicked(n, "LMBDoubleClickedInside", e);
            h.MMBDoubleClickedInside += (_, e) => Clicked(n, "MMBDoubleClickedInside", e);
            h.RMBDoubleClickedInside += (_, e) => Clicked(n, "RMBDoubleClickedInside", e);
            if (!insideOnly) { h.DoubleClickedOutside += (_, e) => Clicked(n, "DoubleClickedOutside", e); }
            h.DragStart += (_, e) => DragStarted(n, "DragStart", e);
            if (!insideOnly) { h.DragStartOutside += (_, e) => DragStarted(n, "DragStartOutside", e); }
            h.Dragged += (_, e) => Dragged(n, e);
            h.DragEnd += (_, e) => DragEnded(n, e);
        }

        for (int i = 0; i < handlers.Length; i++)
        {
            Hook(HandlerNames[i], handlers[i], i == 3);
        }

        int t = 0;
        int frame = 0;
        Point position = new(5, 5);
        bool left = false, middle = false, right = false;

        //  The release time of the latest click of each button, written when the click is current: the release object that a kept click designates is refilled by the next release.
        TimeSpan[] lastClickReleasedAt = new TimeSpan[3];

        void LogTracker()
        {
            StringBuilder state = new();
            foreach (MouseButton button in MouseTracker.MouseButtons)
            {
                BaseMousePressedEventArgs pressed = mouse.CurrentButtonPressedEvents[button];
                if (pressed != null) { state.Append($" P:{button}{P(pressed.Position)}@{Ms(pressed.PressedAt)}"); }
                BaseMouseReleasedEventArgs released = mouse.CurrentButtonReleasedEvents[button];
                if (released != null) { state.Append($" R:{button}{P(released.Position)}@{Ms(released.ReleasedAt)}"); }
                BaseMouseClickedEventArgs clicked = mouse.CurrentButtonClickedEvents[button];
                if (clicked != null) { lastClickReleasedAt[(int)button] = clicked.ReleasedArgs.ReleasedAt; state.Append($" C:{button}{P(clicked.Position)} n={clicked.ClickCount} seq={clicked.MultiClickSequenceId}"); }
                BaseMouseClickedEventArgs doubleClicked = mouse.CurrentButtonDoubleClickedEvents[button];
                if (doubleClicked != null) { state.Append($" DC:{button} n={doubleClicked.ClickCount} seq={doubleClicked.MultiClickSequenceId}"); }
                foreach (DragStartCondition condition in MouseHandler.DragStartConditions)
                {
                    BaseMouseDragStartEventArgs dragStart = mouse.CurrentDragStartEvents[condition][button];
                    if (dragStart != null) { state.Append($" DS:{condition}{button}{P(dragStart.Position)}@{Ms(dragStart.StartedAt)}"); }
                    BaseMouseDraggedEventArgs dragged = mouse.CurrentDraggedEvents[condition][button];
                    if (dragged != null) { state.Append($" DG:{condition}{button}{P(dragged.Position)}@{Ms(dragged.Timestamp)}"); }
                    BaseMouseDragEndEventArgs dragEnd = mouse.CurrentDragEndEvents[condition][button];
                    if (dragEnd != null) { state.Append($" DE:{condition}{button}{P(dragEnd.EndPosition)}@{Ms(dragEnd.Timestamp)}"); }
                }
            }

            StringBuilder recent = new();
            foreach (MouseButton button in MouseTracker.MouseButtons)
            {
                BaseMousePressedEventArgs held = mouse.RecentButtonPressedEvents[button];
                BaseMouseClickedEventArgs lastClick = mouse.RecentButtonClickedEvents[button];
                recent.Append($" {button}:held={(held == null ? "-" : P(held.Position) + "@" + Ms(held.PressedAt))} last={(lastClick == null ? "-" : lastClick.ClickCount + "@" + Ms(lastClickReleasedAt[(int)button]))}");
            }

            journal.Add($" trk{state}");
            journal.Add($" recent{recent}");
        }

        void Step(int dt, Point? to = null, int wheel = 0)
        {
            t += dt;
            if (to.HasValue)
            {
                position = to.Value;
            }

            frame++;
            tracker.Update(new UpdateBaseArgs(TimeSpan.FromMilliseconds(t), TimeSpan.FromMilliseconds(dt),
                new MouseState(position.X, position.Y, wheel,
                    left ? ButtonState.Pressed : ButtonState.Released, middle ? ButtonState.Pressed : ButtonState.Released, right ? ButtonState.Pressed : ButtonState.Released,
                    ButtonState.Released, ButtonState.Released),
                new KeyboardState()));
            journal.Add($"frame {frame} t={t} mouse={P(position)} buttons={(left ? "L" : "-")}{(middle ? "M" : "-")}{(right ? "R" : "-")}");
            LogTracker();
            for (int i = 0; i < handlers.Length; i++)
            {
                handlers[i].ManualUpdate();
            }
        }

        void Set(MouseButton button, bool down)
        {
            switch (button)
            {
                case MouseButton.Left: left = down; break;
                case MouseButton.Middle: middle = down; break;
                default: right = down; break;
            }
        }

        void Section(string label) => journal.Add($"== {label}");
        void Wait(int milliseconds) { Step(milliseconds); }
        void Idle(int frames, Point? to = null) { for (int i = 0; i < frames; i++) { Step(16, i == 0 ? to : null); } }
        void Press(MouseButton button, Point? to = null) { Set(button, true); Step(16, to); }
        void Release(MouseButton button, Point? to = null) { Set(button, false); Step(16, to); }
        void Click(MouseButton button, Point at, int heldFrames = 2, int gapFrames = 1)
        {
            Press(button, at);
            for (int i = 1; i < heldFrames; i++) { Step(16); }
            Release(button);
            for (int i = 0; i < gapFrames; i++) { Step(16); }
        }

        Point inX = new(50, 50);
        Point inY = new(250, 50);
        Point inW = new(50, 170);
        Point inParent = new(350, 250);

        Section("idle");
        Idle(2, inX);

        Section("single click of each button, in X");
        Click(MouseButton.Left, inX);
        Wait(700);
        Click(MouseButton.Middle, inX);
        Wait(700);
        Click(MouseButton.Right, inX);
        Wait(700);

        Section("double, triple and fourth click, in X");
        Click(MouseButton.Left, inX);
        Click(MouseButton.Left, inX);
        Click(MouseButton.Left, inX);
        Click(MouseButton.Left, inX);
        Wait(700);
        Click(MouseButton.Right, inX);
        Click(MouseButton.Right, inX);
        Wait(700);
        Click(MouseButton.Middle, inX);
        Click(MouseButton.Middle, inX);
        Click(MouseButton.Middle, inX);
        Wait(700);

        Section("a click of another button between two clicks of a sequence");
        Click(MouseButton.Left, inX);
        Click(MouseButton.Right, inX);
        Click(MouseButton.Left, inX);
        Click(MouseButton.Middle, inX);
        Click(MouseButton.Left, inX);
        Wait(700);

        Section("a press of a button while another is held");
        Press(MouseButton.Left, inX);
        Step(16);
        Press(MouseButton.Middle);
        Step(16);
        Release(MouseButton.Middle);
        Press(MouseButton.Right);
        Release(MouseButton.Left);
        Step(16);
        Release(MouseButton.Right);
        Wait(700);

        Section("a release that is not a click (held too long, then moved too far)");
        Press(MouseButton.Left, inX);
        Wait(200);
        Wait(200);
        Release(MouseButton.Left);
        Wait(700);
        Press(MouseButton.Left, inX);
        Step(16, new Point(inX.X + 2, inX.Y));
        Release(MouseButton.Left);
        Wait(700);

        Section("drag started at the first move, in X (left, middle, right)");
        foreach (MouseButton button in MouseTracker.MouseButtons)
        {
            Press(button, inX);
            Step(16, new Point(inX.X + 1, inX.Y));
            Step(16, new Point(inX.X + 6, inX.Y + 2));
            Step(16);
            Step(16, new Point(inX.X + 20, inX.Y + 10));
            Step(16, new Point(inX.X + 40, inX.Y + 20));
            Release(button, new Point(inX.X + 40, inX.Y + 20));
            Wait(700);
        }

        Section("drag started at the press, in Y");
        Press(MouseButton.Left, inY);
        Step(16, new Point(inY.X + 10, inY.Y + 5));
        Step(16, new Point(inY.X + 30, inY.Y + 15));
        Release(MouseButton.Left);
        Press(MouseButton.Right, inY);
        Step(16, new Point(inY.X + 4, inY.Y));
        Release(MouseButton.Right);
        Wait(700);

        Section("drag with both conditions, in W");
        Press(MouseButton.Left, inW);
        Step(16, new Point(inW.X + 10, inW.Y + 5));
        Step(16, new Point(inW.X + 30, inW.Y + 15));
        Release(MouseButton.Left);
        Wait(700);

        Section("a release outside the element that started the drag, then outside the parent");
        Press(MouseButton.Left, inX);
        Step(16, new Point(inX.X + 10, inX.Y));
        Step(16, inParent);
        Release(MouseButton.Left);
        Wait(700);
        Press(MouseButton.Left, inX);
        Step(16, new Point(inX.X + 10, inX.Y));
        Step(16, new Point(450, 350));
        Release(MouseButton.Left);
        Wait(700);
        Press(MouseButton.Left, inX);
        Step(16, inY);
        Release(MouseButton.Left);
        Wait(700);

        Section("a press and a click on the parent only, and outside every handler");
        Click(MouseButton.Left, inParent);
        Click(MouseButton.Left, inParent);
        Wait(700);
        Click(MouseButton.Left, new Point(450, 350));
        Wait(700);

        Section("the element X handles the drag start");
        xHandlesDrag = true;
        Press(MouseButton.Left, inX);
        Step(16, new Point(inX.X + 10, inX.Y));
        Step(16, new Point(inX.X + 30, inX.Y + 10));
        Step(16, inY);
        Release(MouseButton.Left);
        xHandlesDrag = false;
        Wait(700);

        Section("the element X handles its clicks");
        xHandlesClicks = true;
        Click(MouseButton.Left, inX);
        Click(MouseButton.Left, inX);
        Wait(700);
        xHandlesClicks = false;

        Section("a click handled by the parent before the child");
        hostEarly.Enabled = true;
        Click(MouseButton.Left, inX);
        Click(MouseButton.Left, inX);
        Click(MouseButton.Right, inX);
        Wait(700);
        Press(MouseButton.Left, inX);
        Step(16, new Point(inX.X + 10, inX.Y));
        Step(16, new Point(inX.X + 30, inX.Y + 10));
        Release(MouseButton.Left);
        Wait(700);
        hostEarly.Enabled = false;

        Section("a handler that always handles the events it receives, in W");
        Click(MouseButton.Left, inW);
        Click(MouseButton.Left, inW);
        Click(MouseButton.Middle, inW);
        Wait(700);

        Section("a double click of the same button on X, then on Y");
        Click(MouseButton.Left, inX);
        Click(MouseButton.Left, inX);
        Wait(700);
        Click(MouseButton.Left, inY);
        Click(MouseButton.Left, inY);
        Wait(700);

        Section("a click on X, then two clicks on Y at once (the pointer moved too far for a multi click)");
        Click(MouseButton.Left, inX);
        Click(MouseButton.Left, inY);
        Click(MouseButton.Left, inY);
        Wait(700);

        Section("a triple click on X, then a double click on Y, then a double click on X");
        Click(MouseButton.Left, inX);
        Click(MouseButton.Left, inX);
        Click(MouseButton.Left, inX);
        Wait(700);
        Click(MouseButton.Left, inY);
        Click(MouseButton.Left, inY);
        Click(MouseButton.Left, inX);
        Click(MouseButton.Left, inX);
        Wait(700);

        Section("a double click on Y with the right button, then on X with the right button");
        Click(MouseButton.Right, inY);
        Click(MouseButton.Right, inY);
        Click(MouseButton.Right, inX);
        Click(MouseButton.Right, inX);
        Wait(700);

        Section("the wheel, for the tick that follows a click");
        Click(MouseButton.Left, inX);
        Step(16, null, 120);
        Step(16, null, 120);
        Wait(700);

        return journal;
    }
}
