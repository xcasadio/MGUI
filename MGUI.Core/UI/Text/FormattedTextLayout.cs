using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Text;

/// <summary>A piece of a <see cref="FormattedTextLine"/>: the same thing an <see cref="MGTextRun"/> of an <see cref="MGTextLine"/> is.</summary>
internal readonly struct FormattedTextPiece
{
    /// <summary>The run the piece is of, in <see cref="FormattedTextLayout.Source"/> (the settings, the tooltip, the action, the image), or -1 for the empty run that
    /// <see cref="MGTextLine.ParseLines(ITextMeasurer, double, bool, IEnumerable{MGTextRun}, bool)"/> puts in an empty line.</summary>
    public readonly int RunIndex;

    /// <summary>Text pieces: the characters of the piece (a part of a run, the suffix of a word that was cut included) are in <see cref="FormattedTextLayout"/>'s own character buffer.</summary>
    public readonly int Start;
    public readonly int Length;

    /// <summary>Text pieces: the index, in the text of the runs (the characters of the text runs and of the line breaks, in order), of the first character of the piece. The characters of a piece are
    /// consecutive there, except the last one when <see cref="HasSuffix"/> is true, a hyphen that wrapping inserted: it is not in the text, and takes the index of the character that follows the
    /// piece, as <see cref="MGTextLine.OriginalCharacterIndices"/> gives it.</summary>
    public readonly int OriginalStart;
    public readonly bool HasSuffix;

    public FormattedTextPiece(int RunIndex, int Start, int Length, int OriginalStart, bool HasSuffix)
    {
        this.RunIndex = RunIndex;
        this.Start = Start;
        this.Length = Length;
        this.OriginalStart = OriginalStart;
        this.HasSuffix = HasSuffix;
    }
}

/// <summary>One line of a <see cref="FormattedTextLayout"/>: the same numbers an <see cref="MGTextLine"/> holds for the same runs, without an object per line.</summary>
internal readonly struct FormattedTextLine
{
    /// <summary>Index of the first piece of the line in the layout's pieces. The pieces of a line are consecutive.</summary>
    public readonly int FirstPiece;
    public readonly int PieceCount;
    public readonly float Width;
    public readonly float TextHeight;
    public readonly float ImageHeight;
    public readonly float TotalHeight;
    public readonly bool EndsInLinebreak;

    /// <summary>The number of characters of the line break that ends the line (2 for "\r\n"), 0 when the line does not end in a line break.</summary>
    public readonly int LineBreakCharacterCount;

    /// <summary>The index, in the text of the runs, that follows the last character of the line (the first character of its line break when it has one).</summary>
    public readonly int OriginalEnd;

    public FormattedTextLine(int FirstPiece, int PieceCount, float Width, float TextHeight, float ImageHeight, float TotalHeight, bool EndsInLinebreak, int LineBreakCharacterCount, int OriginalEnd)
    {
        this.FirstPiece = FirstPiece;
        this.PieceCount = PieceCount;
        this.Width = Width;
        this.TextHeight = TextHeight;
        this.ImageHeight = ImageHeight;
        this.TotalHeight = TotalHeight;
        this.EndsInLinebreak = EndsInLinebreak;
        this.LineBreakCharacterCount = LineBreakCharacterCount;
        this.OriginalEnd = OriginalEnd;
    }
}

/// <summary>
/// The lines of a formatted text (<see cref="FormattedTextRuns"/>), computed without allocating once its buffers have grown.<para/>
/// <see cref="Build"/> reproduces what <see cref="MGTextLine.ParseLines(ITextMeasurer, double, bool, IEnumerable{MGTextRun}, bool)"/> makes of the runs
/// <see cref="MGTextRun.ParseRuns(string, MGTextRunConfig)"/> makes of the same text: the same lines, the same pieces of text per line, the same widths and heights, with the same quirks
/// (a word that continues in the next run, when there is no delimiter between two runs, is measured with it; the wrapping decision of the words that follow a word cut in several
/// lines ignores the width of the pieces already put in the line; ...). The code follows that method step by step on purpose: a difference is a change of behavior.<para/>
/// It is the generalization of <see cref="PlainTextLayout"/> to runs of different settings, images and tooltips: every word is a range of characters of its run, and the words
/// that the legacy code removes from a run's list of remaining words are the ones this code skips (<c>Cursor</c>).
/// </summary>
internal sealed class FormattedTextLayout
{
    /// <summary>Same value as <c>MultiLineWordSuffix</c> of <see cref="MGTextLine"/>: appended to a piece that ends in the middle of a word.</summary>
    private const char MultiLineWordSuffix = '-';
    private const string MultiLineWordSuffixText = "-";

