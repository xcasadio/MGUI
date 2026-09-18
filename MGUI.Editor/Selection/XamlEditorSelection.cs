using System;
using System.Collections.Generic;
using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Adorners;
using MGUI.Core.UI.XAML;
using MGUI.Editor.Document;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;

namespace MGUI.Editor.Selection;

/// <summary>Owns the single selection shared by the three inputs of the XAML editor's <c>## Selection</c> feature
/// (<c>Docs/editor-architecture.md</c>): the document tree, a click in the non-interactive preview, and the text
/// caret. Constructed by <see cref="XamlEditorView"/> once every other pane exists (after <see cref="XamlEditorView.PreviewHost"/>
/// and <see cref="XamlEditorView.TextEditorPane"/>), exposed as <see cref="XamlEditorView.Selection"/>; the caller does
/// nothing more than construct it, every wire below is set up by the constructor.</summary>
public sealed class XamlEditorSelection
{
    private readonly XamlEditorView _view;
    private readonly MGAdornerLayer _adornerLayer;
    private readonly MGBoundsAdorner _adorner;

    /// <summary>One reentrancy guard for every direction (tree -&gt; selection, selection -&gt; tree, selection -&gt; caret,
    /// caret -&gt; selection): <see cref="MGUI.Core.UI.MGTreeView.SelectItem"/> raises <see cref="MGUI.Core.UI.MGTreeView.SelectionChanged"/>
    /// even for a code-driven selection, so every write this class makes to <see cref="XamlEditorView.TreePane"/> or
    /// <see cref="XamlEditorView.TextPane"/> must happen while this flag is set, and every handler that could be re-entered by
    /// that write must check it first.</summary>
    private bool _isSynchronizing;

    /// <summary>The exact text <see cref="DocumentModel"/> was parsed from, kept so the caret-polling loop can tell an edit that
    /// produced no new model (invalid XAML) from one that is simply still being typed on top of the last valid model.</summary>
    private string _lastParsedText;

    /// <summary>The last caret index this class saw, including one it wrote itself: writing it here right after a node-driven
    /// caret move keeps that write from being reported back as a caret move by the polling loop below.</summary>
    private int _lastSeenCaretIndex = -1;

    private readonly Dictionary<int, MGTreeViewItem> _treeItemsByOrdinal = new();

    /// <summary>The last model that parsed. Re-parsed on <see cref="XamlEditorSession.TextChanged"/>; on a parse failure the
    /// previous model stays current, exactly like <see cref="Preview.XamlPreviewHost"/>.</summary>
    public XamlDocumentModel DocumentModel { get; private set; }

    /// <summary>Raised after each successful re-parse of <see cref="DocumentModel"/> (X5 uses this).</summary>
    public event EventHandler DocumentModelChanged;

    /// <summary>The selected document node, or null.</summary>
    public XamlDocumentNode SelectedNode { get; private set; }

    /// <summary>The element in the preview that represents <see cref="SelectedNode"/>, or null. Only a representative: a node
    /// generated N times by a template (a list's item template) can have many matching elements, and this is just one of them --
    /// see <see cref="ResolveRepresentativeElement"/>.</summary>
    public MGElement SelectedElement { get; private set; }

    /// <summary>Raised when <see cref="SelectedNode"/> or <see cref="SelectedElement"/> actually changes.</summary>
    public event EventHandler Changed;

    /// <summary>Suspends only the caret-polling loop (the caret -&gt; node direction); every other direction keeps working.
    /// False by default. X6 sets this during a grid edit, so that editing a property does not fight a caret sitting somewhere
    /// unrelated in the text.</summary>
    public bool IsSuspended { get; set; }

