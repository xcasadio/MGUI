using System.Globalization;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Text;

/// <summary>
/// Reads a formatted text (inline markup such as <c>[b]</c>, <c>[color=Red]</c>, <c>[tooltip=Name]</c>, <c>[img=Name 16x16]</c>) into a reused <see cref="FormattedTextRuns"/> without allocating:
/// the work of <see cref="FTTokenizer"/>, <see cref="FTParser"/>, <see cref="MGTextRun.ParseRuns(string, MGTextRunConfig)"/> and of the sanitizing of <see cref="MGTextBlock"/>, in one pass over the
/// characters, with no regular expression, no substring, no LINQ and no collection created per call.<para/>
/// The result is the one the legacy code gives, quirks included, for every text it can read without throwing. Where the legacy code throws (a tag that is not closed, a tag of a known
/// type that is not well formed, a color that does not exist, ...), or where this scanner cannot be sure it would read the same thing (a non ASCII letter or digit where the grammar
/// compares letters or digits), <see cref="TryScan"/> returns false and the caller runs the legacy code instead, which throws or reads the text itself.<para/>
/// The language, as the legacy tokenizer defines it:<list type="bullet">
/// <item>A tab is four spaces. A line break is <c>\r\n</c>, <c>\r</c> or <c>\n</c>.</item>
/// <item><c>\</c> followed by <c>[</c> is a literal <c>[</c>; two backslashes before a <c>[</c> are a literal backslash and the <c>[</c> starts a tag (the pairs of a run of backslashes before a
/// <c>[</c> collapse to one backslash each, an odd one escapes the <c>[</c>); backslashes that are not before a <c>[</c> are literal and are not collapsed.</item>
/// <item>A tag is <c>[type]</c>, <c>[type=value]</c> or <c>[/type]</c> (<c>[\type]</c>), the type being matched without case. A <c>[</c> that is followed by no known type turns the rest of the text,
/// from that <c>[</c>, into a literal (line breaks included, escapes not processed). A value that is a name (tooltip, action, image, background, ...) runs up to the first <c>]</c>.</item>
/// </list>
/// The values that the legacy code interprets with a regular expression or a converter (colors, brushes, images) are interpreted by the same legacy functions and kept in a small cache
/// (<see cref="CacheLimit"/> entries per kind), keyed by the characters of the value, and the names of the tooltips and the actions are interned the same way: a text whose values were
/// already seen allocates nothing; a value never seen allocates its string (and what its legacy interpretation allocates) once.
/// </summary>
internal sealed class FormattedTextScanner
{
    /// <summary>The number of entries of each cache. A cache that is full is emptied before it takes a new entry, so a text block that keeps showing new names does not grow without bound.</summary>
    internal const int CacheLimit = 128;

    /// <summary>True if <paramref name="Tokenizer"/> reads <c>[I]</c> as the italic tag. The legacy patterns ignore case by the rules of the culture that was current when the tokenizer was
    /// created: under a Turkish or an Azerbaijani culture an uppercase <c>I</c> is not the letter <c>i</c>, and <c>[I]</c>, <c>[ITALIC]</c> or <c>[IMG=...]</c> are literals.</summary>
    internal static bool LegacyFoldsUpperI(FTTokenizer Tokenizer)
    {
        if (!Tokenizer.TryTokenize("[I]", false, out var Tokens))
        {
            return false;
        }

        foreach (var Token in Tokens)
        {
            if (Token.TokenType == FTTokenType.ItalicOpenTagType)
            {
                return true;
            }
        }

        return false;
    }

    private static readonly bool DefaultFoldsUpperI = LegacyFoldsUpperI(MGTextRun.Tokenizer);

    /// <summary>False if an uppercase <c>I</c> in the type of a tag makes the text ambiguous (see <see cref="LegacyFoldsUpperI"/>).</summary>
    private readonly bool FoldsUpperI;

    public FormattedTextScanner()
        : this(DefaultFoldsUpperI)
    {
    }

