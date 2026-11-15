using Microsoft.Xna.Framework;
using MGUI.Shared.Helpers;
using System.Globalization;
using ColorTranslator = System.Drawing.ColorTranslator;
using System.Text;
using System.Text.RegularExpressions;
using MGUI.Core.UI.Brushes.FillBrushes;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Core.UI.XAML;
using Thickness = MonoGame.Extended.Thickness;

namespace MGUI.Core.UI.Text;

public record struct MGTextRunUnderlineConfig(bool IsEnabled = false, int Height = 1, int VerticalOffset = 0, IFillBrush Brush = null);

public record struct MGTextRunBackgroundConfig(IFillBrush Brush = null, Thickness Padding = default)
{
    public static readonly MGTextRunBackgroundConfig Empty = new(null, default);
    public bool HasBackground => Brush != null;
}

public record struct MGTextRunShadowConfig(Color? ShadowColor = null, Vector2? ShadowOffset = null)
{
    public static readonly MGTextRunShadowConfig Empty = new(null, null);
    public bool IsShadowed => ShadowColor.HasValue;
}

public record struct MGTextRunConfig(bool IsBold, bool IsItalic = false, float Opacity = 1.0f,
    Color? Foreground = null, MGTextRunUnderlineConfig Underline = new(), MGTextRunBackgroundConfig Background = new(), MGTextRunShadowConfig Shadow = new())
{
    public bool HasBackground => Background.HasBackground;
    public bool IsShadowed => Shadow.IsShadowed;

    public MGTextRunConfig ApplyAction(FTAction Action, IList<float> PreviousOpacities, IList<Color?> PreviousForegrounds,
        IList<MGTextRunUnderlineConfig> PreviousUnderlines, IList<MGTextRunBackgroundConfig> PreviousBackgrounds, IList<MGTextRunShadowConfig> PreviousShadows)
    {
        //  A closing tag with no opening one before it changes nothing: each revert below falls back to the current value.
        switch (Action.ActionType)
        {
            //  Bold
            case FTActionType.EnableBold:
                return this with { IsBold = true };
            case FTActionType.DisableBold:
                return this with { IsBold = false };

            //  Italic
            case FTActionType.EnableItalic:
                return this with { IsItalic = true };
            case FTActionType.DisableItalic:
                return this with { IsItalic = false };

            //  Underline
            case FTActionType.SetUnderline:
            {
                PreviousUnderlines.Add(Underline);
                MGTextRunUnderlineConfig UnderlineConfig = new(true);
                if (!string.IsNullOrEmpty(Action.Parameter))
                {
                    UnderlineConfig = ParseUnderlineValue(Action.Parameter.Substring(1));
                }
                return this with { Underline = UnderlineConfig };
            }
            case FTActionType.RevertUnderline:
                return this with { Underline = GeneralUtils.RemoveLast(PreviousUnderlines, Underline) };

            //  Opacity
            case FTActionType.SetOpacity:
            {
                PreviousOpacities.Add(Opacity);
                var Value = float.Parse(Action.Parameter.Substring(1), CultureInfo.InvariantCulture);
                return this with { Opacity = Value };
            }
            case FTActionType.RevertOpacity:
                return this with { Opacity = GeneralUtils.RemoveLast(PreviousOpacities, Opacity) };

            //  Foreground
            case FTActionType.SetForeground:
            {
                PreviousForegrounds.Add(Foreground);
                var Value = ParseForegroundValue(Action.Parameter.Substring(1));
                return this with { Foreground = Value };
            }
            case FTActionType.RevertForeground:
                return this with { Foreground = GeneralUtils.RemoveLast(PreviousForegrounds, Foreground) };

            //  Background
            case FTActionType.SetBackground:
            {
                PreviousBackgrounds.Add(Background);
                return this with { Background = ParseBackgroundValue(Action.Parameter.Substring(1)) };
            }
            case FTActionType.RevertBackground:
                return this with { Background = GeneralUtils.RemoveLast(PreviousBackgrounds, Background) };

            //  Shadowing
            case FTActionType.SetShadowing:
            {
                PreviousShadows.Add(Shadow);
                return this with { Shadow = ParseShadowValue(Action.Parameter.Substring(1)) };
            }
            case FTActionType.RevertShadowing:
                return this with { Shadow = GeneralUtils.RemoveLast(PreviousShadows, Shadow) };

            //  Image
            case FTActionType.Image:
                return this;

            //  ToolTip
            case FTActionType.SetToolTip:
                return this;
            case FTActionType.RevertToolTip:
                return this;

            //  Action
            case FTActionType.SetAction:
                return this;
            case FTActionType.RevertAction:
                return this;

            //  String value
            case FTActionType.StringLiteral:
                return this;

            //  LineBreak
            case FTActionType.LineBreak:
                return this;
            default: throw new NotImplementedException($"Unrecognized {nameof(FTActionType)}: {Action.ActionType}");
        }
    }

    internal readonly MGTextRunConfig WithForeground(Color? Value) => this with { Foreground = Value };

    //  The interpretation of the value of a tag (the text after the '=' sign), shared by ApplyAction and the allocation-free scanner (FormattedTextScanner),
    //  so both read a value the same way.

    /// <summary>The color of a <c>[color=...]</c> tag.</summary>
    internal static Color ParseForegroundValue(string Value)
        => ColorStringConverter.ParseColor(Value).ToXNAColor(); //ColorTranslator.FromHtml(Value).AsXNAColor();

    /// <summary>The underline of a <c>[underline=...]</c> tag with a (possibly empty) value.</summary>
    internal static MGTextRunUnderlineConfig ParseUnderlineValue(string Value)
    {
        (var Height, var VerticalOffset, var Brush) = FTTokenizer.ParseUnderlineValue(Value);
        return new MGTextRunUnderlineConfig(true, Height ?? 1, VerticalOffset ?? 0, Brush);
    }

    /// <summary>The background of a <c>[background=...]</c> tag.</summary>
    internal static MGTextRunBackgroundConfig ParseBackgroundValue(string Value)
    {
        (var Brush, var Padding) = FTTokenizer.ParseBackgroundValue(Value);
        return new MGTextRunBackgroundConfig(Brush, Padding);
    }

    //  The offset that may end the value of a shadow: two numbers separated by a space or a comma, as the tokenizer accepts them.
    private static readonly Regex ShadowOffsetParser = new(@" (?<X>-?\d{1,2})( |,)(?<Y>-?\d{1,2})$", RegexOptions.CultureInvariant);

    /// <summary>The shadow of a <c>[shadow=...]</c> tag: a color, then an optional offset (<c>Red 1 2</c> or <c>Red 1,2</c>; (1,1) without one).</summary>
    internal static MGTextRunShadowConfig ParseShadowValue(string Value)
    {
        var ColorString = Value;
        Vector2 ParsedShadowOffset = new(1, 1);
        var Match = ShadowOffsetParser.Match(Value);
        if (Match.Success)
        {
            ColorString = Value.Substring(0, Match.Index);
            ParsedShadowOffset = new(int.Parse(Match.Groups["X"].Value, CultureInfo.InvariantCulture), int.Parse(Match.Groups["Y"].Value, CultureInfo.InvariantCulture));
        }

        var ParsedShadowColor = ColorTranslator.FromHtml(ColorString).AsXNAColor();
        return new MGTextRunShadowConfig(ParsedShadowColor, ParsedShadowOffset);
    }
}

