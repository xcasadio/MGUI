using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Editor.Preview;

namespace MGUI.Editor;

/// <summary>Composes the empty shell of the XAML editor: a text pane, a preview pane, a document tree pane, a property
/// pane and a diagnostics placeholder, hosted by MGUI's own docking manager through <see cref="CreateDockHost"/>.<para/>
/// This is a plain composing class, not an <see cref="MGElement"/>: it builds its elements against the given
/// <see cref="MGWindow"/> but does not set the window's content and does not add the window to a desktop; the host does.<para/>
/// <see cref="TextPane"/> holds the session's text, and <see cref="PreviewHost"/> renders that text into
/// <see cref="PreviewPresenter"/>, hot; no other pane has any behaviour yet.</summary>
public class XamlEditorView
{
    /// <summary>The dockable ID of <see cref="TextPane"/>.</summary>
    public const string TextDockableId = "xaml-editor.text";
    /// <summary>The dockable ID of <see cref="PreviewPane"/>.</summary>
    public const string PreviewDockableId = "xaml-editor.preview";
    /// <summary>The dockable ID of <see cref="TreePane"/>.</summary>
    public const string TreeDockableId = "xaml-editor.tree";
    /// <summary>The dockable ID of <see cref="PropertyPane"/>.</summary>
    public const string PropertiesDockableId = "xaml-editor.properties";
    /// <summary>The dockable ID of <see cref="DiagnosticsPane"/>.</summary>
    public const string DiagnosticsDockableId = "xaml-editor.diagnostics";

    /// <summary>The window every element of this view was constructed against.</summary>
    public MGWindow Window { get; }
    /// <summary>The session this view was built for.</summary>
    public XamlEditorSession Session { get; }

    /// <summary>The text editor for the raw XAML markup.</summary>
    public MGRichTextBox TextPane { get; }
    /// <summary>An overlay panel that will host the rendered preview, and later the adorner layer.</summary>
    public MGOverlayPanel PreviewPane { get; }
    /// <summary>The content presenter, inside <see cref="PreviewPane"/>, that will host the previewed content.</summary>
    public MGContentPresenter PreviewPresenter { get; }
    /// <summary>The visual tree of the previewed document.</summary>
    public MGTreeView TreePane { get; }
    /// <summary>The property grid for the current selection.</summary>
    public MGPropertyGrid PropertyPane { get; }
    /// <summary>An empty placeholder for the diagnostics list; filled by a later slice.</summary>
    public MGContentPresenter DiagnosticsPane { get; }

    /// <summary>Renders the session's text into <see cref="PreviewPresenter"/>, hot, on the update tick of <see cref="Window"/>.
    /// Constructed last, once every pane above it exists.</summary>
    public XamlPreviewHost PreviewHost { get; }

    /// <summary>The five dockables of this view, in the order text, preview, tree, properties, diagnostics.
    /// Each <see cref="DockableDefinition.ContentFactory"/> returns the matching pane instance above,
    /// always the same instance.</summary>
    public IReadOnlyList<DockableDefinition> Dockables { get; }

    /// <summary>Set by <see cref="CreateDockHost"/> so a second call on the same view is refused: the panes and
    /// their content factories are shared, single instances that only one dock host may claim.</summary>
    private bool _dockHostCreated;

    public XamlEditorView(MGWindow window, XamlEditorSession session)
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
        Session = session ?? throw new ArgumentNullException(nameof(session));