    /// <param name="FoldsUpperI">What <see cref="LegacyFoldsUpperI"/> answers for the tokenizer of the legacy code.</param>
    internal FormattedTextScanner(bool FoldsUpperI)
    {
        this.FoldsUpperI = FoldsUpperI;
    }


    private enum TagKind : byte
    {
        Bold,
        Italic,
        Opacity,
        Underline,
        Foreground,
        Background,
        Shadow,
        Image,
        ToolTip,
        Action
    }

    private readonly struct ParsedImage
    {
        public readonly string SourceName;
        public readonly int Width;
        public readonly int Height;

        public ParsedImage(string SourceName, int Width, int Height)
        {
            this.SourceName = SourceName;
            this.Width = Width;
            this.Height = Height;
        }
    }

    /// <summary>A dictionary keyed by strings that is read with the characters of a value, without building the string.</summary>
    private sealed class ValueCache<T>
    {
        private readonly Dictionary<string, T> Items = new();
        private readonly Dictionary<string, T>.AlternateLookup<ReadOnlySpan<char>> Lookup;

        public ValueCache()
        {
            Lookup = Items.GetAlternateLookup<ReadOnlySpan<char>>();
        }

        public bool TryGet(ReadOnlySpan<char> Key, out T Value) => Lookup.TryGetValue(Key, out Value);

        public void Add(string Key, T Value)
        {
            if (Items.Count >= CacheLimit)
            {
                Items.Clear();
            }

            Items[Key] = Value;
        }
    }

    private ValueCache<Color> ForegroundCache;
    private ValueCache<MGTextRunUnderlineConfig> UnderlineCache;
    private ValueCache<MGTextRunBackgroundConfig> BackgroundCache;
    private ValueCache<MGTextRunShadowConfig> ShadowCache;
    private ValueCache<ParsedImage> ImageCache;
    private ValueCache<string> NameCache;

    private readonly List<float> PreviousOpacities = new();
    private readonly List<Color?> PreviousForegrounds = new();
    private readonly List<MGTextRunUnderlineConfig> PreviousUnderlines = new();
    private readonly List<MGTextRunBackgroundConfig> PreviousBackgrounds = new();
    private readonly List<MGTextRunShadowConfig> PreviousShadows = new();
    private readonly List<string> PreviousToolTipIds = new();
    private readonly List<string> PreviousActionIds = new();

    /// <summary>The text with its tabs replaced by spaces.</summary>
    private char[] Source = new char[64];
    private int SourceLength;

    //  State of the scan in progress
    private FormattedTextRuns Output;
    private MGTextRunConfig State;
    private string ToolTipId;
    private string ActionId;
    private int OpenStart;
    private int OpenLength;
    private bool IsAmbiguous;

    /// <summary>Reads <paramref name="Text"/> into <paramref name="Output"/>, replacing its content.</summary>
    /// <param name="DefaultSettings">The settings of a text outside any tag.</param>
    /// <param name="Desktop">Gives the default size of an image that has none (null if the text block has no desktop: such an image is then not supported).</param>
    /// <returns>False if the legacy code has to read the text (<paramref name="Output"/> is then unspecified).</returns>
    public bool TryScan(ReadOnlySpan<char> Text, in MGTextRunConfig DefaultSettings, MGDesktop Desktop, FormattedTextRuns Output)
    {
        this.Output = Output;
        Output.Clear();
        NormalizeTabs(Text);

        State = DefaultSettings;
        ToolTipId = null;
        ActionId = null;
        OpenStart = -1;
        OpenLength = 0;
        IsAmbiguous = false;
        PreviousOpacities.Clear();
        PreviousForegrounds.Clear();
        PreviousUnderlines.Clear();
        PreviousBackgrounds.Clear();
        PreviousShadows.Clear();
        PreviousToolTipIds.Clear();
        PreviousActionIds.Clear();

        bool Success;
        try
        {
            Success = ScanText(Desktop) && !IsAmbiguous;
        }
        catch (Exception)
        {
            //  A value the legacy interpretation rejects (an unknown color, a number that does not fit, ...): the legacy code reads the text and throws what it threw before.
            Success = false;
        }

        this.Output = null;
        return Success;
    }

