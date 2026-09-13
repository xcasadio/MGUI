namespace MGUI.Core.UI.TextEditing;

public readonly record struct MGTextLineSpan(int LineIndex, int StartIndex, int EndIndex, int StartColumn, int EndColumn)
{
    public int Length => EndIndex - StartIndex;
    public bool IsEmpty => Length == 0;
}