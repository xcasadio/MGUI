using System.Collections.ObjectModel;
using MGUI.Core.UI;
using MGUI.Core.UI.TextEditing;
using MGUI.Core.UI.TextEditing.Xaml;
using MGUI.Core.UI.XAML;

namespace MGUI.Editor.Text;

/// <summary>Composes <see cref="XamlEditorView.TextPane"/> with the XAML syntax highlighter and the loader's
/// diagnostics (<c>Docs/Tasks/xaml-editor-tasks.md</c>): the text pane shows XAML colors and underlines
/// every current <see cref="XamlLoaderDiagnostic"/> at its position, and <see cref="XamlEditorView.DiagnosticsPane"/>
/// is filled with a clickable list of them. Wired from <see cref="XamlEditorView"/>'s constructor, the same way
/// <see cref="Preview.XamlPreviewHost"/> is: a property, constructed once every other pane exists.</summary>
public sealed class XamlEditorTextPane
{
    private readonly XamlEditorView _view;
    private readonly MGListBox<XamlLoaderDiagnostic> _diagnosticsList;

    /// <summary>The current diagnostics, read by <see cref="DiagnosticsHighlighterDecorator"/> on every highlight
    /// pass; kept in sync with <see cref="Preview.XamlPreviewHost.Diagnostics"/> by <see cref="RefreshDiagnostics"/>.</summary>
    private IReadOnlyList<XamlLoaderDiagnostic> _diagnostics = Array.Empty<XamlLoaderDiagnostic>();

