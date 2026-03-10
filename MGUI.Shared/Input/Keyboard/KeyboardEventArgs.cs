using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MGUI.Shared.Input.Keyboard
{
    public class BaseKeyPressedEventArgs : HandledByEventArgs<IKeyboardHandlerHost>
    {
        private static readonly Regex Alphanumeric = new(@"^[A-Za-z0-9]$");
        public bool IsAlphanumeric() => IsPrintableKey && Alphanumeric.IsMatch(PrintableValue);

        public KeyboardTracker Tracker { get; }

        public TimeSpan PressedAt { get; }
        public TimeSpan? RepeatedAt { get; }
        public Keys Key { get; }
        public bool IsPrintableKey { get; }
        public string PrintableValue { get; }
        public bool IsRepeat => RepeatedAt.HasValue;

        public BaseKeyPressedEventArgs(KeyboardTracker Tracker, Keys Key, string PrintableValue, TimeSpan PressedAt, TimeSpan? RepeatedAt = null)
            : base()
        {
            this.Tracker = Tracker;
            this.PressedAt = PressedAt;
            this.RepeatedAt = RepeatedAt;
            this.Key = Key;
            IsPrintableKey = !string.IsNullOrEmpty(PrintableValue);
            this.PrintableValue = PrintableValue;
        }
    }

    public class BaseKeyRepeatedEventArgs : BaseKeyPressedEventArgs
    {
        public BaseKeyPressedEventArgs InitialPressedArgs { get; }

        public TimeSpan HeldDuration => RepeatedAt!.Value.Subtract(InitialPressedArgs.PressedAt);

        public BaseKeyRepeatedEventArgs(KeyboardTracker Tracker, BaseKeyPressedEventArgs InitialPressedArgs, Keys Key, string PrintableValue)
            : base(Tracker, Key, PrintableValue, InitialPressedArgs.PressedAt, Tracker.CurrentTotalElapsed)
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
