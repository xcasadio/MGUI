using System.Globalization;
using System.IO;
using System.Text;
using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using MGUI.Core.UI.TextEditing;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit.Abstractions;

namespace MGUI.Tests.Text;

/// <summary>
/// The geometry that the caret of a text box reads, frozen: for a corpus of text box contents, the position, width, original index and parsed index of every character of
/// <see cref="TextRenderInfo"/>, the character found and the caret position (line, original index, parsed index, bounds) after <see cref="MGTextCaret.MoveToApproximateScreenPosition"/> for
/// every point of a grid of screen positions (left halves and right halves of characters, empty lines, whitespace lines, outside the text), and the caret positions of the navigation by
/// index. Each scenario is written as text (see <see cref="Describe"/>), and the text is compared by its line count and its SHA-256 (the same convention as <c>FrozenJournal</c>); a failure
/// writes the whole text.<para/>
/// How the expectations of <see cref="TextBoxCaretGeometryFrozen"/> were captured: on the code before the caret geometry stopped reading <c>MGTextBlock.Lines</c> (task T5.8), by running this
/// class with the environment variable <c>MGUI_CAPTURE_CARET_GEOMETRY</c> set to a folder. In that mode the tests assert nothing: each scenario's text is written to
/// <c>&lt;folder&gt;/&lt;name&gt;.txt</c>, and its name, line count and hash to <c>&lt;folder&gt;/frozen.txt</c>, which was pasted into <see cref="TextBoxCaretGeometryFrozen"/>.
/// Two text engines are used: the fractional one of <see cref="FractionalTestTextEngine"/>, whose metrics are not additive, and the default one of <see cref="GraphTestRuntime"/>.
/// </summary>
public class TextBoxCaretGeometryTests
{
    private readonly ITestOutputHelper Output;

    public TextBoxCaretGeometryTests(ITestOutputHelper output)
    {
        Output = output;
    }

    internal enum BoxKind
    {
        TextBox,
        TextBoxWithSelection,
        PasswordBox,
        RichTextBox,
        RichTextBoxWithSelection,
        RichTextBoxWithStyledSpans,
        RichTextBoxWithStyledSpansAndSelection,
        RichTextBoxWithHighlighter,
        /// <summary>A text box whose text block does not allow inline formatting: its text is laid out by the plain text layout (the escaped text is shown as it is).</summary>
        TextBoxWithoutMarkup,
    }

    internal sealed record Scenario(string Name, BoxKind Kind, string Text, int Width, bool Wrap, HorizontalAlignment Alignment = HorizontalAlignment.Left, int SelectionStart = 0, int SelectionEnd = 0)
    {
        public bool HasExplicitRuns => Kind is BoxKind.RichTextBoxWithStyledSpans or BoxKind.RichTextBoxWithStyledSpansAndSelection or BoxKind.RichTextBoxWithHighlighter;
    }

    /// <summary>A colouriser: every word of four characters or more is bold, and every word that starts with a vowel is italic.</summary>
    private sealed class WordHighlighter : IRichTextSyntaxHighlighter
    {
        public MGRichTextHighlightResult Highlight(MGRichTextHighlightContext context)
        {
            List<MGStyledTextSpan> spans = new();
            string text = context.Text;
            int index = 0;
            while (index < text.Length)
            {
                if (!char.IsLetter(text[index]))
                {
                    index++;
                    continue;
                }

                int start = index;
                while (index < text.Length && char.IsLetter(text[index]))
                {
                    index++;
                }

                bool bold = index - start >= 4;
                bool italic = "aeiou".Contains(char.ToLowerInvariant(text[start]));
                if (bold || italic)
                {
                    spans.Add(new MGStyledTextSpan(new MGTextRange(start, index), new MGRichTextStyle(IsBold: bold, IsItalic: italic)));
                }
            }

            return new MGRichTextHighlightResult(context.Version, spans);
        }
    }