    private void NormalizeTabs(ReadOnlySpan<char> Text)
    {
        var Tabs = 0;
        foreach (var c in Text)
        {
            if (c == '\t')
            {
                Tabs++;
            }
        }

        var Needed = Text.Length + Tabs * (MGTextRun.TabSpacesCount - 1);
        if (Source.Length < Needed)
        {
            var Length = Source.Length;
            while (Length < Needed)
            {
                Length *= 2;
            }

            Source = new char[Length];
        }

        var Count = 0;
        foreach (var c in Text)
        {
            if (c == '\t')
            {
                for (var i = 0; i < MGTextRun.TabSpacesCount; i++)
                {
                    Source[Count++] = ' ';
                }
            }
            else
            {
                Source[Count++] = c;
            }
        }

        SourceLength = Count;
    }

    private bool ScanText(MGDesktop Desktop)
    {
        var Position = 0;
        while (Position < SourceLength)
        {
            var c = Source[Position];
            if (c == '[')
            {
                if (!ScanTag(ref Position, Desktop))
                {
                    return false;
                }
            }
            else if (c == '\\')
            {
                var End = Position;
                while (End < SourceLength && Source[End] == '\\')
                {
                    End++;
                }

                var Count = End - Position;
                if (End < SourceLength && Source[End] == '[')
                {
                    //  Each pair of backslashes before a '[' is one literal backslash. An odd one left escapes the '[' (a literal); otherwise the '[' starts a tag.
                    for (var i = 0; i < Count / 2; i++)
                    {
                        AppendLiteral('\\');
                    }

                    if ((Count & 1) == 1)
                    {
                        AppendLiteral('[');
                        Position = End + 1;
                    }
                    else
                    {
                        Position = End;
                    }
                }
                else
                {
                    for (var i = 0; i < Count; i++)
                    {
                        AppendLiteral('\\');
                    }

                    Position = End;
                }
            }
            else if (c == '\r' || c == '\n')
            {
                CloseText();
                var Length = c == '\r' && Position + 1 < SourceLength && Source[Position + 1] == '\n' ? 2 : 1;
                Output.AddLineBreak(Length);
                Position += Length;
            }
            else
            {
                AppendLiteral(c);
                Position++;
            }
        }

        CloseText();
        return true;
    }

    private void AppendLiteral(char Value)
    {
        if (OpenStart < 0)
        {
            OpenStart = Output.ReserveChars(1);
            OpenLength = 0;
        }

        Output.AppendChar(Value);
        OpenLength++;
    }

    /// <summary>Ends the text run being written, if there is one. The settings, the tooltip and the action of the run are the current ones: every tag closes the run before it changes them.</summary>
    private void CloseText()
    {
        if (OpenStart >= 0 && OpenLength > 0)
        {
            Output.AddText(OpenStart, OpenLength, State, ToolTipId, ActionId);
        }

        OpenStart = -1;
        OpenLength = 0;
    }

    /// <summary>Reads the tag that starts at <paramref name="Position"/> (a '[') and moves <paramref name="Position"/> after it.</summary>
    /// <returns>False if the legacy code has to read the text.</returns>
    private bool ScanTag(ref int Position, MGDesktop Desktop)
    {
        var Start = Position + 1;
        if (Start >= SourceLength)
        {
            //  A '[' that ends the text: the legacy parser throws.
            return false;
        }

        if (!MatchType(Start, out var Kind, out var IsClose, out var AfterType))
        {
            if (IsAmbiguous)
            {
                return false;
            }

            //  Not a tag: the legacy tokenizer reads an invalid token right after the '[', and the parser turns the '[' and everything after it, line breaks and
            //  escapes included, into one literal, which joins the literal that comes just before it.
            for (var i = Position; i < SourceLength; i++)
            {
                AppendLiteral(Source[i]);
            }

            Position = SourceLength;
            return true;
        }

        if (IsClose)
        {
            if (AfterType >= SourceLength || Source[AfterType] != ']')
            {
                return false;
            }

            CloseText();
            ApplyClose(Kind);
            Position = AfterType + 1;
            return true;
        }

        switch (Kind)
        {
            case TagKind.Bold:
            case TagKind.Italic:
                if (AfterType >= SourceLength || Source[AfterType] != ']')
                {
                    return false;
                }

                CloseText();
                if (Kind == TagKind.Bold)
                {
                    State.IsBold = true;
                }
                else
                {
                    State.IsItalic = true;
                }

                Position = AfterType + 1;
                return true;

            case TagKind.Underline:
                return ScanUnderline(AfterType, ref Position);

            case TagKind.Opacity:
                return ScanOpacity(AfterType, ref Position);

            default:
                return ScanValuedTag(Kind, AfterType, ref Position, Desktop);
        }
    }

