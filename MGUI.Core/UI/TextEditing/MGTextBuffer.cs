namespace MGUI.Core.UI.TextEditing;

public sealed class MGTextBuffer
{
    private readonly List<int> _lineStarts = new() { 0 };
    private string _text = string.Empty;

    public string Text => _text;
    public int Length => _text.Length;
    public int Version { get; private set; }
    public int LineCount => _lineStarts.Count;
    public IReadOnlyList<int> LineStarts => _lineStarts;

    public MGTextBuffer(string text = null)
    {
        _text = NormalizeLineEndings(text);
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
        string normalizedText = NormalizeLineEndings(text);
        if (_text == normalizedText)
        {
            return false;
        }

        _text = normalizedText;
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
        MGTextRange removedRange = NormalizeRange(range);
        string insertionText = NormalizeLineEndings(text);
        string removedText = GetText(removedRange);
        string nextText = _text.Remove(removedRange.StartIndex, removedRange.Length).Insert(removedRange.StartIndex, insertionText);

        if (nextText != _text)
        {
            _text = nextText;
            Version++;
            RebuildLineStarts();
        }

        MGTextRange insertedRange = MGTextRange.FromStartAndLength(removedRange.StartIndex, insertionText.Length);
        return new MGTextEditResult(removedRange, insertedRange, removedText, insertionText, Version);
    }

    public string GetText(MGTextRange range)
    {
        MGTextRange actualRange = NormalizeRange(range);
        return _text.Substring(actualRange.StartIndex, actualRange.Length);
    }

    public MGTextBufferSnapshot CreateSnapshot()
        => new(_text, Version, _lineStarts);

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

        int lineIndex = GetLineIndexForIndex(index);
        position = new MGTextPosition(lineIndex, index - _lineStarts[lineIndex]);
        return true;
    }

    public MGTextPosition GetPosition(int index)
    {
        int actualIndex = Math.Clamp(index, 0, Length);
        TryGetPosition(actualIndex, out MGTextPosition position);
        return position;
    }

    public bool TryGetIndex(MGTextPosition position, out int index)
    {
        if (position.Line < 0 || position.Line >= LineCount || position.Column < 0)
        {
            index = default;
            return false;
        }

        int lineLength = GetLineLength(position.Line);
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
        MGTextPosition clampedPosition = position.ClampToNonNegative();
        int lineIndex = Math.Clamp(clampedPosition.Line, 0, LineCount - 1);
        int column = Math.Clamp(clampedPosition.Column, 0, GetLineLength(lineIndex));
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
        int lineStartIndex = _lineStarts[lineIndex];
        int lineEndIndex = GetLineEndIndex(lineIndex);
        return Math.Max(0, lineEndIndex - lineStartIndex);
    }

    public string GetLineText(int lineIndex)
    {
        int lineStartIndex = GetLineStartIndex(lineIndex);
        int lineLength = GetLineLength(lineIndex);
        return _text.Substring(lineStartIndex, lineLength);
    }

    public int GetVisualColumn(MGTextPosition position, int tabSize)
    {
        int actualTabSize = Math.Max(1, tabSize);
        int lineIndex = Math.Clamp(position.Line, 0, LineCount - 1);
        int targetColumn = Math.Clamp(position.Column, 0, GetLineLength(lineIndex));
        int lineStartIndex = _lineStarts[lineIndex];
        int visualColumn = 0;

        for (int column = 0; column < targetColumn; column++)
        {
            char character = _text[lineStartIndex + column];
            visualColumn += character == '\t' ? actualTabSize - (visualColumn % actualTabSize) : 1;
        }

        return visualColumn;
    }

    public IReadOnlyList<MGTextLineSpan> SplitRangeByLine(MGTextRange range)
    {
        MGTextRange actualRange = NormalizeRange(range);
        List<MGTextLineSpan> spans = new();

        if (actualRange.IsEmpty)
        {
            MGTextPosition position = GetPosition(actualRange.StartIndex);
            spans.Add(new MGTextLineSpan(position.Line, actualRange.StartIndex, actualRange.StartIndex, position.Column, position.Column));
            return spans;
        }

        int startLineIndex = GetLineIndexForIndex(actualRange.StartIndex);
        int endLineIndex = GetLineIndexForIndex(Math.Max(actualRange.StartIndex, actualRange.EndIndex - 1));

        for (int lineIndex = startLineIndex; lineIndex <= endLineIndex; lineIndex++)
        {
            int lineStartIndex = _lineStarts[lineIndex];
            int lineEndIndex = GetLineEndIndex(lineIndex);
            int spanStartIndex = Math.Max(actualRange.StartIndex, lineStartIndex);
            int spanEndIndex = Math.Min(actualRange.EndIndex, lineEndIndex);
            int startColumn = Math.Clamp(spanStartIndex - lineStartIndex, 0, GetLineLength(lineIndex));
            int endColumn = Math.Clamp(spanEndIndex - lineStartIndex, startColumn, GetLineLength(lineIndex));
            spans.Add(new MGTextLineSpan(lineIndex, spanStartIndex, spanEndIndex, startColumn, endColumn));
        }

        return spans;
    }

    private int GetLineEndIndex(int lineIndex)
    {
        int nextLineIndex = lineIndex + 1;
        if (nextLineIndex < _lineStarts.Count)
        {
            return Math.Max(_lineStarts[lineIndex], _lineStarts[nextLineIndex] - 1);
        }

        return Length;
    }

    private int GetLineIndexForIndex(int index)
    {
        int searchIndex = _lineStarts.BinarySearch(index);
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

        for (int index = 0; index < _text.Length; index++)
        {
            if (_text[index] == '\n')
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