using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended;
using System.ComponentModel;
using System.Collections.ObjectModel;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;

namespace MGUI.Shared.Input.Mouse
{
    /// <summary>This class is just intended to be a convenient way to create a concrete implementation of <see cref="IMouseHandlerHost"/></summary>
    public class MouseHandlerHost : IMouseHandlerHost
    {
        private readonly Func<Rectangle> ComputeBounds;
        private readonly Func<RectangleF> ComputeBoundsF;
        private readonly Func<Vector2> ComputeOffset;

        public IMouseHandlerHost MouseInputParent { get; set; }

        public MouseHandlerHost(Func<Rectangle> ComputeBounds)
            : this(ComputeBounds, null) { }

        public MouseHandlerHost(Func<Rectangle> ComputeBounds, Func<Vector2> ComputeOffset)
        {
            this.ComputeBounds = ComputeBounds;
            ComputeBoundsF = null;
            this.ComputeOffset = ComputeOffset;
        }

        public MouseHandlerHost(Func<RectangleF> ComputeBounds)
            : this(ComputeBounds, null) { }

        public MouseHandlerHost(Func<RectangleF> ComputeBounds, Func<Vector2> ComputeOffset)
        {
            this.ComputeBounds = null;
            ComputeBoundsF = ComputeBounds;
            this.ComputeOffset = ComputeOffset;
        }

        Vector2 IMouseViewport.GetOffset() => ComputeOffset?.Invoke() ?? Vector2.Zero;
        bool IMouseViewport.IsInside(Vector2 Position) => ComputeBounds != null ? ComputeBounds().ContainsInclusive(Position) : ComputeBoundsF().ContainsInclusive(Position);
        IMouseHandlerHost IMouseHandlerHost.GetMouseInputParent() => MouseInputParent;
    }

    public interface IMouseViewport
    {
        public bool IsInside(Vector2 Position);
        /// <summary>Retrives an offset that should be added to the real mouse screen position before checking if the position is inside the viewport.<para/>
        /// For example, if a TextBlock is inside a ScrollViewer that is scrolled to VerticalOffset=100,<br/>
        /// the viewport Rectangle remains unchanged, but the real mouse position is offseted by +100 along the Y-axis before checking if the position is inside the viewport.</summary>
        public Vector2 GetOffset();
    }

    public interface IMouseHandlerHost : IMouseViewport
    {
        public bool CanReceiveMouseInput() => true;
        public IMouseHandlerHost GetMouseInputParent() => null;
    }

    public enum MouseButton
    {
        Left,
        Middle,
        Right
    }

    /// <summary>Detects changes to the mouse state between the previous and current Update ticks, but does not invoke the events. Instead, events are subscribed to and fired by <see cref="MouseHandler"/>.<br/>
    /// See: <see cref="MouseTracker.CreateHandler{T}(T, double?, bool, bool, bool)"/>, <see cref="MouseTracker.CreateHandler{T}(T, InputUpdatePriority, bool, bool)"/><para/>
    /// This class is automatically instantiated when creating an instance of <see cref="InputTracker"/>. See: <see cref="InputTracker.Mouse"/></summary>
    public class MouseTracker
    {
        public static readonly ReadOnlyCollection<MouseButton> MouseButtons = Enum.GetValues(typeof(MouseButton)).Cast<MouseButton>().ToList().AsReadOnly();
        /// <summary>Same values as <see cref="MouseButtons"/>, in an array: a <c>foreach</c> over it allocates no enumerator.</summary>
        private static readonly MouseButton[] MouseButtonValues = MouseButtons.ToArray();

        /// <summary>The maximum number of pixels that the mouse can move by (in either the X or Y direction) while pressed before releasing for a Click event to be invoked.</summary>
        public int ClickPositionThreshold { get; set; } = 2;
        /// <summary>The maximum amount of time that can pass between a mouse button press and mouse button release to still be registered as a Click event</summary>
        public TimeSpan ClickTimeThreshold { get; set; } = TimeSpan.FromMilliseconds(300);
        /// <summary>The maximum amount of time that can pass between two consecutive clicks of the same button to still be registered as a multi-click.</summary>
        public TimeSpan MultiClickTimeThreshold { get; set; } = TimeSpan.FromMilliseconds(500);
        /// <summary>The maximum number of pixels that the pointer can move between two consecutive clicks of the same button to still be registered as a multi-click.</summary>
        public int MultiClickPositionThreshold { get; set; } = 4;

        /// <summary>The minimum number of pixels that the mouse must move by (in either the X or Y direction) while pressed before the a mouse drag event begins.<para/>
        /// Only used if <see cref="DragStartCondition"/> == <see cref="DragStartCondition.MouseMovedAfterPress"/></summary>
        public int DragThreshold { get; set; } = 3;

        public InputTracker InputTracker { get; }

        private List<MouseHandler> _Handlers { get; }
        /// <summary>To create a <see cref="MouseHandler"/>, use <see cref="CreateHandler{T}(T, double?, bool, bool, bool)"/> or <see cref="CreateHandler{T}(T, InputUpdatePriority, bool, bool)"/></summary>
        public IReadOnlyList<MouseHandler> Handlers => _Handlers;

        internal void RemoveHandler(MouseHandler Handler)
        {
            if (_Handlers.Remove(Handler))
            {
                _OrderedHandlers = null;
            }
        }

