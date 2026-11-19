using MGUI.Shared.Input;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace MGUI.Tests.Input;

/// <summary>
/// A scripted scenario of the keyboard events, run frame by frame on one <see cref="InputTracker"/> and four manual <see cref="KeyboardHandler"/>s, that writes one journal:
/// for each frame the keys that are down, the text input that is queued, the events that the tracker holds for the frame, then every event that each handler received (press, repeat,
/// release, click), with the values written at the time of the event: key, printable value, press time, repeat time, stream id, start time and owner of the stream, handled state
/// and who handled. Its journal is frozen in <see cref="KeyboardEventsGolden"/> (taken from the code before the keyboard events were reused: a new press, release, click and stream
/// for each key, a new repeat for each repeat of each handler). A difference in any frame is a change of behaviour.
/// <para/>
/// The handlers: <c>A</c> and <c>B</c> (focusable, each with its own repeat interval, and flags that make them mark the press, repeat, release or click handled), <c>C</c> (always focused, receives
/// the events that were already handled) and <c>D</c> (always handles the events it receives, only receives while enabled). The script covers: single printable, non printable and normalized
/// keys, a key held for several repeat periods, two keys at once and overlapping holds, modifiers with a printable key, native text input with and without a matching key press and without
/// any key press, a focus that changes while a key is held (the stream owned or not), a held key whose repeat goes to two handlers of which one marks it handled, the release of a key in the
/// frame where a native text input of the same key is not consumed, a key pressed, handled and released by A and then pressed, repeated and released again after the focus moved to B, and the
/// click threshold.
/// </summary>
internal static class KeyboardEventsScenario
{
    private sealed class Host : IKeyboardHandlerHost
    {
        public bool Can { get; set; } = true;
        public bool Focus { get; set; }

        public bool CanReceiveKeyboardInput() => Can;
        public bool HasKeyboardFocus() => Focus;
    }

    private static readonly Keys[] Probed =
    {
        Keys.A, Keys.S, Keys.D, Keys.E, Keys.Q, Keys.W, Keys.R, Keys.K, Keys.C, Keys.Left, Keys.Enter, Keys.Tab, Keys.Space, Keys.D1, Keys.OemMinus,
        Keys.LeftShift, Keys.RightShift, Keys.LeftControl,
    };

