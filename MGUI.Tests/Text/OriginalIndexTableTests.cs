using System.Text;
using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit.Abstractions;

namespace MGUI.Tests.Text;

/// <summary>
/// The table of original character indices that the internal layouts give (<see cref="PlainTextLayout.AppendOriginalIndices"/>, <see cref="FormattedTextLayout.AppendOriginalIndices"/>) against the
/// table that the legacy code computes, <see cref="MGTextLine.OriginalCharacterIndices"/> of the lines of <see cref="MGTextLine.ParseLines(ITextMeasurer, double, bool, IEnumerable{MGTextRun}, bool)"/>:
/// the same number of lines, and for each line the same indices, entry by entry (the hyphen that wrapping inserts shares the index of the next character, a line break counts its characters, a line
/// of white space that wrapping ignores still takes its index, an empty line has one entry). The caret geometry of a text box reads this table instead of the legacy lines (decision D19).<para/>
/// Texts: the corpus of the plain text tests, the corpus of the formatted text tests (fixed and generated from a seed), and a seeded random draw of plain texts, each with and without wrapping,
/// with and without ignoring the lines of white space, at a set of widths and, for the draw, at random widths. The text engine is the fractional one, whose metrics are not additive.
/// </summary>
public class OriginalIndexTableTests
{
    private const int PlainSeed = 20261116;
    private const int PlainDrawCount = 700;
    private const int FormattedGeneratedCount = 1500;

    private static readonly double[] LayoutWidths = { 0, 0.5, 1, 5, 17, 33.5, 60, 100, 250, 1000, 100000, double.MaxValue };

    private readonly ITestOutputHelper Output;

    public OriginalIndexTableTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private static MGTextRunConfig Settings(bool bold, bool italic)
        => new(bold, italic, 1, null, new MGTextRunUnderlineConfig(false), default, default);

    private static List<MGTextRun> LegacyRuns(string text, MGTextRunConfig settings)
        => MGTextRun.ParseRuns(FTTokenizer.TokenizeLineBreaks(text, true).ToList(), settings).ToList();

    /// <summary>Plain texts drawn from a fixed seed: words, long words, runs of spaces, hyphens, tabs and the three kinds of line break.</summary>
    internal static List<string> BuildPlainDraw(int seed, int count)
    {
        Random random = new(seed);
        string[] separators = { " ", " ", " ", "  ", "   ", "-", "--", "\n", "\r\n", "\r", "\t", " - ", "\n\n" };
        List<string> texts = new(count);
        for (int i = 0; i < count; i++)
        {
            StringBuilder builder = new();
            int parts = random.Next(1, 30);
            for (int j = 0; j < parts; j++)
            {
                if (random.Next(3) == 0)
                {
                    builder.Append(separators[random.Next(separators.Length)]);
                }
                else
                {
                    int length = random.Next(6) == 0 ? random.Next(12, 60) : random.Next(1, 10);
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

    private static string Escape(string text) => TextRunDescriber.Escape(text);

    private static void AssertSameTables(List<MGTextLine> expected, int lineCount, Action<int, List<int>> appendActual, string context)
    {
        Assert.True(expected.Count == lineCount, $"{context}: expected {expected.Count} lines, got {lineCount}");
        List<int> actual = new();
        for (int i = 0; i < expected.Count; i++)
        {
            actual.Clear();
            appendActual(i, actual);
            Assert.True(expected[i].OriginalCharacterIndices.SequenceEqual(actual),
                $"{context}: line {i} ({string.Join("|", expected[i].Runs.Select(TextRunDescriber.Describe))})\n  expected [{string.Join(", ", expected[i].OriginalCharacterIndices)}]\n  got      [{string.Join(", ", actual)}]");
        }
    }

    [Fact]
    public void ThePlainLayout_GivesTheOriginalIndicesOfParseLines_OnTheCorpusOfThePlainTextTests()
    {
        PlainTextDifferentialTests.Fixture fixture = new(400, true, null, 0, false, false);
        PlainTextLayout layout = new();
        int texts = 0;
        int compared = 0;
        int lines = 0;

        foreach (string text in PlainTextDifferentialTests.BuildCorpus())
        {
            texts++;
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
                            AssertSameTables(expected, layout.LineCount, layout.AppendOriginalIndices, $"text=\"{Escape(text)}\" bold={bold} width={width} wrap={wrap} ignoreEmptySpaceLines={ignoreEmptySpaceLines}");
                            compared++;
                            lines += expected.Count;
                        }
                    }
                }
            }
        }

        Output.WriteLine($"plain corpus: {texts} texts, {compared} layouts compared, {lines} lines");
        Assert.True(compared > 20000, $"compared {compared} layouts");
    }

