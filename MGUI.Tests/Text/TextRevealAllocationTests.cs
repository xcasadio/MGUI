using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit.Abstractions;

namespace MGUI.Tests.Text;

/// <summary>
/// Zero allocation of the progressive reveal of a text, driven by direct writes of <see cref="MGTextBlock.TextProgress"/> every frame (the setter, the measure, the draw), on plain and formatted
/// text, and of the bounds that the draw keeps for the tooltips and the actions of a text (their lists are reused from one draw to the next). The reveal driven by
/// <see cref="MGTextBlock.TextCharactersPerSecond"/> (the animation engine) is not covered here.
/// </summary>
public class TextRevealAllocationTests
{
    private const int WarmupIterations = 1000;
    private const int MeasuredIterations = 500;

    private readonly ITestOutputHelper Output;

    public TextRevealAllocationTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private const string RichText =
        "[b]Title[/b] [tt=Info]hover this tooltip text that wraps across the lines[/tt] and [action=Go]click this action text that also wraps[/action] with [bg=Red 1,1]a background[/bg] " +
        "[u=2 -1 Blue]underlined[/u] [shadow=Black 1 1]shadow[/shadow] [img=Icon 16x16] [tt=A][action=B]both on one text[/action][/tt]\nsecond line [color=Green]colored[/color]";

    private const string PlainSentence = "The quick brown fox jumps over the lazy dog while the sleepy cat watches the whole thing happen.";

    public static IEnumerable<object[]> RevealCases() => new[]
    {
        new object[] { "plain", PlainSentence, false, false },
        new object[] { "plain, changing text", PlainSentence, false, true },
        new object[] { "formatted", RichText, true, false },
        new object[] { "formatted, changing text", RichText, true, true },
    };

    /// <summary>Writes <see cref="MGTextBlock.TextProgress"/> every frame (a ramp that wraps around, with a null now and then), measures and draws.</summary>
    private static long RevealLoop(FormattedTextHarness harness, NonRecordingDrawTransaction transaction, string text, bool changesEachFrame)
    {
        MGTextBlock textBlock = harness.TextBlock;
        char[] first = text.ToCharArray();
        char[] second = (text + "!").ToCharArray();
        textBlock.SetText(first);
        textBlock.UpdateLines();
        ElementDrawArgs drawArgs = harness.CreateDrawArgs(transaction);
        Size available = new(harness.Width, 300);
        int frame = 0;

        void Step()
        {
            int tick = frame++;
            textBlock.TextProgress = tick % 97 == 96 ? null : (tick % 101) / 100.0;
            if (changesEachFrame)
            {
                textBlock.SetText((tick & 1) == 0 ? first : second);
                textBlock.UpdateLines();
            }

            textBlock.MeasureSelfOverride(available, out _);
            textBlock.DrawSelf(drawArgs, textBlock.LayoutBounds);
        }

        for (int i = 0; i < WarmupIterations; i++)
        {
            Step();
        }

        long calls = transaction.CallCount;
        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            Step();
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(transaction.CallCount - calls >= MeasuredIterations, "the measured loop did draw");
        return allocated;
    }

