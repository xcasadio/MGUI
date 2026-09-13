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

    internal CharRenderInfo GetFirstChar() => Lines.First(x => x.Characters.Any()).FirstCharacter;
    internal CharRenderInfo GetLastChar() => Lines.Last(x => x.Characters.Any()).LastCharacter;

    internal TextRenderInfo(MGTextBox TextBox, MGTextBlock TextBlockElement)
    {
        this.TextBox = TextBox;
        this.TextBlockElement = TextBlockElement;

        _Lines = new();

        this.TextBox.TextChanged += (sender, e) => { IsDirty = true; };
        this.TextBlockElement.OnLayoutBoundsChanged += (sender, e) => { IsDirty = true; };
    }

    private void UpdateLines()
    {
        IsDirty = false;
        var LineIndex = 0;
        var CharCounter = 0;
        _Lines.Clear();

        var LayoutBounds = TextBlockElement.LayoutBounds;
        var Padding = TextBlockElement.ResolvedPadding;
        var LinePadding = TextBlockElement.LinePadding;
        var PaddedBounds = TextBlockElement.GetPaddedLayoutBounds(LayoutBounds);

        // engine is shared; fonts are resolved per-run to respect inline bold/italic formatting
        var engine = TextBlockElement.GetTextEngine();

        if (TextBlockElement.Lines?.Any() != true)
        {
            var emptyLineHeight = TextBlockElement.MeasureText("|", false, false).Y;
            var currentY = TextBlockElement.GetRenderedTextStartY(LayoutBounds, emptyLineHeight);
            LineRenderInfo LineInfo = new(this, null, LineIndex, currentY, emptyLineHeight);
            _Lines.Add(LineInfo);

            float X = PaddedBounds.Left;
            LineInfo.AddCharacter(0, 0, X, DefaultCharacterWidth);
        }
        else
        {
            var CurrentY = TextBlockElement.GetRenderedTextStartY(LayoutBounds, TextBlockElement.Lines);
            foreach (var Line in TextBlockElement.Lines)
            {
                var CharacterIndexInWrappedLines = 0;
                Rectangle LineBounds = new(PaddedBounds.Left, (int)CurrentY, PaddedBounds.Width, (int)Line.LineTotalHeight);
                float CurrentX = MGElement.ApplyAlignment(LineBounds, TextBlockElement.TextAlignment, VerticalAlignment.Center, new Size((int)Line.LineWidth, (int)Line.LineTotalHeight)).Left;

                LineRenderInfo LineInfo = new(this, Line, LineIndex, LineBounds.Top, Line.LineTotalHeight);
                _Lines.Add(LineInfo);
                LineIndex++;

                if (!Line.Runs.All(x => x.RunType == TextRunType.Text))
                {
                    throw new NotImplementedException($"{nameof(TextRenderInfo)}.{nameof(UpdateLines)} can only handle {nameof(MGTextLine)}s which consist only of {nameof(MGTextRun)}s of type={nameof(TextRunType)}.{nameof(TextRunType.Text)}");
                }

                var Runs = Line.Runs.Cast<MGTextRunText>().ToList();
                if (!Runs.Any(x => !string.IsNullOrEmpty(x.Text)))
                {
                    LineInfo.AddCharacter(Line.OriginalCharacterIndices[CharacterIndexInWrappedLines], CharCounter, CurrentX, DefaultCharacterWidth);
                    //CharacterIndexInWrappedLines++;
                }
                else
                {
                    foreach (var Run in Runs)
                    {
                        // Resolve the correct font variant for this run's inline style
                        var resolved = TextBlockElement.GetResolvedFont(Run.Settings.IsBold, Run.Settings.IsItalic);

                        if (string.IsNullOrEmpty(Run.Text))
                        {
                            continue;
                        }

                        // 1. Compute per-glyph advances.
                        //    The first character on a line has its negative LSB clamped to 0 (matching
                        //    SpriteFont behaviour); subsequent characters use the full signed advance.
                        var advances = new float[Run.Text.Length];
                        var glyphSum = 0f;
                        for (var gi = 0; gi < Run.Text.Length; gi++)
                        {
                            var glyph = engine.MeasureGlyph(resolved, Run.Text[gi]);
                            advances[gi] = gi == 0 ? glyph.TotalWidthFirstGlyph : glyph.TotalWidth;
                            glyphSum += advances[gi];
                        }

                        // 2. Reconcile with the whole-string measurement so each run's right edge
                        //    aligns exactly with where the next run (or line end) starts, absorbing
                        //    any SpriteFont.Spacing or kerning residual between characters.
                        if (glyphSum > 0f)
                        {
                            var wholeWidth = engine.MeasureText(resolved, Run.Text).X;
                            var residual = wholeWidth - glyphSum;
                            if (MathF.Abs(residual) > 0.001f)
                            {
                                for (var gi = 0; gi < advances.Length; gi++)
                                {
                                    advances[gi] += residual * (advances[gi] / glyphSum);
                                }
                            }
                        }

                        // 3. Emit one caret record per character with the reconciled advance
                        for (var gi = 0; gi < Run.Text.Length; gi++)
                        {
                            LineInfo.AddCharacter(Line.OriginalCharacterIndices[CharacterIndexInWrappedLines], CharCounter, CurrentX, advances[gi]);
                            CharacterIndexInWrappedLines++;
                            CharCounter++;
                            CurrentX += advances[gi];
                        }
                    }
                }

                CurrentY += Line.LineTotalHeight + LinePadding;
            }
        }
    }

    internal bool TryGetCharAtOriginalIndex(int Index, out CharRenderInfo Result)
    {
        foreach (var Line in Lines)
        {
            if (Line.TryGetCharAtOriginalIndex(Index, out Result))
            {
                return true;
            }
        }

        Result = default;
        return false;
    }

    internal bool TryGetCharAtParsedIndex(int Index, out CharRenderInfo Result)
    {
        foreach (var Line in Lines)
        {
            if (Line.TryGetCharAtParsedIndex(Index, out Result))
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
    public readonly MGTextLine Source;

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

    internal LineRenderInfo(TextRenderInfo TextInfo, MGTextLine Source, int LineIndex, float YPosition, float Height)
    {
        this.TextInfo = TextInfo;
        this.Source = Source;
        this.LineIndex = LineIndex;
        this.YPosition = YPosition;
        this.Height = Height;
        _Characters = new();
    }

    internal bool TryGetCharAtOriginalIndex(int Index, out CharRenderInfo Result)
    {
        if (!Characters.Any() || _Characters[^1].IndexInOriginalText < Index || Index < 0)
        {
            Result = default;
            return false;
        }
        else
        {
            foreach (var CharInfo in Characters)
            {
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
        if (!Characters.Any() || _Characters[^1].IndexInParsedText < Index || Index < 0)
        {
            Result = default;
            return false;
        }
        else
        {
            foreach (var CharInfo in Characters)
            {
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
        LineRenderInfo TranslatedLine = new(TextInfo, Source, LineIndex, YPosition + Offset.Y, Height);
        foreach (var CharInfo in Characters)
        {
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