        /// <summary>The auto-updated <see cref="Handlers"/> in <see cref="UpdateHandlers"/> order (see <see cref="InputHandlerOrder.Build{T}"/>).
        /// Null after a handler is added or removed, rebuilt into a new array by the next <see cref="UpdateHandlers"/>, so a walk in progress
        /// keeps the handlers it started with.</summary>
        private MouseHandler[] _OrderedHandlers;

        public MouseState PreviousState { get; private set; }
        public MouseState CurrentState { get; private set; }
        internal TimeSpan CurrentTotalElapsed { get; private set; }

        /// <summary>True if the mouse left button was just pressed during the current update.<para/>
        /// See also: <see cref="MouseLeftButtonReleasedRecently"/></summary>
        public bool MouseLeftButtonPressedRecently => _CurrentButtonPressedEvents[MouseButton.Left] != null;
        /// <summary>True if the mouse left button was just released during the current update.<para/>
        /// See also: <see cref="MouseLeftButtonPressedRecently"/></summary>
        public bool MouseLeftButtonReleasedRecently => _CurrentButtonReleasedEvents[MouseButton.Left] != null;

        /// <summary>True if the mouse position changed between the previous update and the current update.</summary>
        public bool MouseMovedRecently => CurrentState.Position != PreviousState.Position;

        internal MouseTracker(InputTracker InputTracker, int ClickPositionThreshold = 2, int DragThreshold = 3)
        {
            this.InputTracker = InputTracker;
            _Handlers = new();
            this.ClickPositionThreshold = ClickPositionThreshold;
            this.DragThreshold = DragThreshold;
        }

        /// <param name="UpdatePriority">The priority with which the handler receives mouse events. A higher priority means this handler will have the first chance to receive and handle events.</param>
        /// <param name="AlwaysHandlesEvents">If true, the handler will always set <see cref="HandledByEventArgs{THandlerType}.HandledBy"/> to <paramref name="Owner"/> when receiving the mouse event.</param>
        /// <param name="InvokeEvenIfHandled">If true, the handler will still receive the mouse event even if <see cref="HandledByEventArgs{THandlerType}.IsHandled"/> is true.<para/>
        /// <b>Warning:</b> this is equivalent to <see cref="HandledInputPolicy.Any"/> and therefore defeats occlusion.
        /// Prefer the <see cref="CreateHandler{T}(T, InputUpdatePriority, HandledInputPolicy, bool)"/> overload.</param>
        public MouseHandler CreateHandler<T>(T Owner, InputUpdatePriority UpdatePriority, bool AlwaysHandlesEvents = false, bool InvokeEvenIfHandled = false)
            where T : IMouseHandlerHost => CreateHandler(Owner, (int)UpdatePriority, AlwaysHandlesEvents, InvokeEvenIfHandled);

        /// <param name="UpdatePriority">The priority with which the handler receives mouse events. A higher priority means this handler will have the first chance to receive and handle events.</param>
        /// <param name="HandledInputPolicy">Determines which already-handled events this handler still receives.</param>
        /// <param name="AlwaysHandlesEvents">If true, the handler will always set <see cref="HandledByEventArgs{THandlerType}.HandledBy"/> to <paramref name="Owner"/> when receiving the mouse event.</param>
        public MouseHandler CreateHandler<T>(T Owner, InputUpdatePriority UpdatePriority, HandledInputPolicy HandledInputPolicy, bool AlwaysHandlesEvents = false)
            where T : IMouseHandlerHost => CreateHandler(Owner, (int)UpdatePriority, HandledInputPolicy, AlwaysHandlesEvents);

        /// <param name="UpdatePriority">The priority with which the handler receives mouse events. A higher priority means this handler will have the first chance to receive and handle events.<para/>
        /// If null, the caller is expected to manually call <see cref="MouseHandler.ManualUpdate"/> themselves, and the handler is NOT added to <see cref="Handlers"/>
        /// (it isn't read by <see cref="UpdateHandlers"/>, so registering it would only be a dead GC root keeping <paramref name="Owner"/> alive).<br/>
        /// If not null, <see cref="MouseHandler.AutoUpdate"/> will automatically be invoked when updating this <see cref="MouseTracker"/>, and the handler is added to <see cref="Handlers"/><para/>
        /// Minimum Value = 0. Maximum Value = 100. Recommended to use preset values from <see cref="InputUpdatePriority"/>.</param>
        /// <param name="AlwaysHandlesEvents">If true, the handler will always set <see cref="HandledByEventArgs{THandlerType}.HandledBy"/> to <paramref name="Owner"/> when receiving the mouse event.</param>
        /// <param name="InvokeEvenIfHandled">If true, the handler will still receive the mouse event even if <see cref="HandledByEventArgs{THandlerType}.IsHandled"/> is true.<para/>
        /// <b>Warning:</b> this is equivalent to <see cref="HandledInputPolicy.Any"/>. Occlusion is expressed by having the
        /// topmost element mark the event as handled, so this also makes the handler react to clicks aimed at whatever is
        /// drawn overtop of <paramref name="Owner"/>. Prefer <see cref="CreateHandler{T}(T, double?, HandledInputPolicy, bool)"/>
        /// with <see cref="HandledInputPolicy.Descendant"/> when the intent is only to see clicks consumed by a child.</param>
        /// <param name="InvokeIfHandledBySelf">If true, the handler will still receive the mouse event even if <see cref="HandledByEventArgs{THandlerType}.IsHandled"/> is true,
        /// as long as the handler is the same as <see cref="MouseHandler.Owner"/></param>
        public MouseHandler CreateHandler<T>(T Owner, double? UpdatePriority, bool AlwaysHandlesEvents = false, bool InvokeEvenIfHandled = false, bool InvokeIfHandledBySelf = true)
            where T : IMouseHandlerHost
        {
            HandledInputPolicy Policy = InvokeEvenIfHandled ? HandledInputPolicy.Any
                : InvokeIfHandledBySelf ? HandledInputPolicy.Self
                : HandledInputPolicy.None;
            return CreateHandler(Owner, UpdatePriority, Policy, AlwaysHandlesEvents);
        }