    private readonly List<FormattedTextLine> Lines = new();
    private readonly List<FormattedTextPiece> Pieces = new();
    private char[] Chars = new char[64];
    private int CharCount;

    private FormattedTextRuns SourceCopy;
    private FormattedTextRuns Runs;

    //  The runs that take part in the layout: all of them but the empty text runs (the legacy "wrappable runs"), then for each one the position of its first word that is not in a line yet
    //  (the legacy list of remaining words), and whether its last word continues in the next run.
    private int[] Wrappable = new int[8];
    private int[] Cursor = new int[8];
    private bool[] LinksToNext = new bool[8];
    private int WrappableCount;

    //  The words that are measured together: a word and the words of the following runs that continue it
    private int[] ChainRun = new int[4];
    private int[] ChainStart = new int[4];
    private int[] ChainLength = new int[4];
    private int ChainCount;

    //  State of the line being built
    private MGTextBlock Owner;
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

    //  The index in the text of the next character that a piece or a line break takes: what the counter of ParseLines (CurrentIndexInOriginalText) is, whether the characters end up in
    //  a line or in a line that is ignored.
    private int OriginalIndex;

    /// <summary>The width the lines were computed for.</summary>
    public int ParseWidth { get; private set; }

    /// <summary>Whether the lines were computed with word wrapping.</summary>
    public bool WrapText { get; private set; }

    public int LineCount => Lines.Count;

    public FormattedTextLine GetLine(int Index) => Lines[Index];

    public FormattedTextPiece GetPiece(int Index) => Pieces[Index];

    public ReadOnlySpan<char> GetPieceText(FormattedTextPiece Piece) => new(Chars, Piece.Start, Piece.Length);

    /// <summary>The runs the lines were computed from, and the one <see cref="FormattedTextPiece.RunIndex"/> points in. A copy that this layout owns when <c>KeepSource</c> was true.</summary>
    public FormattedTextRuns Source => Runs;

    /// <summary>Computes the lines of <paramref name="Runs"/>, replacing the previous ones.</summary>
    /// <param name="Owner">Measures the pieces of text and resolves the fonts.</param>
    /// <param name="MaxLineWidth">Same meaning as the <c>MaxLineWidth</c> of <see cref="MGTextLine.ParseLines(ITextMeasurer, double, bool, IEnumerable{MGTextRun}, bool)"/>.</param>
    /// <param name="KeepSource">If true, the layout keeps a copy of <paramref name="Runs"/> (<see cref="Source"/>): its pieces point in it, so the layout can be drawn and its public lines
    /// can be built later from the runs these lines are of. If false, <see cref="Source"/> is <paramref name="Runs"/> itself, which must not change while the lines are used.</param>
    public void Build(MGTextBlock Owner, FormattedTextRuns Runs, double MaxLineWidth, bool WrapText, bool IgnoreEmptySpaceLines, bool KeepSource)
    {
        Lines.Clear();
        Pieces.Clear();
        CharCount = 0;
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
        OriginalIndex = 0;

        if (KeepSource)
        {
            SourceCopy ??= new FormattedTextRuns();
            SourceCopy.CopyFrom(Runs);
            this.Runs = SourceCopy;
        }
        else
        {
            this.Runs = Runs;
        }

        if (Runs.Count == 0 || MaxLineWidth < 1)
        {
            return;
        }

        this.Owner = Owner;
        this.MaxLineWidth = MaxLineWidth;
        this.IgnoreEmptySpaceLines = IgnoreEmptySpaceLines;
        MinLineHeight = (float)Math.Ceiling(Owner.MeasureText(" ".AsSpan(), false, false).Y);

        FindWrappableRuns();

        var Wraps = WrapText && MaxLineWidth != double.MaxValue && MaxLineWidth < 100000;
        for (var Slot = 0; Slot < WrappableCount; Slot++)
        {
            var RunIndex = Wrappable[Slot];
            ref var Run = ref this.Runs[RunIndex];
            if (Run.Kind == ScannedRunKind.LineBreak)
            {
                if (CurrentPieceCount == 0)
                {
                    AddEmptyPiece();
                }

                FlushLine(true, Run.LineBreakCharacterCount);
            }
            else if (Run.Kind == ScannedRunKind.Image)
            {
                if (!Wraps)
                {
                    AddImagePiece(RunIndex);
                }
                else
                {
                    var ImgWidth = Run.ImageWidth;
                    if (CurrentPieceCount > 0 && CommittedRunsWidth + ImgWidth > MaxLineWidth)
                    {
                        FlushLine(false);
                    }

                    AddImagePiece(RunIndex);
                    CurrentX += ImgWidth;
                    CommittedRunsWidth += ImgWidth;
                }
            }
            else if (!Wraps)
            {
                //  The remaining words of the run are all of them: the whole run is one piece
                var Text = this.Runs.GetText(Run);
                AddPiece(RunIndex, Text, false);
                CommittedRunsWidth += Measure(Text, Run.Settings.IsBold, Run.Settings.IsItalic).X;
            }
            else
            {
                LayoutWrappedRun(Slot);
                if (IsTerminated)
                {
                    return;
                }
            }
        }

        if (CurrentPieceCount == 0)
        {
            AddEmptyPiece();
        }

        FlushLine(false);
    }