public enum TextRunType
{
    Text,
    LineBreak,
    Image
}

/// <summary>Represents data that can be rendered in-line with other <see cref="MGTextRun"/>s.<para/>
/// See also:<br/><see cref="MGTextRunText"/><br/><see cref="MGTextRunLineBreak"/><br/><see cref="MGTextRunImage"/></summary>
public abstract class MGTextRun
{
    /// <summary>The number of spaces to convert tab characters into before parsing <see cref="MGTextRun"/>s.<para/>
    /// Default value: 4</summary>
    public const int TabSpacesCount = 4;
    public static readonly string TabReplacement = string.Join("", Enumerable.Repeat(" ", TabSpacesCount));

    public static readonly FTParser Parser = new();
    public static readonly FTTokenizer Tokenizer = new();

    public readonly TextRunType RunType;

    public readonly string ToolTipId;
    public bool HasToolTip => !string.IsNullOrEmpty(ToolTipId);

    public readonly string ActionId;
    public bool HasAction => !string.IsNullOrEmpty(ActionId);

    protected MGTextRun(TextRunType RunType, string ToolTipId, string ActionId)
    {
        this.RunType = RunType;
        this.ToolTipId = ToolTipId;
        this.ActionId = ActionId;
    }

    public static IEnumerable<MGTextRun> ParseRuns(string FormattedText, MGTextRunConfig DefaultSettings)
    {
        if (string.IsNullOrEmpty(FormattedText))
        {
            yield break;
        }

        FormattedText = FormattedText.Replace("\t", TabReplacement);

        //  The tokenizer only returns the tokens of well formed tags (see IsWellFormedTag), so no markup makes the parsing below fail.
        var Tokens = Tokenizer.Tokenize(FormattedText, true).ToList();
        foreach (var Run in ParseRuns(Tokens, DefaultSettings))
        {
            yield return Run;
        }
    }