        /// <param name="UpdatePriority">The priority with which the handler receives mouse events. A higher priority means this handler will have the first chance to receive and handle events.<para/>
        /// If null, the caller is expected to manually call <see cref="MouseHandler.ManualUpdate"/> themselves, and the handler is NOT added to <see cref="Handlers"/>
        /// (it isn't read by <see cref="UpdateHandlers"/>, so registering it would only be a dead GC root keeping <paramref name="Owner"/> alive).<br/>
        /// If not null, <see cref="MouseHandler.AutoUpdate"/> will automatically be invoked when updating this <see cref="MouseTracker"/>, and the handler is added to <see cref="Handlers"/><para/>
        /// Minimum Value = 0. Maximum Value = 100. Recommended to use preset values from <see cref="InputUpdatePriority"/>.</param>
        /// <param name="HandledInputPolicy">Determines which already-handled events this handler still receives.
        /// See <see cref="Mouse.HandledInputPolicy"/> for why this should be as narrow as possible.</param>
        /// <param name="AlwaysHandlesEvents">If true, the handler will always set <see cref="HandledByEventArgs{THandlerType}.HandledBy"/> to <paramref name="Owner"/> when receiving the mouse event.</param>
        public MouseHandler CreateHandler<T>(T Owner, double? UpdatePriority, HandledInputPolicy HandledInputPolicy, bool AlwaysHandlesEvents = false)
            where T : IMouseHandlerHost
        {
            MouseHandler Handler = new(this, Owner, UpdatePriority, AlwaysHandlesEvents, HandledInputPolicy);
            if (UpdatePriority.HasValue)
            {
                _Handlers.Add(Handler);
                _OrderedHandlers = null;
            }
            return Handler;
        }

        public bool IsPressedInside(MouseButton Button, RectangleF Viewport)
            => IsPressedInside(Button, Viewport, Vector2.Zero);
        public bool IsPressedInside(MouseButton Button, RectangleF Viewport, Vector2 Offset)
            => _RecentButtonPressedEvents[Button] != null && Viewport.ContainsInclusive(_RecentButtonPressedEvents[Button].Position.ToVector2() + Offset);
        public bool IsPressedInside(MouseButton Button, IMouseHandlerHost Owner)
            => _RecentButtonPressedEvents[Button] != null && Owner.IsInside(_RecentButtonPressedEvents[Button].Position.ToVector2() + Owner.GetOffset());

        #region Events
        /// <summary>The mouse scroll event that occurred on the current Update tick, or null if the mouse wheel didn't change between the previous Update tick and the current Update tick.<para/>
        /// The same instance is refilled from one wheel tick to the next (with its handled state reset), so that a wheel movement allocates nothing:
        /// read it during the current tick, do not keep it.</summary>
        public BaseMouseScrolledEventArgs CurrentScrollEvent { get; private set; } = null;
        /// <summary>The instance behind <see cref="CurrentScrollEvent"/>, created at the first wheel movement.</summary>
        private BaseMouseScrolledEventArgs _ReusedScrollEvent;

        /// <summary>The mouse move event that occurred on the current Update tick, or null if the mouse position didn't change between the previous Update tick and the current Update tick.<para/>
        /// The same instance is refilled from one move to the next, so that a move allocates nothing: read it during the current tick, do not keep it.</summary>
        public BaseMouseMovedEventArgs CurrentMoveEvent { get; private set; } = null;
        /// <summary>The instance behind <see cref="CurrentMoveEvent"/>, created at the first move.</summary>
        private BaseMouseMovedEventArgs _ReusedMoveEvent;

