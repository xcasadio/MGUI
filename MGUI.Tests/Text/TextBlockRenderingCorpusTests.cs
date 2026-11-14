using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Text;

public enum TextBlockCorpusId
{
    ShortNumber,
    OneLineSentence,
    MultiLine,
    WrappedSentence,
    Formatted
}

/// <summary>A text of the corpus, with two variants for the alternations of the measurement bench.</summary>
/// <param name="SameLengthText">Same length as <paramref name="Text"/>.</param>
/// <param name="DifferentLengthText">Not the same length as <paramref name="Text"/>.</param>
internal sealed record TextBlockCorpusEntry(string Text, string SameLengthText, string DifferentLengthText, bool WrapText, int WindowWidth);

internal static class TextBlockCorpus
{
    public static TextBlockCorpusEntry Get(TextBlockCorpusId id) => id switch
    {
        TextBlockCorpusId.ShortNumber => new("0.42", "0.43", "10.00", false, 300),
        TextBlockCorpusId.OneLineSentence => new("The quick brown fox jumps over the lazy dog", "The quick brown fox jumps over the lazy cat",
            "The quick brown fox jumps over the lazy dog again", false, 400),
        TextBlockCorpusId.MultiLine => new("First line\nSecond line\nThird line", "First line\nSecond line\nThird lime",
            "First line\nSecond line\nThird line\nFourth line", false, 300),
        //  About 70 characters at 6 px per character in a 120 px window: the text wraps on several lines.
        TextBlockCorpusId.WrappedSentence => new("The quick brown fox jumps over the lazy dog while the sleepy cat watches.",
            "The quick brown fox jumps over the lazy dog while the sleepy cat watches!",
            "The quick brown fox jumps over the lazy dog while the sleepy cat quietly watches.", true, 120),
        TextBlockCorpusId.Formatted => new("Value: [b]0.42[/b] and [color=Red]red text[/color] here", "Value: [b]0.43[/b] and [color=Red]red text[/color] here",
            "Value: [b]10.00[/b] and [color=Red]red text[/color] here", false, 400),
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
    };
}

/// <summary>A desktop on a <see cref="GraphTestRuntime"/> with one window whose content is one <see cref="MGTextBlock"/>, laid out by two frames.<para/>
/// The test text engine measures a text as <c>length * 6</c> px wide and <c>12</c> px tall at font size 12, whatever the characters.</summary>
internal sealed class TextBlockFixture
{
    private int ElapsedMilliseconds;

    public GraphTestRuntime Runtime { get; }
    public MGDesktop Desktop { get; }
    public MGWindow Window { get; }
    public MGTextBlock TextBlock { get; }
    public NonRecordingDrawTransaction Transaction { get; }
    public Size AvailableSize { get; }

    public TextBlockFixture(TextBlockCorpusId id, bool allowsInlineFormatting)
    {
        TextBlockCorpusEntry entry = TextBlockCorpus.Get(id);
        Runtime = new GraphTestRuntime(new Rectangle(0, 0, 640, 360));
        Desktop = new MGDesktop(Runtime);
        Transaction = new NonRecordingDrawTransaction(Runtime, DrawSettings.Default);
        Window = new MGWindow(Desktop, 0, 0, entry.WindowWidth, 200)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0)
        };
        TextBlock = new MGTextBlock(Window, entry.Text, Color.White, 12, allowsInlineFormatting)
        {
            WrapText = entry.WrapText
        };
        Window.SetContent(TextBlock);
        Desktop.Windows.Add(Window);
        AvailableSize = new Size(entry.WindowWidth, 200);
        Frame();
        Frame();
    }

    public void Update()
    {
        Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(ElapsedMilliseconds), TimeSpan.FromMilliseconds(16), default, default));
        Desktop.Update();
        ElapsedMilliseconds += 16;
    }

    public void Frame()
    {
        Update();
        Desktop.Draw(Transaction);
    }

    public ElementDrawArgs CreateDrawArgs(IUIDrawTransaction transaction)
        => new(new DrawBaseArgs(TimeSpan.Zero, transaction, 1f), new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);
}