    /// <summary>True if <paramref name="TagTokens"/>, the tokens of one tag from <see cref="FTTokenType.OpenTag"/> to <see cref="FTTokenType.CloseTag"/>, are a tag the parser knows whose value
    /// can be interpreted (a color that exists, a number that fits, ...). The tokenizer asks, and reads the open tag character of anything else as a literal character (MGUI ADR-0030).</summary>
    internal static bool IsWellFormedTag(IReadOnlyList<FTTokenMatch> TagTokens)
    {
        try
        {
            var Count = 0;
            foreach (var Action in Parser.ParseTokens(TagTokens))
            {
                Count++;
                if (Action.ActionType == FTActionType.Image)
                {
                    FTTokenizer.ParseImageValue(Action.Parameter.Substring(1));
                }
                else
                {
                    //  Interprets the value as the parsing of the runs will, on settings and lists that are thrown away
                    default(MGTextRunConfig).ApplyAction(Action, new List<float>(), new List<Color?>(), new List<MGTextRunUnderlineConfig>(), new List<MGTextRunBackgroundConfig>(), new List<MGTextRunShadowConfig>());
                }
            }

            return Count == 1;
        }
        catch (Exception)
        {
            //  The parser knows no such tag, or the interpretation of the value rejected it
            return false;
        }
    }

    public static IEnumerable<MGTextRun> ParseRuns(List<FTTokenMatch> Tokens, MGTextRunConfig DefaultSettings)
    {
        var CurrentState = DefaultSettings with { };

        List<float> PreviousOpacities = new();
        List<Color?> PreviousForegrounds = new();
        List<MGTextRunUnderlineConfig> PreviousUnderlines = new();
        List<MGTextRunBackgroundConfig> PreviousBackgrounds = new();
        List<MGTextRunShadowConfig> PreviousShadows = new();

        string CurrentToolTipId = null;
        List<string> PreviousToolTipIds = new();

        string CurrentActionId = null;
        List<string> PreviousActionIds = new();

        var Actions = Parser.ParseTokens(Tokens).ToList();
        for (var i = 0; i < Actions.Count; i++)
        {
            var CurrentAction = Actions[i];
            CurrentState = CurrentState.ApplyAction(CurrentAction, PreviousOpacities, PreviousForegrounds, PreviousUnderlines, PreviousBackgrounds, PreviousShadows);

            if (CurrentAction.ActionType == FTActionType.StringLiteral)
            {
                StringBuilder LiteralValue = new();

                var HasNextAction = true;
                var NextAction = CurrentAction;
                while (HasNextAction && NextAction.ActionType is FTActionType.StringLiteral or FTActionType.Ignore)
                {
                    CurrentAction = NextAction;
                    if (CurrentAction.ActionType == FTActionType.StringLiteral)
                    {
                        LiteralValue.Append(CurrentAction.Parameter);
                    }

                    var NextIndex = i + 1;
                    HasNextAction = NextIndex < Actions.Count;
                    NextAction = HasNextAction ? Actions[NextIndex] : default;
                    i++;
                }
                i--;

                yield return new MGTextRunText(LiteralValue.ToString(), CurrentState, CurrentToolTipId, CurrentActionId);
            }
            else if (CurrentAction.ActionType == FTActionType.LineBreak)
            {
                yield return new MGTextRunLineBreak(CurrentAction.Parameter?.Length ?? 1);
            }
            else if (CurrentAction.ActionType == FTActionType.Image)
            {
                var (SourceName, TargetWidth, TargetHeight) = FTTokenizer.ParseImageValue(CurrentAction.Parameter.Substring(1));
                yield return new MGTextRunImage(SourceName, TargetWidth ?? 0, TargetHeight ?? 0, CurrentToolTipId, CurrentActionId);
            }
            else if (CurrentAction.ActionType == FTActionType.SetToolTip)
            {
                if (!string.IsNullOrEmpty(CurrentToolTipId))
                {
                    PreviousToolTipIds.Add(CurrentToolTipId);
                }

                var NewToolTipValue = CurrentAction.Parameter.Substring(1);
                CurrentToolTipId = FTTokenizer.ToolTipValueParser.Match(NewToolTipValue).Groups["ToolTipName"].Value;
            }
            else if (CurrentAction.ActionType == FTActionType.RevertToolTip)
            {
                CurrentToolTipId = GeneralUtils.RemoveLast(PreviousToolTipIds, null);
            }
            else if (CurrentAction.ActionType == FTActionType.SetAction)
            {
                if (!string.IsNullOrEmpty(CurrentActionId))
                {
                    PreviousActionIds.Add(CurrentActionId);
                }

                var NewActionValue = CurrentAction.Parameter.Substring(1);
                CurrentActionId = FTTokenizer.ActionValueParser.Match(NewActionValue).Groups["ActionName"].Value;
            }
            else if (CurrentAction.ActionType == FTActionType.RevertAction)
            {
                CurrentActionId = GeneralUtils.RemoveLast(PreviousActionIds, null);
            }
        }
    }
}

