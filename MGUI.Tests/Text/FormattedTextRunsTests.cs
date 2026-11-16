using System.ComponentModel;
using System.Globalization;
using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using Xunit.Abstractions;

namespace MGUI.Tests.Text;

/// <summary>
/// The runs of a formatted text read by the allocation-free scanner (<see cref="FormattedTextScanner"/>) against the ones of the legacy pipeline (<see cref="FTTokenizer"/>, <see cref="FTParser"/>,
/// <see cref="MGTextRun.ParseRuns(string, MGTextRunConfig)"/> and the sanitizing of <see cref="MGTextBlock"/>), reached through <see cref="MGTextBlock.ForceLegacyTextPath"/>.<para/>
/// The public <see cref="MGTextBlock.Runs"/> of a block on the new path are built from what the scanner read (they are not parsed again), so comparing them with the legacy ones compares the scanner
/// with the legacy code. <see cref="MGTextBlock.AreFormattedRunsKnown"/> says which path read a text: the tests check that the scanner reads everything the legacy code reads: the grammar of tags is ASCII in both
/// (MGUI ADR-0033), so no text is declined (but an image with no size in a block that has no desktop, which these blocks have).
/// </summary>
public class FormattedTextRunsTests
{
    private readonly ITestOutputHelper Output;

    public FormattedTextRunsTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private static (Exception Error, List<string> Runs) SetAndRead(FormattedTextHarness harness, string text, bool fromSpan)
    {
        Exception error = null;
        try
        {
            if (fromSpan)
            {
                harness.TextBlock.SetText(text.AsSpan());
            }
            else
            {
                harness.TextBlock.Text = text;
            }
        }
        catch (Exception exception)
        {
            error = exception;
        }

        return (error, TextRunDescriber.Describe(harness.TextBlock.Runs));
    }

    public static IEnumerable<object[]> Defaults() => new[]
    {
        new object[] { false, false, false, false },
        new object[] { true, false, false, false },
        new object[] { false, true, true, true },
        new object[] { true, true, false, true },
    };

    [Theory]
    [MemberData(nameof(Defaults))]
    public void TheRuns_AreTheRunsOfTheLegacyPipeline(bool bold, bool italic, bool underlined, bool shadowed)
        => CompareTheCorpus(bold, italic, underlined, shadowed);