/// <summary>
/// Characterization of what <see cref="MGTextBlock"/> produces for a corpus of texts: its <see cref="MGTextBlock.Runs"/>, its <see cref="MGTextBlock.Lines"/>,
/// the size it measures and the text draw calls of <see cref="MGTextBlock.DrawSelf"/>. The expected values were taken from the code as it was before the
/// allocation-free rework of the text pipeline, and every later step of that rework keeps them: a difference is a change of behaviour.<para/>
/// The test text engine measures a text as <c>length * 6</c> px wide and <c>12</c> px tall at font size 12, whatever the characters (bold included),
/// and the lines of a text block are separated by a 2 px line padding.
/// </summary>
public class TextBlockRenderingCorpusTests
{
    /// <param name="Text">The text of the run, or null for a line break run.</param>
    private readonly record struct ExpectedRun(string Text, bool IsBold = false, bool IsItalic = false, Color? Foreground = null);

    private readonly record struct ExpectedLine(string Text, float Width);

    private readonly record struct ExpectedDraw(string Text, float X, float Y);

    [Fact]
    public void ShortNumber()
    {
        AssertRendering(TextBlockCorpusId.ShortNumber, true,
            runs: new[] { new ExpectedRun("0.42") },
            lines: new[] { new ExpectedLine("0.42", 24) },
            measuredWidth: 24, measuredHeight: 12,
            draws: new[] { new ExpectedDraw("0.42", 1, 94) });
    }

