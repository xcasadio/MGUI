using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Editor.Document;
using MGUI.Editor.Preview;
using MGUI.Editor.Properties;
using MGUI.Editor.Selection;
using MGUI.Editor.Text;

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

    /// <summary>Gives <see cref="TextPane"/> XAML colors and error underlines, and fills <see cref="DiagnosticsPane"/>
    /// with the current diagnostics list. Constructed last, since it reads <see cref="PreviewHost"/>'s diagnostics.</summary>
    public XamlEditorTextPane TextEditorPane { get; }

    /// <summary>The single selection shared by the document tree, a click in the non-interactive preview, and the text caret.
    /// Constructed after <see cref="PreviewHost"/> and <see cref="TextEditorPane"/>, once every pane it wires exists.</summary>
    public XamlEditorSelection Selection { get; }

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

        //  Left/Top, never stretched: a previewed root is shown at its own size. A stretched presenter would allocate
        //  the whole pane to the root, and an MGWindow root, whose alignment is forced to Stretch, would fill the pane
        //  instead of keeping the Width and Height its document declares. What overflows the pane is clipped.
        PreviewPresenter = new MGContentPresenter(window)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
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
        TextEditorPane = new XamlEditorTextPane(this);
        Selection = new XamlEditorSelection(this);
        BindTextPaneToSession();
        BindPropertyPaneToSelection();
    }

    /// <summary>The source currently assigned to <see cref="PropertyPane"/>'s <see cref="MGPropertyGrid.SelectedObject"/>, or null
    /// when nothing is selected. Tracked here (rather than read back from <see cref="MGPropertyGrid.SelectedObject"/>) so what it
    /// describes can be compared without an unnecessary cast on every selection change: see
    /// <see cref="CanRefreshPropertyPaneInPlace"/> for what "the same rows" means.</summary>
    private XamlNodePropertySource _propertySource;

    /// <summary>Follows X4's precedent (<see cref="BindTextPaneToSession"/>): drives <see cref="PropertyPane"/> from
    /// <see cref="Selection"/>, per <c>Docs/editor-architecture.md</c>'s <c>## Grille de proprietes</c>.<para/>
    /// <see cref="XamlEditorSelection.Changed"/> fires when the selected node changes (a different ordinal, or null), and also when
    /// only the representative element changed for the same node (raised from <see cref="Preview.XamlPreviewHost"/>'s update through
    /// <see cref="XamlEditorSelection"/>'s own re-resolution). Either way the target goes through
    /// <see cref="SetPropertyPaneTarget"/>, which decides between assigning a brand-new <see cref="XamlNodePropertySource"/> --
    /// rebuilding the grid, since the source is a new <see cref="System.ComponentModel.ICustomTypeDescriptor"/> instance -- and
    /// <see cref="XamlNodePropertySource.Update"/> plus <see cref="MGPropertyGrid.RefreshVisibleValues"/> on the instance already
    /// in place, never a reassignment of the same instance (the setter early-returns on it).<para/>
    /// <see cref="XamlEditorSelection.DocumentModelChanged"/> fires on every successful re-parse, before <see cref="Selection"/>
    /// re-resolves its own <see cref="XamlEditorSelection.SelectedNode"/> by ordinal: the still-old <see cref="XamlEditorSelection.SelectedNode"/>'s
    /// ordinal is looked up again in the just-updated <see cref="XamlEditorSelection.DocumentModel"/> and, when it still resolves,
    /// goes through <see cref="SetPropertyPaneTarget"/> too (a re-parse that drops the selected node instead reaches
    /// <see cref="XamlEditorSelection.Changed"/> with a null <see cref="XamlEditorSelection.SelectedNode"/>, handled below).<para/>
    /// <see cref="MGElement.OnParentChanged"/> on <see cref="PropertyPane"/> refreshes the pane once it has a non-null parent again:
    /// a re-activated docking pane raises this three times, but <see cref="MGPropertyGrid.RefreshVisibleValues"/> is idempotent.</summary>
    private void BindPropertyPaneToSelection()
    {
        Selection.Changed += (_, _) => ApplySelectionToPropertyPane();
        Selection.DocumentModelChanged += (_, _) => ApplyReparseToPropertyPane();
        PropertyPane.OnParentChanged += (_, e) =>
        {
            if (e.NewValue != null)
            {
                PropertyPane.RefreshVisibleValues();
            }
        };

        ApplySelectionToPropertyPane();
    }

    private void ApplySelectionToPropertyPane()
        => SetPropertyPaneTarget(Selection.SelectedNode, Selection.SelectedElement);

    private void ApplyReparseToPropertyPane()
    {
        if (_propertySource == null || Selection.SelectedNode?.Ordinal is not int ordinal)
        {
            return;
        }

        if (Selection.DocumentModel != null && Selection.DocumentModel.TryGetNodeByOrdinal(ordinal, out XamlDocumentNode node))
        {
            SetPropertyPaneTarget(node, Selection.SelectedElement);
        }
    }

    /// <summary>True when <paramref name="node"/> and <paramref name="representative"/> produce the same ROWS the current source
    /// already describes, so the grid can be refreshed in place instead of rebuilt. The node's ordinal is not enough on its own:
    /// the rows come from the node's DTO type, so a tag rewritten at the same ordinal (<c>Button</c> becoming <c>CheckBox</c>)
    /// changes them; and the "Resolved (runtime)" category exists only when there is a representative element, so a node selected
    /// from the caret before the debounced preview has ever rendered must grow that category once the preview arrives. Both cases
    /// need a real rebuild. Steady typing changes neither, so it still refreshes without rebuilding.</summary>
    private bool CanRefreshPropertyPaneInPlace(XamlDocumentNode node, MGElement representative)
        => _propertySource != null
           && _propertySource.Node.Ordinal == node.Ordinal
           && _propertySource.Node.DtoType == node.DtoType
           && (_propertySource.RepresentativeElement == null) == (representative == null);

    private void SetPropertyPaneTarget(XamlDocumentNode node, MGElement representative)
    {
        if (node == null)
        {
            _propertySource = null;
            PropertyPane.SelectedObject = null;
            return;
        }

        if (CanRefreshPropertyPaneInPlace(node, representative))
        {
            _propertySource.Update(node, representative);
            PropertyPane.RefreshVisibleValues();
            return;
        }

        _propertySource = new XamlNodePropertySource(node, representative);
        PropertyPane.SelectedObject = _propertySource;
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