    public XamlEditorTextPane(XamlEditorView view)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));

        //  The selection (XamlEditorSelection.SyncCaretFromSelection) moves this pane's caret without taking keyboard
        //  focus, so the caret has to stay visible without focus, or a preview click / tree selection would move it
        //  somewhere the author cannot see.
        _view.TextPane.Caret.ShowWhenUnfocused = true;

        _view.TextPane.SyntaxHighlighter = new DiagnosticsHighlighterDecorator(this);

        _diagnosticsList = new MGListBox<XamlLoaderDiagnostic>(view.Window)
        {
            ItemTemplate = CreateDiagnosticItemContent,
        };
        _diagnosticsList.SelectionChanged += OnDiagnosticsSelectionChanged;
        view.DiagnosticsPane.SetContent(_diagnosticsList);

        view.PreviewHost.PreviewUpdated += (_, _) => RefreshDiagnostics();
        RefreshDiagnostics();
    }

    private void RefreshDiagnostics()
    {
        _diagnostics = _view.PreviewHost.Diagnostics;
        _diagnosticsList.SetItemsSource(_diagnostics.ToList());
        //  The diagnostics feed the highlighter decorator too: a re-parse that only changes diagnostics (the text
        //  itself unchanged) must still refresh the underlines.
        _view.TextPane.RefreshSyntaxHighlighting();
    }

    /// <summary>Clicking a diagnostics-list entry places the caret at that diagnostic's position and focuses the
    /// text pane. The "XAML" dockable is not activated here if it is an inactive tab or a closed auto-hide drawer:
    /// this view does not own the dock host it may be hosted by (X1b), so it cannot reach across to activate a tab
    /// or open a drawer; the caret is simply set, and shows once the pane comes back on its own.</summary>
    private void OnDiagnosticsSelectionChanged(object sender, ReadOnlyCollection<MGListBoxItem<XamlLoaderDiagnostic>> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var diagnostic = items[0].Data;
        if (TryComputeIndex(_view.TextPane.TextBuffer, diagnostic, out var index))
        {
            _view.TextPane.CaretIndex = index;
        }

        _view.TextPane.Focus();
    }

    private MGElement CreateDiagnosticItemContent(XamlLoaderDiagnostic diagnostic)
    {
        var line = diagnostic.LineNumber?.ToString() ?? "-";
        var column = diagnostic.LinePosition?.ToString() ?? "-";
        return new MGTextBlock(_view.Window, $"[{diagnostic.Code}] {diagnostic.Message} ({line}:{column})");
    }

    /// <summary>Maps a 1-based loader position to a 0-based character index into the text pane's buffer, per the
    /// conversion this slice's brief measured against <c>XamlLoaderLineInfoTests</c>: <c>new MGTextPosition(LineNumber - 1,
    /// LinePosition - 1 - correction)</c> then <see cref="MGTextBuffer.TryGetIndex"/>. <paramref name="correction"/>
    /// accounts for <see cref="MGUI.Core.Tooling.UIToolingService.LoadPreview"/>'s <c>replaceLinebreakLiterals: true</c>,
    /// which rewrites every literal two-character <c>\n</c> escape inside the markup into the six-character XML
    /// character reference <c>&amp;#x0a;</c> before parsing: each occurrence earlier on the same line shifts the
    /// loader-reported column right by four characters relative to the text actually in the editor.<para/>
    /// Returns false (never throws) for a diagnostic with no line/column, or one whose computed position falls
    /// outside the text.</summary>
    internal static bool TryComputeIndex(MGTextBuffer buffer, XamlLoaderDiagnostic diagnostic, out int index)
    {
        index = default;
        if (diagnostic.LineNumber is not int lineNumber || diagnostic.LinePosition is not int linePosition)
        {
            return false;
        }

        var lineIndex = lineNumber - 1;
        if (lineIndex < 0 || lineIndex >= buffer.LineCount)
        {
            return false;
        }

        var preparedColumn = linePosition - 1;
        if (preparedColumn < 0)
        {
            return false;
        }

        var correction = ComputeCorrection(buffer.GetLineText(lineIndex), preparedColumn);
        return buffer.TryGetIndex(new MGTextPosition(lineIndex, preparedColumn - correction), out index);
    }

    /// <summary>How many characters the loader-reported column must be shifted left, to land back on the original
    /// line's text, because of literal <c>\n</c> escapes the loader expanded before that column. Converges in a
    /// handful of iterations: each guess narrows the window that is searched for escapes, which can only shrink the
    /// count found, so the correction it computes only ever goes down until it stabilizes.</summary>
    private static int ComputeCorrection(string originalLineText, int preparedColumn)
    {
        if (string.IsNullOrEmpty(originalLineText) || preparedColumn <= 0)
        {
            return 0;
        }

        var correction = 0;
        for (var iteration = 0; iteration < 8; iteration++)
        {
            var candidateColumn = Math.Max(0, preparedColumn - correction);
            var searchLength = Math.Min(candidateColumn, originalLineText.Length);
            var occurrenceCount = CountLiteralNewlineEscapes(originalLineText, searchLength);
            var newCorrection = occurrenceCount * 4;
            if (newCorrection == correction)
            {
                return correction;
            }

            correction = newCorrection;
        }

        return correction;
    }

    /// <summary>Counts occurrences of the literal two-character sequence <c>\n</c> (a backslash followed by the
    /// letter 'n', as typed in an attribute value, not an actual line break) within the first <paramref name="length"/>
    /// characters of <paramref name="text"/>.</summary>
    private static int CountLiteralNewlineEscapes(string text, int length)
    {
        var count = 0;
        var actualLength = Math.Min(length, text.Length);
        for (var index = 0; index < actualLength - 1; index++)
        {
            if (text[index] == '\\' && text[index + 1] == 'n')
            {
                count++;
                index++;
            }
        }

        return count;
    }

    /// <summary>Wraps <see cref="XamlSyntaxHighlighter"/> and appends one underlined span per current diagnostic.
    /// <see cref="MGRichTextBox"/> holds exactly one highlighter, so composition happens by decoration rather than
    /// by giving the text box a list of highlighters. It never modifies the text and always echoes
    /// <see cref="MGRichTextHighlightContext.Version"/>.<para/>
    /// Overlap: <see cref="MGRichTextBox.BuildStyledTextRuns"/> does not merge two spans that cover the same
    /// characters -- it advances a cursor span by span and only ever shows the earliest one's style for the
    /// overlapping part, silently clipping a later, fully-covered span down to nothing. To still show both the XAML
    /// color and the diagnostic underline on the same characters, this decorator never emits two spans over the same
    /// range: it splits every span the base highlighter returns at the diagnostic's boundaries and merges the
    /// diagnostic's underline into the overlapping piece with <see cref="MGRichTextStyle.MergeOver"/> (so the
    /// existing foreground survives, since the diagnostic's style carries no foreground of its own), filling any part
    /// of the diagnostic's range that carried no highlighter span at all with a plain underlined span. The result is
    /// still a flat, non-overlapping set of spans.</summary>
    private sealed class DiagnosticsHighlighterDecorator : IRichTextSyntaxHighlighter
    {
        private const string DiagnosticClassification = "xaml.diagnostic";
        private static readonly MGRichTextStyle UnderlineOnlyStyle = new(IsUnderlined: true);

        private readonly XamlSyntaxHighlighter _innerHighlighter = new();
        private readonly XamlEditorTextPane _owner;

        public DiagnosticsHighlighterDecorator(XamlEditorTextPane owner)
        {
            _owner = owner;
        }

        public MGRichTextHighlightResult Highlight(MGRichTextHighlightContext context)
        {
            var innerResult = _innerHighlighter.Highlight(context);
            if (innerResult.Version != context.Version)
            {
                return innerResult;
            }

            List<MGStyledTextSpan> spans = new(innerResult.Spans);
            var buffer = new MGTextBuffer(context.Text);

            foreach (var diagnostic in _owner._diagnostics)
            {
                if (!TryComputeUnderlineRange(buffer, diagnostic, out var range))
                {
                    continue;
                }

                spans = ApplyUnderline(spans, range);
            }

            return new MGRichTextHighlightResult(context.Version, spans);
        }

        /// <summary>The range to underline for one diagnostic: the token starting at its computed position, or -- when
        /// no token starts there -- the rest of that line.</summary>
        private static bool TryComputeUnderlineRange(MGTextBuffer buffer, XamlLoaderDiagnostic diagnostic, out MGTextRange range)
        {
            range = default;
            if (!TryComputeIndex(buffer, diagnostic, out var index) || index < 0 || index > buffer.Length)
            {
                return false;
            }

            var lineIndex = buffer.GetPosition(index).Line;
            var lineEndIndex = buffer.GetLineStartIndex(lineIndex) + buffer.GetLineLength(lineIndex);

            var token = XamlTokenizer.Tokenize(buffer.Text).FirstOrDefault(candidate => candidate.Range.StartIndex == index && candidate.Kind != XamlTokenKind.EndOfFile);
            var endIndex = token.Range.Length > 0 ? token.Range.EndIndex : lineEndIndex;
            range = new MGTextRange(index, Math.Max(index, endIndex)).Clamp(buffer.Length);
            return !range.IsEmpty;
        }

        private static List<MGStyledTextSpan> ApplyUnderline(List<MGStyledTextSpan> baseSpans, MGTextRange range)
        {
            if (range.IsEmpty)
            {
                return baseSpans;
            }

            List<MGStyledTextSpan> result = new();
            var cursor = range.StartIndex;

            foreach (var span in baseSpans)
            {
                if (span.Range.EndIndex <= range.StartIndex || span.Range.StartIndex >= range.EndIndex)
                {
                    result.Add(span);
                    continue;
                }

                if (span.Range.StartIndex > cursor)
                {
                    result.Add(new MGStyledTextSpan(new MGTextRange(cursor, span.Range.StartIndex), UnderlineOnlyStyle, DiagnosticClassification));
                }

                if (span.Range.StartIndex < range.StartIndex)
                {
                    result.Add(span with { Range = new MGTextRange(span.Range.StartIndex, range.StartIndex) });
                }

                var overlapStart = Math.Max(span.Range.StartIndex, range.StartIndex);
                var overlapEnd = Math.Min(span.Range.EndIndex, range.EndIndex);
                result.Add(new MGStyledTextSpan(new MGTextRange(overlapStart, overlapEnd), UnderlineOnlyStyle.MergeOver(span.Style), span.Classification));

                if (span.Range.EndIndex > range.EndIndex)
                {
                    result.Add(span with { Range = new MGTextRange(range.EndIndex, span.Range.EndIndex) });
                }

                cursor = Math.Max(cursor, span.Range.EndIndex);
            }

            if (cursor < range.EndIndex)
            {
                result.Add(new MGStyledTextSpan(new MGTextRange(cursor, range.EndIndex), UnderlineOnlyStyle, DiagnosticClassification));
            }

            return result;
        }
    }
}
