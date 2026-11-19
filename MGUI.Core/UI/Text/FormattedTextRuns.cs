using System.Collections.ObjectModel;

namespace MGUI.Core.UI.Text;

internal enum ScannedRunKind : byte
{
    Text,
    LineBreak,
    Image
}

/// <summary>One run of a <see cref="FormattedTextRuns"/>: the same data an <see cref="MGTextRun"/> holds, without an object per run.</summary>
internal struct ScannedRun
{
    public ScannedRunKind Kind;

    /// <summary>Text runs: the characters of the run are <see cref="FormattedTextRuns.Chars"/>[<see cref="Start"/>..<see cref="Start"/> + <see cref="Length"/>).</summary>
    public int Start;
    public int Length;

    /// <summary>Line break runs: the number of characters of the line break (<see cref="MGTextRunLineBreak.LineBreakCharacterCount"/>).</summary>
    public int LineBreakCharacterCount;

    /// <summary>Text runs: the settings of the run.</summary>
    public MGTextRunConfig Settings;

    public string ToolTipId;
    public string ActionId;

    /// <summary>Image runs: the name of the texture, and the size to draw it with (an image without a size already has the default size of its texture).</summary>
    public string ImageSource;
    public int ImageWidth;
    public int ImageHeight;

    public readonly bool HasAction => !string.IsNullOrEmpty(ActionId);
}

/// <summary>
/// The runs of a formatted text (<see cref="MGTextBlock.AllowsInlineFormatting"/> true), in buffers that are reused from one text to the next: what
/// <see cref="MGTextRun.ParseRuns(string, MGTextRunConfig)"/> followed by the sanitizing of <see cref="MGTextBlock"/> make of the same text, without an object per run.
/// <see cref="FormattedTextScanner"/> fills it; <see cref="FormattedTextLayout"/> lays it out; <see cref="Materialize"/> builds the public runs from it.
/// </summary>
internal sealed class FormattedTextRuns
{
    private ScannedRun[] Items = new ScannedRun[4];
    private char[] CharBuffer = new char[64];

    public int Count { get; private set; }
    public int CharCount { get; private set; }

    /// <summary>The number of characters of the text runs (the line breaks are not counted).</summary>
    public int TextLength { get; private set; }

    /// <summary>True when a run has an action: the text block then tracks mouse clicks.</summary>
    public bool HasAction { get; private set; }

    public char[] Chars => CharBuffer;

    public ref ScannedRun this[int Index] => ref Items[Index];

    public ReadOnlySpan<char> GetText(in ScannedRun Run) => new(CharBuffer, Run.Start, Run.Length);

    public void Clear()
    {
        Count = 0;
        CharCount = 0;
        TextLength = 0;
        HasAction = false;
    }

    /// <summary>Makes room for <paramref name="Extra"/> more characters and returns the index where they go.</summary>
    public int ReserveChars(int Extra)
    {
        if (CharBuffer.Length < CharCount + Extra)
        {
            var Grown = new char[GrownLength(CharBuffer.Length, CharCount + Extra)];
            Array.Copy(CharBuffer, Grown, CharCount);
            CharBuffer = Grown;
        }

        return CharCount;
    }

    public void AppendChar(char Value)
    {
        ReserveChars(1);
        CharBuffer[CharCount++] = Value;
    }

    public void AppendChars(ReadOnlySpan<char> Value)
    {
        ReserveChars(Value.Length);
        Value.CopyTo(CharBuffer.AsSpan(CharCount));
        CharCount += Value.Length;
    }

    /// <summary>Lengthens the last run, a text run whose characters end the buffer, by the characters appended since (the text of a literal that follows it).</summary>
    public void ExtendLastTextRun(int AddedChars)
    {
        Items[Count - 1].Length += AddedChars;
        TextLength += AddedChars;
    }

