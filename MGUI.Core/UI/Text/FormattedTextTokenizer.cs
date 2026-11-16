using MGUI.Core.UI.XAML;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using MGUI.Core.UI.Brushes.FillBrushes;
using Thickness = MonoGame.Extended.Thickness;

namespace MGUI.Core.UI.Text;

/// <summary>A token parsed from a piece of formatted text that contains markdown.<br/>
/// These tokens are used as a first-pass, prior to more detailed parsing performed via <see cref="FTParser"/>.</summary>
public enum FTTokenType
{
    OpenTag,
    CloseTag,

    BoldOpenTagType,
    BoldCloseTagType,

    ItalicOpenTagType,
    ItalicCloseTagType,

    OpacityOpenTagType,
    OpacityCloseTagType,
    OpacityValue,

    ForegroundOpenTagType,
    ForegroundCloseTagType,
    ForegroundValue,

    UnderlineOpenTagType,
    UnderlineCloseTagType,
    UnderlineValue,

    BackgroundOpenTagType,
    BackgroundCloseTagType,
    BackgroundValue,

    ShadowOpenTagType,
    ShadowCloseTagType,
    ShadowValue,

    ImageOpenTagType,
    ImageValue,

    ToolTipOpenTagType,
    ToolTipCloseTagType,
    ToolTipValue,

    ActionOpenTagType,
    ActionCloseTagType,
    ActionValue,

    /// <summary>Used by the tokenizer to recognize that no well formed tag starts at an open tag character. <see cref="FTTokenizer.Tokenize"/> never returns it:
    /// such a character is part of a <see cref="StringLiteral"/>.</summary>
    InvalidToken,

    StringLiteral,

    LineBreak
}

/// <summary>Describes a pattern to look for when tokenizing a piece of formatted text.</summary>
public class FTTokenDefinition
{
    private Regex Regex { get; }
    private FTTokenType Type { get; }
    private ReadOnlyCollection<FTTokenType?> PrecededBy { get; }
    private ReadOnlyCollection<FTTokenType?> NotPrecededBy { get; }

    public FTTokenDefinition(FTTokenType Type, string Pattern, IEnumerable<FTTokenType?> PrecededBy, IEnumerable<FTTokenType?> NotPrecededBy)
    {
        //  CultureInvariant: kept from before the patterns spelled both cases of a letter themselves (MGUI ADR-0033), so that no option of the process can change what a pattern matches.
        Regex = new Regex(Pattern, RegexOptions.CultureInvariant);
        this.Type = Type;
        this.PrecededBy = PrecededBy.ToList().AsReadOnly();
        this.NotPrecededBy = NotPrecededBy.ToList().AsReadOnly();
    }

    public FTTokenMatch? Match(FTTokenType? Previous, string Input)
    {
        if (PrecededBy.Any() && !PrecededBy.Any(x => (x == null && !Previous.HasValue) || (x != null && Previous.HasValue && x.Value == Previous.Value)))
        {
            return null;
        }

        if (NotPrecededBy.Any(x => (x == null && !Previous.HasValue) || (x != null && Previous.HasValue && x.Value == Previous.Value)))
        {
            return null;
        }

        var Match = Regex.Match(Input);
        if (Match.Success)
        {
            var RemainingText = "";
            if (Match.Length != Input.Length)
            {
                RemainingText = Input.Substring(Match.Length);
            }

            return new FTTokenMatch(Type, Match.Value, RemainingText);
        }
        else
        {
            return null;
        }
    }
}

public record struct FTTokenMatch(FTTokenType TokenType, string Value, string RemainingText);

/// <summary>Tokenizes a piece of formatted text that contains markdown into a list of <see cref="FTTokenType"/> tokens.</summary>
public class FTTokenizer
{
    /// <summary>If this character is immediately followed by <see cref="OpenTagChar"/>, then this character instructs the tokenizer to treat the following 
    /// <see cref="OpenTagChar"/> literally instead of as the start of a markdown formatting code.</summary>
    public const char EscapeOpenTagChar = '\\';
    public const char OpenTagChar = '[';
    public const char CloseTagChar = ']';