    [Fact]
    public void ShortNumber_WithoutInlineFormatting_IsTheSame()
    {
        AssertRendering(TextBlockCorpusId.ShortNumber, false,
            runs: new[] { new ExpectedRun("0.42") },
            lines: new[] { new ExpectedLine("0.42", 24) },
            measuredWidth: 24, measuredHeight: 12,
            draws: new[] { new ExpectedDraw("0.42", 1, 94) });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OneLineSentence(bool allowsInlineFormatting)
    {
        AssertRendering(TextBlockCorpusId.OneLineSentence, allowsInlineFormatting,
            runs: new[] { new ExpectedRun("The quick brown fox jumps over the lazy dog") },
            lines: new[] { new ExpectedLine("The quick brown fox jumps over the lazy dog", 258) },
            measuredWidth: 258, measuredHeight: 12,
            draws: new[] { new ExpectedDraw("The quick brown fox jumps over the lazy dog", 1, 94) });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MultiLine(bool allowsInlineFormatting)
    {
        AssertRendering(TextBlockCorpusId.MultiLine, allowsInlineFormatting,
            runs: new[]
            {
                new ExpectedRun("First line"), new ExpectedRun(null),
                new ExpectedRun("Second line"), new ExpectedRun(null),
                new ExpectedRun("Third line")
            },
            lines: new[] { new ExpectedLine("First line", 60), new ExpectedLine("Second line", 66), new ExpectedLine("Third line", 60) },
            measuredWidth: 66, measuredHeight: 40,
            draws: new[] { new ExpectedDraw("First line", 1, 80), new ExpectedDraw("Second line", 1, 94), new ExpectedDraw("Third line", 1, 108) });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WrappedSentence(bool allowsInlineFormatting)
    {
        AssertRendering(TextBlockCorpusId.WrappedSentence, allowsInlineFormatting,
            runs: new[] { new ExpectedRun("The quick brown fox jumps over the lazy dog while the sleepy cat watches.") },
            lines: new[]
            {
                new ExpectedLine("The quick brown fox", 114),
                new ExpectedLine(" jumps over the ", 96),
                new ExpectedLine("lazy dog while the ", 114),
                new ExpectedLine("sleepy cat watches.", 114)
            },
            measuredWidth: 114, measuredHeight: 54,
            draws: new[]
            {
                new ExpectedDraw("The quick brown fox", 1, 73),
                new ExpectedDraw(" jumps over the ", 1, 87),
                new ExpectedDraw("lazy dog while the ", 1, 101),
                new ExpectedDraw("sleepy cat watches.", 1, 115)
            });
    }

    [Fact]
    public void Formatted_BoldAndColorTagsBecomeRuns()
    {
        AssertRendering(TextBlockCorpusId.Formatted, true,
            runs: new[]
            {
                new ExpectedRun("Value: "),
                new ExpectedRun("0.42", IsBold: true),
                new ExpectedRun(" and "),
                new ExpectedRun("red text", Foreground: Color.Red),
                new ExpectedRun(" here")
            },
            lines: new[] { new ExpectedLine("Value: 0.42 and red text here", 174) },
            measuredWidth: 174, measuredHeight: 12,
            draws: new[]
            {
                new ExpectedDraw("Value: ", 1, 94),
                new ExpectedDraw("0.42", 43, 94),
                new ExpectedDraw(" and ", 67, 94),
                new ExpectedDraw("red text", 97, 94),
                new ExpectedDraw(" here", 145, 94)
            });
    }

    [Fact]
    public void Formatted_WithoutInlineFormatting_KeepsTheTagsAsLiteralText()
    {
        const string text = "Value: [b]0.42[/b] and [color=Red]red text[/color] here";
        AssertRendering(TextBlockCorpusId.Formatted, false,
            runs: new[] { new ExpectedRun(text) },
            lines: new[] { new ExpectedLine(text, 330) },
            measuredWidth: 330, measuredHeight: 12,
            draws: new[] { new ExpectedDraw(text, 1, 94) });
    }

    private static void AssertRendering(TextBlockCorpusId id, bool allowsInlineFormatting, ExpectedRun[] runs, ExpectedLine[] lines,
        int measuredWidth, int measuredHeight, ExpectedDraw[] draws)
    {
        TextBlockFixture fixture = new(id, allowsInlineFormatting);
        MGTextBlock textBlock = fixture.TextBlock;

        Assert.Equal(runs.Length, textBlock.Runs.Count);
        for (int i = 0; i < runs.Length; i++)
        {
            if (runs[i].Text == null)
            {
                Assert.Equal(TextRunType.LineBreak, textBlock.Runs[i].RunType);
                continue;
            }

            MGTextRunText actual = Assert.IsType<MGTextRunText>(textBlock.Runs[i]);
            Assert.Equal(runs[i].Text, actual.Text);
            Assert.Equal(runs[i].IsBold, actual.Settings.IsBold);
            Assert.Equal(runs[i].IsItalic, actual.Settings.IsItalic);
            Assert.Equal(runs[i].Foreground, actual.Settings.Foreground);
        }

        Assert.Equal(lines.Length, textBlock.Lines.Count);
        for (int i = 0; i < lines.Length; i++)
        {
            MGTextLine actual = textBlock.Lines[i];
            Assert.Equal(lines[i].Text, string.Concat(actual.Runs.OfType<MGTextRunText>().Select(x => x.Text)));
            Assert.Equal(lines[i].Width, actual.LineWidth);
            Assert.Equal(i + 1, actual.LineNumber);
        }

        //  The desired size of a text block is returned as a Thickness whose Left is the width and Top the height.
        Thickness measured = textBlock.MeasureSelfOverride(fixture.AvailableSize, out _);
        Assert.Equal(measuredWidth, measured.Left);
        Assert.Equal(measuredHeight, measured.Top);

        GraphNoOpDrawTransaction transaction = new(fixture.Runtime, DrawSettings.Default);
        textBlock.DrawSelf(fixture.CreateDrawArgs(transaction), textBlock.LayoutBounds);
        Assert.Equal(draws.Length, transaction.DrawTextCalls.Count);
        for (int i = 0; i < draws.Length; i++)
        {
            GraphDrawTextCall actual = transaction.DrawTextCalls[i];
            Assert.Equal(draws[i].Text, actual.Text);
            Assert.Equal(new Vector2(draws[i].X, draws[i].Y), actual.Position);
            Assert.Equal(1f, actual.Scale);
        }
    }
}