/// <summary>An <see cref="MGTextRun"/> that renders a piece of text with the given <see cref="MGTextRunConfig"/> settings.</summary>
public class MGTextRunText : MGTextRun
{
    public readonly string Text;
    public readonly MGTextRunConfig Settings;

    public MGTextRunText(string Text, MGTextRunConfig Settings, string ToolTipId, string ActionId)
        : base(TextRunType.Text, ToolTipId, ActionId)
    {
        this.Text = Text;
        this.Settings = Settings;
    }

    public string FirstWord(params char[] WordDelimiters) => new string(Text.TakeWhile(x => !WordDelimiters.Contains(x)).ToArray());

    public bool HasAny(params char[] characters)
    {
        foreach (var c in characters)
        {
            if (Text.Contains(c))
            {
                return true;
            }
        }

        return false;
    }

    public bool StartsWithAny(params char[] characters)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return false;
        }

        foreach (var c in characters)
        {
            if (Text.StartsWith(c))
            {
                return true;
            }
        }

        return false;
    }

    public override string ToString() => $"{nameof(MGTextRunText)}: {Text?.Truncate(50) ?? ""} ({Settings})";
}

/// <summary>An <see cref="MGTextRun"/> that instructs the renderer to move to the start of a new line.</summary>
public class MGTextRunLineBreak : MGTextRun
{
    /// <summary>The number of characters used to specify the linebreak.<para/>
    /// Windows typically uses "\r\n" (2 characters) while Mac and Linux typically uses '\r' and '\n' (1 character)</summary>
    public readonly int LineBreakCharacterCount;

    /// <param name="LineBreakCharactersCount">The number of characters used to specify the linebreak.<para/>
    /// Windows typically uses "\r\n" (2 characters) while Mac and Linux typically uses '\r' and '\n' (1 character)</param>
    public MGTextRunLineBreak(int LineBreakCharactersCount)
        : base(TextRunType.LineBreak, null, null)
    {
        LineBreakCharacterCount = LineBreakCharactersCount;
    }

    public override string ToString() => $"{nameof(MGTextRunLineBreak)}: {LineBreakCharacterCount} characters";
}

/// <summary>An <see cref="MGTextRun"/> that renders a specific portion of a <see cref="Texture2D"/> with a given size.</summary>
public class MGTextRunImage : MGTextRun
{
    /// <summary>The name of the <see cref="MGTextureData"/> to render. This name should exist in <see cref="MGResources.Textures"/><para/>
    /// See also:<br/><see cref="MGDesktop.Resources"/><br/><see cref="MGElement.GetResources"/><br/><see cref="MGResources.Textures"/><br/><see cref="MGResources.AddTexture(string, MGTextureData)"/></summary>
    public readonly string SourceName;
    public readonly int TargetWidth;
    public readonly int TargetHeight;

    public MGTextRunImage(string SourceName, int TargetWidth, int TargetHeight, string ToolTipId, string ActionId)
        : base(TextRunType.Image, ToolTipId, ActionId)
    {
        this.SourceName = SourceName;
        this.TargetWidth = TargetWidth;
        this.TargetHeight = TargetHeight;
    }

    public override string ToString() => $"{nameof(MGTextRunImage)}: {SourceName} - {TargetWidth},{TargetHeight}";
}