    /// <summary>The whole corpus again under the cultures whose rules of case or of digits differ (tr-TR is the one where i and I are not a pair, fr-FR and ar-SA have their own number formats):
    /// what is a tag does not depend on the culture of the process, and the scanner reads every text.</summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    [InlineData("fr-FR")]
    [InlineData("")]
    public void TheRuns_AreTheRunsOfTheLegacyPipeline_UnderEveryCulture(string culture)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CompareTheCorpus(true, true, false, true);
            CompareTheCorpus(false, false, true, false);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private void CompareTheCorpus(bool bold, bool italic, bool underlined, bool shadowed)
    {
        void Configure(MGTextBlock block)
        {
            block.IsBold = bold;
            block.IsItalic = italic;
            block.IsUnderlined = underlined;
            block.IsShadowed = shadowed;
            block.ShadowOffset = new Microsoft.Xna.Framework.Point(2, 1);
        }

        FormattedTextHarness legacy = new(300, true, configure: Configure);
        FormattedTextHarness scanned = new(300, false, configure: Configure);
        List<string> corpus = FormattedTextCorpus.Build();
        int read = 0;
        int threw = 0;
        int declined = 0;

        foreach (string text in corpus)
        {
            foreach (bool fromSpan in new[] { false, true })
            {
                //  Start every text from the same text, so a text that throws leaves the same previous runs in both blocks.
                SetAndRead(legacy, "[b]previous[/b]", false);
                SetAndRead(scanned, "[b]previous[/b]", false);

                (Exception expectedError, List<string> expectedRuns) = SetAndRead(legacy, text, false);
                (Exception actualError, List<string> actualRuns) = SetAndRead(scanned, text, fromSpan);
                string context = $"text=\"{TextRunDescriber.Escape(text)}\" fromSpan={fromSpan}";

                Assert.True((expectedError == null) == (actualError == null), $"{context}: legacy threw {expectedError?.GetType().Name}, scanned threw {actualError?.GetType().Name}");
                if (expectedError != null)
                {
                    Assert.True(expectedError.GetType() == actualError.GetType() && expectedError.Message == actualError.Message, $"{context}: {expectedError.GetType().Name} '{expectedError.Message}' vs {actualError.GetType().Name} '{actualError.Message}'");
                }

                Assert.True(expectedRuns.SequenceEqual(actualRuns), $"{context}:\nexpected {string.Join("\n         ", expectedRuns)}\nactual   {string.Join("\n         ", actualRuns)}");

                if (expectedError != null)
                {
                    //  The runs are the ones of the previous text, and the scanner did not read this one.
                    threw++;
                }
                else if (scanned.TextBlock.AreFormattedRunsKnown)
                {
                    read++;
                }
                else
                {
                    //  The grammar of tags is ASCII (MGUI ADR-0033): the scanner leaves no text to the legacy code but an image with no size in a block with no desktop, and the blocks here have one.
                    declined++;
                    Assert.Fail($"{context}: the scanner declined a text that the legacy code reads");
                }
            }
        }

        Output.WriteLine($"{corpus.Count} texts (x2): {read} read by the scanner, {threw} that the legacy code throws on, {declined} declined without a throw");
        Assert.True(corpus.Count >= 500);
        Assert.True(read >= 1000, $"{read} read");
        //  No markup makes the reading fail (MGUI ADR-0030)
        Assert.Equal(0, threw);
        Assert.Equal(0, declined);
        Assert.Equal(corpus.Count * 2, read);
    }

    public static IEnumerable<object[]> TextsWithAnOpenTagCharacterThatOpensNoTag() => new[]
    {
        //  text, then what is expected: the texts of the runs, "|" between two runs, "*" before a bold one, "/" for a line break
        new object[] { "[bold", "[bold" },
        new object[] { "abc[", "abc[" },
        new object[] { "[", "[" },
        new object[] { "[foo]", "[foo]" },
        new object[] { "[color=NotAColor]x", "[color=NotAColor]x" },
        new object[] { "[o=1.5]x", "[o=1.5]x" },
        new object[] { "[u=99999999999]x[/u]", "[u=99999999999]x" },
        new object[] { "[b x]y", "[b x]y" },
        new object[] { "[/b x]y", "[/b x]y" },
        new object[] { "[tt=Name", "[tt=Name" },
        new object[] { "a [x] [b]bold[/b] c", "a [x] |*bold| c" },
        new object[] { "[[b]x[/b]", "[|*x" },
        new object[] { "[b][x[/b]y", "*[x|y" },
        new object[] { "[foo]\nbar", "[foo]|/|bar" },
        new object[] { "[foo] \\[b] [b]x", "[foo] [b] |*x" },
        new object[] { "[color=NotAColor][b]x[/b]", "[color=NotAColor]|*x" },
        //  A tag ends at the first close tag character on its line: a value does not start with one, and does not go past a line feed
        new object[] { "[tt=]a]b", "[tt=]a]b" },
        new object[] { "[bg=]x]y", "[bg=]x]y" },
        new object[] { "[tt=a\nb]c", "[tt=a|/|b]c" },
        //  ... and holds no other open tag character
        new object[] { "[tt=a[b]x[/b]y", "[tt=a|*x|y" },
        new object[] { "[bg=[bg=Red]x", "[bg=|x" },
    };

