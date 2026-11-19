using MGUI.Shared.Input;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Reflection;

namespace MGUI.Tests.Input;

/// <summary>
/// <see cref="KeyboardTracker"/> keeps one press, release, click and input stream per key, and one more press and stream per key for the press that a native text input creates alone;
/// each <see cref="KeyboardHandler"/> keeps one repeat per key (ADR-0034). They are created at their first use and refilled afterwards, with their handled state reset and, for a stream, a new
/// identity and no owner. The events delivered are the ones that <see cref="KeyboardEventsReplayTests"/> freezes; these tests pin the three delicate cases (the release of a key in the frame of a
/// native text input of the same key, the owner of a stream that does not survive into the next press of the key, the repeat of a key that goes to two handlers) and the zero allocation of the frames.
/// </summary>
public class KeyboardEventsReuseTests
{
    private const int WarmupIterations = 2000;
    private const int MeasuredIterations = 1000;

    private sealed class Host : IKeyboardHandlerHost
    {
        public bool Focus { get; set; }
        public bool HasKeyboardFocus() => Focus;
    }

    private static UpdateBaseArgs Args(int elapsedMilliseconds, params Keys[] keys)
        => new(TimeSpan.FromMilliseconds(elapsedMilliseconds), TimeSpan.FromMilliseconds(16), new MouseState(), new KeyboardState(keys));

