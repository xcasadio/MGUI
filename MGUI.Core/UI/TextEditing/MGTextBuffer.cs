namespace MGUI.Core.UI.TextEditing;

public sealed class MGTextBuffer
{
    private readonly List<int> _lineStarts = new() { 0 };

    public string Text { get; private set; } = string.Empty;

    public int Length => Text.Length;
    public int Version { get; private set; }
    public int LineCount => _lineStarts.Count;
    public IReadOnlyList<int> LineStarts => _lineStarts;

    public MGTextBuffer(string text = null)
    {
        Text = NormalizeLineEndings(text);
        RebuildLineStarts();
    }

    public static string NormalizeLineEndings(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    public bool SetText(string text)
    {
        var normalizedText = NormalizeLineEndings(text);
        if (Text == normalizedText)
        {
            return false;
        }

        Text = normalizedText;
        Version++;
        RebuildLineStarts();
        return true;
    }

    public MGTextRange NormalizeRange(MGTextRange range)
        => range.Clamp(Length).Normalize();

    public MGTextRange Insert(int index, string text)
        => Replace(MGTextRange.EmptyAt(index), text);

    public MGTextRange Delete(MGTextRange range)
        => Replace(range, string.Empty);

    public MGTextRange Replace(MGTextRange range, string text)
        => ApplyEdit(range, text).InsertedRange;

    public MGTextEditResult ApplyEdit(MGTextRange range, string text)
    {
        var removedRange = NormalizeRange(range);
        var insertionText = NormalizeLineEndings(text);
        var removedText = GetText(removedRange);
        var nextText = Text.Remove(removedRange.StartIndex, removedRange.Length).Insert(removedRange.StartIndex, insertionText);

        if (nextText != Text)
        {
            Text = nextText;
            Version++;
            RebuildLineStarts();
        }

        var insertedRange = MGTextRange.FromStartAndLength(removedRange.StartIndex, insertionText.Length);
        return new MGTextEditResult(removedRange, insertedRange, removedText, insertionText, Version);
    }

    public string GetText(MGTextRange range)
    {
        var actualRange = NormalizeRange(range);
        return Text.Substring(actualRange.StartIndex, actualRange.Length);
    }

    public MGTextBufferSnapshot CreateSnapshot()
        => new(Text, Version, _lineStarts);

    public bool RestoreSnapshot(MGTextBufferSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        return SetText(snapshot.Text);
    }

    public bool TryGetPosition(int index, out MGTextPosition position)
    {
        if (index < 0 || index > Length)
        {
            position = default;
            return false;
        }

        var lineIndex = GetLineIndexForIndex(index);
        position = new MGTextPosition(lineIndex, index - _lineStarts[lineIndex]);
        return true;
    }

    public MGTextPosition GetPosition(int index)
    {
        var actualIndex = Math.Clamp(index, 0, Length);
        TryGetPosition(actualIndex, out var position);
        return position;
    }

    public bool TryGetIndex(MGTextPosition position, out int index)
    {
        if (position.Line < 0 || position.Line >= LineCount || position.Column < 0)
        {
            index = default;
            return false;
        }

        var lineLength = GetLineLength(position.Line);
        if (position.Column > lineLength)
        {
            index = default;
            return false;
        }

        index = _lineStarts[position.Line] + position.Column;
        return true;
    }

    public int GetIndex(MGTextPosition position)
    {
        var clampedPosition = position.ClampToNonNegative();
        var lineIndex = Math.Clamp(clampedPosition.Line, 0, LineCount - 1);
        var column = Math.Clamp(clampedPosition.Column, 0, GetLineLength(lineIndex));
        return _lineStarts[lineIndex] + column;
    }

    public int GetLineStartIndex(int lineIndex)
    {
        ValidateLineIndex(lineIndex);
        return _lineStarts[lineIndex];
    }

    public int GetLineLength(int lineIndex)
    {
        ValidateLineIndex(lineIndex);
        var lineStartIndex = _lineStarts[lineIndex];
        var lineEndIndex = GetLineEndIndex(lineIndex);
        return Math.Max(0, lineEndIndex - lineStartIndex);
    }

    public string GetLineText(int lineIndex)
    {
        var lineStartIndex = GetLineStartIndex(lineIndex);
        var lineLength = GetLineLength(lineIndex);
        return Text.Substring(lineStartIndex, lineLength);
    }

    public int GetVisualColumn(MGTextPosition position, int tabSize)
    {
        var actualTabSize = Math.Max(1, tabSize);
        var lineIndex = Math.Clamp(position.Line, 0, LineCount - 1);
        var targetColumn = Math.Clamp(position.Column, 0, GetLineLength(lineIndex));
        var lineStartIndex = _lineStarts[lineIndex];
        var visualColumn = 0;

        for (var column = 0; column < targetColumn; column++)
        {
            var character = Text[lineStartIndex + column];
            visualColumn += character == '\t' ? actualTabSize - (visualColumn % actualTabSize) : 1;
        }

        return visualColumn;
    }

    public IReadOnlyList<MGTextLineSpan> SplitRangeByLine(MGTextRange range)
    {
        var actualRange = NormalizeRange(range);
        List<MGTextLineSpan> spans = new();

        if (actualRange.IsEmpty)
        {
            var position = GetPosition(actualRange.StartIndex);
            spans.Add(new MGTextLineSpan(position.Line, actualRange.StartIndex, actualRange.StartIndex, position.Column, position.Column));
            return spans;
        }

        var startLineIndex = GetLineIndexForIndex(actualRange.StartIndex);
        var endLineIndex = GetLineIndexForIndex(Math.Max(actualRange.StartIndex, actualRange.EndIndex - 1));

        for (var lineIndex = startLineIndex; lineIndex <= endLineIndex; lineIndex++)
        {
            var lineStartIndex = _lineStarts[lineIndex];
            var lineEndIndex = GetLineEndIndex(lineIndex);
            var spanStartIndex = Math.Max(actualRange.StartIndex, lineStartIndex);
            var spanEndIndex = Math.Min(actualRange.EndIndex, lineEndIndex);
            var startColumn = Math.Clamp(spanStartIndex - lineStartIndex, 0, GetLineLength(lineIndex));
            var endColumn = Math.Clamp(spanEndIndex - lineStartIndex, startColumn, GetLineLength(lineIndex));
            spans.Add(new MGTextLineSpan(lineIndex, spanStartIndex, spanEndIndex, startColumn, endColumn));
        }

        return spans;
    }

    private int GetLineEndIndex(int lineIndex)
    {
        var nextLineIndex = lineIndex + 1;
        if (nextLineIndex < _lineStarts.Count)
        {
            return Math.Max(_lineStarts[lineIndex], _lineStarts[nextLineIndex] - 1);
        }

        return Length;
    }

    private int GetLineIndexForIndex(int index)
    {
        var searchIndex = _lineStarts.BinarySearch(index);
        if (searchIndex >= 0)
        {
            return searchIndex;
        }

        return Math.Max(0, ~searchIndex - 1);
    }

    private void RebuildLineStarts()
    {
        _lineStarts.Clear();
        _lineStarts.Add(0);

        for (var index = 0; index < Text.Length; index++)
        {
            if (Text[index] == '\n')
            {
                _lineStarts.Add(index + 1);
            }
        }
    }

    private void ValidateLineIndex(int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= LineCount)
        {
            throw new ArgumentOutOfRangeException(nameof(lineIndex), lineIndex, $"{nameof(lineIndex)} must be within the current text line range.");
        }
    }
}