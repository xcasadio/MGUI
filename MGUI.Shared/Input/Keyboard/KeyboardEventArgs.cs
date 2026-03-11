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
    /// The stream is used for internal routing and ownership independently from the public <see cref="HandledByEventArgs{THandlerType}.HandledBy"/> state.</summary>
    public class KeyboardInputStream
    {
        public long Id { get; }
        public Keys Key { get; }
        public TimeSpan StartedAt { get; }
        public BaseKeyPressedEventArgs InitialPressedArgs { get; private set; }

        internal KeyboardInputStreamRoutingMetadata RoutingMetadata { get; } = new();

        internal KeyboardInputStream(long Id, Keys Key, TimeSpan StartedAt)
        {
            this.Id = Id;
            this.Key = Key;
            this.StartedAt = StartedAt;
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

    public class BaseKeyPressedEventArgs : HandledByEventArgs<IKeyboardHandlerHost>
    {
        private static readonly Regex Alphanumeric = new(@"^[A-Za-z0-9]$");
        public bool IsAlphanumeric() => IsPrintableKey && Alphanumeric.IsMatch(PrintableValue);

        public KeyboardTracker Tracker { get; }
        /// <summary>The stable technical stream that this key press belongs to.
        /// This is used for internal routing and is distinct from the public handled state.</summary>
        public KeyboardInputStream Stream { get; }
        public long StreamId => Stream?.Id ?? 0;

        public TimeSpan PressedAt { get; }
        public TimeSpan? RepeatedAt { get; }
        public Keys Key { get; }
        public bool IsPrintableKey { get; }
        public string PrintableValue { get; }
        public bool IsRepeat => RepeatedAt.HasValue;

        public BaseKeyPressedEventArgs(KeyboardTracker Tracker, Keys Key, string PrintableValue, TimeSpan PressedAt, KeyboardInputStream Stream, TimeSpan? RepeatedAt = null)
            : base()
        {
            this.Tracker = Tracker;
            this.Stream = Stream;
            this.PressedAt = PressedAt;
            this.RepeatedAt = RepeatedAt;
            this.Key = Key;
            IsPrintableKey = !string.IsNullOrEmpty(PrintableValue);
            this.PrintableValue = PrintableValue;

            this.Stream?.RegisterInitialPress(this);
        }
    }

    public class BaseKeyRepeatedEventArgs : BaseKeyPressedEventArgs
    {
        public BaseKeyPressedEventArgs InitialPressedArgs { get; }

        public TimeSpan HeldDuration => RepeatedAt!.Value.Subtract(InitialPressedArgs.PressedAt);

        public BaseKeyRepeatedEventArgs(KeyboardTracker Tracker, BaseKeyPressedEventArgs InitialPressedArgs, Keys Key, string PrintableValue)
            : base(Tracker, Key, PrintableValue, InitialPressedArgs.PressedAt, InitialPressedArgs.Stream, Tracker.CurrentTotalElapsed)
        {
            this.InitialPressedArgs = InitialPressedArgs;
        }
    }

    public class BaseKeyReleasedEventArgs : HandledByEventArgs<IKeyboardHandlerHost>
    {
        private static readonly Regex Alphanumeric = new(@"^[A-Za-z0-9]$");
        public bool IsAlphanumeric() => IsPrintableKey && Alphanumeric.IsMatch(PrintableValue);

        public KeyboardTracker Tracker { get; }

        public BaseKeyPressedEventArgs PressedArgs { get; }
        /// <summary>The stable technical stream associated with the original key press.</summary>
        public KeyboardInputStream Stream => PressedArgs?.Stream;
        public long StreamId => Stream?.Id ?? 0;

        public TimeSpan ReleasedAt { get; }

        public Keys Key { get; }
        public bool IsPrintableKey { get; }
        public string PrintableValue { get; }

        public TimeSpan HeldDuration => ReleasedAt.Subtract(PressedArgs.PressedAt);

        public BaseKeyReleasedEventArgs(KeyboardTracker Tracker, BaseKeyPressedEventArgs PressedArgs, Keys Key, string PrintableValue, TimeSpan ReleasedAt)
            : base()
        {
            this.Tracker = Tracker;
            this.PressedArgs = PressedArgs;
            this.ReleasedAt = ReleasedAt;
            this.Key = Key;
            IsPrintableKey = !string.IsNullOrEmpty(PrintableValue);
            this.PrintableValue = PrintableValue;
        }
    }

    public class BaseKeyClickedEventArgs : HandledByEventArgs<IKeyboardHandlerHost>
    {
        private static readonly Regex Alphanumeric = new(@"^[A-Za-z0-9]$");
        public bool IsAlphanumeric() => IsPrintableKey && Alphanumeric.IsMatch(PrintableValue);

        public KeyboardTracker Tracker { get; }

        public BaseKeyReleasedEventArgs ReleasedArgs { get; }
        public BaseKeyPressedEventArgs PressedArgs => ReleasedArgs.PressedArgs;
        /// <summary>The stable technical stream associated with the original key press.</summary>
        public KeyboardInputStream Stream => PressedArgs?.Stream;
        public long StreamId => Stream?.Id ?? 0;

        public Keys Key { get; }
        public bool IsPrintableKey { get; }
        public string PrintableValue { get; }

        public BaseKeyClickedEventArgs(KeyboardTracker Tracker, BaseKeyReleasedEventArgs ReleasedArgs, Keys Key, string PrintableValue)
            : base()
        {
            this.Tracker = Tracker;
            this.ReleasedArgs = ReleasedArgs;
            this.Key = Key;
            IsPrintableKey = !string.IsNullOrEmpty(PrintableValue);
            this.PrintableValue = PrintableValue;
        }
    }
}
