using MGUI.Core.UI.Styling;
using MGUI.Core.UI.TextEditing;
using System;

namespace MGUI.Core.UI
{
    public class MGRichTextBox : MGTextBox
    {
        private int _tabSize;
        private bool _showLineNumbers;

        public MGTextBuffer TextBuffer { get; } = new();

        public int TabSize
        {
            get => _tabSize;
            set
            {
                int actualValue = Math.Max(1, value);
                if (_tabSize != actualValue)
                {
                    _tabSize = actualValue;
                    NPC(nameof(TabSize));
                }
            }
        }

        public bool ShowLineNumbers
        {
            get => _showLineNumbers;
            set
            {
                if (_showLineNumbers != value)
                {
                    _showLineNumbers = value;
                    LayoutChanged(this, true);
                    NPC(nameof(ShowLineNumbers));
                }
            }
        }

        public int CaretIndex
        {
            get => Caret.HasPosition ? MGTextEditingInputHelpers.NormalizeEditableCaretIndex(Caret.Position.Value.IndexInOriginalText, Text.Length) : Text.Length;
            set
            {
                int actualValue = MGTextEditingInputHelpers.NormalizeEditableCaretIndex(value, Text.Length);
                if (Text.Length > 0)
                {
                    Caret.MoveToOriginalCharacterIndexOrEnd(actualValue, true);
                }

                NPC(nameof(CaretIndex));
            }
        }

        public MGTextSelectionState SelectionState
        {
            get
            {
                if (CurrentSelection.HasValue)
                {
                    TextSelection selection = CurrentSelection.Value;
                    return new MGTextSelectionState(selection.Index1, selection.Index2).Clamp(Text.Length);
                }

                return MGTextSelectionState.EmptyAt(CaretIndex);
            }
            set
            {
                MGTextSelectionState actualValue = value.Clamp(Text.Length);
                CurrentSelection = actualValue.HasSelection ? new TextSelection(actualValue.AnchorIndex, actualValue.ActiveIndex) : null;
                CaretIndex = actualValue.CaretIndex;
                NPC(nameof(SelectionState));
            }
        }

        public MGRichTextBox(MGWindow window, int? characterLimit = null, bool showLineNumbers = true, int tabSize = 4)
            : base(window, MGElementType.RichTextBox, characterLimit, false, true)
        {
            using (BeginInitializing())
            {
                DefaultControlTemplateName = MGControlTemplateCatalog.TextBoxTemplateName;
                HorizontalContentAlignment = HorizontalAlignment.Left;
                VerticalContentAlignment = VerticalAlignment.Top;
                WrapText = false;
                MinLines = 4;
                AcceptsReturn = true;
                AcceptsTab = true;
                ShowLineNumbers = showLineNumbers;
                TabSize = tabSize;
            }
        }

        protected override bool SetText(string Value, bool ExecuteEvenIfSameValue)
        {
            string normalizedValue = MGTextBuffer.NormalizeLineEndings(Value);
            bool changed = base.SetText(normalizedValue, ExecuteEvenIfSameValue);
            if (TextBuffer != null && (changed || TextBuffer.Text != Text))
            {
                TextBuffer.SetText(Text);
                NPC(nameof(TextBuffer));
            }

            return changed;
        }
    }
}