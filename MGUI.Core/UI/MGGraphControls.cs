using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Graph;
using MGUI.Core.UI.Responsive;
using MGUI.Core.UI.Styling;
using MGUI.Core.UI.Text;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.Core.UI;

public class MGGraphView : MGSingleContentHost
{
    public const string OuterBorderPartName = "PART_OuterBorder";
    public const string ViewportHostPartName = "PART_ViewportHost";
    public const string NodesCanvasPartName = "PART_NodesCanvas";
    public const string OverlayPanelPartName = "PART_OverlayPanel";
    public const string SurfacePartName = NodesCanvasPartName;
    private const string GraphClipboardPrefix = "MGUI.GraphClipboard.v1";

    protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
    {
        yield return new(OuterBorderPartName, typeof(MGBorder));
        yield return new(ViewportHostPartName, typeof(MGOverlayPanel));
        yield return new(NodesCanvasPartName, typeof(MGCanvas));
        yield return new(OverlayPanelPartName, typeof(MGOverlayPanel));
    }

    private GraphDocument _Document;
    private GraphDocumentViewSynchronizer _Synchronizer;
    private IFillBrush _GridLineBrush = new MGSolidFillBrush(Color.White * 0.08f);
    private IFillBrush _MajorGridLineBrush;
    private IFillBrush _EdgeBrush = new MGSolidFillBrush(new Color(128, 180, 255));
    private float _EdgeThickness = 2.0f;
    private int _MajorGridLineFrequency = 4;
    private bool _IsPanningViewport;
    private Point _PanStartScreenPosition;
    private Vector2 _PanStartValue;
    private bool _IsDraggingNodes;
    private bool _IsDraggingComments;
    private bool _IsResizingComment;
    private Guid _PressedNodeId;
    private Guid _PressedCommentId;
    private bool _PressedCommentResizeHandle;
    private Vector2 _PointerPressViewportPoint;
    private Vector2 _CurrentSelectionViewportPoint;
    private readonly Dictionary<Guid, Vector2> _NodeDragStartPositions = new();
    private readonly Dictionary<Guid, Rectangle> _CommentDragStartBounds = new();
    private Rectangle _CommentResizeStartBounds;
    private readonly HashSet<MGGraphNode> _RegisteredNodeInputs = new();
    private readonly HashSet<MGGraphPort> _RegisteredConnectionPorts = new();
    private readonly HashSet<MGGraphCommentBox> _RegisteredCommentInputs = new();
    private readonly GraphHitTestService _HitTestService = new();
    private readonly GraphSerializer _ClipboardSerializer = new();
    private CommentEditorState _ActiveCommentEditor;

    private sealed class CommentEditorState
    {
        public Guid CommentId { get; init; }
        public MGGraphCommentBox CommentBox { get; init; }
        public EventHandler<BaseKeyPressedEventArgs> BodyKeyPressedHandler { get; init; }
        public EventHandler<EventArgs<MGElement>> FocusChangedHandler { get; init; }
    }

    public MGBorder OuterBorder { get; private set; }
    public MGOverlayPanel ViewportHost { get; private set; }
    public MGCanvas NodesCanvas { get; private set; }
    public MGOverlayPanel OverlayPanel { get; private set; }
    public MGCanvas Surface => NodesCanvas;
    public GraphViewportTransform ViewportTransform { get; } = new();
    public GraphViewportTransform Viewport => ViewportTransform;
    public GraphEdgeGeometryCache EdgeGeometryCache { get; } = new();
    public GraphCullingService CullingService { get; } = new();
    public GraphCommandStack Commands { get; } = new();
    public GraphSelectionManager Selection { get; }
    public GraphConnectionController ConnectionController { get; }
    public GraphNodePalette NodePalette { get; set; } = GraphNodePalette.CreateDefault();
    public GraphCullingDiagnostics CullingDiagnostics { get; private set; }
    public HashSet<Guid> SelectedNodeIds { get; } = new();
    public HashSet<Guid> SelectedEdgeIds { get; } = new();
    public HashSet<Guid> SelectedCommentIds { get; } = new();
    internal Func<string> ClipboardTextReader { get; set; }
    internal Action<string> ClipboardTextWriter { get; set; }
    public bool EnableViewportCulling { get; set; } = true;
    public float CullingPadding { get; set; } = 96.0f;
    public bool ShowGrid { get; set; } = true;
    public bool AllowZoom { get; set; } = true;
    public bool AllowPan { get; set; } = true;
    public bool SnapToGrid { get; set; }
    public float FramePadding { get; set; } = 32.0f;
    public bool IsSelectionRectangleActive { get; private set; }

    public RectangleF SelectionRectangleViewportBounds => CreateRectangle(_PointerPressViewportPoint, _CurrentSelectionViewportPoint);
    internal bool IsCommentEditorOpen => _ActiveCommentEditor != null;
    internal Guid EditingCommentId => _ActiveCommentEditor?.CommentId ?? Guid.Empty;
    internal MGTextBox CommentEditorBodyTextBox => _ActiveCommentEditor?.CommentBox?.BodyTextBox;

    internal static string GetCommentDescriptionText(string title, string text)
        => !string.IsNullOrWhiteSpace(text) ? text : title ?? string.Empty;

    public IFillBrush GridLineBrush
    {
        get => _GridLineBrush;
        set
        {
            if (_GridLineBrush != value)
            {
                _GridLineBrush = value;
                NPC(nameof(GridLineBrush));
            }
        }
    }

    public IFillBrush MajorGridLineBrush
    {
        get => _MajorGridLineBrush;
        set
        {
            if (_MajorGridLineBrush != value)
            {
                _MajorGridLineBrush = value;
                NPC(nameof(MajorGridLineBrush));
            }
        }
    }

    public IFillBrush EdgeBrush
    {
        get => _EdgeBrush;
        set
        {
            if (_EdgeBrush != value)
            {
                _EdgeBrush = value;
                NPC(nameof(EdgeBrush));
            }
        }
    }

    public float EdgeThickness
    {
        get => _EdgeThickness;
        set
        {
            float next = Math.Max(0.1f, value);
            if (!_EdgeThickness.Equals(next))
            {
                _EdgeThickness = next;
                NPC(nameof(EdgeThickness));
            }
        }
    }

    public int MajorGridLineFrequency
    {
        get => _MajorGridLineFrequency;
        set
        {
            int next = Math.Max(1, value);
            if (_MajorGridLineFrequency != next)
            {
                _MajorGridLineFrequency = next;
                NPC(nameof(MajorGridLineFrequency));
            }
        }
    }

    public GraphDocument Document
    {
        get => _Document;
        set
        {
            GraphDocument next = value ?? new GraphDocument();
            if (!ReferenceEquals(_Document, next))
            {
                if (_Document != null)
                {
                    _Document.GraphChanged -= OnDocumentGraphChanged;
                }

                _Document = next;
                _Document.GraphChanged += OnDocumentGraphChanged;
                SynchronizeDocument();
                NPC(nameof(Document));
            }
        }
    }

    public MGGraphView(MGWindow window)
        : this(window, new GraphDocument()) { }

    public MGGraphView(MGWindow window, GraphDocument document)
        : base(window, MGElementType.GraphView)
    {
        using (BeginInitializing())
        {
            StringClipboard clipboard = new();
            _Document = document ?? new GraphDocument();
            _Document.GraphChanged += OnDocumentGraphChanged;
            Selection = new GraphSelectionManager(SelectedNodeIds, SelectedEdgeIds);
            ConnectionController = new GraphConnectionController(this);
            ClipboardTextReader = () => clipboard.Text;
            ClipboardTextWriter = value => clipboard.Text = value ?? string.Empty;
            IsFocusable = true;
            DefaultControlTemplateName = MGControlTemplateCatalog.GraphViewTemplateName;
            ContextMenuRequested += OnGraphContextMenuRequested;
            RegisterViewportInputHandlers();
        }
    }

    protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure structure)
    {
        OuterBorder = structure.Parts[OuterBorderPartName] as MGBorder;
        ViewportHost = structure.Parts[ViewportHostPartName] as MGOverlayPanel;
        NodesCanvas = structure.Parts[NodesCanvasPartName] as MGCanvas;
        OverlayPanel = structure.Parts[OverlayPanelPartName] as MGOverlayPanel;

        ViewportHost.ClipToBounds = true;

        using (ViewportHost.AllowChangingContentTemporarily())
        {
            ViewportHost.TryAddChild(NodesCanvas, default, 0);
            ViewportHost.TryAddChild(OverlayPanel, default, 100);
        }

        ViewportHost.CanChangeContent = false;
        NodesCanvas.CanChangeContent = false;
        OverlayPanel.CanChangeContent = false;

        using (OuterBorder.AllowChangingContentTemporarily())
        {
            OuterBorder.SetContent(ViewportHost);
        }

        using (AllowChangingContentTemporarily())
        {
            SetContent(OuterBorder);
        }

        _Synchronizer = new GraphDocumentViewSynchronizer(this);
        SynchronizeDocument();
    }

    public void SynchronizeDocument()
    {
        EdgeGeometryCache.RetainEdges(Document?.Edges);
        _Synchronizer?.Synchronize();
    }

    public void RefreshThemeVisuals()
    {
        MGTheme theme = GetTheme();
        NotifyThemeChanged(theme, theme);
    }

    protected internal override void OnThemeChanged(MGTheme PreviousTheme, MGTheme CurrentTheme)
    {
        base.OnThemeChanged(PreviousTheme, CurrentTheme);

        // Graph visuals are materialized on demand from the document.
        // Refresh them before descendant theme propagation continues so existing
        // and newly-created node/port controls resolve the current theme consistently.
        SynchronizeDocument();
        UpdateSelectionVisuals();
    }

    internal Rectangle GetCullingLayoutBounds()
    {
        Rectangle bounds = NodesCanvas?.LayoutBounds ?? ViewportHost?.LayoutBounds ?? LayoutBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            bounds = NodesCanvas?.ActualLayoutBounds ?? ViewportHost?.ActualLayoutBounds ?? ActualLayoutBounds;
        }

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            bounds = SelfOrParentWindow?.LayoutBounds ?? Rectangle.Empty;
        }

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            MGWindow window = SelfOrParentWindow;
            if (window != null)
            {
                bounds = new Rectangle(0, 0, window.WindowWidth, window.WindowHeight);
            }
        }

        int width = Math.Max(1, bounds.Width);
        int height = Math.Max(1, bounds.Height);
        return new Rectangle(0, 0, width, height);
    }

    internal RectangleF GetCullingWorldViewport(Rectangle? layoutBounds = null)
        => CullingService.CreateWorldViewport(ViewportTransform, layoutBounds ?? GetCullingLayoutBounds(), Math.Max(0.0f, CullingPadding));

    internal void SetNodeCommentCullingDiagnostics(GraphCullingDiagnostics diagnostics)
        => CullingDiagnostics = diagnostics;

    internal void SetEdgeCullingDiagnostics(int edgesVisible, int edgesCulled)
    {
        GraphCullingDiagnostics diagnostics = CullingDiagnostics;
        diagnostics.EdgesVisible = edgesVisible;
        diagnostics.EdgesCulled = edgesCulled;
        diagnostics.EdgeCacheHits = EdgeGeometryCache.CacheHits;
        diagnostics.EdgeCacheMisses = EdgeGeometryCache.CacheMisses;
        CullingDiagnostics = diagnostics;
    }

    public bool TryGetNodeControl(Guid nodeId, out MGGraphNode node)
    {
        node = null;
        return _Synchronizer?.TryGetNode(nodeId, out node) == true;
    }

    public bool TryGetPortControl(Guid portId, out MGGraphPort port)
    {
        port = null;
        return _Synchronizer?.TryGetPort(portId, out port) == true;
    }

    public bool TryGetCommentControl(Guid commentId, out MGGraphCommentBox commentBox)
    {
        commentBox = null;
        return _Synchronizer?.TryGetComment(commentId, out commentBox) == true;
    }

    internal void RegisterGraphPort(MGGraphPort port)
    {
        if (port == null || !_RegisteredConnectionPorts.Add(port))
        {
            return;
        }

        port.MouseHandler.DragStart += (sender, e) =>
        {
            if (e.IsLMB && ConnectionController.BeginDrag(port.PortId, port.GetLayoutAnchor()))
            {
                e.SetHandledBy(port, false);
            }
        };

        port.MouseHandler.Dragged += (sender, e) =>
        {
            if (e.IsLMB && ConnectionController.IsDragging && ConnectionController.StartPortId == port.PortId)
            {
                Vector2 viewportPoint = GetViewportPoint(e.Position);
                ConnectionController.UpdateDrag(viewportPoint, TryGetPortAtScreenPosition(e.Position, out MGGraphPort targetPort) ? targetPort.PortId : null);
            }
        };

        port.MouseHandler.DragEnd += (sender, e) =>
        {
            if (e.IsLMB && ConnectionController.IsDragging && ConnectionController.StartPortId == port.PortId)
            {
                if (TryGetPortAtScreenPosition(e.EndPosition, out MGGraphPort targetPort))
                {
                    ConnectionController.CompleteDrag(targetPort.PortId);
                }
                else
                {
                    Guid startPortId = ConnectionController.StartPortId;
                    ConnectionController.Cancel();
                    TryOpenNodeCreationMenu(e.EndPosition, startPortId);
                }
            }
        };
    }

    internal void RegisterGraphNode(MGGraphNode node)
    {
        if (node == null || !_RegisteredNodeInputs.Add(node))
        {
            return;
        }

        node.MouseHandler.DragStartCondition = DragStartCondition.Both;

        node.MouseHandler.LMBPressedInside += (sender, e) =>
        {
            Vector2 viewportPoint = GetViewportPoint(e.Position);
            if (TryGetPortAtScreenPosition(e.Position, out _))
            {
                return;
            }

            Focus(KeyboardFocusSource.Pointer);
            _PointerPressViewportPoint = viewportPoint;
            _CurrentSelectionViewportPoint = viewportPoint;
            _PressedNodeId = node.NodeId;
            _PressedCommentId = Guid.Empty;
            _PressedCommentResizeHandle = false;

            bool controlDown = IsControlDown();
            SelectNode(node.NodeId, additive: controlDown, toggle: controlDown);
            e.SetHandledBy(node, false);
        };

        node.MouseHandler.DragStart += (sender, e) =>
        {
            if (!e.IsLMB || e.Condition != DragStartCondition.MouseMovedAfterPress)
            {
                return;
            }

            Vector2 viewportPoint = GetViewportPoint(e.Position);
            if (TryGetPortAtScreenPosition(e.Position, out _))
            {
                return;
            }

            _PressedNodeId = node.NodeId;
            _PointerPressViewportPoint = viewportPoint;
            _CurrentSelectionViewportPoint = viewportPoint;
            BeginNodeDrag();
            e.SetHandledBy(node, false);
        };

        node.MouseHandler.Dragged += (sender, e) =>
        {
            if (_IsDraggingNodes && e.IsLMB)
            {
                UpdateNodeDrag(GetViewportPoint(e.Position));
            }
        };

        node.MouseHandler.DragEnd += (sender, e) =>
        {
            if (e.IsLMB)
            {
                CompleteNodeOrRectangleDrag(e.EndPosition);
            }
        };

        node.MouseHandler.ReleasedOutside += (sender, e) =>
        {
            if (e.IsLMB && IsNodePointerInteractionActive(node.NodeId))
            {
                CompleteNodeOrRectangleDrag(e.Position);
                e.SetHandledBy(node, false);
            }
        };
    }

    internal void SetPortConnectionFeedback(Guid portId, bool isSource, bool isTarget, bool isCompatible)
    {
        if (TryGetPortControl(portId, out MGGraphPort port))
        {
            port.IsConnectionDragSource = isSource;
            port.IsConnectionDragTarget = isTarget;
            port.IsConnectionCompatible = isCompatible;
        }
    }

    public bool ClearSelection()
    {
        bool changed = Selection.Clear();
        if (SelectedCommentIds.Count > 0)
        {
            SelectedCommentIds.Clear();
            changed = true;
        }

        if (changed)
        {
            UpdateSelectionVisuals();
        }

        return changed;
    }

    public bool SelectNode(Guid nodeId, bool additive = false, bool toggle = false)
    {
        bool changed = Selection.SelectNode(nodeId, additive, toggle);
        if (!additive && SelectedCommentIds.Count > 0)
        {
            SelectedCommentIds.Clear();
            changed = true;
        }

        if (SelectedNodeIds.Contains(nodeId))
        {
            changed |= BringSelectedNodesToFront();
        }

        if (changed)
        {
            UpdateSelectionVisuals();
        }

        return changed;
    }

    public bool SelectEdge(Guid edgeId, bool additive = false, bool toggle = false)
    {
        bool changed = Selection.SelectEdge(edgeId, additive, toggle);
        if (!additive && SelectedCommentIds.Count > 0)
        {
            SelectedCommentIds.Clear();
            changed = true;
        }

        if (changed)
        {
            UpdateSelectionVisuals();
        }

        return changed;
    }

    public bool SelectNodesInWorldRectangle(RectangleF worldRectangle, bool additive = false)
    {
        bool changed = Selection.SelectNodesInRectangle(Document, worldRectangle, additive);
        if (!additive && SelectedCommentIds.Count > 0)
        {
            SelectedCommentIds.Clear();
            changed = true;
        }

        changed |= SelectCommentsInWorldRectangle(worldRectangle, additive: true);
        if (SelectedNodeIds.Count > 0)
        {
            changed |= BringSelectedNodesToFront();
        }

        if (changed)
        {
            UpdateSelectionVisuals();
        }

        return changed;
    }

    public bool SelectComment(Guid commentId, bool additive = false, bool toggle = false)
    {
        if (commentId == Guid.Empty)
        {
            return false;
        }

        bool changed = false;
        if (!additive)
        {
            changed |= Selection.Clear();
            if (SelectedCommentIds.Count > 0 && !(SelectedCommentIds.Count == 1 && SelectedCommentIds.Contains(commentId)))
            {
                SelectedCommentIds.Clear();
                changed = true;
            }
        }

        if (toggle && SelectedCommentIds.Contains(commentId))
        {
            SelectedCommentIds.Remove(commentId);
            UpdateSelectionVisuals();
            return true;
        }

        changed |= SelectedCommentIds.Add(commentId);
        if (changed)
        {
            UpdateSelectionVisuals();
        }

        return changed;
    }

    public bool MoveSelectedCommentsBy(Vector2 worldDelta)
    {
        if (Document == null || SelectedCommentIds.Count == 0 || worldDelta == Vector2.Zero)
        {
            return false;
        }

        List<IGraphCommand> commands = new();
        foreach (Guid commentId in SelectedCommentIds)
        {
            GraphCommentModel comment = Document.TryGetComment(commentId);
            if (comment == null)
            {
                continue;
            }

            Rectangle next = OffsetRectangle(comment.Bounds, worldDelta);
            if (SnapToGrid)
            {
                Vector2 snapped = ViewportTransform.SnapPoint(new Vector2(next.X, next.Y));
                next = new Rectangle(
                    (int)MathF.Round(snapped.X),
                    (int)MathF.Round(snapped.Y),
                    next.Width,
                    next.Height);
            }

            if (next != comment.Bounds)
            {
                commands.Add(new MoveCommentCommand(comment.Id, comment.Bounds, next));
            }
        }

        if (commands.Count == 0)
        {
            return false;
        }

        return Commands.Execute(Document, new GraphBatchCommand("Move Comments", commands));
    }

    public bool ResizeComment(Guid commentId, Rectangle newBounds)
    {
        GraphCommentModel comment = Document?.TryGetComment(commentId);
        if (comment == null)
        {
            return false;
        }

        Rectangle normalized = NormalizeCommentBounds(newBounds);
        if (comment.Bounds == normalized)
        {
            return false;
        }

        return Commands.Execute(Document, new ResizeCommentCommand(commentId, comment.Bounds, normalized));
    }

    internal Rectangle NormalizeCommentBoundsToContent(GraphCommentModel comment, MGGraphCommentBox commentBox, string title = null, string text = null)
    {
        if (comment == null)
        {
            return Rectangle.Empty;
        }

        Rectangle normalized = NormalizeCommentBounds(comment.Bounds);
        if (commentBox == null)
        {
            return normalized;
        }

        float zoom = Math.Max(0.01f, ViewportTransform.Zoom);
        int scaledWidth = Math.Max(1, (int)MathF.Round(normalized.Width * zoom));
        int scaledHeight = Math.Max(1, (int)MathF.Round(normalized.Height * zoom));
        string description = GetCommentDescriptionText(title ?? comment.Title, text ?? comment.Text);
        int measuredHeight = commentBox.MeasureRequiredHeight(scaledWidth, description);
        int requiredScaledHeight = Math.Max(scaledHeight, measuredHeight);
        int requiredWorldHeight = Math.Max(normalized.Height, (int)Math.Ceiling(requiredScaledHeight / zoom));
        return new Rectangle(normalized.X, normalized.Y, normalized.Width, requiredWorldHeight);
    }

    internal void RegisterGraphComment(MGGraphCommentBox commentBox)
    {
        if (commentBox == null || !_RegisteredCommentInputs.Add(commentBox))
        {
            return;
        }

        commentBox.MouseHandler.DragStartCondition = DragStartCondition.Both;

        commentBox.MouseHandler.LMBPressedInside += (sender, e) =>
        {
            if (commentBox.IsEditing)
            {
                return;
            }

            if (!TryResolveCommentPointerInteraction(commentBox, e.Position, out Vector2 viewportPoint))
            {
                return;
            }

            Focus(KeyboardFocusSource.Pointer);
            _PointerPressViewportPoint = viewportPoint;
            _CurrentSelectionViewportPoint = viewportPoint;
            _PressedNodeId = Guid.Empty;
            _PressedCommentId = commentBox.CommentId;
            _PressedCommentResizeHandle = IsCommentResizeHandleHit(commentBox, viewportPoint);

            bool controlDown = IsControlDown();
            SelectComment(commentBox.CommentId, additive: controlDown, toggle: controlDown);
            e.SetHandledBy(commentBox, false);
        };

        commentBox.MouseHandler.DragStart += (sender, e) =>
        {
            if (commentBox.IsEditing)
            {
                return;
            }

            if (!e.IsLMB || e.Condition != DragStartCondition.MouseMovedAfterPress)
            {
                return;
            }

            if (!TryResolveCommentPointerInteraction(commentBox, e.Position, out Vector2 viewportPoint))
            {
                return;
            }

            _PressedNodeId = Guid.Empty;
            _PressedCommentId = commentBox.CommentId;
            _PointerPressViewportPoint = viewportPoint;
            _CurrentSelectionViewportPoint = viewportPoint;
            _PressedCommentResizeHandle = IsCommentResizeHandleHit(commentBox, viewportPoint);

            if (_PressedCommentResizeHandle)
            {
                BeginCommentResize();
            }
            else
            {
                BeginCommentDrag();
            }

            e.SetHandledBy(commentBox, false);
        };

        commentBox.MouseHandler.Dragged += (sender, e) =>
        {
            if (_IsDraggingComments && e.IsLMB)
            {
                UpdateCommentDrag(GetViewportPoint(e.Position));
            }
            else if (_IsResizingComment && e.IsLMB)
            {
                UpdateCommentResize(GetViewportPoint(e.Position));
            }
        };

        commentBox.MouseHandler.DragEnd += (sender, e) =>
        {
            if (e.IsLMB && IsCommentPointerInteractionActive(commentBox.CommentId))
            {
                CompleteNodeOrRectangleDrag(e.EndPosition);
            }
        };

        commentBox.MouseHandler.ReleasedOutside += (sender, e) =>
        {
            if (e.IsLMB && IsCommentPointerInteractionActive(commentBox.CommentId))
            {
                CompleteNodeOrRectangleDrag(e.Position);
                e.SetHandledBy(commentBox, false);
            }
        };

        commentBox.MouseHandler.LMBDoubleClickedInside += (sender, e) =>
        {
            if (commentBox.IsEditing)
            {
                return;
            }

            if (!TryResolveCommentPointerInteraction(commentBox, e.Position, out Vector2 viewportPoint))
            {
                return;
            }

            if (IsCommentResizeHandleHit(commentBox, viewportPoint))
            {
                return;
            }

            SelectComment(commentBox.CommentId, additive: false, toggle: false);
            if (TryBeginCommentEdit(commentBox.CommentId))
            {
                e.SetHandledBy(commentBox, false);
            }
        };
    }

    private bool TryResolveCommentPointerInteraction(MGGraphCommentBox commentBox, Point screenPosition, out Vector2 viewportPoint)
    {
        viewportPoint = Vector2.Zero;
        if (commentBox == null || !IsPointerInsideViewport(screenPosition))
        {
            return false;
        }

        if (!TryGetCommentAtScreenPosition(screenPosition, out MGGraphCommentBox targetComment) || !ReferenceEquals(targetComment, commentBox))
        {
            return false;
        }

        viewportPoint = GetViewportPoint(screenPosition);
        return true;
    }

    internal bool TryBeginCommentEdit(Guid commentId)
    {
        if (commentId == Guid.Empty)
        {
            return false;
        }

        if (_ActiveCommentEditor?.CommentId == commentId)
        {
            return true;
        }

        if (_ActiveCommentEditor != null)
        {
            CommitActiveCommentEditor();
        }

        if (!TryGetCommentControl(commentId, out MGGraphCommentBox commentBox))
        {
            return false;
        }

        GraphCommentModel comment = Document?.TryGetComment(commentId);
        if (comment == null)
        {
            return false;
        }

        CancelCurrentInteraction();
        commentBox.BeginEdit();
        MGTextBox bodyTextBox = commentBox.BodyTextBox;
        if (bodyTextBox == null)
        {
            commentBox.EndEdit(commitChanges: false);
            return false;
        }

        CommentEditorState state = null;
        EventHandler<BaseKeyPressedEventArgs> bodyKeyPressedHandler = (sender, e) =>
        {
            if (e.IsHandled || _ActiveCommentEditor != state)
            {
                return;
            }

            if (e.Key == Keys.Escape)
            {
                CancelActiveCommentEditor();
                e.SetHandledBy(bodyTextBox, false);
            }
            else if (e.Key == Keys.Enter && IsControlDown())
            {
                CommitActiveCommentEditor();
                e.SetHandledBy(bodyTextBox, false);
            }
        };
        EventHandler<EventArgs<MGElement>> focusChangedHandler = (sender, e) =>
        {
            if (_ActiveCommentEditor == state && IsCommentEditorElement(e.PreviousValue) && !IsCommentEditorElement(e.NewValue))
            {
                CommitActiveCommentEditor();
            }
        };

        state = new CommentEditorState
        {
            CommentId = commentId,
            CommentBox = commentBox,
            BodyKeyPressedHandler = bodyKeyPressedHandler,
            FocusChangedHandler = focusChangedHandler,
        };

        bodyTextBox.KeyboardHandler.Pressed += bodyKeyPressedHandler;
        GetDesktop().FocusedKeyboardHandlerChanged += focusChangedHandler;

        _ActiveCommentEditor = state;

        InvokeLater(() =>
        {
            if (_ActiveCommentEditor == state)
            {
                bodyTextBox.RequestFocus();
                bodyTextBox.SelectAll();
            }
        }, 1, InvokeLaterPriority.OnEndUpdate);

        return true;
    }

    internal bool CommitActiveCommentEditor()
    {
        if (_ActiveCommentEditor == null)
        {
            return false;
        }

        CommentEditorState state = _ActiveCommentEditor;
        GraphCommentModel comment = Document?.TryGetComment(state.CommentId);
        MGGraphCommentBox commentBox = state.CommentBox;
        MGTextBox bodyTextBox = commentBox?.BodyTextBox;
        if (commentBox == null || bodyTextBox == null)
        {
            CloseActiveCommentEditor(commitChanges: false);
            return false;
        }

        string newDescription = bodyTextBox.Text ?? string.Empty;
        bool hasComment = comment != null;
        string currentDescription = hasComment ? GetCommentDescriptionText(comment.Title, comment.Text) : string.Empty;
        Rectangle newBounds = hasComment ? NormalizeCommentBoundsToContent(comment, commentBox, text: newDescription) : Rectangle.Empty;
        bool changed = hasComment && (newDescription != currentDescription || newBounds != comment.Bounds);
        bool committed = changed && Commands.Execute(Document, new EditCommentCommand(state.CommentId, comment.Title, string.Empty, comment.Text, newDescription, comment.Bounds, newBounds));

        CloseActiveCommentEditor(commitChanges: committed);

        if (!hasComment)
        {
            return false;
        }

        if (!changed)
        {
            return false;
        }

        return committed;
    }

    internal bool CancelActiveCommentEditor()
    {
        if (_ActiveCommentEditor == null)
        {
            return false;
        }

        CloseActiveCommentEditor(commitChanges: false);
        return true;
    }

    private void CloseActiveCommentEditor(bool commitChanges)
    {
        CommentEditorState state = _ActiveCommentEditor;
        if (state == null)
        {
            return;
        }

        _ActiveCommentEditor = null;
        if (state.CommentBox?.BodyTextBox != null)
        {
            state.CommentBox.BodyTextBox.KeyboardHandler.Pressed -= state.BodyKeyPressedHandler;
        }

        GetDesktop().FocusedKeyboardHandlerChanged -= state.FocusChangedHandler;

        if (state.CommentBox != null)
        {
            state.CommentBox.EndEdit(commitChanges);
        }
    }

    private bool IsCommentEditorElement(MGElement element)
        => _ActiveCommentEditor?.CommentBox != null && element != null && _ActiveCommentEditor.CommentBox.IsSelfOrAncestorOf(element);

    public bool MoveSelectedNodesBy(Vector2 worldDelta)
    {
        if (Document == null || SelectedNodeIds.Count == 0 || worldDelta == Vector2.Zero)
        {
            return false;
        }

        List<GraphNodeMove> moves = new();
        foreach (Guid nodeId in SelectedNodeIds)
        {
            GraphNodeModel node = Document.TryGetNode(nodeId);
            if (node == null)
            {
                continue;
            }

            Vector2 next = node.Position + worldDelta;
            if (SnapToGrid)
            {
                next = ViewportTransform.SnapPoint(next);
            }

            if (node.Position != next)
            {
                moves.Add(new GraphNodeMove(node.Id, node.Position, next));
            }
        }

        return ExecuteMoveCommand(moves);
    }

    public bool PanViewportBy(Vector2 delta)
    {
        if (!AllowPan || delta == Vector2.Zero)
        {
            return false;
        }

        ViewportTransform.PanBy(delta);
        SynchronizeDocument();
        return true;
    }

    public bool ZoomAtViewportPoint(Vector2 viewportPoint, int scrollWheelDelta)
    {
        if (!AllowZoom || scrollWheelDelta == 0)
        {
            return false;
        }

        float wheelSteps = scrollWheelDelta / 120.0f;
        float zoomFactor = MathF.Pow(1.1f, wheelSteps);
        return ZoomAtViewportPoint(viewportPoint, zoomFactor);
    }

    public bool ZoomAtViewportPoint(Vector2 viewportPoint, float zoomFactor)
    {
        if (!AllowZoom || zoomFactor <= 0.0f)
        {
            return false;
        }

        float previousZoom = ViewportTransform.Zoom;
        Vector2 previousPan = ViewportTransform.Pan;
        ViewportTransform.ZoomAt(viewportPoint, zoomFactor);
        if (ViewportTransform.Zoom.Equals(previousZoom) && ViewportTransform.Pan == previousPan)
        {
            return false;
        }

        SynchronizeDocument();
        return true;
    }

    public void FrameOrigin(Rectangle? viewportBounds = null)
    {
        ViewportTransform.FrameOrigin(viewportBounds ?? GetViewportBoundsForFraming());
        SynchronizeDocument();
    }

    public bool FrameAll(Rectangle? viewportBounds = null, float? padding = null)
    {
        if (Document == null || Document.Nodes.Count == 0)
        {
            FrameOrigin(viewportBounds);
            return false;
        }

        ViewportTransform.FrameAll(Document.Nodes, viewportBounds ?? GetViewportBoundsForFraming(), padding ?? FramePadding);
        SynchronizeDocument();
        return true;
    }

    public bool FrameSelection(Rectangle? viewportBounds = null, float? padding = null)
    {
        if (Document == null || SelectedNodeIds.Count == 0)
        {
            return FrameAll(viewportBounds, padding);
        }

        List<GraphNodeModel> selectedNodes = new();
        for (int nodeIndex = 0; nodeIndex < Document.Nodes.Count; nodeIndex++)
        {
            GraphNodeModel node = Document.Nodes[nodeIndex];
            if (node != null && SelectedNodeIds.Contains(node.Id))
            {
                selectedNodes.Add(node);
            }
        }

        if (selectedNodes.Count == 0)
        {
            return FrameAll(viewportBounds, padding);
        }

        ViewportTransform.FrameAll(selectedNodes, viewportBounds ?? GetViewportBoundsForFraming(), padding ?? FramePadding);
        SynchronizeDocument();
        return true;
    }

    public bool HandleGraphShortcut(Keys key)
        => HandleGraphShortcut(key, IsControlDown());

    public bool HandleGraphShortcut(Keys key, bool controlDown)
    {
        if (controlDown)
        {
            switch (key)
            {
                case Keys.C:
                    return CopySelectionToClipboard();
                case Keys.V:
                    return PasteFromClipboard();
                case Keys.Z:
                    return Commands.Undo(Document);
                case Keys.Y:
                    return Commands.Redo(Document);
                case Keys.D:
                    return false;
            }
        }

        switch (key)
        {
            case Keys.A:
                if (controlDown)
                {
                    return false;
                }

                FrameAll();
                return true;
            case Keys.F:
                if (controlDown)
                {
                    return false;
                }

                FrameSelection();
                return true;
            case Keys.Home:
                FrameOrigin();
                return true;
            case Keys.Delete:
                return DeleteSelection();
            case Keys.Escape:
                CancelCurrentInteraction();
                return true;
            default:
                return false;
        }
    }

    public bool DeleteSelection()
    {
        if (Document == null || (SelectedNodeIds.Count == 0 && SelectedEdgeIds.Count == 0 && SelectedCommentIds.Count == 0))
        {
            return false;
        }

        HashSet<Guid> selectedNodeIds = new(SelectedNodeIds);
        List<IGraphCommand> commands = new();

        foreach (Guid edgeId in SelectedEdgeIds)
        {
            GraphEdgeModel edge = Document.TryGetEdge(edgeId);
            if (edge != null && !selectedNodeIds.Contains(edge.SourceNodeId) && !selectedNodeIds.Contains(edge.TargetNodeId))
            {
                commands.Add(new DisconnectPortsCommand(edgeId));
            }
        }

        foreach (Guid nodeId in selectedNodeIds)
        {
            if (Document.TryGetNode(nodeId) != null)
            {
                commands.Add(new DeleteNodeCommand(nodeId));
            }
        }

        foreach (Guid commentId in SelectedCommentIds)
        {
            if (Document.TryGetComment(commentId) != null)
            {
                commands.Add(new DeleteCommentCommand(commentId));
            }
        }

        bool deleted = commands.Count > 0 && Commands.Execute(Document, new GraphBatchCommand("Delete Selection", commands));
        if (deleted)
        {
            ClearSelection();
        }

        return deleted;
    }

    public bool CopySelectionToClipboard()
    {
        if (!TryCreateClipboardDocument(out GraphDocument clipboardDocument))
        {
            return false;
        }

        GraphSerializationResult result = _ClipboardSerializer.Serialize(clipboardDocument);
        return result.Success
               && !string.IsNullOrWhiteSpace(result.Json)
               && TryWriteClipboardText($"{GraphClipboardPrefix}\n{result.Json}");
    }

    public bool PasteFromClipboard(Vector2? worldPosition = null)
    {
        if (Document == null || !TryReadClipboardDocument(out GraphDocument clipboardDocument))
        {
            return false;
        }

        Vector2 sourceOrigin = GetClipboardWorldOrigin(clipboardDocument);
        Vector2 targetOrigin = worldPosition ?? GetDefaultPasteWorldPosition();
        if (SnapToGrid)
        {
            targetOrigin = ViewportTransform.SnapPoint(targetOrigin);
        }

        Vector2 delta = targetOrigin - sourceOrigin;
        Dictionary<Guid, Guid> nodeIdMap = new();
        Dictionary<Guid, Guid> portIdMap = new();
        List<Guid> pastedNodeIds = new();
        List<Guid> pastedCommentIds = new();
        List<IGraphCommand> commands = new();

        for (int nodeIndex = 0; nodeIndex < clipboardDocument.Nodes.Count; nodeIndex++)
        {
            GraphNodeModel pastedNode = CreatePastedNode(clipboardDocument.Nodes[nodeIndex], delta, nodeIdMap, portIdMap);
            pastedNodeIds.Add(pastedNode.Id);
            commands.Add(new CreateNodeCommand(pastedNode));
        }

        for (int commentIndex = 0; commentIndex < clipboardDocument.Comments.Count; commentIndex++)
        {
            GraphCommentModel pastedComment = CreatePastedComment(clipboardDocument.Comments[commentIndex], delta);
            pastedCommentIds.Add(pastedComment.Id);
            commands.Add(new CreateCommentCommand(pastedComment));
        }

        for (int edgeIndex = 0; edgeIndex < clipboardDocument.Edges.Count; edgeIndex++)
        {
            GraphEdgeModel sourceEdge = clipboardDocument.Edges[edgeIndex];
            if (!nodeIdMap.TryGetValue(sourceEdge.SourceNodeId, out Guid sourceNodeId)
                || !nodeIdMap.TryGetValue(sourceEdge.TargetNodeId, out Guid targetNodeId)
                || !portIdMap.TryGetValue(sourceEdge.SourcePortId, out Guid sourcePortId)
                || !portIdMap.TryGetValue(sourceEdge.TargetPortId, out Guid targetPortId))
            {
                continue;
            }

            commands.Add(new ConnectPortsCommand(new GraphEdgeModel(Guid.NewGuid(), sourceNodeId, sourcePortId, targetNodeId, targetPortId)
            {
                RenderMetadata = CopyMetadata(sourceEdge.RenderMetadata),
            }));
        }

        bool pasted = commands.Count > 0 && Commands.Execute(Document, new GraphBatchCommand("Paste Selection", commands));
        if (!pasted)
        {
            return false;
        }

        SelectedNodeIds.Clear();
        SelectedEdgeIds.Clear();
        SelectedCommentIds.Clear();
        for (int i = 0; i < pastedNodeIds.Count; i++)
        {
            SelectedNodeIds.Add(pastedNodeIds[i]);
        }

        for (int i = 0; i < pastedCommentIds.Count; i++)
        {
            SelectedCommentIds.Add(pastedCommentIds[i]);
        }

        UpdateSelectionVisuals();
        return true;
    }

    internal bool CanPasteFromClipboard()
        => TryReadClipboardDocument(out _);

    private bool TryCreateClipboardDocument(out GraphDocument clipboardDocument)
    {
        clipboardDocument = null;
        if (Document == null || (SelectedNodeIds.Count == 0 && SelectedCommentIds.Count == 0))
        {
            return false;
        }

        HashSet<Guid> selectedNodeIds = new(SelectedNodeIds);
        GraphDocument selectionDocument = new()
        {
            Version = Document.Version,
            DisallowCycles = Document.DisallowCycles,
        };

        foreach (Guid nodeId in selectedNodeIds)
        {
            GraphNodeModel node = Document.TryGetNode(nodeId);
            if (node != null)
            {
                selectionDocument.AddNode(CloneNode(node));
            }
        }

        for (int edgeIndex = 0; edgeIndex < Document.Edges.Count; edgeIndex++)
        {
            GraphEdgeModel edge = Document.Edges[edgeIndex];
            if (edge != null && selectedNodeIds.Contains(edge.SourceNodeId) && selectedNodeIds.Contains(edge.TargetNodeId))
            {
                selectionDocument.AddEdge(new GraphEdgeModel(edge.Id, edge.SourceNodeId, edge.SourcePortId, edge.TargetNodeId, edge.TargetPortId)
                {
                    RenderMetadata = CopyMetadata(edge.RenderMetadata),
                }, validate: false);
            }
        }

        foreach (Guid commentId in SelectedCommentIds)
        {
            GraphCommentModel comment = Document.TryGetComment(commentId);
            if (comment != null)
            {
                selectionDocument.AddComment(CloneComment(comment));
            }
        }

        if (selectionDocument.Nodes.Count == 0 && selectionDocument.Comments.Count == 0)
        {
            return false;
        }

        clipboardDocument = selectionDocument;
        return true;
    }

    private bool TryReadClipboardDocument(out GraphDocument clipboardDocument)
    {
        clipboardDocument = null;
        string clipboardText = TryReadClipboardText();
        if (string.IsNullOrWhiteSpace(clipboardText) || !clipboardText.StartsWith(GraphClipboardPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string json = clipboardText.Substring(GraphClipboardPrefix.Length).TrimStart('\r', '\n');
        GraphSerializationResult result = _ClipboardSerializer.Deserialize(json);
        if (!result.Success || result.Document == null || (result.Document.Nodes.Count == 0 && result.Document.Comments.Count == 0))
        {
            return false;
        }

        clipboardDocument = result.Document;
        return true;
    }

    private bool TryWriteClipboardText(string text)
    {
        try
        {
            ClipboardTextWriter?.Invoke(text ?? string.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string TryReadClipboardText()
    {
        try
        {
            return ClipboardTextReader?.Invoke() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private Vector2 GetDefaultPasteWorldPosition()
    {
        Point mousePosition = InputTracker.Mouse.CurrentPosition;
        if (IsPointerInsideViewport(mousePosition))
        {
            return GetWorldPointFromScreenPosition(mousePosition);
        }

        Rectangle viewportBounds = GetViewportBoundsForFraming();
        return ViewportTransform.LayoutToWorld(new Vector2(viewportBounds.Center.X, viewportBounds.Center.Y));
    }

    private static Vector2 GetClipboardWorldOrigin(GraphDocument clipboardDocument)
    {
        bool hasValue = false;
        float left = 0.0f;
        float top = 0.0f;

        for (int nodeIndex = 0; nodeIndex < clipboardDocument.Nodes.Count; nodeIndex++)
        {
            Vector2 position = clipboardDocument.Nodes[nodeIndex].Position;
            if (!hasValue)
            {
                left = position.X;
                top = position.Y;
                hasValue = true;
            }
            else
            {
                left = Math.Min(left, position.X);
                top = Math.Min(top, position.Y);
            }
        }

        for (int commentIndex = 0; commentIndex < clipboardDocument.Comments.Count; commentIndex++)
        {
            Rectangle bounds = clipboardDocument.Comments[commentIndex].Bounds;
            if (!hasValue)
            {
                left = bounds.Left;
                top = bounds.Top;
                hasValue = true;
            }
            else
            {
                left = Math.Min(left, bounds.Left);
                top = Math.Min(top, bounds.Top);
            }
        }

        return hasValue ? new Vector2(left, top) : Vector2.Zero;
    }

    private static GraphNodeModel CloneNode(GraphNodeModel source)
    {
        GraphNodeModel clone = new(source.Id, source.NodeType, source.Title, source.Position)
        {
            Size = source.Size,
            IsCollapsed = source.IsCollapsed,
            Properties = CopyMetadata(source.Properties),
            EditorMetadata = CopyMetadata(source.EditorMetadata),
        };

        for (int portIndex = 0; portIndex < source.Ports.Count; portIndex++)
        {
            GraphPortModel port = source.Ports[portIndex];
            clone.Ports.Add(new GraphPortModel(port.NodeId, port.Id, port.Name, port.Direction, port.ValueType, port.Cardinality, port.IsRequired)
            {
                DefaultValue = port.DefaultValue,
                CustomTypeName = port.CustomTypeName,
                EditorMetadata = CopyMetadata(port.EditorMetadata),
            });
        }

        return clone;
    }

    private static GraphNodeModel CreatePastedNode(GraphNodeModel source, Vector2 delta, Dictionary<Guid, Guid> nodeIdMap, Dictionary<Guid, Guid> portIdMap)
    {
        Guid newNodeId = Guid.NewGuid();
        nodeIdMap[source.Id] = newNodeId;
        GraphNodeModel clone = new(newNodeId, source.NodeType, source.Title, source.Position + delta)
        {
            Size = source.Size,
            IsCollapsed = source.IsCollapsed,
            Properties = CopyMetadata(source.Properties),
            EditorMetadata = CopyMetadata(source.EditorMetadata),
        };

        for (int portIndex = 0; portIndex < source.Ports.Count; portIndex++)
        {
            GraphPortModel port = source.Ports[portIndex];
            Guid newPortId = Guid.NewGuid();
            portIdMap[port.Id] = newPortId;
            clone.Ports.Add(new GraphPortModel(newNodeId, newPortId, port.Name, port.Direction, port.ValueType, port.Cardinality, port.IsRequired)
            {
                DefaultValue = port.DefaultValue,
                CustomTypeName = port.CustomTypeName,
                EditorMetadata = CopyMetadata(port.EditorMetadata),
            });
        }

        return clone;
    }

    private static GraphCommentModel CloneComment(GraphCommentModel source)
        => new(source.Id, source.Bounds, source.Title, source.Text)
        {
            Color = source.Color,
            EditorMetadata = CopyMetadata(source.EditorMetadata),
        };

    private static GraphCommentModel CreatePastedComment(GraphCommentModel source, Vector2 delta)
        => new(Guid.NewGuid(), OffsetRectangle(source.Bounds, delta), source.Title, source.Text)
        {
            Color = source.Color,
            EditorMetadata = CopyMetadata(source.EditorMetadata),
        };

    private static Dictionary<string, string> CopyMetadata(Dictionary<string, string> source)
        => source == null ? new(StringComparer.Ordinal) : new(source, StringComparer.Ordinal);

    public GraphCommentModel CreateCommentAt(Vector2 worldPosition, string title = "Comment", string text = "")
    {
        if (Document == null)
        {
            return null;
        }

        Vector2 position = SnapToGrid ? ViewportTransform.SnapPoint(worldPosition) : worldPosition;
        string description = !string.IsNullOrEmpty(text)
            ? text
            : string.Equals(title, "Comment", StringComparison.Ordinal) ? string.Empty : title ?? string.Empty;
        GraphCommentModel comment = new(Guid.NewGuid(), new Rectangle((int)MathF.Round(position.X), (int)MathF.Round(position.Y), 260, 120), string.Empty, description);
        if (!Commands.Execute(Document, new CreateCommentCommand(comment)))
        {
            return null;
        }

        ClearSelection();
        SelectComment(comment.Id);
        return comment;
    }

    public GraphNodeModel CreateNodeFromDefinition(GraphNodeDefinition definition, Vector2 worldPosition, Guid? connectFromPortId = null)
    {
        if (definition == null || Document == null)
        {
            return null;
        }

        GraphNodeModel node = definition.CreateNode(Guid.NewGuid(), worldPosition);
        bool positionedFromConnectionAnchor = false;
        if (connectFromPortId.HasValue)
        {
            GraphPortModel draggedPort = Document.TryGetPort(connectFromPortId.Value);
            if (draggedPort != null && NodePalette?.TryFindCompatiblePort(node, draggedPort, out GraphPortModel compatiblePort) == true)
            {
                node.Position = GetNodePositionForPortAnchor(node, compatiblePort, worldPosition);
                positionedFromConnectionAnchor = true;
            }
        }

        if (SnapToGrid && !positionedFromConnectionAnchor)
        {
            node.Position = ViewportTransform.SnapPoint(node.Position);
        }

        if (!Commands.Execute(Document, new CreateNodeCommand(node)))
        {
            return null;
        }

        ClearSelection();
        SelectNode(node.Id);

        if (connectFromPortId.HasValue)
        {
            GraphPortModel draggedPort = Document.TryGetPort(connectFromPortId.Value);
            if (draggedPort != null && NodePalette?.TryFindCompatiblePort(node, draggedPort, out GraphPortModel compatiblePort) == true)
            {
                ConnectionController.TryCreateConnection(connectFromPortId.Value, compatiblePort.Id);
            }
        }

        return node;
    }

    private static Vector2 GetNodePositionForPortAnchor(GraphNodeModel node, GraphPortModel port, Vector2 anchorWorldPosition)
    {
        if (node == null || port == null)
        {
            return anchorWorldPosition;
        }

        Vector2 originalPosition = node.Position;
        node.Position = Vector2.Zero;
        bool hasAnchor = GraphPortAnchorResolver.TryGetPortWorldAnchor(node, port, out Vector2 anchorOffset);
        node.Position = originalPosition;
        return hasAnchor ? anchorWorldPosition - anchorOffset : anchorWorldPosition;
    }

    public MGContextMenu CreateNodeCreationMenu(Vector2 worldPosition, Guid? connectFromPortId = null)
    {
        MGContextMenu menu = new(ParentWindow, string.Empty);
        PopulateNodeCreationMenu(menu, worldPosition, connectFromPortId);

        if (!connectFromPortId.HasValue)
        {
            if (menu.Items.Count > 0)
            {
                menu.AddSeparator();
            }

            MGContextMenuButton commentButton = menu.AddButton("Comment", _ => CreateCommentAt(worldPosition));
            commentButton.CommandId = "graph.createComment";
        }

        return menu;
    }

    public MGContextMenu CreateGraphContextMenu(Vector2 worldPosition, bool includeCreationItems = true)
    {
        MGContextMenu menu = new(ParentWindow, string.Empty);
        bool hasEditActions = false;
        if (SelectedNodeIds.Count > 0 || SelectedCommentIds.Count > 0)
        {
            MGContextMenuButton copyButton = menu.AddButton("Copy", _ => CopySelectionToClipboard());
            copyButton.CommandId = "graph.copy";
            hasEditActions = true;
        }

        if (CanPasteFromClipboard())
        {
            MGContextMenuButton pasteButton = menu.AddButton("Paste", _ => PasteFromClipboard(worldPosition));
            pasteButton.CommandId = "graph.paste";
            hasEditActions = true;
        }

        if (!includeCreationItems)
        {
            return menu;
        }

        if (hasEditActions)
        {
            menu.AddSeparator();
        }

        MGContextMenuButton commentButton = menu.AddButton("Comment", _ => CreateCommentAt(worldPosition));
        commentButton.CommandId = "graph.createComment";
        PopulateNodeCreationMenu(menu, worldPosition, connectFromPortId: null, addLeadingSeparator: true);
        return menu;
    }

    private bool PopulateNodeCreationMenu(MGContextMenu menu, Vector2 worldPosition, Guid? connectFromPortId = null, bool addLeadingSeparator = false)
    {
        if (menu == null)
        {
            return false;
        }

        GraphPortModel draggedPort = connectFromPortId.HasValue ? Document?.TryGetPort(connectFromPortId.Value) : null;
        IEnumerable<GraphNodeDefinition> definitions = NodePalette?.GetDefinitions(draggedPort) ?? Array.Empty<GraphNodeDefinition>();
        string previousCategory = null;
        bool addedAny = false;
        foreach (GraphNodeDefinition definition in definitions)
        {
            if (definition == null)
            {
                continue;
            }

            if (previousCategory != null && !string.Equals(previousCategory, definition.Category, StringComparison.Ordinal))
            {
                menu.AddSeparator();
            }
            else if (!addedAny && addLeadingSeparator)
            {
                menu.AddSeparator();
            }

            previousCategory = definition.Category;
            GraphNodeDefinition capturedDefinition = definition;
            string label = string.IsNullOrWhiteSpace(definition.Category)
                ? definition.DisplayName
                : $"{definition.Category} / {definition.DisplayName}";
            MGContextMenuButton button = menu.AddButton(label, _ => CreateNodeFromDefinition(capturedDefinition, worldPosition, connectFromPortId));
            button.CommandId = $"graph.createNode:{definition.NodeType}";
            addedAny = true;
        }

        return addedAny;
    }

    public void CancelCurrentInteraction()
    {
        _IsPanningViewport = false;
        _IsDraggingNodes = false;
        _IsDraggingComments = false;
        _IsResizingComment = false;
        IsSelectionRectangleActive = false;
        _PressedNodeId = Guid.Empty;
        _PressedCommentId = Guid.Empty;
        _PressedCommentResizeHandle = false;
        _NodeDragStartPositions.Clear();
        _CommentDragStartBounds.Clear();
        ConnectionController.Cancel();
    }

    private void OnDocumentGraphChanged(object sender, EventArgs e)
        => SynchronizeDocument();

    private void OnGraphContextMenuRequested(object sender, ContextMenuRequestedEventArgs e)
    {
        if (!IsPointerInsideViewport(e.Position))
        {
            e.Menu = null;
            return;
        }

        Vector2 viewportPoint = GetViewportPoint(e.Position);
        Vector2 worldPosition = GetWorldPointFromScreenPosition(e.Position);
        bool overExistingElement = TryGetNodeAtScreenPosition(e.Position, out _)
                                   || TryGetPortAtScreenPosition(e.Position, out _)
                                   || TryGetCommentAtScreenPosition(e.Position, out _)
                                   || TryGetEdgeAtViewportPoint(viewportPoint, out _);
        MGContextMenu menu = CreateGraphContextMenu(worldPosition, includeCreationItems: !overExistingElement);
        if (menu.Items.Count == 0)
        {
            e.Handled = true;
            return;
        }

        e.Menu = menu;
    }

    private bool TryOpenNodeCreationMenu(Point screenPosition, Guid? connectFromPortId)
    {
        if (!connectFromPortId.HasValue || !IsPointerInsideViewport(screenPosition))
        {
            return false;
        }

        Vector2 worldPosition = GetWorldPointFromScreenPosition(screenPosition);
        MGContextMenu menu = CreateNodeCreationMenu(worldPosition, connectFromPortId);
        return menu.Items.Count > 0 && menu.TryOpenContextMenu(screenPosition);
    }

    private void RegisterViewportInputHandlers()
    {
        MouseHandler.DragStartCondition = DragStartCondition.Both;

        MouseHandler.DragStart += (sender, e) =>
        {
            if (AllowPan && e.IsMMB && IsPointerInsideViewport(e.Position))
            {
                _IsPanningViewport = true;
                _PanStartScreenPosition = e.Position;
                _PanStartValue = ViewportTransform.Pan;
                e.SetHandledBy(this, false);
                Focus(KeyboardFocusSource.Pointer);
            }

            if (e.IsLMB && e.Condition == DragStartCondition.MouseMovedAfterPress && IsPointerInsideViewport(e.Position))
            {
                if (IsCommentEditorElement(SelfOrParentWindow?.HoveredElement))
                {
                    return;
                }

                if (_PressedNodeId != Guid.Empty)
                {
                    BeginNodeDrag();
                }
                else if (_PressedCommentId != Guid.Empty)
                {
                    if (_PressedCommentResizeHandle)
                    {
                        BeginCommentResize();
                    }
                    else
                    {
                        BeginCommentDrag();
                    }
                }
                else
                {
                    IsSelectionRectangleActive = true;
                    _CurrentSelectionViewportPoint = GetViewportPoint(e.Position);
                }
            }
        };

        MouseHandler.Dragged += (sender, e) =>
        {
            if (_IsPanningViewport && e.IsMMB)
            {
                Point delta = e.Position - _PanStartScreenPosition;
                Vector2 nextPan = _PanStartValue + delta.ToVector2();
                if (ViewportTransform.Pan != nextPan)
                {
                    ViewportTransform.Pan = nextPan;
                    SynchronizeDocument();
                }
            }

            else if (_IsDraggingNodes && e.IsLMB)
            {
                UpdateNodeDrag(GetViewportPoint(e.Position));
            }

            else if (_IsDraggingComments && e.IsLMB)
            {
                UpdateCommentDrag(GetViewportPoint(e.Position));
            }

            else if (_IsResizingComment && e.IsLMB)
            {
                UpdateCommentResize(GetViewportPoint(e.Position));
            }

            else if (IsSelectionRectangleActive && e.IsLMB)
            {
                _CurrentSelectionViewportPoint = GetViewportPoint(e.Position);
            }
        };

        MouseHandler.DragEnd += (sender, e) =>
        {
            if (e.IsMMB)
            {
                _IsPanningViewport = false;
            }

            if (e.IsLMB)
            {
                CompleteNodeOrRectangleDrag(e.EndPosition);
            }
        };

        MouseHandler.ReleasedOutside += (sender, e) =>
        {
            if (e.IsMMB)
            {
                _IsPanningViewport = false;
            }

            if (e.IsLMB)
            {
                CompleteNodeOrRectangleDrag(e.Position);
            }
        };

        MouseHandler.LMBPressedInside += (sender, e) =>
        {
            if (!IsPointerInsideViewport(e.Position))
            {
                return;
            }

            if (IsCommentEditorElement(SelfOrParentWindow?.HoveredElement))
            {
                return;
            }

            Focus(KeyboardFocusSource.Pointer);
            _PointerPressViewportPoint = GetViewportPoint(e.Position);
            _CurrentSelectionViewportPoint = _PointerPressViewportPoint;
            bool controlDown = IsControlDown();
            if (TryGetPortAtScreenPosition(e.Position, out _))
            {
                return;
            }
            else if (TryGetNodeAtScreenPosition(e.Position, out MGGraphNode node))
            {
                _PressedNodeId = node.NodeId;
                _PressedCommentId = Guid.Empty;
                _PressedCommentResizeHandle = false;
                SelectNode(node.NodeId, additive: controlDown, toggle: controlDown);
                e.SetHandledBy(this, false);
            }
            else if (TryGetCommentAtScreenPosition(e.Position, out MGGraphCommentBox commentBox))
            {
                _PressedNodeId = Guid.Empty;
                _PressedCommentId = commentBox.CommentId;
                _PressedCommentResizeHandle = IsCommentResizeHandleHit(commentBox, _PointerPressViewportPoint);
                SelectComment(commentBox.CommentId, additive: controlDown, toggle: controlDown);
                e.SetHandledBy(this, false);
            }
            else if (TryGetEdgeAtViewportPoint(_PointerPressViewportPoint, out GraphEdgeModel edge))
            {
                _PressedNodeId = Guid.Empty;
                _PressedCommentId = Guid.Empty;
                _PressedCommentResizeHandle = false;
                SelectEdge(edge.Id, additive: controlDown, toggle: controlDown);
                e.SetHandledBy(this, false);
            }
            else
            {
                _PressedNodeId = Guid.Empty;
                _PressedCommentId = Guid.Empty;
                _PressedCommentResizeHandle = false;
                if (!controlDown)
                {
                    ClearSelection();
                }
            }
        };

        MouseHandler.Scrolled += (sender, e) =>
        {
            if (AllowZoom && IsPointerInsideViewport(e.Position))
            {
                Vector2 viewportPoint = GetViewportPoint(e.Position);
                if (ZoomAtViewportPoint(viewportPoint, e.ScrollWheelDelta))
                {
                    e.SetHandledBy(this, false);
                }
            }
        };

        KeyboardHandler.Pressed += (sender, e) =>
        {
            if (HandleGraphShortcut(e.Key, IsControlDown()))
            {
                e.SetHandledBy(this, false);
            }
        };
    }

    public void UpdateSelectionVisuals()
    {
        if (Document == null)
        {
            return;
        }

        for (int nodeIndex = 0; nodeIndex < Document.Nodes.Count; nodeIndex++)
        {
            GraphNodeModel nodeModel = Document.Nodes[nodeIndex];
            if (nodeModel != null && TryGetNodeControl(nodeModel.Id, out MGGraphNode node))
            {
                node.IsSelected = SelectedNodeIds.Contains(nodeModel.Id);
                node.ApplySelectionVisual();
            }
        }

        for (int commentIndex = 0; commentIndex < Document.Comments.Count; commentIndex++)
        {
            GraphCommentModel commentModel = Document.Comments[commentIndex];
            if (commentModel != null && TryGetCommentControl(commentModel.Id, out MGGraphCommentBox commentBox))
            {
                commentBox.IsSelected = SelectedCommentIds.Contains(commentModel.Id);
                commentBox.ApplySelectionVisual();
            }
        }
    }

    private void BeginNodeDrag()
    {
        _NodeDragStartPositions.Clear();
        foreach (Guid selectedNodeId in SelectedNodeIds)
        {
            GraphNodeModel node = Document.TryGetNode(selectedNodeId);
            if (node != null)
            {
                _NodeDragStartPositions[selectedNodeId] = node.Position;
            }
        }

        _IsDraggingNodes = _NodeDragStartPositions.Count > 0;
    }

    private void UpdateNodeDrag(Vector2 currentViewportPoint)
    {
        Vector2 startWorld = ViewportTransform.LayoutToWorld(_PointerPressViewportPoint);
        Vector2 currentWorld = ViewportTransform.LayoutToWorld(currentViewportPoint);
        Vector2 worldDelta = currentWorld - startWorld;
        bool changed = false;

        foreach (KeyValuePair<Guid, Vector2> item in _NodeDragStartPositions)
        {
            GraphNodeModel node = Document.TryGetNode(item.Key);
            if (node == null)
            {
                continue;
            }

            Vector2 next = item.Value + worldDelta;
            if (SnapToGrid)
            {
                next = ViewportTransform.SnapPoint(next);
            }

            if (node.Position != next)
            {
                node.Position = next;
                changed = true;
            }
        }

        if (changed)
        {
            Document.NotifyGraphChanged();
        }
    }

    private void CompleteNodeOrRectangleDrag(Point screenPosition)
    {
        if (_IsDraggingNodes)
        {
            CommitNodeDrag();
        }
        else if (_IsDraggingComments)
        {
            CommitCommentDrag();
        }
        else if (_IsResizingComment)
        {
            CommitCommentResize();
        }
        else if (IsSelectionRectangleActive)
        {
            _CurrentSelectionViewportPoint = GetViewportPoint(screenPosition);
            RectangleF viewportRectangle = SelectionRectangleViewportBounds;
            if (viewportRectangle.Width > 1.0f && viewportRectangle.Height > 1.0f)
            {
                SelectNodesInWorldRectangle(ViewportRectangleToWorldRectangle(viewportRectangle), additive: IsControlDown());
            }
        }

        _PressedNodeId = Guid.Empty;
        _PressedCommentId = Guid.Empty;
        _PressedCommentResizeHandle = false;
        _IsDraggingNodes = false;
        _IsDraggingComments = false;
        _IsResizingComment = false;
        IsSelectionRectangleActive = false;
        _NodeDragStartPositions.Clear();
        _CommentDragStartBounds.Clear();
    }

    private bool IsNodePointerInteractionActive(Guid nodeId)
        => nodeId != Guid.Empty && (_PressedNodeId == nodeId || (_IsDraggingNodes && SelectedNodeIds.Contains(nodeId)));

    private bool IsCommentPointerInteractionActive(Guid commentId)
        => commentId != Guid.Empty && (_PressedCommentId == commentId || (_IsDraggingComments && SelectedCommentIds.Contains(commentId)) || (_IsResizingComment && _PressedCommentId == commentId));

    private void CommitNodeDrag()
    {
        List<GraphNodeMove> moves = new();
        foreach (KeyValuePair<Guid, Vector2> item in _NodeDragStartPositions)
        {
            GraphNodeModel node = Document.TryGetNode(item.Key);
            if (node != null && node.Position != item.Value)
            {
                moves.Add(new GraphNodeMove(item.Key, item.Value, node.Position));
            }
        }

        if (moves.Count == 0)
        {
            return;
        }

        ExecuteMoveCommand(moves);
    }

    private bool ExecuteMoveCommand(List<GraphNodeMove> moves)
    {
        if (moves == null || moves.Count == 0)
        {
            return false;
        }

        if (moves.Count == 1)
        {
            GraphNodeMove move = moves[0];
            return Commands.Execute(Document, new MoveNodeCommand(move.NodeId, move.OldPosition, move.NewPosition));
        }

        return Commands.Execute(Document, new MoveNodesCommand(moves));
    }

    private bool SelectCommentsInWorldRectangle(RectangleF worldRectangle, bool additive = false)
    {
        bool changed = false;
        if (!additive && SelectedCommentIds.Count > 0)
        {
            SelectedCommentIds.Clear();
            changed = true;
        }

        if (Document == null || worldRectangle.Width <= 0.0f || worldRectangle.Height <= 0.0f)
        {
            return changed;
        }

        for (int commentIndex = 0; commentIndex < Document.Comments.Count; commentIndex++)
        {
            GraphCommentModel comment = Document.Comments[commentIndex];
            if (comment != null && Intersects(worldRectangle, ToRectangleF(comment.Bounds)))
            {
                changed |= SelectedCommentIds.Add(comment.Id);
            }
        }

        return changed;
    }

    private void BeginCommentDrag()
    {
        _CommentDragStartBounds.Clear();
        foreach (Guid selectedCommentId in SelectedCommentIds)
        {
            GraphCommentModel comment = Document.TryGetComment(selectedCommentId);
            if (comment != null)
            {
                _CommentDragStartBounds[selectedCommentId] = comment.Bounds;
            }
        }

        _IsDraggingComments = _CommentDragStartBounds.Count > 0;
    }

    private void UpdateCommentDrag(Vector2 currentViewportPoint)
    {
        Vector2 startWorld = ViewportTransform.LayoutToWorld(_PointerPressViewportPoint);
        Vector2 currentWorld = ViewportTransform.LayoutToWorld(currentViewportPoint);
        Vector2 worldDelta = currentWorld - startWorld;
        bool changed = false;

        foreach (KeyValuePair<Guid, Rectangle> item in _CommentDragStartBounds)
        {
            GraphCommentModel comment = Document.TryGetComment(item.Key);
            if (comment == null)
            {
                continue;
            }

            Rectangle next = OffsetRectangle(item.Value, worldDelta);
            if (SnapToGrid)
            {
                Vector2 snapped = ViewportTransform.SnapPoint(new Vector2(next.X, next.Y));
                next = new Rectangle((int)MathF.Round(snapped.X), (int)MathF.Round(snapped.Y), next.Width, next.Height);
            }

            if (comment.Bounds != next)
            {
                comment.Bounds = next;
                changed = true;
            }
        }

        if (changed)
        {
            Document.NotifyGraphChanged();
        }
    }

    private void CommitCommentDrag()
    {
        List<IGraphCommand> commands = new();
        foreach (KeyValuePair<Guid, Rectangle> item in _CommentDragStartBounds)
        {
            GraphCommentModel comment = Document.TryGetComment(item.Key);
            if (comment != null && comment.Bounds != item.Value)
            {
                commands.Add(new MoveCommentCommand(comment.Id, item.Value, comment.Bounds));
            }
        }

        if (commands.Count > 0)
        {
            Commands.Execute(Document, new GraphBatchCommand("Move Comments", commands));
        }
    }

    private void BeginCommentResize()
    {
        GraphCommentModel comment = Document.TryGetComment(_PressedCommentId);
        if (comment == null)
        {
            return;
        }

        _CommentResizeStartBounds = comment.Bounds;
        _IsResizingComment = true;
    }

    private void UpdateCommentResize(Vector2 currentViewportPoint)
    {
        GraphCommentModel comment = Document.TryGetComment(_PressedCommentId);
        if (comment == null)
        {
            return;
        }

        Vector2 startWorld = ViewportTransform.LayoutToWorld(_PointerPressViewportPoint);
        Vector2 currentWorld = ViewportTransform.LayoutToWorld(currentViewportPoint);
        Vector2 worldDelta = currentWorld - startWorld;
        Rectangle next = NormalizeCommentBounds(new Rectangle(
            _CommentResizeStartBounds.X,
            _CommentResizeStartBounds.Y,
            _CommentResizeStartBounds.Width + (int)MathF.Round(worldDelta.X),
            _CommentResizeStartBounds.Height + (int)MathF.Round(worldDelta.Y)));

        if (comment.Bounds != next)
        {
            comment.Bounds = next;
            Document.NotifyGraphChanged();
        }
    }

    private void CommitCommentResize()
    {
        GraphCommentModel comment = Document.TryGetComment(_PressedCommentId);
        if (comment != null && comment.Bounds != _CommentResizeStartBounds)
        {
            Commands.Execute(Document, new ResizeCommentCommand(comment.Id, _CommentResizeStartBounds, comment.Bounds));
        }
    }

    private bool BringSelectedNodesToFront()
    {
        if (NodesCanvas == null || SelectedNodeIds.Count == 0)
        {
            return false;
        }

        List<(MGGraphNode Node, int? Left, int? Top, int? Right, int? Bottom)> selectedNodes = new();
        for (int childIndex = 0; childIndex < NodesCanvas.Children.Count; childIndex++)
        {
            if (NodesCanvas.Children[childIndex] is MGGraphNode node && SelectedNodeIds.Contains(node.NodeId))
            {
                selectedNodes.Add((node, MGCanvas.GetLeft(node), MGCanvas.GetTop(node), MGCanvas.GetRight(node), MGCanvas.GetBottom(node)));
            }
        }

        if (selectedNodes.Count == 0)
        {
            return false;
        }

        int startIndex = NodesCanvas.Children.Count - selectedNodes.Count;
        bool alreadyInFront = startIndex >= 0;
        if (alreadyInFront)
        {
            for (int selectedIndex = 0; selectedIndex < selectedNodes.Count; selectedIndex++)
            {
                if (!ReferenceEquals(NodesCanvas.Children[startIndex + selectedIndex], selectedNodes[selectedIndex].Node))
                {
                    alreadyInFront = false;
                    break;
                }
            }
        }

        if (alreadyInFront)
        {
            return false;
        }

        using (NodesCanvas.AllowChangingContentTemporarily())
        {
            for (int selectedIndex = 0; selectedIndex < selectedNodes.Count; selectedIndex++)
            {
                NodesCanvas.TryRemoveChild(selectedNodes[selectedIndex].Node);
            }

            for (int selectedIndex = 0; selectedIndex < selectedNodes.Count; selectedIndex++)
            {
                (MGGraphNode node, int? left, int? top, int? right, int? bottom) = selectedNodes[selectedIndex];
                NodesCanvas.TryAddChild(node, left, top, right, bottom);
            }
        }

        return true;
    }

    private bool TryGetNodeAtViewportPoint(Vector2 viewportPoint, out MGGraphNode node)
    {
        node = null;
        if (NodesCanvas == null)
        {
            return false;
        }

        for (int childIndex = NodesCanvas.Children.Count - 1; childIndex >= 0; childIndex--)
        {
            if (NodesCanvas.Children[childIndex] is MGGraphNode graphNode)
            {
                Rectangle bounds = GetNodeViewportBounds(graphNode);
                if (bounds.Contains((int)MathF.Round(viewportPoint.X), (int)MathF.Round(viewportPoint.Y)))
                {
                    node = graphNode;
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryGetNodeAtScreenPosition(Point screenPosition, out MGGraphNode node)
    {
        MGElement topmostVisual = GetTopmostGraphVisualAtScreenPosition(screenPosition);
        if (topmostVisual is MGGraphNode graphNode)
        {
            node = graphNode;
            return true;
        }

        return TryGetNodeAtViewportPoint(GetViewportPoint(screenPosition), out node);
    }

    private bool TryGetCommentAtViewportPoint(Vector2 viewportPoint, out MGGraphCommentBox commentBox)
    {
        commentBox = null;
        if (NodesCanvas == null)
        {
            return false;
        }

        for (int childIndex = NodesCanvas.Children.Count - 1; childIndex >= 0; childIndex--)
        {
            if (NodesCanvas.Children[childIndex] is MGGraphCommentBox candidate)
            {
                Rectangle bounds = GetCommentViewportBounds(candidate);
                if (bounds.Contains((int)MathF.Round(viewportPoint.X), (int)MathF.Round(viewportPoint.Y)))
                {
                    commentBox = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryGetCommentAtScreenPosition(Point screenPosition, out MGGraphCommentBox commentBox)
    {
        MGElement topmostVisual = GetTopmostGraphVisualAtScreenPosition(screenPosition);
        if (topmostVisual is MGGraphCommentBox graphCommentBox)
        {
            commentBox = graphCommentBox;
            return true;
        }

        return TryGetCommentAtViewportPoint(GetViewportPoint(screenPosition), out commentBox);
    }

    private bool TryGetPortAtViewportPoint(Vector2 viewportPoint, out MGGraphPort port)
    {
        port = null;
        if (NodesCanvas == null)
        {
            return false;
        }

        for (int nodeIndex = NodesCanvas.Children.Count - 1; nodeIndex >= 0; nodeIndex--)
        {
            if (NodesCanvas.Children[nodeIndex] is MGGraphCommentBox commentBox)
            {
                Rectangle commentBounds = GetCommentViewportBounds(commentBox);
                if (commentBounds.Contains((int)MathF.Round(viewportPoint.X), (int)MathF.Round(viewportPoint.Y)))
                {
                    return false;
                }

                continue;
            }

            if (NodesCanvas.Children[nodeIndex] is not MGGraphNode graphNode)
            {
                continue;
            }

            Rectangle nodeBounds = GetNodeViewportBounds(graphNode);
            if (!nodeBounds.Contains((int)MathF.Round(viewportPoint.X), (int)MathF.Round(viewportPoint.Y)))
            {
                continue;
            }

            if (graphNode.PortsPanel == null)
            {
                return false;
            }

            for (int portIndex = graphNode.PortsPanel.Children.Count - 1; portIndex >= 0; portIndex--)
            {
                if (graphNode.PortsPanel.Children[portIndex] is MGGraphPort candidate)
                {
                    Rectangle bounds = candidate.ActualLayoutBounds.Width > 0 || candidate.ActualLayoutBounds.Height > 0
                        ? candidate.ActualLayoutBounds
                        : candidate.LayoutBounds;
                    if (bounds.Width <= 0 || bounds.Height <= 0)
                    {
                        Vector2 anchor = candidate.GetLayoutAnchor();
                        bounds = new Rectangle((int)anchor.X - 8, (int)anchor.Y - 8, 16, 16);
                    }

                    if (bounds.Contains((int)MathF.Round(viewportPoint.X), (int)MathF.Round(viewportPoint.Y)))
                    {
                        port = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        return false;
    }

    private bool TryGetPortAtScreenPosition(Point screenPosition, out MGGraphPort port)
    {
        MGElement topmostVisual = GetTopmostGraphVisualAtScreenPosition(screenPosition);
        if (topmostVisual is MGGraphPort graphPort)
        {
            port = graphPort;
            return true;
        }

        return TryGetPortAtViewportPoint(GetViewportPoint(screenPosition), out port);
    }

    private MGElement GetTopmostGraphVisualAtScreenPosition(Point screenPosition)
    {
        if (NodesCanvas == null || !IsPointerInsideViewport(screenPosition))
        {
            return null;
        }

        MGElement current = SelfOrParentWindow?.HoveredElement;
        while (current != null)
        {
            if (current == NodesCanvas)
            {
                return current;
            }

            if (current is MGGraphPort or MGGraphNode or MGGraphCommentBox)
            {
                return NodesCanvas.IsSelfOrAncestorOf(current) ? current : null;
            }

            current = current.Parent;
        }

        return null;
    }

    private bool TryGetEdgeAtViewportPoint(Vector2 viewportPoint, out GraphEdgeModel edge)
    {
        edge = null;
        if (Document == null)
        {
            return false;
        }

        int segmentCount = ViewportTransform.Zoom < 0.35f ? 8 : GraphBezierGeometry.DefaultSegmentCount;
        float tolerance = Math.Max(6.0f, EdgeThickness * 2.0f);
        for (int edgeIndex = Document.Edges.Count - 1; edgeIndex >= 0; edgeIndex--)
        {
            GraphEdgeModel candidate = Document.Edges[edgeIndex];
            if (candidate == null)
            {
                continue;
            }

            if (!TryGetPortLayoutAnchorForInteraction(candidate.SourcePortId, out Vector2 start)
                || !TryGetPortLayoutAnchorForInteraction(candidate.TargetPortId, out Vector2 end))
            {
                continue;
            }

            IReadOnlyList<Vector2> points = EdgeGeometryCache.GetOrCreate(candidate.Id, start, end, EdgeThickness, ViewportTransform.Zoom, segmentCount);
            if (_HitTestService.HitTestEdge(points, viewportPoint, tolerance))
            {
                edge = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryGetPortLayoutAnchorForInteraction(Guid portId, out Vector2 layoutAnchor)
    {
        if (TryGetPortControl(portId, out MGGraphPort port) && IsRuntimePortAnchorUsable(port))
        {
            layoutAnchor = port.GetLayoutAnchor();
            return true;
        }

        if (GraphPortAnchorResolver.TryGetPortLayoutAnchor(Document, ViewportTransform, portId, out Vector2 viewportAnchor))
        {
            layoutAnchor = viewportAnchor + GetViewportLayoutOrigin();
            return true;
        }

        layoutAnchor = default;
        return false;
    }

    private bool IsRuntimePortAnchorUsable(MGGraphPort port)
    {
        if (port == null || port.Visibility != Visibility.Visible || !HasVisibleActualBounds(port))
        {
            return false;
        }

        if (port.Model == null || port.Model.NodeId == Guid.Empty)
        {
            return true;
        }

        return TryGetNodeControl(port.Model.NodeId, out MGGraphNode node) && node.Visibility == Visibility.Visible && HasVisibleActualBounds(node);
    }

    private static bool HasVisibleActualBounds(MGElement element)
        => element != null && element.ActualLayoutBounds.Width > 0 && element.ActualLayoutBounds.Height > 0;

    private Vector2 GetViewportLayoutOrigin()
    {
        Rectangle bounds = NodesCanvas?.AlignedContentBounds ?? NodesCanvas?.LayoutBounds ?? Rectangle.Empty;
        return new Vector2(bounds.Left, bounds.Top);
    }

    private Rectangle GetNodeViewportBounds(MGGraphNode node)
    {
        Rectangle bounds = node.ActualLayoutBounds.Width > 0 || node.ActualLayoutBounds.Height > 0 ? node.ActualLayoutBounds : node.LayoutBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            GraphNodeModel model = Document.TryGetNode(node.NodeId);
            RectangleF worldBounds = GraphSelectionManager.GetNodeWorldBounds(model);
            Vector2 topLeft = ViewportTransform.WorldToViewport(new Vector2(worldBounds.Left, worldBounds.Top));
            Vector2 bottomRight = ViewportTransform.WorldToViewport(new Vector2(worldBounds.Right, worldBounds.Bottom));
            RectangleF viewportBounds = CreateRectangle(topLeft, bottomRight);
            return new Rectangle((int)MathF.Floor(viewportBounds.X), (int)MathF.Floor(viewportBounds.Y), (int)MathF.Ceiling(viewportBounds.Width), (int)MathF.Ceiling(viewportBounds.Height));
        }

        return bounds;
    }

    private Rectangle GetCommentViewportBounds(MGGraphCommentBox commentBox)
    {
        Rectangle bounds = commentBox.ActualLayoutBounds.Width > 0 || commentBox.ActualLayoutBounds.Height > 0 ? commentBox.ActualLayoutBounds : commentBox.LayoutBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            GraphCommentModel model = Document.TryGetComment(commentBox.CommentId);
            if (model == null)
            {
                return Rectangle.Empty;
            }

            Vector2 topLeft = ViewportTransform.WorldToViewport(new Vector2(model.Bounds.Left, model.Bounds.Top));
            Vector2 bottomRight = ViewportTransform.WorldToViewport(new Vector2(model.Bounds.Right, model.Bounds.Bottom));
            RectangleF viewportBounds = CreateRectangle(topLeft, bottomRight);
            return new Rectangle((int)MathF.Floor(viewportBounds.X), (int)MathF.Floor(viewportBounds.Y), (int)MathF.Ceiling(viewportBounds.Width), (int)MathF.Ceiling(viewportBounds.Height));
        }

        return bounds;
    }

    private bool IsCommentResizeHandleHit(MGGraphCommentBox commentBox, Vector2 viewportPoint)
    {
        Rectangle bounds = GetCommentViewportBounds(commentBox);
        const int handleSize = 12;
        Rectangle resizeHandle = new(bounds.Right - handleSize, bounds.Bottom - handleSize, handleSize, handleSize);
        return resizeHandle.Contains((int)MathF.Round(viewportPoint.X), (int)MathF.Round(viewportPoint.Y));
    }

    private bool IsPointerInsideViewport(Point screenPosition)
    {
        if (ViewportHost == null)
        {
            Vector2 graphUnscaled = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.UnscaledScreen, screenPosition.ToVector2());
            return ContainsUnscaledInputPoint(graphUnscaled);
        }

        Vector2 viewportUnscaled = ViewportHost.ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.UnscaledScreen, screenPosition.ToVector2());
        return ViewportHost.ContainsUnscaledInputPoint(viewportUnscaled);
    }

    private Vector2 GetViewportPoint(Point screenPosition)
        => NodesCanvas == null
            ? ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, screenPosition.ToVector2())
            : NodesCanvas.ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, screenPosition.ToVector2());

    private Vector2 GetViewportLocalPoint(Point screenPosition)
        => GetViewportPoint(screenPosition) - GetViewportLayoutOrigin();

    internal Vector2 GetWorldPointFromScreenPosition(Point screenPosition)
        => ViewportTransform.LayoutToWorld(GetViewportLocalPoint(screenPosition));

    private RectangleF ViewportRectangleToWorldRectangle(RectangleF viewportRectangle)
    {
        Vector2 worldStart = ViewportTransform.LayoutToWorld(new Vector2(viewportRectangle.Left, viewportRectangle.Top));
        Vector2 worldEnd = ViewportTransform.LayoutToWorld(new Vector2(viewportRectangle.Right, viewportRectangle.Bottom));
        return CreateRectangle(worldStart, worldEnd);
    }

    private bool IsControlDown()
    {
        KeyboardState keyboard = InputTracker.Keyboard.CurrentState;
        return keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
    }

    private static RectangleF CreateRectangle(Vector2 first, Vector2 second)
    {
        float left = Math.Min(first.X, second.X);
        float top = Math.Min(first.Y, second.Y);
        float right = Math.Max(first.X, second.X);
        float bottom = Math.Max(first.Y, second.Y);
        return new RectangleF(left, top, right - left, bottom - top);
    }

    private static Rectangle OffsetRectangle(Rectangle rectangle, Vector2 delta)
        => new(rectangle.X + (int)MathF.Round(delta.X), rectangle.Y + (int)MathF.Round(delta.Y), rectangle.Width, rectangle.Height);

    private static Rectangle NormalizeCommentBounds(Rectangle bounds)
        => new(bounds.X, bounds.Y, Math.Max(80, bounds.Width), Math.Max(48, bounds.Height));

    private static RectangleF ToRectangleF(Rectangle rectangle)
        => new(rectangle.X, rectangle.Y, Math.Max(1, rectangle.Width), Math.Max(1, rectangle.Height));

    private static bool Intersects(RectangleF first, RectangleF second)
        => first.Left < second.Right && first.Right > second.Left && first.Top < second.Bottom && first.Bottom > second.Top;

    private Rectangle GetViewportBoundsForFraming()
    {
        Rectangle bounds = NodesCanvas?.LayoutBounds ?? ViewportHost?.LayoutBounds ?? LayoutBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            bounds = NodesCanvas?.ActualLayoutBounds ?? ViewportHost?.ActualLayoutBounds ?? ActualLayoutBounds;
        }

        int width = Math.Max(1, bounds.Width);
        int height = Math.Max(1, bounds.Height);
        return new Rectangle(0, 0, width, height);
    }
}

public class MGGraphNode : MGSingleContentHost
{
    public const string OuterBorderPartName = "PART_OuterBorder";
    public const string HeaderTextBlockPartName = "PART_HeaderTextBlock";
    public const string PortsPanelPartName = "PART_PortsPanel";
    public const string BodyPresenterPartName = "PART_BodyPresenter";

    protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
    {
        yield return new(OuterBorderPartName, typeof(MGBorder));
        yield return new(HeaderTextBlockPartName, typeof(MGTextBlock));
        yield return new(PortsPanelPartName, typeof(MGGrid));
        yield return new(BodyPresenterPartName, typeof(MGContentPresenter));
    }

    private string _Title = string.Empty;
    private Guid _NodeId;
    private bool _HasError;
    private bool _HasWarning;
    private bool _IsCollapsed;
    private bool _HasCapturedZoomMetrics;
    private int _BaseHeaderFontSize;
    private Thickness _BaseHeaderPadding;
    private Thickness _BasePortsPadding;
    private Thickness _BaseBodyPadding;

    public MGBorder OuterBorder { get; private set; }
    public MGTextBlock HeaderTextBlock { get; private set; }
    public MGGrid PortsPanel { get; private set; }
    public MGContentPresenter BodyPresenter { get; private set; }
    public GraphNodeModel Model { get; }

    public Guid NodeId
    {
        get => _NodeId;
        set
        {
            if (_NodeId != value)
            {
                _NodeId = value;
                NPC(nameof(NodeId));
            }
        }
    }

    public string Title
    {
        get => _Title;
        set
        {
            string next = value ?? string.Empty;
            if (_Title != next)
            {
                _Title = next;
                if (HeaderTextBlock != null)
                {
                    HeaderTextBlock.Text = _Title;
                }

                NPC(nameof(Title));
            }
        }
    }

    public bool HasError
    {
        get => _HasError;
        set
        {
            if (_HasError != value)
            {
                _HasError = value;
                NPC(nameof(HasError));
            }
        }
    }

    public bool HasWarning
    {
        get => _HasWarning;
        set
        {
            if (_HasWarning != value)
            {
                _HasWarning = value;
                NPC(nameof(HasWarning));
            }
        }
    }

    public bool IsCollapsed
    {
        get => _IsCollapsed;
        set
        {
            if (_IsCollapsed != value)
            {
                _IsCollapsed = value;
                UpdateCollapsedVisualState();
                LayoutChanged(this, true);
                NPC(nameof(IsCollapsed));
            }
        }
    }

    public MGGraphNode(MGWindow window)
        : this(window, null) { }

    public MGGraphNode(MGWindow window, GraphNodeModel model)
        : base(window, MGElementType.GraphNode)
    {
        using (BeginInitializing())
        {
            Model = model;
            _NodeId = model?.Id ?? Guid.Empty;
            _Title = model?.Title ?? string.Empty;
            _IsCollapsed = model?.IsCollapsed ?? false;
            DefaultControlTemplateName = MGControlTemplateCatalog.GraphNodeTemplateName;
        }
    }

    protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure structure)
    {
        OuterBorder = structure.Parts[OuterBorderPartName] as MGBorder;
        HeaderTextBlock = structure.Parts[HeaderTextBlockPartName] as MGTextBlock;
        PortsPanel = structure.Parts[PortsPanelPartName] as MGGrid;
        BodyPresenter = structure.Parts[BodyPresenterPartName] as MGContentPresenter;
        HeaderTextBlock.Text = Title;
        UpdateCollapsedVisualState();
        ApplySelectionVisual();

        if (Content != null)
        {
            using (BodyPresenter.AllowChangingContentTemporarily())
            {
                BodyPresenter.SetContent(Content);
            }
        }

        using (AllowChangingContentTemporarily())
        {
            SetContent(OuterBorder);
        }
    }

    protected override void SetContentVirtual(MGElement value)
    {
        if (BodyPresenter != null && !ReferenceEquals(value, OuterBorder))
        {
            using (BodyPresenter.AllowChangingContentTemporarily())
            {
                BodyPresenter.SetContent(value);
            }
            return;
        }

        base.SetContentVirtual(value);
    }

    internal void ApplyZoomScale(float zoom)
    {
        if (HeaderTextBlock == null || PortsPanel == null || BodyPresenter == null)
        {
            return;
        }

        if (!_HasCapturedZoomMetrics)
        {
            _BaseHeaderFontSize = Math.Max(1, HeaderTextBlock.FontSize);
            _BaseHeaderPadding = HeaderTextBlock.Padding;
            _BasePortsPadding = PortsPanel.Padding;
            _BaseBodyPadding = BodyPresenter.Padding;
            _HasCapturedZoomMetrics = true;
        }

        float clampedZoom = Math.Max(0.1f, zoom);
        _ = HeaderTextBlock.TrySetFont(HeaderTextBlock.FontFamily, Math.Max(1, UIResponsiveMath.ScaleInt(_BaseHeaderFontSize, clampedZoom)));
        HeaderTextBlock.SetPadding(UIResponsiveMath.ScaleThickness(_BaseHeaderPadding, clampedZoom), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        PortsPanel.SetPadding(UIResponsiveMath.ScaleThickness(_BasePortsPadding, clampedZoom), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        BodyPresenter.SetPadding(UIResponsiveMath.ScaleThickness(_BaseBodyPadding, clampedZoom), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
    }

    /// <summary><see cref="OnThemeChanged"/> re-applies the selection visual, whose border thickness is the layout-affecting
    /// <see cref="MGThemeGraphSettings.NodeBorderThickness"/>, or <see cref="MGThemeGraphSettings.NodeSelectedBorderThickness"/> while selected:
    /// request a layout pass only when that thickness changes (backlog task 7).</summary>
    protected internal override UIInvalidationKind GetThemeInvalidation(MGTheme PreviousTheme, MGTheme CurrentTheme)
    {
        if (OuterBorder == null)
        {
            return UIInvalidationKind.Draw;
        }

        if (PreviousTheme == null || CurrentTheme == null)
        {
            return UIInvalidationKind.Draw | UIThemeValueInvalidation.LayoutAffecting;
        }

        return IsSelected
            ? UIThemeValueInvalidation.ForChange("Graph.NodeSelectedBorderThickness", PreviousTheme.Graph.NodeSelectedBorderThickness, CurrentTheme.Graph.NodeSelectedBorderThickness)
            : UIThemeValueInvalidation.ForChange("Graph.NodeBorderThickness", PreviousTheme.Graph.NodeBorderThickness, CurrentTheme.Graph.NodeBorderThickness);
    }

    protected internal override void OnThemeChanged(MGTheme PreviousTheme, MGTheme CurrentTheme)
    {
        base.OnThemeChanged(PreviousTheme, CurrentTheme);
        _HasCapturedZoomMetrics = false;
        ApplySelectionVisual();
    }

    internal void ApplySelectionVisual()
    {
        if (OuterBorder == null)
        {
            return;
        }

        MGTheme theme = GetTheme();
        // ADR-0005: internal selection state, VisualState(70) -- correctly outranks the template catalogue's
        // GraphNode.BorderBrush/BorderThickness (Template(60), migrated in S3), so the selection highlight
        // wins over the template default while a plain application LocalValue(90) still wins over selection.
        OuterBorder.SetBorderBrush((IsSelected ? theme.Graph.NodeSelectedBorderBrush : theme.Graph.NodeBorderBrush)?.Copy(), UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));
        OuterBorder.SetBorderThickness(IsSelected ? theme.Graph.NodeSelectedBorderThickness : theme.Graph.NodeBorderThickness, UIValueResolutionSource.VisualState(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
    }

    private void UpdateCollapsedVisualState()
    {
        if (PortsPanel != null)
        {
            PortsPanel.Visibility = IsCollapsed ? Visibility.Collapsed : Visibility.Visible;
        }

        if (BodyPresenter != null)
        {
            BodyPresenter.Visibility = IsCollapsed ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}

public class MGGraphPort : MGSingleContentHost
{
    internal const int ConnectorIconSize = 10;
    internal const int ConnectorSlotWidth = 18;

    public const string OuterBorderPartName = "PART_OuterBorder";
    public const string LeadingIconPresenterPartName = "PART_LeadingIconPresenter";
    public const string TrailingIconPresenterPartName = "PART_TrailingIconPresenter";
    public const string LabelPartName = "PART_Label";

    protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
    {
        yield return new(OuterBorderPartName, typeof(MGBorder));
        yield return new(LeadingIconPresenterPartName, typeof(MGContentPresenter));
        yield return new(TrailingIconPresenterPartName, typeof(MGContentPresenter));
        yield return new(LabelPartName, typeof(MGTextBlock));
    }

    private string _PortName = string.Empty;
    private Guid _PortId;
    private GraphPortDirection _Direction;
    private GraphValueType _ValueType;
    private bool _IsConnected;
    private bool _IsRequired;
    private bool _IsConnectionDragSource;
    private bool _IsConnectionDragTarget;
    private bool _IsConnectionCompatible;
    private bool _HasCapturedZoomMetrics;
    private int _BaseLabelFontSize;
    private Thickness _BaseOuterPadding;
    private int _CurrentConnectorIconSize = ConnectorIconSize;
    private int _CurrentConnectorSlotWidth = ConnectorSlotWidth;

    public MGBorder OuterBorder { get; private set; }
    public MGContentPresenter LeadingIconPresenter { get; private set; }
    public MGContentPresenter TrailingIconPresenter { get; private set; }
    public MGTextBlock Label { get; private set; }
    public GraphPortModel Model { get; }

    public Guid PortId
    {
        get => _PortId;
        set
        {
            if (_PortId != value)
            {
                _PortId = value;
                NPC(nameof(PortId));
            }
        }
    }

    public GraphPortDirection Direction
    {
        get => _Direction;
        set
        {
            if (_Direction != value)
            {
                _Direction = value;
                RefreshConnectorVisual();
                NPC(nameof(Direction));
            }
        }
    }

    public GraphValueType ValueType
    {
        get => _ValueType;
        set
        {
            if (_ValueType != value)
            {
                _ValueType = value;
                RefreshConnectorVisual();
                NPC(nameof(ValueType));
            }
        }
    }

    public bool IsConnected
    {
        get => _IsConnected;
        set
        {
            if (_IsConnected != value)
            {
                _IsConnected = value;
                NPC(nameof(IsConnected));
            }
        }
    }

    public bool IsRequired
    {
        get => _IsRequired;
        set
        {
            if (_IsRequired != value)
            {
                _IsRequired = value;
                NPC(nameof(IsRequired));
            }
        }
    }

    public bool IsConnectionDragSource
    {
        get => _IsConnectionDragSource;
        set
        {
            if (_IsConnectionDragSource != value)
            {
                _IsConnectionDragSource = value;
                NPC(nameof(IsConnectionDragSource));
            }
        }
    }

    public bool IsConnectionDragTarget
    {
        get => _IsConnectionDragTarget;
        set
        {
            if (_IsConnectionDragTarget != value)
            {
                _IsConnectionDragTarget = value;
                NPC(nameof(IsConnectionDragTarget));
            }
        }
    }

    public bool IsConnectionCompatible
    {
        get => _IsConnectionCompatible;
        set
        {
            if (_IsConnectionCompatible != value)
            {
                _IsConnectionCompatible = value;
                NPC(nameof(IsConnectionCompatible));
            }
        }
    }

    public string PortName
    {
        get => _PortName;
        set
        {
            string next = value ?? string.Empty;
            if (_PortName != next)
            {
                _PortName = next;
                if (Label != null)
                {
                    Label.Text = _PortName;
                }

                NPC(nameof(PortName));
            }
        }
    }

    public MGGraphPort(MGWindow window)
        : this(window, null) { }

    public MGGraphPort(MGWindow window, GraphPortModel model)
        : base(window, MGElementType.GraphPort)
    {
        using (BeginInitializing())
        {
            Model = model;
            _PortId = model?.Id ?? Guid.Empty;
            _PortName = model?.Name ?? string.Empty;
            _Direction = model?.Direction ?? GraphPortDirection.Input;
            _ValueType = model?.ValueType ?? GraphValueType.Wildcard;
            _IsRequired = model?.IsRequired ?? false;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Top;
            DefaultControlTemplateName = MGControlTemplateCatalog.GraphPortTemplateName;
        }
    }

    protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure structure)
    {
        OuterBorder = structure.Parts[OuterBorderPartName] as MGBorder;
        LeadingIconPresenter = structure.Parts[LeadingIconPresenterPartName] as MGContentPresenter;
        TrailingIconPresenter = structure.Parts[TrailingIconPresenterPartName] as MGContentPresenter;
        Label = structure.Parts[LabelPartName] as MGTextBlock;
        Label.Text = PortName;
        RefreshConnectorVisual();
        UpdateConnectorLayoutMetrics();

        using (AllowChangingContentTemporarily())
        {
            SetContent(OuterBorder);
        }
    }

    internal void ApplyZoomScale(float zoom)
    {
        if (OuterBorder == null || Label == null)
        {
            return;
        }

        if (!_HasCapturedZoomMetrics)
        {
            _BaseLabelFontSize = Math.Max(1, Label.FontSize);
            _BaseOuterPadding = OuterBorder.Padding;
            _HasCapturedZoomMetrics = true;
        }

        float clampedZoom = Math.Max(0.1f, zoom);
        _ = Label.TrySetFont(Label.FontFamily, Math.Max(1, UIResponsiveMath.ScaleInt(_BaseLabelFontSize, clampedZoom)));
        OuterBorder.SetPadding(UIResponsiveMath.ScaleThickness(_BaseOuterPadding, clampedZoom), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        _CurrentConnectorIconSize = Math.Max(1, UIResponsiveMath.ScaleInt(ConnectorIconSize, clampedZoom));
        _CurrentConnectorSlotWidth = Math.Max(_CurrentConnectorIconSize, UIResponsiveMath.ScaleInt(ConnectorSlotWidth, clampedZoom));
        UpdateConnectorLayoutMetrics();
    }

    protected internal override void OnThemeChanged(MGTheme PreviousTheme, MGTheme CurrentTheme)
    {
        base.OnThemeChanged(PreviousTheme, CurrentTheme);
        _HasCapturedZoomMetrics = false;
        RefreshConnectorVisual();
    }

    private void RefreshConnectorVisual()
    {
        if (LeadingIconPresenter == null || TrailingIconPresenter == null || Label == null)
        {
            return;
        }

        bool showLabel = ValueType != GraphValueType.Exec;
        bool useLeadingIcon = Direction == GraphPortDirection.Input;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        if (OuterBorder != null)
        {
            OuterBorder.HorizontalContentAlignment = useLeadingIcon ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            OuterBorder.VerticalContentAlignment = VerticalAlignment.Center;
        }

        Label.Text = PortName;
        Label.Visibility = showLabel ? Visibility.Visible : Visibility.Collapsed;
        Label.TextAlignment = useLeadingIcon ? HorizontalAlignment.Left : HorizontalAlignment.Right;

        SetIconContent(LeadingIconPresenter, useLeadingIcon ? CreateConnectorIcon() : null);
        SetIconContent(TrailingIconPresenter, useLeadingIcon ? null : CreateConnectorIcon());
        UpdateConnectorLayoutMetrics();
    }

    private void UpdateConnectorLayoutMetrics()
    {
        UpdateConnectorPresenterLayout(LeadingIconPresenter);
        UpdateConnectorPresenterLayout(TrailingIconPresenter);

        if (OuterBorder?.Content is MGGrid layout && layout.Columns.Count >= 3)
        {
            layout.Columns[0].Length = GridLength.CreatePixelLength(_CurrentConnectorSlotWidth);
            layout.Columns[2].Length = GridLength.CreatePixelLength(_CurrentConnectorSlotWidth);
        }
    }

    private void UpdateConnectorPresenterLayout(MGContentPresenter presenter)
    {
        if (presenter == null)
        {
            return;
        }

        presenter.PreferredWidth = _CurrentConnectorSlotWidth;
        presenter.PreferredHeight = _CurrentConnectorIconSize;

        switch (presenter.Content)
        {
            case MGRectangle rectangle:
                rectangle.Width = _CurrentConnectorIconSize;
                rectangle.Height = _CurrentConnectorIconSize;
                rectangle.CornerRadius = new MGCornerRadius(_CurrentConnectorIconSize / 2);
                break;
            case MGTriangleArrowIcon triangle:
                triangle.PreferredWidth = _CurrentConnectorIconSize;
                triangle.PreferredHeight = _CurrentConnectorIconSize;
                break;
        }
    }

    private void SetIconContent(MGContentPresenter presenter, MGElement content)
    {
        if (presenter == null)
        {
            return;
        }

        presenter.Visibility = content == null ? Visibility.Collapsed : Visibility.Visible;
        using (presenter.AllowChangingContentTemporarily())
        {
            presenter.SetContent(content);
        }
    }

    private MGElement CreateConnectorIcon()
    {
        MGTheme theme = GetTheme();
        Color foreground = theme?.Graph?.PortForeground?.NormalValue ?? Color.White;
        if (ValueType == GraphValueType.Exec)
        {
            return new MGTriangleArrowIcon(SelfOrParentWindow)
            {
                Color = foreground,
                Direction = UITriangleArrowDirection.Right,
                PreferredWidth = ConnectorIconSize,
                PreferredHeight = ConnectorIconSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        MGRectangle icon = new(SelfOrParentWindow, ConnectorIconSize, ConnectorIconSize, foreground, 1, theme?.Graph?.PortBackground?.Copy() ?? foreground.AsFillBrush())
        {
            CornerRadius = new MGCornerRadius(ConnectorIconSize / 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        return icon;
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        base.DrawSelf(DA, layoutBounds);

        if (!TryGetPresenterConnectorContentLayoutBounds(out _) && TryGetConnectorIconLayoutBounds(out Rectangle connectorBounds))
        {
            DrawFallbackConnectorIcon(DA, connectorBounds);
        }
    }

    private void DrawFallbackConnectorIcon(ElementDrawArgs DA, Rectangle connectorBounds)
    {
        MGTheme theme = GetTheme();
        Color foreground = (theme?.Graph?.PortForeground?.NormalValue ?? Color.White) * DA.Opacity;
        if (ValueType == GraphValueType.Exec)
        {
            UISymbolDrawing.DrawFilledTriangleArrow(DA.DT, DA.Offset.ToVector2(), connectorBounds, UITriangleArrowDirection.Right, foreground);
            return;
        }

        Color fill = theme?.Graph?.PortBackground is MGSolidFillBrush solid ? solid.Color * DA.Opacity : foreground;
        DA.DT.StrokeAndFillCircle(
            connectorBounds.Center.ToVector2(),
            foreground,
            fill,
            Math.Max(1.0f, connectorBounds.Width * 0.5f),
            1.0f,
            16);
    }

    public Vector2 GetLayoutAnchor()
    {
        if (TryGetConnectorIconLayoutBounds(out Rectangle connectorBounds))
        {
            float connectorX = Direction == GraphPortDirection.Input ? connectorBounds.Left : connectorBounds.Right;
            return new Vector2(connectorX, connectorBounds.Top + connectorBounds.Height * 0.5f);
        }

        Rectangle bounds = GetAnchorBounds(this);
        float anchorX = Direction == GraphPortDirection.Input ? bounds.Left : bounds.Right;
        return new Vector2(anchorX, bounds.Top + bounds.Height * 0.5f);
    }

    private bool TryGetConnectorIconLayoutBounds(out Rectangle bounds)
    {
        if (TryGetPresenterConnectorContentLayoutBounds(out bounds))
        {
            return true;
        }

        if (TryGetConnectorSlotLayoutBounds(out Rectangle slotBounds))
        {
            return TryCreateCenteredConnectorLayoutBounds(slotBounds, out bounds);
        }

        bounds = Rectangle.Empty;
        return false;
    }

    private bool TryGetPresenterConnectorContentLayoutBounds(out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        MGContentPresenter presenter = Direction == GraphPortDirection.Input ? LeadingIconPresenter : TrailingIconPresenter;
        if (presenter == null || presenter.Visibility != Visibility.Visible || presenter.Content == null || presenter.Content.Visibility != Visibility.Visible)
        {
            return false;
        }

        bounds = GetAnchorBounds(presenter.Content);
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            return true;
        }

        if (TryGetConnectorSlotLayoutBounds(presenter, out Rectangle slotBounds))
        {
            return TryCreateCenteredConnectorLayoutBounds(slotBounds, out bounds);
        }

        bounds = Rectangle.Empty;
        return false;
    }

    private bool TryGetConnectorSlotLayoutBounds(out Rectangle bounds)
    {
        MGContentPresenter presenter = Direction == GraphPortDirection.Input ? LeadingIconPresenter : TrailingIconPresenter;
        if (TryGetConnectorSlotLayoutBounds(presenter, out bounds))
        {
            return true;
        }

        Rectangle portBounds = GetAnchorBounds(this);
        return TryGetFallbackConnectorSlotLayoutBounds(portBounds, out bounds);
    }

    private static bool TryGetConnectorSlotLayoutBounds(MGContentPresenter presenter, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (presenter == null || presenter.Visibility != Visibility.Visible)
        {
            return false;
        }

        bounds = GetAnchorBounds(presenter);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static Rectangle GetAnchorBounds(MGElement element)
    {
        if (element == null)
        {
            return Rectangle.Empty;
        }

        return element.LayoutBounds.Width > 0 || element.LayoutBounds.Height > 0
            ? element.LayoutBounds
            : element.ActualLayoutBounds;
    }

    private bool TryGetFallbackConnectorSlotLayoutBounds(Rectangle portBounds, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (portBounds.Width <= 0 || portBounds.Height <= 0)
        {
            return false;
        }

        int width = Math.Max(1, Math.Min(ConnectorSlotWidth, portBounds.Width));
        width = Math.Max(1, Math.Min(_CurrentConnectorSlotWidth, portBounds.Width));
        int left = Direction == GraphPortDirection.Input ? portBounds.Left : portBounds.Right - width;
        bounds = new Rectangle(left, portBounds.Top, width, portBounds.Height);
        return true;
    }

    private bool TryCreateCenteredConnectorLayoutBounds(Rectangle slotBounds, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (slotBounds.Width <= 0 || slotBounds.Height <= 0)
        {
            return false;
        }

        int size = Math.Max(1, Math.Min(_CurrentConnectorIconSize, Math.Min(slotBounds.Width, slotBounds.Height)));
        int left = slotBounds.Left + (slotBounds.Width - size) / 2;
        int top = slotBounds.Top + (slotBounds.Height - size) / 2;
        bounds = new Rectangle(left, top, size, size);
        return true;
    }

    public Vector2 GetWorldAnchor(GraphViewportTransform viewport)
        => viewport == null ? GetLayoutAnchor() : viewport.ViewportToWorld(GetLayoutAnchor());
}

public class MGGraphCommentBox : MGSingleContentHost
{
    public const string OuterBorderPartName = "PART_OuterBorder";
    public const string TitleTextBoxPartName = "PART_TitleTextBox";
    public const string BodyTextBoxPartName = "PART_BodyTextBox";

    protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
    {
        yield return new(OuterBorderPartName, typeof(MGBorder));
        yield return new(TitleTextBoxPartName, typeof(MGTextBox));
        yield return new(BodyTextBoxPartName, typeof(MGTextBox));
    }

    protected internal override void ValidateControlTemplateParts()
    {
        ValidateRequiredTemplatePart(OuterBorderPartName, typeof(MGBorder));
        ValidateRequiredTemplatePart(BodyTextBoxPartName, typeof(MGTextBox));
    }

    private void ValidateRequiredTemplatePart(string partName, Type partType)
    {
        string availableParts = TemplateParts.Any()
            ? string.Join(", ", TemplateParts.Select(x => $"{x.Key}:{x.Value?.GetType().Name ?? nameof(MGElement)}"))
            : "<none>";

        if (!TryGetTemplatePart(partName, out MGElement part))
        {
            throw new InvalidOperationException(
                $"Control template '{ControlTemplate?.Name ?? ResolveControlTemplateName() ?? "<unnamed>"}' for '{GetType().Name}' is missing required part '{partName}' of type '{partType.Name}'. Available parts: {availableParts}.");
        }

        if (part != null && !partType.IsAssignableFrom(part.GetType()))
        {
            throw new InvalidOperationException(
                $"Control template '{ControlTemplate?.Name ?? ResolveControlTemplateName() ?? "<unnamed>"}' for '{GetType().Name}' requires part '{partName}' to be assignable to '{partType.Name}', but got '{part.GetType().Name}'. Available parts: {availableParts}.");
        }
    }

    private string _Title = string.Empty;
    private string _Text = string.Empty;
    private Guid _CommentId;
    private bool _HasCapturedZoomMetrics;
    private int _BaseBodyFontSize;
    private Thickness _BaseOuterPadding;

    public MGBorder OuterBorder { get; private set; }
    public MGTextBox TitleTextBox { get; private set; }
    public MGTextBox BodyTextBox { get; private set; }
    public GraphCommentModel Model { get; }
    public bool IsEditing { get; private set; }

    public Guid CommentId
    {
        get => _CommentId;
        set
        {
            if (_CommentId != value)
            {
                _CommentId = value;
                NPC(nameof(CommentId));
            }
        }
    }

    public string Title
    {
        get => _Title;
        set
        {
            string next = value ?? string.Empty;
            if (_Title != next)
            {
                _Title = next;
                if (TitleTextBox != null && TitleTextBox.Text != _Title)
                {
                    TitleTextBox.SetText(_Title, SuppressLayoutChanged: true);
                }

                NPC(nameof(Title));
            }
        }
    }

    public string Text
    {
        get => _Text;
        set
        {
            string next = value ?? string.Empty;
            if (_Text != next)
            {
                _Text = next;
                if (!IsEditing && BodyTextBox != null && BodyTextBox.Text != _Text)
                {
                    BodyTextBox.SetText(_Text, SuppressLayoutChanged: true);
                }

                NPC(nameof(Text));
            }
        }
    }

    public MGGraphCommentBox(MGWindow window)
        : this(window, null) { }

    public MGGraphCommentBox(MGWindow window, GraphCommentModel model)
        : base(window, MGElementType.GraphCommentBox)
    {
        using (BeginInitializing())
        {
            Model = model;
            _CommentId = model?.Id ?? Guid.Empty;
            _Title = model?.Title ?? string.Empty;
            _Text = MGGraphView.GetCommentDescriptionText(_Title, model?.Text);
            DefaultControlTemplateName = MGControlTemplateCatalog.GraphCommentBoxTemplateName;
        }
    }

    protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure structure)
    {
        OuterBorder = structure.Parts[OuterBorderPartName] as MGBorder;
        TitleTextBox = structure.Parts[TitleTextBoxPartName] as MGTextBox;
        BodyTextBox = structure.Parts[BodyTextBoxPartName] as MGTextBox;
        ConfigureHiddenTitleTextBox(TitleTextBox);
        ConfigureInlineTextBox(BodyTextBox);
        SyncEditorText(TitleTextBox, Title);
        SyncEditorText(BodyTextBox, Text);
        ApplyEditingState();
        ApplySelectionVisual();

        using (AllowChangingContentTemporarily())
        {
            SetContent(OuterBorder);
        }
    }

    private static void ConfigureInlineTextBox(MGTextBox textBox)
    {
        if (textBox == null)
        {
            return;
        }

        textBox.AcceptsReturn = true;
        textBox.AcceptsTab = true;
        textBox.WrapText = true;
        textBox.MinLines = 1;
        textBox.MaxLines = null;
        textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        textBox.VerticalAlignment = VerticalAlignment.Stretch;
        textBox.SetBorderThicknessTagged(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        textBox.SetBackground(new VisualStateFillBrush(SolidFillBrushes.Transparent), UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
        textBox.SetBorderBrushTagged(MGUniformBorderBrush.Transparent, UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
        textBox.SetPadding(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        textBox.CornerRadius = MGCornerRadius.Zero;
        textBox.HasStableTextFootprint = true;
    }

    private static void ConfigureHiddenTitleTextBox(MGTextBox textBox)
    {
        if (textBox == null)
        {
            return;
        }

        textBox.Visibility = Visibility.Collapsed;
        textBox.IsReadonly = true;
        textBox.IsHitTestVisible = false;
        textBox.AllowsTextSelection = false;
        textBox.SetBorderThicknessTagged(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        textBox.SetBackground(new VisualStateFillBrush(SolidFillBrushes.Transparent), UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
        textBox.SetBorderBrushTagged(MGUniformBorderBrush.Transparent, UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
        textBox.SetPadding(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        textBox.SetMargin(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        textBox.MinLines = 1;
        textBox.MaxLines = 1;
        textBox.HasStableTextFootprint = true;
    }

    private static void SyncEditorText(MGTextBox textBox, string value)
    {
        if (textBox != null && textBox.Text != (value ?? string.Empty))
        {
            textBox.SetText(value ?? string.Empty, SuppressLayoutChanged: true);
        }
    }

    private void ApplyEditingState()
        => ApplyTextBoxEditingState(BodyTextBox);

    private void ApplyTextBoxEditingState(MGTextBox textBox)
    {
        if (textBox == null)
        {
            return;
        }

        textBox.IsReadonly = !IsEditing;
        textBox.IsHitTestVisible = IsEditing;
        textBox.AllowsTextSelection = IsEditing;
    }

    internal void BeginEdit()
    {
        SyncEditorText(BodyTextBox, Text);
        IsEditing = true;
        ApplyEditingState();
    }

    internal void EndEdit(bool commitChanges)
    {
        if (commitChanges)
        {
            Text = BodyTextBox?.Text ?? Text;
        }
        else
        {
            SyncEditorText(BodyTextBox, Text);
        }

        IsEditing = false;
        ApplyEditingState();
    }

    internal void ApplyZoomScale(float zoom)
    {
        if (OuterBorder == null || BodyTextBox == null)
        {
            return;
        }

        if (!_HasCapturedZoomMetrics)
        {
            _BaseBodyFontSize = Math.Max(1, BodyTextBox.FontSize);
            _BaseOuterPadding = OuterBorder.Padding;
            _HasCapturedZoomMetrics = true;
        }

        float clampedZoom = Math.Max(0.1f, zoom);
        BodyTextBox.TrySetFontSize(Math.Max(1, UIResponsiveMath.ScaleInt(_BaseBodyFontSize, clampedZoom)));
        OuterBorder.SetPadding(UIResponsiveMath.ScaleThickness(_BaseOuterPadding, clampedZoom), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
    }

    protected internal override void OnThemeChanged(MGTheme PreviousTheme, MGTheme CurrentTheme)
    {
        base.OnThemeChanged(PreviousTheme, CurrentTheme);
        _HasCapturedZoomMetrics = false;
    }

    internal int MeasureRequiredHeight(int availableWidth)
        => MeasureRequiredHeight(availableWidth, Text);

    internal int MeasureRequiredHeight(int availableWidth, string description)
    {
        if (OuterBorder == null)
        {
            return Math.Max(1, PreferredHeight ?? 1);
        }

        string nextText = description ?? string.Empty;
        string previousText = BodyTextBox?.Text ?? Text;
        bool restoreText = BodyTextBox != null && !string.Equals(previousText, nextText, StringComparison.Ordinal);

        if (restoreText)
        {
            BodyTextBox.SetText(nextText, SuppressLayoutChanged: true);
        }

        try
        {
            OuterBorder.UpdateMeasurement(new Size(Math.Max(1, availableWidth), 1000000), out _, out Thickness fullSize, out _, out _);
            return Math.Max(1, fullSize.Height);
        }
        finally
        {
            if (restoreText)
            {
                BodyTextBox.SetText(previousText, SuppressLayoutChanged: true);
            }
        }
    }

    internal void ApplySelectionVisual()
    {
        if (OuterBorder != null)
        {
            // ADR-0005: internal selection state, VisualState(70) -- outranks the template catalogue's
            // GraphCommentBox.BorderThickness (Template(60), migrated in S3).
            OuterBorder.SetBorderThickness(IsSelected ? new Thickness(2) : new Thickness(1), UIValueResolutionSource.VisualState(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        }
    }
}