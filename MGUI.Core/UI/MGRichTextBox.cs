using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;
using MGUI.Core.UI.Text;
using MGUI.Core.UI.TextEditing;
using MGUI.Shared.Input.Keyboard;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI;

public class MGRichTextBox : MGTextBox
{
    private int _tabSize;
    private bool _showLineNumbers;
    private IRichTextSyntaxHighlighter _syntaxHighlighter;
    private IRichTextCompletionProvider _completionProvider;
    private MGRichTextSyntaxPalette _syntaxPalette;
    private readonly List<MGStyledTextSpan> _styledSpans = new();

    private readonly record struct StyledTextSegment(int StartIndex, int EndIndex, MGRichTextStyle Style);

    public MGTextBuffer TextBuffer { get; } = new();
    public MGRichTextCompletionPopupController CompletionPopup { get; } = new();
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

    public IRichTextCompletionProvider CompletionProvider
    {
        get => _completionProvider;
        set
        {
            if (!ReferenceEquals(_completionProvider, value))
            {
                _completionProvider = value;
                NPC(nameof(CompletionProvider));
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
            TextBlockComponent.Element.VerticalContentAlignment = VerticalAlignment.Top;
            WrapText = false;
            MinLines = 4;
            AcceptsReturn = true;
            AcceptsTab = true;
            ShowLineNumbers = showLineNumbers;
            TabSize = tabSize;

            KeyboardHandler.Pressed += (_, e) => HandleCompletionPopupKey(e);
            KeyboardHandler.KeyRepeat += (_, e) => HandleCompletionPopupKey(e);
        }
    }

    private void HandleCompletionPopupKey(BaseKeyPressedEventArgs e)
    {
        if (CompletionPopup.TryHandleDismissKey(e.Key))
        {
            e.SetHandledBy(this, false);
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
        RebuildStyledTextRuns();
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

    public MGRichTextCompletionResult RequestCompletions(MGRichTextCompletionTrigger trigger = MGRichTextCompletionTrigger.Manual,
        char? triggerCharacter = null)
    {
        if (CompletionProvider == null)
        {
            return MGRichTextCompletionResult.Empty;
        }

        MGRichTextCompletionContext context = MGRichTextCompletionService.CreateContext(Text, CaretIndex, trigger, triggerCharacter, TextBuffer.Version);
        return CompletionProvider.GetCompletions(context);
    }

    public bool OpenCompletionPopup(MGRichTextCompletionTrigger trigger = MGRichTextCompletionTrigger.Manual, char? triggerCharacter = null)
        => CompletionPopup.Open(RequestCompletions(trigger, triggerCharacter));

    public void CloseCompletionPopup()
        => CompletionPopup.Close();

    public bool MoveCompletionSelection(int delta)
        => CompletionPopup.MoveSelection(delta);

    public bool AcceptSelectedCompletion()
    {
        if (!CompletionPopup.TryAcceptSelected(out MGRichTextCompletionAcceptance acceptance))
        {
            return false;
        }

        ApplyTextEdit(acceptance.ReplacementRange, acceptance.InsertText);
        SelectionState = MGTextSelectionState.EmptyAt(acceptance.NewCaretIndex);
        return true;
    }

    public bool AcceptCompletion(MGRichTextCompletionItem item, MGTextRange replacementRange)
    {
        if (item == null)
        {
            return false;
        }

        MGRichTextCompletionAcceptance acceptance = MGRichTextCompletionService.CreateAcceptance(item, replacementRange);
        ApplyTextEdit(acceptance.ReplacementRange, acceptance.InsertText);
        SelectionState = MGTextSelectionState.EmptyAt(acceptance.NewCaretIndex);
        return true;
    }

    protected override bool SetText(string Value, bool ExecuteEvenIfSameValue, bool SuppressLayoutChanged)
    {
        string normalizedValue = MGTextBuffer.NormalizeLineEndings(Value);
        bool changed = base.SetText(normalizedValue, ExecuteEvenIfSameValue, SuppressLayoutChanged);
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

    protected override void UpdateFormattedText(bool Silent)
    {
        if (SyntaxHighlighter == null && !HasStyledSpans)
        {
            base.UpdateFormattedText(Silent);
            return;
        }

        RebuildStyledTextRuns(Silent);
    }

    internal static IReadOnlyList<MGTextRun> BuildStyledTextRuns(string text, IEnumerable<MGStyledTextSpan> spans)
        => BuildStyledTextRuns(text, spans, null, null);

    internal static IReadOnlyList<MGTextRun> BuildStyledTextRuns(string text, IEnumerable<MGStyledTextSpan> spans, MGTextRange? selectionRange, Color? selectionBackground)
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
        List<StyledTextSegment> segments = BuildStyledTextSegments(sourceText.Length, orderedSpans);
        MGTextRange actualSelection = selectionRange.GetValueOrDefault().Clamp(sourceText.Length);
        bool hasSelection = selectionRange.HasValue && selectionBackground.HasValue && !actualSelection.IsEmpty;

        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            StyledTextSegment segment = segments[segmentIndex];
            if (!hasSelection || segment.EndIndex <= actualSelection.StartIndex || segment.StartIndex >= actualSelection.EndIndex)
            {
                AddTextRun(runs, sourceText, segment.StartIndex, segment.EndIndex, segment.Style);
                continue;
            }

            int selectedStartIndex = Math.Max(segment.StartIndex, actualSelection.StartIndex);
            int selectedEndIndex = Math.Min(segment.EndIndex, actualSelection.EndIndex);
            AddTextRun(runs, sourceText, segment.StartIndex, selectedStartIndex, segment.Style);

            MGRichTextStyle selectedStyle = new MGRichTextStyle(Background: selectionBackground.Value).MergeOver(segment.Style);
            AddTextRun(runs, sourceText, selectedStartIndex, selectedEndIndex, selectedStyle);
            AddTextRun(runs, sourceText, selectedEndIndex, segment.EndIndex, segment.Style);
        }

        return runs;
    }

    private static List<StyledTextSegment> BuildStyledTextSegments(int textLength, IReadOnlyList<MGStyledTextSpan> orderedSpans)
    {
        List<StyledTextSegment> segments = new();
        int cursor = 0;
        for (int spanIndex = 0; spanIndex < orderedSpans.Count; spanIndex++)
        {
            MGStyledTextSpan span = orderedSpans[spanIndex];
            int spanStartIndex = Math.Max(cursor, span.Range.StartIndex);
            int spanEndIndex = Math.Max(spanStartIndex, span.Range.EndIndex);

            AddStyledTextSegment(segments, cursor, spanStartIndex, MGRichTextStyle.Default);
            AddStyledTextSegment(segments, spanStartIndex, spanEndIndex, span.Style);
            cursor = Math.Max(cursor, spanEndIndex);
        }

        AddStyledTextSegment(segments, cursor, textLength, MGRichTextStyle.Default);
        return segments;
    }

    private static void AddStyledTextSegment(List<StyledTextSegment> segments, int startIndex, int endIndex, MGRichTextStyle style)
    {
        if (endIndex > startIndex)
        {
            segments.Add(new StyledTextSegment(startIndex, endIndex, style));
        }
    }

    private void RebuildStyledTextRuns(bool silent = false)
    {
        MGTextRange? selectionRange = GetSelectionRange();
        if (_styledSpans.Count == 0 && !selectionRange.HasValue && SyntaxHighlighter == null)
        {
            TextBlockComponent.Element.ClearTextRuns(silent);
            return;
        }

        Color? selectionBackground = selectionRange.HasValue ? GetCurrentSelectionBackground() : null;
        TextBlockComponent.Element.SetTextRuns(BuildStyledTextRuns(Text, _styledSpans, selectionRange, selectionBackground), silent);
    }

    private MGTextRange? GetSelectionRange()
    {
        if (!CurrentSelection.HasValue || CurrentSelection.Value.ActualLength(Text) <= 0)
        {
            return null;
        }

        return new MGTextRange(CurrentSelection.Value.ActualStartIndex(Text), CurrentSelection.Value.ActualEndIndex(Text));
    }

    private Color GetCurrentSelectionBackground()
    {
        bool hasFocus = GetDesktop().FocusedKeyboardHandler == this;
        return hasFocus ? FocusedSelectionBackgroundColor : UnfocusedSelectionBackgroundColor;
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

        MGTextRunConfig runConfig = ToTextRunConfig(style);
        int textStartIndex = startIndex;
        for (int index = startIndex; index < endIndex; index++)
        {
            char value = text[index];
            if (value != '\r' && value != '\n')
            {
                continue;
            }

            AddTextRunSegment(runs, text, textStartIndex, index, runConfig);

            int lineBreakCharacterCount = 1;
            if (value == '\r' && index + 1 < endIndex && text[index + 1] == '\n')
            {
                lineBreakCharacterCount = 2;
                index++;
            }

            runs.Add(new MGTextRunLineBreak(lineBreakCharacterCount));
            textStartIndex = index + 1;
        }

        AddTextRunSegment(runs, text, textStartIndex, endIndex, runConfig);
    }

    private static void AddTextRunSegment(List<MGTextRun> runs, string text, int startIndex, int endIndex, MGTextRunConfig config)
    {
        if (endIndex <= startIndex)
        {
            return;
        }

        runs.Add(new MGTextRunText(text.Substring(startIndex, endIndex - startIndex), config, null, null));
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