using Microsoft.Xna.Framework;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using MonoGame.Extended;

namespace MGUI.Core.UI.Text;

internal class TextRenderInfo
{
    public MGTextBox TextBox { get; }
    private MGTextBlock TextBlockElement { get; }
    internal float DefaultCharacterWidth => TextBlockElement.SpaceWidth * 2;

    internal bool IsDirty { get; private set; } = true;

    private List<LineRenderInfo> _Lines { get; }
    public IReadOnlyList<LineRenderInfo> Lines
    {
        get
        {
            if (IsDirty)
            {
                UpdateLines();
            }

            return _Lines;
        }
    }

    public LineRenderInfo FirstLine => Lines[0];
    public LineRenderInfo LastLine => Lines[^1];

    //  The first line that has a character, by index: First(predicate) and Last(predicate) over the interface enumerate through a boxed enumerator.
    internal CharRenderInfo GetFirstChar()
    {
        var AllLines = Lines;
        for (var i = 0; i < AllLines.Count; i++)
        {
            if (AllLines[i].HasCharacters)
            {
                return AllLines[i].FirstCharacter;
            }
        }

        throw new InvalidOperationException("Sequence contains no matching element");
    }

    internal CharRenderInfo GetLastChar()
    {
        var AllLines = Lines;
        for (var i = AllLines.Count - 1; i >= 0; i--)
        {
            if (AllLines[i].HasCharacters)
            {
                return AllLines[i].LastCharacter;
            }
        }

        throw new InvalidOperationException("Sequence contains no matching element");
    }

    internal TextRenderInfo(MGTextBox TextBox, MGTextBlock TextBlockElement)
    {
        this.TextBox = TextBox;
        this.TextBlockElement = TextBlockElement;

        _Lines = new();

        this.TextBox.TextChanged += (sender, e) => { IsDirty = true; };
        this.TextBlockElement.OnLayoutBoundsChanged += (sender, e) => { IsDirty = true; };
    }

    //  The per-glyph advances of the run being laid out, kept between updates (grown when a run is longer than any before): each one is read and used in the same iteration.
    private float[] _AdvancesBuffer = new float[64];

    private float[] GetAdvancesBuffer(int length)
    {
        if (_AdvancesBuffer.Length < length)
        {
            _AdvancesBuffer = new float[Math.Max(length, _AdvancesBuffer.Length * 2)];
        }

        return _AdvancesBuffer;
    }

    /// <summary>The original index of each character of the line being laid out, from the table of the line (see <see cref="PlainTextLayout.AppendOriginalIndices"/>), kept between updates.</summary>
    private readonly List<int> _OriginalIndices = new();

    private void UpdateLines()
    {
        IsDirty = false;
        _Lines.Clear();

        var LayoutBounds = TextBlockElement.LayoutBounds;
        var Padding = TextBlockElement.ResolvedPadding;
        var PaddedBounds = TextBlockElement.GetPaddedLayoutBounds(LayoutBounds);

        //  The text block that has an internal layout (plain text, or text with markup, and no explicit runs) gives the table of original indices itself, so the lines are not built again with
        //  MGTextLine.ParseLines for the caret. The text block that has explicit runs (a rich text box with styled segments, a colouriser) has no internal layout, and its lines are read.
        var PlainLayout = TextBlockElement.KnownPlainLines;
        var FormattedLayout = PlainLayout == null ? TextBlockElement.KnownFormattedLines : null;
        if (PlainLayout != null)
        {
            if (PlainLayout.LineCount == 0)
            {
                AddEmptyLine(LayoutBounds, PaddedBounds);
            }
            else
            {
                UpdateLines(PlainLayout, LayoutBounds, PaddedBounds);
            }

            return;
        }

        if (FormattedLayout != null)
        {
            if (FormattedLayout.LineCount == 0)
            {
                AddEmptyLine(LayoutBounds, PaddedBounds);
            }
            else
            {
                UpdateLines(FormattedLayout, LayoutBounds, PaddedBounds);
            }

            return;
        }

        var SourceLines = TextBlockElement.Lines;
        if (SourceLines == null || SourceLines.Count == 0)
        {
            AddEmptyLine(LayoutBounds, PaddedBounds);
        }
        else
        {
            UpdateLines(SourceLines, LayoutBounds, PaddedBounds);
        }
    }

