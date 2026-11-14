using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Text;

/// <summary>A piece of text of a <see cref="PlainTextLine"/>: <see cref="Length"/> characters starting at <see cref="Start"/> in <see cref="PlainTextLayout"/>'s own character buffer.
/// An empty piece (<see cref="Length"/> is 0) is the empty run that <see cref="MGTextLine.ParseLines(ITextMeasurer, double, bool, IEnumerable{MGTextRun}, bool)"/> puts in an empty line.</summary>
internal readonly struct PlainTextPiece
{
    public readonly int Start;
    public readonly int Length;

    public PlainTextPiece(int Start, int Length)
    {
        this.Start = Start;
        this.Length = Length;
    }
}

/// <summary>One line of a <see cref="PlainTextLayout"/>: the same numbers an <see cref="MGTextLine"/> holds for the same text, without an object per line.</summary>
internal readonly struct PlainTextLine
{
    /// <summary>Index of the first piece of the line in the layout's pieces. The pieces of a line are consecutive.</summary>
    public readonly int FirstPiece;
    public readonly int PieceCount;
    public readonly float Width;
    public readonly float TextHeight;
    public readonly float TotalHeight;
    public readonly bool EndsInLinebreak;

    public PlainTextLine(int FirstPiece, int PieceCount, float Width, float TextHeight, float TotalHeight, bool EndsInLinebreak)
    {
        this.FirstPiece = FirstPiece;
        this.PieceCount = PieceCount;
        this.Width = Width;
        this.TextHeight = TextHeight;
        this.TotalHeight = TotalHeight;
        this.EndsInLinebreak = EndsInLinebreak;
    }
}

/// <summary>
/// The lines of a plain text (no inline formatting, no explicit runs, no image), computed without allocating once its buffers have grown.<para/>
/// <see cref="Build"/> reproduces what <see cref="MGTextLine.ParseLines(ITextMeasurer, double, bool, IEnumerable{MGTextRun}, bool)"/> makes of the runs that
/// <see cref="FTTokenizer.TokenizeLineBreaks"/> and <see cref="MGTextRun.ParseRuns(List{FTTokenMatch}, MGTextRunConfig)"/> make of the same text: the same lines, the same
/// pieces of text per line, the same widths and heights, with the same quirks (for example the wrapping decision of the words that follow a word cut in several lines
/// ignores the width of the pieces already put in the line). The code follows that method step by step on purpose: a difference is a change of behavior.<para/>
/// Plain text is a sequence of text runs separated by line break runs, all text runs sharing the same <see cref="MGTextRunConfig"/>, and two text runs are never adjacent,
/// so a word never continues in the next run (no <c>HasNext</c> word).
/// </summary>
internal sealed class PlainTextLayout
{
    /// <summary>Same value as <c>MultiLineWordSuffix</c> of <see cref="MGTextLine"/>: appended to a piece that ends in the middle of a word.</summary>
    private const char MultiLineWordSuffix = '-';
    private const string MultiLineWordSuffixText = "-";

    private readonly List<PlainTextLine> Lines = new();
    private readonly List<PlainTextPiece> Pieces = new();
    private char[] Chars = new char[64];
    private int CharCount;
    private char[] SourceChars = new char[64];
    private int SourceLength;

    //  State of the line being built
    private MGTextBlock Owner;
    private bool IsBold;
    private bool IsItalic;
    private double MaxLineWidth;
    private bool IgnoreEmptySpaceLines;
    private float MinLineHeight;
    private int CurrentFirstPiece;
    private int CurrentPieceCount;
    private int CurrentCharStart;
    private float CurrentX;
    private float CommittedRunsWidth;
    private bool HasPreviousLine;
    private bool PreviousLineEndsInLinebreak;
    private bool IsTerminated;

    /// <summary>The settings of the text runs the lines were computed with.</summary>
    public MGTextRunConfig Settings { get; private set; }

    /// <summary>The width the lines were computed for.</summary>
    public int ParseWidth { get; private set; }

    /// <summary>Whether the lines were computed with word wrapping.</summary>
    public bool WrapText { get; private set; }

    public int LineCount => Lines.Count;

    public PlainTextLine GetLine(int Index) => Lines[Index];

    public PlainTextPiece GetPiece(int Index) => Pieces[Index];

    public ReadOnlySpan<char> GetPieceText(PlainTextPiece Piece) => new(Chars, Piece.Start, Piece.Length);

    /// <summary>The characters of the text the lines were computed from (only kept when <c>KeepSource</c> was true).</summary>
    public ReadOnlySpan<char> Source => new(SourceChars, 0, SourceLength);

