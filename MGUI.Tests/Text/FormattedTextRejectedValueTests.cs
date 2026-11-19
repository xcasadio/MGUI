using System.Globalization;
using System.Runtime.ExceptionServices;
using MGUI.Core.UI;
using Xunit.Abstractions;

namespace MGUI.Tests.Text;

/// <summary>
/// A tag value that its interpretation rejects (a color that does not exist, a number that does not fit, a brush that is not one) is not a tag: the '[' is a literal character and the reading
/// goes on right after it (MGUI ADR-0030). The scanner (<see cref="FormattedTextScanner"/>) keeps such a value as rejected in the cache of its values, with the values it accepts, so the interpretation
/// throws once per value: a text that holds one, changed at every frame, allocates nothing and throws nothing once its values were read. The runs stay the runs of the legacy pipeline.
/// </summary>
public class FormattedTextRejectedValueTests
{
    private const int WarmupIterations = 1000;
    private const int MeasuredIterations = 500;

    private readonly ITestOutputHelper Output;

    public FormattedTextRejectedValueTests(ITestOutputHelper output)
    {
        Output = output;
    }

    /// <summary>One text per kind of value that is kept (a color, a shadow, an underline, a background), whose value the interpretation rejects, then the same text with other characters.
    /// The images, the tooltips and the actions have no rejection to keep: their values are never rejected.</summary>
    public static IEnumerable<object[]> RejectedCases() => new[]
    {
        new object[] { "color name", "[color=NotAColor]first[/color] tail", "[color=NotAColor]second[/color] tail!" },
        new object[] { "color rgb out of range", "[fg=RGB(300,1,2)]first[/fg] tail", "[fg=RGB(300,1,2)]second[/fg] tail!" },
        new object[] { "shadow name", "[shadow=NotAColor]first[/shadow] tail", "[shadow=NotAColor]second[/shadow] tail!" },
        new object[] { "shadow color with a factor", "[s=Red * 0.5]first[/s] tail", "[s=Red * 0.5]second[/s] tail!" },
        new object[] { "underline height that does not fit", "[u=99999999999]first[/u] tail", "[u=99999999999]second[/u] tail!" },
        new object[] { "underline offset that does not fit", "[u=2 99999999999]first[/u] tail", "[u=2 99999999999]second[/u] tail!" },
        new object[] { "underline brush", "[u=2 -1 NotAColor]first[/u] tail", "[u=2 -1 NotAColor]second[/u] tail!" },
        new object[] { "background name", "[bg=NotAColor]first[/bg] tail", "[bg=NotAColor]second[/bg] tail!" },
        new object[] { "background with three colors", "[bg=Red|Blue|Green]first[/bg] tail", "[bg=Red|Blue|Green]second[/bg] tail!" },
        new object[] { "all of them, with accepted values among them", "[color=NotAColor]a[/color][color=Red]b[/color][u=99999999999]c[/u][u=2 -1 Red]d[/u][bg=NotAColor]e[/bg][bg=Blue 1,1]f[/bg][shadow=NotAColor]g[/shadow][shadow=Red 1 2]h[/shadow]",
                       "[color=NotAColor]a![/color][color=Red]b[/color][u=99999999999]c[/u][u=2 -1 Red]d[/u][bg=NotAColor]e[/bg][bg=Blue 1,1]f[/bg][shadow=NotAColor]g[/shadow][shadow=Red 1 2]h[/shadow]" },
    };

    private static int CountFirstChanceExceptions(Action action)
    {
        int threadId = Environment.CurrentManagedThreadId;
        int exceptions = 0;
        void OnFirstChance(object sender, FirstChanceExceptionEventArgs e)
        {
            if (Environment.CurrentManagedThreadId == threadId)
            {
                exceptions++;
            }
        }

        AppDomain.CurrentDomain.FirstChanceException += OnFirstChance;
        try
        {
            action();
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= OnFirstChance;
        }

        return exceptions;
    }