    private List<FTTokenDefinition> Definitions { get; }
    private static IEnumerable<T> AsEnumerable<T>(params T[] values) => values;

    /// <summary>The pattern of <paramref name="Letters"/> (ASCII letters) without regard to case, spelled as classes (<c>bold</c> is <c>[Bb][Oo][Ll][Dd]</c>). The grammar of tags is ASCII (MGUI ADR-0033):
    /// the inline <c>(?i)</c> option would also match the letters that fold to an ASCII one (U+212A KELVIN SIGN is a <c>k</c>), and, without <see cref="RegexOptions.CultureInvariant"/>, those of the culture
    /// of the process (U+0130 is an <c>i</c> under a Turkish culture).</summary>
    private static string Word(string Letters)
    {
        StringBuilder Result = new();
        foreach (var Letter in Letters)
        {
            Result.Append('[').Append(char.ToUpperInvariant(Letter)).Append(char.ToLowerInvariant(Letter)).Append(']');
        }

        return Result.ToString();
    }

    public FTTokenizer()
    {
        Definitions = new();

        List<FTTokenType> OpenTagTypesWithoutValues = new() {
            FTTokenType.BoldOpenTagType,
            FTTokenType.ItalicOpenTagType
        };

        List<FTTokenType> OpenTagTypesWithValues = new() {
            FTTokenType.OpacityOpenTagType,
            FTTokenType.ForegroundOpenTagType,
            FTTokenType.UnderlineOpenTagType,
            FTTokenType.BackgroundOpenTagType,
            FTTokenType.ShadowOpenTagType,
            FTTokenType.ImageOpenTagType,
            FTTokenType.ToolTipOpenTagType,
            FTTokenType.ActionOpenTagType
        };

        List<FTTokenType> CloseTagTypes = new() {
            FTTokenType.BoldCloseTagType,
            FTTokenType.ItalicCloseTagType,
            FTTokenType.UnderlineCloseTagType,
            FTTokenType.OpacityCloseTagType,
            FTTokenType.ForegroundCloseTagType,
            FTTokenType.BackgroundCloseTagType,
            FTTokenType.ShadowCloseTagType,
            FTTokenType.ToolTipCloseTagType,
            FTTokenType.ActionCloseTagType
        };

        List<FTTokenType> TagValues = new() {
            FTTokenType.OpacityValue,
            FTTokenType.ForegroundValue,
            FTTokenType.UnderlineValue,
            FTTokenType.BackgroundValue,
            FTTokenType.ShadowValue,
            FTTokenType.ImageValue,
            FTTokenType.ToolTipValue,
            FTTokenType.ActionValue
        };

        var EscapedOpenTag = Regex.Escape(OpenTagChar.ToString());
        var EscapedCloseTag = Regex.Escape(CloseTagChar.ToString());

        //  Open Tag
        Definitions.Add(new(FTTokenType.OpenTag, $@"^{EscapedOpenTag}",
                AsEnumerable<FTTokenType?>(null, FTTokenType.CloseTag, FTTokenType.StringLiteral, FTTokenType.LineBreak), //PrecededBy
                AsEnumerable<FTTokenType?>(FTTokenType.InvalidToken)) //NotPrecededBy
        );

        //  Close Tag
        Definitions.Add(new(FTTokenType.CloseTag, $@"^{EscapedCloseTag}",
            OpenTagTypesWithoutValues.Union(TagValues).Union(CloseTagTypes).Cast<FTTokenType?>(),
            Enumerable.Empty<FTTokenType?>())
        );

        //  Tag Types
        var BoldTypePattern = $@"({Word("bold")}|{Word("b")}(?={EscapedCloseTag}))";
        Definitions.Add(new(FTTokenType.BoldOpenTagType, $@"^{BoldTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );
        Definitions.Add(new(FTTokenType.BoldCloseTagType, $@"^(\\|\/){BoldTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );

        var ItalicTypePattern = $@"({Word("italic")}|{Word("i")}(?={EscapedCloseTag}))";
        Definitions.Add(new(FTTokenType.ItalicOpenTagType, $@"^{ItalicTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );
        Definitions.Add(new(FTTokenType.ItalicCloseTagType, $@"^(\\|\/){ItalicTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );

        var OpacityTypePattern = $@"({Word("opacity")}|{Word("o")}(?=(\=|{EscapedCloseTag})))";
        Definitions.Add(new(FTTokenType.OpacityOpenTagType, $@"^{OpacityTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );
        Definitions.Add(new(FTTokenType.OpacityCloseTagType, $@"^(\\|\/){OpacityTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );

        var UnderlineTypePattern = $@"({Word("underline")}|{Word("u")}(?=(\=|{EscapedCloseTag})))";
        Definitions.Add(new(FTTokenType.UnderlineOpenTagType, $@"^{UnderlineTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );
        Definitions.Add(new(FTTokenType.UnderlineCloseTagType, $@"^(\\|\/){UnderlineTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );

        var ForegroundTypePattern = $@"({Word("fg")}|{Word("foreground")}|{Word("color")}|{Word("c")}(?=(\=|{EscapedCloseTag})))";
        Definitions.Add(new(FTTokenType.ForegroundOpenTagType, $@"^{ForegroundTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );
        Definitions.Add(new(FTTokenType.ForegroundCloseTagType, $@"^(\\|\/){ForegroundTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );

        var BackgroundTypePattern = $@"({Word("bg")}|{Word("background")})";
        Definitions.Add(new(FTTokenType.BackgroundOpenTagType, $@"^{BackgroundTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );
        Definitions.Add(new(FTTokenType.BackgroundCloseTagType, $@"^(\\|\/){BackgroundTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );

        var ShadowTypePattern = $@"({Word("shadow")}|{Word("s")}(?=(\=|{EscapedCloseTag})))";
        Definitions.Add(new(FTTokenType.ShadowOpenTagType, $@"^{ShadowTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );
        Definitions.Add(new(FTTokenType.ShadowCloseTagType, $@"^(\\|\/){ShadowTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );

        var ImageTypePattern = $@"({Word("image")}|{Word("img")})(?=\=)";
        Definitions.Add(new(FTTokenType.ImageOpenTagType, $@"^{ImageTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );

        var ToolTipTypePattern = $@"({Word("tooltip")}|{Word("tt")})";
        Definitions.Add(new(FTTokenType.ToolTipOpenTagType, $@"^{ToolTipTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );
        Definitions.Add(new(FTTokenType.ToolTipCloseTagType, $@"^(\\|\/){ToolTipTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );

        var ActionTypePattern = $@"({Word("action")}|{Word("command")})";
        Definitions.Add(new(FTTokenType.ActionOpenTagType, $@"^{ActionTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );
        Definitions.Add(new(FTTokenType.ActionCloseTagType, $@"^(\\|\/){ActionTypePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpenTag),
            Enumerable.Empty<FTTokenType?>())
        );

        //  Tag Values
        var OpacityPattern = $@"0?\.[0-9][0-9]?";
        Definitions.Add(new(FTTokenType.OpacityValue, $@"^={OpacityPattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.OpacityOpenTagType),
            Enumerable.Empty<FTTokenType?>())
        );

        Definitions.Add(new(FTTokenType.UnderlineValue, $@"^(={UnderlineValuePattern})?",
            AsEnumerable<FTTokenType?>(FTTokenType.UnderlineOpenTagType),
            Enumerable.Empty<FTTokenType?>())
        );

        const string ColorHexPattern = @"#[a-fA-F0-9]{6,8}";
        const string ColorNamePattern = @"[a-zA-Z]+";
        var ColorRGBPattern = $@"{Word("rgb")}[Aa]?\([0-9]{{1,3}}, ?[0-9]{{1,3}}, ?[0-9]{{1,3}}(, ?[0-9]{{1,3}})?\)";
        var ColorPattern = $@"({ColorRGBPattern}|{ColorHexPattern}|{ColorNamePattern})( ?\* ?[0-9]+(\.[0-9]+)?)?";
        Definitions.Add(new(FTTokenType.ForegroundValue, $@"^={ColorPattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.ForegroundOpenTagType),
            Enumerable.Empty<FTTokenType?>())
        );

        Definitions.Add(new(FTTokenType.BackgroundValue, $@"^={BackgroundValuePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.BackgroundOpenTagType),
            Enumerable.Empty<FTTokenType?>())
        );

        var ShadowValuePattern = $@"{ColorPattern}( -?[0-9]{{1,2}}( |,)-?[0-9]{{1,2}})?";
        Definitions.Add(new(FTTokenType.ShadowValue, $@"^={ShadowValuePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.ShadowOpenTagType),
            Enumerable.Empty<FTTokenType?>())
        );

        Definitions.Add(new(FTTokenType.ImageValue, $@"^={ImageValuePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.ImageOpenTagType),
            Enumerable.Empty<FTTokenType?>())
        );

        Definitions.Add(new(FTTokenType.ToolTipValue, $@"^={ToolTipValuePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.ToolTipOpenTagType),
            Enumerable.Empty<FTTokenType?>())
        );

        Definitions.Add(new(FTTokenType.ActionValue, $@"^={ActionValuePattern}",
            AsEnumerable<FTTokenType?>(FTTokenType.ActionOpenTagType),
            Enumerable.Empty<FTTokenType?>())
        );

        //  Invalid Token
        Definitions.Add(new(FTTokenType.InvalidToken, @"^.*",
            Enumerable.Empty<FTTokenType?>(),
            AsEnumerable<FTTokenType?>(null, FTTokenType.CloseTag, FTTokenType.StringLiteral, FTTokenType.LineBreak))
        );

#if NEVER //DEBUG
            try
            {
                //string sample = $"H{EscapeOpenTagChar}{EscapeOpenTagChar}[b]e\nllo[img=Test 32x64][bold]W[bg=Red]orld[color=Green][shadow=Red 1 2]def[/color]Test";
                string sample = $"[c=RGB(100,120,140)]Hello world[/c]";
                List<FTTokenMatch> tokens = Tokenize(sample, true).ToList();
            }
            catch (Exception) { }
#endif
    }

    //  Regex pattern that checks if the next character is either a close tag ']' or end of string, but does not consume that character (lookahead)
    private static readonly string CloseTagOrEndOfStringLookahead = $@"(?=({Regex.Escape(CloseTagChar.ToString())}|$))";
    //  Regex pattern that checks if the next character is a close tag ']', but does not consume that character (lookahead)
    private static readonly string CloseTagLookahead = $@"(?=({Regex.Escape(CloseTagChar.ToString())}))";

    //  The underline Height (positive integer) followed by a vertical offset (integer, can be negative), followed by the underline's fill brush (usually a single color) 
    //  All parameters are optional
    //  EX: "[Underline=2 -1 Blue]" uses a solid blue brush to draw a 2px tall rectangle, offsetted upwards by 1px above the bottom of the text it's anchored to
    //      "[Underline=4]" uses the TextBlock's current Foreground Color as the brush, to draw a 4px tall rectangle at the default location just below the text it's anchored to
    private static readonly string UnderlineValuePattern = $@"((?<Height>[0-9]+)(( (?<Offset>-?[0-9]+))(( (?<Brush>.+?)))?)?)?{CloseTagOrEndOfStringLookahead}";
    public static readonly Regex UnderlineValueParser = new(UnderlineValuePattern);
    public static (int? Height, int? VerticalOffset, IFillBrush Brush) ParseUnderlineValue(string Value)
    {
        var Match = UnderlineValueParser.Match(Value);

        int? Height = Match.Groups["Height"].Success ? int.Parse(Match.Groups["Height"].Value) : null;
        int? VerticalOffset = Match.Groups["Offset"].Success ? int.Parse(Match.Groups["Offset"].Value) : null;
        IFillBrush Brush = null;
        if (Match.Groups["Brush"].Success)
        {
            var BrushString = Match.Groups["Brush"].Value;
            Brush = FillBrushStringConverter.ParseFillBrush(BrushString).ToFillBrush(null, null);
        }

        return (Height, VerticalOffset, Brush);
    }

    //  The background fill brush (usually a single color) followed by an optional Padding Thickness
    //  EX: "[Background=Red 2,-4,2,8]" uses a solid red brush, with Padding="2,-4,2,8"
    private static readonly string BackgroundValuePattern = $@"(?<Brush>.+?)( (?<Padding>-?[0-9]+(,-?[0-9]+){{0,3}}))?{CloseTagOrEndOfStringLookahead}";
    public static readonly Regex BackgroundValueParser = new(BackgroundValuePattern);
    public static (IFillBrush Brush, Thickness Padding) ParseBackgroundValue(string Value)
    {
        var Match = BackgroundValueParser.Match(Value);

        var BrushString = Match.Groups["Brush"].Value;
        var Brush = FillBrushStringConverter.ParseFillBrush(BrushString).ToFillBrush(null, null);
        var Padding = Match.Groups["Padding"].Success ? Thickness.Parse(Match.Groups["Padding"].Value) : default;

        return (Brush, Padding);
    }

    //  The name to reference the ToolTip by
    private static readonly string ToolTipValuePattern = $@"(?<ToolTipName>.+?){CloseTagOrEndOfStringLookahead}";
    public static readonly Regex ToolTipValueParser = new(ToolTipValuePattern);

    //  The name to reference the Action delegate by
    private static readonly string ActionValuePattern = $@"(?<ActionName>.+?)(?=({Regex.Escape(CloseTagChar.ToString())}|$))";
    public static readonly Regex ActionValueParser = new(ActionValuePattern);

    //  The name to reference the texture by, followed by an optional target size X Y
    //  EX: "[img=Gold.png 32x16]" would reference a texture by name="Gold.png", and draw it with Width=32, Height=16
    private static readonly string ImageValuePattern = $@"(?<SourceName>.+?)( (?<Width>[0-9]{{1,4}})( |,|x|X)(?<Height>[0-9]{{1,4}}))?{CloseTagOrEndOfStringLookahead}";
    private static readonly Regex ImageValueParser = new(ImageValuePattern);

    internal static (string SourceName, int? TargetWidth, int? TargetHeight) ParseImageValue(string Value)
    {
        var Match = ImageValueParser.Match(Value);

        var SourceName = Match.Groups["SourceName"].Value;
        int? Width = Match.Groups["Width"].Success ? int.Parse(Match.Groups["Width"].Value) : null;
        int? Height = Match.Groups["Height"].Success ? int.Parse(Match.Groups["Height"].Value) : null;

        return (SourceName, Width, Height);
    }

    /// <summary>Removes all formatted text markdown from the given <paramref name="Text"/> by prefixing all instances of <see cref="OpenTagChar"/> with <see cref="EscapeOpenTagChar"/>.<para/>
    /// (This method does not modify <see cref="FTTokenType.LineBreak"/>s)</summary>
    public static string EscapeMarkdown(string Text)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return Text;
        }

        StringBuilder Result = new();

        // FIX (Task 7): Count consecutive EscapeOpenTagChar ('\') immediately before each OpenTagChar ('[').
        // An ODD count means the last '\' already escapes the '[', so we only need to double the preceding
        // backslashes to preserve them as literals.
        // An EVEN count (including 0) means no backslash currently escapes the '[', so we must add one.
        //
        // General rule for N consecutive backslashes before '[':
        //   • Append N additional backslashes (to double each existing one as a literal pair).
        //   • Append 1 escape backslash (to escape the '[').
        //   • Then append '[' as usual.
        // This collapses correctly for N=0 (no preceding backslash): just prepend 1 escape. ✓
        // For N=1 (old case that was already handled): output '\\\[' (pair + escaped '[');
        //   tokenizer: '\\' → literal '\', '\[' → literal '['. Renders as '\['. ✓
        //
        // The tokenizer interprets consecutive backslashes before '[' as follows (proof tests in FormattedTextTokenizerTest):
        //   "\[tag]"   → N=1 (odd):  '[' IS escaped  → renders literal '[tag]'
        //   "\\[tag]"  → N=2 (even): '[' NOT escaped → renders literal '\' + bold/tag
        //   "\\\[tag]" → N=3 (odd):  '[' IS escaped  → renders literal '\[tag]'
        var consecutiveEscapeCount = 0;

        foreach (var c in Text)
        {
            if (c == OpenTagChar)
            {
                // Add 'consecutiveEscapeCount' extra backslashes (doubling the run) + 1 escape for '['.
                for (var k = 0; k < consecutiveEscapeCount; k++)
                {
                    Result.Append(EscapeOpenTagChar);
                }

                Result.Append(EscapeOpenTagChar);
                consecutiveEscapeCount = 0;
            }
            else if (c == EscapeOpenTagChar)
            {
                consecutiveEscapeCount++;
            }
            else
            {
                consecutiveEscapeCount = 0;
            }
            Result.Append(c);
        }

        return Result.ToString();
    }

    private static readonly string EscapedEscapeOpenTagChar = Regex.Escape(EscapeOpenTagChar.ToString());
    private static readonly string EscapedOpenTagChar = Regex.Escape(OpenTagChar.ToString());

    private static readonly string SubPattern1 = $@"[^{EscapedEscapeOpenTagChar}{EscapedOpenTagChar}]";
    private static readonly string SubPattern2 = $@"({EscapedEscapeOpenTagChar}({EscapedEscapeOpenTagChar}*)(?!({EscapedEscapeOpenTagChar}|{EscapedOpenTagChar})))";
    private static readonly string SubPattern3 = $@"({EscapedEscapeOpenTagChar}{EscapedOpenTagChar})";
    //  Anchored where the match starts (\G), so the text is read in place, from an index, without a copy of what is left of it.
    private static readonly Regex LiteralRegex = new($@"\G({SubPattern1}|{SubPattern2}|{SubPattern3})*");

    /// <param name="ShouldTokenizeLineBreaks">If true, linebreaks '\n', '\r', "\r\n" will be tokenized as <see cref="FTTokenType.LineBreak"/>.<br/>
    /// If false, they will not be tokenized, and will instead remain as a substring inside a <see cref="FTTokenType.StringLiteral"/> token.<para/>
    /// Warning - A linebreak is treated as the character literals: '\n', '\r', "\r\n", as opposed to the escaped strings such as @"\n". Be sure not to accidentally use a literal escape character '\\', such as if prefixing a hard-coded string with @.</param>
    public IEnumerable<FTTokenMatch> Tokenize(string Text, bool ShouldTokenizeLineBreaks)
    {
        if (string.IsNullOrEmpty(Text))
        {
            yield break;
        }

        var Index = 0;
        StringBuilder CurrentStringLiteral = new();

        FTTokenType? PreviousToken = null;
        while (Index < Text.Length)
        {
            var Match = LiteralRegex.Match(Text, Index);
            if (Match.Success && Match.Length > 0)
            {
                var StringLiteral = Match.Value.Replace($"{EscapeOpenTagChar}{OpenTagChar}", $"{OpenTagChar}");
                CurrentStringLiteral.Append(StringLiteral);
                Index += Match.Length;
            }
            else if (Text[Index] == EscapeOpenTagChar && Index + 1 < Text.Length && Text[Index + 1] == EscapeOpenTagChar)
            {
                CurrentStringLiteral.Append(EscapeOpenTagChar);
                Index += 2;
            }
            else
            {
                //  An open tag character. If it does not open a well formed tag, it is a character like another and the reading goes on right after it (MGUI ADR-0030).
                //  A tag ends at the first close tag character that follows, with no line feed and no other open tag character before it: only these characters are read to
                //  decide, so each character of the text is looked at a bounded number of times whatever the text is.
                var TagEndSearchStop = Index + 1;
                while (TagEndSearchStop < Text.Length && Text[TagEndSearchStop] != CloseTagChar && Text[TagEndSearchStop] != OpenTagChar && Text[TagEndSearchStop] != '\n')
                {
                    TagEndSearchStop++;
                }

                List<FTTokenMatch> TagTokens = null;
                if (TagEndSearchStop < Text.Length && Text[TagEndSearchStop] == CloseTagChar)
                {
                    TagTokens = TokenizeTag(CurrentStringLiteral.Length > 0 ? FTTokenType.StringLiteral : PreviousToken, Text, Index, TagEndSearchStop);
                }

                if (TagTokens == null)
                {
                    CurrentStringLiteral.Append(Text[Index]);
                    Index++;
                    continue;
                }

                if (CurrentStringLiteral.Length > 0)
                {
                    foreach (var Item in TokenizeLineBreaks(CurrentStringLiteral.ToString(), ShouldTokenizeLineBreaks))
                    {
                        yield return Item;
                    }

                    CurrentStringLiteral.Clear();
                }

                foreach (var Item in TagTokens)
                {
                    yield return Item;
                }

                Index = TagEndSearchStop + 1;
                PreviousToken = FTTokenType.CloseTag;
            }
        }

        if (CurrentStringLiteral.Length > 0)
        {
            foreach (var Item in TokenizeLineBreaks(CurrentStringLiteral.ToString(), ShouldTokenizeLineBreaks))
            {
                yield return Item;
            }
        }
    }

    /// <summary>The tokens of the tag written from <paramref name="Start"/> (an <see cref="OpenTagChar"/>) to <paramref name="End"/> (the first <see cref="CloseTagChar"/> after it) in
    /// <paramref name="Text"/>, from <see cref="FTTokenType.OpenTag"/> to <see cref="FTTokenType.CloseTag"/>. Only these characters are read, so a text that is not a tag costs its own length.</summary>
    /// <returns>Null if these characters are not a well formed tag: an unknown type, a value that is not one of its type, or a value its interpretation rejects.</returns>
    private List<FTTokenMatch> TokenizeTag(FTTokenType? PreviousToken, string Text, int Start, int End)
    {
        List<FTTokenMatch> Tokens = new();
        var RemainingTag = Text.Substring(Start, End - Start + 1);
        while (RemainingTag.Length > 0)
        {
            var Match = Definitions.Select(x => x.Match(PreviousToken, RemainingTag)).First(x => x != null).Value;
            if (Match.TokenType == FTTokenType.InvalidToken)
            {
                return null;
            }

            Tokens.Add(Match);
            RemainingTag = Match.RemainingText;
            PreviousToken = Match.TokenType;
        }

        if (PreviousToken != FTTokenType.CloseTag || !MGTextRun.IsWellFormedTag(Tokens))
        {
            return null;
        }

        //  The tag is one: its tokens now say what is left of the whole text after each of them
        var TextAfterTag = Text.Substring(End + 1);
        for (var i = 0; i < Tokens.Count; i++)
        {
            Tokens[i] = Tokens[i] with { RemainingText = Tokens[i].RemainingText + TextAfterTag };
        }

        return Tokens;
    }

    private static readonly Regex LineBreakRegex = new("(\r\n|\r|\n)"); // "\r\n" for Windows, '\r' for Mac, '\n' for Linux

    /// <param name="Enabled">True if '\n', '\r', and "\r\n" characters should be tokenized as <see cref="FTTokenType.LineBreak"/>.<br/>
    /// False if the entire <paramref name="Text"/> should be returned as a single <see cref="FTTokenType.StringLiteral"/> token.</param>
    public static IEnumerable<FTTokenMatch> TokenizeLineBreaks(string Text, bool Enabled = true)
    {
        if (string.IsNullOrEmpty(Text))
        {
            yield break;
        }

        if (Enabled)
        {
            Match Match;
            while ((Match = LineBreakRegex.Match(Text)).Success)
            {
                var Index = Match.Index;
                if (Index != 0)
                {
                    yield return new(FTTokenType.StringLiteral, Text.Substring(0, Index), "");
                }

                yield return new(FTTokenType.LineBreak, Match.Value, "");
                Text = Text.Substring(Index + Match.Length);
                if (Text == string.Empty)
                {
                    break;
                }
            }

            if (Text != string.Empty)
            {
                yield return new(FTTokenType.StringLiteral, Text, "");
            }
        }
        else
        {
            yield return new(FTTokenType.StringLiteral, Text, "");
        }
    }
}