using System.Globalization;
using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit.Abstractions;

namespace MGUI.Tests.Text;

/// <summary>
/// Zero allocation of the measure, the lines, the local text invalidation modes and the draw of a formatted text (<see cref="MGTextBlock.AllowsInlineFormatting"/> true), after the change
/// of text that <see cref="FormattedTextAllocationTests"/> covers. The texts here have no tooltip and no action: the bounds those keep are covered with the reveal of the text.
/// Each test warms up, then counts the bytes allocated on its thread over a measured loop (<see cref="AllocationWindow"/>).
/// </summary>
public class FormattedTextLayoutAllocationTests
{
    private const int WarmupIterations = 1000;
    private const int MeasuredIterations = 500;

    private readonly ITestOutputHelper Output;

    public FormattedTextLayoutAllocationTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private static long ChangeMeasureAndWrap(FormattedTextHarness harness, char[] first, char[] second)
    {
        MGTextBlock textBlock = harness.TextBlock;
        int index = 0;
        Size available = new(harness.Width, 300);

        void Step()
        {
            textBlock.SetText((index++ & 1) == 0 ? first : second);
            textBlock.MeasureSelfOverride(available, out _);
            textBlock.UpdateLines();
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

    private static bool HasToolTipOrAction(string text)
        => text.Contains("[tt=") || text.Contains("[tooltip=") || text.Contains("[action=") || text.Contains("[command=");

    /// <summary>The texts of <see cref="FormattedTextAllocationTests.ChangeCases"/> that have no tooltip and no action, in a block that wraps and in one that does not.</summary>
    public static IEnumerable<object[]> LayoutCases()
    {
        foreach (object[] change in FormattedTextAllocationTests.ChangeCases())
        {
            if (HasToolTipOrAction((string)change[1]))
            {
                continue;
            }

            foreach ((bool wrap, int width) in new[] { (false, 300), (true, 60) })
            {
                yield return new object[] { change[0], change[1], change[2], wrap, width };
            }
        }
    }

    [Theory]
    [MemberData(nameof(LayoutCases))]
    public void TheMeasureAndTheLines_AfterAChangeOfFormattedText_AllocateNothing(string name, string firstText, string secondText, bool wrap, int width)
    {
        FormattedTextHarness harness = new(width, false, wrap);
        harness.TextBlock.Text = firstText;
        Assert.True(harness.TextBlock.AreFormattedRunsKnown, name);

        long allocated = ChangeMeasureAndWrap(harness, firstText.ToCharArray(), secondText.ToCharArray());

        Assert.Equal(0, allocated);
        //  The measure and the lines did their work on the last text.
        Assert.True(harness.TextBlock.Lines.Count >= 1);
        Assert.True(harness.TextBlock.AreFormattedRunsKnown);
    }

    [Fact]
    public void TheMeasureAndTheLines_OfALegacyFormattedText_Allocate()
    {
        //  Counter-test: the same loop on the legacy pipeline (an object per line and per run, LINQ, substrings).
        FormattedTextHarness harness = new(60, true);
        string firstText = "Value: [b]0.42[/b] and [color=Red]red text[/color] here";
        string secondText = "Value: [b]10.00[/b] and [color=Red]red text[/color] here";
        harness.TextBlock.Text = firstText;

        long allocated = ChangeMeasureAndWrap(harness, firstText.ToCharArray(), secondText.ToCharArray());

        Output.WriteLine($"legacy pipeline: {allocated} B over {MeasuredIterations} iterations ({allocated / MeasuredIterations} B per change, measure and lines)");
        Assert.True(allocated > MeasuredIterations * 1000);
    }

    /// <summary>Two texts that measure the same (6 px per character, whatever the tags) and so keep a stable footprint local.</summary>
    public static IEnumerable<object[]> LocalCases()
    {
        foreach (MGTextInvalidationMode mode in new[] { MGTextInvalidationMode.ContentOnly, MGTextInvalidationMode.ReflowLocal })
        {
            yield return new object[] { "number", "Value: [b]0.42[/b] and [color=Red]red[/color]", "Value: [b]0.43[/b] and [color=Red]red[/color]", false, 300, mode };
            yield return new object[] { "background and underline", "[bg=Red 1,1]ab[/bg] [u=2 -1 Blue]cd[/u] [shadow=Black 2 2]ef[/shadow]", "[bg=Red 1,1]ab[/bg] [u=2 -1 Blue]ce[/u] [shadow=Black 2 2]ef[/shadow]", false, 300, mode };
            yield return new object[] { "wrapped", "The quick [b]brown[/b] fox jumps over the [i]lazy[/i] dog while the sleepy cat watches.", "The quick [b]brown[/b] fox jumps over the [i]lazy[/i] dog while the sleepy cat watchez.", true, 120, mode };
            yield return new object[] { "images", "text [img=Icon 16x16] and [img=Icon] end", "text [img=Icon 16x16] and [img=Icon] enb", true, 120, mode };
        }
    }

    private static long LocalChangeLoop(FormattedTextHarness harness, string firstText, string secondText, MGTextInvalidationMode mode)
    {
        MGTextBlock textBlock = harness.TextBlock;
        textBlock.Text = firstText;
        harness.Frame();
        harness.Frame();
        //  There is a cached measurement to compare against, as after any layout: the local branch re-measures each of them.
        textBlock.MeasureSelfOverride(new Size(harness.Width, 300), out _);
        harness.TextBlock.ResetCount();
        char[] first = firstText.ToCharArray();
        char[] second = secondText.ToCharArray();
        int index = 0;

        for (int i = 0; i < WarmupIterations; i++)
        {
            textBlock.SetText((index++ & 1) == 0 ? first : second, mode);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            textBlock.SetText((index++ & 1) == 0 ? first : second, mode);
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Theory]
    [MemberData(nameof(LocalCases))]
    public void ALocalChangeOfFormattedText_OnAStableFootprint_AllocatesNothing_AndKeepsTheParentLayout(string name, string firstText, string secondText, bool wrap, int width, MGTextInvalidationMode mode)
    {
        FormattedTextHarness harness = new(width, false, wrap, stableFootprint: true);

        long allocated = LocalChangeLoop(harness, firstText, secondText, mode);

        Assert.Equal(0, allocated);
        //  The local branch was taken: the lines were computed in the setter and the parent layout was not requested.
        Assert.Equal(0, harness.TextBlock.LayoutChangedCallCount);
        Assert.True(harness.TextBlock.AreFormattedRunsKnown, name);

        //  The lines are the ones of the last text, as a legacy text block lays it out
        FormattedTextHarness legacy = new(width, true, wrap, stableFootprint: true);
        legacy.TextBlock.Text = harness.TextBlock.Text;
        legacy.Frame();
        legacy.Frame();
        Assert.Equal(TextRunDescriber.Describe(legacy.TextBlock.Lines), TextRunDescriber.Describe(harness.TextBlock.Lines));
    }

    [Fact]
    public void ALocalChangeOfALegacyFormattedText_Allocates()
    {
        //  Counter-test: the same local change on the legacy pipeline.
        FormattedTextHarness harness = new(300, true, false, stableFootprint: true);

        long allocated = LocalChangeLoop(harness, "Value: [b]0.42[/b] and [color=Red]red[/color]", "Value: [b]0.43[/b] and [color=Red]red[/color]", MGTextInvalidationMode.ContentOnly);

        Output.WriteLine($"legacy pipeline: {allocated} B over {MeasuredIterations} local changes ({allocated / MeasuredIterations} B per local change)");
        Assert.True(allocated > MeasuredIterations * 1000);
    }

    /// <summary>Texts drawn with backgrounds, underlines (default and with a brush), shadows, opacities, images: no tooltip and no action.</summary>
    public static IEnumerable<object[]> DrawCases()
    {
        string[][] texts =
        {
            new[] { "Value: [b]0.42[/b] and [color=Red]red text[/color] here", "Value: [b]10.00[/b] and [color=Red]red text[/color] here" },
            new[] { "[bg=Red 2,1]background[/bg] and [bg=Blue]more[/bg] [bg=Red|Blue]gradient[/bg]", "[bg=Red 2,1]background![/bg] and [bg=Blue]more[/bg] [bg=Red|Blue]gradient[/bg]" },
            new[] { "[u]default underline[/u] [u=3 1 Green]own brush[/u] [u=2 -1]offset[/u]", "[u]default underline![/u] [u=3 1 Green]own brush[/u] [u=2 -1]offset[/u]" },
            new[] { "[shadow=Black 2 2]shadow[/shadow] [s=Red]red[/s] [o=0.5]faint[/o] [c=#00FF0080]green[/c]", "[shadow=Black 2 2]shadow![/shadow] [s=Red]red[/s] [o=0.5]faint[/o] [c=#00FF0080]green[/c]" },
            new[] { "text [img=Icon 16x16] more text that wraps around the line [img=Icon][img=Icon 8,12] end", "text [img=Icon 16x16] more text that wraps around the line [img=Icon][img=Icon 8,12] ends" },
            new[] { "first\n[b]second[/b]\r\nthird with [i]a tail[/i]\n", "first\n[b]second![/b]\r\nthird with [i]a tail[/i]\n" },
            new[] { "everything [b]bold[/b] [i]italic[/i] [u=2 -1 Red]underlined[/u] [bg=Blue 1,1]background[/bg] [shadow=Black 1 1]shadow[/shadow] [img=Icon 4x5] and the rest of the text", "everything [b]bold[/b] [i]italic[/i] [u=2 -1 Red]underlined[/u] [bg=Blue 1,1]background[/bg] [shadow=Black 1 1]shadow[/shadow] [img=Icon 4x5] and the rest of the text!" },
        };

        foreach (string[] pair in texts)
        {
            foreach (bool wrap in new[] { false, true })
            {
                foreach (bool changesEachFrame in new[] { false, true })
                {
                    foreach (bool decorated in new[] { false, true })
                    {
                        yield return new object[] { pair[0], pair[1], wrap, changesEachFrame, decorated, false };
                    }
                }
            }
        }

        //  A text being revealed (TextProgress) is truncated by slicing.
        yield return new object[] { texts[0][0], texts[0][1], false, true, true, true };
        yield return new object[] { texts[5][0], texts[5][1], true, false, false, true };
        yield return new object[] { texts[4][0], texts[4][1], true, true, true, true };
    }

    private static long DrawLoop(FormattedTextHarness harness, NonRecordingDrawTransaction transaction, string firstText, string secondText, bool changesEachFrame, bool decorated, bool revealing)
    {
        MGTextBlock textBlock = harness.TextBlock;
        if (decorated)
        {
            //  The block itself underlines and shadows its text, over the markup's own settings
            textBlock.IsUnderlined = true;
            textBlock.IsShadowed = true;
            textBlock.ShadowOffset = new Point(2, 1);
        }

        if (revealing)
        {
            textBlock.TextProgress = 0.5;
        }

        char[] first = firstText.ToCharArray();
        char[] second = secondText.ToCharArray();
        textBlock.SetText(first);
        textBlock.UpdateLines();
        ElementDrawArgs drawArgs = harness.CreateDrawArgs(transaction);
        Size available = new(harness.Width, 300);
        int index = 0;

        void Step()
        {
            if (changesEachFrame)
            {
                textBlock.SetText((index++ & 1) == 0 ? first : second);
                textBlock.MeasureSelfOverride(available, out _);
                textBlock.UpdateLines();
            }

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
    [MemberData(nameof(DrawCases))]
    public void DrawSelf_OfAFormattedText_AllocatesNothing(string firstText, string secondText, bool wrap, bool changesEachFrame, bool decorated, bool revealing)
    {
        FormattedTextHarness harness = new(wrap ? 120 : 400, false, wrap);
        NonRecordingDrawTransaction transaction = new(harness.Runtime, DrawSettings.Default);

        long allocated = DrawLoop(harness, transaction, firstText, secondText, changesEachFrame, decorated, revealing);

        Assert.Equal(0, allocated);
        Assert.True(harness.TextBlock.AreFormattedRunsKnown);
    }

    [Fact]
    public void DrawSelf_OfALegacyFormattedText_Allocates()
    {
        //  Counter-test: the same draw on the legacy pipeline (a Lines loop that substrings and measures strings).
        FormattedTextHarness harness = new(120, true, true);
        NonRecordingDrawTransaction transaction = new(harness.Runtime, DrawSettings.Default);

        long allocated = DrawLoop(harness, transaction, "[u]default underline[/u] [bg=Red]background[/bg] text that wraps", "[u]default underline![/u] [bg=Red]background[/bg] text that wraps", false, false, false);

        Output.WriteLine($"legacy pipeline: {allocated} B over {MeasuredIterations} DrawSelf ({allocated / MeasuredIterations} B per draw)");
        Assert.True(allocated > 0, $"allocated {allocated} bytes");
    }

    [Fact]
    public void ADefaultUnderline_IsDrawn_WithoutCreatingABrush()
    {
        //  The default underline used to be drawn by a MGSolidFillBrush made for each draw.
        FormattedTextHarness harness = new(400, false, false, block => block.IsUnderlined = true);
        NonRecordingDrawTransaction transaction = new(harness.Runtime, DrawSettings.Default);

        long allocated = DrawLoop(harness, transaction, "plain underlined text", "plain underlined test", false, false, false);

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void AFormattedNumber_IsSet_Measured_AndDrawn_WithoutAllocating()
    {
        //  End to end, as a value label with markup does it every frame: format a float with "F2" into a reused buffer between tags, set it, measure and cut it in lines as the layout
        //  would, draw it.
        FormattedTextHarness harness = new(400, false, false);
        NonRecordingDrawTransaction transaction = new(harness.Runtime, DrawSettings.Default);
        MGTextBlock textBlock = harness.TextBlock;
        ElementDrawArgs drawArgs = harness.CreateDrawArgs(transaction);
        Size available = new(harness.Width, 300);
        const string Prefix = "Value: [b]";
        const string Suffix = "[/b] units";
        char[] reused = new char[64];
        Prefix.CopyTo(0, reused, 0, Prefix.Length);
        float value = 0.5f;

        void Step()
        {
            value += 0.37f;
            if (value > 1000f)
            {
                value = 0.5f;
            }

            Assert.True(value.TryFormat(reused.AsSpan(Prefix.Length), out int written, "F2", CultureInfo.InvariantCulture));
            Suffix.CopyTo(0, reused, Prefix.Length + written, Suffix.Length);
            textBlock.SetText(reused.AsSpan(0, Prefix.Length + written + Suffix.Length));
            textBlock.MeasureSelfOverride(available, out _);
            textBlock.UpdateLines();
            textBlock.DrawSelf(drawArgs, textBlock.LayoutBounds);
        }

        for (int i = 0; i < WarmupIterations * 2; i++)
        {
            Step();
        }

        long calls = transaction.CallCount;
        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations * 2; i++)
        {
            Step();
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(transaction.CallCount - calls >= MeasuredIterations * 2);
        Assert.Equal($"{Prefix}{value.ToString("F2", CultureInfo.InvariantCulture)}{Suffix}", textBlock.Text);
    }
}