    private void FindWrappableRuns()
    {
        if (Wrappable.Length < Runs.Count)
        {
            var Length = Wrappable.Length;
            while (Length < Runs.Count)
            {
                Length *= 2;
            }

            Wrappable = new int[Length];
            Cursor = new int[Length];
            LinksToNext = new bool[Length];
        }

        WrappableCount = 0;
        for (var i = 0; i < Runs.Count; i++)
        {
            ref var Run = ref Runs[i];
            if (Run.Kind != ScannedRunKind.Text || Run.Length > 0)
            {
                Wrappable[WrappableCount] = i;
                Cursor[WrappableCount] = 0;
                LinksToNext[WrappableCount] = false;
                WrappableCount++;
            }
        }

        //  The last word of a text run continues in the next run when that one is a text run too, and neither of the two words is a delimiter
        for (var Slot = 0; Slot + 1 < WrappableCount; Slot++)
        {
            ref var Current = ref Runs[Wrappable[Slot]];
            ref var Next = ref Runs[Wrappable[Slot + 1]];
            if (Current.Kind == ScannedRunKind.Text && Next.Kind == ScannedRunKind.Text)
            {
                LinksToNext[Slot] = !IsDelimiter(Runs.Chars[Current.Start + Current.Length - 1]) && !IsDelimiter(Runs.Chars[Next.Start]);
            }
        }
    }

