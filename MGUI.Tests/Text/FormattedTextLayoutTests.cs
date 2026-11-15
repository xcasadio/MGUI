using System.Globalization;
using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit.Abstractions;

namespace MGUI.Tests.Text;

/// <summary>
/// The lines, the measure, the invalidation and the drawing of a formatted text (<see cref="FormattedTextLayout"/> and <c>DrawFormattedLines</c> behind <see cref="MGTextBlock"/>)
/// against the legacy pipeline they replace (<see cref="MGTextLine.ParseLines(ITextMeasurer, double, bool, IEnumerable{MGTextRun}, bool)"/> over the public runs, and the loop of
/// <see cref="MGTextBlock.DrawSelf"/> over <see cref="MGTextBlock.Lines"/>), reached through <see cref="MGTextBlock.ForceLegacyTextPath"/>. The text engine used is fractional and not
/// additive, so a difference in the order or the grouping of the float arithmetic shows up in a measurement.
/// </summary>
public class FormattedTextLayoutTests
{
    private static readonly double[] LayoutWidths = { 0, 0.5, 1, 5, 17, 33.5, 60, 100, 250, 1000, 100000, double.MaxValue };

    private readonly ITestOutputHelper Output;

    public FormattedTextLayoutTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private static FormattedTextHarness CreateHarness(int width, bool legacy, bool wrap = true, Action<MGTextBlock> configure = null, bool stableFootprint = false, bool fractional = true)
        => new(width, legacy, wrap, configure, stableFootprint, fractional ? new FractionalTestTextEngine() : null);

    private static MGTextRunConfig Settings(bool bold, bool italic)
        => new(bold, italic, 1, null, new MGTextRunUnderlineConfig(false), default, default);

    /// <summary>The texts of the corpus that the legacy code reads without throwing.</summary>
    private static readonly Lazy<List<string>> ReadableCorpus = new(() => FormattedTextCorpus.Build().Where(IsReadableByTheLegacyCode).ToList());

