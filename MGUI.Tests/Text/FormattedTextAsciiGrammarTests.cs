using System.Globalization;
using System.Text;
using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using Xunit.Abstractions;

namespace MGUI.Tests.Text;

/// <summary>
/// The grammar of tags is ASCII (MGUI ADR-0033): a tag type and a number of a tag are made of ASCII letters and ASCII digits, in the legacy pipeline and in the allocation-free scanner, whatever the
/// culture of the process is. A <c>[</c> that opens no well formed tag is a literal character (ADR-0030), so a number written with other digits, or a type written with other letters, is text. What is
/// not a type or a number (the names of tooltips, actions and images, the name of a color, the text itself) keeps every character it had.
/// </summary>
public class FormattedTextAsciiGrammarTests
{
    private readonly ITestOutputHelper Output;

    public FormattedTextAsciiGrammarTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private static readonly string[] Cultures = { "en-US", "tr-TR", "fr-FR", "" };

    private static void UnderCulture(string name, Action action)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(name);
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static string Describe(MGTextBlock block)
    {
        List<string> parts = new();
        foreach (MGTextRun run in block.Runs)
        {
            parts.Add(run switch
            {
                MGTextRunText text => (text.Settings.IsBold ? "*" : "") + (text.Settings.IsItalic ? "/i/" : "") + text.Text,
                MGTextRunLineBreak => "<br>",
                MGTextRunImage image => $"<img {image.SourceName} {image.TargetWidth}x{image.TargetHeight}>",
                _ => run.GetType().Name
            });
        }

        return string.Join("|", parts);
    }

    /// <summary>What the runs are, written as the texts of the runs, "|" between two runs, "*" before a bold one, "/i/" before an italic one.</summary>
    public static IEnumerable<object[]> NotTags() => new[]
    {
        //  A number written with digits that are not ASCII: the value is not one of its type, so the tag is text. (Before ADR-0033 the digits matched \d and the interpretation of the value failed, with the same result.)
        new object[] { "[o=٠.٥]x[/o]", "[o=٠.٥]x" },
        new object[] { "[o=０.５]x[/o]y", "[o=０.５]x|y" },
        new object[] { "[u=٢]x[/u]", "[u=٢]x" },
        new object[] { "[shadow=Red ١ ٢]x[/shadow]", "[shadow=Red ١ ٢]x" },
        new object[] { "[bg=Red ٢]x[/bg]", "[bg=Red ٢]x" },
        new object[] { "[color=RGB(١,2,3)]x[/color]", "[color=RGB(١,2,3)]x" },
        new object[] { "[color=Red * ٠.٥]x[/color]", "[color=Red * ٠.٥]x" },
        //  A type written with a letter that is not ASCII: not a type, so the tag is text. The Kelvin sign is not a k, the Turkish dotted and dotless i are not an i, the full width letters are not letters of a type.
        new object[] { "[İtalic]x[/italic]", "[İtalic]x" },
        new object[] { "[ı]x[/i]", "[ı]x" },
        new object[] { "[ｂ]x[/ｂ]", "[ｂ]x[/ｂ]" },
        new object[] { "[ｂold]x[/bold]", "[ｂold]x" },
        new object[] { "[color=Red]x[/Ｃolor]", "x[/Ｃolor]" },
        new object[] { "[İmg=Icon]", "[İmg=Icon]" },
        //  A tag whose type is spelled in ASCII, whatever the case, under any culture: still a tag
        new object[] { "[ITALIC]x[/ITALIC]y", "/i/x|y" },
        new object[] { "[I]x[/I]y", "/i/x|y" },
        new object[] { "[Bold]x[/BOLD]y", "*x|y" },
    };

    [Theory]
    [MemberData(nameof(NotTags))]
    public void ATypeOrANumberThatIsNotASCII_MakesNoTag_OnBothPipelines_UnderEveryCulture(string text, string expected)
    {
        foreach (string culture in Cultures)
        {
            UnderCulture(culture, () =>
            {
                foreach (bool legacy in new[] { true, false })
                {
                    FormattedTextHarness harness = new(300, legacy);
                    harness.TextBlock.Text = text;
                    Assert.Equal(!legacy, harness.TextBlock.AreFormattedRunsKnown);
                    Assert.True(expected == Describe(harness.TextBlock), $"culture \"{culture}\" legacy={legacy} text \"{TextRunDescriber.Escape(text)}\": expected \"{TextRunDescriber.Escape(expected)}\", got \"{TextRunDescriber.Escape(Describe(harness.TextBlock))}\"");
                }
            });
        }
    }

