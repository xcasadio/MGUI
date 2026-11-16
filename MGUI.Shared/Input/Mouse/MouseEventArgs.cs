using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Shared.Input.Mouse
{
    /// <summary>The target that adopted a <see cref="MouseClickSequence"/>. The weak reference is created at the first adoption and re-armed by
    /// <see cref="SetTarget"/> afterwards, so that adopting a target allocates nothing once the sequence of a button has adopted one.</summary>
    internal sealed class MouseClickSequenceRoutingMetadata
    {
        public long? ValidatedTargetId { get; private set; }
        private WeakReference<IMouseHandlerHost> _ValidatedTargetHost;

        /// <summary>Forgets the adopted target, as the metadata of a new sequence has none.</summary>
        public void Clear()
        {
            ValidatedTargetId = null;
            _ValidatedTargetHost?.SetTarget(null);
        }

        public void SetTarget(long TargetId, IMouseHandlerHost TargetHost)
        {
            ValidatedTargetId = TargetId;
            if (TargetHost == null)
            {
                _ValidatedTargetHost?.SetTarget(null);
            }
            else if (_ValidatedTargetHost == null)
            {
                _ValidatedTargetHost = new(TargetHost);
            }
            else
            {
                _ValidatedTargetHost.SetTarget(TargetHost);
            }
        }

        public bool TryGetTargetHost(out IMouseHandlerHost TargetHost)
        {
            TargetHost = null;
            return _ValidatedTargetHost != null && _ValidatedTargetHost.TryGetTarget(out TargetHost);
        }
    }

    /// <summary>Raised for a mouse wheel movement. <see cref="MouseTracker"/> reuses one instance from one wheel tick to the next (see
    /// <see cref="MouseTracker.CurrentScrollEvent"/>): read it during the tick, do not keep it. A handler that needs a value later copies the value.
    /// See ADR-0034.</summary>
    public class BaseMouseScrolledEventArgs : HandledByEventArgs<IMouseHandlerHost>
    {
        public MouseTracker Tracker { get; }

        public int ScrollWheelDelta { get; private set; }
        public Point Position { get; private set; }
        public Vector2 AdjustedPosition(IMouseHandlerHost Handler) => Position.ToVector2() + Handler.GetOffset();

        public BaseMouseScrolledEventArgs(MouseTracker Tracker, int ScrollWheelDelta, Point Position)
            : base()
        {
            this.Tracker = Tracker;
            this.ScrollWheelDelta = ScrollWheelDelta;
            this.Position = Position;
        }

        /// <summary>Refills this reused instance for a new wheel tick: new values, and the handled state back to not handled.</summary>
        internal void SetScroll(int ScrollWheelDelta, Point Position)
        {
            Reset();
            this.ScrollWheelDelta = ScrollWheelDelta;
            this.Position = Position;
        }
    }

    /// <summary>Raised for a mouse move. <see cref="MouseTracker"/> reuses one instance from one move to the next (see
    /// <see cref="MouseTracker.CurrentMoveEvent"/>): read it during the event, do not keep it.</summary>
    public class BaseMouseMovedEventArgs : EventArgs
    {
        public MouseTracker Tracker { get; }

        public Point PreviousPosition { get; private set; }
        public Point CurrentPosition { get; private set; }
        public Point PositionDelta => CurrentPosition - PreviousPosition;

        public Vector2 AdjustedPreviousPosition(IMouseHandlerHost Handler) => PreviousPosition.ToVector2() + Handler.GetOffset();
        public Vector2 AdjustedCurrentPosition(IMouseHandlerHost Handler) => CurrentPosition.ToVector2() + Handler.GetOffset();

        public BaseMouseMovedEventArgs(MouseTracker Tracker, Point PreviousPosition, Point CurrentPosition)
        {
            this.Tracker = Tracker;
            this.PreviousPosition = PreviousPosition;
            this.CurrentPosition = CurrentPosition;
        }

        /// <summary>Refills this reused instance for a new move.</summary>
        internal void SetPositions(Point PreviousPosition, Point CurrentPosition)
        {
            this.PreviousPosition = PreviousPosition;
            this.CurrentPosition = CurrentPosition;
        }
    }

    #region Press / Release / Click
    /// <summary>Represents the stable technical identity of a candidate multi-click sequence.
    /// This sequence is used for internal routing and does not depend on the public <see cref="HandledByEventArgs{THandlerType}.HandledBy"/> state.<para/>
    /// <see cref="MouseTracker"/> keeps one sequence per button and restarts it, with a new <see cref="Id"/>, its start time and no adopted target, at the first click of
    /// each sequence of that button: a sequence is valid until the next sequence of the same button. Read it during the event, do not keep it (keep its <see cref="Id"/>).
    /// See ADR-0034.</summary>
    public class MouseClickSequence
    {
        public long Id { get; private set; }
        public MouseButton Button { get; private set; }
        public TimeSpan StartedAt { get; private set; }

        /// <summary>The click count of the latest click of this sequence, 0 before its first click.</summary>
        public int ClickCount { get; private set; }

        internal MouseClickSequenceRoutingMetadata RoutingMetadata { get; } = new();

        internal MouseClickSequence(long Id, MouseButton Button, TimeSpan StartedAt)
        {
            this.Id = Id;
            this.Button = Button;
            this.StartedAt = StartedAt;
        }

        /// <summary>Restarts this reused sequence as a new one: a new identity, no click yet and no adopted target.</summary>
        internal void Restart(long Id, MouseButton Button, TimeSpan StartedAt)
        {
            this.Id = Id;
            this.Button = Button;
            this.StartedAt = StartedAt;
            ClickCount = 0;
            RoutingMetadata.Clear();
        }

        internal void RegisterClick(int ClickCount)
        {
            this.ClickCount = ClickCount;
        }

        internal bool CanBeAdoptedBy(long TargetId)
            => !RoutingMetadata.ValidatedTargetId.HasValue || RoutingMetadata.ValidatedTargetId == TargetId;

        internal bool TryAdoptTarget(long TargetId, IMouseHandlerHost TargetHost)
        {
            if (!CanBeAdoptedBy(TargetId))
            {
                return false;
            }

            RoutingMetadata.SetTarget(TargetId, TargetHost);
            return true;
        }

        internal bool IsOwnedBy(long TargetId)
            => RoutingMetadata.ValidatedTargetId == TargetId;

        internal bool TryGetValidatedTargetHost(out IMouseHandlerHost TargetHost)
            => RoutingMetadata.TryGetTargetHost(out TargetHost);
    }

    /// <summary>Raised when a mouse button is pressed. <see cref="MouseTracker"/> keeps one instance per button and refills it, with its handled state reset, at each press of that button:
    /// read it during the event, do not keep it. It stays valid, unchanged, until the next press of the same button, so while the button is held and during its release. A handler that needs a
    /// value later copies the value. See ADR-0034.</summary>
    public class BaseMousePressedEventArgs : HandledByEventArgs<IMouseHandlerHost>
    {
        public MouseTracker Tracker { get; }

        public TimeSpan PressedAt { get; private set; }
        public MouseButton Button { get; }
        public Point Position { get; private set; }
        public Vector2 AdjustedPosition(IMouseHandlerHost Handler) => Position.ToVector2() + Handler.GetOffset();

        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Left"/></summary>
        public bool IsLMB => Button == MouseButton.Left;
        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Right"/></summary>
        public bool IsRMB => Button == MouseButton.Right;
        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Middle"/></summary>
        public bool IsMMB => Button == MouseButton.Middle;

        public BaseMousePressedEventArgs(MouseTracker Tracker, MouseButton Button, Point Position, TimeSpan PressedAt)
            : base()
        {
            this.Tracker = Tracker;
            this.PressedAt = PressedAt;
            this.Button = Button;
            this.Position = Position;
        }

        /// <summary>Refills this reused instance for a new press of its button: new values, and the handled state back to not handled.</summary>
        internal void Set(Point Position, TimeSpan PressedAt)
        {
            Reset();
            this.Position = Position;
            this.PressedAt = PressedAt;
        }
    }

    /// <summary>Raised when a mouse button is released. <see cref="MouseTracker"/> keeps one instance per button and refills it, with its handled state reset, at each release of that button:
    /// read it during the event, do not keep it. It stays valid, unchanged, until the next release of the same button. <see cref="PressedArgs"/> designates the press instance of the same
    /// button (valid until the next press of that button), and <see cref="HeldDuration"/> is a value written when the release is filled. See ADR-0034.</summary>
    public class BaseMouseReleasedEventArgs : HandledByEventArgs<IMouseHandlerHost>
    {
        public MouseTracker Tracker { get; }

        public BaseMousePressedEventArgs PressedArgs { get; private set; }

        public TimeSpan ReleasedAt { get; private set; }
        public MouseButton Button { get; }
        public Point Position { get; private set; }
        public Vector2 AdjustedPosition(IMouseHandlerHost Handler) => Position.ToVector2() + Handler.GetOffset();

        public TimeSpan HeldDuration { get; private set; }

        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Left"/></summary>
        public bool IsLMB => Button == MouseButton.Left;
        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Right"/></summary>
        public bool IsRMB => Button == MouseButton.Right;
        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Middle"/></summary>
        public bool IsMMB => Button == MouseButton.Middle;

        public BaseMouseReleasedEventArgs(MouseTracker Tracker, BaseMousePressedEventArgs PressedArgs, MouseButton Button, Point Position, TimeSpan ReleasedAt)
            : base()
        {
            this.Tracker = Tracker;
            this.Button = Button;
            Fill(PressedArgs, Position, ReleasedAt);
        }

        /// <summary>Refills this reused instance for a new release of its button: new values, and the handled state back to not handled.</summary>
        internal void Set(BaseMousePressedEventArgs PressedArgs, Point Position, TimeSpan ReleasedAt)
        {
            Reset();
            Fill(PressedArgs, Position, ReleasedAt);
        }

        private void Fill(BaseMousePressedEventArgs PressedArgs, Point Position, TimeSpan ReleasedAt)
        {
            this.ReleasedAt = ReleasedAt;
            this.PressedArgs = PressedArgs;
            this.Position = Position;
            HeldDuration = PressedArgs == null ? TimeSpan.Zero : ReleasedAt.Subtract(PressedArgs.PressedAt);
        }
    }

    /// <summary>Raised when a mouse button is clicked. <see cref="MouseTracker"/> keeps one instance per button and refills it, with its handled state reset, at each click of that button:
    /// read it during the event, do not keep it. It stays valid, unchanged, until the next click of the same button. <see cref="ReleasedArgs"/> and <see cref="PressedArgs"/> designate the release and
    /// press instances of the same button (valid until the next release and the next press of that button), and <see cref="Sequence"/> the sequence of that button (valid until the next sequence
    /// of the button): a handler that needs a value of the click, such as <see cref="MultiClickSequenceId"/>, later copies the value. See ADR-0034.</summary>
    public class BaseMouseClickedEventArgs : HandledByEventArgs<IMouseHandlerHost>
    {
        public MouseTracker Tracker { get; }

        /// <summary>The release event that completed this click.</summary>
        public BaseMouseReleasedEventArgs ReleasedArgs { get; private set; }
        public BaseMousePressedEventArgs PressedArgs => ReleasedArgs?.PressedArgs;

        /// <summary>The explicit multi-click sequence that this click belongs to.
        /// This sequence provides routing identity independently from the public handled state.</summary>
        public MouseClickSequence Sequence { get; private set; }

        /// <summary>An identifier shared by clicks that belong to the same candidate multi-click sequence.</summary>
        public long MultiClickSequenceId => Sequence?.Id ?? 0;

        /// <summary>The raw click count within the current candidate multi-click sequence.</summary>
        public int ClickCount { get; private set; }

        /// <summary>True when this click completes a recognized double-click pair within the current sequence, such as click counts 2, 4, 6, etc.</summary>
        public bool IsDoubleClick => MouseTracker.IsDoubleClickCount(ClickCount);

        /// <summary>True when this click belongs to any multi-click sequence.</summary>
        public bool IsMultiClick => ClickCount > 1;

        public MouseButton Button { get; }
        public Point Position { get; private set; }
        public Vector2 AdjustedPosition(IMouseHandlerHost Handler) => Position.ToVector2() + Handler.GetOffset();

        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Left"/></summary>
        public bool IsLMB => Button == MouseButton.Left;
        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Right"/></summary>
        public bool IsRMB => Button == MouseButton.Right;
        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Middle"/></summary>
        public bool IsMMB => Button == MouseButton.Middle;

        public BaseMouseClickedEventArgs(MouseTracker Tracker, BaseMouseReleasedEventArgs ReleasedArgs, MouseButton Button, Point Position,
            int ClickCount = 1, MouseClickSequence Sequence = null)
            : base()
        {
            this.Tracker = Tracker;
            this.Button = Button;
            Fill(ReleasedArgs, Position, ClickCount, Sequence);
        }

        /// <summary>Refills this reused instance for a new click of its button: new values, and the handled state back to not handled.</summary>
        internal void Set(BaseMouseReleasedEventArgs ReleasedArgs, Point Position, int ClickCount, MouseClickSequence Sequence)
        {
            Reset();
            Fill(ReleasedArgs, Position, ClickCount, Sequence);
        }

        private void Fill(BaseMouseReleasedEventArgs ReleasedArgs, Point Position, int ClickCount, MouseClickSequence Sequence)
        {
            this.ReleasedArgs = ReleasedArgs;
            this.Position = Position;
            this.ClickCount = Math.Max(1, ClickCount);
            this.Sequence = Sequence;

            this.Sequence?.RegisterClick(this.ClickCount);
        }
    }
    #endregion Press / Release / Click

    #region Drag
    /// <summary>Raised when a drag starts. <see cref="MouseTracker"/> keeps one instance per drag start condition and per button and refills it, with its handled state reset, at each drag start
    /// of that condition and button: read it during the event, do not keep it. It stays valid, unchanged, until the next drag start of the same condition and button, so during the whole drag
    /// and its drag end, whose <see cref="BaseMouseDraggedEventArgs.DragStartArgs"/> and <see cref="BaseMouseDragEndEventArgs.DragStartArgs"/> designate it. See ADR-0034.</summary>
    public class BaseMouseDragStartEventArgs : HandledByEventArgs<IMouseHandlerHost>
    {
        public MouseTracker Tracker { get; }

        public TimeSpan StartedAt { get; private set; }
        public MouseButton Button { get; }
        public Point Position { get; private set; }
        public Vector2 AdjustedPosition(IMouseHandlerHost Handler) => Position.ToVector2() + Handler.GetOffset();

        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Left"/></summary>
        public bool IsLMB => Button == MouseButton.Left;
        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Right"/></summary>
        public bool IsRMB => Button == MouseButton.Right;
        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Middle"/></summary>
        public bool IsMMB => Button == MouseButton.Middle;

        public DragStartCondition Condition { get; }

        public BaseMouseDragStartEventArgs(MouseTracker Tracker, MouseButton Button, Point Position, DragStartCondition Condition, TimeSpan StartedAt)
        {
            this.Tracker = Tracker;
            this.StartedAt = StartedAt;
            this.Button = Button;
            this.Position = Position;
            this.Condition = Condition;
        }

        /// <summary>Refills this reused instance for a new drag of its condition and button: new values, and the handled state back to not handled.</summary>
        internal void Set(Point Position, TimeSpan StartedAt)
        {
            Reset();
            this.Position = Position;
            this.StartedAt = StartedAt;
        }
    }

    /// <summary>Raised for each move of a drag. <see cref="MouseTracker"/> reuses one instance per drag start condition and per button from one
    /// drag frame to the next (see <see cref="MouseTracker.CurrentDraggedEvents"/>): read it during the event, do not keep it.</summary>
    public class BaseMouseDraggedEventArgs// : HandledByEventArgs<IMouseHandler>
    {
        public MouseTracker Tracker { get; }

        public BaseMouseDragStartEventArgs DragStartArgs { get; private set; }

        public TimeSpan Timestamp { get; private set; }
        public MouseButton Button { get; }
        public Point Position { get; private set; }

        public Point StartPosition => DragStartArgs.Position;

        public Vector2 AdjustedPosition(IMouseHandlerHost Handler) => Position.ToVector2() + Handler.GetOffset();
        public Point PositionDelta => Position - DragStartArgs.Position;

        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Left"/></summary>
        public bool IsLMB => Button == MouseButton.Left;
        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Right"/></summary>
        public bool IsRMB => Button == MouseButton.Right;
        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Middle"/></summary>
        public bool IsMMB => Button == MouseButton.Middle;

        public BaseMouseDraggedEventArgs(MouseTracker Tracker, BaseMouseDragStartEventArgs DragStartArgs, MouseButton Button, Point Position, TimeSpan Timestamp)
        {
            this.Tracker = Tracker;
            this.DragStartArgs = DragStartArgs;
            this.Timestamp = Timestamp;
            this.Button = Button;
            this.Position = Position;
        }

        /// <summary>Refills this reused instance for a new drag frame of the same button and drag start condition.</summary>
        internal void Set(BaseMouseDragStartEventArgs DragStartArgs, Point Position, TimeSpan Timestamp)
        {
            this.DragStartArgs = DragStartArgs;
            this.Position = Position;
            this.Timestamp = Timestamp;
        }

        /// <summary>Returns the distance the mouse has moved from the original position that the drag originated from, to its current position.</summary>
        /// <param name="SquaredDistance">True to retrieve squared distance, to improve performance by avoiding square root computation</param>
        public double GetTotalDistanceMoved(bool SquaredDistance = false)
        {
            double DeltaX = PositionDelta.X;
            double DeltaY = PositionDelta.Y;
            double DistanceSquared = DeltaX * DeltaX + DeltaY * DeltaY;
            if (SquaredDistance)
            {
                return DistanceSquared;
            }
            else
            {
                return Math.Sqrt(DistanceSquared);
            }
        }

        /// <summary>This is currently not implemented - it does nothing.</summary>
        public void SetHandled<T>(T HandledBy, bool OverwriteIfAlreadyHandled) { }
    }

    /// <summary>Raised when a drag ends. <see cref="MouseTracker"/> keeps one instance per drag start condition and per button and refills it at each drag end of that condition and button:
    /// read it during the event, do not keep it. It stays valid, unchanged, until the next drag end of the same condition and button. A handler that needs a value later copies the value. See ADR-0034.</summary>
    public class BaseMouseDragEndEventArgs// : HandledByEventArgs<IMouseHandler>
    {
        public MouseTracker Tracker { get; }

        public BaseMouseDragStartEventArgs DragStartArgs { get; private set; }

        public TimeSpan Timestamp { get; private set; }
        public MouseButton Button { get; }
        private Point Position { get; set; }

        public Point StartPosition => DragStartArgs.Position;
        public Vector2 AdjustedStartPosition(IMouseHandlerHost Handler) => Position.ToVector2() + Handler.GetOffset();
        public Point EndPosition => Position;
        public Vector2 AdjustedEndPosition(IMouseHandlerHost Handler) => Position.ToVector2() + Handler.GetOffset();
        public Point PositionDelta => EndPosition - StartPosition;

        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Left"/></summary>
        public bool IsLMB => Button == MouseButton.Left;
        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Right"/></summary>
        public bool IsRMB => Button == MouseButton.Right;
        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Middle"/></summary>
        public bool IsMMB => Button == MouseButton.Middle;

        public BaseMouseDragEndEventArgs(MouseTracker Tracker, BaseMouseDragStartEventArgs DragStartArgs, MouseButton Button, Point Position, TimeSpan Timestamp)
        {
            this.Tracker = Tracker;
            this.DragStartArgs = DragStartArgs;
            this.Timestamp = Timestamp;
            this.Button = Button;
            this.Position = Position;
        }

        /// <summary>Refills this reused instance for a new drag end of its condition and button.</summary>
        internal void Set(BaseMouseDragStartEventArgs DragStartArgs, Point Position, TimeSpan Timestamp)
        {
            this.DragStartArgs = DragStartArgs;
            this.Position = Position;
            this.Timestamp = Timestamp;
        }

        /// <summary>Returns the distance the mouse has moved from the original position that the drag originated from, to its current position.</summary>
        /// <param name="SquaredDistance">True to retrieve squared distance, to improve performance by avoiding square root computation</param>
        public double GetTotalDistanceMoved(bool SquaredDistance = false)
        {
            double DeltaX = PositionDelta.X;
            double DeltaY = PositionDelta.Y;
            double DistanceSquared = DeltaX * DeltaX + DeltaY * DeltaY;
            if (SquaredDistance)
            {
                return DistanceSquared;
            }
            else
            {
                return Math.Sqrt(DistanceSquared);
            }
        }

        /// <summary>This is currently not implemented - it does nothing.</summary>
        public void SetHandled<T>(T HandledBy, bool OverwriteIfAlreadyHandled) { }
    }
    #endregion Drag
}