        //  The press, release, click, drag start and drag end events are instances kept by the tracker (one per button, and per drag start condition for the drag events),
        //  created at the first use and refilled, with their handled state reset, at each event of their kind (ADR-0034). The Current* and Recent* members below designate them.
        private readonly Dictionary<MouseButton, BaseMousePressedEventArgs> _ReusedPressedEvents = new()
        {
            { MouseButton.Left, null },
            { MouseButton.Middle, null },
            { MouseButton.Right, null }
        };
        private readonly Dictionary<MouseButton, BaseMouseReleasedEventArgs> _ReusedReleasedEvents = new()
        {
            { MouseButton.Left, null },
            { MouseButton.Middle, null },
            { MouseButton.Right, null }
        };
        private readonly Dictionary<MouseButton, BaseMouseClickedEventArgs> _ReusedClickedEvents = new()
        {
            { MouseButton.Left, null },
            { MouseButton.Middle, null },
            { MouseButton.Right, null }
        };
        /// <summary>The click sequence of each button, restarted at the first click of each sequence of that button.</summary>
        private readonly Dictionary<MouseButton, MouseClickSequence> _ReusedClickSequences = new()
        {
            { MouseButton.Left, null },
            { MouseButton.Middle, null },
            { MouseButton.Right, null }
        };
        /// <summary>The values of the previous click of each button, indexed by <see cref="MouseButton"/>.</summary>
        private readonly PreviousClickValues[] _PreviousClicks = new PreviousClickValues[3];
        private readonly Dictionary<DragStartCondition, Dictionary<MouseButton, BaseMouseDragStartEventArgs>> _ReusedDragStartEvents = new()
        {
            {
                DragStartCondition.MouseMovedAfterPress,
                new()
                {
                    { MouseButton.Left, null },
                    { MouseButton.Middle, null },
                    { MouseButton.Right, null }
                }
            },
            {
                DragStartCondition.MousePressed,
                new()
                {
                    { MouseButton.Left, null },
                    { MouseButton.Middle, null },
                    { MouseButton.Right, null }
                }
            }
        };
        private readonly Dictionary<DragStartCondition, Dictionary<MouseButton, BaseMouseDragEndEventArgs>> _ReusedDragEndEvents = new()
        {
            {
                DragStartCondition.MouseMovedAfterPress,
                new()
                {
                    { MouseButton.Left, null },
                    { MouseButton.Middle, null },
                    { MouseButton.Right, null }
                }
            },
            {
                DragStartCondition.MousePressed,
                new()
                {
                    { MouseButton.Left, null },
                    { MouseButton.Middle, null },
                    { MouseButton.Right, null }
                }
            }
        };

        /// <summary>The most recent MouseButton Press events that have occurred, even if they occurred on a prior Update tick. These values are set back to null when the button is released.<para/>
        /// The instance of a button is refilled at each press of that button, so it is valid until the next press of that button: read it, do not keep it.</summary>
        private readonly Dictionary<MouseButton, BaseMousePressedEventArgs> _RecentButtonPressedEvents = new()
        {
            { MouseButton.Left, null },
            { MouseButton.Middle, null },
            { MouseButton.Right, null }
        };
        /// <summary>The most recent MouseButton Press events that have occurred, even if they occurred on a prior Update tick. These values are set back to null when the button is released.<para/>
        /// The instance of a button is refilled at each press of that button, so it is valid until the next press of that button: read it, do not keep it.</summary>
        public IReadOnlyDictionary<MouseButton, BaseMousePressedEventArgs> RecentButtonPressedEvents => _RecentButtonPressedEvents;

        private readonly Dictionary<MouseButton, BaseMousePressedEventArgs> _CurrentButtonPressedEvents = new()
        {
            { MouseButton.Left, null },
            { MouseButton.Middle, null },
            { MouseButton.Right, null }
        };
        /// <summary>The MouseButton Press events that occurred on the current Update tick, or null if the button's <see cref="ButtonState"/> wasn't just set to <see cref="ButtonState.Pressed"/> on the current Update tick.<para/>
        /// The instance of a button is refilled at each press of that button (ADR-0034): read it during the event, do not keep it.</summary>
        public IReadOnlyDictionary<MouseButton, BaseMousePressedEventArgs> CurrentButtonPressedEvents => _CurrentButtonPressedEvents;
        internal bool HasCurrentButtonPressedEvents;

        private readonly Dictionary<MouseButton, BaseMouseReleasedEventArgs> _CurrentButtonReleasedEvents = new()
        {
            { MouseButton.Left, null },
            { MouseButton.Middle, null },
            { MouseButton.Right, null }
        };
        /// <summary>The MouseButton Release events that occurred on the current Update tick, or null if the button's <see cref="ButtonState"/> wasn't just set to <see cref="ButtonState.Released"/> on the current Update tick.<para/>
        /// The instance of a button is refilled at each release of that button (ADR-0034): read it during the event, do not keep it.</summary>
        public IReadOnlyDictionary<MouseButton, BaseMouseReleasedEventArgs> CurrentButtonReleasedEvents => _CurrentButtonReleasedEvents;
        internal bool HasCurrentButtonReleasedEvents;

        /// <summary>The MouseButton Click events that occurred on the current Update tick, or null if the button didn't just finish clicking on the current Update tick.</summary>
        private readonly Dictionary<MouseButton, BaseMouseClickedEventArgs> _CurrentButtonClickedEvents = new()
        {
            { MouseButton.Left, null },
            { MouseButton.Middle, null },
            { MouseButton.Right, null }
        };
        /// <summary>The MouseButton Click events that occurred on the current Update tick, or null if the button didn't just finish clicking on the current Update tick.<para/>
        /// The instance of a button is refilled at each click of that button (ADR-0034): read it during the event, do not keep it.</summary>
        public IReadOnlyDictionary<MouseButton, BaseMouseClickedEventArgs> CurrentButtonClickedEvents => _CurrentButtonClickedEvents;
        internal bool HasCurrentButtonClickedEvents;

        private readonly Dictionary<MouseButton, BaseMouseClickedEventArgs> _RecentButtonClickedEvents = new()
        {
            { MouseButton.Left, null },
            { MouseButton.Middle, null },
            { MouseButton.Right, null }
        };
        /// <summary>The latest click of each button, or null before the first click of the button. It is the instance of the button that is refilled at each click (ADR-0034), so it always
        /// reads as the latest click: read it, do not keep it.</summary>
        public IReadOnlyDictionary<MouseButton, BaseMouseClickedEventArgs> RecentButtonClickedEvents => _RecentButtonClickedEvents;