    private sealed class Fixture
    {
        private int Elapsed;

        public GraphTestRuntime Runtime { get; }
        public MGDesktop Desktop { get; }
        public MGWindow Window { get; }
        public MGTextBox TextBox { get; }
        public MGTextBlock Block { get; }

        public Fixture(Scenario scenario, bool fractional)
        {
            Runtime = new GraphTestRuntime(new Rectangle(0, 0, 1200, 600));
            if (fractional)
            {
                Runtime.TextEngine = new FractionalTestTextEngine();
            }

            Desktop = new MGDesktop(Runtime);
            Window = new MGWindow(Desktop, 0, 0, 600, 400);

            switch (scenario.Kind)
            {
                case BoxKind.PasswordBox:
                    TextBox = new MGPasswordBox(Window, 1000);
                    break;
                case BoxKind.RichTextBox:
                case BoxKind.RichTextBoxWithSelection:
                case BoxKind.RichTextBoxWithStyledSpans:
                case BoxKind.RichTextBoxWithStyledSpansAndSelection:
                case BoxKind.RichTextBoxWithHighlighter:
                    TextBox = new MGRichTextBox(Window);
                    break;
                default:
                    TextBox = new MGTextBox(Window);
                    break;
            }

            TextBox.HorizontalAlignment = HorizontalAlignment.Left;
            TextBox.PreferredWidth = scenario.Width;
            TextBox.AcceptsReturn = scenario.Kind != BoxKind.PasswordBox;
            Window.SetContent(TextBox);
            Desktop.Windows.Add(Window);
            Block = TextBox.TextBlockComponent.Element;
            Block.TextAlignment = scenario.Alignment;
            if (scenario.Kind == BoxKind.TextBoxWithoutMarkup)
            {
                Block.AllowsInlineFormatting = false;
            }

            TextBox.WrapText = scenario.Wrap;
            Frames(3);

            TextBox.SetText(scenario.Text);
            Frames(3);

            switch (scenario.Kind)
            {
                case BoxKind.TextBoxWithSelection:
                    TextBox.CurrentSelection = new MGTextBox.TextSelection(scenario.SelectionStart, scenario.SelectionEnd);
                    break;
                case BoxKind.RichTextBoxWithSelection:
                    ((MGRichTextBox)TextBox).SelectionState = new MGTextSelectionState(scenario.SelectionStart, scenario.SelectionEnd);
                    break;
                case BoxKind.RichTextBoxWithStyledSpans:
                case BoxKind.RichTextBoxWithStyledSpansAndSelection:
                    ((MGRichTextBox)TextBox).SetStyledSpans(new[]
                    {
                        new MGStyledTextSpan(new MGTextRange(0, Math.Min(4, scenario.Text.Length)), new MGRichTextStyle(IsBold: true)),
                        new MGStyledTextSpan(new MGTextRange(Math.Min(5, scenario.Text.Length), Math.Min(11, scenario.Text.Length)), new MGRichTextStyle(IsItalic: true)),
                        new MGStyledTextSpan(new MGTextRange(Math.Min(12, scenario.Text.Length), Math.Min(18, scenario.Text.Length)), new MGRichTextStyle(IsBold: true, IsItalic: true)),
                    });
                    if (scenario.Kind == BoxKind.RichTextBoxWithStyledSpansAndSelection)
                    {
                        ((MGRichTextBox)TextBox).SelectionState = new MGTextSelectionState(scenario.SelectionStart, scenario.SelectionEnd);
                    }

                    break;
                case BoxKind.RichTextBoxWithHighlighter:
                    ((MGRichTextBox)TextBox).SyntaxHighlighter = new WordHighlighter();
                    break;
            }

            Frames(3);
        }