    private bool ScanOpacity(int At, ref int Position)
    {
        //  =0?\.\d\d?
        if (At >= SourceLength || Source[At] != '=')
        {
            return false;
        }

        var ValueStart = At + 1;
        var i = ValueStart;
        if (i < SourceLength && Source[i] == '0')
        {
            i++;
        }

        if (i >= SourceLength || Source[i] != '.')
        {
            return false;
        }

        i++;
        if (i >= SourceLength || !IsDigit(Source[i]))
        {
            return false;
        }

        i++;
        if (i < SourceLength && IsDigit(Source[i]))
        {
            i++;
        }

        if (i >= SourceLength || Source[i] != ']' || IsAmbiguous)
        {
            return false;
        }

        var Value = float.Parse(new ReadOnlySpan<char>(Source, ValueStart, i - ValueStart), CultureInfo.InvariantCulture);
        CloseText();
        PreviousOpacities.Add(State.Opacity);
        State.Opacity = Value;
        Position = i + 1;
        return true;
    }

    private bool ScanUnderline(int At, ref int Position)
    {
        MGTextRunUnderlineConfig Config;
        int End;
        if (At < SourceLength && Source[At] == '=')
        {
            //  =  then: nothing, or a height, or a height and an offset, or a height, an offset and a brush (which is any text up to the ']'), as the legacy pattern has it.
            var ValueStart = At + 1;
            End = IndexOfCloseTag(ValueStart);
            if (End < 0 || Contains(ValueStart, End, '\n') || !IsUnderlineValue(ValueStart, End))
            {
                return false;
            }

            var Value = new ReadOnlySpan<char>(Source, ValueStart, End - ValueStart);
            Config = Value.IsEmpty ? new MGTextRunUnderlineConfig(true) : GetUnderline(Value);
        }
        else
        {
            if (At >= SourceLength || Source[At] != ']')
            {
                return false;
            }

            End = At;
            Config = new MGTextRunUnderlineConfig(true);
        }

        if (IsAmbiguous)
        {
            return false;
        }

        CloseText();
        PreviousUnderlines.Add(State.Underline);
        State.Underline = Config;
        Position = End + 1;
        return true;
    }

    /// <summary>The value of an underline: <c>\d+( -?\d+( .+)?)?</c>, or nothing.</summary>
    private bool IsUnderlineValue(int Start, int End)
    {
        if (Start == End)
        {
            return true;
        }

        var i = Start;
        while (i < End && IsDigit(Source[i]))
        {
            i++;
        }

        if (i == Start)
        {
            return false;
        }

        if (i == End)
        {
            return true;
        }

        if (Source[i] != ' ')
        {
            return false;
        }

        i++;
        if (i < End && Source[i] == '-')
        {
            i++;
        }

        var DigitsStart = i;
        while (i < End && IsDigit(Source[i]))
        {
            i++;
        }

        if (i == DigitsStart)
        {
            return false;
        }

        if (i == End)
        {
            return true;
        }

        //  A brush: a space then at least one character
        return Source[i] == ' ' && i + 1 < End;
    }