    [Theory]
    [MemberData(nameof(RevealCases))]
    public void ARevealDrivenByDirectWritesOfTextProgress_AllocatesNothing(string name, string text, bool formatted, bool changesEachFrame)
    {
        FormattedTextHarness harness = new(140, false, true, allowsInlineFormatting: formatted);
        NonRecordingDrawTransaction transaction = new(harness.Runtime, DrawSettings.Default);

        long allocated = RevealLoop(harness, transaction, text, changesEachFrame);

        Assert.Equal(0, allocated);
        Assert.Equal(formatted, harness.TextBlock.AreFormattedRunsKnown);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARevealOfALegacyText_Allocates(bool formatted)
    {
        //  Counter-test: the same reveal on the legacy pipeline (the loop over Lines substrings the text of a run, and measures it).
        FormattedTextHarness harness = new(140, true, true, allowsInlineFormatting: formatted);
        NonRecordingDrawTransaction transaction = new(harness.Runtime, DrawSettings.Default);

        long allocated = RevealLoop(harness, transaction, formatted ? RichText : PlainSentence, false);

        Output.WriteLine($"legacy pipeline, formatted={formatted}: {allocated} B over {MeasuredIterations} frames ({allocated / MeasuredIterations} B per frame)");
        Assert.True(allocated > MeasuredIterations * 100);
    }

    [Fact]
    public void TheTextProgressSetter_AllocatesNothing_AndTheRevealShowsWhatItShould()
    {
        //  Alone: just the writes (a value, null, the same value again), then the characters drawn at a few progresses.
        FormattedTextHarness harness = new(400, false, false, allowsInlineFormatting: true);
        MGTextBlock textBlock = harness.TextBlock;
        textBlock.Text = "[b]abcd[/b]efgh";
        textBlock.UpdateLines();

        for (int i = 0; i < WarmupIterations; i++)
        {
            textBlock.TextProgress = i % 2 == 0 ? 0.25 : null;
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            textBlock.TextProgress = i % 3 == 0 ? 0.25 : i % 3 == 1 ? 0.5 : null;
            textBlock.TextProgress = textBlock.TextProgress;
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);

        foreach ((double? progress, string expected) in new (double?, string)[] { (null, "abcdefgh"), (0.0, ""), (0.5, "abcd"), (0.75, "abcdef"), (1.0, "abcdefgh") })
        {
            textBlock.TextProgress = progress;
            GraphNoOpDrawTransaction recording = new(harness.Runtime, DrawSettings.Default);
            textBlock.DrawSelf(harness.CreateDrawArgs(recording), textBlock.LayoutBounds);
            Assert.Equal(expected, string.Concat(recording.DrawTextCalls.Select(x => x.Text)));
        }
    }

    //  ---- The bounds of the tooltips and of the actions ----

    [Fact]
    public void TheBoundsOfTheTooltipsAndTheActions_AreReused_FromOneDrawToTheNext()
    {
        //  A text that wraps so a name has several rectangles, and with two names that overlap.
        FormattedTextHarness harness = new(120, false, true);
        NonRecordingDrawTransaction transaction = new(harness.Runtime, DrawSettings.Default);
        MGTextBlock textBlock = harness.TextBlock;
        textBlock.Text = "[tt=A]a tooltip text that wraps over several lines of the block[/tt] [action=Go]an action text that wraps over several lines too[/action] [tt=B][action=Run]both[/action][/tt]";
        textBlock.UpdateLines();
        ElementDrawArgs drawArgs = harness.CreateDrawArgs(transaction);

        for (int i = 0; i < WarmupIterations; i++)
        {
            textBlock.DrawSelf(drawArgs, textBlock.LayoutBounds);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            textBlock.DrawSelf(drawArgs, textBlock.LayoutBounds);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheBoundsOfTheTooltipsAndTheActions_AreReused_WhenTheTextChangesEachFrame(bool revealing)
    {
        FormattedTextHarness harness = new(120, false, true);
        NonRecordingDrawTransaction transaction = new(harness.Runtime, DrawSettings.Default);
        if (revealing)
        {
            harness.TextBlock.TextProgress = 0.6;
        }

        long allocated = RevealLoop(harness, transaction, RichText, true);

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void LookingUpTheBounds_AfterADraw_AllocatesNothing()
    {
        //  What the bounds are for: the click on an action and the tooltip under the mouse. They are read after each draw, as the desktop does.
        FormattedTextHarness harness = new(120, false, true, block => block.VerticalContentAlignment = VerticalAlignment.Top);
        NonRecordingDrawTransaction transaction = new(harness.Runtime, DrawSettings.Default);
        CountingFormattedTextBlock textBlock = harness.TextBlock;
        harness.Desktop.Resources.AddCommand("Go", _ => { });
        harness.Desktop.Resources.AddNamedToolTip("A", new MGToolTip(harness.Window, textBlock, 80, 20));
        textBlock.Text = "[tt=A]a tooltip text that wraps over several lines of the block[/tt] [action=Go]an action text that wraps over several lines too[/action]";
        textBlock.UpdateLines();
        ElementDrawArgs drawArgs = harness.CreateDrawArgs(transaction);
        harness.MoveMouse(new Point(30, 5));
        int shown = 0;
        int invoked = 0;

        void Step()
        {
            textBlock.DrawSelf(drawArgs, textBlock.LayoutBounds);
            if (textBlock.TryInvokeActionAt(new Point(30, 5)))
            {
                invoked++;
            }

            if (textBlock.ProbeToolTip(out _))
            {
                shown++;
            }
        }

        for (int i = 0; i < WarmupIterations; i++)
        {
            Step();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            Step();
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Output.WriteLine($"{allocated} B for {MeasuredIterations} draws with a lookup of each ({shown} tooltips shown)");
        Assert.True(shown > MeasuredIterations, $"{shown} tooltips shown");
        Assert.Equal(0, allocated);
    }
}