    [Fact]
    public void ThePlainLayout_GivesTheOriginalIndicesOfParseLines_OnASeededRandomDraw()
    {
        PlainTextDifferentialTests.Fixture fixture = new(400, true, null, 0, false, false);
        PlainTextLayout layout = new();
        List<string> texts = BuildPlainDraw(PlainSeed, PlainDrawCount);
        Random widthRandom = new(PlainSeed + 1);
        MGTextRunConfig settings = Settings(false, false);
        int compared = 0;
        int lines = 0;
        int wrappedHyphens = 0;

        foreach (string text in texts)
        {
            List<MGTextRun> runs = LegacyRuns(text, settings);
            double[] widths = { widthRandom.Next(1, 40) + widthRandom.NextDouble(), widthRandom.Next(40, 120) + widthRandom.NextDouble(), widthRandom.Next(120, 400) + widthRandom.NextDouble(), 100000 };
            foreach (double width in widths)
            {
                foreach (bool wrap in new[] { true, false })
                {
                    foreach (bool ignoreEmptySpaceLines in new[] { true, false })
                    {
                        List<MGTextLine> expected = MGTextLine.ParseLines(fixture.TextBlock, width, wrap, runs, ignoreEmptySpaceLines).ToList();
                        layout.Build(fixture.TextBlock, text.AsSpan(), width, wrap, ignoreEmptySpaceLines, settings, true);
                        AssertSameTables(expected, layout.LineCount, layout.AppendOriginalIndices, $"seed={PlainSeed} text=\"{Escape(text)}\" width={width} wrap={wrap} ignoreEmptySpaceLines={ignoreEmptySpaceLines}");
                        compared++;
                        lines += expected.Count;
                        for (int i = 0; i + 1 < expected.Count; i++)
                        {
                            var current = expected[i].OriginalCharacterIndices;
                            if (!expected[i].EndsInLinebreakCharacter && current.Count > 0 && expected[i + 1].OriginalCharacterIndices.Count > 0 && current[^1] == expected[i + 1].OriginalCharacterIndices[0])
                            {
                                wrappedHyphens++;
                            }
                        }
                    }
                }
            }
        }

        Output.WriteLine($"plain draw: seed {PlainSeed}, {texts.Count} texts, {compared} layouts compared, {lines} lines, {wrappedHyphens} lines that end with a hyphen that shares the next index");
        Assert.True(compared >= PlainDrawCount * 16);
        Assert.True(wrappedHyphens > 200, $"hyphens: {wrappedHyphens}");
    }

    [Fact]
    public void TheFormattedLayout_GivesTheOriginalIndicesOfParseLines_OnTheFormattedCorpusAndASeededDraw()
    {
        FormattedTextHarness legacy = new(400, true, true, null, false, new FractionalTestTextEngine());
        FormattedTextHarness legacyBold = new(400, true, true, block => block.IsBold = true, false, new FractionalTestTextEngine());
        FormattedTextScanner scanner = new();
        FormattedTextRuns scanned = new();
        FormattedTextLayout layout = new();
        List<string> corpus = FormattedTextCorpus.Build(FormattedGeneratedCount);
        int texts = 0;
        int compared = 0;
        int lines = 0;
        int withImages = 0;
        int withSeveralRuns = 0;

        foreach (string text in corpus)
        {
            foreach (bool bold in new[] { false, true })
            {
                FormattedTextHarness harness = bold ? legacyBold : legacy;
                List<MGTextRun> runs;
                try
                {
                    harness.TextBlock.Text = text;
                    runs = harness.TextBlock.Runs.ToList();
                }
                catch (Exception)
                {
                    //  The legacy parser rejects the text: there is no legacy table to compare with.
                    continue;
                }

                if (!scanner.TryScan(text.AsSpan(), Settings(bold, false), harness.Desktop, scanned))
                {
                    continue;
                }

                texts++;
                withImages += runs.Any(x => x is MGTextRunImage) ? 1 : 0;
                withSeveralRuns += runs.Count(x => x is MGTextRunText) > 1 ? 1 : 0;
                foreach (double width in LayoutWidths)
                {
                    foreach (bool wrap in new[] { true, false })
                    {
                        foreach (bool ignoreEmptySpaceLines in new[] { true, false })
                        {
                            List<MGTextLine> expected = MGTextLine.ParseLines(harness.TextBlock, width, wrap, runs, ignoreEmptySpaceLines).ToList();
                            layout.Build(harness.TextBlock, scanned, width, wrap, ignoreEmptySpaceLines, true);
                            AssertSameTables(expected, layout.LineCount, layout.AppendOriginalIndices, $"text=\"{Escape(text)}\" bold={bold} width={width} wrap={wrap} ignoreEmptySpaceLines={ignoreEmptySpaceLines}");
                            compared++;
                            lines += expected.Count;
                        }
                    }
                }
            }
        }

        Output.WriteLine($"formatted corpus: {corpus.Count} texts ({FormattedGeneratedCount} generated from the seed of FormattedTextCorpus), {texts} read by both pipelines, {compared} layouts compared, {lines} lines, {withImages} with images, {withSeveralRuns} with several text runs");
        Assert.True(compared > 20000, $"compared {compared} layouts");
        Assert.True(withImages >= 20 && withSeveralRuns >= 200);
    }

    /// <summary>A layout rebuilt in the same buffers gives the same table as a fresh one, whatever was built before.</summary>
    [Fact]
    public void TheTables_DoNotDependOnWhatTheLayoutsWereBuiltFromBefore()
    {
        PlainTextDifferentialTests.Fixture fixture = new(400, true, null, 0, false, false);
        PlainTextLayout reused = new();
        MGTextRunConfig settings = Settings(false, false);
        List<int> reusedTable = new();
        List<int> freshTable = new();
        foreach (string text in BuildPlainDraw(PlainSeed + 2, 200))
        {
            PlainTextLayout fresh = new();
            reused.Build(fixture.TextBlock, text.AsSpan(), 60, true, true, settings, true);
            fresh.Build(fixture.TextBlock, text.AsSpan(), 60, true, true, settings, true);
            Assert.Equal(fresh.LineCount, reused.LineCount);
            for (int i = 0; i < fresh.LineCount; i++)
            {
                reusedTable.Clear();
                freshTable.Clear();
                reused.AppendOriginalIndices(i, reusedTable);
                fresh.AppendOriginalIndices(i, freshTable);
                Assert.Equal(freshTable, reusedTable);
            }
        }
    }
}