    [Theory]
    [MemberData(nameof(TextsWithAnOpenTagCharacterThatOpensNoTag))]
    public void AnOpenTagCharacterThatOpensNoWellFormedTag_IsALiteralCharacter_AndTheReadingGoesOn(string text, string expected)
    {
        foreach (bool legacy in new[] { true, false })
        {
            FormattedTextHarness harness = new(300, legacy);
            Exception error = Record.Exception(() => harness.TextBlock.Text = text);
            Assert.Null(error);
            Assert.Equal(!legacy, harness.TextBlock.AreFormattedRunsKnown);

            List<string> parts = new();
            foreach (MGTextRun run in harness.TextBlock.Runs)
            {
                parts.Add(run switch
                {
                    MGTextRunText textRun => (textRun.Settings.IsBold ? "*" : "") + textRun.Text,
                    MGTextRunLineBreak => "/",
                    _ => run.GetType().Name
                });
            }

            Assert.Equal(expected, string.Join("|", parts));
        }
    }

    [Fact]
    public void ALongTextOfOpenTagCharactersThatOpenNoTag_IsNotReadOncePerOpenTagCharacter()
    {
        //  Each of these took seconds (or, for the scanner, a hundred milliseconds) when every '[' made the rest of the text be read again
        string[] texts =
        {
            new string('[', 20000),
            string.Concat(Enumerable.Repeat("[tt=", 5000)),
            "[é]" + string.Concat(Enumerable.Repeat("[tt=", 5000)),
            "[é]" + string.Concat(Enumerable.Repeat("[bg=", 5000)),
            string.Concat(Enumerable.Repeat("[u=1 ", 4000)),
            string.Concat(Enumerable.Repeat("[tt=a\n", 3000)) + "]",
            string.Concat(Enumerable.Repeat("[c=x", 4000)) + "]",
            string.Concat(Enumerable.Repeat("[bg=", 5000)) + "]",
            string.Concat(Enumerable.Repeat("[u=1 2 ", 2858)) + "]",
            "[é]" + string.Concat(Enumerable.Repeat("[bg=", 5000)) + "]",
            new string('[', 20000) + "]",
        };

        foreach (bool legacy in new[] { true, false })
        {
            FormattedTextHarness harness = new(300, legacy);
            harness.TextBlock.Text = "[b]warm[/b] [tt=up]";
            foreach (string text in texts)
            {
                System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
                harness.TextBlock.Text = text;
                watch.Stop();
                Output.WriteLine($"legacy={legacy} {text.Length} characters starting with \"{TextRunDescriber.Escape(text.Substring(0, 8))}\": {watch.ElapsedMilliseconds} ms");
                Assert.True(watch.ElapsedMilliseconds < 1000, $"legacy={legacy} \"{TextRunDescriber.Escape(text.Substring(0, 8))}\"...: {watch.ElapsedMilliseconds} ms");
            }
        }
    }

    [Fact]
    public void TheRuns_AreBuiltWhenRead_AndAreTheSameInstancesUntilTheTextChanges()
    {
        FormattedTextHarness harness = new(300, false);
        MGTextBlock block = harness.TextBlock;

        block.SetText("[b]one[/b] two".AsSpan());
        var first = block.Runs;
        Assert.Same(first, block.Runs);
        Assert.Equal(2, first.Count);

        block.SetText("[b]one[/b] three".AsSpan());
        var second = block.Runs;
        Assert.NotSame(first, second);
        Assert.Same(second, block.Runs);
        Assert.Equal(" three", ((MGTextRunText)second[1]).Text);

        //  A text that did not change does nothing: same runs.
        block.SetText("[b]one[/b] three".AsSpan());
        Assert.Same(second, block.Runs);
    }

