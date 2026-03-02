using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Shared.Input.Keyboard
{
    /// <summary>Exposes several keyboard-related events that you can subscribe and respond to, such as <see cref="KeyboardHandler.Pressed"/>, <see cref="KeyboardHandler.Released"/>, <see cref="KeyboardHandler.Clicked"/>.<para/>
    /// This class is instantiated via: <see cref="KeyboardTracker.CreateHandler{T}(T, double?, bool, bool)"/></summary>
    public class KeyboardHandler
    {
        public static readonly ReadOnlyCollection<Keys> AllKeys = Enum.GetValues(typeof(Keys)).Cast<Keys>().ToList().AsReadOnly();

        public KeyboardTracker Tracker { get; }
        public IKeyboardHandlerHost Owner { get; }
        public double? UpdatePriority { get; }
        public bool IsManualUpdate => !UpdatePriority.HasValue;

        /// <summary>If true, <see cref="HandledByEventArgs{THandlerType}.HandledBy"/> will always be set to <see cref="Owner"/> after invoking an event.</summary>
        private bool AlwaysHandlesEvents { get; }
        /// <summary>If true, events will still be invoked even if <see cref="HandledByEventArgs{THandlerType}.IsHandled"/> is true.</summary>
        private bool InvokeEvenIfHandled { get; }

        public override string ToString() => $"{nameof(KeyboardHandler)}: {nameof(Owner)} = {Owner}";

        internal KeyboardHandler(KeyboardTracker Tracker, IKeyboardHandlerHost Owner, double? UpdatePriority, bool AlwaysHandlesEvents, bool InvokeEvenIfHandled)
        {
            this.Tracker = Tracker;
            this.Owner = Owner;
            this.UpdatePriority = UpdatePriority;
            this.AlwaysHandlesEvents = AlwaysHandlesEvents;
            this.InvokeEvenIfHandled = InvokeEvenIfHandled;
        }

        #region Events
        private EventHandler<BaseKeyPressedEventArgs> _pressed;
        private EventHandler<BaseKeyReleasedEventArgs> _released;
        private EventHandler<BaseKeyClickedEventArgs> _clicked;

        /// <summary>Invoked immediately after a Key has been pressed.</summary>
        public event EventHandler<BaseKeyPressedEventArgs> Pressed
        {
            add    { _pressed += value; _hasSubscribedEvents = _pressed != null || _released != null || _clicked != null; }
            remove { _pressed -= value; _hasSubscribedEvents = _pressed != null || _released != null || _clicked != null; }
        }
        /// <summary>Invoked immediately after a Key has been released.<para/>
        /// Note: This event is invoked before <see cref="Clicked"/></summary>
        public event EventHandler<BaseKeyReleasedEventArgs> Released
        {
            add    { _released += value; _hasSubscribedEvents = _pressed != null || _released != null || _clicked != null; }
            remove { _released -= value; _hasSubscribedEvents = _pressed != null || _released != null || _clicked != null; }
        }
        /// <summary>Invoked immediately after a Key has been clicked.<para/>
        /// Note: This event is invoked after <see cref="Released"/></summary>
        public event EventHandler<BaseKeyClickedEventArgs> Clicked
        {
            add    { _clicked += value; _hasSubscribedEvents = _pressed != null || _released != null || _clicked != null; }
            remove { _clicked -= value; _hasSubscribedEvents = _pressed != null || _released != null || _clicked != null; }
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
            if (IsValid && HasSubscribedEvents && Owner.CanReceiveKeyboardInput() && Owner.HasKeyboardFocus())
            {
                foreach (Keys Key in AllKeys)
                {
                    //  Invoke Key Pressed
                    BaseKeyPressedEventArgs PressedArgs = Tracker.CurrentKeyPressedEvents[Key];
                    if (PressedArgs != null && _pressed != null && (InvokeEvenIfHandled || !PressedArgs.IsHandled))
                    {
                        _pressed.Invoke(this, PressedArgs);
                        if (AlwaysHandlesEvents)
                            PressedArgs.SetHandledBy(Owner, false);
                    }

                    //  Invoke Key Released
                    BaseKeyReleasedEventArgs ReleasedArgs = Tracker.CurrentKeyReleasedEvents[Key];
                    if (ReleasedArgs != null && _released != null && (InvokeEvenIfHandled || !ReleasedArgs.IsHandled))
                    {
                        _released.Invoke(this, ReleasedArgs);
                        if (AlwaysHandlesEvents)
                            ReleasedArgs.SetHandledBy(Owner, false);
                    }

                    //  Invoke Key Clicked
                    BaseKeyClickedEventArgs ClickedArgs = Tracker.CurrentKeyClickedEvents[Key];
                    if (ClickedArgs != null && _clicked != null && (InvokeEvenIfHandled || !ClickedArgs.IsHandled) && (InvokeEvenIfHandled || !ClickedArgs.ReleasedArgs.IsHandled || ClickedArgs.ReleasedArgs.HandledBy == Owner))
                    {
                        _clicked.Invoke(this, ClickedArgs);
                        if (AlwaysHandlesEvents)
                            ClickedArgs.SetHandledBy(Owner, false);
                    }
                }
            }
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
