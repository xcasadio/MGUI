using MGUI.Core.UI;
using MGUI.Tests.Architecture;
using Xunit.Abstractions;

namespace MGUI.Tests.Text;

/// <summary>
/// Measurement bench of the text cases the allocation-free chantier still has to bring to zero: how many bytes the setter, <see cref="MGDesktop.Update"/> and
/// <see cref="MGDesktop.Draw(MGUI.Shared.Rendering.IUIDrawTransaction, float)"/> allocate per frame, with the text block in a window of a <see cref="TextBlockFixture"/>.<para/>
/// These tests assert no byte count: they write one line per case to the test output. They assert preconditions only (the draw calls something, the text takes the path
/// the case is about), so that a measurement cannot silently measure another path. The cases are not the steps of <see cref="TextBlockAllocationMeasurements"/>, which
/// measures the pipeline step by step on the corpus.
/// </summary>
[Collection(MGUI.Tests.Architecture.AllocationMeasurementsCollection.Name)]
public class TextCaseAllocationMeasurements
{
    private const string Sentence = "The quick brown fox jumps over the lazy dog";
    private const string LongerSentence = "The quick brown fox jumps over the lazy dog again";

    /// <summary>An e with an acute accent, a non ASCII letter (U+00E9).</summary>
    private const char AcuteE = (char)0xE9;

    private readonly ITestOutputHelper Output;

    public TextCaseAllocationMeasurements(ITestOutputHelper output)
    {
        Output = output;
    }

    private static TextBlockFixture CreateFixture(bool allowsInlineFormatting, string text)
    {
        TextBlockFixture fixture = new(TextBlockCorpusId.OneLineSentence, allowsInlineFormatting);
        fixture.TextBlock.Text = text;
        fixture.Frame();
        fixture.Frame();
        return fixture;
    }

    /// <summary>Measures the frames of <paramref name="fixture"/> with <paramref name="prepare"/> before each update, and writes the line of the case.</summary>
    private FrameBytes Run(string label, TextBlockFixture fixture, Action<int> prepare = null, string setterNote = null)
    {
        long callsBefore = fixture.Transaction.CallCount;
        FrameBytes bytes = FrameAllocationMeter.Measure(prepare, fixture.Update, () => fixture.Desktop.Draw(fixture.Transaction));
        Assert.True(fixture.Transaction.CallCount > callsBefore, $"{label}: the measured frames drew nothing");
        string setter = prepare == null ? "" : $"setter {bytes.Prepare} B/frame{setterNote}, ";
        Output.WriteLine($"{label}: {setter}Update {bytes.Update} B/frame, Draw {bytes.Draw} B/frame");
        return bytes;
    }

    private static string TextOf(MGTextBlock textBlock)
    {
        System.Text.StringBuilder builder = new();
        foreach (MGUI.Core.UI.Text.MGTextRun run in textBlock.Runs)
        {
            if (run is MGUI.Core.UI.Text.MGTextRunText text)
            {
                builder.Append(text.Text);
            }
        }

        return builder.ToString();
    }

    // ---- a. plain text block, underlined ----

    [Fact]
    public void PlainTextUnderlined()
    {
        TextBlockFixture reference = CreateFixture(false, Sentence);
        Run("plain text block (no markup), not underlined (reference), unchanged text", reference);

        TextBlockFixture fixture = CreateFixture(false, Sentence);
        fixture.TextBlock.IsUnderlined = true;
        fixture.Frame();
        fixture.Frame();
        Assert.True(fixture.TextBlock.IsUnderlined);
        Run("plain text block (no markup), underlined, unchanged text", fixture);
    }

    // ---- b. plain text block with a background ----

    [Fact]
    public void TextWithABackground()
    {
        //  A plain block (AllowsInlineFormatting false) cannot get a text background through the existing API: MGTextBlock builds its plain settings from
        //  DefaultTextRunSettings, whose background is always default (MGTextBlock.UpdatePlainRuns), and only a [bg] tag sets one, which makes the text formatted.
        //  The two cases below are the closest ones that exist, and are labelled as what they are.
        TextBlockFixture formatted = CreateFixture(true, "The quick brown fox [bg=Red 1,1]jumps over[/bg] the lazy dog");
        Run("formatted text block with a [bg] tag (not a plain block), unchanged text", formatted);

        TextBlockFixture plain = CreateFixture(false, Sentence);
        plain.TextBlock.BackgroundBrush = new VisualStateFillBrush(new MGUI.Core.UI.Brushes.FillBrushes.MGSolidFillBrush(Microsoft.Xna.Framework.Color.Blue));
        plain.Frame();
        plain.Frame();
        Run("plain text block with a background brush on the element (BackgroundBrush, not a text background), unchanged text", plain);
    }