    [Theory]
    [MemberData(nameof(RejectedCases))]
    public void ATextWithARejectedValue_ChangedEveryFrame_AllocatesNothing_AndThrowsNothing(string name, string firstText, string secondText)
    {
        FormattedTextHarness harness = new(300, false);
        MGTextBlock textBlock = harness.TextBlock;
        char[] first = firstText.ToCharArray();
        char[] second = secondText.ToCharArray();
        int index = 0;

        //  The first reads: each value is interpreted (and rejected) once.
        for (int i = 0; i < WarmupIterations; i++)
        {
            textBlock.SetText((index++ & 1) == 0 ? first : second);
        }

        Assert.True(textBlock.AreFormattedRunsKnown, name);

        long allocated = 0;
        int exceptions = CountFirstChanceExceptions(() =>
        {
            long before = AllocationWindow.Start();
            for (int i = 0; i < MeasuredIterations; i++)
            {
                textBlock.SetText((index++ & 1) == 0 ? first : second);
            }

            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        });

        Assert.Equal(0, allocated);
        Assert.Equal(0, exceptions);
        Assert.True(textBlock.AreFormattedRunsKnown, name);
    }

    [Theory]
    [MemberData(nameof(RejectedCases))]
    public void ATextWithARejectedValue_ThrowsOnceForTheValue_NotAtEveryRead(string name, string firstText, string secondText)
    {
        //  Counter-measure of the test above: the first read of the value is what throws (the test above would pass for a scanner that never sees the exception if this one did not see it).
        FormattedTextHarness harness = new(300, false);

        int exceptions = CountFirstChanceExceptions(() => harness.TextBlock.Text = firstText);
        int again = CountFirstChanceExceptions(() =>
        {
            for (int i = 0; i < 10; i++)
            {
                harness.TextBlock.Text = i % 2 == 0 ? secondText : firstText;
            }
        });

        Assert.True(exceptions >= 1, $"{name}: the first read threw {exceptions} exceptions");
        Assert.Equal(0, again);
    }

    private static (List<string> Expected, List<string> Actual) ReadBoth(FormattedTextHarness legacy, FormattedTextHarness scanned, string text)
    {
        legacy.TextBlock.Text = text;
        scanned.TextBlock.Text = text;
        Assert.True(scanned.TextBlock.AreFormattedRunsKnown, text);
        return (TextRunDescriber.Describe(legacy.TextBlock.Runs), TextRunDescriber.Describe(scanned.TextBlock.Runs));
    }

    [Theory]
    [MemberData(nameof(RejectedCases))]
    public void ATextWithARejectedValue_HasTheRunsOfTheLegacyPipeline_EveryTimeItIsRead(string name, string firstText, string secondText)
    {
        FormattedTextHarness legacy = new(300, true);
        FormattedTextHarness scanned = new(300, false);

        //  The first read (the interpretation throws) and the following ones (the rejection is in the cache) give the same runs, which are the legacy ones.
        for (int i = 0; i < 6; i++)
        {
            string text = i % 2 == 0 ? firstText : secondText;
            (List<string> expected, List<string> actual) = ReadBoth(legacy, scanned, text);
            Assert.True(expected.SequenceEqual(actual), $"{name}, read {i}:\nexpected {string.Join("\n         ", expected)}\nactual   {string.Join("\n         ", actual)}");
        }
    }

    [Fact]
    public void AValue_RejectedForOneTagType_IsAcceptedForAnother()
    {
        //  "Red * 0.5" is a color with a factor, which a color accepts and a shadow rejects (it hands the whole text to the converter of the system, which does not know the factor): the rejection
        //  of one tag type is not the one of the other, whatever the order of the reads.
        string rejectedThenAccepted = "[shadow=Red * 0.5]a[/shadow][color=Red * 0.5]b[/color]";
        string acceptedThenRejected = "[color=Red * 0.5]a[/color][shadow=Red * 0.5]b[/shadow]";
        FormattedTextHarness legacy = new(300, true);
        FormattedTextHarness scanned = new(300, false);

        for (int i = 0; i < 6; i++)
        {
            (List<string> expected, List<string> actual) = ReadBoth(legacy, scanned, i % 2 == 0 ? rejectedThenAccepted : acceptedThenRejected);
            Assert.True(expected.SequenceEqual(actual), $"read {i}:\nexpected {string.Join("\n         ", expected)}\nactual   {string.Join("\n         ", actual)}");
        }

        //  The runs say it: the shadow tag is a literal, the color tag is a tag.
        scanned.TextBlock.Text = rejectedThenAccepted;
        Assert.Contains(scanned.TextBlock.Runs.OfType<MGUI.Core.UI.Text.MGTextRunText>(), x => x.Text.StartsWith("[shadow=Red * 0.5]a"));
        Assert.DoesNotContain(scanned.TextBlock.Runs.OfType<MGUI.Core.UI.Text.MGTextRunText>(), x => x.Text.Contains("[color="));

        long allocated = 0;
        int index = 0;
        string[] texts = { rejectedThenAccepted, acceptedThenRejected };
        for (int i = 0; i < WarmupIterations; i++)
        {
            scanned.TextBlock.Text = texts[index++ & 1];
        }

        int exceptions = CountFirstChanceExceptions(() =>
        {
            long before = AllocationWindow.Start();
            for (int i = 0; i < MeasuredIterations; i++)
            {
                scanned.TextBlock.Text = texts[index++ & 1];
            }

            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        });

        Assert.Equal(0, allocated);
        Assert.Equal(0, exceptions);
    }