    public ref ScannedRun Add(ScannedRunKind Kind)
    {
        if (Count == Items.Length)
        {
            Array.Resize(ref Items, Items.Length * 2);
        }

        ref var Run = ref Items[Count++];
        Run = default;
        Run.Kind = Kind;
        return ref Run;
    }

    /// <summary>Adds a text run over the <paramref name="Length"/> characters that were appended last.</summary>
    public void AddText(int Start, int Length, in MGTextRunConfig Settings, string ToolTipId, string ActionId)
    {
        ref var Run = ref Add(ScannedRunKind.Text);
        Run.Start = Start;
        Run.Length = Length;
        Run.Settings = Settings;
        Run.ToolTipId = ToolTipId;
        Run.ActionId = ActionId;
        TextLength += Length;
        HasAction |= Run.HasAction;
    }

    public void AddLineBreak(int LineBreakCharacterCount)
    {
        ref var Run = ref Add(ScannedRunKind.LineBreak);
        Run.LineBreakCharacterCount = LineBreakCharacterCount;
    }

    public void AddImage(string ImageSource, int Width, int Height, string ToolTipId, string ActionId)
    {
        ref var Run = ref Add(ScannedRunKind.Image);
        Run.ImageSource = ImageSource;
        Run.ImageWidth = Width;
        Run.ImageHeight = Height;
        Run.ToolTipId = ToolTipId;
        Run.ActionId = ActionId;
        HasAction |= Run.HasAction;
    }

    public void CopyFrom(FormattedTextRuns Other)
    {
        if (Items.Length < Other.Count)
        {
            Items = new ScannedRun[GrownLength(Items.Length, Other.Count)];
        }

        Array.Copy(Other.Items, Items, Other.Count);
        if (CharBuffer.Length < Other.CharCount)
        {
            CharBuffer = new char[GrownLength(CharBuffer.Length, Other.CharCount)];
        }

        Array.Copy(Other.CharBuffer, CharBuffer, Other.CharCount);
        Count = Other.Count;
        CharCount = Other.CharCount;
        TextLength = Other.TextLength;
        HasAction = Other.HasAction;
    }

    /// <summary>Builds the public runs: the objects <see cref="MGTextRun.ParseRuns(string, MGTextRunConfig)"/> would have made of the same text. The brushes of the settings are
    /// copies, so changing the brush of a public run does not change the brush the scanner keeps for the next texts.</summary>
    public ReadOnlyCollection<MGTextRun> Materialize()
    {
        List<MGTextRun> Result = new(Count);
        for (var i = 0; i < Count; i++)
        {
            ref var Run = ref Items[i];
            switch (Run.Kind)
            {
                case ScannedRunKind.Text:
                    Result.Add(new MGTextRunText(new string(CharBuffer, Run.Start, Run.Length), CopyBrushes(Run.Settings), Run.ToolTipId, Run.ActionId));
                    break;
                case ScannedRunKind.LineBreak:
                    Result.Add(new MGTextRunLineBreak(Run.LineBreakCharacterCount));
                    break;
                default:
                    Result.Add(new MGTextRunImage(Run.ImageSource, Run.ImageWidth, Run.ImageHeight, Run.ToolTipId, Run.ActionId));
                    break;
            }
        }

        return Result.AsReadOnly();
    }

    private static MGTextRunConfig CopyBrushes(MGTextRunConfig Settings)
    {
        if (Settings.Underline.Brush != null)
        {
            Settings = Settings with { Underline = Settings.Underline with { Brush = Settings.Underline.Brush.Copy() } };
        }

        if (Settings.Background.Brush != null)
        {
            Settings = Settings with { Background = Settings.Background with { Brush = Settings.Background.Brush.Copy() } };
        }

        return Settings;
    }

    private static int GrownLength(int Current, int Needed)
    {
        var Length = Math.Max(Current, 16);
        while (Length < Needed)
        {
            Length *= 2;
        }

        return Length;
    }
}
