using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using MGUI.Core.UI.TextEditing;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Text;

public class RichTextBoxStyledRunTests
{
    [Fact]
    public void BuildStyledTextRuns_ConvertsStyledSpansWithoutChangingText()
    {
        MGStyledTextSpan keywordSpan = new(new MGTextRange(0, 5), new MGRichTextStyle(Foreground: Color.CornflowerBlue, IsBold: true), "keyword");

        IReadOnlyList<MGTextRun> runs = MGRichTextBox.BuildStyledTextRuns("class Demo", new[] { keywordSpan });

        Assert.Collection(runs,
            first =>
            {
                MGTextRunText textRun = Assert.IsType<MGTextRunText>(first);
                Assert.Equal("class", textRun.Text);
                Assert.Equal(Color.CornflowerBlue, textRun.Settings.Foreground);
                Assert.True(textRun.Settings.IsBold);
            },
            second =>
            {
                MGTextRunText textRun = Assert.IsType<MGTextRunText>(second);
                Assert.Equal(" Demo", textRun.Text);
                Assert.Null(textRun.Settings.Foreground);
                Assert.False(textRun.Settings.IsBold);
            });
    }

    [Fact]
    public void BuildStyledTextRuns_ClampsAndOrdersSpans()
    {
        MGStyledTextSpan lateSpan = new(new MGTextRange(6, 10), new MGRichTextStyle(Foreground: Color.Orange), "identifier");
        MGStyledTextSpan earlySpan = new(new MGTextRange(-2, 5), new MGRichTextStyle(Foreground: Color.MediumPurple), "keyword");

        IReadOnlyList<MGTextRun> runs = MGRichTextBox.BuildStyledTextRuns("class Demo", new[] { lateSpan, earlySpan });

        Assert.Collection(runs,
            first => Assert.Equal("class", Assert.IsType<MGTextRunText>(first).Text),
            second => Assert.Equal(" ", Assert.IsType<MGTextRunText>(second).Text),
            third => Assert.Equal("Demo", Assert.IsType<MGTextRunText>(third).Text));

        Assert.Equal(Color.MediumPurple, ((MGTextRunText)runs[0]).Settings.Foreground);
        Assert.Null(((MGTextRunText)runs[1]).Settings.Foreground);
        Assert.Equal(Color.Orange, ((MGTextRunText)runs[2]).Settings.Foreground);
    }

    [Fact]
    public void BuildStyledTextRuns_SkipsOverlappingEarlierTextAfterCursor()
    {
        MGStyledTextSpan firstSpan = new(new MGTextRange(0, 7), new MGRichTextStyle(Foreground: Color.Red), "first");
        MGStyledTextSpan overlappingSpan = new(new MGTextRange(3, 10), new MGRichTextStyle(Foreground: Color.Yellow), "second");

        IReadOnlyList<MGTextRun> runs = MGRichTextBox.BuildStyledTextRuns("class Demo", new[] { firstSpan, overlappingSpan });

        Assert.Collection(runs,
            first => Assert.Equal("class D", Assert.IsType<MGTextRunText>(first).Text),
            second => Assert.Equal("emo", Assert.IsType<MGTextRunText>(second).Text));
    }

    [Fact]
    public void BuildStyledTextRuns_ConvertsNewLinesToExplicitLineBreakRuns()
    {
        MGStyledTextSpan keywordSpan = new(new MGTextRange(0, 1), new MGRichTextStyle(Foreground: Color.CornflowerBlue), "keyword");

        IReadOnlyList<MGTextRun> runs = MGRichTextBox.BuildStyledTextRuns("a\n\nb", new[] { keywordSpan });

        Assert.Collection(runs,
            first =>
            {
                MGTextRunText textRun = Assert.IsType<MGTextRunText>(first);
                Assert.Equal("a", textRun.Text);
                Assert.Equal(Color.CornflowerBlue, textRun.Settings.Foreground);
            },
            second => Assert.Equal(1, Assert.IsType<MGTextRunLineBreak>(second).LineBreakCharacterCount),
            third => Assert.Equal(1, Assert.IsType<MGTextRunLineBreak>(third).LineBreakCharacterCount),
            fourth => Assert.Equal("b", Assert.IsType<MGTextRunText>(fourth).Text));

        foreach (MGTextRunText textRun in runs.OfType<MGTextRunText>())
        {
            Assert.DoesNotContain("\n", textRun.Text);
            Assert.DoesNotContain("\r", textRun.Text);
        }
    }

    [Fact]
    public void BuildStyledTextRuns_SelectionAddsBackgroundWithoutRemovingSyntaxForeground()
    {
        MGStyledTextSpan keywordSpan = new(new MGTextRange(0, 5), new MGRichTextStyle(Foreground: Color.CornflowerBlue, IsBold: true), "keyword");

        IReadOnlyList<MGTextRun> runs = MGRichTextBox.BuildStyledTextRuns("class Demo", new[] { keywordSpan }, new MGTextRange(1, 7), Color.DarkBlue);

        Assert.Collection(runs,
            first =>
            {
                MGTextRunText textRun = Assert.IsType<MGTextRunText>(first);
                Assert.Equal("c", textRun.Text);
                Assert.Equal(Color.CornflowerBlue, textRun.Settings.Foreground);
                Assert.True(textRun.Settings.IsBold);
                Assert.Null(textRun.Settings.Background.Brush);
            },
            second =>
            {
                MGTextRunText textRun = Assert.IsType<MGTextRunText>(second);
                Assert.Equal("lass", textRun.Text);
                Assert.Equal(Color.CornflowerBlue, textRun.Settings.Foreground);
                Assert.True(textRun.Settings.IsBold);
                Assert.NotNull(textRun.Settings.Background.Brush);
            },
            third =>
            {
                MGTextRunText textRun = Assert.IsType<MGTextRunText>(third);
                Assert.Equal(" D", textRun.Text);
                Assert.Null(textRun.Settings.Foreground);
                Assert.False(textRun.Settings.IsBold);
                Assert.NotNull(textRun.Settings.Background.Brush);
            },
            fourth =>
            {
                MGTextRunText textRun = Assert.IsType<MGTextRunText>(fourth);
                Assert.Equal("emo", textRun.Text);
                Assert.Null(textRun.Settings.Foreground);
                Assert.Null(textRun.Settings.Background.Brush);
            });
    }
}