    public XamlEditorSelection(XamlEditorView view)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));

        _adornerLayer = new MGAdornerLayer(view.Window);
        _adorner = new MGBoundsAdorner(view.Window)
        {
            BorderThickness = 2,
            BorderColor = Color.DodgerBlue,
            FillColor = new Color(30, 144, 255, 40),
        };
        _adornerLayer.TryAddAdorner(_adorner);
        //  Same z-index as MGUI.Samples/Features/AdornerLite.xaml.cs: above the preview's own content, and the layer's own
        //  IsHitTestVisible == false (set by its constructor) keeps it from ever eating the click this class listens for below.
        view.PreviewPane.TryAddChild(_adornerLayer, default, 500);

        view.Session.TextChanged += (_, _) => ReparseDocument();
        ReparseDocument();

        view.TreePane.SelectionChanged += OnTreeSelectionChanged;
        view.PreviewPane.MouseHandler.LMBClickedInside += OnPreviewClicked;
        view.PreviewHost.PreviewUpdated += OnPreviewUpdated;
        view.Window.OnBeginUpdate += OnWindowBeginUpdate;
    }

    /// <summary>Clears the selection when <paramref name="ordinal"/> is null or does not resolve in <see cref="DocumentModel"/>,
    /// otherwise selects that node.</summary>
    public void SelectByOrdinal(int? ordinal)
    {
        XamlDocumentNode node = null;
        if (ordinal.HasValue && DocumentModel != null && DocumentModel.TryGetNodeByOrdinal(ordinal.Value, out XamlDocumentNode found))
        {
            node = found;
        }

        ApplySelection(node, null, syncCaret: true);
    }

    /// <summary>Walks <paramref name="element"/>'s <see cref="MGElement.Parent"/> chain (which also reaches a component's owner,
    /// since <see cref="MGElement.AddComponent"/> sets it) up to and including the current preview root, and selects the node of
    /// the first element that carries a <see cref="XamlSourcePosition"/> whose <see cref="XamlSourcePosition.SourceName"/> is this
    /// session's. This is what makes a click on a template part, on an inner control's own button, or on the label content of
    /// <c>&lt;Button Content="OK"/&gt;</c> select the declared node that owns it. When no element before the preview root carries
    /// such a position, the click selects nothing: the existing selection, if any, is left untouched.</summary>
    public void SelectByElement(MGElement element)
    {
        if (element == null)
        {
            return;
        }

        MGElement root = _view.PreviewHost.PreviewRoot;
        MGElement current = element;
        while (current != null)
        {
            if (UIToolingService.TryGetXamlSourcePosition(current, out XamlSourcePosition position)
                && string.Equals(position.SourceName, _view.Session.SourceName, StringComparison.Ordinal))
            {
                if (DocumentModel != null && DocumentModel.TryGetNodeByOrdinal(position.Ordinal, out XamlDocumentNode node))
                {
                    ApplySelection(node, current, syncCaret: true);
                }

                return;
            }

            if (current == root)
            {
                break;
            }

            current = current.Parent;
        }
    }

    /// <summary>Selects the deepest node whose <see cref="XamlDocumentNode.FullRange"/> contains <paramref name="caretIndex"/>,
    /// walking up to the first ancestor that has an <see cref="XamlDocumentNode.Ordinal"/> when the deepest one is a property
    /// element. Clears the selection when no such node exists (caret outside the document, or every ancestor is a property
    /// element). Does not itself move the caret back (see <see cref="ApplySelection"/>'s <c>syncCaret</c>): a selection driven
    /// by the caret must not fight whatever positioned that caret, whether the polling loop below or another caller (X2's
    /// diagnostics list already moves the caret to a diagnostic's own position, which is not necessarily a node's tag start).</summary>
    public void SelectByCaret(int caretIndex)
    {
        XamlDocumentNode node = DocumentModel?.FindDeepestNodeAt(caretIndex);
        while (node != null && !node.Ordinal.HasValue)
        {
            node = node.Parent;
        }

        ApplySelection(node, null, syncCaret: false);
    }

    /// <summary>The single write path for every direction: sets <see cref="SelectedNode"/>/<see cref="SelectedElement"/>, then,
    /// only if something actually changed, syncs the tree and (when <paramref name="syncCaret"/>) the caret, and raises
    /// <see cref="Changed"/>. Guarded by <see cref="_isSynchronizing"/> so a write this method makes to the tree or the caret
    /// can never re-enter here.</summary>
    private void ApplySelection(XamlDocumentNode node, MGElement explicitRepresentative, bool syncCaret)
    {
        if (_isSynchronizing)
        {
            return;
        }

        _isSynchronizing = true;
        try
        {
            MGElement representative = ResolveRepresentativeElement(node, explicitRepresentative);
            bool changed = !ReferenceEquals(SelectedNode, node) || !ReferenceEquals(SelectedElement, representative);

            SelectedNode = node;
            SelectedElement = representative;

            if (changed)
            {
                SyncTreeSelection();
                if (syncCaret)
                {
                    SyncCaretFromSelection();
                }
            }

            SyncAdorner();

            if (changed)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    /// <summary>The representative for <paramref name="node"/>: the element a click carried, when there is one, otherwise the
    /// first element, in the preorder of the current preview root's visual tree, whose source position matches <paramref name="node"/>'s
    /// ordinal. Null is not an error (a node with no element currently in the preview stays selectable from the tree and the caret).</summary>
    private MGElement ResolveRepresentativeElement(XamlDocumentNode node, MGElement explicitRepresentative)
    {
        if (node == null)
        {
            return null;
        }

        return explicitRepresentative ?? FindFirstElementForNode(node);
    }

    private MGElement FindFirstElementForNode(XamlDocumentNode node)
    {
        MGElement root = _view.PreviewHost.PreviewRoot;
        if (root == null || node?.Ordinal is not int ordinal)
        {
            return null;
        }

        foreach (MGElement candidate in root.TraverseVisualTree(includeComponents: true))
        {
            if (UIToolingService.TryGetXamlSourcePosition(candidate, out XamlSourcePosition position)
                && position.Ordinal == ordinal
                && string.Equals(position.SourceName, _view.Session.SourceName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private void SyncAdorner() => _adorner.TargetElement = SelectedElement;

    /// <summary>Selecting a node from the tree or from a preview click places the caret at the start of its start tag (see
    /// <see cref="ApplySelection"/>'s <c>syncCaret</c> for the one direction that skips this). Always records the resulting
    /// caret index as the last one seen, so the polling loop below never reports this write back as a user move.</summary>
    private void SyncCaretFromSelection()
    {
        if (SelectedNode != null)
        {
            _view.TextPane.CaretIndex = SelectedNode.StartTagRange.StartIndex;
        }

        _lastSeenCaretIndex = _view.TextPane.CaretIndex;
    }

    private void SyncTreeSelection()
    {
        if (SelectedNode?.Ordinal is int ordinal && _treeItemsByOrdinal.TryGetValue(ordinal, out MGTreeViewItem item))
        {
            _view.TreePane.SelectItem(item);
        }
        else
        {
            _view.TreePane.SelectItem(null);
        }
    }

    private void OnTreeSelectionChanged(object sender, MGTreeViewItem item)
    {
        if (_isSynchronizing)
        {
            return;
        }

        SelectByOrdinal(item?.Tag is int ordinal ? ordinal : null);
    }

    private void OnPreviewClicked(object sender, BaseMouseClickedEventArgs e)
    {
        if (_view.PreviewHost.IsInteractive || _view.PreviewHost.PreviewRoot == null)
        {
            return;
        }

        MGElement hit = UIToolingService.HitTest(_view.PreviewHost.PreviewRoot, e.Position);
        SelectByElement(hit);
    }

    /// <summary>Runs once per completed preview re-parse: the preview root was just replaced (or cleared), so the previous
    /// <see cref="SelectedElement"/>, if any, is stale and is recomputed for the still-current <see cref="SelectedNode"/> --
    /// this does not go through <see cref="ApplySelection"/>, since the logical selection (the node) has not changed, only its
    /// representative element and, through it, the adorner.</summary>
    private void OnPreviewUpdated(object sender, EventArgs e)
    {
        if (SelectedNode == null)
        {
            SyncAdorner();
            return;
        }

        MGElement resolved = FindFirstElementForNode(SelectedNode);
        if (!ReferenceEquals(resolved, SelectedElement))
        {
            SelectedElement = resolved;
            SyncAdorner();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Re-parses <see cref="DocumentModel"/> on every <see cref="XamlEditorSession.TextChanged"/>. On success, re-resolves
    /// <see cref="SelectedNode"/> by its ordinal (surviving the re-parse without touching the caret or firing <see cref="Changed"/>
    /// unless the ordinal disappeared, in which case the selection is cleared and that is reported) and rebuilds the tree. On
    /// failure (invalid XAML) everything -- the model and the selection -- is left exactly as it was, so a selection survives
    /// invalid XAML the same way <see cref="Preview.XamlPreviewHost"/> keeps its previous root.</summary>
    private void ReparseDocument()
    {
        if (!XamlDocumentModel.TryParse(_view.Session.Text, out XamlDocumentModel model, out _))
        {
            return;
        }

        DocumentModel = model;
        _lastParsedText = _view.Session.Text;
        DocumentModelChanged?.Invoke(this, EventArgs.Empty);

        XamlDocumentNode resolvedNode = null;
        if (SelectedNode?.Ordinal is int ordinal && model.TryGetNodeByOrdinal(ordinal, out XamlDocumentNode found))
        {
            resolvedNode = found;
        }

        bool selectionCleared = SelectedNode != null && resolvedNode == null;
        SelectedNode = resolvedNode;
        if (selectionCleared)
        {
            SelectedElement = null;
        }

        _isSynchronizing = true;
        try
        {
            RebuildTree();
        }
        finally
        {
            _isSynchronizing = false;
        }

        if (selectionCleared)
        {
            SyncAdorner();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Rebuilds <see cref="XamlEditorView.TreePane"/> from <see cref="DocumentModel"/>: one item per node that has an
    /// <see cref="XamlDocumentNode.Ordinal"/> (a property element is walked through but never shown, so its children attach to
    /// the nearest ancestor item, or to the tree's root when there is none), <see cref="MGTreeViewItem.Header"/> = the local name,
    /// plus the <c>Name</c> attribute when there is one, and the ordinal boxed into <see cref="MGElement.Tag"/> -- the item-to-node
    /// mapping this class uses everywhere else. Expansion state is preserved by ordinal; a brand-new item (an ordinal this tree did
    /// not show before the rebuild) defaults to expanded, so a first parse shows the tree open.</summary>
    private void RebuildTree()
    {
        HashSet<int> expandedOrdinals = new();
        HashSet<int> existingOrdinals = new();
        CollectExpansionState(_view.TreePane.Items, expandedOrdinals, existingOrdinals);

        _view.TreePane.ClearItems();
        _treeItemsByOrdinal.Clear();

        if (DocumentModel?.Root != null)
        {
            AddNodeItems(DocumentModel.Root, null, expandedOrdinals, existingOrdinals);
        }

        SyncTreeSelection();
    }

    private static void CollectExpansionState(IReadOnlyList<MGTreeViewItem> items, HashSet<int> expandedOrdinals, HashSet<int> existingOrdinals)
    {
        foreach (MGTreeViewItem item in items)
        {
            if (item.Tag is int ordinal)
            {
                existingOrdinals.Add(ordinal);
                if (item.IsExpanded)
                {
                    expandedOrdinals.Add(ordinal);
                }
            }

            CollectExpansionState(item.Items, expandedOrdinals, existingOrdinals);
        }
    }

    private void AddNodeItems(XamlDocumentNode node, MGTreeViewItem parentItem, HashSet<int> expandedOrdinals, HashSet<int> existingOrdinals)
    {
        MGTreeViewItem currentParent = parentItem;

        if (node.Ordinal is int ordinal)
        {
            string name = FindNameAttribute(node);
            string header = string.IsNullOrEmpty(name) ? node.LocalName : $"{node.LocalName} \"{name}\"";

            MGTreeViewItem item = new(_view.Window)
            {
                Header = header,
                Tag = ordinal,
                IsExpanded = !existingOrdinals.Contains(ordinal) || expandedOrdinals.Contains(ordinal),
            };

            if (parentItem != null)
            {
                parentItem.AddItem(item);
            }
            else
            {
                _view.TreePane.AddItem(item);
            }

            _treeItemsByOrdinal[ordinal] = item;
            currentParent = item;
        }

        foreach (XamlDocumentNode child in node.Children)
        {
            AddNodeItems(child, currentParent, expandedOrdinals, existingOrdinals);
        }
    }

    private static string FindNameAttribute(XamlDocumentNode node)
    {
        foreach (XamlDocumentAttribute attribute in node.Attributes)
        {
            if (string.Equals(attribute.Name, "Name", StringComparison.Ordinal))
            {
                return attribute.Value;
            }
        }

        return null;
    }

    /// <summary>The caret -&gt; node direction: <see cref="MGUI.Core"/> raises no caret-move event, so this polls
    /// <see cref="XamlEditorView.TextPane"/>'s <see cref="MGRichTextBox.CaretIndex"/> on every update tick of <see cref="XamlEditorView.Window"/>
    /// and calls <see cref="SelectByCaret"/> only when it changed since the last tick. Guards: <see cref="IsSuspended"/>; the caret
    /// must have a position; the session's text must equal <see cref="_lastParsedText"/> (a keystroke that has not produced a new,
    /// valid document model yet must not select against a stale one).</summary>
    private void OnWindowBeginUpdate(object sender, MGElement.ElementUpdateEventArgs e)
    {
        if (IsSuspended)
        {
            return;
        }

        if (!_view.TextPane.Caret.HasPosition)
        {
            return;
        }

        if (!string.Equals(_view.Session.Text, _lastParsedText, StringComparison.Ordinal))
        {
            return;
        }

        int caretIndex = _view.TextPane.CaretIndex;
        if (caretIndex == _lastSeenCaretIndex)
        {
            return;
        }

        _lastSeenCaretIndex = caretIndex;
        SelectByCaret(caretIndex);
    }
}
