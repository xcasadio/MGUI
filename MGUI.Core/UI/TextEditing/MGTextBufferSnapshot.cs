namespace MGUI.Core.UI.TextEditing;

public sealed class MGTextBufferSnapshot
{
    private readonly int[] _lineStarts;

    public string Text { get; }
    public int Version { get; }
    public int LineCount => _lineStarts.Length;
    public IReadOnlyList<int> LineStarts => _lineStarts;

    internal MGTextBufferSnapshot(string text, int version, IReadOnlyList<int> lineStarts)
    {
        Text = text ?? string.Empty;
        Version = version;
        _lineStarts = new int[lineStarts?.Count ?? 0];

        if (lineStarts == null || lineStarts.Count == 0)
        {
            _lineStarts = new[] { 0 };
            return;
        }

        for (int index = 0; index < lineStarts.Count; index++)
        {
            _lineStarts[index] = lineStarts[index];
        }
    }
}