        private long _NextClickSequenceId = 1;

        private readonly Dictionary<MouseButton, BaseMouseClickedEventArgs> _CurrentButtonDoubleClickedEvents = new()
        {
            { MouseButton.Left, null },
            { MouseButton.Middle, null },
            { MouseButton.Right, null }
        };
        /// <summary>The MouseButton Double-Click events that occurred on the current Update tick, or null if the button did not just finish a double-click on the current Update tick.<para/>
        /// This is the click instance of the button (see <see cref="CurrentButtonClickedEvents"/>), valid until the next click of that button (ADR-0034): read it during the event, do not keep it.</summary>
        public IReadOnlyDictionary<MouseButton, BaseMouseClickedEventArgs> CurrentButtonDoubleClickedEvents => _CurrentButtonDoubleClickedEvents;
        internal bool HasCurrentButtonDoubleClickedEvents;

        /// <summary>The most recent DragStart events that have occurred, even if they occurred on a prior Update tick. These values are set back to null when the DragEnd event is detected.</summary>
        private readonly Dictionary<DragStartCondition, Dictionary<MouseButton, BaseMouseDragStartEventArgs>> RecentDragStartEvents = new()
        {
            {
                DragStartCondition.MouseMovedAfterPress,
                new()
                {
                    { MouseButton.Left, null },
                    { MouseButton.Middle, null },
                    { MouseButton.Right, null }
                }
            },
            {
                DragStartCondition.MousePressed,
                new()
                {
                    { MouseButton.Left, null },
                    { MouseButton.Middle, null },
                    { MouseButton.Right, null }
                }
            }
        };

        private readonly Dictionary<DragStartCondition, Dictionary<MouseButton, BaseMouseDragStartEventArgs>> _CurrentDragStartEvents = new()
        {
            {
                DragStartCondition.MouseMovedAfterPress,
                new()
                {
                    { MouseButton.Left, null },
                    { MouseButton.Middle, null },
                    { MouseButton.Right, null }
                }
            },
            {
                DragStartCondition.MousePressed,
                new()
                {
                    { MouseButton.Left, null },
                    { MouseButton.Middle, null },
                    { MouseButton.Right, null }
                }
            }
        };
        /// <summary>The Mouse DragStart events that occurred on the current Update tick, or null if a DragStart didn't just occur the current Update tick.<para/>
        /// The instance of a condition and button is refilled at each drag start of that condition and button, so it is valid, handled state included, until the next one: it covers the
        /// drag and its drag end (ADR-0034). Read it, do not keep it.</summary>
        public IReadOnlyDictionary<DragStartCondition, Dictionary<MouseButton, BaseMouseDragStartEventArgs>> CurrentDragStartEvents => _CurrentDragStartEvents;
        internal bool HasCurrentDragStartEvents;

        private readonly Dictionary<DragStartCondition, Dictionary<MouseButton, BaseMouseDraggedEventArgs>> _CurrentDraggedEvents = new()
        {
            {
                DragStartCondition.MouseMovedAfterPress,
                new()
                {
                    { MouseButton.Left, null },
                    { MouseButton.Middle, null },
                    { MouseButton.Right, null }
                }
            },
            {
                DragStartCondition.MousePressed,
                new()
                {
                    { MouseButton.Left, null },
                    { MouseButton.Middle, null },
                    { MouseButton.Right, null }
                }
            }
        };
        /// <summary>The Mouse Dragged events that occurred on the current Update tick, or null if a Dragged event didn't just occur the current Update tick.<para/>
        /// The instance of a given condition and button is refilled from one drag frame to the next, so that a drag allocates nothing: read it during
        /// the current tick, do not keep it.</summary>
        public IReadOnlyDictionary<DragStartCondition, Dictionary<MouseButton, BaseMouseDraggedEventArgs>> CurrentDraggedEvents => _CurrentDraggedEvents;
        /// <summary>The instances behind <see cref="CurrentDraggedEvents"/>, one per condition and button, created at their first drag frame.</summary>
        private readonly Dictionary<DragStartCondition, Dictionary<MouseButton, BaseMouseDraggedEventArgs>> _ReusedDraggedEvents = new()
        {
            {
                DragStartCondition.MouseMovedAfterPress,
                new()
                {
                    { MouseButton.Left, null },
                    { MouseButton.Middle, null },
                    { MouseButton.Right, null }
                }
            },
            {
                DragStartCondition.MousePressed,
                new()
                {
                    { MouseButton.Left, null },
                    { MouseButton.Middle, null },
                    { MouseButton.Right, null }
                }
            }
        };
        internal bool HasCurrentDraggedEvents;

        private readonly Dictionary<DragStartCondition, Dictionary<MouseButton, BaseMouseDragEndEventArgs>> _CurrentDragEndEvents = new()
        {
            {
                DragStartCondition.MouseMovedAfterPress,
                new()
                {
                    { MouseButton.Left, null },
                    { MouseButton.Middle, null },
                    { MouseButton.Right, null }
                }
            },
            {
                DragStartCondition.MousePressed,
                new()
                {
                    { MouseButton.Left, null },
                    { MouseButton.Middle, null },
                    { MouseButton.Right, null }
                }
            }
        };
        /// <summary>The Mouse DragEnd events that occurred on the current Update tick, or null if a DragEnd didn't just occur the current Update tick.<para/>
        /// The instance of a condition and button is refilled at each drag end of that condition and button (ADR-0034): read it during the event, do not keep it.</summary>
        public IReadOnlyDictionary<DragStartCondition, Dictionary<MouseButton, BaseMouseDragEndEventArgs>> CurrentDragEndEvents => _CurrentDragEndEvents;
        internal bool HasCurrentDragEndEvents;

