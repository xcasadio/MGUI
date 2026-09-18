using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Editor.Preview;

/// <summary>Renders the text of a <see cref="XamlEditorSession"/> into an <see cref="MGContentPresenter"/>, hot: every
/// call to <see cref="RequestRefresh"/> marks the document dirty, and the actual re-parse happens on the editor
/// window's own update tick once <see cref="DebounceDelay"/> has elapsed since the last request, measured with the
/// update tick's own time. No thread and no system timer are used anywhere in this class.<para/>
/// A <see cref="XamlLoaderException"/> raised while parsing keeps the previous <see cref="PreviewRoot"/> displayed and
/// publishes its diagnostic through <see cref="Diagnostics"/>; empty or whitespace-only text clears the preview instead.
/// A <see cref="Core.UI.XAML.Controls.Window"/> root is anchored to the top-left corner of the presenter's pane and kept at
/// its own declared size (never resized or scaled); a hidden pane (<c>Parent == null</c>) hides the root and skips
/// anchoring, but re-parses keep happening while it is hidden.</summary>
public class XamlPreviewHost
{
    private readonly MGWindow _window;
    private readonly XamlEditorSession _session;
    private readonly MGContentPresenter _presenter;
    private readonly MGOverlayPanel _pane;

    private bool _isDirty;
    private System.TimeSpan _lastKnownTotalElapsed;
    private System.TimeSpan _lastRequestElapsed;
    private Rectangle? _lastAnchoredPaneBounds;

    /// <summary>The <see cref="Visibility"/> the loaded document gave its root, restored when the pane is attached again:
    /// hiding the root while the pane is detached must not turn a root the document declared hidden into a visible one.</summary>
    private Visibility _declaredRootVisibility = Visibility.Visible;

    /// <summary>The <c>Left</c> and <c>Top</c> the loaded document gave a <see cref="MGWindow"/> root, kept because
    /// anchoring overwrites them: the pane is the preview's origin, and the window sits at its declared offset from
    /// that corner, the way it would sit at that offset from the corner of a desktop.</summary>
    private Microsoft.Xna.Framework.Point _declaredRootOffset;

    /// <summary>The last root that a re-parse produced. Null before the first successful re-parse, or after the
    /// session's text becomes empty or whitespace-only.</summary>
    public MGElement PreviewRoot { get; private set; }

    /// <summary>The diagnostic of the last failed re-parse, or an empty list when the last re-parse succeeded or the
    /// session's text was empty.</summary>
    public System.Collections.Generic.IReadOnlyList<XamlLoaderDiagnostic> Diagnostics { get; private set; }
        = System.Array.Empty<XamlLoaderDiagnostic>();

    /// <summary>Incremented on every completed re-parse cycle, whether it produced a new root, kept the previous one
    /// after a failure, or cleared the preview for empty text.</summary>
    public int PreviewVersion { get; private set; }

    /// <summary>Raised once per completed re-parse cycle, after <see cref="PreviewRoot"/> and <see cref="Diagnostics"/>
    /// have been updated.</summary>
    public event System.EventHandler PreviewUpdated;

    /// <summary>How long a burst of <see cref="RequestRefresh"/> calls must stay quiet, measured with the editor
    /// window's own update tick, before the pending re-parse actually runs. 250 ms by default.</summary>
    public System.TimeSpan DebounceDelay { get; set; } = System.TimeSpan.FromMilliseconds(250);

    private bool _isInteractive;
    /// <summary>False by default: the preview's root subtree receives no input (<see cref="MGElement.IsHitTestVisible"/>
    /// is cleared on it), so a click reaches the editor underneath instead. Setting this to true restores normal input
    /// on the current root; setting it back to false disables it again. Applied immediately to the current
    /// <see cref="PreviewRoot"/>, and to every future one.</summary>
    public bool IsInteractive
    {
        get => _isInteractive;
        set
        {
            if (_isInteractive != value)
            {
                _isInteractive = value;
                if (PreviewRoot != null)
                {
                    PreviewRoot.IsHitTestVisible = _isInteractive;
                }
            }
        }
    }

    /// <param name="view">The view whose <see cref="XamlEditorView.Window"/>, <see cref="XamlEditorView.Session"/> and
    /// <see cref="XamlEditorView.PreviewPresenter"/>/<see cref="XamlEditorView.PreviewPane"/> this host renders into.
    /// The host subscribes to the session's <see cref="XamlEditorSession.TextChanged"/> and
    /// <see cref="XamlEditorSession.DesignDataContextChanged"/> events and to the window's update tick itself: no
    /// further wiring is required from the caller beyond constructing this host.</param>
    public XamlPreviewHost(XamlEditorView view)
    {
        if (view == null)
        {
            throw new System.ArgumentNullException(nameof(view));
        }

        _window = view.Window;
        _session = view.Session;
        _presenter = view.PreviewPresenter;
        _pane = view.PreviewPane;

        _session.TextChanged += (_, _) => RequestRefresh();
        _session.DesignDataContextChanged += (_, _) => RequestRefresh();
        _window.OnBeginUpdate += OnWindowBeginUpdate;
    }

