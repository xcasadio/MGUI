using System.Text;

namespace MGUI.Tests.Text;

/// <summary>
/// The texts the formatted text tests run through the legacy pipeline (tokenizer, parser, runs, lines) and through the allocation-free one: every tag the language has, alone and
/// nested, markup that is not closed or not well formed, escapes, line breaks, and a few hundred generated texts that mix valid and broken markup, from a fixed seed.
/// </summary>
internal static class FormattedTextCorpus
{
    /// <summary>The name of a texture the tests register with a size (20x30).</summary>
    public const string Icon = "Icon";

    public static readonly string[] Fixed =
    {
        //  Plain
        "", " ", "word", "two words", "multiple   spaces   between", "a-b-c-d hyphen-ated", "tab\there", "\t", "Value: 0.42",
        //  Every tag, alone
        "[b]bold[/b] text", "[bold]bold[/bold] text", "[BOLD]bold[/Bold] text", "[B]b[/B]", "[i]italic[/i] text", "[italic]x[/italic]",
        "[o=0.5]faint[/o] text", "[opacity=.25]faint[/opacity]", "[opacity=0.99]faint[/opacity]", "[o=.5]x[/o]",
        "[u]under[/u] line", "[underline]x[/underline]", "[u=2]x[/u]", "[u=2 -1]x[/u]", "[u=3 1 Red]x[/u]", "[u=2 -1 Dark Blue]x[/u]", "[underline=4 0 Red|Blue]x[/underline]", "[u=]x[/u]",
        "[color=Red]red[/color] text", "[c=Green]green[/c]", "[fg=#00FF00]x[/fg]", "[foreground=#00FF0080]x[/foreground]", "[color=RGB(1,2,3)]x[/color]", "[color=rgba(1, 2, 3, 4)]x[/color]",
        "[color=Red*0.5]x[/color]", "[color=Red * 2]x[/color]", "[color=#FF0000 * 0.5]x[/color]", "[COLOR=blue]x[/COLOR]", "[color=Transparent]x[/color]",
        "[bg=Red]x[/bg] y", "[background=Red 1,2,3,4]x[/background]", "[bg=Red 2]x[/bg]", "[bg=Red|Blue]x[/bg]", "[bg=Red|Blue|Green|Yellow]x[/bg]", "[bg=Blue -2,4]word[/bg]",
        "[shadow=Black]x[/shadow] y", "[s=Red 1 2]x[/s]", "[shadow=Red 1,2]x[/shadow]", "[shadow=Red -1 -2]x[/shadow]", "[shadow=#00000080 2 2]x[/shadow]",
        "[img=Icon]", "[img=Icon 5x0]", "[img=Icon 0x7]", "[img=Icon 16x16]", "[image=Icon 8,12]", "[img=Icon 0x0]", "[img=Missing]", "[img=Icon 5 6]x", "text [img=Icon 16x16] text", "[IMG=Icon 7X9]",
        "[tt=Info]tip[/tt] text", "[tooltip=Name with spaces]x[/tooltip]", "[TT=Upper]x[/TT]", "[action=Click]act[/action]", "[command=Go]x[/command]", "[ACTION=Up]x[/ACTION]",
        //  Close tag variants
        "[b]x[\\b]y", "[\\i]x", "[/c]x", "[\\color]x",
        //  Nested
        "[b][i]both[/i][/b]", "[b]a[i]b[/b]c[/i]d", "[color=Red]r[color=Blue]b[/color]r[/color]", "[tt=A]a[tt=B]b[/tt]a[/tt]", "[action=A]a[action=B]b[/action]a[/action]",
        "[tt=A][action=X]both[/action][/tt]", "[u]a[u=3]b[/u]c[/u]", "[o=0.5]a[o=0.25]b[/o]c[/o]", "[bg=Red]a[bg=Blue]b[/bg]c[/bg]", "[shadow=Red]a[shadow=Blue 3 3]b[/shadow]c[/shadow]",
        "[b][i][u][color=Red][o=0.5][bg=Blue][shadow=Black]all[/shadow][/bg][/o][/color][/u][/i][/b]", "[tt=A]a [img=Icon 16x16] b[/tt]", "[action=A]a [img=Icon] b[/action]",
        //  Not closed
        "[b]not closed", "[color=Red]not closed [b]more", "[tt=A]x", "[action=A]x", "[u]x", "[o=0.5]x", "[bg=Red]x", "[shadow=Red]x",
        //  Closed without being opened
        "[/b]x", "[/color]x", "x[/tt]y", "[/o]", "[/u]", "[/bg]", "[/shadow]", "[/action]", "[/i]", "[/b][/b][/b]", "[/tt][/tt]x", "[/action]a[/action]",
        //  Escapes
        "\\[b]not bold", "\\\\[b]bold with a backslash", "a\\\\\\[b]", "\\\\\\\\[b]x[/b]", "back\\slash", "ends with backslash\\", "\\\\", "\\\\x", "\\[", "\\\\\\", "a\\\\\\\\b", "\\\\\\\\\\[b]", "x\\[b]y[/b]z",
        "[\\[b]", "\\\\\\\\\\\\[color=Red]x", "\\n not a line break",
        //  Not a tag: the text from the '[' on is a literal
        "[foo]", "[]x", "[ b]x", "[1]", "Array[0] = 1", "[b=1]x[/b]", "[img]x", "[/img]x", "[/image=x]", "[bold]x[b ]y", "[[b]]x", "a [b]x[/b] [foo] y [b]z[/b]", "a\n[x\ny[b]z",
        "[foo]a\nb", "x [oops\r\ny", "[u=x]", "[color]", "[ ]", "[\n]", "[b\n]", "tail [bogus] with [b]markup[/b] and\nline breaks",
        //  Not well formed: the legacy parser throws
        "[b ]x", "[bold", "abc[", "[", "[b][", "[tt=]x", "[tt]x", "[color]x[/color]", "[color=]x", "[color=NotAColor]x", "[color=#12345]x", "[o=1.5]x[/o]", "[o=0.555]x[/o]", "[o]x", "[u=2x]y",
        "[shadow=Red 123 4]x", "[shadow]x", "[bg]x", "[bg=]x", "[img=]x", "[b]x[/b ]", "[/b x]", "[tt=Name", "[color=Red", "[u=2", "[u=2 -1 Blue", "[o=0.5", "[color=RGB(1,2)]x", "[shadow=Red*0.5 1 2]x", "[bg=NotAColor]x",
        "[u=99999999999]x[/u]", "[bg=Red 99999999999]x[/bg]", "[color=Red]x[/color", "[underline x]", "[i]x[/italic",
        //  Empty tags
        "[b][/b]", "[b][/b]x", "[color=Red][/color]", "[tt=A][/tt]", "[action=A][/action]x", "x[b][/b]y",
        //  Adjacent tags
        "[b][i][u]x", "x[b][b][/b][/b]", "[b]a[/b][b]b[/b]", "[color=Red]a[/color][color=Blue]b[/color]", "[b]a[/b]b[i]c[/i]d",
        //  Across line breaks
        "[b]line1\nline2[/b]\nline3", "[color=Red]a\r\nb[/color]", "[tt=A]x\n[/tt]y", "a\n\n[b]\nb", "\n", "\r\n", "\r", "a\n", "\na", "a\rb", "a\r\n\r\nb", "[b]\n[/b]", "\n[b]x[/b]\n", "a \n b",
        //  Tabs
        "a\tb", "[b]\t[/b]", "[tt=a\tb]x[/tt]", "\t[b]x[/b]\t",
        //  Text that wraps
        "The quick brown fox jumps over the lazy dog while the sleepy cat watches.",
        "Value: [b]0.42[/b] and [color=Red]red text[/color] here",
        "[b]aaaa[/b]bbbb[i]cccc[/i]dddd", "Hello[b]World[/b]Hello World", "a[b]b[/b]c d[i]e[/i]f-g[b]h[/b]-i", "[b]x[/b][i]y[/i][u]z[/u]",
        "word [b]supercalifragilisticexpialidocious_overlong_word_that_is_wider_than_any_reasonable_line[/b] tail",
        "aaaa[b]bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb[/b]cccc dd", "[b]aaaaaaaaaaaaaaaaaaaaaaaa[/b][i]bbbbbbbbbbbbbbbbbbbbbbbbbbbb[/i][u]cccccccccccccccccccc[/u]",
        "[color=Red]The quick[/color] [b]brown fox[/b] jumps [i]over[/i] the [u]lazy[/u] dog while the [bg=Blue]sleepy cat[/bg] watches the whole thing happen.",
        "[tt=Info]The quick brown fox[/tt] jumps over [action=Go]the lazy dog while the sleepy cat[/action] watches.",
        "text [img=Icon 16x16] more text that wraps around the line [img=Icon 16x16][img=Icon 8x8] and then some more text after the images",
        "[img=Icon][img=Icon][img=Icon][img=Icon][img=Icon]", "[img=Icon 16x16]\n[img=Icon]\ntext",
        "[u=2 -1 Red]underlined and wrapped across several lines of text in a narrow block[/u]",
        "[bg=Red 2,2]background and wrapped across several lines of text in a narrow block[/bg]",
        "[shadow=Blue 2 2]shadowed and wrapped across several lines[/shadow]",
        "   [b]  leading spaces[/b]  and   trailing  ", "[b] [/b] [i] [/i]", " [b]x[/b] ", "[b]-[/b]-[b]-[/b]",
        //  Edges of the values
        "[tt=a\rb]x[/tt]", "[tt=a\nb]x[/tt]", "[tt=a\n]x", "[tt=a\n", "[color=Red\n]x", "[bg=Red\n 2]x", "[u=2\n]x",
        "[color=Red* 5]x[/color]", "[color=Red *5.]x[/color]", "[color=Red*5.5]x[/color]", "[color=RGB(1, 2, 3, 4)]x[/color]", "[color=RGB(1,2,3,4,5)]x", "[color=RGB(1234,2,3)]x", "[color=RGBA(1,2,3)]x[/color]", "[color=rGbA(10,20,30,40)]x[/color]",
        "[color=#FFFFFFFFF]x", "[color=#FFFFFF*0.5]x[/color]", "[color=#FFFFFFFF]x[/color]", "[color=RGB(1,2,3)*0.5]x[/color]",
        "[shadow=Red 1 2 3]x", "[shadow=Red 12 34]x[/shadow]", "[shadow=Red -12,-34]x[/shadow]", "[shadow=Red 1  2]x", "[shadow=Red 1 2]a[/shadow][shadow=Blue]b[/shadow]",
        "[o=00.5]x", "[o=.]x", "[o=0.]x", "[o=0.55]x[/o]", "[o=.555]x", "[o=0.5 ]x",
        "[u=2 3 4]x[/u]", "[u=2 -1 ]x", "[u=2 x]x", "[u=-2]x", "[u= 2]x", "[u=2  -1]x", "[underline=0]x[/u]", "[u=2 -1 Red Blue]x[/u]", "[u=2 -1 Red|Blue|Green|Yellow]x[/u]",
        "[bg=Red 1,2,3,4,5]x", "[bg=Red -5]x[/bg]", "[bg=Red 1,2]x[/bg]", "[bg=Red  2]x", "[bg=Red 2,]x",
        "[img=Icon 1234x5678]", "[img=Icon 12345x6]", "[img=Icon 16 x 16]", "[img=Icon 16,16]", "[img=Icon  16x16]", "[img=Icon 16X16]x", "[img=Icon 3 4]",
        //  Non ASCII
        "é[b]é[/b]é", "[é]x", "[日本語] text", "[b]日本語[/b] テキスト", "[o=٠.٥]x[/o]", "[u=٢]x[/u]", "[color=Rèd]x[/color]", "[bacKground=Red]x", "[tt=Info é]x[/tt]", "[action=Go日本]x[/action]", "[Kold]x",
        //  Names and values that are not ASCII or that repeat
        "[tt=a]1[/tt][tt=a]2[/tt][tt=b]3[/tt]", "[action=a]1[/action][action=a]2[/action]", "[tt=a][/tt][tt=a][/tt]",
    };