    /// <summary>The single line of a text that has no line: one character, as wide as a default character, at the start of the block.</summary>
    private void AddEmptyLine(Rectangle LayoutBounds, Rectangle PaddedBounds)
    {
        var emptyLineHeight = TextBlockElement.MeasureText("|", false, false).Y;
        var currentY = TextBlockElement.GetRenderedTextStartY(LayoutBounds, emptyLineHeight);
        LineRenderInfo LineInfo = new(this, false, true, 0, currentY, emptyLineHeight);
        _Lines.Add(LineInfo);

        float X = PaddedBounds.Left;
        LineInfo.AddCharacter(0, 0, X, DefaultCharacterWidth);
    }

    /// <summary>Starts the line number <see cref="_Lines"/>.Count: its position and the X of its first character, from its width and height and the alignment of the text.</summary>
    private LineRenderInfo AddLine(Rectangle PaddedBounds, float CurrentY, float LineWidth, float LineTotalHeight, bool HasOnlyEmptyText, out float CurrentX)
    {
        Rectangle LineBounds = new(PaddedBounds.Left, (int)CurrentY, PaddedBounds.Width, (int)LineTotalHeight);
        CurrentX = MGElement.ApplyAlignment(LineBounds, TextBlockElement.TextAlignment, VerticalAlignment.Center, new Size((int)LineWidth, (int)LineTotalHeight)).Left;

        LineRenderInfo LineInfo = new(this, true, HasOnlyEmptyText, _Lines.Count, LineBounds.Top, LineTotalHeight);
        _Lines.Add(LineInfo);
        return LineInfo;
    }

    /// <summary>The lines of a plain text, from its layout.</summary>
    private void UpdateLines(PlainTextLayout Layout, Rectangle LayoutBounds, Rectangle PaddedBounds)
    {
        var CharCounter = 0;
        var LinePadding = TextBlockElement.LinePadding;
        var engine = TextBlockElement.GetTextEngine();
        var CurrentY = TextBlockElement.GetRenderedTextStartY(LayoutBounds, Layout);
        var Settings = Layout.Settings;
        for (var LineNumber = 0; LineNumber < Layout.LineCount; LineNumber++)
        {
            var Line = Layout.GetLine(LineNumber);
            var CharacterIndexInWrappedLines = 0;

            var HasNonEmptyPiece = false;
            for (var PieceNumber = 0; PieceNumber < Line.PieceCount; PieceNumber++)
            {
                if (Layout.GetPiece(Line.FirstPiece + PieceNumber).Length > 0)
                {
                    HasNonEmptyPiece = true;
                    break;
                }
            }

            var LineInfo = AddLine(PaddedBounds, CurrentY, Line.Width, Line.TotalHeight, !HasNonEmptyPiece, out var CurrentX);
            _OriginalIndices.Clear();
            Layout.AppendOriginalIndices(LineNumber, _OriginalIndices);

            if (!HasNonEmptyPiece)
            {
                LineInfo.AddCharacter(_OriginalIndices[CharacterIndexInWrappedLines], CharCounter, CurrentX, DefaultCharacterWidth);
            }
            else
            {
                for (var PieceNumber = 0; PieceNumber < Line.PieceCount; PieceNumber++)
                {
                    var Piece = Layout.GetPiece(Line.FirstPiece + PieceNumber);
                    if (Piece.Length == 0)
                    {
                        continue;
                    }

                    AddCharacters(LineInfo, engine, Settings.IsBold, Settings.IsItalic, null, Layout.GetPieceText(Piece), _OriginalIndices, ref CharacterIndexInWrappedLines, ref CharCounter, ref CurrentX);
                }
            }

            CurrentY += Line.TotalHeight + LinePadding;
        }
    }

