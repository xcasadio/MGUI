using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MGUI.Shared.Input.Keyboard
{
    internal sealed class KeyboardInputStreamRoutingMetadata
    {
        public long? OwnerHandlerId { get; set; }
    }

    /// <summary>Represents the stable technical identity of a held-key input stream.
    /// The stream is used for internal routing and ownership independently from the public <see cref="HandledByEventArgs{THandlerType}.HandledBy"/> state.<para/>
    /// <see cref="KeyboardTracker"/> keeps one stream per key for the press of the key, and another per key for the press that a native text input creates alone, and restarts it at each new press
    /// of that key, with a new <see cref="Id"/>, its start time, its initial press and no owner: a stream is valid until the next press of its key. Read it during the event, do not keep it
    /// (keep its <see cref="Id"/>). See ADR-0034.</summary>
    public class KeyboardInputStream
    {
        public long Id { get; private set; }
        public Keys Key { get; }
        public TimeSpan StartedAt { get; private set; }
        public BaseKeyPressedEventArgs InitialPressedArgs { get; private set; }

        internal KeyboardInputStreamRoutingMetadata RoutingMetadata { get; } = new();

        internal KeyboardInputStream(long Id, Keys Key, TimeSpan StartedAt)
        {
            this.Id = Id;
            this.Key = Key;
            this.StartedAt = StartedAt;
        }

        /// <summary>Restarts this reused stream as a new one: a new identity, no initial press yet and no owner.</summary>
        internal void Restart(long Id, TimeSpan StartedAt)
        {
            this.Id = Id;
            this.StartedAt = StartedAt;
            InitialPressedArgs = null;
            RoutingMetadata.OwnerHandlerId = null;
        }

        internal void RegisterInitialPress(BaseKeyPressedEventArgs PressedArgs)
        {
            if (InitialPressedArgs == null)
            {
                InitialPressedArgs = PressedArgs;
            }
        }

        internal bool HasOwner => OwnerHandlerId.HasValue;

        internal long? OwnerHandlerId => RoutingMetadata.OwnerHandlerId;

        internal bool TryAdoptOwner(long HandlerId)
        {
            if (OwnerHandlerId.HasValue && OwnerHandlerId != HandlerId)
            {
                return false;
            }

            RoutingMetadata.OwnerHandlerId = HandlerId;
            return true;
        }

        internal bool IsOwnedBy(long HandlerId)
            => OwnerHandlerId == HandlerId;
    }

    /// <summary>Raised when a key is pressed. <see cref="KeyboardTracker"/> keeps one instance per key and refills it, with its handled state reset, at each press of that key: read it during
    /// the event, do not keep it. It stays valid, unchanged, until the next press of the same key, so while the key is held and during its release. The press that a native text input creates
    /// alone (no key is down) is another instance of the key, with its own stream, so that the release and the click built in the same frame keep their press, its time and its stream. A handler
    /// that needs a value later copies the value. See ADR-0034.</summary>
    public class BaseKeyPressedEventArgs : HandledByEventArgs<IKeyboardHandlerHost>
    {
        private static readonly Regex Alphanumeric = new(@"^[A-Za-z0-9]$");
        public bool IsAlphanumeric() => IsPrintableKey && Alphanumeric.IsMatch(PrintableValue);

        public KeyboardTracker Tracker { get; }
        /// <summary>The stable technical stream that this key press belongs to.
        /// This is used for internal routing and is distinct from the public handled state.</summary>
        public KeyboardInputStream Stream { get; private set; }
        public long StreamId => Stream?.Id ?? 0;

        public TimeSpan PressedAt { get; private set; }
        public TimeSpan? RepeatedAt { get; private set; }
        public Keys Key { get; }
        public bool IsPrintableKey { get; private set; }
        public string PrintableValue { get; private set; }
        public bool IsRepeat => RepeatedAt.HasValue;

        public BaseKeyPressedEventArgs(KeyboardTracker Tracker, Keys Key, string PrintableValue, TimeSpan PressedAt, KeyboardInputStream Stream, TimeSpan? RepeatedAt = null)
            : base()
        {
            this.Tracker = Tracker;
            this.Key = Key;
            Fill(PrintableValue, PressedAt, Stream, RepeatedAt);
        }

        /// <summary>Refills this reused instance for a new press of its key: new values, and the handled state back to not handled.</summary>
        internal void Set(string PrintableValue, TimeSpan PressedAt, KeyboardInputStream Stream, TimeSpan? RepeatedAt = null)
        {
            Reset();
            Fill(PrintableValue, PressedAt, Stream, RepeatedAt);
        }

        private void Fill(string PrintableValue, TimeSpan PressedAt, KeyboardInputStream Stream, TimeSpan? RepeatedAt)
        {
            this.Stream = Stream;
            this.PressedAt = PressedAt;
            this.RepeatedAt = RepeatedAt;
            IsPrintableKey = !string.IsNullOrEmpty(PrintableValue);
            this.PrintableValue = PrintableValue;

            this.Stream?.RegisterInitialPress(this);
        }
    }

    /// <summary>Raised for each repeat of a held key. Each <see cref="KeyboardHandler"/> keeps one instance per key and refills it, with its handled state reset, at each repeat that it delivers:
    /// the handled state is the one of that handler's event, as it was when each repeat was a new object. Read it during the event, do not keep it. <see cref="InitialPressedArgs"/> designates the
    /// press instance of the key (valid until the next press of the key). See ADR-0034.</summary>
    public class BaseKeyRepeatedEventArgs : BaseKeyPressedEventArgs
    {
        public BaseKeyPressedEventArgs InitialPressedArgs { get; private set; }

        /// <summary>The time between the initial press and this repeat (the press time of a repeat is the one of the initial press).</summary>
        public TimeSpan HeldDuration => RepeatedAt!.Value.Subtract(PressedAt);

        public BaseKeyRepeatedEventArgs(KeyboardTracker Tracker, BaseKeyPressedEventArgs InitialPressedArgs, Keys Key, string PrintableValue)
            : base(Tracker, Key, PrintableValue, InitialPressedArgs.PressedAt, InitialPressedArgs.Stream, Tracker.CurrentTotalElapsed)
        {
            this.InitialPressedArgs = InitialPressedArgs;
        }

        /// <summary>Refills this reused instance for a new repeat of its key.</summary>
        internal void SetRepeat(BaseKeyPressedEventArgs InitialPressedArgs, string PrintableValue)
        {
            Set(PrintableValue, InitialPressedArgs.PressedAt, InitialPressedArgs.Stream, Tracker.CurrentTotalElapsed);
            this.InitialPressedArgs = InitialPressedArgs;
        }
    }

    /// <summary>Raised when a key is released. <see cref="KeyboardTracker"/> keeps one instance per key and refills it, with its handled state reset, at each release of that key: read it during
    /// the event, do not keep it. It stays valid, unchanged, until the next release of the same key. <see cref="PressedArgs"/> and <see cref="Stream"/> designate the press instance and the stream of the
    /// key (valid until the next press of the key), and <see cref="HeldDuration"/> is a value written when the release is filled. See ADR-0034.</summary>
    public class BaseKeyReleasedEventArgs : HandledByEventArgs<IKeyboardHandlerHost>
    {
        private static readonly Regex Alphanumeric = new(@"^[A-Za-z0-9]$");
        public bool IsAlphanumeric() => IsPrintableKey && Alphanumeric.IsMatch(PrintableValue);

        public KeyboardTracker Tracker { get; }

        public BaseKeyPressedEventArgs PressedArgs { get; private set; }
        /// <summary>The stable technical stream associated with the original key press.</summary>
        public KeyboardInputStream Stream => PressedArgs?.Stream;
        public long StreamId => Stream?.Id ?? 0;

        public TimeSpan ReleasedAt { get; private set; }

        public Keys Key { get; }
        public bool IsPrintableKey { get; private set; }
        public string PrintableValue { get; private set; }

        public TimeSpan HeldDuration { get; private set; }

        public BaseKeyReleasedEventArgs(KeyboardTracker Tracker, BaseKeyPressedEventArgs PressedArgs, Keys Key, string PrintableValue, TimeSpan ReleasedAt)
            : base()
        {
            this.Tracker = Tracker;
            this.Key = Key;
            Fill(PressedArgs, PrintableValue, ReleasedAt);
        }

        /// <summary>Refills this reused instance for a new release of its key: new values, and the handled state back to not handled.</summary>
        internal void Set(BaseKeyPressedEventArgs PressedArgs, string PrintableValue, TimeSpan ReleasedAt)
        {
            Reset();
            Fill(PressedArgs, PrintableValue, ReleasedAt);
        }

        private void Fill(BaseKeyPressedEventArgs PressedArgs, string PrintableValue, TimeSpan ReleasedAt)
        {
            this.PressedArgs = PressedArgs;
            this.ReleasedAt = ReleasedAt;
            IsPrintableKey = !string.IsNullOrEmpty(PrintableValue);
            this.PrintableValue = PrintableValue;
            HeldDuration = PressedArgs == null ? TimeSpan.Zero : ReleasedAt.Subtract(PressedArgs.PressedAt);
        }
    }

    /// <summary>Raised when a key is clicked. <see cref="KeyboardTracker"/> keeps one instance per key and refills it, with its handled state reset, at each click of that key: read it during
    /// the event, do not keep it. It stays valid, unchanged, until the next click of the same key. <see cref="ReleasedArgs"/>, <see cref="PressedArgs"/> and <see cref="Stream"/> designate the release
    /// and press instances and the stream of the key (valid until the next release and the next press of the key). See ADR-0034.</summary>
    public class BaseKeyClickedEventArgs : HandledByEventArgs<IKeyboardHandlerHost>
    {
        private static readonly Regex Alphanumeric = new(@"^[A-Za-z0-9]$");
        public bool IsAlphanumeric() => IsPrintableKey && Alphanumeric.IsMatch(PrintableValue);

        public KeyboardTracker Tracker { get; }

        public BaseKeyReleasedEventArgs ReleasedArgs { get; private set; }
        public BaseKeyPressedEventArgs PressedArgs => ReleasedArgs.PressedArgs;
        /// <summary>The stable technical stream associated with the original key press.</summary>
        public KeyboardInputStream Stream => PressedArgs?.Stream;
        public long StreamId => Stream?.Id ?? 0;

        public Keys Key { get; }
        public bool IsPrintableKey { get; private set; }
        public string PrintableValue { get; private set; }

        public BaseKeyClickedEventArgs(KeyboardTracker Tracker, BaseKeyReleasedEventArgs ReleasedArgs, Keys Key, string PrintableValue)
            : base()
        {
            this.Tracker = Tracker;
            this.Key = Key;
            Fill(ReleasedArgs, PrintableValue);
        }

        /// <summary>Refills this reused instance for a new click of its key: new values, and the handled state back to not handled.</summary>
        internal void Set(BaseKeyReleasedEventArgs ReleasedArgs, string PrintableValue)
        {
            Reset();
            Fill(ReleasedArgs, PrintableValue);
        }

        private void Fill(BaseKeyReleasedEventArgs ReleasedArgs, string PrintableValue)
        {
            this.ReleasedArgs = ReleasedArgs;
            IsPrintableKey = !string.IsNullOrEmpty(PrintableValue);
            this.PrintableValue = PrintableValue;
        }
    }
}