    private bool ScanValuedTag(TagKind Kind, int At, ref int Position, MGDesktop Desktop)
    {
        if (At >= SourceLength || Source[At] != '=')
        {
            return false;
        }

        var ValueStart = At + 1;
        int End;
        switch (Kind)
        {
            case TagKind.Foreground:
                if (!MatchColor(ValueStart, out End))
                {
                    return false;
                }

                break;

            case TagKind.Shadow:
                if (!MatchColor(ValueStart, out End))
                {
                    return false;
                }

                End = MatchShadowOffset(End);
                break;

            default:
                //  Background, image, tooltip, action: a name that runs up to the first ']'
                if (ValueStart >= SourceLength || Source[ValueStart] == ']')
                {
                    return false;
                }

                End = IndexOfCloseTag(ValueStart);
                if (End < 0 || Contains(ValueStart, End, '\n'))
                {
                    return false;
                }

                break;
        }

        if (End >= SourceLength || Source[End] != ']' || IsAmbiguous)
        {
            return false;
        }

        var Value = new ReadOnlySpan<char>(Source, ValueStart, End - ValueStart);
        switch (Kind)
        {
            case TagKind.Foreground:
            {
                var Color = GetForeground(Value);
                CloseText();
                PreviousForegrounds.Add(State.Foreground);
                State = State.WithForeground(Color);
                break;
            }

            case TagKind.Background:
            {
                var Background = GetBackground(Value);
                CloseText();
                PreviousBackgrounds.Add(State.Background);
                State.Background = Background;
                break;
            }

            case TagKind.Shadow:
            {
                var Shadow = GetShadow(Value);
                CloseText();
                PreviousShadows.Add(State.Shadow);
                State.Shadow = Shadow;
                break;
            }

            case TagKind.Image:
            {
                var Image = GetImage(Value);
                var Width = Image.Width;
                var Height = Image.Height;
                //  An image that has no size takes the default size of its texture
                if (Width <= 0 && Height <= 0)
                {
                    (var DefaultWidth, var DefaultHeight) = Desktop.Resources.GetTextureDimensions(Image.SourceName);
                    Width = DefaultWidth ?? 0;
                    Height = DefaultHeight ?? 0;
                }

                CloseText();
                Output.AddImage(Image.SourceName, Width, Height, ToolTipId, ActionId);
                break;
            }

            case TagKind.ToolTip:
            {
                var Name = GetName(Value);
                CloseText();
                if (!string.IsNullOrEmpty(ToolTipId))
                {
                    PreviousToolTipIds.Add(ToolTipId);
                }

                ToolTipId = Name;
                break;
            }

            default:
            {
                var Name = GetName(Value);
                CloseText();
                if (!string.IsNullOrEmpty(ActionId))
                {
                    PreviousActionIds.Add(ActionId);
                }

                ActionId = Name;
                break;
            }
        }

        Position = End + 1;
        return true;
    }

    private void ApplyClose(TagKind Kind)
    {
        switch (Kind)
        {
            case TagKind.Bold:
                State.IsBold = false;
                break;
            case TagKind.Italic:
                State.IsItalic = false;
                break;
            case TagKind.Opacity:
                State.Opacity = Pop(PreviousOpacities, 1.0f);
                break;
            case TagKind.Underline:
                State.Underline = Pop(PreviousUnderlines, default);
                break;
            case TagKind.Foreground:
                State = State.WithForeground(Pop(PreviousForegrounds, null));
                break;
            case TagKind.Background:
                State.Background = Pop(PreviousBackgrounds, default);
                break;
            case TagKind.Shadow:
                State.Shadow = Pop(PreviousShadows, default);
                break;
            case TagKind.ToolTip:
                ToolTipId = Pop(PreviousToolTipIds, null);
                break;
            default:
                ActionId = Pop(PreviousActionIds, null);
                break;
        }
    }

    private static T Pop<T>(List<T> Items, T DefaultValue)
    {
        if (Items.Count == 0)
        {
            return DefaultValue;
        }

        var Value = Items[Items.Count - 1];
        Items.RemoveAt(Items.Count - 1);
        return Value;
    }

    //  ---- Values: the legacy interpretation, kept by the characters of the value ----