    /// <summary>A name of letters only (a color name is read as letters), distinct for each index.</summary>
    private static string LetterName(int index)
    {
        char[] name = new char[4];
        for (int i = 0; i < name.Length; i++)
        {
            name[i] = (char)('A' + index % 26);
            index /= 26;
        }

        return new string(name);
    }

    [Fact]
    public void MoreThanTheCacheLimit_OfRejectedValues_StillGivesTheRunsOfTheLegacyPipeline()
    {
        //  The cache of a kind holds 128 values, accepted or rejected, and is emptied when it takes one more: 400 distinct rejected values go through it several times, with accepted values
        //  among them (which the emptying must not corrupt, and which must not corrupt a rejection). Every text is read twice, so a value read again after the emptying is interpreted again.
        FormattedTextHarness legacy = new(300, true);
        FormattedTextHarness scanned = new(300, false);
        int compared = 0;

        for (int pass = 0; pass < 3; pass++)
        {
            for (int i = 0; i < 400; i++)
            {
                string name = LetterName(i);
                string[] texts =
                {
                    $"[color={name}]rejected {i}[/color][color=Red]accepted[/color]",
                    $"[shadow={name}]rejected {i}[/shadow][shadow=Red 1 2]accepted[/shadow]",
                    $"[u=99999999{i + 1000}]rejected {i}[/u][u=2 -1 Red]accepted[/u]",
                    $"[bg={name}]rejected {i}[/bg][bg=Blue 1,1]accepted[/bg]",
                };

                foreach (string text in texts)
                {
                    for (int read = 0; read < 2; read++)
                    {
                        (List<string> expected, List<string> actual) = ReadBoth(legacy, scanned, text);
                        Assert.True(expected.SequenceEqual(actual), $"pass {pass}, read {read}, text \"{text}\":\nexpected {string.Join("\n         ", expected)}\nactual   {string.Join("\n         ", actual)}");
                        compared++;
                    }
                }
            }
        }

        Assert.Equal(3 * 400 * 4 * 2, compared);
        Output.WriteLine($"{compared} reads of texts with a rejected value, over 3 passes of 400 distinct values for each of 4 kinds");
    }

    [Fact]
    public void ARejection_IsNotKeptAcrossACultureChange()
    {
        //  The converter of colors can read the current culture (the separator of a list of numbers), so a rejection is only kept for the culture it was made under; read under another
        //  one, the value is interpreted again, like the legacy pipeline does. On the platform of the tests no value is read differently by en-US and fr-FR (the output shows it): this test
        //  checks that the scanner keeps agreeing with the legacy pipeline across culture changes, it cannot show a difference of outcome.
        string text = "[bg=1,2,3]a[/bg][u=1 1 1;2;3]b[/u][bg=4;5;6]c[/bg]";
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo[] cultures = { new CultureInfo("en-US"), new CultureInfo("fr-FR"), new CultureInfo("en-US"), new CultureInfo("fr-FR") };
            FormattedTextHarness legacy = new(300, true);
            FormattedTextHarness scanned = new(300, false);
            List<string> seen = new();

            foreach (CultureInfo culture in cultures)
            {
                CultureInfo.CurrentCulture = culture;
                for (int read = 0; read < 3; read++)
                {
                    (List<string> expected, List<string> actual) = ReadBoth(legacy, scanned, text);
                    Assert.True(expected.SequenceEqual(actual), $"{culture.Name}, read {read}:\nexpected {string.Join("\n         ", expected)}\nactual   {string.Join("\n         ", actual)}");
                    if (read == 0)
                    {
                        seen.Add($"{culture.Name}: {string.Join(" ## ", expected).Replace("bold=False italic=False opacity=1 fg=null ", "")}");
                    }
                }
            }

            Output.WriteLine(string.Join("\n", seen));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
