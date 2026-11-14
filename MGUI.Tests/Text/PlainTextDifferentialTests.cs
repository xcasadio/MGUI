using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Text;

/// <summary>A text engine whose metrics are fractional and not additive (bold is wider, a repeated character is narrower, the line height has decimals),
/// so a difference in the order or the grouping of the float arithmetic of the text pipeline shows up in a measurement. It is exact on strings and spans alike.</summary>
internal sealed class FractionalTestTextEngine : ITextMeasurementEngine
{
    public ResolvedFont ResolveFont(FontSpec spec)
    {
        int size = Math.Max(1, spec.Size);
        FontSpec effectiveSpec = string.IsNullOrWhiteSpace(spec.Family) ? FontSpec.Normal("TestSans", size) : spec;
        return new ResolvedFont(effectiveSpec, size, 1.0f, 1.0f, size * 1.2347f, size * 0.4113f, new Vector2(0.37f, 0.21f), false, new object());
    }

    public Vector2 MeasureText(ResolvedFont font, string text) => MeasureText(font, text.AsSpan());

    public Vector2 MeasureText(ResolvedFont font, ReadOnlySpan<char> text)
    {
        float size = font.ActualSize;
        bool bold = (font.Spec.Style & CustomFontStyles.Bold) != 0;
        float width = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            float characterWidth = c == ' ' ? font.SpaceWidth : size * (0.29f + (c % 11) * 0.0373f);
            if (bold)
            {
                characterWidth *= 1.07f;
            }

            if (i > 0 && text[i] == text[i - 1])
            {
                characterWidth -= 0.13f;
            }

            width += characterWidth;
        }

        return new Vector2(width, font.LineHeight);
    }

    public GlyphMetrics MeasureGlyph(ResolvedFont font, char character) => new(0.0f, font.SpaceWidth, 0.0f, font.LineHeight);

    public float GetLineHeight(ResolvedFont font) => font.LineHeight;

    public float GetSpaceWidth(ResolvedFont font) => font.SpaceWidth;

    public void InvalidateCache() { }
}

/// <summary>
/// The plain text layout (<see cref="PlainTextLayout"/>, behind <see cref="MGTextBlock"/> when <see cref="MGTextBlock.AllowsInlineFormatting"/> is false) against the code it replaces:
/// the runs of the tokenizer and the parser, laid out by <see cref="MGTextLine.ParseLines(ITextMeasurer, double, bool, IEnumerable{MGTextRun}, bool)"/>. For a varied set of plain
/// texts and widths, with and without wrapping, the same lines come out (the same pieces of text, the same widths and heights), and a text block measures the same size.
/// </summary>
public class PlainTextDifferentialTests
{
    private static readonly double[] LayoutWidths = { 0, 0.5, 1, 5, 17, 33.5, 60, 100, 250, 1000, 100000, double.MaxValue };

    private static readonly string[] FixedTexts =
    {
        "", "word", "a", "  ", " ", "   spaces only   ", " leading", "trailing ", "multiple   spaces   between   words", "two words",
        "a-b-c-d", "hyphen-ated words-and- more -", "----", "-", "a - b",
        "supercalifragilisticexpialidocious_overlong_word_that_is_wider_than_any_reasonable_line", "x supercalifragilisticexpialidocious y",
        "\n", "\r\n", "\r", "a\nb", "a\r\nb", "a\rb", "a\n\nb", "a\n\n\nb", "a\n", "\na", "a\r\n", "\r\na", "a\r\n\r\nb", "a \n b", " \n ", "  \n  \n  ",
        "line one\nline two is a little longer\nthree", "tab\there\tand\tthere", "\t", "[b]literal[/b] tags and [color=Red]colors[/color]", "back\\slash and [ bracket",
        "The quick brown fox jumps over the lazy dog while the sleepy cat watches the whole thing happen.",
        "aaaa bbbb cccc dddd eeee ffff gggg hhhh iiii jjjj kkkk llll mmmm nnnn",
        "word " + new string('w', 40) + " word " + new string('v', 30) + " tail",
    };

