namespace MGUI.Core.UI.TextEditing
{
    internal sealed class MGRichTextEditController
    {
        private readonly MGTextUndoStack<EditSnapshot> _undoStack;
        private readonly MGTextUndoStack<EditSnapshot> _redoStack;

        public MGTextBuffer Buffer { get; }
        public MGTextSelectionState Selection { get; private set; }

        public MGRichTextEditController(string text = null, int undoLimit = 100)
        {
            Buffer = new MGTextBuffer(text);
            Selection = MGTextSelectionState.EmptyAt(0);
            _undoStack = new MGTextUndoStack<EditSnapshot>(undoLimit);
            _redoStack = new MGTextUndoStack<EditSnapshot>(undoLimit);
        }

        public void SetText(string text)
        {
            Buffer.SetText(text);
            Selection = Selection.Clamp(Buffer.Length);
            _undoStack.Clear();
            _redoStack.Clear();
        }

        public void MoveCaret(int caretIndex, bool extendSelection = false)
        {
            Selection = extendSelection
                ? Selection.ExtendTo(caretIndex, Buffer.Length)
                : Selection.MoveCaret(caretIndex, Buffer.Length);
        }

        public void Select(MGTextRange range)
            => Selection = MGTextSelectionState.SelectRange(range, Buffer.Length);

        public void SelectAll()
            => Select(new MGTextRange(0, Buffer.Length));

        public MGTextEditResult InsertText(string text)
            => Replace(Selection.Range, text);

        public MGTextEditResult Replace(MGTextRange range, string text)
        {
            PushUndoSnapshot();
            MGTextEditResult editResult = Buffer.ApplyEdit(range, text);
            Selection = Selection.MoveAfterEdit(editResult, Buffer.Length);
            _redoStack.Clear();
            return editResult;
        }

        public bool Backspace()
        {
            if (Selection.HasSelection)
            {
                Replace(Selection.Range, string.Empty);
                return true;
            }

            int caretIndex = Selection.CaretIndex;
            if (caretIndex <= 0)
            {
                return false;
            }

            Replace(new MGTextRange(caretIndex - 1, caretIndex), string.Empty);
            return true;
        }

        public bool DeleteForward()
        {
            if (Selection.HasSelection)
            {
                Replace(Selection.Range, string.Empty);
                return true;
            }

            int caretIndex = Selection.CaretIndex;
            if (caretIndex >= Buffer.Length)
            {
                return false;
            }

            Replace(new MGTextRange(caretIndex, caretIndex + 1), string.Empty);
            return true;
        }

        public bool TryUndo()
        {
            if (!_undoStack.TryPop(out EditSnapshot snapshot))
            {
                return false;
            }

            _redoStack.Push(CreateSnapshot());
            RestoreSnapshot(snapshot);
            return true;
        }

        public bool TryRedo()
        {
            if (!_redoStack.TryPop(out EditSnapshot snapshot))
            {
                return false;
            }

            _undoStack.Push(CreateSnapshot());
            RestoreSnapshot(snapshot);
            return true;
        }

        private void PushUndoSnapshot()
            => _undoStack.Push(CreateSnapshot());

        private EditSnapshot CreateSnapshot()
            => new(Buffer.Text, Selection);

        private void RestoreSnapshot(EditSnapshot snapshot)
        {
            Buffer.SetText(snapshot.Text);
            Selection = snapshot.Selection.Clamp(Buffer.Length);
        }

        private readonly record struct EditSnapshot(string Text, MGTextSelectionState Selection);
    }
}