        public void Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(Elapsed), TimeSpan.FromMilliseconds(16), default, default));
                Desktop.Update();
                Elapsed += 16;
            }
        }
    }

    // ---- the corpus ----

    private const string OneLine = "Hello caret world";
    private const string Multi = "first\nsecond line\nthird";
    private const string Sentence = "The quick brown fox jumps over the lazy dog while the sleepy cat watches";
    private const string LongWord = "x supercalifragilisticexpialidocious y";
    private const string Spaces = "aaaa bbbb   cccc  dddd eeee ffff";
    private const string EmptyLines = "a\n\nb\n";
    private const string OnlyBreaks = "\n\n";
    private const string CarriageReturns = "a\r\nb\r\n\r\nc";
    private const string Escapes = "a[b]c \\d [e] \\\\ [[x";
    private const string Hyphens = "well-known multi-part-word list-";

    internal static List<Scenario> BuildScenarios()
    {
        List<Scenario> result = new();
        (string Label, string Text)[] texts =
        {
            ("one-line", OneLine), ("multi", Multi), ("sentence", Sentence), ("long-word", LongWord), ("spaces", Spaces), ("empty-lines", EmptyLines),
            ("only-breaks", OnlyBreaks), ("crlf", CarriageReturns), ("escapes", Escapes), ("hyphens", Hyphens), ("empty", ""), ("blank", "   "), ("tab", "a\tb"),
        };

        //  A text box laid out with word wrapping at several widths: the same text meets the line end at different places.
        foreach (var (label, text) in texts)
        {
            foreach (int width in new[] { 60, 95, 140, 220 })
            {
                result.Add(new Scenario($"textbox-wrap-{label}-{width}", BoxKind.TextBox, text, width, true));
            }
        }

        //  A width sweep over a text with runs of spaces, so that a line holds exactly one space at some width (the whitespace line that wrapping ignores).
        for (int width = 44; width <= 132; width += 8)
        {
            result.Add(new Scenario($"textbox-sweep-spaces-{width}", BoxKind.TextBox, Spaces, width, true));
            result.Add(new Scenario($"textbox-sweep-sentence-{width}", BoxKind.TextBox, "aaaa bbbb cccc dddd eeee ffff gggg", width, true));
        }

        foreach (var (label, text) in new[] { ("one-line", OneLine), ("multi", Multi), ("sentence", Sentence), ("empty-lines", EmptyLines), ("escapes", Escapes), ("empty", "") })
        {
            result.Add(new Scenario($"textbox-nowrap-{label}-140", BoxKind.TextBox, text, 140, false));
        }

        foreach (HorizontalAlignment alignment in new[] { HorizontalAlignment.Center, HorizontalAlignment.Right })
        {
            foreach (var (label, text) in new[] { ("multi", Multi), ("sentence", Sentence), ("empty-lines", EmptyLines), ("long-word", LongWord), ("empty", ""), ("one-line", OneLine) })
            {
                result.Add(new Scenario($"textbox-{alignment}-{label}-140", BoxKind.TextBox, text, 140, true, alignment));
            }
        }

        //  A text box with a selection: its text is formatted with selection tags around the selected characters.
        result.Add(new Scenario("textbox-selection-multi-140", BoxKind.TextBoxWithSelection, Multi, 140, true, SelectionStart: 3, SelectionEnd: 13));
        result.Add(new Scenario("textbox-selection-escapes-140", BoxKind.TextBoxWithSelection, Escapes, 140, true, SelectionStart: 1, SelectionEnd: 9));
        result.Add(new Scenario("textbox-selection-sentence-95", BoxKind.TextBoxWithSelection, Sentence, 95, true, SelectionStart: 10, SelectionEnd: 40));

        //  A password box: every character is the password character (a long word without delimiter), line breaks are not accepted.
        foreach (int width in new[] { 60, 95, 220 })
        {
            result.Add(new Scenario($"passwordbox-hunter-{width}", BoxKind.PasswordBox, "hunter2 hunter2 hunter2", width, true));
        }

        result.Add(new Scenario("passwordbox-empty-95", BoxKind.PasswordBox, "", 95, true));

        //  A rich text box (not wrapping by default; wrapping is set by the scenario).
        foreach (var (label, text) in new[] { ("one-line", OneLine), ("multi", Multi), ("empty-lines", EmptyLines), ("escapes", Escapes) })
        {
            result.Add(new Scenario($"richtextbox-{label}-220", BoxKind.RichTextBox, text, 220, false));
            result.Add(new Scenario($"richtextbox-wrap-{label}-95", BoxKind.RichTextBox, text, 95, true));
        }

        //  A rich text box with a selection alone formats the selection with tags, like a text box: it has no explicit runs.
        foreach (var (label, text) in new[] { ("multi", Multi), ("sentence", Sentence), ("empty-lines", EmptyLines), ("long-word", LongWord) })
        {
            foreach (bool wrap in new[] { false, true })
            {
                int width = wrap ? 95 : 220;
                result.Add(new Scenario($"richtextbox-selection-{label}-{(wrap ? "wrap" : "nowrap")}-{width}", BoxKind.RichTextBoxWithSelection, text, width, wrap, SelectionStart: 2, SelectionEnd: Math.Min(text.Length, 14)));
            }
        }

        //  A rich text box with styled spans (bold and italic segments), with or without a selection, or with a colouriser, lays out explicit runs: the caret geometry keeps reading MGTextBlock.Lines.
        foreach (var (label, text) in new[] { ("multi", Multi), ("sentence", Sentence), ("empty-lines", EmptyLines), ("long-word", LongWord) })
        {
            foreach (bool wrap in new[] { false, true })
            {
                int width = wrap ? 95 : 220;
                result.Add(new Scenario($"richtextbox-styled-selection-{label}-{(wrap ? "wrap" : "nowrap")}-{width}", BoxKind.RichTextBoxWithStyledSpansAndSelection, text, width, wrap, SelectionStart: 2, SelectionEnd: Math.Min(text.Length, 14)));
                result.Add(new Scenario($"richtextbox-highlighter-{label}-{(wrap ? "wrap" : "nowrap")}-{width}", BoxKind.RichTextBoxWithHighlighter, text, width, wrap));
                result.Add(new Scenario($"richtextbox-styled-{label}-{(wrap ? "wrap" : "nowrap")}-{width}", BoxKind.RichTextBoxWithStyledSpans, text, width, wrap));
            }
        }

        result.Add(new Scenario("richtextbox-selection-center-multi-220", BoxKind.RichTextBoxWithSelection, Multi, 220, false, HorizontalAlignment.Center, 1, 9));
        result.Add(new Scenario("richtextbox-styled-selection-center-multi-220", BoxKind.RichTextBoxWithStyledSpansAndSelection, Multi, 220, false, HorizontalAlignment.Center, 1, 9));
        result.Add(new Scenario("richtextbox-styled-right-sentence-95", BoxKind.RichTextBoxWithStyledSpans, Sentence, 95, true, HorizontalAlignment.Right));

        //  A text box whose block lays out plain text (no inline formatting): same corpus of texts, wrapping or not, aligned left, centre and right.
        foreach (var (label, text) in texts)
        {
            foreach (int width in new[] { 60, 140 })
            {
                result.Add(new Scenario($"plainblock-wrap-{label}-{width}", BoxKind.TextBoxWithoutMarkup, text, width, true));
            }
        }

        for (int width = 44; width <= 132; width += 16)
        {
            result.Add(new Scenario($"plainblock-sweep-spaces-{width}", BoxKind.TextBoxWithoutMarkup, Spaces, width, true));
        }

        foreach (var (label, text) in new[] { ("multi", Multi), ("sentence", Sentence), ("empty-lines", EmptyLines), ("escapes", Escapes) })
        {
            result.Add(new Scenario($"plainblock-nowrap-{label}-140", BoxKind.TextBoxWithoutMarkup, text, 140, false));
            result.Add(new Scenario($"plainblock-center-{label}-140", BoxKind.TextBoxWithoutMarkup, text, 140, true, HorizontalAlignment.Center));
            result.Add(new Scenario($"plainblock-right-{label}-140", BoxKind.TextBoxWithoutMarkup, text, 140, true, HorizontalAlignment.Right));
        }

        return result;
    }

    // ---- the text of a scenario ----

    private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string DescribeChar(CharRenderInfo ch) => $"({ch.Line.LineIndex}|{ch.IndexInOriginalText}|{ch.IndexInParsedText})";

    private static string DescribeCaret(MGTextCaret caret, bool moved)
    {
        if (!moved || !caret.Position.HasValue)
        {
            return $"none({moved})";
        }

        var p = caret.Position.Value;
        return $"line={p.LineIndex} orig={p.IndexInOriginalText} parsed={p.IndexInParsedText} bounds=[{p.Bounds.X},{p.Bounds.Y},{p.Bounds.Width},{p.Bounds.Height}]({moved})";
    }

    /// <summary>The text that is frozen for a scenario: the characters of every line, the character and the caret position of every point of the grid, the navigation by index.</summary>
    private static List<string> Describe(Fixture fixture, Scenario scenario)
    {
        List<string> lines = new();
        MGTextBox box = fixture.TextBox;
        TextRenderInfo info = box.TextRenderInfo;
        MGTextCaret caret = box.Caret;
        var all = info.Lines;
        lines.Add($"scenario {scenario.Name} text.Length={box.Text.Length} bounds={fixture.Block.LayoutBounds} lines={all.Count} defaultCharacterWidth={F(info.DefaultCharacterWidth)}");

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        for (int i = 0; i < all.Count; i++)
        {
            LineRenderInfo line = all[i];
            StringBuilder builder = new();
            foreach (CharRenderInfo ch in line.Characters)
            {
                builder.Append($" ({ch.IndexInOriginalText}|{ch.IndexInParsedText} x={F(ch.XPosition)} w={F(ch.Width)})");
                minX = Math.Min(minX, ch.MinX);
                maxX = Math.Max(maxX, ch.MaxX);
            }

            lines.Add($"line {line.LineIndex} y={F(line.YPosition)} h={F(line.Height)} n={line.Characters.Count}:{builder}");
        }

        CharRenderInfo first = info.GetFirstChar();
        CharRenderInfo last = info.GetLastChar();
        lines.Add($"first={DescribeChar(first)} last={DescribeChar(last)}");

        //  The lookup by index (original and parsed), then the caret moves by index, on both sides.
        for (int index = -1; index <= box.Text.Length + 2; index++)
        {
            bool foundOriginal = info.TryGetCharAtOriginalIndex(index, out var byOriginal);
            bool foundParsed = info.TryGetCharAtParsedIndex(index, out var byParsed);
            StringBuilder builder = new();
            builder.Append($"index {index}: original={(foundOriginal ? DescribeChar(byOriginal) : "none")} parsed={(foundParsed ? DescribeChar(byParsed) : "none")}");
            foreach (bool left in new[] { true, false })
            {
                caret.Position = null;
                bool movedEnd = caret.MoveToOriginalCharacterIndexOrEnd(index, left);
                builder.Append($" | end(left={left}) {DescribeCaret(caret, movedEnd)}");
            }

            caret.Position = null;
            bool movedLeft = caret.MoveToOriginalCharacterIndexOrLeft(index, true);
            builder.Append($" | orLeft {DescribeCaret(caret, movedLeft)}");
            caret.Position = null;
            bool movedRight = caret.MoveToOriginalCharacterIndexOrRight(index, false);
            builder.Append($" | orRight {DescribeCaret(caret, movedRight)}");
            caret.Position = null;
            bool movedParsedEnd = caret.MoveToParsedCharacterIndexOrEnd(index, false);
            builder.Append($" | parsedEnd {DescribeCaret(caret, movedParsedEnd)}");
            lines.Add(builder.ToString());
        }

        //  The grid of screen positions: the left half and the right half of the characters, the empty lines, the whitespace lines, the margins around the text.
        float top = all[0].YPosition - 6;
        float bottom = all[^1].MaxY + 6;
        for (float y = top; y <= bottom; y += 4.3f)
        {
            for (float x = minX - 6; x <= maxX + 6; x += 3.1f)
            {
                Vector2 position = new(x, y);
                bool found = info.TryGetCharAtScreenPosition(position, out var ch);
                caret.Position = null;
                bool moved = caret.MoveToApproximateScreenPosition(position);
                lines.Add($"point {F(x)},{F(y)} -> {(found ? DescribeChar(ch) : "none")} | {DescribeCaret(caret, moved)}");
            }
        }

        //  The caret navigation by arrows from every character of the first line, and from the end of the text.
        foreach (CharRenderInfo ch in all[0].Characters)
        {
            caret.MoveToCharacter(ch, true);
            StringBuilder builder = new();
            builder.Append($"navigate from {DescribeChar(ch)}:");
            foreach (var step in new (string Name, Func<bool> Move)[]
                     {
                         ("right", () => caret.MoveRight(1)), ("left", () => caret.MoveLeft(1)), ("down", () => caret.MoveDown(1)), ("up", () => caret.MoveUp(1)),
                         ("home", () => caret.MoveToStartOfCurrentLine()), ("end", () => caret.MoveToEndOfCurrentLine()),
                     })
            {
                caret.MoveToCharacter(ch, true);
                bool movedStep = step.Move();
                builder.Append($" {step.Name}: {DescribeCaret(caret, movedStep)};");
            }

            lines.Add(builder.ToString());
        }

        return lines;
    }

    // ---- the comparison ----

    private static readonly string CaptureFolder = Environment.GetEnvironmentVariable("MGUI_CAPTURE_CARET_GEOMETRY");

    private static string Key(Scenario scenario, bool fractional) => $"{scenario.Name}|{(fractional ? "fractional" : "default")}";

    private static void Check(Scenario scenario, bool fractional, List<string> lines, Dictionary<string, (int Lines, string Hash)> expected)
    {
        string key = Key(scenario, fractional);
        string journal = string.Join('\n', lines);
        string hash = FrozenJournal.Hash(journal);

        if (!string.IsNullOrEmpty(CaptureFolder))
        {
            Directory.CreateDirectory(CaptureFolder);
            File.WriteAllText(Path.Combine(CaptureFolder, key.Replace('|', '.') + ".txt"), journal);
            File.AppendAllText(Path.Combine(CaptureFolder, "frozen.txt"), $"(\"{key}\", {lines.Count}, \"{hash}\"),\n");
            return;
        }

        Assert.True(expected.TryGetValue(key, out var frozen), $"{key}: no frozen expectation (lines={lines.Count}, hash={hash})");
        if (frozen.Lines != lines.Count || frozen.Hash != hash)
        {
            Assert.Fail($"{key}: expected {frozen.Lines} lines, hash {frozen.Hash}; got {lines.Count} lines, hash {hash}.\nText:\n{journal}");
        }
    }

    private static Dictionary<string, (int Lines, string Hash)> Expected()
    {
        Dictionary<string, (int Lines, string Hash)> result = new();
        foreach (var (key, lines, hash) in TextBoxCaretGeometryFrozen.Entries)
        {
            result.Add(key, (lines, hash));
        }

        return result;
    }

    [Fact]
    public void TheCorpus_IsFrozen_ForEveryScenarioAndEngine()
    {
        List<Scenario> scenarios = BuildScenarios();
        var expected = Expected();
        int checkedCount = 0;
        foreach (Scenario scenario in scenarios)
        {
            foreach (bool fractional in new[] { true, false })
            {
                Fixture fixture = new(scenario, fractional);
                Assert.True(scenario.HasExplicitRuns == fixture.Block.HasExplicitRuns, $"{Key(scenario, fractional)}: HasExplicitRuns={fixture.Block.HasExplicitRuns}");
                //  The caret geometry of a block that has an internal layout reads it (T5.8); the one that has explicit runs has none, and reads MGTextBlock.Lines.
                bool hasInternalLayout = fixture.Block.KnownPlainLines != null || fixture.Block.KnownFormattedLines != null;
                Assert.True(hasInternalLayout == !scenario.HasExplicitRuns, $"{Key(scenario, fractional)}: internal layout known={hasInternalLayout}");
                Assert.True((fixture.Block.KnownPlainLines != null) == (scenario.Kind == BoxKind.TextBoxWithoutMarkup), $"{Key(scenario, fractional)}: plain layout known={fixture.Block.KnownPlainLines != null}");
                Check(scenario, fractional, Describe(fixture, scenario), expected);
                checkedCount++;
            }
        }

        if (string.IsNullOrEmpty(CaptureFolder))
        {
            Assert.Equal(expected.Count, checkedCount);
        }

        Output.WriteLine($"{scenarios.Count} scenarios, {checkedCount} frozen texts");
    }

    /// <summary>The corpus covers what the plan asks: hyphens that share the index of the next character, whitespace lines that wrapping ignores, empty lines, escaped characters, every alignment,
    /// explicit runs, and a character on the right half of an empty line.</summary>
    [Fact]
    public void TheCorpus_CoversTheCasesThatTheGeometryHasQuirksFor()
    {
        int sharedIndexAfterWrap = 0;
        int ignoredSpaceLines = 0;
        int emptyLines = 0;
        int alignments = 0;
        HashSet<HorizontalAlignment> seenAlignments = new();
        int explicitRuns = 0;
        foreach (Scenario scenario in BuildScenarios())
        {
            Fixture fixture = new(scenario, true);
            var lines = fixture.TextBox.TextRenderInfo.Lines;
            if (seenAlignments.Add(scenario.Alignment))
            {
                alignments++;
            }

            explicitRuns += fixture.Block.HasExplicitRuns ? 1 : 0;
            for (int i = 0; i + 1 < lines.Count; i++)
            {
                var current = lines[i];
                var next = lines[i + 1];
                if (!current.HasCharacters || !next.HasCharacters)
                {
                    continue;
                }

                int lastOfCurrent = current.LastCharacter.IndexInOriginalText;
                int firstOfNext = next.FirstCharacter.IndexInOriginalText;
                if (lastOfCurrent == firstOfNext)
                {
                    sharedIndexAfterWrap++;
                }
                else if (firstOfNext - lastOfCurrent == 2 && fixture.TextBox.Text.Length > firstOfNext && fixture.TextBox.Text[lastOfCurrent + 1] == ' ')
                {
                    ignoredSpaceLines++;
                }
            }

            foreach (var line in lines)
            {
                if (line.HasCharacters && line.Characters.Count == 1 && line.FirstCharacter.Width == fixture.TextBox.TextRenderInfo.DefaultCharacterWidth)
                {
                    emptyLines++;
                }
            }
        }

        Output.WriteLine($"shared index after a wrap: {sharedIndexAfterWrap}; ignored space lines: {ignoredSpaceLines}; empty lines: {emptyLines}; alignments: {alignments}; explicit runs: {explicitRuns}");
        Assert.True(sharedIndexAfterWrap >= 5, $"hyphen cases: {sharedIndexAfterWrap}");
        Assert.True(ignoredSpaceLines >= 1, $"ignored space lines: {ignoredSpaceLines}");
        Assert.True(emptyLines >= 5, $"empty lines: {emptyLines}");
        Assert.Equal(3, alignments);
        Assert.True(explicitRuns >= 20, $"explicit runs: {explicitRuns}");
    }
}
