using System.ComponentModel;
using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Text;

/// <summary>
/// <see cref="MGTextBlock.SetText(ReadOnlySpan{char})"/> and <see cref="MGTextBlock.SetText(ReadOnlySpan{char}, MGTextInvalidationMode)"/> on a plain text
/// (<see cref="MGTextBlock.AllowsInlineFormatting"/> false): they allocate nothing once the buffers have grown, and <see cref="MGTextBlock.Text"/>,
/// <see cref="MGTextBlock.Runs"/> and <see cref="MGTextBlock.Lines"/> read back what the string setter gives for the same characters.
/// </summary>
public class PlainTextSetTextTests
{
    private const int WarmupIterations = 2000;
    private const int MeasuredIterations = 1000;

    /// <summary>The runs the plain path used to build in <c>UpdateRuns</c>, taken from the tokenizer and the parser themselves.</summary>
    private static List<MGTextRun> LegacyRuns(string text)
        => MGTextRun.ParseRuns(FTTokenizer.TokenizeLineBreaks(text, true).ToList(), new MGTextRunConfig(false, false, 1, null, new MGTextRunUnderlineConfig(false), default, default)).ToList();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetText_FromASpan_OnAPlainText_AllocatesNothing(bool changesLength)
    {
        TextBlockFixture fixture = new(TextBlockCorpusId.ShortNumber, false);
        MGTextBlock textBlock = fixture.TextBlock;
        char[] first = "0.42".ToCharArray();
        char[] second = (changesLength ? "10.00" : "0.43").ToCharArray();
        int index = 0;

        for (int i = 0; i < WarmupIterations; i++)
        {
            textBlock.SetText((index++ & 1) == 0 ? first : second);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            textBlock.SetText((index++ & 1) == 0 ? first : second);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        //  The last text set is the one shown, and reading it (which builds the string) is outside the measured window.
        Assert.Equal((index - 1 & 1) == 0 ? new string(first) : new string(second), textBlock.Text);
    }

    [Fact]
    public void SetText_WithAMode_FromASpan_AndAnUnstableFootprint_AllocatesNothing()
    {
        //  ContentOnly without a stable footprint resolves to RelayoutParent: the setter does not compute the lines.
        TextBlockFixture fixture = new(TextBlockCorpusId.ShortNumber, false);
        MGTextBlock textBlock = fixture.TextBlock;
        char[] first = "0.42".ToCharArray();
        char[] second = "10.00".ToCharArray();
        int index = 0;

        for (int i = 0; i < WarmupIterations; i++)
        {
            textBlock.SetText((index++ & 1) == 0 ? first : second, MGTextInvalidationMode.ContentOnly);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            textBlock.SetText((index++ & 1) == 0 ? first : second, MGTextInvalidationMode.ContentOnly);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    public static IEnumerable<object[]> Texts() => new[]
    {
        "", "0.42", "word", "   ", " leading", "trailing ", "multiple   spaces here", "First line\nSecond line\nThird line",
        "a\r\nb", "a\rb", "a\n\nb", "a\n", "\n", "\r\n\r\n", "tab\there", "[b]not bold[/b] and [color=Red]not red[/color]", "back\\slash"
    }.Select(x => new object[] { x });

    [Theory]
    [MemberData(nameof(Texts))]
    public void TextRunsAndLines_ReadBack_WhatTheStringSetterGives(string text)
    {
        TextBlockFixture fromSpanFixture = new(TextBlockCorpusId.OneLineSentence, false);
        TextBlockFixture fromStringFixture = new(TextBlockCorpusId.OneLineSentence, false);
        MGTextBlock fromSpan = fromSpanFixture.TextBlock;
        MGTextBlock fromString = fromStringFixture.TextBlock;

        fromSpan.SetText(text.AsSpan());
        fromString.Text = text;
        fromSpan.UpdateLines();
        fromString.UpdateLines();

        Assert.Equal(text, fromSpan.Text);
        Assert.Equal(fromString.Text, fromSpan.Text);

        List<MGTextRun> expectedRuns = LegacyRuns(text);
        AssertRuns(expectedRuns, fromSpan.Runs);
        AssertRuns(expectedRuns, fromString.Runs);

        List<MGTextLine> expectedLines = MGTextLine.ParseLines(fromSpan, fromSpan.LayoutBounds.Width - fromSpan.HorizontalPadding, fromSpan.WrapText, expectedRuns, true).ToList();
        AssertLines(expectedLines, fromSpan.Lines);
        AssertLines(expectedLines, fromString.Lines);
    }

    [Fact]
    public void RunsAndLines_AreTheSameInstances_UntilTheyChange()
    {
        TextBlockFixture fixture = new(TextBlockCorpusId.MultiLine, false);
        MGTextBlock textBlock = fixture.TextBlock;

        textBlock.SetText("one\ntwo".AsSpan());
        var runs = textBlock.Runs;
        Assert.Same(runs, textBlock.Runs);
        textBlock.UpdateLines();
        var lines = textBlock.Lines;
        Assert.Same(lines, textBlock.Lines);

        textBlock.SetText("one\nthree".AsSpan());
        Assert.NotSame(runs, textBlock.Runs);
        Assert.Equal("three", ((MGTextRunText)textBlock.Runs[2]).Text);
    }

    [Fact]
    public void Lines_AreNullUntilTheyAreComputed_AndPropertyChangedFiresWhereItDid()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 300, 200);
        MGTextBlock textBlock = new(window, "start", Color.White, 12, false);
        List<string> notifications = new();
        textBlock.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        Assert.Null(textBlock.Lines);

        textBlock.SetText("0.42".AsSpan());
        int runsIndex = notifications.IndexOf(nameof(MGTextBlock.Runs));
        int textIndex = notifications.IndexOf(nameof(MGTextBlock.Text));
        Assert.True(runsIndex >= 0 && textIndex > runsIndex);
        Assert.DoesNotContain(nameof(MGTextBlock.Lines), notifications);
        Assert.Null(textBlock.Lines);

        notifications.Clear();
        textBlock.UpdateLines();
        Assert.Equal(new[] { nameof(MGTextBlock.Lines) }, notifications);
        Assert.NotNull(textBlock.Lines);
    }

    [Fact]
    public void SettingTheSameCharacters_DoesNothing()
    {
        TextBlockFixture fixture = new(TextBlockCorpusId.ShortNumber, false);
        MGTextBlock textBlock = fixture.TextBlock;
        List<string> notifications = new();
        textBlock.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        //  The text of the fixture is a string; the same characters from a span, then from the buffer, change nothing.
        textBlock.SetText("0.42".AsSpan());
        Assert.Empty(notifications);
        textBlock.SetText("0.43".AsSpan());
        Assert.Contains(nameof(MGTextBlock.Text), notifications);
        notifications.Clear();
        textBlock.SetText("0.43".AsSpan());
        textBlock.Text = "0.43";
        Assert.Empty(notifications);

        //  An empty span is the empty string; null is not the empty string.
        textBlock.SetText(ReadOnlySpan<char>.Empty);
        Assert.Equal("", textBlock.Text);
        notifications.Clear();
        textBlock.Text = null;
        Assert.Contains(nameof(MGTextBlock.Text), notifications);
        Assert.Null(textBlock.Text);
        Assert.Empty(textBlock.Runs);
        notifications.Clear();
        textBlock.SetText(ReadOnlySpan<char>.Empty);
        Assert.Contains(nameof(MGTextBlock.Text), notifications);
        Assert.Equal("", textBlock.Text);
    }

    [Fact]
    public void ASpanSetter_ReplacesExplicitRuns_LikeTheStringSetter()
    {
        TextBlockFixture fixture = new(TextBlockCorpusId.ShortNumber, false);
        MGTextBlock textBlock = fixture.TextBlock;
        textBlock.SetTextRuns(new MGTextRun[] { new MGTextRunText("explicit", new MGTextRunConfig(false), null, null) });
        Assert.True(textBlock.HasExplicitRuns);

        textBlock.SetText("0.42".AsSpan());

        Assert.False(textBlock.HasExplicitRuns);
        Assert.Equal("0.42", textBlock.Text);
        Assert.Equal("0.42", ((MGTextRunText)Assert.Single(textBlock.Runs)).Text);
    }

    [Fact]
    public void AFormattedText_SetFromASpan_KeepsItsRuns()
    {
        TextBlockFixture fixture = new(TextBlockCorpusId.Formatted, true);
        MGTextBlock textBlock = fixture.TextBlock;

        textBlock.SetText("A [b]bold[/b] word".AsSpan());

        Assert.Equal("A [b]bold[/b] word", textBlock.Text);
        Assert.Equal(3, textBlock.Runs.Count);
        Assert.True(((MGTextRunText)textBlock.Runs[1]).Settings.IsBold);
    }

    [Fact]
    public void ChangingTheFontStyle_RebuildsThePlainRunsWithTheNewSettings()
    {
        TextBlockFixture fixture = new(TextBlockCorpusId.ShortNumber, false);
        MGTextBlock textBlock = fixture.TextBlock;
        textBlock.SetText("0.42".AsSpan());
        Assert.False(((MGTextRunText)textBlock.Runs[0]).Settings.IsBold);

        textBlock.IsBold = true;

        Assert.True(((MGTextRunText)textBlock.Runs[0]).Settings.IsBold);
    }

    private static void AssertRuns(IReadOnlyList<MGTextRun> expected, IReadOnlyList<MGTextRun> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].RunType, actual[i].RunType);
            switch (expected[i])
            {
                case MGTextRunText expectedText:
                    MGTextRunText actualText = Assert.IsType<MGTextRunText>(actual[i]);
                    Assert.Equal(expectedText.Text, actualText.Text);
                    Assert.Equal(expectedText.Settings, actualText.Settings);
                    break;
                case MGTextRunLineBreak expectedBreak:
                    Assert.Equal(expectedBreak.LineBreakCharacterCount, Assert.IsType<MGTextRunLineBreak>(actual[i]).LineBreakCharacterCount);
                    break;
            }
        }
    }

    private static void AssertLines(IReadOnlyList<MGTextLine> expected, IReadOnlyList<MGTextLine> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].LineWidth, actual[i].LineWidth);
            Assert.Equal(expected[i].LineTextHeight, actual[i].LineTextHeight);
            Assert.Equal(expected[i].LineTotalHeight, actual[i].LineTotalHeight);
            Assert.Equal(expected[i].LineNumber, actual[i].LineNumber);
            Assert.Equal(expected[i].EndsInLinebreakCharacter, actual[i].EndsInLinebreakCharacter);
            Assert.Equal(expected[i].OriginalCharacterIndices, actual[i].OriginalCharacterIndices);
            Assert.Equal(expected[i].Runs.OfType<MGTextRunText>().Select(x => x.Text), actual[i].Runs.OfType<MGTextRunText>().Select(x => x.Text));
        }
    }
}
