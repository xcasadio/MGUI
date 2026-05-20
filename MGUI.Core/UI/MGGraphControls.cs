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

        public MGBorder OuterBorder { get; private set; }
        public MGOverlayPanel ViewportHost { get; private set; }
        public MGCanvas NodesCanvas { get; private set; }
        public MGOverlayPanel OverlayPanel { get; private set; }
        public MGCanvas Surface => NodesCanvas;
        public GraphViewportTransform ViewportTransform { get; } = new();
        public GraphViewportTransform Viewport => ViewportTransform;
        public GraphEdgeGeometryCache EdgeGeometryCache { get; } = new();
        public HashSet<Guid> SelectedNodeIds { get; } = new();
        public HashSet<Guid> SelectedEdgeIds { get; } = new();
        public bool ShowGrid { get; set; } = true;
        public bool AllowZoom { get; set; } = true;
        public bool AllowPan { get; set; } = true;
        public bool SnapToGrid { get; set; }
        public float FramePadding { get; set; } = 32.0f;

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
            MouseHandler.DragStartCondition = DragStartCondition.MousePressed;

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
            };

            MouseHandler.DragEnd += (sender, e) =>
            {
                if (e.IsMMB)
                {
                    _IsPanningViewport = false;
                }
            };

            MouseHandler.ReleasedOutside += (sender, e) =>
            {
                if (e.IsMMB)
                {
                    _IsPanningViewport = false;
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