        internal bool HasCurrentButtonEvents;
        internal bool HasCurrentDragEvents;
        #endregion Events

        public Point CurrentPosition { get; private set; }

        internal void Update(UpdateBaseArgs BA)
        {
            PreviousState = CurrentState;
            CurrentState = BA.MouseState;
            CurrentTotalElapsed = BA.TotalElapsed;

            CurrentPosition = CurrentState.Position;

            //  Detect changes to the mouse wheel
            CurrentScrollEvent = null;
            if (PreviousState.ScrollWheelValue != CurrentState.ScrollWheelValue)
            {
                int ScrollWheelDelta = CurrentState.ScrollWheelValue - PreviousState.ScrollWheelValue;
                if (_ReusedScrollEvent == null)
                {
                    _ReusedScrollEvent = new(this, ScrollWheelDelta, CurrentPosition);
                }
                else
                {
                    _ReusedScrollEvent.SetScroll(ScrollWheelDelta, CurrentPosition);
                }

                CurrentScrollEvent = _ReusedScrollEvent;
                //Debug.WriteLine($"Scrolled: {CurrentScrollEvent.ScrollWheelDelta} - {CurrentPosition}");
            }

            //  Detect changes to the mouse position
            CurrentMoveEvent = null;
            if (PreviousState.Position != CurrentState.Position)
            {
                if (_ReusedMoveEvent == null)
                {
                    _ReusedMoveEvent = new(this, PreviousState.Position, CurrentState.Position);
                }
                else
                {
                    _ReusedMoveEvent.SetPositions(PreviousState.Position, CurrentState.Position);
                }

                CurrentMoveEvent = _ReusedMoveEvent;
                //Debug.WriteLine($"Moved: {PreviousState.Position} - {CurrentState.Position}");
            }

            //  Detect button press/release/click events
            foreach (MouseButton Button in MouseButtonValues)
            {
                _CurrentButtonPressedEvents[Button] = null;
                _CurrentButtonReleasedEvents[Button] = null;
                _CurrentButtonClickedEvents[Button] = null;
                _CurrentButtonDoubleClickedEvents[Button] = null;
                _CurrentDragStartEvents[DragStartCondition.MousePressed][Button] = null;

                //  Detect buttons that were just pressed
                if (GetButtonState(PreviousState, Button) == ButtonState.Released && GetButtonState(CurrentState, Button) == ButtonState.Pressed)
                {
                    //Debug.WriteLine($"Pressed: {Button} - {CurrentPosition}");
                    BaseMousePressedEventArgs Args = _ReusedPressedEvents[Button];
                    if (Args == null)
                    {
                        Args = new(this, Button, CurrentPosition, BA.TotalElapsed);
                        _ReusedPressedEvents[Button] = Args;
                    }
                    else
                    {
                        Args.Set(CurrentPosition, BA.TotalElapsed);
                    }

                    _RecentButtonPressedEvents[Button] = Args;
                    _CurrentButtonPressedEvents[Button] = Args;

                    //  Detect DragStart events when Condition=MousePressed
                    bool IsDragging = RecentDragStartEvents[DragStartCondition.MousePressed][Button] != null;
                    if (!IsDragging)
                    {
                        BaseMouseDragStartEventArgs DragStartArgs = GetDragStartEvent(DragStartCondition.MousePressed, Button, CurrentPosition, BA.TotalElapsed);
                        RecentDragStartEvents[DragStartCondition.MousePressed][Button] = DragStartArgs;
                        _CurrentDragStartEvents[DragStartCondition.MousePressed][Button] = DragStartArgs;
                    }
                }

                //  Detect buttons that were just released
                if (GetButtonState(PreviousState, Button) == ButtonState.Pressed && GetButtonState(CurrentState, Button) == ButtonState.Released &&
                    _RecentButtonPressedEvents.TryGetValue(Button, out BaseMousePressedEventArgs PressedArgs) && PressedArgs != null)
                {
                    //Debug.WriteLine($"Released: {Button} - {CurrentPosition}");
                    BaseMouseReleasedEventArgs ReleasedArgs = _ReusedReleasedEvents[Button];
                    if (ReleasedArgs == null)
                    {
                        ReleasedArgs = new(this, PressedArgs, Button, CurrentPosition, BA.TotalElapsed);
                        _ReusedReleasedEvents[Button] = ReleasedArgs;
                    }
                    else
                    {
                        ReleasedArgs.Set(PressedArgs, CurrentPosition, BA.TotalElapsed);
                    }

                    _CurrentButtonReleasedEvents[Button] = ReleasedArgs;

                    //  Detect buttons that were just clicked
                    if (Math.Abs(PressedArgs.Position.X - ReleasedArgs.Position.X) <= ClickPositionThreshold && Math.Abs(PressedArgs.Position.Y - ReleasedArgs.Position.Y) <= ClickPositionThreshold &&
                        ReleasedArgs.HeldDuration <= ClickTimeThreshold)
                    {
                        //  The multi click is detected on the values of the previous click of this button, kept here: the previous click object is the one that is refilled below.
                        ref PreviousClickValues Previous = ref _PreviousClicks[(int)Button];
                        int clickCount = GetMultiClickCount(Previous.ClickCount, Previous.ReleasedAt, Previous.Position, ReleasedArgs, MultiClickTimeThreshold, MultiClickPositionThreshold);
                        MouseClickSequence sequence = _ReusedClickSequences[Button];
                        if (sequence == null)
                        {
                            sequence = new MouseClickSequence(_NextClickSequenceId++, Button, ReleasedArgs.ReleasedAt);
                            _ReusedClickSequences[Button] = sequence;
                        }
                        else if (clickCount <= 1)
                        {
                            //  A click that does not continue the sequence of its button starts a new one, with a new identity and no adopted target.
                            sequence.Restart(_NextClickSequenceId++, Button, ReleasedArgs.ReleasedAt);
                        }

                        BaseMouseClickedEventArgs ClickedArgs = _ReusedClickedEvents[Button];
                        if (ClickedArgs == null)
                        {
                            ClickedArgs = new(this, ReleasedArgs, Button, CurrentPosition, clickCount, sequence);
                            _ReusedClickedEvents[Button] = ClickedArgs;
                        }
                        else
                        {
                            ClickedArgs.Set(ReleasedArgs, CurrentPosition, clickCount, sequence);
                        }

                        Previous.ClickCount = ClickedArgs.ClickCount;
                        Previous.ReleasedAt = ReleasedArgs.ReleasedAt;
                        Previous.Position = ClickedArgs.Position;
                        _CurrentButtonClickedEvents[Button] = ClickedArgs;
                        _RecentButtonClickedEvents[Button] = ClickedArgs;

                        if (IsDoubleClickCount(ClickedArgs.ClickCount))
                        {
                            _CurrentButtonDoubleClickedEvents[Button] = ClickedArgs;
                        }
                    }

                    _RecentButtonPressedEvents[Button] = null;
                }
            }

            //  Detect mouse drag events
            foreach (DragStartCondition Condition in MouseHandler.DragStartConditionValues)
            {
                foreach (MouseButton Button in MouseButtonValues)
                {
                    if (Condition != DragStartCondition.MousePressed)
                    {
                        _CurrentDragStartEvents[Condition][Button] = null;
                    }

                    _CurrentDraggedEvents[Condition][Button] = null;
                    _CurrentDragEndEvents[Condition][Button] = null;

                    bool IsDragging = RecentDragStartEvents[Condition][Button] != null;
                    bool IsButtonPressed = _RecentButtonPressedEvents[Button] != null;

                    //  Check if a drag just began
                    if (CurrentMoveEvent != null && !IsDragging && IsButtonPressed)
                    {
                        BaseMousePressedEventArgs PressedArgs = _RecentButtonPressedEvents[Button];
                        if (Condition == DragStartCondition.MouseMovedAfterPress &&
                            (Math.Abs(CurrentPosition.X - PressedArgs.Position.X) >= DragThreshold || Math.Abs(CurrentPosition.Y - PressedArgs.Position.Y) >= DragThreshold))
                        {
                            //Debug.WriteLine($"Drag Start: {Button} - {PressedArgs.Position}");
                            BaseMouseDragStartEventArgs DragStartArgs = GetDragStartEvent(Condition, Button, PressedArgs.Position, BA.TotalElapsed);
                            RecentDragStartEvents[Condition][Button] = DragStartArgs;
                            _CurrentDragStartEvents[Condition][Button] = DragStartArgs;
                        }
                    }
                    else
                    {
                        //  Check if a drag is in progress
                        if (CurrentMoveEvent != null && IsDragging)
                        {
                            BaseMouseDragStartEventArgs StartArgs = RecentDragStartEvents[Condition][Button];
                            //Debug.WriteLine($"Dragged: {Button} - {StartArgs.Position} - {CurrentPosition}");
                            BaseMouseDraggedEventArgs DraggedArgs = _ReusedDraggedEvents[Condition][Button];
                            if (DraggedArgs == null)
                            {
                                DraggedArgs = new(this, StartArgs, Button, CurrentPosition, BA.TotalElapsed);
                                _ReusedDraggedEvents[Condition][Button] = DraggedArgs;
                            }
                            else
                            {
                                DraggedArgs.Set(StartArgs, CurrentPosition, BA.TotalElapsed);
                            }

                            _CurrentDraggedEvents[Condition][Button] = DraggedArgs;
                        }

                        //  Check if a drag just ended
                        if (IsDragging && _CurrentButtonReleasedEvents[Button] != null)
                        {
                            BaseMouseDragStartEventArgs StartArgs = RecentDragStartEvents[Condition][Button];
                            //Debug.WriteLine($"Drag End: {Button} - {StartArgs.Position} - {CurrentPosition}");
                            BaseMouseDragEndEventArgs DragEndArgs = _ReusedDragEndEvents[Condition][Button];
                            if (DragEndArgs == null)
                            {
                                DragEndArgs = new(this, StartArgs, Button, CurrentPosition, BA.TotalElapsed);
                                _ReusedDragEndEvents[Condition][Button] = DragEndArgs;
                            }
                            else
                            {
                                DragEndArgs.Set(StartArgs, CurrentPosition, BA.TotalElapsed);
                            }

                            _CurrentDragEndEvents[Condition][Button] = DragEndArgs;
                            RecentDragStartEvents[Condition][Button] = null;
                        }
                    }
                }
            }

            HasCurrentButtonPressedEvents = HasAnyEvent(_CurrentButtonPressedEvents);
            HasCurrentButtonReleasedEvents = HasAnyEvent(_CurrentButtonReleasedEvents);
            HasCurrentButtonClickedEvents = HasAnyEvent(_CurrentButtonClickedEvents);
            HasCurrentButtonDoubleClickedEvents = HasAnyEvent(_CurrentButtonDoubleClickedEvents);
            HasCurrentButtonEvents = HasCurrentButtonPressedEvents || HasCurrentButtonReleasedEvents || HasCurrentButtonClickedEvents || HasCurrentButtonDoubleClickedEvents;
            HasCurrentDragStartEvents = HasAnyEvent(_CurrentDragStartEvents);
            HasCurrentDraggedEvents = HasAnyEvent(_CurrentDraggedEvents);
            HasCurrentDragEndEvents = HasAnyEvent(_CurrentDragEndEvents);
            HasCurrentDragEvents = HasCurrentDragStartEvents || HasCurrentDraggedEvents || HasCurrentDragEndEvents;
        }

