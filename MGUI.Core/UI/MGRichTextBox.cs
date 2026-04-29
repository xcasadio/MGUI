using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Styling;
using MGUI.Core.UI.Text;
using MGUI.Core.UI.TextEditing;
using System;
using System.Collections.Generic;

namespace MGUI.Core.UI
{
    public class MGRichTextBox : MGTextBox
    {
        private int _tabSize;
        private bool _showLineNumbers;
        private IRichTextSyntaxHighlighter _syntaxHighlighter;
        private MGRichTextSyntaxPalette _syntaxPalette;
        private readonly List<MGStyledTextSpan> _styledSpans = new();

        public MGTextBuffer TextBuffer { get; } = new();
        public IReadOnlyList<MGStyledTextSpan> StyledSpans => _styledSpans;
        public bool HasStyledSpans => _styledSpans.Count > 0;

        public IRichTextSyntaxHighlighter SyntaxHighlighter
        {
            get => _syntaxHighlighter;
            set
            {
                if (!ReferenceEquals(_syntaxHighlighter, value))
                {
                    _syntaxHighlighter = value;
                    RefreshSyntaxHighlighting();
                    NPC(nameof(SyntaxHighlighter));
                }
            }
        }

        public MGRichTextSyntaxPalette SyntaxPalette
        {
            get => _syntaxPalette ?? MGRichTextSyntaxPalette.Default;
            set
            {
                if (!ReferenceEquals(_syntaxPalette, value))
                {
                    _syntaxPalette = value;
                    RefreshSyntaxHighlighting();
                    NPC(nameof(SyntaxPalette));
                }
            }
        }

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

        public void SetStyledSpans(IEnumerable<MGStyledTextSpan> spans)
        {
            _styledSpans.Clear();
            if (spans != null)
            {
                foreach (MGStyledTextSpan span in spans)
                {
                    MGStyledTextSpan clampedSpan = span.Clamp(Text.Length);
                    if (!clampedSpan.IsEmpty)
                    {
                        _styledSpans.Add(clampedSpan);
                    }
                }
            }

            SortStyledSpans(_styledSpans);
            RebuildStyledTextRuns();
            NPC(nameof(StyledSpans));
            NPC(nameof(HasStyledSpans));
        }

        public void ClearStyledSpans()
        {
            if (_styledSpans.Count == 0)
            {
                return;
            }

            _styledSpans.Clear();
            TextBlockComponent.Element.ClearTextRuns();
            NPC(nameof(StyledSpans));
            NPC(nameof(HasStyledSpans));
        }

        public MGTextEditResult ApplyTextEdit(MGTextRange range, string text)
        {
            MGTextBuffer previewBuffer = new(Text);
            MGTextEditResult editResult = previewBuffer.ApplyEdit(range, text);
            SetText(previewBuffer.Text);
            SelectionState = MGTextSelectionState.EmptyAt(editResult.CaretIndexAfterEdit);
            return editResult;
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

            if (SyntaxHighlighter != null)
            {
                RefreshSyntaxHighlighting();
            }
            else if (HasStyledSpans)
            {
                RebuildStyledTextRuns();
            }

            return changed;
        }

        public void RefreshSyntaxHighlighting()
        {
            if (SyntaxHighlighter == null)
            {
                if (HasStyledSpans)
                {
                    ClearStyledSpans();
                }

                return;
            }

            MGRichTextHighlightResult result = SyntaxHighlighter.Highlight(new MGRichTextHighlightContext(Text, TextBuffer.Version, SyntaxPalette));
            if (result.Version == TextBuffer.Version)
            {
                SetStyledSpans(result.Spans);
            }
        }

        internal static IReadOnlyList<MGTextRun> BuildStyledTextRuns(string text, IEnumerable<MGStyledTextSpan> spans)
        {
            string sourceText = text ?? string.Empty;
            List<MGStyledTextSpan> orderedSpans = new();
            if (spans != null)
            {
                foreach (MGStyledTextSpan span in spans)
                {
                    MGStyledTextSpan clampedSpan = span.Clamp(sourceText.Length);
                    if (!clampedSpan.IsEmpty)
                    {
                        orderedSpans.Add(clampedSpan);
                    }
                }
            }

            SortStyledSpans(orderedSpans);

            List<MGTextRun> runs = new();
            int cursor = 0;
            for (int spanIndex = 0; spanIndex < orderedSpans.Count; spanIndex++)
            {
                MGStyledTextSpan span = orderedSpans[spanIndex];
                int spanStartIndex = Math.Max(cursor, span.Range.StartIndex);
                int spanEndIndex = Math.Max(spanStartIndex, span.Range.EndIndex);

                AddTextRun(runs, sourceText, cursor, spanStartIndex, MGRichTextStyle.Default);
                AddTextRun(runs, sourceText, spanStartIndex, spanEndIndex, span.Style);
                cursor = Math.Max(cursor, spanEndIndex);
            }

            AddTextRun(runs, sourceText, cursor, sourceText.Length, MGRichTextStyle.Default);
            return runs;
        }

        private void RebuildStyledTextRuns()
        {
            if (_styledSpans.Count == 0)
            {
                TextBlockComponent.Element.ClearTextRuns();
                return;
            }

            TextBlockComponent.Element.SetTextRuns(BuildStyledTextRuns(Text, _styledSpans));
        }

        private static void SortStyledSpans(List<MGStyledTextSpan> spans)
        {
            spans.Sort((left, right) =>
            {
                int startComparison = left.Range.StartIndex.CompareTo(right.Range.StartIndex);
                return startComparison != 0 ? startComparison : left.Range.EndIndex.CompareTo(right.Range.EndIndex);
            });
        }

        private static void AddTextRun(List<MGTextRun> runs, string text, int startIndex, int endIndex, MGRichTextStyle style)
        {
            if (endIndex <= startIndex)
            {
                return;
            }

            string runText = text.Substring(startIndex, endIndex - startIndex);
            runs.Add(new MGTextRunText(runText, ToTextRunConfig(style), null, null));
        }

        private static MGTextRunConfig ToTextRunConfig(MGRichTextStyle style)
        {
            MGTextRunUnderlineConfig underline = style.IsUnderlined ? new MGTextRunUnderlineConfig(true) : default;
            MGTextRunBackgroundConfig background = style.Background.HasValue
                ? new MGTextRunBackgroundConfig(style.Background.Value.AsFillBrush())
                : default;

            return new MGTextRunConfig(style.IsBold, style.IsItalic, 1.0f, style.Foreground, underline, background);
        }
    }
}