    /// <summary>The fixed texts and <paramref name="GeneratedCount"/> generated ones.</summary>
    public static List<string> Build(int GeneratedCount = 320)
    {
        List<string> Texts = new(Fixed);
        Random Random = new(20261115);
        Texts.AddRange(Generate(Random, GeneratedCount));
        return Texts;
    }

    private static readonly string[] OpenTags =
    {
        "[b]", "[bold]", "[i]", "[italic]", "[o=0.5]", "[opacity=.75]", "[u]", "[u=2]", "[u=2 -1]", "[u=3 1 Red]", "[underline]", "[color=Red]", "[c=Blue]", "[fg=#00FF00]", "[color=RGB(1,2,3)]", "[color=Red*0.5]",
        "[bg=Red]", "[bg=Blue 2,2]", "[background=Red|Blue]", "[shadow=Black]", "[s=Red 1 2]", "[shadow=#00000080 2 2]", "[tt=Tip]", "[tooltip=Another tip]", "[action=Act]", "[command=Go]", "[img=Icon]", "[img=Icon 5x0]", "[img=Icon 0x7]", "[img=Icon 16x16]",
        "[img=Icon 8,12]", "[tt=T1]", "[tt=T2]", "[action=A1]", "[action=A2]",
    };

    private static readonly string[] CloseTags =
    {
        "[/b]", "[/bold]", "[/i]", "[/italic]", "[/o]", "[/opacity]", "[/u]", "[/underline]", "[/color]", "[/c]", "[/fg]", "[/bg]", "[/background]", "[/shadow]", "[/s]", "[/tt]", "[/tooltip]", "[/action]", "[/command]", "[\\b]", "[/B]",
    };