        /// <summary>The click count of a click, given the values of the previous click of the same button (not the previous click object, which <see cref="MouseTracker"/> refills for the
        /// new click): the count after it if the release is close enough in time and position, 1 otherwise.</summary>
        /// <param name="PreviousClickCount">The click count of the previous click of the button, or 0 or less if the button was never clicked.</param>
        /// <param name="PreviousReleasedAt">The release time of the previous click.</param>
        /// <param name="PreviousPosition">The position of the previous click.</param>
        public static int GetMultiClickCount(int PreviousClickCount, TimeSpan PreviousReleasedAt, Point PreviousPosition, BaseMouseReleasedEventArgs ReleasedArgs, TimeSpan multiClickTimeThreshold, int multiClickPositionThreshold)
        {
            if (PreviousClickCount <= 0 || ReleasedArgs == null)
            {
                return 1;
            }

            bool isWithinTime = ReleasedArgs.ReleasedAt - PreviousReleasedAt <= multiClickTimeThreshold;
            bool isWithinPosition = Math.Abs(PreviousPosition.X - ReleasedArgs.Position.X) <= multiClickPositionThreshold
                && Math.Abs(PreviousPosition.Y - ReleasedArgs.Position.Y) <= multiClickPositionThreshold;

            return isWithinTime && isWithinPosition ? PreviousClickCount + 1 : 1;
        }