    /// <summary>The lines of a text with markup, from its layout.</summary>
    private void UpdateLines(FormattedTextLayout Layout, Rectangle LayoutBounds, Rectangle PaddedBounds)
    {
        var CharCounter = 0;
        var LinePadding = TextBlockElement.LinePadding;
        var engine = TextBlockElement.GetTextEngine();
        var Runs = Layout.Source;
        var CurrentY = TextBlockElement.GetRenderedTextStartY(LayoutBounds, Layout);
        for (var LineNumber = 0; LineNumber < Layout.LineCount; LineNumber++)
        {
            var Line = Layout.GetLine(LineNumber);
            var CharacterIndexInWrappedLines = 0;

            //  Only text runs are handled, as for the lines of MGTextBlock.Lines: a piece of an image is not.
            var HasNonEmptyPiece = false;
            var HasImagePiece = false;
            for (var PieceNumber = 0; PieceNumber < Line.PieceCount; PieceNumber++)
            {
                var Piece = Layout.GetPiece(Line.FirstPiece + PieceNumber);
                if (Piece.RunIndex >= 0 && Runs[Piece.RunIndex].Kind == ScannedRunKind.Image)
                {
                    HasImagePiece = true;
                }
                else if (Piece.Length > 0)
                {
                    HasNonEmptyPiece = true;
                }
            }

            var LineInfo = AddLine(PaddedBounds, CurrentY, Line.Width, Line.TotalHeight, !HasNonEmptyPiece && !HasImagePiece, out var CurrentX);
            if (HasImagePiece)
            {
                throw new NotImplementedException($"{nameof(TextRenderInfo)}.{nameof(UpdateLines)} can only handle {nameof(MGTextLine)}s which consist only of {nameof(MGTextRun)}s of type={nameof(TextRunType)}.{nameof(TextRunType.Text)}");
            }

            _OriginalIndices.Clear();
            Layout.AppendOriginalIndices(LineNumber, _OriginalIndices);

            if (!HasNonEmptyPiece)
            {
                LineInfo.AddCharacter(_OriginalIndices[CharacterIndexInWrappedLines], CharCounter, CurrentX, DefaultCharacterWidth);
            }
            else
            {
                for (var PieceNumber = 0; PieceNumber < Line.PieceCount; PieceNumber++)
                {
                    var Piece = Layout.GetPiece(Line.FirstPiece + PieceNumber);
                    if (Piece.Length == 0)
                    {
                        continue;
                    }

                    ref var Run = ref Runs[Piece.RunIndex];
                    AddCharacters(LineInfo, engine, Run.Settings.IsBold, Run.Settings.IsItalic, null, Layout.GetPieceText(Piece), _OriginalIndices, ref CharacterIndexInWrappedLines, ref CharCounter, ref CurrentX);
                }
            }

            CurrentY += Line.TotalHeight + LinePadding;
        }
    }

    /// <summary>The lines of a text block that has explicit runs, read from <see cref="MGTextBlock.Lines"/>, which hold their own table of original indices.</summary>
    private void UpdateLines(IReadOnlyList<MGTextLine> SourceLines, Rectangle LayoutBounds, Rectangle PaddedBounds)
    {
        var CharCounter = 0;
        var LinePadding = TextBlockElement.LinePadding;
        var engine = TextBlockElement.GetTextEngine();
        var CurrentY = TextBlockElement.GetRenderedTextStartY(LayoutBounds, SourceLines);
        for (var LineNumber = 0; LineNumber < SourceLines.Count; LineNumber++)
        {
            var Line = SourceLines[LineNumber];
            var CharacterIndexInWrappedLines = 0;

            //  Indexed loops, not All/Cast/ToList/Any: they allocate an enumerator, a delegate, a list per line at each change of the text.
            var LineRuns = Line.Runs;
            var HasNonEmptyRun = false;
            for (var RunNumber = 0; RunNumber < LineRuns.Count; RunNumber++)
            {
                if (LineRuns[RunNumber] is MGTextRunText TextRun && !string.IsNullOrEmpty(TextRun.Text))
                {
                    HasNonEmptyRun = true;
                    break;
                }
            }

            var LineInfo = AddLine(PaddedBounds, CurrentY, Line.LineWidth, Line.LineTotalHeight, !HasNonEmptyRun, out var CurrentX);

            for (var RunNumber = 0; RunNumber < LineRuns.Count; RunNumber++)
            {
                if (LineRuns[RunNumber].RunType != TextRunType.Text)
                {
                    throw new NotImplementedException($"{nameof(TextRenderInfo)}.{nameof(UpdateLines)} can only handle {nameof(MGTextLine)}s which consist only of {nameof(MGTextRun)}s of type={nameof(TextRunType)}.{nameof(TextRunType.Text)}");
                }
            }

            if (!HasNonEmptyRun)
            {
                LineInfo.AddCharacter(Line.OriginalCharacterIndices[CharacterIndexInWrappedLines], CharCounter, CurrentX, DefaultCharacterWidth);
                //CharacterIndexInWrappedLines++;
            }
            else
            {
                for (var RunNumber = 0; RunNumber < LineRuns.Count; RunNumber++)
                {
                    var Run = (MGTextRunText)LineRuns[RunNumber];
                    if (string.IsNullOrEmpty(Run.Text))
                    {
                        continue;
                    }

                    AddCharacters(LineInfo, engine, Run.Settings.IsBold, Run.Settings.IsItalic, Run.Text, Run.Text, Line.OriginalCharacterIndices, ref CharacterIndexInWrappedLines, ref CharCounter, ref CurrentX);
                }
            }

            CurrentY += Line.LineTotalHeight + LinePadding;
        }
    }