    private static readonly string[] Separators = { " ", " ", " ", "  ", "-", "\n", "\r\n", "\r", "\t", "\\[", "\\\\", "\\", "]", "[" };

    private static IEnumerable<string> Generate(Random Random, int Count)
    {
        for (var n = 0; n < Count; n++)
        {
            StringBuilder Builder = new();
            var Parts = Random.Next(1, 22);
            for (var j = 0; j < Parts; j++)
            {
                var Choice = Random.Next(12);
                if (Choice < 4)
                {
                    var Length = Random.Next(6) == 0 ? Random.Next(12, 40) : Random.Next(1, 9);
                    for (var k = 0; k < Length; k++)
                    {
                        Builder.Append((char)('a' + Random.Next(26)));
                    }
                }
                else if (Choice < 6)
                {
                    Builder.Append(Separators[Random.Next(Separators.Length)]);
                }
                else if (Choice < 9)
                {
                    Builder.Append(OpenTags[Random.Next(OpenTags.Length)]);
                }
                else if (Choice < 11)
                {
                    Builder.Append(CloseTags[Random.Next(CloseTags.Length)]);
                }
                else
                {
                    //  A broken tag: a tag with a character removed, replaced or added, or cut short
                    var Tag = Random.Next(2) == 0 ? OpenTags[Random.Next(OpenTags.Length)] : CloseTags[Random.Next(CloseTags.Length)];
                    switch (Random.Next(4))
                    {
                        case 0:
                            Tag = Tag.Remove(Random.Next(Tag.Length), 1);
                            break;
                        case 1:
                            Tag = Tag.Remove(Random.Next(Tag.Length), 1).Insert(Random.Next(Tag.Length - 1), ((char)('a' + Random.Next(26))).ToString());
                            break;
                        case 2:
                            Tag = Tag.Insert(Random.Next(Tag.Length), Random.Next(2) == 0 ? " " : "=");
                            break;
                        default:
                            Tag = Tag.Substring(0, Random.Next(1, Tag.Length));
                            break;
                    }

                    Builder.Append(Tag);
                }
            }

            yield return Builder.ToString();
        }
    }
}