        /// <summary>The values of the previous click of a button, kept for the detection of the multi click (the click object is refilled at each click).</summary>
        private struct PreviousClickValues
        {
            public int ClickCount;
            public TimeSpan ReleasedAt;
            public Point Position;
        }

        /// <summary>The reused drag start instance of a condition and a button, created at its first use, refilled with its handled state reset afterwards.</summary>
        private BaseMouseDragStartEventArgs GetDragStartEvent(DragStartCondition Condition, MouseButton Button, Point Position, TimeSpan StartedAt)
        {
            BaseMouseDragStartEventArgs DragStartArgs = _ReusedDragStartEvents[Condition][Button];
            if (DragStartArgs == null)
            {
                DragStartArgs = new(this, Button, Position, Condition, StartedAt);
                _ReusedDragStartEvents[Condition][Button] = DragStartArgs;
            }
            else
            {
                DragStartArgs.Set(Position, StartedAt);
            }

            return DragStartArgs;
        }

        /// <summary>True if any button has an event. Walks the dictionary with its struct enumerator, so it allocates nothing.</summary>
        private static bool HasAnyEvent<TEventArgs>(Dictionary<MouseButton, TEventArgs> EventsByButton)
            where TEventArgs : class
        {
            foreach (KeyValuePair<MouseButton, TEventArgs> Entry in EventsByButton)
            {
                if (Entry.Value != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True if any button has an event for any drag start condition. Allocates nothing.</summary>
        private static bool HasAnyEvent<TEventArgs>(Dictionary<DragStartCondition, Dictionary<MouseButton, TEventArgs>> EventsByCondition)
            where TEventArgs : class
        {
            foreach (KeyValuePair<DragStartCondition, Dictionary<MouseButton, TEventArgs>> Entry in EventsByCondition)
            {
                if (HasAnyEvent(Entry.Value))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsDoubleClickCount(int ClickCount)
            => ClickCount > 1 && ClickCount % 2 == 0;

        /// <summary>Should be invoked exactly once per Update tick.<para/>
        /// This method will invoke any pending mouse events on its <see cref="Handlers"/> where <see cref="MouseHandler.IsManualUpdate"/> is false.</summary>
        public void UpdateHandlers()
        {
            MouseHandler[] OrderedHandlers = _OrderedHandlers ??= InputHandlerOrder.Build(_Handlers, static x => x.UpdatePriority);
            for (var i = 0; i < OrderedHandlers.Length; i++)
            {
                OrderedHandlers[i].AutoUpdate();
            }
        }

        public static ButtonState GetButtonState(MouseState State, MouseButton Button) => Button switch
        {
            MouseButton.Left => State.LeftButton,
            MouseButton.Middle => State.MiddleButton,
            MouseButton.Right => State.RightButton,
            _ => throw new NotImplementedException($"Unrecognized {nameof(MouseButton)}: {Button}")
        };
    }
}