    /// <summary>Emits one character record per character of a run of text, at <paramref name="CurrentX"/> and after.</summary>
    /// <param name="TextString">The text as a string when the caller has one (the text engine then measures the string), or null.</param>
    private void AddCharacters(LineRenderInfo LineInfo, ITextMeasurementEngine engine, bool IsBold, bool IsItalic, string TextString, ReadOnlySpan<char> Text, IReadOnlyList<int> OriginalIndices,
        ref int CharacterIndexInWrappedLines, ref int CharCounter, ref float CurrentX)
    {
        // Resolve the correct font variant for this run's inline style
        var resolved = TextBlockElement.GetResolvedFont(IsBold, IsItalic);

        // 1. Compute per-glyph advances.
        //    The first character on a line has its negative LSB clamped to 0 (matching
        //    SpriteFont behaviour); subsequent characters use the full signed advance.
        var advances = GetAdvancesBuffer(Text.Length);
        var glyphSum = 0f;
        for (var gi = 0; gi < Text.Length; gi++)
        {
            var glyph = engine.MeasureGlyph(resolved, Text[gi]);
            advances[gi] = gi == 0 ? glyph.TotalWidthFirstGlyph : glyph.TotalWidth;
            glyphSum += advances[gi];
        }

        // 2. Reconcile with the whole-string measurement so each run's right edge
        //    aligns exactly with where the next run (or line end) starts, absorbing
        //    any SpriteFont.Spacing or kerning residual between characters.
        if (glyphSum > 0f)
        {
            var wholeWidth = (TextString != null ? engine.MeasureText(resolved, TextString) : engine.MeasureText(resolved, Text)).X;
            var residual = wholeWidth - glyphSum;
            if (MathF.Abs(residual) > 0.001f)
            {
                for (var gi = 0; gi < Text.Length; gi++)
                {
                    advances[gi] += residual * (advances[gi] / glyphSum);
                }
            }
        }

        // 3. Emit one caret record per character with the reconciled advance
        for (var gi = 0; gi < Text.Length; gi++)
        {
            LineInfo.AddCharacter(OriginalIndices[CharacterIndexInWrappedLines], CharCounter, CurrentX, advances[gi]);
            CharacterIndexInWrappedLines++;
            CharCounter++;
            CurrentX += advances[gi];
        }
    }

    internal bool TryGetCharAtOriginalIndex(int Index, out CharRenderInfo Result)
    {
        var AllLines = Lines;
        for (var i = 0; i < AllLines.Count; i++)
        {
            if (AllLines[i].TryGetCharAtOriginalIndex(Index, out Result))
            {
                return true;
            }
        }

        Result = default;
        return false;
    }

    internal bool TryGetCharAtParsedIndex(int Index, out CharRenderInfo Result)
    {
        var AllLines = Lines;
        for (var i = 0; i < AllLines.Count; i++)
        {
            if (AllLines[i].TryGetCharAtParsedIndex(Index, out Result))
            {
                return true;
            }
        }

        Result = default;
        return false;
    }

    internal bool TryGetCharAtScreenPosition(Vector2 Position, out CharRenderInfo Result)
    {
        var Line = Lines.OrderBy(x => x.DistanceToCenter(Position.Y)).FirstOrDefault();
        if (Line == null || !Line.Characters.Any())
        {
            Result = default;
            return false;
        }

        Result = Line.Characters.OrderBy(x => x.DistanceToMinX(Position.X)).First();
        return true;
    }

    public override string ToString() => $"{nameof(TextRenderInfo)}: {string.Join(", ", Lines.Select(x => x.ToString()))}";
}

internal class LineRenderInfo
{
    public readonly TextRenderInfo TextInfo;

    /// <summary>False for the single line of a text that has no line, true for every line of the text.</summary>
    public readonly bool HasSource;

    /// <summary>True when the line has no character of text: it is an empty line, whose single character is a placeholder as wide as a default character.</summary>
    public readonly bool HasOnlyEmptyText;

    public readonly int LineIndex;
    public readonly float YPosition;
    public readonly float Height;