        TextPane = new MGRichTextBox(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        PreviewPresenter = new MGContentPresenter(window);
        PreviewPane = new MGOverlayPanel(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        PreviewPane.TryAddChild(PreviewPresenter);

        TreePane = new MGTreeView(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        PropertyPane = new MGPropertyGrid(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        DiagnosticsPane = new MGContentPresenter(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        Dockables = new List<DockableDefinition>
        {
            new(TextDockableId, "XAML") { ContentFactory = () => TextPane, CanClose = false, CanFloat = true, CanAutoHide = true },
            new(PreviewDockableId, "Preview") { ContentFactory = () => PreviewPane, CanClose = false, CanFloat = false, CanAutoHide = true },
            new(TreeDockableId, "Document") { ContentFactory = () => TreePane, CanClose = false, CanFloat = true, CanAutoHide = true },
            new(PropertiesDockableId, "Properties") { ContentFactory = () => PropertyPane, CanClose = false, CanFloat = true, CanAutoHide = true },
            new(DiagnosticsDockableId, "Diagnostics") { ContentFactory = () => DiagnosticsPane, CanClose = false, CanFloat = true, CanAutoHide = true },
        };

        PreviewHost = new XamlPreviewHost(this);
        BindTextPaneToSession();
    }

    /// <summary>Set while one side of <see cref="BindTextPaneToSession"/> writes the other, so the echo it raises is ignored.</summary>
    private bool _isSynchronizingText;

    /// <summary>Makes the text pane and the session hold the same text: typing in <see cref="TextPane"/> writes
    /// <see cref="XamlEditorSession.Text"/> (which is what drives the preview, the document model and the diagnostics),
    /// and writing the session's text from code -- opening a file, an edit made from the property grid -- puts it back
    /// in the pane. The session's text is always the pane's text, which <see cref="MGRichTextBox"/> normalises to LF.</summary>
    private void BindTextPaneToSession()
    {
        TextPane.TextChanged += (_, _) =>
        {
            if (_isSynchronizingText)
            {
                return;
            }

            _isSynchronizingText = true;
            try
            {
                Session.Text = TextPane.Text;
            }
            finally
            {
                _isSynchronizingText = false;
            }
        };

        Session.TextChanged += (_, _) =>
        {
            if (_isSynchronizingText || string.Equals(TextPane.Text, Session.Text, StringComparison.Ordinal))
            {
                return;
            }

            _isSynchronizingText = true;
            try
            {
                TextPane.SetText(Session.Text);
            }
            finally
            {
                _isSynchronizingText = false;
            }
        };

        //  A session built with text already in it (a document opened before the view exists) must reach the pane and
        //  the preview without waiting for a first keystroke.
        if (!string.IsNullOrEmpty(Session.Text))
        {
            TextPane.SetText(Session.Text);
            PreviewHost.RequestRefresh();
        }
    }

    /// <summary>Builds the dock host for this view: a registry seeing the five <see cref="Dockables"/> and the
    /// default layout (XAML | Preview over Diagnostics on the left, Document over Properties on the right).<para/>
    /// A view can be hosted by one dock host only: calling this a second time throws.</summary>
    public MGDockHost CreateDockHost()
    {
        if (_dockHostCreated)
        {
            throw new InvalidOperationException($"{nameof(XamlEditorView)} already created a dock host: a view can be hosted by one dock host only.");
        }
        _dockHostCreated = true;

        MGDockHost host = new(Window);

        DockableRegistry registry = new();
        foreach (DockableDefinition dockable in Dockables)
        {
            registry.Register(dockable);
        }
        host.DockableRegistry = registry;

        DockPanelNode textPanel = Dockables[0].CreatePanelNode();
        DockPanelNode previewPanel = Dockables[1].CreatePanelNode();
        DockPanelNode treePanel = Dockables[2].CreatePanelNode();
        DockPanelNode propertyPanel = Dockables[3].CreatePanelNode();
        DockPanelNode diagnosticsPanel = Dockables[4].CreatePanelNode();

        DockTabGroupNode textGroup = new();
        textGroup.AddPanel(textPanel, -1);
        DockTabGroupNode previewGroup = new();
        previewGroup.AddPanel(previewPanel, -1);
        DockTabGroupNode treeGroup = new();
        treeGroup.AddPanel(treePanel, -1);
        DockTabGroupNode propertyGroup = new();
        propertyGroup.AddPanel(propertyPanel, -1);
        DockTabGroupNode diagnosticsGroup = new();
        diagnosticsGroup.AddPanel(diagnosticsPanel, -1);

        DockSplitNode topSplit = new()
        {
            Orientation = Orientation.Horizontal,
            FirstChild = textGroup,
            SecondChild = previewGroup,
            SplitRatio = 0.45f,
        };

        DockSplitNode leftBlock = new()
        {
            Orientation = Orientation.Vertical,
            FirstChild = topSplit,
            SecondChild = diagnosticsGroup,
            SplitRatio = 0.78f,
        };

        DockSplitNode rightColumn = new()
        {
            Orientation = Orientation.Vertical,
            FirstChild = treeGroup,
            SecondChild = propertyGroup,
            SplitRatio = 0.45f,
        };

        DockSplitNode root = new()
        {
            Orientation = Orientation.Horizontal,
            FirstChild = leftBlock,
            SecondChild = rightColumn,
            SplitRatio = 0.72f,
        };

        host.LayoutModel = new DockLayoutModel(root);

        host.RegisterPanel(textPanel);
        host.RegisterPanel(previewPanel);
        host.RegisterPanel(treePanel);
        host.RegisterPanel(propertyPanel);
        host.RegisterPanel(diagnosticsPanel);

        return host;
    }
}