    private static List<string> BuildCorpus()
    {
        List<string> texts = new(FixedTexts);
        Random random = new(20261114);
        string[] pieces = { " ", " ", "  ", "-", "\n", "\r\n", "\r", "\t" };
        for (int i = 0; i < 260; i++)
        {
            System.Text.StringBuilder builder = new();
            int tokens = random.Next(1, 24);
            for (int j = 0; j < tokens; j++)
            {
                if (random.Next(4) == 0)
                {
                    builder.Append(pieces[random.Next(pieces.Length)]);
                }
                else
                {
                    int length = random.Next(5) == 0 ? random.Next(15, 45) : random.Next(1, 9);
                    for (int k = 0; k < length; k++)
                    {
                        builder.Append((char)('a' + random.Next(26)));
                    }
                }
            }

            texts.Add(builder.ToString());
        }

        return texts;
    }

    private static MGTextRunConfig Settings(bool bold, bool italic)
        => new(bold, italic, 1, null, new MGTextRunUnderlineConfig(false), default, default);

    private static List<MGTextRun> LegacyRuns(string text, MGTextRunConfig settings)
        => MGTextRun.ParseRuns(FTTokenizer.TokenizeLineBreaks(text, true).ToList(), settings).ToList();

    private sealed class Fixture
    {
        public GraphTestRuntime Runtime { get; }
        public MGDesktop Desktop { get; }
        public MGWindow Window { get; }
        public MGTextBlock TextBlock { get; }
        public int Width { get; }

        public Fixture(int width, bool wrap, int? maxLines, int minLines, bool bold, bool legacy, bool stableFootprint = false, Action<MGTextBlock> configure = null)
        {
            Width = width;
            Runtime = new GraphTestRuntime(new Rectangle(0, 0, 1200, 600)) { TextEngine = new FractionalTestTextEngine() };
            Desktop = new MGDesktop(Runtime);
            Window = new MGWindow(Desktop, 0, 0, width, 300) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
            TextBlock = new MGTextBlock(Window, "", Color.White, 12, false)
            {
                WrapText = wrap,
                MaxLines = maxLines,
                MinLines = minLines,
                IsBold = bold,
                HasStableTextFootprint = stableFootprint,
                ForceLegacyTextPath = legacy
            };
            configure?.Invoke(TextBlock);
            Window.SetContent(TextBlock);
            Desktop.Windows.Add(Window);
            Frame();
            Frame();
        }

        private int Elapsed;

