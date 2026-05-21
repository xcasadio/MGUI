using System.Collections.Generic;
using System;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Graph;
using MGUI.Core.UI.Responsive;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.Core.UI
{
    public class MGGraphView : MGSingleContentHost
    {
        public const string OuterBorderPartName = "PART_OuterBorder";
        public const string ViewportHostPartName = "PART_ViewportHost";
        public const string NodesCanvasPartName = "PART_NodesCanvas";
        public const string OverlayPanelPartName = "PART_OverlayPanel";
        public const string SurfacePartName = NodesCanvasPartName;

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
        private IFillBrush _EdgeBrush = new MGSolidFillBrush(new Color(128, 180, 255));
        private float _EdgeThickness = 2.0f;
        private int _MajorGridLineFrequency = 4;
        private bool _IsPanningViewport;
        private Point _PanStartScreenPosition;
        private Vector2 _PanStartValue;
        private bool _IsDraggingNodes;
        private bool _IsDraggingComments;
        private bool _IsResizingComment;
        private bool _IsSelectingRectangle;
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
        public bool EnableViewportCulling { get; set; } = true;
        public float CullingPadding { get; set; } = 96.0f;
        public bool ShowGrid { get; set; } = true;
        public bool AllowZoom { get; set; } = true;
        public bool AllowPan { get; set; } = true;
        public bool SnapToGrid { get; set; }
        public float FramePadding { get; set; } = 32.0f;
        public bool IsSelectionRectangleActive => _IsSelectingRectangle;
        public RectangleF SelectionRectangleViewportBounds => CreateRectangle(_PointerPressViewportPoint, _CurrentSelectionViewportPoint);

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
                _Document = document ?? new GraphDocument();
                _Document.GraphChanged += OnDocumentGraphChanged;
                Selection = new GraphSelectionManager(SelectedNodeIds, SelectedEdgeIds);
                ConnectionController = new GraphConnectionController(this);
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
                    ConnectionController.UpdateDrag(viewportPoint, TryGetPortAtViewportPoint(viewportPoint, out MGGraphPort targetPort) ? targetPort.PortId : null);
                }
            };

            port.MouseHandler.DragEnd += (sender, e) =>
            {
                if (e.IsLMB && ConnectionController.IsDragging && ConnectionController.StartPortId == port.PortId)
                {
                    Vector2 viewportPoint = GetViewportPoint(e.EndPosition);
                    if (TryGetPortAtViewportPoint(viewportPoint, out MGGraphPort targetPort))
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
                if (TryGetPortAtViewportPoint(viewportPoint, out _))
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
                if (TryGetPortAtViewportPoint(viewportPoint, out _))
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
                if (e.IsLMB)
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

        public GraphCommentModel CreateCommentAt(Vector2 worldPosition, string title = "Comment", string text = "")
        {
            if (Document == null)
            {
                return null;
            }

            Vector2 position = SnapToGrid ? ViewportTransform.SnapPoint(worldPosition) : worldPosition;
            GraphCommentModel comment = new(Guid.NewGuid(), new Rectangle((int)MathF.Round(position.X), (int)MathF.Round(position.Y), 260, 120), title, text);
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

            GraphNodeModel node = definition.CreateNode(Guid.NewGuid(), SnapToGrid ? ViewportTransform.SnapPoint(worldPosition) : worldPosition);
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

        public MGContextMenu CreateNodeCreationMenu(Vector2 worldPosition, Guid? connectFromPortId = null)
        {
            MGContextMenu menu = new(ParentWindow, string.Empty);
            GraphPortModel draggedPort = connectFromPortId.HasValue ? Document?.TryGetPort(connectFromPortId.Value) : null;
            IEnumerable<GraphNodeDefinition> definitions = NodePalette?.GetDefinitions(draggedPort) ?? Array.Empty<GraphNodeDefinition>();
            string previousCategory = null;
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

                previousCategory = definition.Category;
                GraphNodeDefinition capturedDefinition = definition;
                string label = string.IsNullOrWhiteSpace(definition.Category)
                    ? definition.DisplayName
                    : $"{definition.Category} / {definition.DisplayName}";
                MGContextMenuButton button = menu.AddButton(label, _ => CreateNodeFromDefinition(capturedDefinition, worldPosition, connectFromPortId));
                button.CommandId = $"graph.createNode:{definition.NodeType}";
            }

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

        public void CancelCurrentInteraction()
        {
            _IsPanningViewport = false;
            _IsDraggingNodes = false;
            _IsDraggingComments = false;
            _IsResizingComment = false;
            _IsSelectingRectangle = false;
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
            if (TryGetNodeAtViewportPoint(viewportPoint, out _) || TryGetPortAtViewportPoint(viewportPoint, out _))
            {
                e.Menu = null;
                return;
            }

            Vector2 worldPosition = ViewportTransform.LayoutToWorld(viewportPoint);
            MGContextMenu menu = CreateNodeCreationMenu(worldPosition);
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

            Vector2 worldPosition = ViewportTransform.LayoutToWorld(GetViewportPoint(screenPosition));
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
                        _IsSelectingRectangle = true;
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

                else if (_IsSelectingRectangle && e.IsLMB)
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

                Focus(KeyboardFocusSource.Pointer);
                _PointerPressViewportPoint = GetViewportPoint(e.Position);
                _CurrentSelectionViewportPoint = _PointerPressViewportPoint;
                bool controlDown = IsControlDown();
                if (TryGetNodeAtViewportPoint(_PointerPressViewportPoint, out MGGraphNode node))
                {
                    _PressedNodeId = node.NodeId;
                    _PressedCommentId = Guid.Empty;
                    _PressedCommentResizeHandle = false;
                    SelectNode(node.NodeId, additive: controlDown, toggle: controlDown);
                    e.SetHandledBy(this, false);
                }
                else if (TryGetCommentAtViewportPoint(_PointerPressViewportPoint, out MGGraphCommentBox commentBox))
                {
                    _PressedNodeId = Guid.Empty;
                    _PressedCommentId = commentBox.CommentId;
                    _PressedCommentResizeHandle = IsCommentResizeHandleHit(commentBox, _PointerPressViewportPoint);
                    SelectComment(commentBox.CommentId, additive: controlDown, toggle: controlDown);
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
            else if (_IsSelectingRectangle)
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
            _IsSelectingRectangle = false;
            _NodeDragStartPositions.Clear();
            _CommentDragStartBounds.Clear();
        }

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

        private bool TryGetPortAtViewportPoint(Vector2 viewportPoint, out MGGraphPort port)
        {
            port = null;
            if (NodesCanvas == null)
            {
                return false;
            }

            for (int nodeIndex = NodesCanvas.Children.Count - 1; nodeIndex >= 0; nodeIndex--)
            {
                if (NodesCanvas.Children[nodeIndex] is not MGGraphNode graphNode || graphNode.PortsPanel == null)
                {
                    continue;
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
            }

            return false;
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
            yield return new(PortsPanelPartName, typeof(MGStackPanel));
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
        private int _BasePortsSpacing;
        private Thickness _BaseBodyPadding;

        public MGBorder OuterBorder { get; private set; }
        public MGTextBlock HeaderTextBlock { get; private set; }
        public MGStackPanel PortsPanel { get; private set; }
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
            PortsPanel = structure.Parts[PortsPanelPartName] as MGStackPanel;
            BodyPresenter = structure.Parts[BodyPresenterPartName] as MGContentPresenter;
            HeaderTextBlock.Text = Title;
            UpdateCollapsedVisualState();

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
                _BasePortsSpacing = PortsPanel.Spacing;
                _BaseBodyPadding = BodyPresenter.Padding;
                _HasCapturedZoomMetrics = true;
            }

            float clampedZoom = Math.Max(0.1f, zoom);
            _ = HeaderTextBlock.TrySetFont(HeaderTextBlock.FontFamily, Math.Max(1, UIResponsiveMath.ScaleInt(_BaseHeaderFontSize, clampedZoom)));
            HeaderTextBlock.Padding = UIResponsiveMath.ScaleThickness(_BaseHeaderPadding, clampedZoom);
            PortsPanel.Padding = UIResponsiveMath.ScaleThickness(_BasePortsPadding, clampedZoom);
            PortsPanel.Spacing = Math.Max(0, UIResponsiveMath.ScaleInt(_BasePortsSpacing, clampedZoom));
            BodyPresenter.Padding = UIResponsiveMath.ScaleThickness(_BaseBodyPadding, clampedZoom);
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
        public const string OuterBorderPartName = "PART_OuterBorder";
        public const string LabelPartName = "PART_Label";

        protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
        {
            yield return new(OuterBorderPartName, typeof(MGBorder));
            yield return new(LabelPartName, typeof(MGTextBlock));
        }

        private string _PortName = string.Empty;
        private Guid _PortId;
        private bool _IsConnected;
        private bool _IsRequired;
        private bool _IsConnectionDragSource;
        private bool _IsConnectionDragTarget;
        private bool _IsConnectionCompatible;
        private bool _HasCapturedZoomMetrics;
        private int _BaseLabelFontSize;
        private Thickness _BaseOuterPadding;

        public MGBorder OuterBorder { get; private set; }
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
        public GraphPortDirection Direction { get; set; }
        public GraphValueType ValueType { get; set; }

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
                Direction = model?.Direction ?? GraphPortDirection.Input;
                ValueType = model?.ValueType ?? GraphValueType.Wildcard;
                _IsRequired = model?.IsRequired ?? false;
                DefaultControlTemplateName = MGControlTemplateCatalog.GraphPortTemplateName;
            }
        }

        protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure structure)
        {
            OuterBorder = structure.Parts[OuterBorderPartName] as MGBorder;
            Label = structure.Parts[LabelPartName] as MGTextBlock;
            Label.Text = PortName;

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
            OuterBorder.Padding = UIResponsiveMath.ScaleThickness(_BaseOuterPadding, clampedZoom);
        }

        public Vector2 GetLayoutAnchor()
        {
            Rectangle bounds = ActualLayoutBounds.Width > 0 || ActualLayoutBounds.Height > 0
                ? ActualLayoutBounds
                : LayoutBounds;
            float anchorX = Direction == GraphPortDirection.Input ? bounds.Left : bounds.Right;
            return new Vector2(anchorX, bounds.Top + bounds.Height * 0.5f);
        }

        public Vector2 GetWorldAnchor(GraphViewportTransform viewport)
            => viewport == null ? GetLayoutAnchor() : viewport.ViewportToWorld(GetLayoutAnchor());
    }

    public class MGGraphCommentBox : MGSingleContentHost
    {
        public const string OuterBorderPartName = "PART_OuterBorder";
        public const string TitleTextBlockPartName = "PART_TitleTextBlock";
        public const string BodyTextBlockPartName = "PART_BodyTextBlock";

        protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
        {
            yield return new(OuterBorderPartName, typeof(MGBorder));
            yield return new(TitleTextBlockPartName, typeof(MGTextBlock));
            yield return new(BodyTextBlockPartName, typeof(MGTextBlock));
        }

        private string _Title = string.Empty;
        private string _Text = string.Empty;
        private Guid _CommentId;
        private bool _HasCapturedZoomMetrics;
        private int _BaseTitleFontSize;
        private int _BaseBodyFontSize;
        private Thickness _BaseOuterPadding;

        public MGBorder OuterBorder { get; private set; }
        public MGTextBlock TitleTextBlock { get; private set; }
        public MGTextBlock BodyTextBlock { get; private set; }
        public GraphCommentModel Model { get; }

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
                    if (TitleTextBlock != null)
                    {
                        TitleTextBlock.Text = _Title;
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
                    if (BodyTextBlock != null)
                    {
                        BodyTextBlock.Text = _Text;
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
                _Text = model?.Text ?? string.Empty;
                DefaultControlTemplateName = MGControlTemplateCatalog.GraphCommentBoxTemplateName;
            }
        }

        protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure structure)
        {
            OuterBorder = structure.Parts[OuterBorderPartName] as MGBorder;
            TitleTextBlock = structure.Parts[TitleTextBlockPartName] as MGTextBlock;
            BodyTextBlock = structure.Parts[BodyTextBlockPartName] as MGTextBlock;
            TitleTextBlock.Text = Title;
            BodyTextBlock.Text = Text;
            ApplySelectionVisual();

            using (AllowChangingContentTemporarily())
            {
                SetContent(OuterBorder);
            }
        }

        internal void ApplyZoomScale(float zoom)
        {
            if (OuterBorder == null || TitleTextBlock == null || BodyTextBlock == null)
            {
                return;
            }

            if (!_HasCapturedZoomMetrics)
            {
                _BaseTitleFontSize = Math.Max(1, TitleTextBlock.FontSize);
                _BaseBodyFontSize = Math.Max(1, BodyTextBlock.FontSize);
                _BaseOuterPadding = OuterBorder.Padding;
                _HasCapturedZoomMetrics = true;
            }

            float clampedZoom = Math.Max(0.1f, zoom);
            _ = TitleTextBlock.TrySetFont(TitleTextBlock.FontFamily, Math.Max(1, UIResponsiveMath.ScaleInt(_BaseTitleFontSize, clampedZoom)));
            _ = BodyTextBlock.TrySetFont(BodyTextBlock.FontFamily, Math.Max(1, UIResponsiveMath.ScaleInt(_BaseBodyFontSize, clampedZoom)));
            OuterBorder.Padding = UIResponsiveMath.ScaleThickness(_BaseOuterPadding, clampedZoom);
        }

        internal void ApplySelectionVisual()
        {
            if (OuterBorder != null)
            {
                OuterBorder.BorderThickness = IsSelected ? new Thickness(2) : new Thickness(1);
            }
        }
    }
}