    [Fact]
    public void ThePublicRuns_AreNotAffectedByMutatingTheirBrushes()
    {
        //  The scanner keeps one brush per value it has read; the runs it builds for the public hold copies, so changing one does not change what the next text gets.
        FormattedTextHarness harness = new(300, false);
        MGTextBlock block = harness.TextBlock;
        block.SetText("[bg=Red]x[/bg]".AsSpan());
        var brush = (Core.UI.Brushes.FillBrushes.MGSolidFillBrush)((MGTextRunText)block.Runs[0]).Settings.Background.Brush;
        brush.Color = Microsoft.Xna.Framework.Color.Lime;

        block.SetText("[bg=Red]y[/bg]".AsSpan());
        var next = (Core.UI.Brushes.FillBrushes.MGSolidFillBrush)((MGTextRunText)block.Runs[0]).Settings.Background.Brush;

        Assert.Equal(Microsoft.Xna.Framework.Color.Red, next.Color);
    }

    [Fact]
    public void ChangingTheText_DoesNotComputeTheLines()
    {
        foreach (bool legacy in new[] { true, false })
        {
            FormattedTextHarness harness = new(300, legacy);
            MGTextBlock block = harness.TextBlock;
            block.Text = "[b]one[/b] two";
            harness.Frame();
            var computed = block.Lines;
            Assert.NotNull(computed);

            block.SetText("[b]one[/b] three".AsSpan());

            //  The lines of the last layout stay until the next layout, as they did.
            Assert.Same(computed, block.Lines);
            block.UpdateLines();
            Assert.NotSame(computed, block.Lines);
            Assert.Equal(2, block.Lines[0].Runs.Count);
        }
    }

    public static IEnumerable<object[]> Texts() => new[]
    {
        new object[] { "[b]one[/b] two [color=Red]three[/color]" },
        new object[] { "plain text" },
        new object[] { "[tt=A]a[/tt] [action=B]b[/action] [img=Icon]" },
        new object[] { "" },
        new object[] { "[foo] literal tail" },
    };

    [Theory]
    [MemberData(nameof(Texts))]
    public void TheNotificationsOfASetText_AreTheSameOnBothPipelines(string text)
    {
        foreach (bool fromSpan in new[] { false, true })
        {
            FormattedTextHarness legacy = new(300, true);
            FormattedTextHarness scanned = new(300, false);
            legacy.TextBlock.Text = "previous";
            scanned.TextBlock.Text = "previous";
            List<string> expected = new();
            List<string> actual = new();
            legacy.TextBlock.PropertyChanged += (sender, e) => expected.Add(e.PropertyName);
            scanned.TextBlock.PropertyChanged += (sender, e) => actual.Add(e.PropertyName);

            legacy.TextBlock.Text = text;
            if (fromSpan)
            {
                scanned.TextBlock.SetText(text.AsSpan());
            }
            else
            {
                scanned.TextBlock.Text = text;
            }

            Assert.Equal(expected, actual);
            Assert.Contains(nameof(MGTextBlock.Runs), actual);
            Assert.Contains(nameof(MGTextBlock.Text), actual);
        }
    }