        public void Frame()
        {
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(Elapsed), TimeSpan.FromMilliseconds(16), default, default));
            Desktop.Update();
            Elapsed += 16;
        }
    }

    [Fact]
    public void TheCorpus_HasWhatTheCoverageClaims()
    {
        List<string> corpus = BuildCorpus();
        Assert.True(corpus.Count >= 200 + FixedTexts.Length);
        Assert.Contains("", corpus);
        Assert.Contains(corpus, x => x.Contains('\t'));
        Assert.Contains(corpus, x => x.Contains("\r\n"));
        Assert.Contains(corpus, x => x.EndsWith('\n'));
    }

    [Fact]
    public void TheLayout_GivesTheLinesOfParseLines()
    {
        Fixture fixture = new(400, true, null, 0, false, false);
        PlainTextLayout layout = new();
        int compared = 0;

        foreach (string text in BuildCorpus())
        {
            foreach (bool bold in new[] { false, true })
            {
                MGTextRunConfig settings = Settings(bold, false);
                List<MGTextRun> runs = LegacyRuns(text, settings);
                foreach (double width in LayoutWidths)
                {
                    foreach (bool wrap in new[] { true, false })
                    {
                        foreach (bool ignoreEmptySpaceLines in new[] { true, false })
                        {
                            List<MGTextLine> expected = MGTextLine.ParseLines(fixture.TextBlock, width, wrap, runs, ignoreEmptySpaceLines).ToList();
                            layout.Build(fixture.TextBlock, text.AsSpan(), width, wrap, ignoreEmptySpaceLines, settings, true);
                            AssertSameLines(expected, layout, $"text=\"{Escape(text)}\" bold={bold} width={width} wrap={wrap} ignoreEmptySpaceLines={ignoreEmptySpaceLines}");
                            Assert.Equal(text, new string(layout.Source));
                            compared++;
                        }
                    }
                }
            }
        }

        Assert.True(compared > 20000, $"compared {compared} layouts");
    }

    [Fact]
    public void TheLayout_IsRebuiltInTheSameBuffers_WithoutLeftovers()
    {
        //  One layout object is reused for every text of the corpus, in this order: a build must not depend on what was built before.
        Fixture fixture = new(400, true, null, 0, false, false);
        PlainTextLayout reused = new();
        PlainTextLayout fresh = new();
        MGTextRunConfig settings = Settings(false, false);

        foreach (string text in BuildCorpus())
        {
            reused.Build(fixture.TextBlock, text.AsSpan(), 60, true, true, settings, true);
            fresh.Build(fixture.TextBlock, text.AsSpan(), 60, true, true, settings, true);
            Assert.Equal(fresh.LineCount, reused.LineCount);
            for (int i = 0; i < fresh.LineCount; i++)
            {
                Assert.Equal(PiecesOf(fresh, fresh.GetLine(i)), PiecesOf(reused, reused.GetLine(i)));
            }
        }
    }

    [Theory]
    [InlineData(false, 0, null)]
    [InlineData(true, 0, null)]
    [InlineData(true, 0, 1)]
    [InlineData(true, 0, 2)]
    [InlineData(true, 3, null)]
    [InlineData(true, 4, 2)]
    [InlineData(false, 3, null)]
    [InlineData(false, 0, 1)]
    public void ATextBlock_MeasuresTheSameSize_AsTheLegacyPipeline(bool wrap, int minLines, int? maxLines)
    {
        List<string> corpus = BuildCorpus();
        foreach (bool bold in new[] { false, true })
        {
            foreach (int width in new[] { 1, 23, 61, 140, 400 })
            {
                Fixture plain = new(width, wrap, maxLines, minLines, bold, false);
                Fixture legacy = new(width, wrap, maxLines, minLines, bold, true);
                foreach (string text in corpus)
                {
                    plain.TextBlock.Text = text;
                    legacy.TextBlock.Text = text;
                    foreach (Size available in new[] { new Size(width, 300), new Size(width + 40, 300), new Size(7, 300) })
                    {
                        Thickness expected = legacy.TextBlock.MeasureSelfOverride(available, out _);
                        Thickness actual = plain.TextBlock.MeasureSelfOverride(available, out _);
                        Assert.True(expected.Left == actual.Left && expected.Top == actual.Top,
                            $"text=\"{Escape(text)}\" bold={bold} width={width} available={available} wrap={wrap} minLines={minLines} maxLines={maxLines}: expected {expected.Left}x{expected.Top}, got {actual.Left}x{actual.Top}");
                    }
                }
            }
        }
    }

    private sealed class CountingTextBlock : MGTextBlock
    {
        public int LayoutChangedCallCount { get; private set; }

        public CountingTextBlock(MGWindow window) : base(window, "", Color.White, 12, false) { }

        public void ResetCount() => LayoutChangedCallCount = 0;

        protected override void LayoutChanged(MGElement Source, bool NotifyParent)
        {
            LayoutChangedCallCount++;
            base.LayoutChanged(Source, NotifyParent);
        }
    }

    /// <summary>A text block with a stable footprint in a desktop, changed through <c>SetText</c> in a given mode, then laid out again when the change asked for it.</summary>
    private sealed class LocalChangeHarness
    {
        private readonly GraphTestRuntime Runtime;
        private readonly MGDesktop Desktop;
        private readonly bool Legacy;
        private readonly MGTextInvalidationMode Mode;
        private int Elapsed;

        public CountingTextBlock Block { get; }

        public LocalChangeHarness(bool legacy, bool wrap, MGTextInvalidationMode mode)
        {
            Legacy = legacy;
            Mode = mode;
            Runtime = new GraphTestRuntime(new Rectangle(0, 0, 1200, 600)) { TextEngine = new FractionalTestTextEngine() };
            Desktop = new MGDesktop(Runtime);
            MGWindow window = new(Desktop, 0, 0, 220, 300) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
            Block = new CountingTextBlock(window) { WrapText = wrap, MinLines = 2, HasStableTextFootprint = true, ForceLegacyTextPath = legacy };
            window.SetContent(Block);
            Desktop.Windows.Add(window);
            Frame();
            Frame();
        }

        private void Frame()
        {
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(Elapsed), TimeSpan.FromMilliseconds(16), default, default));
            Desktop.Update();
            Elapsed += 16;
        }

        public void Change(string text)
        {
            Block.ResetCount();
            if (Legacy)
            {
                Block.SetText(text, Mode);
            }
            else
            {
                Block.SetText(text.AsSpan(), Mode);
            }

            //  The change asked for a layout: a desktop frame runs it.
            if (Block.LayoutChangedCallCount > 0)
            {
                Frame();
                Frame();
            }
        }
    }

    [Theory]
    [InlineData(MGTextInvalidationMode.ContentOnly)]
    [InlineData(MGTextInvalidationMode.ReflowLocal)]
    [InlineData(MGTextInvalidationMode.RelayoutParent)]
    public void ALocalTextChange_KeepsOrEscalatesTheLayout_LikeTheLegacyPipeline(MGTextInvalidationMode mode)
    {
        List<string> corpus = BuildCorpus();
        foreach (bool wrap in new[] { false, true })
        {
            LocalChangeHarness legacy = new(true, wrap, mode);
            LocalChangeHarness plain = new(false, wrap, mode);
            foreach (string text in corpus)
            {
                legacy.Change(text);
                plain.Change(text);

                Assert.True(legacy.Block.LayoutChangedCallCount == plain.Block.LayoutChangedCallCount,
                    $"text=\"{Escape(text)}\" wrap={wrap} mode={mode}: legacy {legacy.Block.LayoutChangedCallCount} LayoutChanged calls, plain {plain.Block.LayoutChangedCallCount}");
                AssertSamePublicLines(legacy.Block, plain.Block, $"text=\"{Escape(text)}\" wrap={wrap} mode={mode}");
            }
        }
    }

    /// <summary>What <see cref="MGTextBlock.DrawSelf"/> asks the transaction to draw, with the fonts compared by what they are (each text block resolves its own).</summary>
    private static List<(string Text, Vector2 Position, Vector2 Origin, float Scale, Color Color, int Size, CustomFontStyles Style)> Draw(Fixture fixture, float opacity)
    {
        GraphNoOpDrawTransaction transaction = new(fixture.Runtime, DrawSettings.Default);
        ElementDrawArgs drawArgs = new(new DrawBaseArgs(TimeSpan.Zero, transaction, opacity), new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);
        fixture.TextBlock.DrawSelf(drawArgs, fixture.TextBlock.LayoutBounds);
        return transaction.DrawTextCalls.Select(x => (x.Text, x.Position, x.Origin, x.Scale, x.Color, x.Font.ActualSize, x.Font.Spec.Style)).ToList();
    }

    [Fact]
    public void ADrawnPlainText_MakesTheDrawCallsOfTheLegacyPipeline()
    {
        List<string> corpus = BuildCorpus();
        int configurations = 0;
        int draws = 0;
        int nonEmptyDraws = 0;
        double?[] progresses = { null, 0.0, 0.3, 0.77, 1.0 };
        HorizontalAlignment[] horizontals = { HorizontalAlignment.Left, HorizontalAlignment.Center, HorizontalAlignment.Right };
        VerticalAlignment[] verticals = { VerticalAlignment.Top, VerticalAlignment.Center, VerticalAlignment.Bottom };

        foreach (bool wrap in new[] { false, true })
        foreach (bool bold in new[] { false, true })
        foreach (bool shadow in new[] { false, true })
        foreach (bool underline in new[] { false, true })
        foreach (int width in new[] { 40, 400 })
        foreach (int alignment in new[] { 0, 1, 2 })
        {
            int configuration = configurations++;
            HorizontalAlignment horizontal = horizontals[alignment];
            VerticalAlignment vertical = verticals[(alignment + configuration) % verticals.Length];

            void Configure(MGTextBlock block)
            {
                block.IsShadowed = shadow;
                block.ShadowOffset = new Point(2, 1);
                block.IsUnderlined = underline;
                block.TextAlignment = horizontal;
                block.VerticalContentAlignment = vertical;
                block.LinePadding = 3.5f;
            }

            Fixture plain = new(width, wrap, null, 0, bold, false, configure: Configure);
            Fixture legacy = new(width, wrap, null, 0, bold, true, configure: Configure);

            //  A few texts of the corpus per configuration and per progress, a different few each time, so the whole corpus is drawn over the configurations.
            foreach (double? progress in progresses)
            {
                plain.TextBlock.TextProgress = progress;
                legacy.TextBlock.TextProgress = progress;
                for (int t = 0; t < 5; t++)
                {
                    string text = corpus[(configuration * 11 + t * 53 + (progress.HasValue ? (int)(progress.Value * 10) * 17 : 0)) % corpus.Count];
                    plain.TextBlock.Text = text;
                    legacy.TextBlock.Text = text;
                    plain.Frame();
                    plain.Frame();
                    legacy.Frame();
                    legacy.Frame();

                    var expected = Draw(legacy, 0.8f);
                    var actual = Draw(plain, 0.8f);
                    string context = $"text=\"{Escape(text)}\" wrap={wrap} bold={bold} shadow={shadow} underline={underline} width={width} h={horizontal} v={vertical} progress={progress}";
                    Assert.True(expected.Count == actual.Count, $"{context}: {expected.Count} draw calls, got {actual.Count}");
                    for (int i = 0; i < expected.Count; i++)
                    {
                        Assert.True(expected[i].Equals(actual[i]), $"{context}: draw call {i} is {actual[i]}, expected {expected[i]}");
                    }

                    draws += expected.Count;
                    nonEmptyDraws += expected.Count(x => x.Text.Length > 0);
                }
            }
        }

        Assert.True(configurations >= 96 && draws > 2500 && nonEmptyDraws > 1500, $"{configurations} configurations, {draws} draw calls, {nonEmptyDraws} non empty");
    }

    private static void AssertSamePublicLines(MGTextBlock expectedBlock, MGTextBlock actualBlock, string context)
    {
        Assert.True((expectedBlock.Lines == null) == (actualBlock.Lines == null), $"{context}: Lines null state differs");
        if (expectedBlock.Lines == null)
        {
            return;
        }

        Assert.True(expectedBlock.Lines.Count == actualBlock.Lines.Count, $"{context}: {expectedBlock.Lines.Count} lines, got {actualBlock.Lines.Count}");
        for (int i = 0; i < expectedBlock.Lines.Count; i++)
        {
            MGTextLine expected = expectedBlock.Lines[i];
            MGTextLine actual = actualBlock.Lines[i];
            Assert.True(expected.LineWidth == actual.LineWidth && expected.LineTextHeight == actual.LineTextHeight && expected.LineTotalHeight == actual.LineTotalHeight &&
                        expected.LineNumber == actual.LineNumber && expected.EndsInLinebreakCharacter == actual.EndsInLinebreakCharacter &&
                        expected.OriginalCharacterIndices.SequenceEqual(actual.OriginalCharacterIndices) &&
                        expected.Runs.OfType<MGTextRunText>().Select(x => x.Text).SequenceEqual(actual.Runs.OfType<MGTextRunText>().Select(x => x.Text)),
                $"{context}: line {i} differs");
        }
    }

    private static List<string> PiecesOf(PlainTextLayout layout, PlainTextLine line)
    {
        List<string> pieces = new();
        for (int i = 0; i < line.PieceCount; i++)
        {
            pieces.Add(new string(layout.GetPieceText(layout.GetPiece(line.FirstPiece + i))));
        }

        return pieces;
    }

    private static void AssertSameLines(List<MGTextLine> expected, PlainTextLayout actual, string context)
    {
        Assert.True(expected.Count == actual.LineCount, $"{context}: expected {expected.Count} lines, got {actual.LineCount}");
        for (int i = 0; i < expected.Count; i++)
        {
            PlainTextLine line = actual.GetLine(i);
            List<string> expectedPieces = expected[i].Runs.OfType<MGTextRunText>().Select(x => x.Text).ToList();
            List<string> actualPieces = PiecesOf(actual, line);
            Assert.True(expectedPieces.SequenceEqual(actualPieces), $"{context}: line {i} pieces [{string.Join("|", expectedPieces.Select(Escape))}] vs [{string.Join("|", actualPieces.Select(Escape))}]");
            Assert.True(expected[i].LineWidth == line.Width, $"{context}: line {i} width {expected[i].LineWidth} vs {line.Width}");
            Assert.True(expected[i].LineTextHeight == line.TextHeight, $"{context}: line {i} text height {expected[i].LineTextHeight} vs {line.TextHeight}");
            Assert.True(expected[i].LineTotalHeight == line.TotalHeight, $"{context}: line {i} total height {expected[i].LineTotalHeight} vs {line.TotalHeight}");
            Assert.True(expected[i].EndsInLinebreakCharacter == line.EndsInLinebreak, $"{context}: line {i} ends in line break {expected[i].EndsInLinebreakCharacter} vs {line.EndsInLinebreak}");
        }
    }

    private static string Escape(string text) => text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
}