    public static List<string> Run()
    {
        List<string> journal = new();
        InputTracker tracker = new();
        KeyboardTracker keyboard = tracker.Keyboard;

        Host hostA = new() { Focus = true };
        Host hostB = new();
        Host hostC = new() { Focus = true };
        Host hostD = new() { Can = false, Focus = true };
        KeyboardHandler hA = keyboard.CreateHandler(hostA, (double?)null);
        KeyboardHandler hB = keyboard.CreateHandler(hostB, (double?)null);
        KeyboardHandler hC = keyboard.CreateHandler(hostC, (double?)null, false, true);
        KeyboardHandler hD = keyboard.CreateHandler(hostD, (double?)null, true, false);
        hA.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(300);
        hA.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(100);
        hB.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(300);
        hB.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(150);
        hC.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(300);
        hC.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(100);
        hD.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(300);
        hD.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(100);
        string[] names = { "A", "D", "B", "C" };
        KeyboardHandler[] handlers = { hA, hD, hB, hC };

        Dictionary<IKeyboardHandlerHost, string> hostNames = new(ReferenceEqualityComparer.Instance)
        {
            { hostA, "A" }, { hostB, "B" }, { hostC, "C" }, { hostD, "D" },
        };
        PropertyInfo ownerIdProperty = typeof(KeyboardHandler).GetProperty("StreamOwnerId", BindingFlags.NonPublic | BindingFlags.Instance);
        Dictionary<long, string> ownerNames = new();
        for (int i = 0; i < handlers.Length; i++)
        {
            ownerNames[(long)ownerIdProperty.GetValue(handlers[i])] = names[i];
        }

        bool aHandlesPress = false, aHandlesRepeat = false, aHandlesRelease = false, aHandlesClick = false, bHandlesPress = false;

        string Ms(TimeSpan t) => t.TotalMilliseconds.ToString("R", CultureInfo.InvariantCulture);
        string Val(string value) => value == null ? "null" : "'" + value.Replace("\n", "\\n") + "'";
        string Who(IKeyboardHandlerHost host) => host == null ? "-" : hostNames[host];
        string Handled(HandledByEventArgs<IKeyboardHandlerHost> e) => e.IsHandled ? "by " + Who(e.HandledBy) : "free";
        string Owner(KeyboardInputStream stream)
            => stream == null ? "nostream" : !stream.HasOwner ? "none" : ownerNames[stream.OwnerHandlerId.Value];
        string StreamText(KeyboardInputStream stream)
            => stream == null ? "nostream" : $"stream={stream.Id} start={Ms(stream.StartedAt)} first={(stream.InitialPressedArgs == null ? "-" : Ms(stream.InitialPressedArgs.PressedAt))} owner={Owner(stream)}";

        void Pressed(string name, BaseKeyPressedEventArgs e)
        {
            string kind = e is BaseKeyRepeatedEventArgs ? "KeyRepeat" : "Pressed";
            string extra = e is BaseKeyRepeatedEventArgs r ? $" held={Ms(r.HeldDuration)} initialAt={Ms(r.InitialPressedArgs.PressedAt)}" : "";
            journal.Add($"  {name}.{kind} {e.Key} val={Val(e.PrintableValue)} printable={e.IsPrintableKey} alnum={e.IsAlphanumeric()} pressedAt={Ms(e.PressedAt)} repeatedAt={(e.RepeatedAt.HasValue ? Ms(e.RepeatedAt.Value) : "-")} " +
                        $"isRepeat={e.IsRepeat} streamId={e.StreamId} {StreamText(e.Stream)}{extra} {Handled(e)}");
            int index = journal.Count - 1;
            if (kind == "Pressed" && ((name == "A" && aHandlesPress) || (name == "B" && bHandlesPress)))
            {
                e.SetHandledBy(name == "A" ? hostA : hostB, false);
            }
            else if (kind == "KeyRepeat" && name == "A" && aHandlesRepeat)
            {
                e.SetHandledBy(hostA, false);
            }

            journal[index] += " -> " + Handled(e);
        }

        void Released(string name, BaseKeyReleasedEventArgs e)
        {
            journal.Add($"  {name}.Released {e.Key} val={Val(e.PrintableValue)} printable={e.IsPrintableKey} alnum={e.IsAlphanumeric()} releasedAt={Ms(e.ReleasedAt)} held={Ms(e.HeldDuration)} pressedAt={Ms(e.PressedArgs.PressedAt)} " +
                        $"streamId={e.StreamId} {StreamText(e.Stream)} {Handled(e)} press {Handled(e.PressedArgs)}");
            int index = journal.Count - 1;
            if (name == "A" && aHandlesRelease)
            {
                e.SetHandledBy(hostA, false);
            }

            journal[index] += " -> " + Handled(e);
        }

        void Clicked(string name, BaseKeyClickedEventArgs e)
        {
            journal.Add($"  {name}.Clicked {e.Key} val={Val(e.PrintableValue)} printable={e.IsPrintableKey} alnum={e.IsAlphanumeric()} streamId={e.StreamId} {StreamText(e.Stream)} releasedAt={Ms(e.ReleasedArgs.ReleasedAt)} " +
                        $"pressedAt={Ms(e.PressedArgs.PressedAt)} {Handled(e)} release {Handled(e.ReleasedArgs)} press {Handled(e.PressedArgs)}");
            int index = journal.Count - 1;
            if (name == "A" && aHandlesClick)
            {
                e.SetHandledBy(hostA, false);
            }

            journal[index] += " -> " + Handled(e);
        }

        for (int i = 0; i < handlers.Length; i++)
        {
            string n = names[i];
            handlers[i].Pressed += (_, e) => Pressed(n, e);
            handlers[i].Released += (_, e) => Released(n, e);
            handlers[i].Clicked += (_, e) => Clicked(n, e);
            handlers[i].KeyRepeat += (_, e) => Pressed(n, e);
        }

        int t = 0;
        int frame = 0;
        Keys[] down = Array.Empty<Keys>();
        List<string> queued = new();

        void LogTracker()
        {
            StringBuilder state = new();
            foreach (Keys key in Probed)
            {
                BaseKeyPressedEventArgs pressed = keyboard.CurrentKeyPressedEvents[key];
                if (pressed != null) { state.Append($" P:{key}={Val(pressed.PrintableValue)}@{Ms(pressed.PressedAt)}#{pressed.StreamId}"); }
                BaseKeyReleasedEventArgs released = keyboard.CurrentKeyReleasedEvents[key];
                if (released != null) { state.Append($" R:{key}={Val(released.PrintableValue)}@{Ms(released.ReleasedAt)}#{released.StreamId}"); }
                BaseKeyClickedEventArgs clicked = keyboard.CurrentKeyClickedEvents[key];
                if (clicked != null) { state.Append($" C:{key}={Val(clicked.PrintableValue)}#{clicked.StreamId}"); }
            }

            StringBuilder held = new();
            foreach (Keys key in Probed)
            {
                if (keyboard.IsPressed(key)) { held.Append(' ').Append(key); }
            }

            journal.Add($" trk{state}");
            journal.Add($" held{held} shift={keyboard.IsShiftDown} ctrl={keyboard.IsControlDown} alt={keyboard.IsAltDown}");
        }

        void Frame(int dt, params Keys[] keys)
        {
            t += dt;
            down = keys;
            frame++;
            tracker.Update(new UpdateBaseArgs(TimeSpan.FromMilliseconds(t), TimeSpan.FromMilliseconds(dt), new MouseState(), new KeyboardState(keys)));
            journal.Add($"frame {frame} t={t} down=[{string.Join(",", keys)}] queued=[{string.Join(",", queued)}] focus A={hostA.Focus} B={hostB.Focus} D={hostD.Can}");
            queued.Clear();
            LogTracker();
            for (int i = 0; i < handlers.Length; i++)
            {
                handlers[i].ManualUpdate();
            }
        }

        void Text(char character, Keys key)
        {
            queued.Add($"{(character < 128 ? character.ToString() : "U+" + ((int)character).ToString("X4"))}:{key}");
            keyboard.QueueTextInput(character, key);
        }

        void Section(string label) => journal.Add($"== {label}");
        void Focus(bool a, bool b) { hostA.Focus = a; hostB.Focus = b; }
        void Flags(bool press = false, bool repeat = false, bool release = false, bool click = false, bool bPress = false)
        {
            aHandlesPress = press; aHandlesRepeat = repeat; aHandlesRelease = release; aHandlesClick = click; bHandlesPress = bPress;
        }

        void Tap(Keys key, int heldFrames = 2, int gap = 16)
        {
            for (int i = 0; i < heldFrames; i++) { Frame(16, key); }
            Frame(gap);
        }

        Section("idle");
        Frame(16);
        Frame(16);

        Section("single printable keys, a click each");
        Tap(Keys.A);
        Tap(Keys.S, 1);
        Tap(Keys.D, 3);
        Frame(500);

        Section("non printable and normalized keys");
        Tap(Keys.Left);
        Tap(Keys.Enter);
        Tap(Keys.Tab);
        Tap(Keys.Space);
        Tap(Keys.D1);
        Tap(Keys.OemMinus);
        Frame(500);

        Section("a key held for several repeat periods, released after the click threshold");
        Frame(16, Keys.Q);
        for (int i = 0; i < 12; i++) { Frame(100, Keys.Q); }
        Frame(100);
        Frame(500);

        Section("two keys at once, overlapping holds");
        Frame(16, Keys.A, Keys.S);
        Frame(16, Keys.A, Keys.S);
        Frame(16, Keys.A, Keys.S, Keys.D);
        Frame(16, Keys.S, Keys.D);
        Frame(16, Keys.S, Keys.D);
        Frame(16, Keys.D);
        Frame(16);
        Frame(500);
        Frame(16, Keys.A);
        for (int i = 0; i < 5; i++) { Frame(100, Keys.A, Keys.S); }
        for (int i = 0; i < 4; i++) { Frame(100, Keys.S); }
        Frame(100);
        Frame(500);

        Section("modifiers with a printable key");
        Frame(16, Keys.LeftShift);
        Frame(16, Keys.LeftShift, Keys.A);
        Frame(16, Keys.LeftShift, Keys.A);
        Frame(16, Keys.LeftShift);
        Frame(16);
        Frame(16, Keys.LeftShift, Keys.D1);
        Frame(16, Keys.LeftShift, Keys.D1);
        Frame(16, Keys.D1);
        Frame(16);
        Frame(16, Keys.LeftControl, Keys.C);
        Frame(16, Keys.LeftControl, Keys.C);
        Frame(16, Keys.LeftControl);
        Frame(16);
        Frame(16, Keys.RightShift, Keys.OemMinus);
        for (int i = 0; i < 6; i++) { Frame(100, Keys.RightShift, Keys.OemMinus); }
        Frame(16);
        Frame(500);

        Section("native text input: with a matching key, with an unmatched key, with no key, with a modifier, normalized");
        Text('\u00e9', Keys.E);
        Frame(16, Keys.E);
        Frame(16, Keys.E);
        Frame(16);
        Text('\u00df', Keys.OemMinus);
        Frame(16, Keys.D);
        Frame(16);
        Text('\u00e9', Keys.E);
        Frame(16);
        Frame(16);
        Text('1', Keys.D1);
        Frame(16, Keys.LeftShift, Keys.D1);
        Frame(16);
        Text('\t', Keys.Tab);
        Frame(16, Keys.Tab);
        Frame(16);
        Text('\r', Keys.Enter);
        Frame(16, Keys.Enter);
        Frame(16);
        Text('x', Keys.None);
        Frame(16, Keys.S);
        Frame(16);
        Text('\u00e9', Keys.E);
        Text('\u00e8', Keys.E);
        Frame(16);
        Frame(500);

        Section("the focus moves from A to B while a key is held, the stream is not owned");
        Frame(16, Keys.Q);
        Frame(100, Keys.Q);
        Frame(200, Keys.Q);
        Frame(100, Keys.Q);
        Focus(false, true);
        Frame(100, Keys.Q);
        Frame(100, Keys.Q);
        Frame(100, Keys.Q);
        Frame(16);
        Frame(500);
        Focus(true, false);
        Frame(16, Keys.W);
        Frame(16, Keys.W);
        Focus(false, true);
        Frame(16);
        Frame(500);

        Section("the focus moves from A to B while a key is held, the stream is owned by A");
        Focus(true, false);
        Flags(press: true);
        Frame(16, Keys.W);
        Frame(100, Keys.W);
        Frame(200, Keys.W);
        Focus(false, true);
        Frame(100, Keys.W);
        Frame(100, Keys.W);
        Frame(16);
        Frame(500);
        Frame(16, Keys.W);
        Frame(16, Keys.W);
        Frame(16);
        Flags();
        Focus(true, false);
        Frame(500);

        Section("a held key whose repeat goes to two handlers, one of which marks it handled");
        Focus(true, true);
        Flags(repeat: true);
        Frame(16, Keys.R);
        for (int i = 0; i < 9; i++) { Frame(100, Keys.R); }
        Frame(16);
        Frame(500);
        Flags();
        Focus(true, false);

        Section("a handler that always handles");
        hostD.Can = true;
        Focus(false, true);
        Frame(16, Keys.A);
        Frame(16, Keys.A);
        Frame(16);
        Frame(16, Keys.S);
        for (int i = 0; i < 5; i++) { Frame(100, Keys.S); }
        Frame(16);
        hostD.Can = false;
        Focus(true, false);
        Frame(500);

        Section("the release of a key in the frame of a native text input of the same key (the press is not handled)");
        Frame(16, Keys.E);
        Frame(16, Keys.E);
        Text('\u00e9', Keys.E);
        Frame(16);
        Frame(16);
        Frame(500);
        Frame(16, Keys.E);
        Frame(16, Keys.E);
        Frame(16, Keys.E);
        Text('\u00e8', Keys.E);
        Frame(16);
        Frame(500);

        Section("the release of a key in the frame of a native text input of the same key (the press is handled by A)");
        Flags(press: true, release: true, click: true);
        Frame(16, Keys.E);
        Frame(16, Keys.E);
        Text('\u00e9', Keys.E);
        Frame(16);
        Frame(16);
        Frame(500);
        Flags();

        Section("the release of a key in the frame of a text input of another key, and of a key that stays held");
        Frame(16, Keys.E, Keys.S);
        Text('\u00e9', Keys.S);
        Frame(16, Keys.S);
        Text('\u00e9', Keys.E);
        Frame(16);
        Frame(500);

        Section("a key pressed, handled and released by A, then the focus moves to B, then the same key pressed, repeated and released again");
        Focus(true, false);
        Flags(press: true, release: true, click: true);
        Frame(16, Keys.K);
        Frame(16, Keys.K);
        Frame(16);
        Frame(500);
        Flags();
        Focus(false, true);
        Frame(16, Keys.K);
        for (int i = 0; i < 8; i++) { Frame(100, Keys.K); }
        Frame(16);
        Frame(500);
        Frame(16, Keys.K);
        Frame(16, Keys.K);
        Frame(16);
        Frame(500);

        Section("a key pressed and handled by A, still held when the focus moves to B and the key is released, then pressed again");
        Focus(true, false);
        Flags(press: true);
        Frame(16, Keys.K);
        Frame(100, Keys.K);
        Focus(false, true);
        Frame(16, Keys.K);
        Frame(16);
        Frame(500);
        Flags();
        Frame(16, Keys.K);
        Frame(100, Keys.K);
        Frame(300, Keys.K);
        Frame(16);
        Frame(500);

        Section("B handles the press, then A is focused again");
        Focus(false, true);
        Flags(bPress: true);
        Frame(16, Keys.K);
        Frame(300, Keys.K);
        Frame(100, Keys.K);
        Focus(true, false);
        Frame(100, Keys.K);
        Frame(16);
        Frame(500);
        Flags();
        Frame(16, Keys.K);
        Frame(16);
        Frame(500);

        Section("the click threshold: a hold of exactly 300 ms, then of 316 ms");
        Focus(true, false);
        Frame(16, Keys.A);
        Frame(300);
        Frame(500);
        Frame(16, Keys.A);
        Frame(316);
        Frame(500);

        return journal;
    }
}