    private Color GetForeground(ReadOnlySpan<char> Value)
    {
        var Cache = ForegroundCache ??= new();
        if (!Cache.TryGet(Value, out var Result))
        {
            var Key = new string(Value);
            Result = MGTextRunConfig.ParseForegroundValue(Key);
            Cache.Add(Key, Result);
        }

        return Result;
    }

    private MGTextRunUnderlineConfig GetUnderline(ReadOnlySpan<char> Value)
    {
        var Cache = UnderlineCache ??= new();
        if (!Cache.TryGet(Value, out var Result))
        {
            var Key = new string(Value);
            Result = MGTextRunConfig.ParseUnderlineValue(Key);
            Cache.Add(Key, Result);
        }

        return Result;
    }

    private MGTextRunBackgroundConfig GetBackground(ReadOnlySpan<char> Value)
    {
        var Cache = BackgroundCache ??= new();
        if (!Cache.TryGet(Value, out var Result))
        {
            var Key = new string(Value);
            Result = MGTextRunConfig.ParseBackgroundValue(Key);
            Cache.Add(Key, Result);
        }

        return Result;
    }

    private MGTextRunShadowConfig GetShadow(ReadOnlySpan<char> Value)
    {
        var Cache = ShadowCache ??= new();
        if (!Cache.TryGet(Value, out var Result))
        {
            var Key = new string(Value);
            Result = MGTextRunConfig.ParseShadowValue(Key);
            Cache.Add(Key, Result);
        }

        return Result;
    }

    private ParsedImage GetImage(ReadOnlySpan<char> Value)
    {
        var Cache = ImageCache ??= new();
        if (!Cache.TryGet(Value, out var Result))
        {
            var Key = new string(Value);
            (var SourceName, var Width, var Height) = FTTokenizer.ParseImageValue(Key);
            Result = new ParsedImage(SourceName, Width ?? 0, Height ?? 0);
            Cache.Add(Key, Result);
        }

        return Result;
    }

    /// <summary>The name of a tooltip or of an action. The legacy parser reads the whole value as the name.</summary>
    private string GetName(ReadOnlySpan<char> Value)
    {
        var Cache = NameCache ??= new();
        if (!Cache.TryGet(Value, out var Result))
        {
            Result = new string(Value);
            Cache.Add(Result, Result);
        }

        return Result;
    }

    //  ---- Grammar ----

    /// <summary>The characters of a letter or of a digit that the legacy regular expressions would compare by Unicode or case rules this scanner does not reproduce make the text ambiguous.</summary>
    private bool IsDigit(char Value)
    {
        if (Value >= '0' && Value <= '9')
        {
            return true;
        }

        if (Value > 127 && char.IsDigit(Value))
        {
            IsAmbiguous = true;
            return true;
        }

        return false;
    }

    private int IndexOfCloseTag(int Start)
    {
        for (var i = Start; i < SourceLength; i++)
        {
            if (Source[i] == ']')
            {
                return i;
            }
        }

        return -1;
    }

