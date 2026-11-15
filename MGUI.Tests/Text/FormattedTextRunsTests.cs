using System.ComponentModel;
using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using Xunit.Abstractions;

namespace MGUI.Tests.Text;

/// <summary>
/// The runs of a formatted text read by the allocation-free scanner (<see cref="FormattedTextScanner"/>) against the ones of the legacy pipeline (<see cref="FTTokenizer"/>, <see cref="FTParser"/>,
/// <see cref="MGTextRun.ParseRuns(string, MGTextRunConfig)"/> and the sanitizing of <see cref="MGTextBlock"/>), reached through <see cref="MGTextBlock.ForceLegacyTextPath"/>.<para/>
/// The public <see cref="MGTextBlock.Runs"/> of a block on the new path are built from what the scanner read (they are not parsed again), so comparing them with the legacy ones compares the scanner
/// with the legacy code. <see cref="MGTextBlock.AreFormattedRunsKnown"/> says which path read a text: the tests check that the scanner reads everything the legacy code reads, and declines only a text the
/// legacy code throws on or a text with a non ASCII character (the grammar compares letters and digits by Unicode rules the scanner does not reproduce).
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
                    declined++;
                    Assert.True(text.Any(x => x > 127), $"{context}: the scanner declined a text with only ASCII characters that the legacy code reads");
                }
            }
        }

        Output.WriteLine($"{corpus.Count} texts (x2): {read} read by the scanner, {threw} that the legacy code throws on, {declined} declined without a throw");
        Assert.True(corpus.Count >= 500);
        Assert.True(read >= 700, $"{read} read");
        Assert.True(threw >= 100, $"{threw} threw");
        Assert.True(declined >= 4, $"{declined} declined");
    }

    [Fact]
    public void ATextThatThrows_LeavesTheTextBlockAsTheLegacyCodeLeavesIt()
    {
        foreach (string text in new[] { "[bold", "abc[", "[color=NotAColor]x", "[o=1.5]x", "[u=99999999999]x[/u]" })
        {
            FormattedTextHarness legacy = new(300, true);
            FormattedTextHarness scanned = new(300, false);
            legacy.TextBlock.Text = "[color=Red]before[/color]";
            scanned.TextBlock.Text = "[color=Red]before[/color]";
            Assert.True(scanned.TextBlock.AreFormattedRunsKnown);

            Exception expected = Record.Exception(() => legacy.TextBlock.Text = text);
            Exception actual = Record.Exception(() => scanned.TextBlock.Text = text);

            Assert.NotNull(expected);
            Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(text, scanned.TextBlock.Text);
            Assert.Equal(TextRunDescriber.Describe(legacy.TextBlock.Runs), TextRunDescriber.Describe(scanned.TextBlock.Runs));
            //  The runs of the text before are still the runs, whatever scanned them.
            Assert.Contains(TextRunDescriber.Describe(scanned.TextBlock.Runs), x => x.Contains("before"));
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