    private void LayoutWrappedRun(int Slot)
    {
        var RunIndex = Wrappable[Slot];
        ref var Run = ref Runs[RunIndex];
        var Text = Runs.GetText(Run);

        //  The text not yet in a line is Text[UnwrappedStart..Position): the words that fit, which the wrapping decision measures again with the next word.
        //  Position is the first word that is not in a line yet: the words an earlier run took with it (see the long word case) are behind it.
        var Position = Cursor[Slot];
        var UnwrappedStart = Position;
        while (Position < Text.Length)
        {
            var WordLength = GetWordLength(Text, Position);
            FindChain(Slot, Position, WordLength);

            var Bold = Run.Settings.IsBold;
            var Italic = Run.Settings.IsItalic;
            var CurrentWidth = Measure(Text.Slice(Position, WordLength), Bold, Italic).X;
            double TotalWidthSum = CurrentWidth;
            for (var k = 1; k < ChainCount; k++)
            {
                TotalWidthSum += MeasureChainWord(k).X;
            }

            var TotalWidth = (float)TotalWidthSum;

            //  The projected width is measured as FlushLine measures a line: each run as one string
            var Candidate = Text.Slice(UnwrappedStart, Position + WordLength - UnwrappedStart);
            float ProjectedWidth;
            if (ChainCount == 1)
            {
                ProjectedWidth = CommittedRunsWidth + Measure(Candidate, Bold, Italic).X;
            }
            else
            {
                ProjectedWidth = CommittedRunsWidth;
                ProjectedWidth += Measure(Candidate, Bold, Italic).X;
                for (var k = 1; k < ChainCount; k++)
                {
                    ProjectedWidth += MeasureChainWord(k).X;
                }
            }

            if (ProjectedWidth <= MaxLineWidth)
            {
                CurrentX += CurrentWidth;
                Position += WordLength;
            }
            else
            {
                if (Position > UnwrappedStart)
                {
                    AddPiece(RunIndex, Text.Slice(UnwrappedStart, Position - UnwrappedStart), false);
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
                    //  This chunk of text is wider than an entire line, so it is split up into several pieces
                    double MaxLineSuffixWidth = 0;
                    for (var k = 0; k < ChainCount; k++)
                    {
                        ref var ChainRunRef = ref Runs[ChainRun[k]];
                        MaxLineSuffixWidth = k == 0
                            ? Measure(MultiLineWordSuffixText.AsSpan(), ChainRunRef.Settings.IsBold, ChainRunRef.Settings.IsItalic).X
                            : Math.Max(MaxLineSuffixWidth, Measure(MultiLineWordSuffixText.AsSpan(), ChainRunRef.Settings.IsBold, ChainRunRef.Settings.IsItalic).X);
                    }

                    if (MaxLineSuffixWidth >= MaxLineWidth)
                    {
                        if (CurrentPieceCount > 0)
                        {
                            FlushLine(false);
                        }

                        IsTerminated = true;
                        return;
                    }

                    for (var k = 0; k < ChainCount; k++)
                    {
                        LayoutLongWord(k, MaxLineSuffixWidth);
                    }

                    //  The words are in lines now: they are not remaining words of their runs any more
                    for (var k = 1; k < ChainCount; k++)
                    {
                        Cursor[Slot + k] += ChainLength[k];
                    }

                    Position += WordLength;
                    UnwrappedStart = Position;
                }
            }
        }

        if (Position > UnwrappedStart)
        {
            var CommittedText = Text.Slice(UnwrappedStart, Position - UnwrappedStart);
            AddPiece(RunIndex, CommittedText, false);
            CommittedRunsWidth += Measure(CommittedText, Run.Settings.IsBold, Run.Settings.IsItalic).X;
        }

        Cursor[Slot] = Position;
    }

    /// <summary>The word at <paramref name="Start"/> of the run in <paramref name="Slot"/>, then the first remaining word of each following run as long as the word before continues in it.</summary>
    private void FindChain(int Slot, int Start, int Length)
    {
        ChainCount = 0;
        AddToChain(Wrappable[Slot], Start, Length);
        var ChainSlot = Slot;
        while (HasNext(ChainSlot, ChainStart[ChainCount - 1] + ChainLength[ChainCount - 1]))
        {
            ChainSlot++;
            ref var Next = ref Runs[Wrappable[ChainSlot]];
            var NextText = Runs.GetText(Next);
            AddToChain(Wrappable[ChainSlot], Cursor[ChainSlot], GetWordLength(NextText, Cursor[ChainSlot]));
        }
    }

    /// <summary>True when the word of the run in <paramref name="Slot"/> that ends at <paramref name="WordEnd"/> is the last word of the run and continues in the next run.</summary>
    private bool HasNext(int Slot, int WordEnd)
    {
        ref var Run = ref Runs[Wrappable[Slot]];
        return WordEnd == Run.Length && LinksToNext[Slot];
    }

    private void AddToChain(int RunIndex, int Start, int Length)
    {
        if (ChainCount == ChainRun.Length)
        {
            Array.Resize(ref ChainRun, ChainCount * 2);
            Array.Resize(ref ChainStart, ChainCount * 2);
            Array.Resize(ref ChainLength, ChainCount * 2);
        }

        ChainRun[ChainCount] = RunIndex;
        ChainStart[ChainCount] = Start;
        ChainLength[ChainCount] = Length;
        ChainCount++;
    }

