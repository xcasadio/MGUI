using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Shared.Input.Mouse
{
    public enum DragStartCondition
    {
        /// <summary>The <see cref="MouseHandler.DragStart"/> event will be invoked immediately after the mouse has been pressed inside the viewport.</summary>
        MousePressed,
        /// <summary>The <see cref="MouseHandler.DragStart"/> event will be invoked after the mouse has moved by at least <see cref="MouseTracker.DragThreshold"/> number of pixels, after being pressed inside the viewport.</summary>
        MouseMovedAfterPress,
        /// <summary>The <see cref="MouseHandler.DragStart"/> event will be invoked immediately after the mouse has been pressed inside the viewport<br/>
        /// AND again after the mouse has moved by at least <see cref="MouseTracker.DragThreshold"/> number of pixels, after being pressed inside the viewport.<para/>
        /// Highly recommended to avoid using this value, only intended for specialized use-cases.</summary>
        Both
    }

    /// <summary>Exposes several mouse-related events that you can subscribe and respond to, such as <see cref="MouseHandler.Entered"/>, <see cref="MouseHandler.Dragged"/>, <see cref="MouseHandler.LMBPressedInside"/> etc.<para/>
    /// This class is instantiated via: <see cref="MouseTracker.CreateHandler{T}(T, double?, bool, bool, bool)"/></summary>
    public class MouseHandler
    {
        /// <summary>Only includes distinct values. Does not include combined values such as <see cref="DragStartCondition.Both"/></summary>
        public static readonly IReadOnlyList<DragStartCondition> DragStartConditions = Enum.GetValues(typeof(DragStartCondition)).Cast<DragStartCondition>().Where(x => x != DragStartCondition.Both).ToList();
        public static readonly ReadOnlyCollection<MouseButton> MouseButtons = Enum.GetValues(typeof(MouseButton)).Cast<MouseButton>().ToList().AsReadOnly();

        public MouseTracker Tracker { get; }
        public IMouseHandlerHost Owner { get; }
        public double? UpdatePriority { get; }
        public bool IsManualUpdate => !UpdatePriority.HasValue;

        private static long _NextTargetIdentityId = 1;
        private long TargetIdentityId { get; }

        /// <summary>If true, <see cref="HandledByEventArgs{THandlerType}.HandledBy"/> will always be set to <see cref="Owner"/> after invoking an event.</summary>
        private bool AlwaysHandlesEvents { get; }
        /// <summary>If true, events will still be invoked even if <see cref="HandledByEventArgs{THandlerType}.IsHandled"/> is true.</summary>
        private bool InvokeEvenIfHandled { get; }
        /// <summary>If true, events will still be invoked even if <see cref="HandledByEventArgs{THandlerType}.IsHandled"/> is true, 
        /// as long as the handler is the same as <see cref="Owner"/></summary>
        private bool InvokeIfHandledBySelf { get; }

        public DragStartCondition DragStartCondition { get; set; } = DragStartCondition.MouseMovedAfterPress;

        public override string ToString() => $"{nameof(MouseHandler)}: {nameof(Owner)} = {Owner}";

        internal MouseHandler(MouseTracker Tracker, IMouseHandlerHost Owner, double? UpdatePriority, bool AlwaysHandlesEvents, bool InvokeEvenIfHandled, bool InvokeIfHandledBySelf)
        {
            this.Tracker = Tracker;
            this.Owner = Owner;
            this.UpdatePriority = UpdatePriority;
            this.AlwaysHandlesEvents = AlwaysHandlesEvents;
            this.InvokeEvenIfHandled = InvokeEvenIfHandled;
            this.InvokeIfHandledBySelf = InvokeIfHandledBySelf;
            this.TargetIdentityId = System.Threading.Interlocked.Increment(ref _NextTargetIdentityId);
        }

        private bool IsInside(Vector2 Position) => IsInside(Position, Owner.GetOffset());
        private bool IsInside(Point Position, Vector2 Offset) => IsInside(Position.ToVector2(), Offset);
        private bool IsInside(Vector2 Position, Vector2 Offset) => Owner.IsInside(Position + Offset);
        private void AdoptClickSequence(BaseMouseClickedEventArgs Args)
            => Args.Sequence?.TryAdoptTarget(TargetIdentityId, Owner);
        private bool OwnsClickSequence(BaseMouseClickedEventArgs Args)
            => Args.Sequence?.IsOwnedBy(TargetIdentityId) == true;
        private bool IsLogicalAncestorOfClickSequenceTarget(BaseMouseClickedEventArgs Args)
        {
            if (Args?.Sequence == null || !Args.Sequence.TryGetValidatedTargetHost(out IMouseHandlerHost TargetHost))
            {
                return false;
            }

            IMouseHandlerHost Current = TargetHost.GetMouseInputParent();
            while (Current != null)
            {
                if (ReferenceEquals(Current, Owner))
                {
                    return true;
                }

                Current = Current.GetMouseInputParent();
            }

            return false;
        }
        private bool MatchesClickSequenceTarget(BaseMouseClickedEventArgs Args)
            => OwnsClickSequence(Args) || IsLogicalAncestorOfClickSequenceTarget(Args);

        /// <summary>True if the mouse is currently inside the viewport</summary>
        public bool IsHovered => IsInside(Tracker.CurrentState.Position.ToVector2(), Owner.GetOffset());

        public bool IsButtonPressedInside(MouseButton Button) => Tracker.IsPressedInside(Button, Owner);

        #region Events
        #region Backing fields for all events
        private EventHandler<BaseMouseScrolledEventArgs> _scrolled;
        private EventHandler<BaseMouseMovedEventArgs> _movedInside, _movedOutside, _entered, _exited;
        private EventHandler<BaseMousePressedEventArgs> _pressedInside, _lmbPressedInside, _mmbPressedInside, _rmbPressedInside, _pressedOutside;
        private EventHandler<BaseMouseReleasedEventArgs> _releasedInside, _lmbReleasedInside, _mmbReleasedInside, _rmbReleasedInside, _releasedOutside;
        private EventHandler<BaseMouseClickedEventArgs> _clickedInside, _lmbClickedInside, _mmbClickedInside, _rmbClickedInside, _clickedOutside;
        private EventHandler<BaseMouseClickedEventArgs> _doubleClickedInside, _lmbDoubleClickedInside, _mmbDoubleClickedInside, _rmbDoubleClickedInside, _doubleClickedOutside;
        private EventHandler<BaseMouseDragStartEventArgs> _dragStart, _dragStartOutside;
        private EventHandler<BaseMouseDraggedEventArgs> _dragged;
        private EventHandler<BaseMouseDragEndEventArgs> _dragEnd;
        #endregion Backing fields

        #region Cached monitoring flags
        private bool _hasSubscribedEvents;
        private bool _isMonitoringScroll;
        private bool _isMonitoringMovement;
        private bool _isMonitoringPressed;
        private bool _isMonitoringReleased;
        private bool _isMonitoringClicked;
        private bool _isMonitoringClicks;
        private bool _isMonitoringDrag;

        private void RecomputeMonitoringFlags()
        {
            _isMonitoringScroll  = _scrolled != null;
            _isMonitoringDrag    = _dragStart != null || _dragStartOutside != null || _dragged != null || _dragEnd != null;
            _isMonitoringMovement = _movedInside != null || _movedOutside != null || _entered != null || _exited != null || _isMonitoringDrag;
            _isMonitoringPressed  = _pressedInside != null || _lmbPressedInside != null || _mmbPressedInside != null || _rmbPressedInside != null || _pressedOutside != null;
            _isMonitoringReleased = _releasedInside != null || _lmbReleasedInside != null || _mmbReleasedInside != null || _rmbReleasedInside != null || _releasedOutside != null;
            _isMonitoringClicked  = _clickedInside != null || _lmbClickedInside != null || _mmbClickedInside != null || _rmbClickedInside != null || _clickedOutside != null
                || _doubleClickedInside != null || _lmbDoubleClickedInside != null || _mmbDoubleClickedInside != null || _rmbDoubleClickedInside != null || _doubleClickedOutside != null;
            _isMonitoringClicks   = _isMonitoringPressed || _isMonitoringReleased || _isMonitoringClicked;
            _hasSubscribedEvents  = _isMonitoringScroll || _isMonitoringMovement || _isMonitoringClicks || _isMonitoringDrag;
        }
        #endregion Cached monitoring flags

        /// <summary>True if at least one event has been subscribed to.<para/>
        /// If false, the Update method may return early under the assumption that no further processing is needed.</summary>
        private bool HasSubscribedEvents => _hasSubscribedEvents;
        /// <summary>True if at least one scroll event has been subscribed to</summary>
        private bool IsMonitoringScroll => _isMonitoringScroll;
        /// <summary>True if at least one move event has been subscribed to</summary>
        private bool IsMonitoringMovement => _isMonitoringMovement;
        /// <summary>True if at least one press/release/click event has been subscribed to</summary>
        private bool IsMonitoringClicks => _isMonitoringClicks;
        /// <summary>True if at least one drag event has been subscribed to</summary>
        private bool IsMonitoringDrag => _isMonitoringDrag;

        /// <summary>Invoked when the mouse wheel is scrolled overtop of the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseScrolledEventArgs> Scrolled
        {
            add    { _scrolled += value; RecomputeMonitoringFlags(); }
            remove { _scrolled -= value; RecomputeMonitoringFlags(); }
        }

        #region Movement
        /// <summary>Invoked when the mouse moves to a different position, and that position is inside of the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseMovedEventArgs> MovedInside
        {
            add    { _movedInside += value; RecomputeMonitoringFlags(); }
            remove { _movedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when the mouse moves to a different position, and that position is outside of the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseMovedEventArgs> MovedOutside
        {
            add    { _movedOutside += value; RecomputeMonitoringFlags(); }
            remove { _movedOutside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when the mouse enters the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseMovedEventArgs> Entered
        {
            add    { _entered += value; RecomputeMonitoringFlags(); }
            remove { _entered -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when the mouse exits the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseMovedEventArgs> Exited
        {
            add    { _exited += value; RecomputeMonitoringFlags(); }
            remove { _exited -= value; RecomputeMonitoringFlags(); }
        }
        #endregion Movement

        #region Pressed
        /// <summary>Invoked when any <see cref="MouseButton"/> is pressed while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMousePressedEventArgs> PressedInside
        {
            add    { _pressedInside += value; RecomputeMonitoringFlags(); }
            remove { _pressedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when <see cref="MouseButton.Left"/> is pressed while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMousePressedEventArgs> LMBPressedInside
        {
            add    { _lmbPressedInside += value; RecomputeMonitoringFlags(); }
            remove { _lmbPressedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when <see cref="MouseButton.Middle"/> is pressed while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMousePressedEventArgs> MMBPressedInside
        {
            add    { _mmbPressedInside += value; RecomputeMonitoringFlags(); }
            remove { _mmbPressedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when <see cref="MouseButton.Right"/> is pressed while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMousePressedEventArgs> RMBPressedInside
        {
            add    { _rmbPressedInside += value; RecomputeMonitoringFlags(); }
            remove { _rmbPressedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when any <see cref="MouseButton"/> is pressed while the mouse is currently outside of the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMousePressedEventArgs> PressedOutside
        {
            add    { _pressedOutside += value; RecomputeMonitoringFlags(); }
            remove { _pressedOutside -= value; RecomputeMonitoringFlags(); }
        }
        #endregion Pressed

        #region Released
        /// <summary>Invoked when any <see cref="MouseButton"/> is released while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseReleasedEventArgs> ReleasedInside
        {
            add    { _releasedInside += value; RecomputeMonitoringFlags(); }
            remove { _releasedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when <see cref="MouseButton.Left"/> is released while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseReleasedEventArgs> LMBReleasedInside
        {
            add    { _lmbReleasedInside += value; RecomputeMonitoringFlags(); }
            remove { _lmbReleasedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when <see cref="MouseButton.Middle"/> is released while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseReleasedEventArgs> MMBReleasedInside
        {
            add    { _mmbReleasedInside += value; RecomputeMonitoringFlags(); }
            remove { _mmbReleasedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when <see cref="MouseButton.Right"/> is released while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseReleasedEventArgs> RMBReleasedInside
        {
            add    { _rmbReleasedInside += value; RecomputeMonitoringFlags(); }
            remove { _rmbReleasedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when any <see cref="MouseButton"/> is released while the mouse is currently outside of the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseReleasedEventArgs> ReleasedOutside
        {
            add    { _releasedOutside += value; RecomputeMonitoringFlags(); }
            remove { _releasedOutside -= value; RecomputeMonitoringFlags(); }
        }
        #endregion Released

        #region Clicked
        /// <summary>Invoked when any <see cref="MouseButton"/> is clicked while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseClickedEventArgs> ClickedInside
        {
            add    { _clickedInside += value; RecomputeMonitoringFlags(); }
            remove { _clickedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when <see cref="MouseButton.Left"/> is clicked while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseClickedEventArgs> LMBClickedInside
        {
            add    { _lmbClickedInside += value; RecomputeMonitoringFlags(); }
            remove { _lmbClickedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when <see cref="MouseButton.Middle"/> is clicked while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseClickedEventArgs> MMBClickedInside
        {
            add    { _mmbClickedInside += value; RecomputeMonitoringFlags(); }
            remove { _mmbClickedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when <see cref="MouseButton.Right"/> is clicked while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseClickedEventArgs> RMBClickedInside
        {
            add    { _rmbClickedInside += value; RecomputeMonitoringFlags(); }
            remove { _rmbClickedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when any <see cref="MouseButton"/> is clicked while the mouse is currently outside of the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseClickedEventArgs> ClickedOutside
        {
            add    { _clickedOutside += value; RecomputeMonitoringFlags(); }
            remove { _clickedOutside -= value; RecomputeMonitoringFlags(); }
        }

        /// <summary>Invoked when any <see cref="MouseButton"/> is double-clicked while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseClickedEventArgs> DoubleClickedInside
        {
            add    { _doubleClickedInside += value; RecomputeMonitoringFlags(); }
            remove { _doubleClickedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when <see cref="MouseButton.Left"/> is double-clicked while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseClickedEventArgs> LMBDoubleClickedInside
        {
            add    { _lmbDoubleClickedInside += value; RecomputeMonitoringFlags(); }
            remove { _lmbDoubleClickedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when <see cref="MouseButton.Middle"/> is double-clicked while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseClickedEventArgs> MMBDoubleClickedInside
        {
            add    { _mmbDoubleClickedInside += value; RecomputeMonitoringFlags(); }
            remove { _mmbDoubleClickedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when <see cref="MouseButton.Right"/> is double-clicked while the mouse is currently inside the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseClickedEventArgs> RMBDoubleClickedInside
        {
            add    { _rmbDoubleClickedInside += value; RecomputeMonitoringFlags(); }
            remove { _rmbDoubleClickedInside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when any <see cref="MouseButton"/> is double-clicked while the mouse is currently outside of the <see cref="Owner"/>'s viewport.</summary>
        public event EventHandler<BaseMouseClickedEventArgs> DoubleClickedOutside
        {
            add    { _doubleClickedOutside += value; RecomputeMonitoringFlags(); }
            remove { _doubleClickedOutside -= value; RecomputeMonitoringFlags(); }
        }
        #endregion Clicked

        #region Drag
        /// <summary>Invoked when any <see cref="MouseButton"/> is pressed while the mouse is currently inside the <see cref="Owner"/>'s viewport.<br/>
        /// If <see cref="DragStartCondition"/> is <see cref="DragStartCondition.MouseMovedAfterPress"/>, then will also verify that the mouse has moved by at least <see cref="MouseTracker.DragThreshold"/> while pressed before invoking the event.</summary>
        public event EventHandler<BaseMouseDragStartEventArgs> DragStart
        {
            add    { _dragStart += value; RecomputeMonitoringFlags(); }
            remove { _dragStart -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when any <see cref="MouseButton"/> is pressed while the mouse is currently outside the <see cref="Owner"/>'s viewport.<br/>
        /// If <see cref="DragStartCondition"/> is <see cref="DragStartCondition.MouseMovedAfterPress"/>, then will also verify that the mouse has moved by at least <see cref="MouseTracker.DragThreshold"/> while pressed before invoking the event.<para/>
        /// You should probably subscribe to <see cref="DragStart"/> instead.</summary>
        public event EventHandler<BaseMouseDragStartEventArgs> DragStartOutside
        {
            add    { _dragStartOutside += value; RecomputeMonitoringFlags(); }
            remove { _dragStartOutside -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when <see cref="DragStart"/> is already in progress, and then the mouse moves.</summary>
        public event EventHandler<BaseMouseDraggedEventArgs> Dragged
        {
            add    { _dragged += value; RecomputeMonitoringFlags(); }
            remove { _dragged -= value; RecomputeMonitoringFlags(); }
        }
        /// <summary>Invoked when <see cref="DragStart"/> is currently in progress, and then the pressed <see cref="MouseButton"/> is released.</summary>
        public event EventHandler<BaseMouseDragEndEventArgs> DragEnd
        {
            add    { _dragEnd += value; RecomputeMonitoringFlags(); }
            remove { _dragEnd -= value; RecomputeMonitoringFlags(); }
        }
        #endregion Drag
        #endregion Events

        /// <summary>Should only be invoked via <see cref="MouseTracker.UpdateHandlers"/>></summary>
        internal void AutoUpdate()
        {
            if (IsManualUpdate)
            {
                throw new InvalidOperationException($"{nameof(MouseHandler)}.{nameof(AutoUpdate)} should only be invoked on {nameof(MouseHandler)}s where {nameof(IsManualUpdate)} is false.");
            }

            InvokeQueuedEvents();
        }

        /// <summary>Should be invoked exactly once per Update tick, but only on <see cref="MouseHandler"/>s where <see cref="IsManualUpdate"/> is true.<para/>
        /// This method will invoke any pending mouse events.</summary>
        public void ManualUpdate()
        {
            if (!IsManualUpdate)
            {
                throw new InvalidOperationException($"{nameof(MouseHandler)}.{nameof(ManualUpdate)} should only be invoked on {nameof(MouseHandler)}s where {nameof(IsManualUpdate)} is true.");
            }

            InvokeQueuedEvents();
        }

        private void InvokeQueuedEvents()
        {
            if (IsValid && HasSubscribedEvents)
            {
                Vector2 Offset = Owner.GetOffset();

                //  Invoke scroll/move/click events
                bool CanReceiveInputs = Owner.CanReceiveMouseInput();
                if (CanReceiveInputs)
                {
                    //  Invoke Scrolled event
                    if (Tracker.CurrentScrollEvent != null && _isMonitoringScroll && IsInside(Tracker.CurrentScrollEvent.Position, Offset))
                    {
                        if (InvokeEvenIfHandled || !Tracker.CurrentScrollEvent.IsHandled)
                        {
                            _scrolled.Invoke(this, Tracker.CurrentScrollEvent);
                            if (AlwaysHandlesEvents)
                            {
                                Tracker.CurrentScrollEvent.SetHandledBy(Owner, false);
                            }
                        }
                    }

                    //  Invoke mouse moved events
                    if (Tracker.CurrentMoveEvent != null && _isMonitoringMovement)
                    {
                        bool WasHovering = IsInside(Tracker.CurrentMoveEvent.PreviousPosition, Offset);
                        bool IsHovering = IsInside(Tracker.CurrentMoveEvent.CurrentPosition, Offset);

                        if (IsHovering)
                        {
                            _movedInside?.Invoke(this, Tracker.CurrentMoveEvent);
                        }

                        if (!IsHovering)
                        {
                            _movedOutside?.Invoke(this, Tracker.CurrentMoveEvent);
                        }

                        if (!WasHovering && IsHovering)
                        {
                            _entered?.Invoke(this, Tracker.CurrentMoveEvent);
                        }

                        if (WasHovering && !IsHovering)
                        {
                            _exited?.Invoke(this, Tracker.CurrentMoveEvent);
                        }
                    }

                    if (Tracker.HasCurrentButtonEvents && _isMonitoringClicks)
                    {
                        //  Invoke mouse pressed events
                        if (Tracker.HasCurrentButtonPressedEvents && _isMonitoringPressed)
                        {
                            foreach (MouseButton Button in MouseButtons)
                            {
                                BaseMousePressedEventArgs Args = Tracker.CurrentButtonPressedEvents[Button];
                                if (Args != null)
                                {
                                    bool IsPressedInside = IsInside(Args.Position, Offset);
                                    if (IsPressedInside)
                                    {
                                        if ((InvokeEvenIfHandled || !Args.IsHandled || (InvokeIfHandledBySelf && Args.HandledBy == Owner)) && _pressedInside != null)
                                        {
                                            _pressedInside.Invoke(this, Args);
                                            if (AlwaysHandlesEvents)
                                            {
                                                Args.SetHandledBy(Owner, false);
                                            }
                                        }

                                        if (InvokeEvenIfHandled || !Args.IsHandled || (InvokeIfHandledBySelf && Args.HandledBy == Owner))
                                        {
                                            switch (Button)
                                            {
                                                case MouseButton.Left:
                                                    if (_lmbPressedInside != null)
                                                    {
                                                        _lmbPressedInside.Invoke(this, Args);
                                                        if (AlwaysHandlesEvents)
                                                        {
                                                            Args.SetHandledBy(Owner, false);
                                                        }
                                                    }
                                                    break;
                                                case MouseButton.Middle:
                                                    if (_mmbPressedInside != null)
                                                    {
                                                        _mmbPressedInside.Invoke(this, Args);
                                                        if (AlwaysHandlesEvents)
                                                        {
                                                            Args.SetHandledBy(Owner, false);
                                                        }
                                                    }
                                                    break;
                                                case MouseButton.Right:
                                                    if (_rmbPressedInside != null)
                                                    {
                                                        _rmbPressedInside.Invoke(this, Args);
                                                        if (AlwaysHandlesEvents)
                                                        {
                                                            Args.SetHandledBy(Owner, false);
                                                        }
                                                    }
                                                    break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if ((InvokeEvenIfHandled || !Args.IsHandled || (InvokeIfHandledBySelf && Args.HandledBy == Owner)) && _pressedOutside != null)
                                        {
                                            _pressedOutside.Invoke(this, Args);
                                            if (AlwaysHandlesEvents)
                                            {
                                                Args.SetHandledBy(Owner, false);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        //  Invoke mouse released events
                        if (Tracker.HasCurrentButtonReleasedEvents && _isMonitoringReleased)
                        {
                            foreach (MouseButton Button in MouseButtons)
                            {
                                BaseMouseReleasedEventArgs Args = Tracker.CurrentButtonReleasedEvents[Button];
                                if (Args != null)
                                {
                                    bool IsReleasedInside = IsInside(Args.Position, Offset);
                                    if (IsReleasedInside)
                                    {
                                        if ((InvokeEvenIfHandled || !Args.IsHandled || (InvokeIfHandledBySelf && Args.HandledBy == Owner)) && _releasedInside != null)
                                        {
                                            _releasedInside.Invoke(this, Args);
                                            if (AlwaysHandlesEvents)
                                            {
                                                Args.SetHandledBy(Owner, false);
                                            }
                                        }

                                        if (InvokeEvenIfHandled || !Args.IsHandled || (InvokeIfHandledBySelf && Args.HandledBy == Owner))
                                        {
                                            switch (Button)
                                            {
                                                case MouseButton.Left:
                                                    if (_lmbReleasedInside != null)
                                                    {
                                                        _lmbReleasedInside.Invoke(this, Args);
                                                        if (AlwaysHandlesEvents)
                                                        {
                                                            Args.SetHandledBy(Owner, false);
                                                        }
                                                    }
                                                    break;
                                                case MouseButton.Middle:
                                                    if (_mmbReleasedInside != null)
                                                    {
                                                        _mmbReleasedInside.Invoke(this, Args);
                                                        if (AlwaysHandlesEvents)
                                                        {
                                                            Args.SetHandledBy(Owner, false);
                                                        }
                                                    }
                                                    break;
                                                case MouseButton.Right:
                                                    if (_rmbReleasedInside != null)
                                                    {
                                                        _rmbReleasedInside.Invoke(this, Args);
                                                        if (AlwaysHandlesEvents)
                                                        {
                                                            Args.SetHandledBy(Owner, false);
                                                        }
                                                    }
                                                    break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if ((InvokeEvenIfHandled || !Args.IsHandled || (InvokeIfHandledBySelf && Args.HandledBy == Owner)) && _releasedOutside != null)
                                        {
                                            _releasedOutside.Invoke(this, Args);
                                            if (AlwaysHandlesEvents)
                                            {
                                                Args.SetHandledBy(Owner, false);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        //  Invoke mouse clicked events
                        if (Tracker.HasCurrentButtonClickedEvents && _isMonitoringClicked)
                        {
                            foreach (MouseButton Button in MouseButtons)
                            {
                                BaseMouseClickedEventArgs Args = Tracker.CurrentButtonClickedEvents[Button];
                                if (Args != null)
                                {
                                    bool IsClickedInside = IsInside(Args.Position, Offset);
                                    if (IsClickedInside)
                                    {
                                        AdoptClickSequence(Args);

                                        if ((InvokeEvenIfHandled || !Args.IsHandled || (InvokeIfHandledBySelf && Args.HandledBy == Owner)) && (InvokeEvenIfHandled || !Args.ReleasedArgs.IsHandled || Args.ReleasedArgs.HandledBy == Owner) && _clickedInside != null)
                                        {
                                            _clickedInside.Invoke(this, Args);
                                            if (AlwaysHandlesEvents)
                                            {
                                                Args.SetHandledBy(Owner, false);
                                            }
                                        }

                                        if ((InvokeEvenIfHandled || !Args.IsHandled || (InvokeIfHandledBySelf && Args.HandledBy == Owner)) && (InvokeEvenIfHandled || !Args.ReleasedArgs.IsHandled || Args.ReleasedArgs.HandledBy == Owner))
                                        {
                                            switch (Button)
                                            {
                                                case MouseButton.Left:
                                                    if (_lmbClickedInside != null)
                                                    {
                                                        _lmbClickedInside.Invoke(this, Args);
                                                        if (AlwaysHandlesEvents)
                                                        {
                                                            Args.SetHandledBy(Owner, false);
                                                        }
                                                    }
                                                    break;
                                                case MouseButton.Middle:
                                                    if (_mmbClickedInside != null)
                                                    {
                                                        _mmbClickedInside.Invoke(this, Args);
                                                        if (AlwaysHandlesEvents)
                                                        {
                                                            Args.SetHandledBy(Owner, false);
                                                        }
                                                    }
                                                    break;
                                                case MouseButton.Right:
                                                    if (_rmbClickedInside != null)
                                                    {
                                                        _rmbClickedInside.Invoke(this, Args);
                                                        if (AlwaysHandlesEvents)
                                                        {
                                                            Args.SetHandledBy(Owner, false);
                                                        }
                                                    }
                                                    break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if ((InvokeEvenIfHandled || !Args.IsHandled || (InvokeIfHandledBySelf && Args.HandledBy == Owner)) && (InvokeEvenIfHandled || !Args.ReleasedArgs.IsHandled || Args.ReleasedArgs.HandledBy == Owner) && _clickedOutside != null)
                                        {
                                            _clickedOutside.Invoke(this, Args);
                                            if (AlwaysHandlesEvents)
                                            {
                                                Args.SetHandledBy(Owner, false);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (Tracker.HasCurrentButtonDoubleClickedEvents && _isMonitoringClicked)
                        {
                            foreach (MouseButton Button in MouseButtons)
                            {
                                BaseMouseClickedEventArgs Args = Tracker.CurrentButtonDoubleClickedEvents[Button];
                                if (Args != null)
                                {
                                    bool IsClickedInside = IsInside(Args.Position, Offset);
                                    bool CanInvoke = (InvokeEvenIfHandled || !Args.IsHandled || (InvokeIfHandledBySelf && Args.HandledBy == Owner))
                                        && (InvokeEvenIfHandled || !Args.ReleasedArgs.IsHandled || Args.ReleasedArgs.HandledBy == Owner)
                                        && MatchesClickSequenceTarget(Args);

                                    if (IsClickedInside)
                                    {
                                        if (CanInvoke && _doubleClickedInside != null)
                                        {
                                            _doubleClickedInside.Invoke(this, Args);
                                            if (AlwaysHandlesEvents)
                                            {
                                                Args.SetHandledBy(Owner, false);
                                            }
                                        }

                                        if (CanInvoke)
                                        {
                                            switch (Button)
                                            {
                                                case MouseButton.Left:
                                                    if (_lmbDoubleClickedInside != null)
                                                    {
                                                        _lmbDoubleClickedInside.Invoke(this, Args);
                                                        if (AlwaysHandlesEvents)
                                                        {
                                                            Args.SetHandledBy(Owner, false);
                                                        }
                                                    }
                                                    break;
                                                case MouseButton.Middle:
                                                    if (_mmbDoubleClickedInside != null)
                                                    {
                                                        _mmbDoubleClickedInside.Invoke(this, Args);
                                                        if (AlwaysHandlesEvents)
                                                        {
                                                            Args.SetHandledBy(Owner, false);
                                                        }
                                                    }
                                                    break;
                                                case MouseButton.Right:
                                                    if (_rmbDoubleClickedInside != null)
                                                    {
                                                        _rmbDoubleClickedInside.Invoke(this, Args);
                                                        if (AlwaysHandlesEvents)
                                                        {
                                                            Args.SetHandledBy(Owner, false);
                                                        }
                                                    }
                                                    break;
                                            }
                                        }
                                    }
                                    else if (CanInvoke && _doubleClickedOutside != null)
                                    {
                                        _doubleClickedOutside.Invoke(this, Args);
                                        if (AlwaysHandlesEvents)
                                        {
                                            Args.SetHandledBy(Owner, false);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                //  Invoke mouse drag events
                //  Note: Drag events are invoked even if !Owner.CanReceiveMouseInput(), to ensure that every DragStart event is still followed by a corresponding DragEnd event
                //  (because Owner.CanReceiveMouseInputs conditions could have changed during the drag, which could result in unexpected behavior)
                if (Tracker.HasCurrentDragEvents && _isMonitoringDrag)
                {
                    List<DragStartCondition> Conditions = new();
                    if (DragStartCondition == DragStartCondition.Both)
                    {
                        Conditions.Add(DragStartCondition.MouseMovedAfterPress);
                        Conditions.Add(DragStartCondition.MousePressed);
                    }
                    else
                    {
                        Conditions.Add(DragStartCondition);
                    }

                    foreach (DragStartCondition Condition in Conditions)
                    {
                        foreach (MouseButton Button in MouseButtons)
                        {
                            //  Drag started
                            if (CanReceiveInputs && (_dragStart != null || _dragStartOutside != null))
                            {
                                BaseMouseDragStartEventArgs DragStartPressed = Tracker.CurrentDragStartEvents[DragStartCondition.MousePressed][Button];
                                if (DragStartPressed != null && Condition == DragStartCondition.MousePressed && (InvokeEvenIfHandled || !DragStartPressed.IsHandled || (InvokeIfHandledBySelf && DragStartPressed.HandledBy == Owner)))
                                {
                                    bool IsMouseInsideViewport = IsInside(DragStartPressed.Position, Offset);

                                    if (IsMouseInsideViewport && _dragStart != null)
                                    {
                                        _dragStart.Invoke(this, DragStartPressed);
                                        if (AlwaysHandlesEvents)
                                        {
                                            DragStartPressed.SetHandledBy(Owner, false);
                                        }
                                    }
                                    else if (!IsMouseInsideViewport && _dragStartOutside != null)
                                    {
                                        _dragStartOutside.Invoke(this, DragStartPressed);
                                        if (AlwaysHandlesEvents)
                                        {
                                            DragStartPressed.SetHandledBy(Owner, false);
                                        }
                                    }
                                }

                                BaseMouseDragStartEventArgs DragStartMovedAfterPress = Tracker.CurrentDragStartEvents[DragStartCondition.MouseMovedAfterPress][Button];
                                if (DragStartMovedAfterPress != null && Condition == DragStartCondition.MouseMovedAfterPress && (InvokeEvenIfHandled || !DragStartMovedAfterPress.IsHandled || (InvokeIfHandledBySelf && DragStartMovedAfterPress.HandledBy == Owner)))
                                {
                                    bool IsMouseInsideViewport = IsInside(DragStartMovedAfterPress.Position, Offset);

                                    if (IsMouseInsideViewport && _dragStart != null)
                                    {
                                        _dragStart.Invoke(this, DragStartMovedAfterPress);
                                        if (AlwaysHandlesEvents)
                                        {
                                            DragStartMovedAfterPress.SetHandledBy(Owner, false);
                                        }
                                    }
                                    else if (!IsMouseInsideViewport && _dragStartOutside != null)
                                    {
                                        _dragStartOutside.Invoke(this, DragStartMovedAfterPress);
                                        if (AlwaysHandlesEvents)
                                        {
                                            DragStartMovedAfterPress.SetHandledBy(Owner, false);
                                        }
                                    }
                                }
                            }

                            //  Dragged
                            BaseMouseDraggedEventArgs DraggedArgs = Tracker.CurrentDraggedEvents[Condition][Button];
                            if (DraggedArgs != null && _dragged != null && (IsInside(DraggedArgs.StartPosition, Offset) || DraggedArgs.DragStartArgs.HandledBy == Owner)
                                && (InvokeEvenIfHandled || !DraggedArgs.DragStartArgs.IsHandled || DraggedArgs.DragStartArgs.HandledBy == Owner))
                            {
                                _dragged.Invoke(this, DraggedArgs);
                                if (AlwaysHandlesEvents)
                                {
                                    DraggedArgs.SetHandled(Owner, false);
                                }
                            }

                            //  Drag ended
                            BaseMouseDragEndEventArgs DragEndArgs = Tracker.CurrentDragEndEvents[Condition][Button];
                            if (DragEndArgs != null && _dragEnd != null && (IsInside(DragEndArgs.StartPosition, Offset) || DragEndArgs.DragStartArgs.HandledBy == Owner)
                                && (InvokeEvenIfHandled || !DragEndArgs.DragStartArgs.IsHandled || DragEndArgs.DragStartArgs.HandledBy == Owner))
                            {
                                _dragEnd.Invoke(this, DragEndArgs);
                                if (AlwaysHandlesEvents)
                                {
                                    DragEndArgs.SetHandled(Owner, false);
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool IsValid = true;
        /// <summary>Permanently invalidates this <see cref="MouseHandler"/> so that it will not receive and invoke any further mouse-related events.</summary>
        public void Unsubscribe()
        {
            Tracker.RemoveHandler(this);
            IsValid = false;
        }
    }
}
