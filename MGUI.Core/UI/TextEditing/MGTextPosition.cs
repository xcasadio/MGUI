using System;

namespace MGUI.Core.UI.TextEditing
{
    public readonly record struct MGTextPosition(int Line, int Column) : IComparable<MGTextPosition>
    {
        public static MGTextPosition Start { get; } = new(0, 0);

        public int CompareTo(MGTextPosition other)
        {
            int lineComparison = Line.CompareTo(other.Line);
            return lineComparison != 0 ? lineComparison : Column.CompareTo(other.Column);
        }

        public MGTextPosition ClampToNonNegative()
            => new(Math.Max(0, Line), Math.Max(0, Column));
    }
}