    private ReadOnlySpan<char> ChainWordText(int k)
    {
        ref var Run = ref Runs[ChainRun[k]];
        return Runs.GetText(Run).Slice(ChainStart[k], ChainLength[k]);
    }

    private Vector2 MeasureChainWord(int k)
    {
        ref var Run = ref Runs[ChainRun[k]];
        return Measure(ChainWordText(k), Run.Settings.IsBold, Run.Settings.IsItalic);
    }

    /// <summary>Puts a word of the chain wider than a line in pieces. Mirrors the branch of <see cref="MGTextLine.ParseLines(ITextMeasurer, double, bool, IEnumerable{MGTextRun}, bool)"/>
    /// that handles such a chunk: as there, the words that follow are positioned without counting the width of the last piece left in the line.</summary>
    private void LayoutLongWord(int k, double MaxLineSuffixWidth)
    {
        var RunIndex = ChainRun[k];
        ref var Run = ref Runs[RunIndex];
        var Bold = Run.Settings.IsBold;
        var Italic = Run.Settings.IsItalic;
        var Word = ChainWordText(k);

        //  The part of the word not yet in a piece is Word[PendingStart..PendingStart + PendingLength)
        var PendingStart = 0;
        var PendingLength = 0;

        var WordWidth = Measure(Word, Bold, Italic).X;
        if (CurrentX + WordWidth + MaxLineSuffixWidth <= MaxLineWidth)
        {
            PendingLength = Word.Length;
            CurrentX += WordWidth;
        }
        else
        {
            for (var CharIndex = 0; CharIndex < Word.Length; CharIndex++)
            {
                var CharacterWidth = Measure(Word.Slice(CharIndex, 1), Bold, Italic).X;
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
                    AddPiece(RunIndex, Word.Slice(PendingStart, PendingLength), true);
                    FlushLine(false);

                    PendingStart = CharIndex;
                    PendingLength = 0;
                    CharIndex--;
                }
            }
        }