    private bool Contains(int Start, int End, char Value)
    {
        for (var i = Start; i < End; i++)
        {
            if (Source[i] == Value)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The type of the tag that starts at <paramref name="Start"/> (after the '['), matched in the order of the legacy definitions and without case.</summary>
    private bool MatchType(int Start, out TagKind Kind, out bool IsClose, out int AfterType)
    {
        var At = Start;
        IsClose = Source[At] == '\\' || Source[At] == '/';
        if (IsClose)
        {
            At++;
        }

        //  bold | b(?=\])
        if (MatchWord(At, "bold", out AfterType) || MatchLetter(At, 'b', "]", out AfterType))
        {
            Kind = TagKind.Bold;
            return true;
        }

        if (MatchWord(At, "italic", out AfterType) || MatchLetter(At, 'i', "]", out AfterType))
        {
            Kind = TagKind.Italic;
            return true;
        }

        if (MatchWord(At, "opacity", out AfterType) || MatchLetter(At, 'o', "=]", out AfterType))
        {
            Kind = TagKind.Opacity;
            return true;
        }

        if (MatchWord(At, "underline", out AfterType) || MatchLetter(At, 'u', "=]", out AfterType))
        {
            Kind = TagKind.Underline;
            return true;
        }

        if (MatchWord(At, "fg", out AfterType) || MatchWord(At, "foreground", out AfterType) || MatchWord(At, "color", out AfterType) || MatchLetter(At, 'c', "=]", out AfterType))
        {
            Kind = TagKind.Foreground;
            return true;
        }

        if (MatchWord(At, "bg", out AfterType) || MatchWord(At, "background", out AfterType))
        {
            Kind = TagKind.Background;
            return true;
        }

        if (MatchWord(At, "shadow", out AfterType) || MatchLetter(At, 's', "=]", out AfterType))
        {
            Kind = TagKind.Shadow;
            return true;
        }

        if (!IsClose && (MatchWord(At, "image", out AfterType) || MatchWord(At, "img", out AfterType)) && AfterType < SourceLength && Source[AfterType] == '=')
        {
            Kind = TagKind.Image;
            return true;
        }

        if (MatchWord(At, "tooltip", out AfterType) || MatchWord(At, "tt", out AfterType))
        {
            Kind = TagKind.ToolTip;
            return true;
        }

        if (MatchWord(At, "action", out AfterType) || MatchWord(At, "command", out AfterType))
        {
            Kind = TagKind.Action;
            return true;
        }

        //  No type. A non ASCII character among the first ones could still be a letter the case insensitive comparison of the legacy pattern would accept.
        for (var i = At; i < SourceLength && i < At + 10; i++)
        {
            if (Source[i] > 127)
            {
                IsAmbiguous = true;
                break;
            }
        }

        Kind = default;
        AfterType = 0;
        return false;
    }

    private bool MatchWord(int At, string Word, out int AfterWord)
    {
        AfterWord = At;
        if (At + Word.Length > SourceLength)
        {
            return false;
        }

        for (var i = 0; i < Word.Length; i++)
        {
            var c = Source[At + i];
            if (c == 'I' && Word[i] == 'i' && !FoldsUpperI)
            {
                IsAmbiguous = true;
                return false;
            }

            if (c >= 'A' && c <= 'Z')
            {
                c = (char)(c + 32);
            }

            if (c != Word[i])
            {
                return false;
            }
        }

        AfterWord = At + Word.Length;
        return true;
    }

    /// <summary>A one letter type, which only counts when it is followed by one of <paramref name="Followers"/>.</summary>
    private bool MatchLetter(int At, char Letter, string Followers, out int AfterLetter)
    {
        AfterLetter = At;
        if (At + 1 >= SourceLength)
        {
            return false;
        }

        var c = Source[At];
        if (c == 'I' && Letter == 'i' && !FoldsUpperI)
        {
            IsAmbiguous = true;
            return false;
        }

        if (c >= 'A' && c <= 'Z')
        {
            c = (char)(c + 32);
        }

        if (c != Letter || Followers.IndexOf(Source[At + 1]) < 0)
        {
            return false;
        }

        AfterLetter = At + 1;
        return true;
    }

    /// <summary>The color of a tag: <c>RGB(r, g, b)</c>, <c>RGBA(r, g, b, a)</c>, <c>#RRGGBB</c>, <c>#RRGGBBAA</c> or a name, then an optional <c> * 0.5</c>.</summary>
    private bool MatchColor(int Start, out int End)
    {
        End = Start;
        if (Start >= SourceLength)
        {
            return false;
        }

        if (!MatchRgb(Start, out End))
        {
            var c = Source[Start];
            if (c == '#')
            {
                var i = Start + 1;
                while (i < SourceLength && i < Start + 9 && IsHexDigit(Source[i]))
                {
                    i++;
                }

                if (i - (Start + 1) < 6)
                {
                    return false;
                }

                End = i;
            }
            else
            {
                var i = Start;
                while (i < SourceLength && IsAsciiLetter(Source[i]))
                {
                    i++;
                }

                if (i == Start)
                {
                    return false;
                }

                End = i;
            }
        }

        //  ( ?\* ?\d+(\.\d+)?)?
        var k = End;
        if (k < SourceLength && Source[k] == ' ')
        {
            k++;
        }

        if (k < SourceLength && Source[k] == '*')
        {
            k++;
            if (k < SourceLength && Source[k] == ' ')
            {
                k++;
            }

            var DigitsStart = k;
            while (k < SourceLength && IsDigit(Source[k]))
            {
                k++;
            }

            if (k > DigitsStart)
            {
                if (k < SourceLength && Source[k] == '.')
                {
                    var FractionStart = k + 1;
                    var m = FractionStart;
                    while (m < SourceLength && IsDigit(Source[m]))
                    {
                        m++;
                    }

                    if (m > FractionStart)
                    {
                        k = m;
                    }
                }

                End = k;
            }
        }

        return true;
    }

    private bool MatchRgb(int Start, out int End)
    {
        End = Start;
        if (!MatchWord(Start, "rgb", out var i))
        {
            return false;
        }

        if (i < SourceLength && (Source[i] == 'a' || Source[i] == 'A'))
        {
            i++;
        }

        if (i >= SourceLength || Source[i] != '(')
        {
            return false;
        }

        i++;
        if (!MatchByteNumber(ref i, ','))
        {
            return false;
        }

        i++;
        SkipOptionalSpace(ref i);
        if (!MatchByteNumber(ref i, ','))
        {
            return false;
        }

        i++;
        SkipOptionalSpace(ref i);
        if (!MatchByteNumber(ref i, ')', ','))
        {
            return false;
        }

        if (Source[i] == ',')
        {
            i++;
            SkipOptionalSpace(ref i);
            if (!MatchByteNumber(ref i, ')'))
            {
                return false;
            }
        }

        End = i + 1;
        return true;
    }

    private void SkipOptionalSpace(ref int i)
    {
        if (i < SourceLength && Source[i] == ' ')
        {
            i++;
        }
    }

    /// <summary>One to three digits followed by <paramref name="Follower"/> (or <paramref name="OtherFollower"/>), which is not consumed.</summary>
    private bool MatchByteNumber(ref int i, char Follower, char OtherFollower = '\0')
    {
        var Start = i;
        while (i < SourceLength && i < Start + 3 && IsDigit(Source[i]))
        {
            i++;
        }

        if (i == Start || i >= SourceLength || (Source[i] != Follower && (OtherFollower == '\0' || Source[i] != OtherFollower)))
        {
            return false;
        }

        return true;
    }

    /// <summary>The optional offset of a shadow, <c> -?\d{1,2}( |,)-?\d{1,2}</c>: returns where the value ends.</summary>
    private int MatchShadowOffset(int Start)
    {
        if (Start >= SourceLength || Source[Start] != ' ')
        {
            return Start;
        }

        var i = Start + 1;
        if (i < SourceLength && Source[i] == '-')
        {
            i++;
        }

        var Run = 0;
        while (i + Run < SourceLength && IsDigit(Source[i + Run]))
        {
            Run++;
        }

        var Length = Math.Min(Run, 2);
        while (Length >= 1 && !(i + Length < SourceLength && (Source[i + Length] == ' ' || Source[i + Length] == ',')))
        {
            Length--;
        }

        if (Length < 1)
        {
            return Start;
        }

        i += Length + 1;
        if (i < SourceLength && Source[i] == '-')
        {
            i++;
        }

        var Second = 0;
        while (Second < 2 && i + Second < SourceLength && IsDigit(Source[i + Second]))
        {
            Second++;
        }

        return Second < 1 ? Start : i + Second;
    }

    private static bool IsAsciiLetter(char Value) => (Value >= 'a' && Value <= 'z') || (Value >= 'A' && Value <= 'Z');

    private static bool IsHexDigit(char Value) => (Value >= '0' && Value <= '9') || (Value >= 'a' && Value <= 'f') || (Value >= 'A' && Value <= 'F');
}