    private static bool IsReadableByTheLegacyCode(string text)
    {
        try
        {
            _ = MGTextRun.ParseRuns(text, new MGTextRunConfig(false)).ToList();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string DescribePiece(FormattedTextLayout layout, FormattedTextPiece piece)
    {
        if (piece.RunIndex < 0)
        {
            return $"Text[|{TextRunDescriber.Describe(new MGTextRunConfig(false))}|tt=null|action=null]";
        }

        ref var run = ref layout.Source[piece.RunIndex];
        return run.Kind == ScannedRunKind.Image
            ? $"Image[{run.ImageSource}|{run.ImageWidth}x{run.ImageHeight}|tt={run.ToolTipId ?? "null"}|action={run.ActionId ?? "null"}]"
            : $"Text[{TextRunDescriber.Escape(new string(layout.GetPieceText(piece)))}|{TextRunDescriber.Describe(run.Settings)}|tt={run.ToolTipId ?? "null"}|action={run.ActionId ?? "null"}]";
    }

    private static List<string> DescribePieces(FormattedTextLayout layout, FormattedTextLine line)
    {
        List<string> pieces = new();
        for (int i = 0; i < line.PieceCount; i++)
        {
            pieces.Add(DescribePiece(layout, layout.GetPiece(line.FirstPiece + i)));
        }

        return pieces;
    }

    private static void AssertSameLines(List<MGTextLine> expected, FormattedTextLayout actual, string context)
    {
        Assert.True(expected.Count == actual.LineCount, $"{context}: expected {expected.Count} lines, got {actual.LineCount}");
        for (int i = 0; i < expected.Count; i++)
        {
            FormattedTextLine line = actual.GetLine(i);
            List<string> expectedPieces = expected[i].Runs.Select(TextRunDescriber.Describe).ToList();
            List<string> actualPieces = DescribePieces(actual, line);
            Assert.True(expectedPieces.SequenceEqual(actualPieces), $"{context}: line {i} pieces\n  [{string.Join("\n   ", expectedPieces)}]\nvs\n  [{string.Join("\n   ", actualPieces)}]");
            Assert.True(expected[i].LineWidth == line.Width, $"{context}: line {i} width {expected[i].LineWidth} vs {line.Width}");
            Assert.True(expected[i].LineTextHeight == line.TextHeight, $"{context}: line {i} text height {expected[i].LineTextHeight} vs {line.TextHeight}");
            Assert.True(expected[i].LineImageHeight == line.ImageHeight, $"{context}: line {i} image height {expected[i].LineImageHeight} vs {line.ImageHeight}");
            Assert.True(expected[i].LineTotalHeight == line.TotalHeight, $"{context}: line {i} total height {expected[i].LineTotalHeight} vs {line.TotalHeight}");
            Assert.True(expected[i].EndsInLinebreakCharacter == line.EndsInLinebreak, $"{context}: line {i} ends in line break {expected[i].EndsInLinebreakCharacter} vs {line.EndsInLinebreak}");
        }
    }

    [Fact]
    public void TheLayout_GivesTheLinesOfParseLines()
    {
        //  The measurer is a text block with the fractional engine; the legacy runs are the public runs of a legacy text block, the scanned runs are what the scanner reads.
        FormattedTextHarness plainFont = CreateHarness(400, true);
        FormattedTextHarness boldFont = CreateHarness(400, true, configure: block => block.IsBold = true);
        FormattedTextScanner scanner = new();
        FormattedTextRuns scanned = new();
        FormattedTextLayout layout = new();
        int compared = 0;
        int texts = 0;
        int withImages = 0;
        int withSeveralRuns = 0;
        int wrappedLines = 0;

        foreach (string text in ReadableCorpus.Value)
        {
            texts++;
            foreach (bool bold in new[] { false, true })
            {
                FormattedTextHarness harness = bold ? boldFont : plainFont;
                harness.TextBlock.Text = text;
                List<MGTextRun> legacyRuns = harness.TextBlock.Runs.ToList();
                Assert.True(scanner.TryScan(text.AsSpan(), Settings(bold, false), harness.Desktop, scanned) || text.Any(x => x > 127), $"scan of \"{TextRunDescriber.Escape(text)}\"");
                if (!scanner.TryScan(text.AsSpan(), Settings(bold, false), harness.Desktop, scanned))
                {
                    continue;
                }

                if (legacyRuns.Any(x => x is MGTextRunImage))
                {
                    withImages++;
                }

                if (legacyRuns.Count(x => x is MGTextRunText) > 1)
                {
                    withSeveralRuns++;
                }

                foreach (double width in LayoutWidths)
                {
                    foreach (bool wrap in new[] { true, false })
                    {
                        foreach (bool ignoreEmptySpaceLines in new[] { true, false })
                        {
                            List<MGTextLine> expected = MGTextLine.ParseLines(harness.TextBlock, width, wrap, legacyRuns, ignoreEmptySpaceLines).ToList();
                            layout.Build(harness.TextBlock, scanned, width, wrap, ignoreEmptySpaceLines, true);
                            AssertSameLines(expected, layout, $"text=\"{TextRunDescriber.Escape(text)}\" bold={bold} width={width} wrap={wrap} ignoreEmptySpaceLines={ignoreEmptySpaceLines}");
                            compared++;
                            if (wrap && width == 60)
                            {
                                wrappedLines += expected.Count;
                            }
                        }
                    }
                }
            }
        }

        Output.WriteLine($"{texts} texts, {compared} layouts compared; {withImages} texts with images, {withSeveralRuns} with several text runs, {wrappedLines} lines at width 60");
        Assert.True(compared > 20000, $"compared {compared} layouts");
        Assert.True(withImages >= 20 && withSeveralRuns >= 200 && wrappedLines >= 1000);
    }

    [Fact]
    public void TheLayout_IsRebuiltInTheSameBuffers_WithoutLeftovers()
    {
        //  One layout object and one runs object are reused for every text of the corpus, in this order: a build must not depend on what was built before.
        FormattedTextHarness harness = CreateHarness(400, false);
        FormattedTextScanner scanner = new();
        FormattedTextRuns reusedRuns = new();
        FormattedTextLayout reused = new();

        foreach (string text in ReadableCorpus.Value)
        {
            FormattedTextRuns freshRuns = new();
            FormattedTextLayout fresh = new();
            bool readReused = scanner.TryScan(text.AsSpan(), Settings(false, false), harness.Desktop, reusedRuns);
            bool readFresh = new FormattedTextScanner().TryScan(text.AsSpan(), Settings(false, false), harness.Desktop, freshRuns);
            Assert.Equal(readFresh, readReused);
            if (!readReused)
            {
                continue;
            }

            reused.Build(harness.TextBlock, reusedRuns, 60, true, true, true);
            fresh.Build(harness.TextBlock, freshRuns, 60, true, true, true);
            Assert.Equal(fresh.LineCount, reused.LineCount);
            for (int i = 0; i < fresh.LineCount; i++)
            {
                Assert.Equal(DescribePieces(fresh, fresh.GetLine(i)), DescribePieces(reused, reused.GetLine(i)));
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
        List<string> corpus = ReadableCorpus.Value;
        foreach (bool fractional in new[] { true, false })
        {
            foreach (bool bold in new[] { false, true })
            {
                foreach (int width in new[] { 23, 140 })
                {
                    void Configure(MGTextBlock block)
                    {
                        block.IsBold = bold;
                        block.MinLines = minLines;
                        block.MaxLines = maxLines;
                    }

                    FormattedTextHarness scanned = CreateHarness(width, false, wrap, Configure, fractional: fractional);
                    FormattedTextHarness legacy = CreateHarness(width, true, wrap, Configure, fractional: fractional);
                    foreach (string text in corpus)
                    {
                        scanned.TextBlock.Text = text;
                        legacy.TextBlock.Text = text;
                        foreach (Size available in new[] { new Size(width, 300), new Size(width + 40, 300), new Size(7, 300) })
                        {
                            Thickness expected = legacy.TextBlock.MeasureSelfOverride(available, out _);
                            Thickness actual = scanned.TextBlock.MeasureSelfOverride(available, out _);
                            Assert.True(expected.Left == actual.Left && expected.Top == actual.Top,
                                $"text=\"{TextRunDescriber.Escape(text)}\" bold={bold} width={width} available={available} wrap={wrap} minLines={minLines} maxLines={maxLines} fractional={fractional}: expected {expected.Left}x{expected.Top}, got {actual.Left}x{actual.Top}");
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void ThePublicLines_AfterALayout_AreTheLinesOfTheLegacyPipeline()
    {
        foreach (bool wrap in new[] { false, true })
        {
            FormattedTextHarness scanned = CreateHarness(120, false, wrap);
            FormattedTextHarness legacy = CreateHarness(120, true, wrap);
            int compared = 0;
            foreach (string text in ReadableCorpus.Value)
            {
                scanned.TextBlock.Text = text;
                legacy.TextBlock.Text = text;
                scanned.Frame();
                scanned.Frame();
                legacy.Frame();
                legacy.Frame();

                List<string> expected = TextRunDescriber.Describe(legacy.TextBlock.Lines);
                List<string> actual = TextRunDescriber.Describe(scanned.TextBlock.Lines);
                Assert.True(expected.SequenceEqual(actual), $"text=\"{TextRunDescriber.Escape(text)}\" wrap={wrap}:\nexpected {string.Join("\n         ", expected)}\nactual   {string.Join("\n         ", actual)}");
                //  The same instances until the next layout
                Assert.Same(scanned.TextBlock.Lines, scanned.TextBlock.Lines);
                compared++;
            }

            Assert.True(compared > 400);
        }
    }

    /// <summary>A text block with a stable footprint in a desktop, changed through <c>SetText</c> in a given mode, then laid out again when the change asked for it.</summary>
    private sealed class LocalChangeHarness
    {
        private readonly bool Legacy;
        private readonly MGTextInvalidationMode Mode;
        public FormattedTextHarness Harness { get; }
        public CountingFormattedTextBlock Block => Harness.TextBlock;

        public LocalChangeHarness(bool legacy, bool wrap, MGTextInvalidationMode mode)
        {
            Legacy = legacy;
            Mode = mode;
            Harness = CreateHarness(220, legacy, wrap, block => block.MinLines = 2, stableFootprint: true);
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
                Harness.Frame();
                Harness.Frame();
            }
        }
    }

    [Theory]
    [InlineData(MGTextInvalidationMode.ContentOnly)]
    [InlineData(MGTextInvalidationMode.ReflowLocal)]
    [InlineData(MGTextInvalidationMode.RelayoutParent)]
    public void ALocalTextChange_KeepsOrEscalatesTheLayout_LikeTheLegacyPipeline(MGTextInvalidationMode mode)
    {
        int kept = 0;
        int escalated = 0;
        foreach (bool wrap in new[] { false, true })
        {
            LocalChangeHarness legacy = new(true, wrap, mode);
            LocalChangeHarness scanned = new(false, wrap, mode);
            foreach (string text in ReadableCorpus.Value)
            {
                legacy.Change(text);
                scanned.Change(text);

                string context = $"text=\"{TextRunDescriber.Escape(text)}\" wrap={wrap} mode={mode}";
                Assert.True(legacy.Block.LayoutChangedCallCount == scanned.Block.LayoutChangedCallCount,
                    $"{context}: legacy {legacy.Block.LayoutChangedCallCount} LayoutChanged calls, scanned {scanned.Block.LayoutChangedCallCount}");
                Assert.Equal(TextRunDescriber.Describe(legacy.Block.Lines), TextRunDescriber.Describe(scanned.Block.Lines));
                if (legacy.Block.LayoutChangedCallCount == 0)
                {
                    kept++;
                }
                else
                {
                    escalated++;
                }
            }
        }

        Output.WriteLine($"mode {mode}: {kept} changes kept local, {escalated} asked for the parent layout");
        if (mode != MGTextInvalidationMode.RelayoutParent)
        {
            //  Both outcomes happen, so the comparison of the counts above is not vacuous
            Assert.True(kept > 20 && escalated > 20, $"{kept} kept, {escalated} escalated");
        }
    }

    /// <summary>What <see cref="MGTextBlock.DrawSelf"/> asks the transaction to draw: texts (the fonts compared by what they are), rectangles, textures and gradients.</summary>
    private static List<string> Draw(FormattedTextHarness harness, float opacity)
    {
        GraphNoOpDrawTransaction transaction = new(harness.Runtime, DrawSettings.Default);
        harness.TextBlock.DrawSelf(harness.CreateDrawArgs(transaction, opacity), harness.TextBlock.LayoutBounds);
        List<string> calls = new();
        calls.AddRange(transaction.DrawTextCalls.Select(x => $"text[{TextRunDescriber.Escape(x.Text)}|{x.Position}|{x.Origin}|{x.Scale.ToString("R", CultureInfo.InvariantCulture)}|{x.Color}|{x.Font.ActualSize}|{x.Font.Spec.Style}]"));
        calls.AddRange(transaction.FillRectangleCalls.Select(x => $"rect[{x.Origin}|{x.Destination}|{x.Color}]"));
        calls.AddRange(transaction.DrawTextureToCalls.Select(x => $"texture[{(x.Texture as GraphTestImageResource)?.Id}|{x.Source}|{x.Destination}|{x.ColorMask}]"));
        calls.AddRange(transaction.FillQuadrilateralCalls.Select(x => $"quad[{x.Origin}|{x.TopLeft}{x.TopLeftColor}|{x.TopRight}{x.TopRightColor}|{x.BottomRight}{x.BottomRightColor}|{x.BottomLeft}{x.BottomLeftColor}]"));
        return calls;
    }

    [Fact]
    public void ADrawnFormattedText_MakesTheDrawCallsOfTheLegacyPipeline()
    {
        List<string> corpus = ReadableCorpus.Value;
        int configurations = 0;
        int draws = 0;
        int textDraws = 0;
        int rectangleDraws = 0;
        int textureDraws = 0;
        int quadDraws = 0;
        double?[] progresses = { null, 0.0, 0.3, 0.77, 1.0 };
        HorizontalAlignment[] horizontals = { HorizontalAlignment.Left, HorizontalAlignment.Center, HorizontalAlignment.Right };
        VerticalAlignment[] verticals = { VerticalAlignment.Top, VerticalAlignment.Center, VerticalAlignment.Bottom };

        foreach (bool wrap in new[] { false, true })
        foreach (bool bold in new[] { false, true })
        foreach (bool shadow in new[] { false, true })
        foreach (bool underline in new[] { false, true })
        foreach (int width in new[] { 60, 300 })
        foreach (int alignment in new[] { 0, 1, 2 })
        {
            int configuration = configurations++;
            HorizontalAlignment horizontal = horizontals[alignment];
            VerticalAlignment vertical = verticals[(alignment + configuration) % verticals.Length];

            void Configure(MGTextBlock block)
            {
                block.IsBold = bold;
                block.IsShadowed = shadow;
                block.ShadowOffset = new Point(2, 1);
                block.IsUnderlined = underline;
                block.TextAlignment = horizontal;
                block.VerticalContentAlignment = vertical;
                block.LinePadding = 3.5f;
            }

            FormattedTextHarness scanned = CreateHarness(width, false, wrap, Configure);
            FormattedTextHarness legacy = CreateHarness(width, true, wrap, Configure);

            //  A few texts of the corpus per configuration and per progress, a different few each time, so the whole corpus is drawn over the configurations.
            foreach (double? progress in progresses)
            {
                scanned.TextBlock.TextProgress = progress;
                legacy.TextBlock.TextProgress = progress;
                for (int t = 0; t < 12; t++)
                {
                    string text = corpus[(configuration * 37 + t * 53 + (progress.HasValue ? (int)(progress.Value * 10) * 17 : 0)) % corpus.Count];
                    scanned.TextBlock.Text = text;
                    legacy.TextBlock.Text = text;
                    scanned.TextBlock.UpdateLines();
                    legacy.TextBlock.UpdateLines();

                    float opacity = 0.8f;
                    List<string> expected = Draw(legacy, opacity);
                    List<string> actual = Draw(scanned, opacity);
                    string context = $"text=\"{TextRunDescriber.Escape(text)}\" wrap={wrap} bold={bold} shadow={shadow} underline={underline} width={width} h={horizontal} v={vertical} progress={progress}";
                    Assert.True(expected.SequenceEqual(actual), $"{context}:\nexpected {string.Join("\n         ", expected)}\nactual   {string.Join("\n         ", actual)}");

                    draws += expected.Count;
                    textDraws += expected.Count(x => x.StartsWith("text[") && !x.StartsWith("text[|"));
                    rectangleDraws += expected.Count(x => x.StartsWith("rect["));
                    textureDraws += expected.Count(x => x.StartsWith("texture["));
                    quadDraws += expected.Count(x => x.StartsWith("quad["));
                }
            }
        }

        Output.WriteLine($"{configurations} configurations, {draws} draw calls: {textDraws} texts, {rectangleDraws} rectangles, {textureDraws} textures, {quadDraws} gradients");
        Assert.True(configurations >= 96 && draws > 8000 && textDraws > 4000 && rectangleDraws > 500 && textureDraws > 100 && quadDraws > 20, $"{configurations} configurations, {draws} draw calls");
    }

    [Fact]
    public void EveryTextOfTheCorpus_IsDrawnLikeTheLegacyPipeline()
    {
        List<string> corpus = ReadableCorpus.Value;
        foreach (bool wrap in new[] { false, true })
        {
            FormattedTextHarness scanned = CreateHarness(100, false, wrap, block => block.IsUnderlined = true);
            FormattedTextHarness legacy = CreateHarness(100, true, wrap, block => block.IsUnderlined = true);
            foreach (double? progress in new double?[] { null, 0.5 })
            {
                scanned.TextBlock.TextProgress = progress;
                legacy.TextBlock.TextProgress = progress;
                foreach (string text in corpus)
                {
                    scanned.TextBlock.Text = text;
                    legacy.TextBlock.Text = text;
                    scanned.TextBlock.UpdateLines();
                    legacy.TextBlock.UpdateLines();

                    List<string> expected = Draw(legacy, 1f);
                    List<string> actual = Draw(scanned, 1f);
                    Assert.True(expected.SequenceEqual(actual), $"text=\"{TextRunDescriber.Escape(text)}\" wrap={wrap} progress={progress}:\nexpected {string.Join("\n         ", expected)}\nactual   {string.Join("\n         ", actual)}");
                }
            }
        }
    }

    [Fact]
    public void ADrawnText_MakesTheSameCallsWhenTheLinesAreOfAnEarlierText()
    {
        //  Between a change of text and the next layout the lines of the last layout are drawn (the runs are already the new ones): both pipelines draw the same.
        FormattedTextHarness scanned = CreateHarness(100, false);
        FormattedTextHarness legacy = CreateHarness(100, true);
        string[] texts = { "[b]first[/b] text", "second [color=Red]text[/color] is longer than the first", "x" };
        foreach (string text in texts)
        {
            scanned.TextBlock.Text = text;
            legacy.TextBlock.Text = text;
            scanned.TextBlock.UpdateLines();
            legacy.TextBlock.UpdateLines();
        }

        scanned.TextBlock.SetText("[i]now[/i] a text that is not laid out yet".AsSpan());
        legacy.TextBlock.Text = "[i]now[/i] a text that is not laid out yet";

        Assert.Equal(Draw(legacy, 1f), Draw(scanned, 1f));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(60)]
    public void TheLines_ReadAfterAFontChange_AreTheLinesOfTheLastLayout_LikeTheLegacyPipeline(int width)
    {
        const string text = "hello [b]world[/b] foo [color=Red]aaaaaaaaaaaaaaaaaaaa[/color] bar [img=Icon 4x4]";
        FormattedTextHarness legacy = CreateHarness(width, true);
        FormattedTextHarness scanned = CreateHarness(width, false);
        legacy.TextBlock.Text = text;
        scanned.TextBlock.SetText(text.AsSpan());
        legacy.Frame();
        legacy.Frame();
        scanned.Frame();
        scanned.Frame();

        Assert.True(legacy.TextBlock.TrySetFontSize(20));
        Assert.True(scanned.TextBlock.TrySetFontSize(20));

        List<string> expected = TextRunDescriber.Describe(legacy.TextBlock.Lines);
        List<string> actual = TextRunDescriber.Describe(scanned.TextBlock.Lines);
        Assert.NotNull(expected);
        Assert.Equal(expected, actual);
    }

    //  ---- Tooltips and actions: the bounds a draw keeps, seen through what they are for ----

    private static readonly string[] ToolTipNames = { "Info", "A", "B", "T1", "T2", "Tip", "Another tip", "Name with spaces", "Upper", "a", "b", "Tip0" };
    private static readonly string[] ActionNames = { "Act", "A", "B", "A1", "A2", "Go", "Run", "X", "Click", "Up", "a" };

    private sealed class BoundsProbe
    {
        public FormattedTextHarness Harness { get; }
        public List<string> InvokedCommands { get; } = new();
        private readonly Dictionary<MGToolTip, string> ToolTipNamesByInstance = new();

        public BoundsProbe(bool legacy, bool wrap, int width)
        {
            Harness = CreateHarness(width, legacy, wrap, block => block.VerticalContentAlignment = VerticalAlignment.Top);
            foreach (string name in ActionNames)
            {
                string captured = name;
                Harness.Desktop.Resources.AddCommand(name, _ => InvokedCommands.Add(captured));
            }

            foreach (string name in ToolTipNames)
            {
                MGToolTip toolTip = new(Harness.Window, Harness.TextBlock, 80, 20);
                Harness.Desktop.Resources.AddNamedToolTip(name, toolTip);
                ToolTipNamesByInstance[toolTip] = name;
            }
        }

        public void SetText(string text)
        {
            Harness.TextBlock.Text = text;
            Harness.TextBlock.UpdateLines();
        }

        private void DrawOnce()
        {
            GraphNoOpDrawTransaction transaction = new(Harness.Runtime, DrawSettings.Default);
            Harness.TextBlock.DrawSelf(Harness.CreateDrawArgs(transaction), Harness.TextBlock.LayoutBounds);
        }

        /// <summary>For a grid of positions: which command a click invokes and which tooltip is shown, each after a draw.</summary>
        public List<string> Probe()
        {
            List<string> results = new();
            Rectangle bounds = Harness.TextBlock.LayoutBounds;
            for (int y = bounds.Top; y < bounds.Top + 90; y += 9)
            {
                for (int x = bounds.Left; x < bounds.Right; x += 11)
                {
                    InvokedCommands.Clear();
                    DrawOnce();
                    bool invoked = Harness.TextBlock.TryInvokeActionAt(new Point(x, y));

                    Harness.MoveMouse(new Point(x, y));
                    DrawOnce();
                    bool shown = Harness.TextBlock.ProbeToolTip(out MGToolTip toolTip);
                    results.Add($"({x},{y}) click={invoked}:{string.Join(",", InvokedCommands)} tooltip={shown}:{(toolTip != null && ToolTipNamesByInstance.TryGetValue(toolTip, out string name) ? name : "")}");
                }
            }

            return results;
        }
    }

    [Fact]
    public void TheBoundsOfTheTooltipsAndTheActions_AreTheOnesOfTheLegacyPipeline()
    {
        List<string> corpus = ReadableCorpus.Value.Where(x => x.Contains("[tt=", StringComparison.OrdinalIgnoreCase) || x.Contains("[tooltip=", StringComparison.OrdinalIgnoreCase) ||
                                                              x.Contains("[action=", StringComparison.OrdinalIgnoreCase) || x.Contains("[command=", StringComparison.OrdinalIgnoreCase)).ToList();
        int texts = 0;
        int probes = 0;
        int clicks = 0;
        int toolTipsShown = 0;
        foreach (bool wrap in new[] { false, true })
        {
            BoundsProbe legacy = new(true, wrap, 120);
            BoundsProbe scanned = new(false, wrap, 120);
            foreach (string text in corpus)
            {
                legacy.SetText(text);
                scanned.SetText(text);
                List<string> expected = legacy.Probe();
                List<string> actual = scanned.Probe();
                Assert.True(expected.SequenceEqual(actual), $"text=\"{TextRunDescriber.Escape(text)}\" wrap={wrap}:\n{string.Join("\n", expected.Zip(actual).Where(x => x.First != x.Second).Take(5).Select(x => $"expected {x.First}\nactual   {x.Second}"))}");
                texts++;
                probes += expected.Count;
                clicks += expected.Count(x => x.Contains("click=True"));
                toolTipsShown += expected.Count(x => x.Contains("tooltip=True"));
            }
        }

        Output.WriteLine($"{texts} texts, {probes} probes: {clicks} clicks on an action, {toolTipsShown} tooltips shown");
        Assert.True(texts >= 100 && clicks >= 100 && toolTipsShown >= 100, $"{texts} texts, {clicks} clicks, {toolTipsShown} tooltips");
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(0.8f)]
    [InlineData(0.35f)]
    public void TheDefaultUnderline_HasTheColorOfTheText(float opacity)
    {
        //  The default underline (no brush in the markup) is drawn with the color of the text of its run: the opacity is applied once (it used to be applied twice).
        foreach (string text in new[] { "word", "[color=#336699]word[/color]", "[o=0.5]word[/o]", "[color=#33669980]word[/color]", "[color=Red][o=0.25]word[/o][/color]" })
        {
            foreach (bool legacy in new[] { true, false })
            {
                FormattedTextHarness harness = CreateHarness(300, legacy, false, block => block.IsUnderlined = true);
                harness.TextBlock.Text = text;
                harness.TextBlock.UpdateLines();
                GraphNoOpDrawTransaction actual = new(harness.Runtime, DrawSettings.Default);
                ElementDrawArgs args = harness.CreateDrawArgs(actual, opacity);
                harness.TextBlock.DrawSelf(args, harness.TextBlock.LayoutBounds);

                MGTextRunConfig settings = ((MGTextRunText)harness.TextBlock.Runs[0]).Settings;
                float actualOpacity = opacity * settings.Opacity;
                Color foreground = (settings.Foreground ?? harness.TextBlock.ActualForeground) * actualOpacity;
                Assert.Single(actual.FillRectangleCalls);
                Assert.Equal(foreground, actual.FillRectangleCalls[0].Color);
                Assert.Contains(actual.DrawTextCalls, x => x.Color == foreground);
            }
        }
    }
}
