using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit.Abstractions;

namespace MGUI.Tests.Text;

/// <summary>
/// Zero allocation of the formatted text pipeline of <see cref="MGTextBlock"/> (<see cref="MGTextBlock.AllowsInlineFormatting"/> true, no explicit runs), step by step: the change of text
/// (tokenizing and parsing: <see cref="FormattedTextScanner"/>), then the measure and the lines, then the draw. Each test warms up, then counts the bytes allocated on its thread over a
/// measured loop (<see cref="AllocationWindow"/>), like the other zero allocation tests of this project. The values of the tags (colors, brushes, names, images) are kept by the scanner: a text
/// whose values were already seen allocates nothing, a value never seen allocates once.
/// </summary>
public class FormattedTextAllocationTests
{
    private const int WarmupIterations = 1000;
    private const int MeasuredIterations = 500;

    private readonly ITestOutputHelper Output;

    public FormattedTextAllocationTests(ITestOutputHelper output)
    {
        Output = output;
    }

    /// <summary>Two texts that alternate: they use the same tags and values and differ by their characters (or their length).</summary>
    public static IEnumerable<object[]> ChangeCases() => new[]
    {
        new object[] { "styles", "Value: [b]0.42[/b] and [i]italic[/i] here", "Value: [b]10.00[/b] and [i]italic[/i] here" },
        new object[] { "colors", "[color=Red]red[/color] [c=#00FF00]hex[/c] [fg=RGB(1,2,3)]rgb[/fg] [color=Red*0.5]half[/color]", "[color=Red]red![/color] [c=#00FF00]hex[/c] [fg=RGB(1,2,3)]rgb[/fg] [color=Red*0.5]half[/color]" },
        new object[] { "opacity and underline", "[o=0.5]faint[/o] [u]under[/u] [u=2 -1]two[/u] [u=3 1 Red]red[/u]", "[o=0.5]faint[/o] [u]under![/u] [u=2 -1]two[/u] [u=3 1 Red]red[/u]" },
        new object[] { "background and shadow", "[bg=Red]bg[/bg] [bg=Blue 2,2]padded[/bg] [bg=Red|Blue]gradient[/bg] [shadow=Black]sh[/shadow] [s=Red 1 2]off[/s]", "[bg=Red]bg![/bg] [bg=Blue 2,2]padded[/bg] [bg=Red|Blue]gradient[/bg] [shadow=Black]sh[/shadow] [s=Red 1 2]off[/s]" },
        new object[] { "tooltips and actions", "[tt=Info]tip[/tt] [action=Go]go[/action] [tooltip=Other tip][command=Run]both[/command][/tooltip]", "[tt=Info]tip![/tt] [action=Go]go[/action] [tooltip=Other tip][command=Run]both[/command][/tooltip]" },
        new object[] { "images", "text [img=Icon] and [img=Icon 16x16][img=Icon 8,12] end", "text [img=Icon] and [img=Icon 16x16][img=Icon 8,12] ends" },
        new object[] { "line breaks and tabs", "first\nsecond\r\nthird\there", "first\nsecond!\r\nthird\there" },
        new object[] { "escapes and a literal tail", "a \\[b] c \\\\[b]d[/b] [oops tail\nwith a line break", "a \\[b] c \\\\[b]d[/b] [oops tail!\nwith a line break" },
        new object[] { "plain", "0.42", "0.43" },
        new object[] { "plain, other length", "0.42", "10.00" },
        new object[] { "everything", "[b]a[/b] [i]b[/i] [o=0.5]c[/o] [u=2 -1 Red]d[/u] [color=Red]e[/color] [bg=Blue 1,1]f[/bg] [shadow=Black 2 2]g[/shadow] [tt=T][action=A]h[/action][/tt]\n[img=Icon 4x5] i \\[ [/b]",
            "[b]a[/b] [i]b[/i] [o=0.5]c[/o] [u=2 -1 Red]d[/u] [color=Red]e[/color] [bg=Blue 1,1]f[/bg] [shadow=Black 2 2]g[/shadow] [tt=T][action=A]h![/action][/tt]\n[img=Icon 4x5] i \\[ [/b]" },
    };

