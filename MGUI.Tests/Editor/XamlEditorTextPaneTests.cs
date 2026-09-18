using MGUI.Core.UI;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.TextEditing;
using MGUI.Core.UI.XAML;
using MGUI.Editor;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework.Input;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Editor;

/// <summary><see cref="MGUI.Editor.Text.XamlEditorTextPane"/> gives <see cref="XamlEditorView.TextPane"/> XAML colors
/// and error underlines, and fills <see cref="XamlEditorView.DiagnosticsPane"/> with the current diagnostics.
/// One test group per acceptance bullet of the "Volet texte" section of Docs/Tasks/xaml-editor-tasks.md that concerns
/// this class (the tokenizer/highlighter bullets are covered directly in MGUI.Tests/Text). Assertions are made only
/// against observable behaviour: styled spans, the diagnostics list's items, and the caret index -- never an
/// internal field, per this slice's brief.</summary>
public class XamlEditorTextPaneTests
{
    private const string Ns = "clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core";

    private static (GraphTestRuntime Runtime, MGDesktop Desktop, XamlEditorView View) CreateHostedView(int width = 1280, int height = 720)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, width, height));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, width, height) { WindowStyle = WindowStyle.None };

        XamlEditorSession session = new();
        XamlEditorView view = new(window, session);

        MGDockHost host = view.CreateDockHost();
        window.SetContent(host);
        desktop.Windows.Add(window);

        Frame(runtime, desktop, 0);
        Frame(runtime, desktop, 1);

        return (runtime, desktop, view);
    }

    private static void Frame(GraphTestRuntime runtime, MGDesktop desktop, int frameIndex)
    {
        Point p = new(1, 1);
        MouseState mouse = new(p.X, p.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * frameIndex), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
        desktop.Update();
    }

    /// <summary>Sets the session's text and advances past the 250 ms debounce so exactly one reparse happens, then
    /// returns the single diagnostic it produced.</summary>
    private static XamlLoaderDiagnostic ReparseExpectingOneDiagnostic(GraphTestRuntime runtime, MGDesktop desktop, XamlEditorView view, string text, int startFrame)
    {
        view.Session.Text = text;
        Frame(runtime, desktop, startFrame);
        Frame(runtime, desktop, startFrame + 20); // > 250ms later: the debounced reparse has happened.
        return Assert.Single(view.PreviewHost.Diagnostics);
    }

    private static bool TryFindUnderlinedSpanAt(XamlEditorView view, int expectedStartIndex, int expectedEndIndex, out MGStyledTextSpan span)
    {
        foreach (var candidate in view.TextPane.StyledSpans)
        {
            if (candidate.Style.IsUnderlined && candidate.Range.StartIndex == expectedStartIndex && candidate.Range.EndIndex == expectedEndIndex)
            {
                span = candidate;
                return true;
            }
        }

        span = default;
        return false;
    }

    // -- Underline position: early in the document (the Window root's own invalid attribute, on line 1) --

    [Fact]
    public void Diagnostic_EarlyInTheDocument_UnderlinesTheFailingAttributeNameOnLineOne()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, XamlEditorView view) = CreateHostedView();

        string text = $"<Window xmlns=\"{Ns}\" Width=\"abc\" Left=\"0\" Top=\"0\" Height=\"300\" />\n";
        XamlLoaderDiagnostic diagnostic = ReparseExpectingOneDiagnostic(runtime, desktop, view, text, 2);

        Assert.Equal(1, diagnostic.LineNumber);
        Assert.NotNull(diagnostic.LinePosition);

        int markerIndex = text.IndexOf("Width=\"abc\"", StringComparison.Ordinal);
        Assert.True(view.TextPane.TextBuffer.TryGetIndex(new MGTextPosition(diagnostic.LineNumber.Value - 1, diagnostic.LinePosition.Value - 1), out int expectedIndex));
        Assert.Equal(markerIndex, expectedIndex);

        Assert.True(TryFindUnderlinedSpanAt(view, markerIndex, markerIndex + "Width".Length, out _));
    }

    // -- Underline position: middle of the document (same shape as XamlLoaderLineInfoTests) --

    [Fact]
    public void Diagnostic_InTheMiddleOfTheDocument_UnderlinesTheFailingAttributeName()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, XamlEditorView view) = CreateHostedView();

        string text =
            $"<Window xmlns=\"{Ns}\" Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\">\n" +
            "    <StackPanel>\n" +
            "        <Button Width=\"abc\" Content=\"Hi\" />\n" +
            "    </StackPanel>\n" +
            "</Window>\n";
        XamlLoaderDiagnostic diagnostic = ReparseExpectingOneDiagnostic(runtime, desktop, view, text, 2);

        Assert.Equal(3, diagnostic.LineNumber);

        int markerIndex = text.IndexOf("Width=\"abc\"", StringComparison.Ordinal);
        Assert.True(view.TextPane.TextBuffer.TryGetIndex(new MGTextPosition(diagnostic.LineNumber.Value - 1, diagnostic.LinePosition.Value - 1), out int expectedIndex));
        Assert.Equal(markerIndex, expectedIndex);

        Assert.True(TryFindUnderlinedSpanAt(view, markerIndex, markerIndex + "Width".Length, out _));
    }

    // -- Underline position: a line containing a literal \n (the four-column-per-occurrence correction) --

    [Fact]
    public void Diagnostic_OnALineContainingALiteralBackslashN_UnderlinesTheFailingAttributeNameAfterCorrection()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, XamlEditorView view) = CreateHostedView();

        string text =
            $"<Window xmlns=\"{Ns}\" Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\">\n" +
            "    <StackPanel>\n" +
            "        <TextBlock Text=\"a\\nb\" /><Button Width=\"abc\" Content=\"Hi\" />\n" +
            "    </StackPanel>\n" +
            "</Window>\n";
        XamlLoaderDiagnostic diagnostic = ReparseExpectingOneDiagnostic(runtime, desktop, view, text, 2);

        Assert.Equal(3, diagnostic.LineNumber);
        // One literal "\n" occurs before "Width=" on this line: the loader-reported column is 4 further right than
        // the raw text's column (Docs/Tasks/xaml-editor-tasks.md, and MGUI.Tests/Xaml/XamlLoaderLineInfoTests.cs).
        int markerIndex = text.IndexOf("Width=\"abc\"", StringComparison.Ordinal);
        Assert.NotEqual(markerIndex, diagnostic.LinePosition!.Value - 1 + text.LastIndexOf('\n', markerIndex));

        Assert.True(TryFindUnderlinedSpanAt(view, markerIndex, markerIndex + "Width".Length, out _));
    }

    // -- A diagnostic whose position falls outside the current text (made stale by a fast edit that shrank the
    //    document before the next debounced reparse) underlines nothing and does not throw. --

    [Fact]
    public void StaleDiagnostic_OutsideTheCurrentText_UnderlinesNothing_AndDoesNotThrow()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, XamlEditorView view) = CreateHostedView();

        string longInvalidText =
            $"<Window xmlns=\"{Ns}\" Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\">\n" +
            "    <StackPanel>\n" +
            "        <Button Width=\"abc\" Content=\"Hi\" />\n" +
            "    </StackPanel>\n" +
            "</Window>\n";
        XamlLoaderDiagnostic diagnostic = ReparseExpectingOneDiagnostic(runtime, desktop, view, longInvalidText, 2);
        Assert.Equal(3, diagnostic.LineNumber);

        //  The text shrinks to one short line immediately (TextPane/Session text is synchronous); the diagnostic
        //  above (still referencing line 3) is only replaced on the NEXT debounced reparse, so for one highlight
        //  pass it is stale relative to the now-shorter text.
        var exception = Record.Exception(() => view.TextPane.SetText("ab"));
        Assert.Null(exception);
        Assert.DoesNotContain(view.TextPane.StyledSpans, span => span.Style.IsUnderlined);
    }

    // -- The diagnostics list is the content of the "Diagnostics" dockable, and a click moves the caret --

    [Fact]
    public void DiagnosticsPane_ShowsTheCurrentDiagnostics_AndSelectingOneMovesTheCaretAndFocusesTheTextPane()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, XamlEditorView view) = CreateHostedView();

        string text =
            $"<Window xmlns=\"{Ns}\" Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\">\n" +
            "    <StackPanel>\n" +
            "        <Button Width=\"abc\" Content=\"Hi\" />\n" +
            "    </StackPanel>\n" +
            "</Window>\n";
        XamlLoaderDiagnostic diagnostic = ReparseExpectingOneDiagnostic(runtime, desktop, view, text, 2);

        MGListBox<XamlLoaderDiagnostic> diagnosticsList = Assert.IsType<MGListBox<XamlLoaderDiagnostic>>(view.DiagnosticsPane.Content);
        XamlLoaderDiagnostic listedDiagnostic = Assert.Single(diagnosticsList.ItemsSource);
        Assert.Equal(diagnostic, listedDiagnostic);

        diagnosticsList.SelectItem(listedDiagnostic, true);
        Frame(runtime, desktop, 60);

        int markerIndex = text.IndexOf("Width=\"abc\"", StringComparison.Ordinal);
        Assert.Equal(markerIndex, view.TextPane.CaretIndex);
        Assert.Same(view.TextPane, desktop.FocusedKeyboardHandler);
    }

    /// <summary>A diagnostic that carries no position underlines nothing: every failure the loader can raise today
    /// carries a line and a column, so this guard is reached through the conversion helper itself.</summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData(3, null)]
    [InlineData(null, 5)]
    public void DiagnosticWithoutAPosition_YieldsNoIndex(int? lineNumber, int? linePosition)
    {
        MGTextBuffer buffer = new();
        buffer.SetText("<Window>\n    <Button />\n</Window>\n");

        XamlLoaderDiagnostic diagnostic = new(
            XamlLoaderDiagnosticCode.ParseFailure,
            "Preview",
            "document.xaml",
            null,
            "no position",
            lineNumber,
            linePosition);

        Assert.False(MGUI.Editor.Text.XamlEditorTextPane.TryComputeIndex(buffer, diagnostic, out int index));
        Assert.Equal(0, index);
    }
}