    /// <summary>Marks the session's text dirty: the next re-parse runs once <see cref="DebounceDelay"/> has elapsed,
    /// since this call, on the editor window's update tick. Calling this again before that delay elapses restarts the
    /// wait, so a burst of calls produces exactly one re-parse.</summary>
    public void RequestRefresh()
    {
        _isDirty = true;
        _lastRequestElapsed = _lastKnownTotalElapsed;
    }

    private void OnWindowBeginUpdate(object sender, MGElement.ElementUpdateEventArgs e)
    {
        _lastKnownTotalElapsed = e.UA.BA.TotalElapsed;

        if (_isDirty && _lastKnownTotalElapsed - _lastRequestElapsed >= DebounceDelay)
        {
            _isDirty = false;
            Reparse();
        }

        UpdateAnchorAndVisibility();
    }

    private void Reparse()
    {
        PreviewVersion++;
        string text = _session.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            ReplaceRoot(null);
            Diagnostics = System.Array.Empty<XamlLoaderDiagnostic>();
            PreviewUpdated?.Invoke(this, System.EventArgs.Empty);
            return;
        }

        try
        {
            MGElement newRoot = UIToolingService.LoadPreview(_window, XamlDocumentSource.FromString(text, _session.SourceName),
                _session.DesignDataContext, XamlLoaderMode.Strict, sanitizeXamlString: false, replaceLinebreakLiterals: true);
            Diagnostics = System.Array.Empty<XamlLoaderDiagnostic>();
            ReplaceRoot(newRoot);
        }
        catch (XamlLoaderException ex)
        {
            // The previous PreviewRoot is left exactly as it was: the editor never goes blank on invalid text.
            Diagnostics = new[] { ex.Diagnostic };
        }
        catch (System.Exception ex)
        {
            // Not a XamlLoaderException: XamlLoaderMode.Strict only wraps failures raised while parsing the markup
            // itself, not ones raised later while instantiating the parsed elements (a bad runtime value, a missing
            // resource resolved at construction time, ...). Surfacing it as a diagnostic (rather than letting it
            // propagate) is what keeps a bad keystroke from ever crashing the editor; see the report's "decisions".
            Diagnostics = new[]
            {
                new XamlLoaderDiagnostic(XamlLoaderDiagnosticCode.ParseFailure, "Preview", _session.SourceName, null, ex.Message),
            };
        }

        PreviewUpdated?.Invoke(this, System.EventArgs.Empty);
    }

    private void ReplaceRoot(MGElement newRoot)
    {
        _presenter.Content?.RemoveDataBindings(true);
        _presenter.SetContent(newRoot);
        PreviewRoot = newRoot;
        _lastAnchoredPaneBounds = null;
        _declaredRootVisibility = newRoot?.Visibility ?? Visibility.Visible;
        _declaredRootOffset = newRoot is MGWindow declaredWindow
            ? new Microsoft.Xna.Framework.Point(declaredWindow.Left, declaredWindow.Top)
            : Microsoft.Xna.Framework.Point.Zero;

        if (newRoot != null)
        {
            newRoot.IsHitTestVisible = IsInteractive;
        }
    }

    private void UpdateAnchorAndVisibility()
    {
        if (PreviewRoot == null)
        {
            return;
        }

        bool isPaneAttached = _pane.Parent != null;

        if (!isPaneAttached)
        {
            // Hidden: the root is hidden and anchoring is skipped, but the caller keeps re-parsing on text changes.
            if (PreviewRoot.Visibility != Visibility.Hidden)
            {
                PreviewRoot.Visibility = Visibility.Hidden;
            }
            return;
        }

        if (PreviewRoot.Visibility != _declaredRootVisibility)
        {
            PreviewRoot.Visibility = _declaredRootVisibility;
        }

        if (PreviewRoot is not MGWindow windowRoot)
        {
            //  Only an MGWindow root carries a declared offset; anything else sits on the pane's corner, so the
            //  presenter must lose the margin a previous window root gave it.
            _presenter.Margin = new MonoGame.Extended.Thickness(0);
            return;
        }

        Rectangle paneBounds = _pane.LayoutBounds;
        if (_lastAnchoredPaneBounds == paneBounds)
        {
            return;
        }

        // MGWindow lays itself out at its own Left/Top/WindowWidth/WindowHeight (never at the bounds its parent
        // presenter allocates it), so re-anchoring means writing Left/Top ourselves. Left/Top setters do not
        // invalidate layout on their own: QueueLayoutRefresh does, and ValidateWindowSizeAndPosition() makes the
        // move (and its OnWindowPositionChanged) take effect immediately instead of waiting one extra tick.
        // The pane's corner is the preview's origin and the window keeps the offset its document declared, as it
        // would from the corner of a desktop; the presenter is pushed by that same offset so that its clip, which
        // is what keeps the preview inside the pane, covers the window where it now sits.
        _presenter.Margin = new MonoGame.Extended.Thickness(_declaredRootOffset.X, _declaredRootOffset.Y, 0, 0);
        windowRoot.Left = paneBounds.X + _declaredRootOffset.X;
        windowRoot.Top = paneBounds.Y + _declaredRootOffset.Y;
        windowRoot.QueueLayoutRefresh = true;
        windowRoot.ValidateWindowSizeAndPosition();
        _lastAnchoredPaneBounds = paneBounds;
    }
}
