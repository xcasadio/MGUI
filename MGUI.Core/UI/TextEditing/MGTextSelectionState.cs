using System;

namespace MGUI.Core.UI.TextEditing
{
    public readonly record struct MGTextSelectionState(int AnchorIndex, int ActiveIndex)
    {
        public static MGTextSelectionState EmptyAt(int caretIndex) => new(caretIndex, caretIndex);

        public MGTextRange Range => new(AnchorIndex, ActiveIndex);
        public int CaretIndex => ActiveIndex;
        public bool HasSelection => !Range.IsEmpty;

        public MGTextSelectionState Clamp(int textLength)
        {
            int actualLength = Math.Max(0, textLength);
            return new(Math.Clamp(AnchorIndex, 0, actualLength), Math.Clamp(ActiveIndex, 0, actualLength));
        }

        public MGTextSelectionState MoveCaret(int caretIndex, int textLength)
        {
            int actualLength = Math.Max(0, textLength);
            int actualCaretIndex = Math.Clamp(caretIndex, 0, actualLength);
            return EmptyAt(actualCaretIndex);
        }

        public MGTextSelectionState ExtendTo(int activeIndex, int textLength)
        {
            int actualLength = Math.Max(0, textLength);
            return new(Math.Clamp(AnchorIndex, 0, actualLength), Math.Clamp(activeIndex, 0, actualLength));
        }

        public static MGTextSelectionState SelectRange(MGTextRange range, int textLength)
        {
            MGTextRange actualRange = range.Clamp(textLength).Normalize();
            return new(actualRange.StartIndex, actualRange.EndIndex);
        }

        public MGTextSelectionState MoveAfterEdit(MGTextEditResult editResult, int textLength)
            => EmptyAt(Math.Clamp(editResult.CaretIndexAfterEdit, 0, Math.Max(0, textLength)));
    }
}