    private static long ChangeLoop(FormattedTextHarness harness, char[] first, char[] second, bool measureAndWrap = false)
    {
        MGTextBlock textBlock = harness.TextBlock;
        int index = 0;
        Size available = new(harness.Width, 300);

        void Step()
        {
            textBlock.SetText((index++ & 1) == 0 ? first : second);
            if (measureAndWrap)
            {
                textBlock.MeasureSelfOverride(available, out _);
                textBlock.UpdateLines();
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

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Theory]
    [MemberData(nameof(ChangeCases))]
    public void SetText_FromASpan_OnAFormattedText_AllocatesNothing(string name, string firstText, string secondText)
    {
        FormattedTextHarness harness = new(300, false);
        harness.TextBlock.Text = firstText;
        Assert.True(harness.TextBlock.AreFormattedRunsKnown, name);

        long allocated = ChangeLoop(harness, firstText.ToCharArray(), secondText.ToCharArray());

        Assert.Equal(0, allocated);
        //  The last text set was read by the scanner (it did not fall back to the legacy code) and is the one shown.
        Assert.True(harness.TextBlock.AreFormattedRunsKnown);
        Assert.Equal(MeasuredIterations % 2 == 0 ? secondText : firstText, harness.TextBlock.Text);
    }

    [Theory]
    [MemberData(nameof(ChangeCases))]
    public void SetText_FromAString_OnAFormattedText_AllocatesNothing(string name, string firstText, string secondText)
    {
        //  The setter that takes a string reads the characters of the string in place, so it allocates nothing either.
        FormattedTextHarness harness = new(300, false);
        MGTextBlock textBlock = harness.TextBlock;
        textBlock.Text = firstText;
        Assert.True(textBlock.AreFormattedRunsKnown, name);
        int index = 0;

        for (int i = 0; i < WarmupIterations; i++)
        {
            textBlock.Text = (index++ & 1) == 0 ? firstText : secondText;
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            textBlock.Text = (index++ & 1) == 0 ? firstText : secondText;
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(textBlock.AreFormattedRunsKnown);
    }

    [Fact]
    public void SetText_OnAFormattedTextThatTheLegacyPipelineReads_Allocates()
    {
        //  Counter-test: the same loop on the legacy pipeline (regular expressions, substrings, LINQ, an object per run) is far from zero.
        FormattedTextHarness harness = new(300, true);
        string firstText = "Value: [b]0.42[/b] and [color=Red]red text[/color] here";
        string secondText = "Value: [b]10.00[/b] and [color=Red]red text[/color] here";
        harness.TextBlock.Text = firstText;

        long allocated = ChangeLoop(harness, firstText.ToCharArray(), secondText.ToCharArray());

        Output.WriteLine($"legacy pipeline: {allocated} B over {MeasuredIterations} changes ({allocated / MeasuredIterations} B per change)");
        Assert.True(allocated > MeasuredIterations * 1000, $"allocated {allocated} bytes");
        Assert.False(harness.TextBlock.AreFormattedRunsKnown);
    }

    [Fact]
    public void ATextThatTheScannerDeclines_FallsBackToTheLegacyCode_AndAllocates()
    {
        //  A text the scanner cannot be sure to read like the legacy code (here a non ASCII character right after the '[') is read by the legacy code, which allocates: that case is routed there on purpose.
        FormattedTextHarness harness = new(300, false);
        string firstText = "[é] text [b]bold[/b]";
        string secondText = "[é] text [b]bold![/b]";
        harness.TextBlock.Text = firstText;
        Assert.False(harness.TextBlock.AreFormattedRunsKnown);

        long allocated = ChangeLoop(harness, firstText.ToCharArray(), secondText.ToCharArray());

        Output.WriteLine($"declined text, read by the legacy code: {allocated} B over {MeasuredIterations} changes");
        Assert.True(allocated > 0);
        Assert.False(harness.TextBlock.AreFormattedRunsKnown);
    }

    [Fact]
    public void AValueNeverSeen_AllocatesOnce_ThenNothing()
    {
        FormattedTextHarness harness = new(300, false);
        MGTextBlock textBlock = harness.TextBlock;
        string[] texts =
        {
            "[tt=NeverSeenToolTip]a[/tt]", "[action=NeverSeenAction]b[/action]", "[color=#A1B2C3]c[/color]", "[bg=#102030 7,7]d[/bg]", "[u=9 -3 #405060]e[/u]", "[shadow=#708090 5 6]f[/shadow]", "[img=Icon 33x44]",
        };
        char[][] spans = texts.Select(x => x.ToCharArray()).ToArray();
        textBlock.SetText("warm up".AsSpan());
        for (int i = 0; i < 100; i++)
        {
            textBlock.SetText("[b]warm[/b] up".AsSpan());
            textBlock.SetText("warm up".AsSpan());
        }

        long before = AllocationWindow.Start();
        foreach (char[] span in spans)
        {
            textBlock.SetText(span);
        }

        long firstPass = GC.GetAllocatedBytesForCurrentThread() - before;

        //  The same texts again: every value is known now
        textBlock.SetText("something else".AsSpan());
        before = AllocationWindow.Start();
        for (int round = 0; round < 50; round++)
        {
            foreach (char[] span in spans)
            {
                textBlock.SetText(span);
            }
        }

        long laterPasses = GC.GetAllocatedBytesForCurrentThread() - before;
        Output.WriteLine($"first pass: {firstPass} B for {spans.Length} texts with a never seen value; the next 50 passes: {laterPasses} B");
        Assert.True(firstPass > 0, "a value never seen costs its string");
        Assert.Equal(0, laterPasses);
        Assert.True(textBlock.AreFormattedRunsKnown);
    }

    [Fact]
    public void MoreValuesThanTheCachesHold_AreStillReadRight()
    {
        //  A cache that is full is emptied before it takes a new entry: a block that keeps showing new names reads them right and does not grow without bound.
        FormattedTextHarness legacy = new(300, true);
        FormattedTextHarness scanned = new(300, false);
        for (int i = 0; i < 3 * Core.UI.Text.FormattedTextScanner.CacheLimit; i++)
        {
            string text = $"[tt=Tip{i}][action=Act{i % 7}][color=#{i % 4096:X3}{i % 4096:X3}]x[/color][/action][/tt] [img=Icon {i % 90 + 1}x{i % 50 + 1}]";
            legacy.TextBlock.Text = text;
            scanned.TextBlock.Text = text;
            Assert.Equal(TextRunDescriber.Describe(legacy.TextBlock.Runs), TextRunDescriber.Describe(scanned.TextBlock.Runs));
        }

        Assert.True(scanned.TextBlock.AreFormattedRunsKnown);
    }

    [Fact]
    public void AnActionAppearingAndDisappearing_AllocatesNothing()
    {
        //  A text that gains and loses an action turns the tracking of mouse clicks on and off.
        FormattedTextHarness harness = new(300, false);
        string withAction = "[action=Go]click[/action] here";
        string withoutAction = "just text here";

        long allocated = ChangeLoop(harness, withAction.ToCharArray(), withoutAction.ToCharArray());

        Assert.Equal(0, allocated);
    }
}