    private static long OwnerId(KeyboardHandler handler)
        => (long)typeof(KeyboardHandler).GetProperty("StreamOwnerId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(handler);

    private const string EAcute = "\u00e9";

    [Fact]
    public void TheReleaseOfAKey_InTheFrameOfANativeTextInputOfTheSameKey_KeepsItsPressTimeStreamAndOwner_AndReachesTheSameHandlers()
    {
        InputTracker tracker = new();
        KeyboardTracker keyboard = tracker.Keyboard;
        Host hostA = new() { Focus = true };
        Host hostB = new();
        KeyboardHandler a = keyboard.CreateHandler(hostA, 20);
        KeyboardHandler b = keyboard.CreateHandler(hostB, 10);
        List<string> aLog = new();
        List<string> bLog = new();
        int frame = 0;
        a.Pressed += (_, e) =>
        {
            aLog.Add($"f{frame} pressed {e.PrintableValue} at {e.PressedAt.TotalMilliseconds} #{e.StreamId}");
            if (frame == 1)
            {
                e.SetHandledBy(hostA);
            }
        };
        a.Released += (_, e) => aLog.Add($"f{frame} released {e.PrintableValue} at {e.PressedArgs.PressedAt.TotalMilliseconds} #{e.StreamId} owned={e.Stream.IsOwnedBy(OwnerId(a))}");
        a.Clicked += (_, e) => aLog.Add($"f{frame} clicked {e.PrintableValue} at {e.PressedArgs.PressedAt.TotalMilliseconds} #{e.StreamId} owned={e.Stream.IsOwnedBy(OwnerId(a))}");
        b.Pressed += (_, e) => bLog.Add($"f{frame} pressed {e.PrintableValue}");
        b.Released += (_, e) => bLog.Add($"f{frame} released {e.PrintableValue}");
        b.Clicked += (_, e) => bLog.Add($"f{frame} clicked {e.PrintableValue}");

        tracker.Update(Args(0));
        keyboard.UpdateHandlers();
        frame = 1;
        tracker.Update(Args(100, Keys.E));
        BaseKeyPressedEventArgs held = keyboard.CurrentKeyPressedEvents[Keys.E];
        long heldStreamId = held.StreamId;
        TimeSpan heldPressedAt = held.PressedAt;
        keyboard.UpdateHandlers();
        frame = 2;
        tracker.Update(Args(116, Keys.E));
        keyboard.UpdateHandlers();

        //  E is released in the frame where a native text input of E is not consumed by any key press: the tracker also creates a press for the text input alone.
        frame = 3;
        keyboard.QueueTextInput('\u00e9', Keys.E);
        tracker.Update(Args(132));
        BaseKeyPressedEventArgs textOnly = keyboard.CurrentKeyPressedEvents[Keys.E];
        BaseKeyReleasedEventArgs released = keyboard.CurrentKeyReleasedEvents[Keys.E];
        BaseKeyClickedEventArgs clicked = keyboard.CurrentKeyClickedEvents[Keys.E];
        Assert.NotNull(textOnly);
        Assert.NotNull(released);
        Assert.NotNull(clicked);
        Assert.Equal(EAcute, textOnly.PrintableValue);
        Assert.Equal(TimeSpan.FromMilliseconds(132), textOnly.PressedAt);
        Assert.NotSame(held, textOnly);
        Assert.NotSame(held.Stream, textOnly.Stream);
        Assert.NotEqual(heldStreamId, textOnly.StreamId);
        Assert.False(textOnly.Stream.HasOwner);
        Assert.Same(held, released.PressedArgs);
        Assert.Same(held, clicked.PressedArgs);
        Assert.Equal(heldPressedAt, released.PressedArgs.PressedAt);
        Assert.Equal(heldStreamId, released.StreamId);
        Assert.Equal(heldStreamId, clicked.StreamId);
        Assert.True(released.Stream.HasOwner);
        Assert.True(released.Stream.IsOwnedBy(OwnerId(a)));
        Assert.Equal(TimeSpan.FromMilliseconds(32), released.HeldDuration);
        keyboard.UpdateHandlers();

        string[] aFrame3 = aLog.FindAll(x => x.StartsWith("f3")).ToArray();
        Assert.Equal(new[]
        {
            $"f3 pressed {EAcute} at 132 #{textOnly.StreamId}",
            $"f3 released e at 100 #{heldStreamId} owned=True",
            $"f3 clicked e at 100 #{heldStreamId} owned=True",
        }, aFrame3);
        Assert.Empty(bLog);
    }

    [Fact]
    public void TheOwnerOfAStream_DoesNotSurviveIntoTheNextPressOfTheSameKey_ThePressRepeatReleaseAndClickGoToTheNewFocus()
    {
        InputTracker tracker = new();
        KeyboardTracker keyboard = tracker.Keyboard;
        Host hostA = new() { Focus = true };
        Host hostB = new();
        KeyboardHandler a = keyboard.CreateHandler(hostA, 20);
        KeyboardHandler b = keyboard.CreateHandler(hostB, 10);
        a.RepeatPolicy.InitialDelay = b.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(300);
        a.RepeatPolicy.Interval = b.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(100);
        List<string> aLog = new();
        List<string> bLog = new();
        a.Pressed += (_, e) => { aLog.Add("pressed"); e.SetHandledBy(hostA); };
        a.KeyRepeat += (_, _) => aLog.Add("repeat");
        a.Released += (_, _) => aLog.Add("released");
        a.Clicked += (_, _) => aLog.Add("clicked");
        b.Pressed += (_, e) => bLog.Add("pressed");
        b.KeyRepeat += (_, _) => bLog.Add("repeat");
        b.Released += (_, _) => bLog.Add("released");
        b.Clicked += (_, _) => bLog.Add("clicked");

        void Frame(int ms, params Keys[] keys)
        {
            tracker.Update(Args(ms, keys));
            keyboard.UpdateHandlers();
        }

        Frame(0);
        Frame(100, Keys.K);
        KeyboardInputStream firstStream = keyboard.CurrentKeyPressedEvents[Keys.K].Stream;
        long firstId = firstStream.Id;
        Assert.True(firstStream.IsOwnedBy(OwnerId(a)));
        Frame(116);
        Assert.Equal(new[] { "pressed", "released", "clicked" }, aLog);
        Assert.Empty(bLog);
        aLog.Clear();

        //  The focus moves to B, and K is pressed again: the stream of the new press has no owner, however the old one was owned by A.
        hostA.Focus = false;
        hostB.Focus = true;
        tracker.Update(Args(700, Keys.K));
        KeyboardInputStream secondStream = keyboard.CurrentKeyPressedEvents[Keys.K].Stream;
        Assert.False(secondStream.HasOwner);
        Assert.NotEqual(firstId, secondStream.Id);
        Assert.Same(keyboard.CurrentKeyPressedEvents[Keys.K], secondStream.InitialPressedArgs);
        Assert.Equal(TimeSpan.FromMilliseconds(700), secondStream.StartedAt);
        keyboard.UpdateHandlers();
        Frame(1000, Keys.K);
        Frame(1100, Keys.K);
        Frame(1200);
        Assert.Equal(new[] { "pressed", "repeat", "repeat", "released" }, bLog);
        Assert.Empty(aLog);

        //  A short tap with the focus on B: its click is delivered to B too.
        bLog.Clear();
        Frame(2000, Keys.K);
        Frame(2016);
        Assert.Equal(new[] { "pressed", "released", "clicked" }, bLog);
        Assert.Empty(aLog);
    }

    [Fact]
    public void TheRepeatOfAKey_BelongsToEachHandler_WithItsOwnHandledState()
    {
        InputTracker tracker = new();
        KeyboardTracker keyboard = tracker.Keyboard;
        Host hostA = new() { Focus = true };
        Host hostB = new() { Focus = true };
        KeyboardHandler a = keyboard.CreateHandler(hostA, 20);
        KeyboardHandler b = keyboard.CreateHandler(hostB, 10);
        a.RepeatPolicy.InitialDelay = b.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(300);
        a.RepeatPolicy.Interval = b.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(100);
        List<BaseKeyRepeatedEventArgs> aRepeats = new();
        List<BaseKeyRepeatedEventArgs> bRepeats = new();
        List<bool> handledOnReceipt = new();
        a.KeyRepeat += (_, e) =>
        {
            aRepeats.Add(e);
            handledOnReceipt.Add(e.IsHandled);
            e.SetHandledBy(hostA);
        };
        b.KeyRepeat += (_, e) =>
        {
            bRepeats.Add(e);
            handledOnReceipt.Add(e.IsHandled);
        };

        void Frame(int ms, params Keys[] keys)
        {
            tracker.Update(Args(ms, keys));
            keyboard.UpdateHandlers();
        }

        Frame(0);
        Frame(100, Keys.R);
        Frame(200, Keys.R);
        Frame(400, Keys.R);
        Assert.Single(aRepeats);
        Assert.Single(bRepeats);
        BaseKeyRepeatedEventArgs aRepeat = aRepeats[0];
        BaseKeyRepeatedEventArgs bRepeat = bRepeats[0];
        Assert.NotSame(aRepeat, bRepeat);
        Assert.True(aRepeat.IsHandled);
        Assert.Same(hostA, aRepeat.HandledBy);
        Assert.False(bRepeat.IsHandled);
        Assert.Equal(TimeSpan.FromMilliseconds(300), aRepeat.HeldDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(100), aRepeat.PressedAt);
        Assert.Equal(TimeSpan.FromMilliseconds(400), aRepeat.RepeatedAt);

        Frame(500, Keys.R);
        Frame(600, Keys.R);
        Assert.Equal(3, aRepeats.Count);
        Assert.Equal(3, bRepeats.Count);
        Assert.All(aRepeats, x => Assert.Same(aRepeat, x));
        Assert.All(bRepeats, x => Assert.Same(bRepeat, x));
        Assert.Equal(TimeSpan.FromMilliseconds(600), aRepeat.RepeatedAt);
        Assert.Equal(TimeSpan.FromMilliseconds(600), bRepeat.RepeatedAt);
        //  Each repeat started not handled for both handlers, whatever the handler before it did with its own.
        Assert.All(handledOnReceipt, x => Assert.False(x));
        Assert.True(aRepeat.IsHandled);
        Assert.False(bRepeat.IsHandled);
    }

    [Fact]
    public void ThePressReleaseAndClick_AreTheSameInstancesAtEachPress_WithTheValuesOfEachOne_AndAStreamThatRestarts()
    {
        InputTracker tracker = new();
        KeyboardTracker keyboard = tracker.Keyboard;
        Host host = new() { Focus = true };
        tracker.Update(Args(0));

        tracker.Update(Args(100, Keys.A));
        BaseKeyPressedEventArgs press = keyboard.CurrentKeyPressedEvents[Keys.A];
        KeyboardInputStream stream = press.Stream;
        long firstId = stream.Id;
        press.SetHandledBy(host);
        Assert.True(stream.TryAdoptOwner(7));
        Assert.Same(press, stream.InitialPressedArgs);
        tracker.Update(Args(116));
        BaseKeyReleasedEventArgs release = keyboard.CurrentKeyReleasedEvents[Keys.A];
        BaseKeyClickedEventArgs click = keyboard.CurrentKeyClickedEvents[Keys.A];
        release.SetHandledBy(host);
        click.SetHandledBy(host);
        Assert.Equal(TimeSpan.FromMilliseconds(16), release.HeldDuration);
        Assert.Equal("a", click.PrintableValue);

        tracker.Update(Args(800, Keys.A));
        Assert.Same(press, keyboard.CurrentKeyPressedEvents[Keys.A]);
        Assert.Same(stream, press.Stream);
        Assert.NotEqual(firstId, stream.Id);
        Assert.Equal(TimeSpan.FromMilliseconds(800), stream.StartedAt);
        Assert.Equal(TimeSpan.FromMilliseconds(800), press.PressedAt);
        Assert.Same(press, stream.InitialPressedArgs);
        Assert.False(stream.HasOwner);
        Assert.False(press.IsHandled);
        Assert.Null(press.HandledBy);
        tracker.Update(Args(1400));
        Assert.Same(release, keyboard.CurrentKeyReleasedEvents[Keys.A]);
        Assert.Null(keyboard.CurrentKeyClickedEvents[Keys.A]);
        Assert.False(release.IsHandled);
        Assert.Equal(TimeSpan.FromMilliseconds(600), release.HeldDuration);

        tracker.Update(Args(2000, Keys.A));
        tracker.Update(Args(2016));
        Assert.Same(click, keyboard.CurrentKeyClickedEvents[Keys.A]);
        Assert.False(click.IsHandled);
        Assert.Null(click.HandledBy);

        //  Another key has its own instances and its own stream.
        tracker.Update(Args(3000, Keys.S));
        Assert.NotSame(press, keyboard.CurrentKeyPressedEvents[Keys.S]);
        Assert.NotSame(stream, keyboard.CurrentKeyPressedEvents[Keys.S].Stream);
        Assert.Equal(Keys.S, keyboard.CurrentKeyPressedEvents[Keys.S].Stream.Key);
    }

    public static IEnumerable<object[]> KeySets() => new[]
    {
        new object[] { new[] { Keys.A } },
        new object[] { new[] { Keys.Left } },
        new object[] { new[] { Keys.LeftShift, Keys.LeftControl, Keys.A } },
        new object[] { new[] { Keys.RightShift, Keys.D1 } },
    };

    [Theory]
    [MemberData(nameof(KeySets))]
    public void KeyFrames_WithPressedRepeatReleasedAndClickedSubscribers_AllocateNothing(Keys[] keys)
    {
        InputTracker tracker = new();
        KeyboardTracker keyboard = tracker.Keyboard;
        Host first = new() { Focus = true };
        Host second = new() { Focus = true };
        KeyboardHandler a = keyboard.CreateHandler(first, 20);
        KeyboardHandler b = keyboard.CreateHandler(second, 10, false, true);
        a.RepeatPolicy.InitialDelay = b.RepeatPolicy.InitialDelay = TimeSpan.FromMilliseconds(300);
        a.RepeatPolicy.Interval = b.RepeatPolicy.Interval = TimeSpan.FromMilliseconds(100);
        int events = 0;
        a.Pressed += (_, e) =>
        {
            events++;
            if (e.IsAlphanumeric())
            {
                events++;
            }

            e.SetHandledBy(first);
        };
        a.Released += (_, e) => events += e.IsAlphanumeric() ? 2 : 1;
        a.Clicked += (_, e) => events += e.IsAlphanumeric() ? 2 : 1;
        a.KeyRepeat += (_, _) => events++;
        b.Pressed += (_, _) => events++;
        b.Released += (_, _) => events++;
        b.Clicked += (_, _) => events++;
        b.KeyRepeat += (_, _) => events++;

        //  A tap (press, release, click), then a hold with repeats (press, five held frames of 100 ms, release without click), then a pause.
        int total = WarmupIterations + MeasuredIterations + 11;
        UpdateBaseArgs[] frames = new UpdateBaseArgs[total];
        int clock = 0;
        for (int i = 0; i < total; i++)
        {
            int step = i % 11;
            clock += step switch { <= 3 => 16, <= 9 => 100, _ => 500 };
            bool isDown = step is 0 or (>= 3 and <= 8);
            frames[i] = isDown ? Args(clock, keys) : Args(clock);
        }

        int frame = 0;
        long bytes = Measure(() =>
        {
            tracker.Update(frames[frame++]);
            keyboard.UpdateHandlers();
        });

        Assert.Equal(0, bytes);
        Assert.True(events > 1000, $"{events} events");
    }

    [Fact]
    public void NativeTextInputFrames_OfALatinCharacter_WithAndWithoutAKeyPress_AllocateNothing()
    {
        InputTracker tracker = new();
        KeyboardTracker keyboard = tracker.Keyboard;
        Host host = new() { Focus = true };
        KeyboardHandler handler = keyboard.CreateHandler(host, 10);
        int events = 0;
        handler.Pressed += (_, _) => events++;
        handler.Released += (_, _) => events++;
        handler.Clicked += (_, _) => events++;

        //  Four frames: a text input with a press of its key, the release, a text input with no key, a pause.
        int total = WarmupIterations + MeasuredIterations + 4;
        UpdateBaseArgs[] frames = new UpdateBaseArgs[total];
        int clock = 0;
        for (int i = 0; i < total; i++)
        {
            clock += 16;
            frames[i] = (i & 3) == 0 ? Args(clock, Keys.E) : Args(clock);
        }

        int frame = 0;
        long bytes = Measure(() =>
        {
            int step = frame & 3;
            if (step == 0 || step == 2)
            {
                keyboard.QueueTextInput('\u00e9', Keys.E);
            }

            tracker.Update(frames[frame++]);
            keyboard.UpdateHandlers();
        });

        Assert.Equal(0, bytes);
        Assert.True(events > 1000, $"{events} events");
    }

    private static long Measure(Action Action)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            Action();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            Action();
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
