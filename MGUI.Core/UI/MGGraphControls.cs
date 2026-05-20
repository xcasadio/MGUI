using System.Collections.Generic;
using System;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Graph;
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
        private bool _IsSelectingRectangle;
        private Guid _PressedNodeId;
        private Vector2 _PointerPressViewportPoint;
        private Vector2 _CurrentSelectionViewportPoint;
        private readonly Dictionary<Guid, Vector2> _NodeDragStartPositions = new();
        private readonly HashSet<MGGraphPort> _RegisteredConnectionPorts = new();

        public MGBorder OuterBorder { get; private set; }
        public MGOverlayPanel ViewportHost { get; private set; }
        public MGCanvas NodesCanvas { get; private set; }
        public MGOverlayPanel OverlayPanel { get; private set; }
        public MGCanvas Surface => NodesCanvas;
        public GraphViewportTransform ViewportTransform { get; } = new();
        public GraphViewportTransform Viewport => ViewportTransform;
        public GraphEdgeGeometryCache EdgeGeometryCache { get; } = new();
        public GraphCommandStack Commands { get; } = new();
        public GraphSelectionManager Selection { get; }
        public GraphConnectionController ConnectionController { get; }
        public HashSet<Guid> SelectedNodeIds { get; } = new();
        public HashSet<Guid> SelectedEdgeIds { get; } = new();
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
            => _Synchronizer?.Synchronize();

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
                        ConnectionController.Cancel();
                    }
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
            if (changed)
            {
                UpdateSelectionVisuals();
            }

            return changed;
        }

        public bool SelectNode(Guid nodeId, bool additive = false, bool toggle = false)
        {
            bool changed = Selection.SelectNode(nodeId, additive, toggle);
            if (changed)
            {
                UpdateSelectionVisuals();
            }

            return changed;
        }

        public bool SelectNodesInWorldRectangle(RectangleF worldRectangle, bool additive = false)
        {
            bool changed = Selection.SelectNodesInRectangle(Document, worldRectangle, additive);
            if (changed)
            {
                UpdateSelectionVisuals();
            }

            return changed;
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
        {
            switch (key)
            {
                case Keys.A:
                    FrameAll();
                    return true;
                case Keys.F:
                    FrameSelection();
                    return true;
                case Keys.Home:
                    FrameOrigin();
                    return true;
                default:
                    return false;
            }
        }

        private void OnDocumentGraphChanged(object sender, EventArgs e)
            => SynchronizeDocument();

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
                    SelectNode(node.NodeId, additive: controlDown, toggle: controlDown);
                    e.SetHandledBy(this, false);
                }
                else
                {
                    _PressedNodeId = Guid.Empty;
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
                if (HandleGraphShortcut(e.Key))
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
            _IsDraggingNodes = false;
            _IsSelectingRectangle = false;
            _NodeDragStartPositions.Clear();
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

        public MGBorder OuterBorder { get; private set; }
        public MGTextBlock TitleTextBlock { get; private set; }
        public MGTextBlock BodyTextBlock { get; private set; }
        public GraphCommentModel Model { get; }

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

            using (AllowChangingContentTemporarily())
            {
                SetContent(OuterBorder);
            }
        }
    }
}