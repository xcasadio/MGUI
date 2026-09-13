namespace MGUI.Core.UI.TextEditing;

public readonly record struct MGTextRange(int Index1, int Index2)
{
    public int StartIndex => Math.Min(Index1, Index2);
    public int EndIndex => Math.Max(Index1, Index2);
    public int Length => EndIndex - StartIndex;
    public bool IsEmpty => Length == 0;

    public static MGTextRange EmptyAt(int index) => new(index, index);
    public static MGTextRange FromStartAndLength(int startIndex, int length) => new(startIndex, startIndex + Math.Max(0, length));

    public MGTextRange Normalize() => new(StartIndex, EndIndex);

    public MGTextRange Clamp(int textLength)
    {
        var actualLength = Math.Max(0, textLength);
        var startIndex = Math.Clamp(StartIndex, 0, actualLength);
        var endIndex = Math.Clamp(EndIndex, startIndex, actualLength);
        return new(startIndex, endIndex);
    }

    public bool ContainsIndex(int index, bool includeEnd = false)
        => includeEnd ? index >= StartIndex && index <= EndIndex : index >= StartIndex && index < EndIndex;
}