    /// <summary>Computes the lines of <paramref name="Text"/>, replacing the previous ones.</summary>
    /// <param name="Owner">Measures the pieces of text and resolves the fonts.</param>
    /// <param name="MaxLineWidth">Same meaning as the <c>MaxLineWidth</c> of <see cref="MGTextLine.ParseLines(ITextMeasurer, double, bool, IEnumerable{MGTextRun}, bool)"/>.</param>
    /// <param name="Settings">The settings of every text run: only the bold and italic flags matter to the layout.</param>
    /// <param name="KeepSource">If true, a copy of <paramref name="Text"/> is kept (<see cref="Source"/>), so the public lines can be built later from the text these lines are of.</param>
    public void Build(MGTextBlock Owner, ReadOnlySpan<char> Text, double MaxLineWidth, bool WrapText, bool IgnoreEmptySpaceLines, MGTextRunConfig Settings, bool KeepSource)
    {
        Lines.Clear();
        Pieces.Clear();
        CharCount = 0;
        SourceLength = 0;
        this.Settings = Settings;
        ParseWidth = (int)MaxLineWidth;
        this.WrapText = WrapText;
        CurrentFirstPiece = 0;
        CurrentPieceCount = 0;
        CurrentCharStart = 0;
        CurrentX = 0;
        CommittedRunsWidth = 0;
        HasPreviousLine = false;
        PreviousLineEndsInLinebreak = false;
        IsTerminated = false;

        if (KeepSource)
        {
            if (SourceChars.Length < Text.Length)
            {
                SourceChars = new char[GrownLength(SourceChars.Length, Text.Length)];
            }

            Text.CopyTo(SourceChars);
            SourceLength = Text.Length;
        }

        if (Text.IsEmpty || MaxLineWidth < 1)
        {
            return;
        }

        this.Owner = Owner;
        IsBold = Settings.IsBold;
        IsItalic = Settings.IsItalic;
        this.MaxLineWidth = MaxLineWidth;
        this.IgnoreEmptySpaceLines = IgnoreEmptySpaceLines;
        MinLineHeight = (float)Math.Ceiling(Owner.MeasureText(" ", false, false).Y);

        var Wraps = WrapText && MaxLineWidth != double.MaxValue && MaxLineWidth < 100000;

        var Index = 0;
        while (Index < Text.Length)
        {
            var SegmentStart = Index;
            while (Index < Text.Length && Text[Index] != '\r' && Text[Index] != '\n')
            {
                Index++;
            }

            if (Index > SegmentStart)
            {
                var Run = Text.Slice(SegmentStart, Index - SegmentStart);
                if (Wraps)
                {
                    LayoutWrappedRun(Run);
                    if (IsTerminated)
                    {
                        return;
                    }
                }
                else
                {
                    AddPiece(Run, false);
                }
            }

            if (Index < Text.Length)
            {
                //  A line break run: "\r\n" is one line break, as the tokenizer's (\r\n|\r|\n) pattern matches it
                Index += Text[Index] == '\r' && Index + 1 < Text.Length && Text[Index + 1] == '\n' ? 2 : 1;
                if (CurrentPieceCount == 0)
                {
                    AddPiece(ReadOnlySpan<char>.Empty, false);
                }

                FlushLine(true);
            }
        }

        if (CurrentPieceCount == 0)
        {
            AddPiece(ReadOnlySpan<char>.Empty, false);
        }

        FlushLine(false);
    }

    private void LayoutWrappedRun(ReadOnlySpan<char> Run)
    {
        //  The text not yet in a line is Run[UnwrappedStart..Position): the words that fit, which the wrapping decision measures again with the next word.
        var Position = 0;
        var UnwrappedStart = 0;
        while (Position < Run.Length)
        {
            var WordLength = GetWordLength(Run, Position);
            var Word = Run.Slice(Position, WordLength);
            var CurrentWidth = Measure(Word).X;
            var TotalWidth = CurrentWidth;

            var Candidate = Run.Slice(UnwrappedStart, Position + WordLength - UnwrappedStart);
            float ProjectedWidth = CommittedRunsWidth + Measure(Candidate).X;

            if (ProjectedWidth <= MaxLineWidth)
            {
                CurrentX += CurrentWidth;
                Position += WordLength;
            }
            else
            {
                if (Position > UnwrappedStart)
                {
                    AddPiece(Run.Slice(UnwrappedStart, Position - UnwrappedStart), false);
                }

                if (CurrentPieceCount > 0)
                {
                    FlushLine(false);
                }

                UnwrappedStart = Position;

                if (TotalWidth <= MaxLineWidth)
                {
                    CurrentX += CurrentWidth;
                    Position += WordLength;
                }
                else
                {
                    //  This word is wider than an entire line, so it is split up into several pieces
                    double MaxLineSuffixWidth = Measure(MultiLineWordSuffixText.AsSpan()).X;
                    if (MaxLineSuffixWidth >= MaxLineWidth)
                    {
                        if (CurrentPieceCount > 0)
                        {
                            FlushLine(false);
                        }

                        IsTerminated = true;
                        return;
                    }

                    LayoutLongWord(Word, MaxLineSuffixWidth);
                    Position += WordLength;
                    UnwrappedStart = Position;
                }
            }
        }

        if (Position > UnwrappedStart)
        {
            var CommittedText = Run.Slice(UnwrappedStart, Position - UnwrappedStart);
            AddPiece(CommittedText, false);
            CommittedRunsWidth += Measure(CommittedText).X;
        }
    }