    // ---- c. reveal driven by TextCharactersPerSecond ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RevealByCharactersPerSecond(bool allowsInlineFormatting)
    {
        TextBlockFixture fixture = CreateFixture(allowsInlineFormatting, Sentence);
        MGTextBlock textBlock = fixture.TextBlock;
        //  20 characters per second: 43 characters take about 2.1 s, so a reveal started by a text change is still running 100 frames (1.6 s) later.
        textBlock.TextCharactersPerSecond = 20;
        string[] texts = { Sentence, LongerSentence };
        textBlock.Text = texts[1];
        for (int i = 0; i < 5; i++)
        {
            fixture.Frame();
        }

        Assert.True(textBlock.TextProgress > 0.0 && textBlock.TextProgress < 1.0, $"the reveal is not running (progress {textBlock.TextProgress})");

        int changes = 0;
        Action<int> change = frame =>
        {
            if (frame % 100 == 0)
            {
                textBlock.Text = texts[(frame / 100) & 1];
                changes++;
            }
        };

        string kind = allowsInlineFormatting ? "formatted" : "plain";
        FrameBytes bytes = Run($"{kind} text block, TextCharactersPerSecond 20, text changed every 100 frames", fixture, change, " (average over all frames)");
        Assert.True(textBlock.TextProgress.HasValue);
        Output.WriteLine($"{kind} text block, TextCharactersPerSecond 20: {changes} text changes in {FrameAllocationMeter.WarmupFrames + FrameAllocationMeter.MeasuredFrames} frames; "
            + $"in the {FrameAllocationMeter.MeasuredFrames} measured frames, per change: setter {bytes.PrepareTotal / (FrameAllocationMeter.MeasuredFrames / 100)} B, "
            + $"Update {bytes.UpdateTotal / (FrameAllocationMeter.MeasuredFrames / 100)} B, Draw {bytes.DrawTotal / (FrameAllocationMeter.MeasuredFrames / 100)} B (the 100 frames of one change summed)");
    }

    // ---- d. a tag whose value is rejected ----

    [Fact]
    public void TagWithARejectedValueChangedEveryFrame()
    {
        string[] texts = { "[color=NotAColor]first text[/color] and more", "[color=NotAColor]second text[/color] and more" };
        TextBlockFixture fixture = CreateFixture(true, texts[0]);
        MGTextBlock textBlock = fixture.TextBlock;

        //  The value is rejected: the '[' is a literal character, and the scanner still read the text (no fallback to the legacy pipeline).
        Assert.True(textBlock.AreFormattedRunsKnown);
        Assert.StartsWith("[color=NotAColor]", TextOf(textBlock));

        //  The scanner catches the exception of the rejected value: count the first chance exceptions of this thread over a few frames, outside the measured frames
        //  (the handler allocates).
        int threadId = Environment.CurrentManagedThreadId;
        int exceptions = 0;
        void OnFirstChance(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            if (Environment.CurrentManagedThreadId == threadId)
            {
                exceptions++;
            }
        }

        AppDomain.CurrentDomain.FirstChanceException += OnFirstChance;
        try
        {
            for (int i = 0; i < 10; i++)
            {
                textBlock.Text = texts[i & 1];
                fixture.Frame();
            }
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= OnFirstChance;
        }

        Output.WriteLine($"rejected tag value: {exceptions} first chance exceptions in 10 frames that change the text every frame");

        Run("formatted text with a rejected tag value ([color=NotAColor]), changed every frame between two strings", fixture,
            frame => textBlock.Text = texts[frame & 1]);
        Assert.True(textBlock.AreFormattedRunsKnown);
    }

    // ---- e. a non ASCII character near a '[' ----

    [Fact]
    public void NonAsciiCharacterNearAnOpenTagChangedEveryFrame()
    {
        //  The scanner finds no tag type after the '[' and a non ASCII character among the first ones: the text is ambiguous and the legacy pipeline reads it.
        string[] ambiguous = { $"Value [{AcuteE}] first text", $"Value [{AcuteE}] second text" };
        TextBlockFixture fixture = CreateFixture(true, ambiguous[0]);
        MGTextBlock textBlock = fixture.TextBlock;
        Assert.False(textBlock.AreFormattedRunsKnown, "the scanner read the text: it is not ambiguous");
        Run("formatted text with a non ASCII character right after a '[' (legacy fallback), changed every frame between two strings", fixture,
            frame => textBlock.Text = ambiguous[frame & 1]);
        Assert.False(textBlock.AreFormattedRunsKnown);

        //  Reference: the same texts with an ASCII character, read by the scanner.
        string[] ascii = { "Value [e] first text", "Value [e] second text" };
        TextBlockFixture asciiFixture = CreateFixture(true, ascii[0]);
        MGTextBlock asciiBlock = asciiFixture.TextBlock;
        Assert.True(asciiBlock.AreFormattedRunsKnown, "the scanner did not read the ASCII reference text");
        Run("reference: formatted text with an ASCII character after a '[' (scanner), changed every frame between two strings", asciiFixture,
            frame => asciiBlock.Text = ascii[frame & 1]);
    }
}