    public float MinY => YPosition;
    public float MaxY => MinY + Height;
    public float CenterY => (MinY + MaxY) / 2.0f;

    public float DistanceToMinY(float YValue) => Math.Abs(YValue - MinY);
    public float DistanceToCenter(float YValue) => Math.Abs(YValue - CenterY);
    public float DistanceToMaxY(float YValue) => Math.Abs(YValue - MaxY);

    private readonly List<CharRenderInfo> _Characters;
    public IReadOnlyList<CharRenderInfo> Characters => _Characters;
    public bool HasCharacters => _Characters.Count > 0;

    public CharRenderInfo FirstCharacter => _Characters[0];
    public CharRenderInfo LastCharacter => _Characters[^1];

    internal LineRenderInfo(TextRenderInfo TextInfo, bool HasSource, bool HasOnlyEmptyText, int LineIndex, float YPosition, float Height)
    {
        this.TextInfo = TextInfo;
        this.HasSource = HasSource;
        this.HasOnlyEmptyText = HasOnlyEmptyText;
        this.LineIndex = LineIndex;
        this.YPosition = YPosition;
        this.Height = Height;
        _Characters = new();
    }

    internal bool TryGetCharAtOriginalIndex(int Index, out CharRenderInfo Result)
    {
        if (_Characters.Count == 0 || _Characters[^1].IndexInOriginalText < Index || Index < 0)
        {
            Result = default;
            return false;
        }
        else
        {
            for (var CharIndex = 0; CharIndex < _Characters.Count; CharIndex++)
            {
                var CharInfo = _Characters[CharIndex];
                if (CharInfo.IndexInOriginalText == Index)
                {
                    Result = CharInfo;
                    return true;
                }
            }

            Result = default;
            return false;
        }
    }

    internal bool TryGetCharAtParsedIndex(int Index, out CharRenderInfo Result)
    {
        if (_Characters.Count == 0 || _Characters[^1].IndexInParsedText < Index || Index < 0)
        {
            Result = default;
            return false;
        }
        else
        {
            for (var CharIndex = 0; CharIndex < _Characters.Count; CharIndex++)
            {
                var CharInfo = _Characters[CharIndex];
                if (CharInfo.IndexInParsedText == Index)
                {
                    Result = CharInfo;
                    return true;
                }
            }

            Result = default;
            return false;
        }
    }

    internal CharRenderInfo AddCharacter(int IndexInOriginalText, int IndexInParsedText, float XPosition, float Width)
    {
        CharRenderInfo Result = new(this, IndexInOriginalText, IndexInParsedText, XPosition, Width);
        _Characters.Add(Result);
        return Result;
    }

    internal LineRenderInfo GetTranslated(Vector2 Offset)
    {
        LineRenderInfo TranslatedLine = new(TextInfo, HasSource, HasOnlyEmptyText, LineIndex, YPosition + Offset.Y, Height);
        for (var CharIndex = 0; CharIndex < _Characters.Count; CharIndex++)
        {
            var CharInfo = _Characters[CharIndex];
            TranslatedLine.AddCharacter(CharInfo.IndexInOriginalText, CharInfo.IndexInParsedText, CharInfo.XPosition + Offset.X, CharInfo.Width);
        }

        return TranslatedLine;
    }

    public override string ToString() => $"{nameof(LineRenderInfo)}: {string.Join(", ", Characters.Select(x => x.ToString()))}";
}

internal struct CharRenderInfo
{
    public readonly LineRenderInfo Line;
    public readonly int IndexInOriginalText;
    public readonly int IndexInParsedText;
    public readonly float XPosition;
    public readonly float Width;

    public float MinX => XPosition;
    public float MaxX => MinX + Width;
    public float CenterX => (MinX + MaxX) / 2.0f;

    public float DistanceToMinX(float XValue) => Math.Abs(XValue - MinX);
    public float DistanceToCenter(float XValue) => Math.Abs(XValue - CenterX);
    public float DistanceToMaxX(float XValue) => Math.Abs(XValue - MaxX);

    internal CharRenderInfo(LineRenderInfo Line, int IndexInOriginalText, int IndexInParsedText, float XPosition, float Width)
    {
        this.Line = Line;
        this.IndexInOriginalText = IndexInOriginalText;
        this.IndexInParsedText = IndexInParsedText;
        this.XPosition = XPosition;
        this.Width = Width;
    }

    public override string ToString() => $"({IndexInOriginalText}|{IndexInParsedText})";
}