    /// <summary>Puts a word wider than a line in pieces. Mirrors the branch of <see cref="MGTextLine.ParseLines(ITextMeasurer, double, bool, IEnumerable{MGTextRun}, bool)"/>
    /// that handles such a word: as there, the words that follow are positioned without counting the width of the last piece left in the line.</summary>
    private void LayoutLongWord(ReadOnlySpan<char> Word, double MaxLineSuffixWidth)
    {
        //  The part of the word not yet in a piece is Word[PendingStart..PendingStart + PendingLength)
        var PendingStart = 0;
        var PendingLength = 0;

        var WordWidth = Measure(Word).X;
        if (CurrentX + WordWidth + MaxLineSuffixWidth <= MaxLineWidth)
        {
            PendingLength = Word.Length;
            CurrentX += WordWidth;
        }
        else
        {
            for (var CharIndex = 0; CharIndex < Word.Length; CharIndex++)
            {
                var CharacterWidth = Measure(Word.Slice(CharIndex, 1)).X;
                //  Ensure we at least have 1 character per line to avoid infinite loop
                var FitsOnCurrentLine = (CurrentPieceCount == 0 && PendingLength == 0) ||
                                        (CurrentX + CharacterWidth + MaxLineSuffixWidth <= MaxLineWidth);

                if (FitsOnCurrentLine)
                {
                    PendingLength++;
                    CurrentX += CharacterWidth;
                }
                else
                {
                    AddPiece(Word.Slice(PendingStart, PendingLength), true);
                    FlushLine(false);

                    PendingStart = CharIndex;
                    PendingLength = 0;
                    CharIndex--;
                }
            }
        }

        if (PendingLength > 0)
        {
            AddPiece(Word.Slice(PendingStart, PendingLength), false);
        }
    }

    /// <summary>The length of the word that starts at <paramref name="Position"/>: a delimiter (space or hyphen) is a word of its own, as
    /// <c>SplitAndKeepDelimiters</c> splits it.</summary>
    private static int GetWordLength(ReadOnlySpan<char> Run, int Position)
    {
        if (IsDelimiter(Run[Position]))
        {
            return 1;
        }

        var End = Position + 1;
        while (End < Run.Length && !IsDelimiter(Run[End]))
        {
            End++;
        }

        return End - Position;
    }

    private static bool IsDelimiter(char Value) => Value == ' ' || Value == '-';

    private Vector2 Measure(ReadOnlySpan<char> Text) => Owner.MeasureText(Text, IsBold, IsItalic);

    private void AddPiece(ReadOnlySpan<char> Text, bool WithSuffix)
    {
        var Length = Text.Length + (WithSuffix ? 1 : 0);
        if (Chars.Length < CharCount + Length)
        {
            var Grown = new char[GrownLength(Chars.Length, CharCount + Length)];
            Array.Copy(Chars, Grown, CharCount);
            Chars = Grown;
        }

        Text.CopyTo(Chars.AsSpan(CharCount));
        if (WithSuffix)
        {
            Chars[CharCount + Text.Length] = MultiLineWordSuffix;
        }

        Pieces.Add(new PlainTextPiece(CharCount, Length));
        CharCount += Length;
        CurrentPieceCount++;
    }

    /// <summary>Ends the line being built: measures each of its pieces as a complete string, like <c>FlushLine</c> of <see cref="MGTextLine"/>.</summary>
    private void FlushLine(bool EndsInLinebreak)
    {
        double WidthSum = 0;
        float TextHeight = 0;
        for (var i = 0; i < CurrentPieceCount; i++)
        {
            var Piece = Pieces[CurrentFirstPiece + i];
            //  An empty piece belongs to the empty run of an empty line, whose settings are not those of the text.
            var Size = Piece.Length == 0 ? Vector2.Zero : Measure(GetPieceText(Piece));
            WidthSum += Size.X;
            TextHeight = i == 0 ? Size.Y : Math.Max(TextHeight, Size.Y);
        }

        var Width = (float)WidthSum + 0f;
        var TotalHeight = Math.Max(Math.Max(MinLineHeight, TextHeight), 0f);

        var IsIgnoreable = IgnoreEmptySpaceLines && HasPreviousLine && !PreviousLineEndsInLinebreak &&
                           CurrentPieceCount == 1 && Pieces[CurrentFirstPiece].Length == 1 && Chars[Pieces[CurrentFirstPiece].Start] == ' ';
        HasPreviousLine = true;
        PreviousLineEndsInLinebreak = EndsInLinebreak;

        if (IsIgnoreable)
        {
            Pieces.RemoveRange(CurrentFirstPiece, CurrentPieceCount);
            CharCount = CurrentCharStart;
        }
        else
        {
            Lines.Add(new PlainTextLine(CurrentFirstPiece, CurrentPieceCount, Width, TextHeight, TotalHeight, EndsInLinebreak));
        }

        CurrentFirstPiece = Pieces.Count;
        CurrentPieceCount = 0;
        CurrentCharStart = CharCount;
        CurrentX = 0;
        CommittedRunsWidth = 0;
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