    [Fact]
    public void ARunsNotification_CanReadTheRuns()
    {
        FormattedTextHarness harness = new(300, false);
        List<int> counts = new();
        harness.TextBlock.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(MGTextBlock.Runs))
            {
                counts.Add(harness.TextBlock.Runs.Count);
            }
        };

        harness.TextBlock.Text = "a[b]b[/b]c";

        Assert.Equal(new[] { 3 }, counts);
    }

    [Fact]
    public void AnImageWithoutASize_TakesTheDefaultSizeOfItsTexture_AsItWasWhenTheTextWasSet()
    {
        FormattedTextHarness legacy = new(300, true);
        FormattedTextHarness scanned = new(300, false);
        legacy.TextBlock.Text = "[img=Icon] [img=Icon 5x6] [img=Icon 0x0]";
        scanned.TextBlock.Text = "[img=Icon] [img=Icon 5x6] [img=Icon 0x0]";
        //  A texture added after the text was set does not resize the image of a run that already has its size.
        scanned.Desktop.Resources.AddTexture("Late", new MGTextureData(new Graph.GraphTestImageResource("Late", 3, 4)));
        legacy.Desktop.Resources.AddTexture("Late", new MGTextureData(new Graph.GraphTestImageResource("Late", 3, 4)));

        Assert.Equal(TextRunDescriber.Describe(legacy.TextBlock.Runs), TextRunDescriber.Describe(scanned.TextBlock.Runs));
        Assert.Contains("Image[Icon|20x30", TextRunDescriber.Describe(scanned.TextBlock.Runs)[0]);
        Assert.Contains("Image[Icon|5x6", TextRunDescriber.Describe(scanned.TextBlock.Runs)[2]);
        Assert.Contains("Image[Icon|20x30", TextRunDescriber.Describe(scanned.TextBlock.Runs)[4]);

        legacy.TextBlock.Text = "[img=Late]";
        scanned.TextBlock.Text = "[img=Late]";
        Assert.Equal(TextRunDescriber.Describe(legacy.TextBlock.Runs), TextRunDescriber.Describe(scanned.TextBlock.Runs));
        Assert.Contains("Image[Late|3x4", TextRunDescriber.Describe(scanned.TextBlock.Runs)[0]);
    }

    [Fact]
    public void TheSettingsOfTheBlock_AreTheDefaultsOfTheRuns_AndAChangeOfThemRescansTheText()
    {
        FormattedTextHarness legacy = new(300, true);
        FormattedTextHarness scanned = new(300, false);
        foreach (FormattedTextHarness harness in new[] { legacy, scanned })
        {
            harness.TextBlock.Text = "plain [b]bold[/b] [u=3]ul[/u] [/u] [/shadow]";
        }

        foreach (Action<MGTextBlock> change in new Action<MGTextBlock>[]
                 {
                     b => b.IsBold = true, b => b.IsItalic = true, b => b.IsUnderlined = true, b => b.IsShadowed = true,
                     b => b.ShadowOffset = new Microsoft.Xna.Framework.Point(3, 3), b => b.ShadowColor = Microsoft.Xna.Framework.Color.Lime, b => b.IsBold = false
                 })
        {
            change(legacy.TextBlock);
            change(scanned.TextBlock);
            Assert.Equal(TextRunDescriber.Describe(legacy.TextBlock.Runs), TextRunDescriber.Describe(scanned.TextBlock.Runs));
        }
    }

    [Fact]
    public void TurningTheInlineFormattingOffAndOn_GivesTheSameRunsAsTheLegacyPipeline()
    {
        FormattedTextHarness legacy = new(300, true);
        FormattedTextHarness scanned = new(300, false);
        foreach (FormattedTextHarness harness in new[] { legacy, scanned })
        {
            harness.TextBlock.Text = "[b]x[/b] [color=Red]y[/color]";
        }

        foreach (bool allows in new[] { false, true, false, true })
        {
            legacy.TextBlock.AllowsInlineFormatting = allows;
            scanned.TextBlock.AllowsInlineFormatting = allows;
            Assert.Equal(TextRunDescriber.Describe(legacy.TextBlock.Runs), TextRunDescriber.Describe(scanned.TextBlock.Runs));
        }

        Assert.True(scanned.TextBlock.AreFormattedRunsKnown);
    }

    [Fact]
    public void ExplicitRuns_KeepTheLegacyPath()
    {
        FormattedTextHarness harness = new(300, false);
        harness.TextBlock.SetTextRuns(new MGTextRun[] { new MGTextRunText("explicit", new MGTextRunConfig(true), null, null) });

        Assert.False(harness.TextBlock.AreFormattedRunsKnown);
        Assert.Equal("explicit", ((MGTextRunText)harness.TextBlock.Runs[0]).Text);

        harness.TextBlock.ClearTextRuns();
        harness.TextBlock.Text = "[b]x[/b]";
        Assert.True(harness.TextBlock.AreFormattedRunsKnown);
    }
}