    [Fact]
    public void TheKelvinSign_IsNoLongerAKInATagType()
    {
        //  Before ADR-0033 the case insensitive pattern of the type matched U+212A as a k: this was a background tag. It is text now, like any other letter that is not an ASCII one.
        foreach (bool legacy in new[] { true, false })
        {
            FormattedTextHarness harness = new(300, legacy);

            harness.TextBlock.Text = "[bacKground=Red]x";
            Assert.Equal("[bacKground=Red]x", Describe(harness.TextBlock));
            Assert.False(((MGTextRunText)harness.TextBlock.Runs[0]).Settings.HasBackground);

            harness.TextBlock.Text = "[background=Red]x";
            Assert.True(((MGTextRunText)harness.TextBlock.Runs[0]).Settings.HasBackground);
        }
    }

    [Fact]
    public void AnImageWithASizeWrittenWithOtherDigits_IsAnImageWhoseNameHoldsThem()
    {
        //  The size is [0-9]{1,4}: the characters that follow the name are not a size, so they are part of the name (the name is any text), and the image has no size of its own, so it takes the one of its
        //  texture (none for this name). Before ADR-0033 the digits matched \d, the size failed to be read, and the tag was text.
        foreach (bool legacy in new[] { true, false })
        {
            FormattedTextHarness harness = new(300, legacy);

            harness.TextBlock.Text = "a[img=Icon ١٦x١٦]b";

            Assert.Equal("a|<img Icon ١٦x١٦ 0x0>|b", Describe(harness.TextBlock));
        }
    }

    [Fact]
    public void TheNamesAndTheText_KeepEveryCharacter()
    {
        foreach (bool legacy in new[] { true, false })
        {
            FormattedTextHarness harness = new(300, legacy);

            harness.TextBlock.Text = "[tt=Été 日本]a[/tt] [action=Kİı١]b[/action] [img=Icôn 日 4x5][tt=١]c";
            List<MGTextRun> runs = harness.TextBlock.Runs.ToList();

            Assert.Equal(6, runs.Count);
            Assert.Equal("a", ((MGTextRunText)runs[0]).Text);
            Assert.Equal("Été 日本", ((MGTextRunText)runs[0]).ToolTipId);
            Assert.Equal(" ", ((MGTextRunText)runs[1]).Text);
            Assert.Equal("b", ((MGTextRunText)runs[2]).Text);
            Assert.Equal("Kİı١", ((MGTextRunText)runs[2]).ActionId);
            Assert.Equal(" ", ((MGTextRunText)runs[3]).Text);
            MGTextRunImage image = Assert.IsType<MGTextRunImage>(runs[4]);
            Assert.Equal("Icôn 日", image.SourceName);
            Assert.Equal(4, image.TargetWidth);
            Assert.Equal(5, image.TargetHeight);
            Assert.Equal("c", ((MGTextRunText)runs[5]).Text);
            Assert.Equal("١", ((MGTextRunText)runs[5]).ToolTipId);

            //  The text next to a '[' with other characters than a tag type
            harness.TextBlock.Text = "١٢ [é] [١]x [K]y [b]İ[/b] ＡＢ [";
            Assert.Equal("١٢ [é] [١]x [K]y |*İ| ＡＢ [", Describe(harness.TextBlock));
        }
    }

    [Fact]
    public void AColorNameThatIsNotASCII_IsStillNotAColor()
    {
        //  The name of a color is read as ASCII letters (it was already: [a-zA-Z]+), so a name with another letter ends where the letters end and the tag is text.
        foreach (bool legacy in new[] { true, false })
        {
            FormattedTextHarness harness = new(300, legacy);

            harness.TextBlock.Text = "[color=Rèd]x[/color]";
            Assert.Equal("[color=Rèd]x", Describe(harness.TextBlock));
        }
    }

    // ---- the randomised differential ----

    private static readonly string[] Types =
    {
        "b", "bold", "i", "italic", "o", "opacity", "u", "underline", "c", "color", "fg", "foreground", "bg", "background", "s", "shadow", "img", "image", "tt", "tooltip", "action", "command",
    };

    private static readonly string[] Values =
    {
        "0.5", ".25", "0.75", "2", "2 -1", "3 1 Red", "2 -1 Red|Blue", "Red", "Blue * 0.5", "#00FF00", "RGB(1,2,3)", "rgba(10, 20, 30, 40)", "Red 1 2", "Red -1,2", "Red 12 34", "Blue 1,2,3,4", "Red 2",
        "Icon", "Icon 16x16", "Icon 8,12", "Icon 5 6", "Name", "Two words", "NotAColor", "99999999999", "",
    };

    /// <summary>Characters that are not ASCII and look like, or fold to, an ASCII letter, or are digits of other scripts.</summary>
    private static readonly char[] LookAlikeLetters = { 'K', 'İ', 'ı', 'ſ', 'ｂ', 'ｃ', 'ｉ', 'ｋ', 'ｕ', 'Ｃ', 'Ｓ', 'é', 'ß', 'Α', '日' };
    private static readonly char[] OtherDigits = { '٠', '٥', '۲', '०', '১', '０', '５', '９', '๑' };

