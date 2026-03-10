using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Shared.Input.Keyboard
{
    public class KeyboardRepeatPolicy
    {
        public bool Enabled { get; set; } = true;
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(500);
        public TimeSpan Interval { get; set; } = TimeSpan.FromMilliseconds(1000.0 / 30.0);
        public Func<Keys, bool> CanRepeat { get; set; } = KeyboardTracker.ShouldRepeatKey;
    }

    /// <summary>Exposes several keyboard-related events that you can subscribe and respond to, such as <see cref="KeyboardHandler.Pressed"/>, <see cref="KeyboardHandler.Released"/>, <see cref="KeyboardHandler.Clicked"/>.<para/>
    /// This class is instantiated via: <see cref="KeyboardTracker.CreateHandler{T}(T, double?, bool, bool)"/></summary>
    public class KeyboardHandler
    {
        public static readonly ReadOnlyCollection<Keys> AllKeys = Enum.GetValues(typeof(Keys)).Cast<Keys>().ToList().AsReadOnly();

        public KeyboardTracker Tracker { get; }
        public IKeyboardHandlerHost Owner { get; }
        public double? UpdatePriority { get; }
        public bool IsManualUpdate => !UpdatePriority.HasValue;

        private static long _NextStreamOwnerId = 1;
        private long StreamOwnerId { get; }

        /// <summary>If true, <see cref="HandledByEventArgs{THandlerType}.HandledBy"/> will always be set to <see cref="Owner"/> after invoking an event.</summary>
        private bool AlwaysHandlesEvents { get; }
        /// <summary>If true, events will still be invoked even if <see cref="HandledByEventArgs{THandlerType}.IsHandled"/> is true.</summary>
        private bool InvokeEvenIfHandled { get; }

        public KeyboardRepeatPolicy RepeatPolicy { get; } = new();

        private readonly Dictionary<Keys, TimeSpan?> _LastRepeatedAt = AllKeys.ToDictionary(x => x, _ => (TimeSpan?)null);

        public override string ToString() => $"{nameof(KeyboardHandler)}: {nameof(Owner)} = {Owner}";

        internal KeyboardHandler(KeyboardTracker Tracker, IKeyboardHandlerHost Owner, double? UpdatePriority, bool AlwaysHandlesEvents, bool InvokeEvenIfHandled)
        {
            this.Tracker = Tracker;
            this.Owner = Owner;
            this.UpdatePriority = UpdatePriority;
            this.AlwaysHandlesEvents = AlwaysHandlesEvents;
            this.InvokeEvenIfHandled = InvokeEvenIfHandled;
            this.StreamOwnerId = System.Threading.Interlocked.Increment(ref _NextStreamOwnerId);
        }

        private void AdoptStreamIfHandled(BaseKeyPressedEventArgs PressedArgs)
        {
            if (PressedArgs?.IsHandled == true)
                PressedArgs.Stream?.TryAdoptOwner(StreamOwnerId);
        }

        private bool OwnsStream(KeyboardInputStream Stream)
            => Stream?.IsOwnedBy(StreamOwnerId) == true;

        #region Events
        private EventHandler<BaseKeyPressedEventArgs> _pressed;
        private EventHandler<BaseKeyReleasedEventArgs> _released;
        private EventHandler<BaseKeyClickedEventArgs> _clicked;
        private EventHandler<BaseKeyRepeatedEventArgs> _repeated;

        private void RecomputeHasSubscribedEvents()
            => _hasSubscribedEvents = _pressed != null || _released != null || _clicked != null || _repeated != null;

        /// <summary>Invoked immediately after a Key has been pressed.</summary>
        public event EventHandler<BaseKeyPressedEventArgs> Pressed
        {
            add    { _pressed += value; RecomputeHasSubscribedEvents(); }
            remove { _pressed -= value; RecomputeHasSubscribedEvents(); }
        }
        /// <summary>Alias of <see cref="Pressed"/> with desktop-style naming.</summary>
        public event EventHandler<BaseKeyPressedEventArgs> KeyDown
        {
            add    { _pressed += value; RecomputeHasSubscribedEvents(); }
            remove { _pressed -= value; RecomputeHasSubscribedEvents(); }
        }
        /// <summary>Invoked immediately after a Key has been released.<para/>
        /// Note: This event is invoked before <see cref="Clicked"/></summary>
        public event EventHandler<BaseKeyReleasedEventArgs> Released
        {
            add    { _released += value; RecomputeHasSubscribedEvents(); }
            remove { _released -= value; RecomputeHasSubscribedEvents(); }
        }
        /// <summary>Alias of <see cref="Released"/> with desktop-style naming.</summary>
        public event EventHandler<BaseKeyReleasedEventArgs> KeyUp
        {
            add    { _released += value; RecomputeHasSubscribedEvents(); }
            remove { _released -= value; RecomputeHasSubscribedEvents(); }
        }
        /// <summary>Invoked immediately after a Key has been clicked.<para/>
        /// Note: This event is invoked after <see cref="Released"/></summary>
        public event EventHandler<BaseKeyClickedEventArgs> Clicked
        {
            add    { _clicked += value; RecomputeHasSubscribedEvents(); }
            remove { _clicked -= value; RecomputeHasSubscribedEvents(); }
        }
        /// <summary>Invoked after the initial key press while the key remains held and the repeat thresholds are met.</summary>
        public event EventHandler<BaseKeyRepeatedEventArgs> KeyRepeat
        {
            add    { _repeated += value; RecomputeHasSubscribedEvents(); }
            remove { _repeated -= value; RecomputeHasSubscribedEvents(); }
        }

        private bool _hasSubscribedEvents;
        public bool HasSubscribedEvents => _hasSubscribedEvents;
        #endregion Events

        /// <summary>Should only be invoked via <see cref="KeyboardTracker.UpdateHandlers"/></summary>
        internal void AutoUpdate()
        {
            if (IsManualUpdate)
                throw new InvalidOperationException($"{nameof(KeyboardHandler)}.{nameof(AutoUpdate)} should only be invoked on {nameof(KeyboardHandler)}s where {nameof(IsManualUpdate)} is false.");
            InvokeQueuedEvents();
        }

        /// <summary>Should be invoked exactly once per Update tick, but only on <see cref="KeyboardHandler"/>s where <see cref="IsManualUpdate"/> is true.<para/>
        /// This method will invoke any pending keyboard events.</summary>
        public void ManualUpdate()
        {
            if (!IsManualUpdate)
                throw new InvalidOperationException($"{nameof(KeyboardHandler)}.{nameof(ManualUpdate)} should only be invoked on {nameof(KeyboardHandler)}s where {nameof(IsManualUpdate)} is true.");
            InvokeQueuedEvents();
        }

        private void InvokeQueuedEvents()
        {
            if (IsValid && HasSubscribedEvents && Owner.CanReceiveKeyboardInput())
            {
                foreach (Keys Key in AllKeys)
                {
                    bool hasKeyboardFocus = Owner.HasKeyboardFocus();

                    //  Invoke Key Pressed
                    BaseKeyPressedEventArgs PressedArgs = Tracker.CurrentKeyPressedEvents[Key];
                    if (PressedArgs != null && _pressed != null && hasKeyboardFocus && (InvokeEvenIfHandled || !PressedArgs.IsHandled))
                    {
                        _pressed.Invoke(this, PressedArgs);
                        if (AlwaysHandlesEvents)
                            PressedArgs.SetHandledBy(Owner, false);

                        AdoptStreamIfHandled(PressedArgs);
                    }

                    //  Invoke Key Released
                    BaseKeyReleasedEventArgs ReleasedArgs = Tracker.CurrentKeyReleasedEvents[Key];
                    bool ownsReleasedStream = OwnsStream(ReleasedArgs?.Stream);
                    bool hasOwnedReleasedStream = ReleasedArgs?.Stream?.HasOwner == true;
                    bool canReceiveReleased = ownsReleasedStream || (!hasOwnedReleasedStream && hasKeyboardFocus);
                    if (ReleasedArgs != null && _released != null && canReceiveReleased
                        && (InvokeEvenIfHandled || !ReleasedArgs.IsHandled || ReleasedArgs.HandledBy == Owner || ownsReleasedStream))
                    {
                        _released.Invoke(this, ReleasedArgs);
                        if (AlwaysHandlesEvents)
                            ReleasedArgs.SetHandledBy(Owner, false);
                    }

                    BaseKeyRepeatedEventArgs RepeatedArgs = GetCurrentKeyRepeatEvent(Key);
                    bool ownsRepeatStream = OwnsStream(RepeatedArgs?.Stream);
                    bool hasOwnedStream = RepeatedArgs?.Stream?.HasOwner == true;
                    bool canReceiveRepeat = ownsRepeatStream || (!hasOwnedStream && hasKeyboardFocus);
                    if (RepeatedArgs != null && _repeated != null && canReceiveRepeat
                        && (InvokeEvenIfHandled || !RepeatedArgs.IsHandled || RepeatedArgs.HandledBy == Owner || ownsRepeatStream))
                    {
                        _repeated.Invoke(this, RepeatedArgs);
                        if (AlwaysHandlesEvents)
                            RepeatedArgs.SetHandledBy(Owner, false);
                    }

                    //  Invoke Key Clicked
                    BaseKeyClickedEventArgs ClickedArgs = Tracker.CurrentKeyClickedEvents[Key];
                    bool ownsClickedStream = OwnsStream(ClickedArgs?.Stream);
                    bool hasOwnedClickedStream = ClickedArgs?.Stream?.HasOwner == true;
                    bool canReceiveClicked = ownsClickedStream || (!hasOwnedClickedStream && hasKeyboardFocus);
                    if (ClickedArgs != null && _clicked != null && canReceiveClicked
                        && (InvokeEvenIfHandled || !ClickedArgs.IsHandled || ClickedArgs.HandledBy == Owner || ownsClickedStream)
                        && (InvokeEvenIfHandled || !ClickedArgs.ReleasedArgs.IsHandled || ClickedArgs.ReleasedArgs.HandledBy == Owner || ownsClickedStream))
                    {
                        _clicked.Invoke(this, ClickedArgs);
                        if (AlwaysHandlesEvents)
                            ClickedArgs.SetHandledBy(Owner, false);
                    }
                }
            }
        }

        private BaseKeyRepeatedEventArgs GetCurrentKeyRepeatEvent(Keys key)
        {
            if (_repeated == null || !RepeatPolicy.Enabled)
            {
                _LastRepeatedAt[key] = null;
                return null;
            }

            if (Tracker.CurrentKeyReleasedEvents[key] != null || !Tracker.IsPressed(key))
            {
                _LastRepeatedAt[key] = null;
                return null;
            }

            if (Tracker.CurrentKeyPressedEvents[key] != null)
            {
                _LastRepeatedAt[key] = null;
                return null;
            }

            if (!Tracker.TryGetInitialPressedEvent(key, out BaseKeyPressedEventArgs initialPressedArgs))
                return null;

            TimeSpan? heldSince = Tracker.GetHeldSince(key);
            if (!heldSince.HasValue)
                return null;

            if (RepeatPolicy.CanRepeat != null && !RepeatPolicy.CanRepeat(key))
                return null;

            if (!KeyboardTracker.IsRepeatDue(Tracker.CurrentTotalElapsed, heldSince.Value, _LastRepeatedAt[key], RepeatPolicy.InitialDelay, RepeatPolicy.Interval))
                return null;

            string keyValue = Tracker.KeyToTextInputString(key);
            BaseKeyRepeatedEventArgs repeatedArgs = new(Tracker, initialPressedArgs, key, keyValue);
            _LastRepeatedAt[key] = Tracker.CurrentTotalElapsed;
            return repeatedArgs;
        }

        private bool IsValid = true;
        /// <summary>Permanently invalidates this <see cref="KeyboardHandler"/> so that it will not receive and invoke any further keyboard-related events.</summary>
        public void Unsubscribe()
        {
            Tracker.RemoveHandler(this);
            IsValid = false;
        }
    }
}