        if (PendingLength > 0)
        {
            AddPiece(RunIndex, Word.Slice(PendingStart, PendingLength), false);
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

    private Vector2 Measure(ReadOnlySpan<char> Text, bool IsBold, bool IsItalic) => Owner.MeasureText(Text, IsBold, IsItalic);

    private void EnsureChars(int Length)
    {
        if (Chars.Length < CharCount + Length)
        {
            var Grown = new char[GrownLength(Chars.Length, CharCount + Length)];
            Array.Copy(Chars, Grown, CharCount);
            Chars = Grown;
        }
    }

    private void AddPiece(int RunIndex, ReadOnlySpan<char> Text, bool WithSuffix)
    {
        var Length = Text.Length + (WithSuffix ? 1 : 0);
        EnsureChars(Length);

        Text.CopyTo(Chars.AsSpan(CharCount));
        if (WithSuffix)
        {
            Chars[CharCount + Text.Length] = MultiLineWordSuffix;
        }

        Pieces.Add(new FormattedTextPiece(RunIndex, CharCount, Length, OriginalIndex, WithSuffix));
        CharCount += Length;
        OriginalIndex += Text.Length;
        CurrentPieceCount++;
    }

    /// <summary>The empty run of an empty line: its settings are not those of the text.</summary>
    private void AddEmptyPiece()
    {
        Pieces.Add(new FormattedTextPiece(-1, CharCount, 0, OriginalIndex, false));
        CurrentPieceCount++;
    }

    private void AddImagePiece(int RunIndex)
    {
        Pieces.Add(new FormattedTextPiece(RunIndex, CharCount, 0, OriginalIndex, false));
        CurrentPieceCount++;
    }

    /// <summary>Ends the line being built: measures each of its pieces as a complete string, like <c>FlushLine</c> of <see cref="MGTextLine"/>.</summary>
    private void FlushLine(bool EndsInLinebreak, int LineBreakCharacterCount = 0)
    {
        var OriginalEnd = OriginalIndex;
        OriginalIndex += LineBreakCharacterCount;

        double TextWidthSum = 0;
        double ImageWidthSum = 0;
        float TextHeight = 0;
        float ImageHeight = 0;
        var HasText = false;
        var HasImage = false;
        for (var i = 0; i < CurrentPieceCount; i++)
        {
            var Piece = Pieces[CurrentFirstPiece + i];
            if (Piece.RunIndex >= 0 && Runs[Piece.RunIndex].Kind == ScannedRunKind.Image)
            {
                ref var Image = ref Runs[Piece.RunIndex];
                ImageWidthSum += Image.ImageWidth;
                ImageHeight = HasImage ? Math.Max(ImageHeight, Image.ImageHeight) : Image.ImageHeight;
                HasImage = true;
            }
            else
            {
                //  An empty piece belongs to the empty run of an empty line, whose settings are not those of the text.
                Vector2 Size;
                if (Piece.Length == 0)
                {
                    Size = Vector2.Zero;
                }
                else
                {
                    ref var Run = ref Runs[Piece.RunIndex];
                    Size = Measure(GetPieceText(Piece), Run.Settings.IsBold, Run.Settings.IsItalic);
                }

                TextWidthSum += Size.X;
                TextHeight = HasText ? Math.Max(TextHeight, Size.Y) : Size.Y;
                HasText = true;
            }
        }

        var Width = (float)TextWidthSum + (float)ImageWidthSum;
        var TotalHeight = Math.Max(Math.Max(MinLineHeight, TextHeight), ImageHeight);

        var IsIgnoreable = IgnoreEmptySpaceLines && HasPreviousLine && !PreviousLineEndsInLinebreak &&
                           CurrentPieceCount == 1 && Pieces[CurrentFirstPiece].RunIndex >= 0 && Runs[Pieces[CurrentFirstPiece].RunIndex].Kind == ScannedRunKind.Text &&
                           Pieces[CurrentFirstPiece].Length == 1 && Chars[Pieces[CurrentFirstPiece].Start] == ' ';
        HasPreviousLine = true;
        PreviousLineEndsInLinebreak = EndsInLinebreak;

        if (IsIgnoreable)
        {
            Pieces.RemoveRange(CurrentFirstPiece, CurrentPieceCount);
            CharCount = CurrentCharStart;
        }
        else
        {
            Lines.Add(new FormattedTextLine(CurrentFirstPiece, CurrentPieceCount, Width, TextHeight, ImageHeight, TotalHeight, EndsInLinebreak, LineBreakCharacterCount, OriginalEnd));
        }

        CurrentFirstPiece = Pieces.Count;
        CurrentPieceCount = 0;
        CurrentCharStart = CharCount;
        CurrentX = 0;
        CommittedRunsWidth = 0;
    }

    /// <summary>Appends to <paramref name="Destination"/> what <see cref="MGTextLine.OriginalCharacterIndices"/> holds for the line at <paramref name="LineIndex"/>: for each character of the line, the index of the
    /// character in the text of the runs (the hyphen that wrapping inserted shares the index of the character that follows it), then one index for each character of the line break that
    /// ends the line, or, when the line ends in no line break and has no character of a text run, one index (the one that follows the line).</summary>
    public void AppendOriginalIndices(int LineIndex, List<int> Destination)
    {
        var Line = Lines[LineIndex];
        var HasCharacters = false;
        for (var i = 0; i < Line.PieceCount; i++)
        {
            var Piece = Pieces[Line.FirstPiece + i];
            if (Piece.Length == 0)
            {
                continue;
            }

            HasCharacters = true;
            var TextLength = Piece.HasSuffix ? Piece.Length - 1 : Piece.Length;
            for (var k = 0; k < TextLength; k++)
            {
                Destination.Add(Piece.OriginalStart + k);
            }

            if (Piece.HasSuffix)
            {
                Destination.Add(Piece.OriginalStart + TextLength);
            }
        }

        if (Line.EndsInLinebreak)
        {
            for (var i = 0; i < Line.LineBreakCharacterCount; i++)
            {
                Destination.Add(Line.OriginalEnd + i);
            }
        }
        else if (!HasCharacters)
        {
            Destination.Add(Line.OriginalEnd);
        }
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
