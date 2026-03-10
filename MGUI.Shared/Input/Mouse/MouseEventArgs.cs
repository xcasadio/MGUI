using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Shared.Input.Mouse
{
    internal sealed class MouseClickSequenceRoutingMetadata
    {
        public long? ValidatedTargetId { get; set; }
        public WeakReference<IMouseHandlerHost> ValidatedTargetHost { get; set; }
    }

    public class BaseMouseScrolledEventArgs : HandledByEventArgs<IMouseHandlerHost>
    {
        public MouseTracker Tracker { get; }

        public int ScrollWheelDelta { get; }
        public Point Position { get; }
        public Vector2 AdjustedPosition(IMouseHandlerHost Handler) => Position.ToVector2() + Handler.GetOffset();

        public BaseMouseScrolledEventArgs(MouseTracker Tracker, int ScrollWheelDelta, Point Position)
            : base()
        {
            this.Tracker = Tracker;
            this.ScrollWheelDelta = ScrollWheelDelta;
            this.Position = Position;
        }
    }

    public class BaseMouseMovedEventArgs : EventArgs
    {
        public MouseTracker Tracker { get; }

        public Point PreviousPosition { get; }
        public Point CurrentPosition { get; }
        public Point PositionDelta => CurrentPosition - PreviousPosition;

        public Vector2 AdjustedPreviousPosition(IMouseHandlerHost Handler) => PreviousPosition.ToVector2() + Handler.GetOffset();
        public Vector2 AdjustedCurrentPosition(IMouseHandlerHost Handler) => CurrentPosition.ToVector2() + Handler.GetOffset();

        public BaseMouseMovedEventArgs(MouseTracker Tracker, Point PreviousPosition, Point CurrentPosition)
        {
            this.Tracker = Tracker;
            this.PreviousPosition = PreviousPosition;
            this.CurrentPosition = CurrentPosition;
        }
    }

    #region Press / Release / Click
    /// <summary>Represents the stable technical identity of a candidate multi-click sequence.
    /// This sequence is used for internal routing and does not depend on the public <see cref="HandledByEventArgs{THandlerType}.HandledBy"/> state.</summary>
    public class MouseClickSequence
    {
        public long Id { get; }
        public MouseButton Button { get; }
        public TimeSpan StartedAt { get; }

        public BaseMouseClickedEventArgs FirstClick { get; private set; }
        public BaseMouseClickedEventArgs LatestClick { get; private set; }
        public int ClickCount => LatestClick?.ClickCount ?? 0;

        internal MouseClickSequenceRoutingMetadata RoutingMetadata { get; } = new();

        internal MouseClickSequence(long Id, MouseButton Button, TimeSpan StartedAt)
        {
            this.Id = Id;
            this.Button = Button;
            this.StartedAt = StartedAt;
        }

        internal void RegisterClick(BaseMouseClickedEventArgs ClickedArgs)
        {
            if (FirstClick == null)
                FirstClick = ClickedArgs;

            LatestClick = ClickedArgs;
        }

        internal bool CanBeAdoptedBy(long TargetId)
            => !RoutingMetadata.ValidatedTargetId.HasValue || RoutingMetadata.ValidatedTargetId == TargetId;

        internal bool TryAdoptTarget(long TargetId, IMouseHandlerHost TargetHost)
        {
            if (!CanBeAdoptedBy(TargetId))
                return false;

            RoutingMetadata.ValidatedTargetId = TargetId;
            RoutingMetadata.ValidatedTargetHost = TargetHost != null ? new(TargetHost) : null;
            return true;
        }

        internal bool IsOwnedBy(long TargetId)
            => RoutingMetadata.ValidatedTargetId == TargetId;

        internal bool TryGetValidatedTargetHost(out IMouseHandlerHost TargetHost)
        {
            TargetHost = null;

            if (RoutingMetadata.ValidatedTargetHost == null)
                return false;

            return RoutingMetadata.ValidatedTargetHost.TryGetTarget(out TargetHost);
        }
    }

    public class BaseMousePressedEventArgs : HandledByEventArgs<IMouseHandlerHost>
    {
        public MouseTracker Tracker { get; }

        public TimeSpan PressedAt { get; }
        public MouseButton Button { get; }
        public Point Position { get; }
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
    }

    public class BaseMouseReleasedEventArgs : HandledByEventArgs<IMouseHandlerHost>
    {
        public MouseTracker Tracker { get; }

        public BaseMousePressedEventArgs PressedArgs { get; }

        public TimeSpan ReleasedAt { get; }
        public MouseButton Button { get; }
        public Point Position { get; }
        public Vector2 AdjustedPosition(IMouseHandlerHost Handler) => Position.ToVector2() + Handler.GetOffset();

        public TimeSpan HeldDuration => ReleasedAt.Subtract(PressedArgs.PressedAt);

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
            this.ReleasedAt = ReleasedAt;
            this.PressedArgs = PressedArgs;
            this.Button = Button;
            this.Position = Position;
        }
    }

    public class BaseMouseClickedEventArgs : HandledByEventArgs<IMouseHandlerHost>
    {
        public MouseTracker Tracker { get; }

        /// <summary>The release event that completed this click.</summary>
        public BaseMouseReleasedEventArgs ReleasedArgs { get; }
        public BaseMousePressedEventArgs PressedArgs => ReleasedArgs?.PressedArgs;

        /// <summary>The previous click in the same candidate multi-click sequence, if any.</summary>
        public BaseMouseClickedEventArgs PreviousClickInSequence { get; }

        /// <summary>The explicit multi-click sequence that this click belongs to.
        /// This sequence provides routing identity independently from the public handled state.</summary>
        public MouseClickSequence Sequence { get; }

        /// <summary>An identifier shared by clicks that belong to the same candidate multi-click sequence.</summary>
        public long MultiClickSequenceId => Sequence?.Id ?? 0;

        /// <summary>The raw click count within the current candidate multi-click sequence.</summary>
        public int ClickCount { get; }

        /// <summary>True when this click completes a recognized double-click pair within the current sequence, such as click counts 2, 4, 6, etc.</summary>
        public bool IsDoubleClick => MouseTracker.IsDoubleClickCount(ClickCount);

        /// <summary>True when this click belongs to any multi-click sequence.</summary>
        public bool IsMultiClick => ClickCount > 1;

        public MouseButton Button { get; }
        public Point Position { get; }
        public Vector2 AdjustedPosition(IMouseHandlerHost Handler) => Position.ToVector2() + Handler.GetOffset();

        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Left"/></summary>
        public bool IsLMB => Button == MouseButton.Left;
        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Right"/></summary>
        public bool IsRMB => Button == MouseButton.Right;
        /// <summary>True if the <see cref="Button"/> associated with this event is <see cref="MouseButton.Middle"/></summary>
        public bool IsMMB => Button == MouseButton.Middle;

        public BaseMouseClickedEventArgs(MouseTracker Tracker, BaseMouseReleasedEventArgs ReleasedArgs, MouseButton Button, Point Position,
            int ClickCount = 1, BaseMouseClickedEventArgs PreviousClickInSequence = null, MouseClickSequence Sequence = null)
            : base()
        {
            this.Tracker = Tracker;
            this.ReleasedArgs = ReleasedArgs;
            this.Button = Button;
            this.Position = Position;
            this.ClickCount = Math.Max(1, ClickCount);
            this.PreviousClickInSequence = PreviousClickInSequence;
            this.Sequence = Sequence;

            this.Sequence?.RegisterClick(this);
        }
    }
    #endregion Press / Release / Click

    #region Drag
    public class BaseMouseDragStartEventArgs : HandledByEventArgs<IMouseHandlerHost>
    {
        public MouseTracker Tracker { get; }

        public TimeSpan StartedAt { get; }
        public MouseButton Button { get; }
        public Point Position { get; }
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
    }

    public class BaseMouseDraggedEventArgs// : HandledByEventArgs<IMouseHandler>
    {
        public MouseTracker Tracker { get; }

        public BaseMouseDragStartEventArgs DragStartArgs { get; }

        public TimeSpan Timestamp { get; }
        public MouseButton Button { get; }
        public Point Position { get; }

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

        /// <summary>Returns the distance the mouse has moved from the original position that the drag originated from, to its current position.</summary>
        /// <param name="SquaredDistance">True to retrieve squared distance, to improve performance by avoiding square root computation</param>
        public double GetTotalDistanceMoved(bool SquaredDistance = false)
        {
            double DeltaX = PositionDelta.X;
            double DeltaY = PositionDelta.Y;
            double DistanceSquared = DeltaX * DeltaX + DeltaY * DeltaY;
            if (SquaredDistance)
                return DistanceSquared;
            else
                return Math.Sqrt(DistanceSquared);
        }

        /// <summary>This is currently not implemented - it does nothing.</summary>
        public void SetHandled<T>(T HandledBy, bool OverwriteIfAlreadyHandled) { }
    }

    public class BaseMouseDragEndEventArgs// : HandledByEventArgs<IMouseHandler>
    {
        public MouseTracker Tracker { get; }

        public BaseMouseDragStartEventArgs DragStartArgs { get; }

        public TimeSpan Timestamp { get; }
        public MouseButton Button { get; }
        private Point Position { get; }

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

        /// <summary>Returns the distance the mouse has moved from the original position that the drag originated from, to its current position.</summary>
        /// <param name="SquaredDistance">True to retrieve squared distance, to improve performance by avoiding square root computation</param>
        public double GetTotalDistanceMoved(bool SquaredDistance = false)
        {
            double DeltaX = PositionDelta.X;
            double DeltaY = PositionDelta.Y;
            double DistanceSquared = DeltaX * DeltaX + DeltaY * DeltaY;
            if (SquaredDistance)
                return DistanceSquared;
            else
                return Math.Sqrt(DistanceSquared);
        }

        /// <summary>This is currently not implemented - it does nothing.</summary>
        public void SetHandled<T>(T HandledBy, bool OverwriteIfAlreadyHandled) { }
    }
    #endregion Drag
}