    private static string Corrupt(string value, Random random, int oneIn)
    {
        StringBuilder result = new();
        foreach (char c in value)
        {
            if (c >= '0' && c <= '9' && random.Next(oneIn) == 0)
            {
                result.Append(OtherDigits[random.Next(OtherDigits.Length)]);
            }
            else if (((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) && random.Next(oneIn * 3) == 0)
            {
                result.Append(LookAlikeLetters[random.Next(LookAlikeLetters.Length)]);
            }
            else if (c >= 'a' && c <= 'z' && random.Next(oneIn * 2) == 0)
            {
                result.Append(char.ToUpperInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    private static readonly string[] Pieces = { " ", " ", "  ", "-", "\n", "\r\n", "\t", "\\[", "\\\\", "\\", "]", "[", "=", "/", ",", "*", ".", "#", "|", "x", "X", "1", "0.5", "Icon", "word", "é", "日本", "١٢", "K", "İ", "ı" };

    private static string RandomText(Random random)
    {
        StringBuilder text = new();
        int count = random.Next(1, 24);
        for (int i = 0; i < count; i++)
        {
            int choice = random.Next(10);
            if (choice < 4)
            {
                string type = Types[random.Next(Types.Length)];
                bool hasValue = type is "o" or "opacity" or "u" or "underline" or "c" or "color" or "fg" or "foreground" or "bg" or "background" or "s" or "shadow" or "img" or "image" or "tt" or "tooltip" or "action" or "command" && random.Next(5) != 0;
                string value = Values[random.Next(Values.Length)];
                string body = Corrupt(type, random, 4) + (hasValue ? "=" + Corrupt(value, random, 3) : "");
                text.Append('[').Append(random.Next(6) == 0 ? (random.Next(2) == 0 ? "/" : "\\") : "").Append(body).Append(random.Next(25) == 0 ? "" : "]");
            }
            else if (choice < 6)
            {
                text.Append("[/").Append(Corrupt(Types[random.Next(Types.Length)], random, 4)).Append(']');
            }
            else if (choice < 8)
            {
                text.Append(Pieces[random.Next(Pieces.Length)]);
            }
            else
            {
                int length = random.Next(1, 7);
                for (int k = 0; k < length; k++)
                {
                    text.Append(random.Next(5) == 0 ? LookAlikeLetters[random.Next(LookAlikeLetters.Length)] : (char)('a' + random.Next(26)));
                }
            }
        }

        return text.ToString();
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public void ARandomText_HasTheRunsOfTheLegacyPipeline_AndIsAlwaysReadByTheScanner(string culture)
    {
        const int Count = 20000;
        UnderCulture(culture, () =>
        {
            FormattedTextHarness legacy = new(300, true);
            FormattedTextHarness scanned = new(300, false);
            Random random = new(20261116);
            int withNonAscii = 0;
            int withTags = 0;

            for (int n = 0; n < Count; n++)
            {
                string text = RandomText(random);
                legacy.TextBlock.Text = "previous";
                scanned.TextBlock.Text = "previous";
                Exception legacyError = Record.Exception(() => legacy.TextBlock.Text = text);
                Exception scannedError = Record.Exception(() => scanned.TextBlock.Text = text);
                string context = $"culture \"{culture}\" text #{n} \"{TextRunDescriber.Escape(text)}\"";

                Assert.True(legacyError == null, $"{context}: the legacy pipeline threw {legacyError}");
                Assert.True(scannedError == null, $"{context}: the scanner threw {scannedError}");
                Assert.True(scanned.TextBlock.AreFormattedRunsKnown, $"{context}: the scanner declined the text");

                List<string> expected = TextRunDescriber.Describe(legacy.TextBlock.Runs);
                List<string> actual = TextRunDescriber.Describe(scanned.TextBlock.Runs);
                Assert.True(expected.SequenceEqual(actual), $"{context}:\nexpected {string.Join("\n         ", expected)}\nactual   {string.Join("\n         ", actual)}");

                if (text.Any(x => x > 127))
                {
                    withNonAscii++;
                }

                if (expected.Count > 1 || legacy.TextBlock.Runs.OfType<MGTextRunText>().Any(x => x.Settings.IsBold || x.Settings.IsItalic || x.Settings.Underline.IsEnabled || x.Settings.HasBackground || x.Settings.IsShadowed || x.ToolTipId != null || x.ActionId != null))
                {
                    withTags++;
                }
            }

            Output.WriteLine($"{culture}: {Count} random texts, {withNonAscii} with a character that is not ASCII, {withTags} that hold at least one tag or a line break");
            Assert.True(withNonAscii > Count / 2, $"{withNonAscii} with a non ASCII character");
            Assert.True(withTags > Count / 4, $"{withTags} with a tag");
        });
    }
}
