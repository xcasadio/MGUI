using System.Collections.Generic;
using System;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Graph;
using MGUI.Core.UI.Styling;
using Microsoft.Xna.Framework;
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

        public MGBorder OuterBorder { get; private set; }
        public MGOverlayPanel ViewportHost { get; private set; }
        public MGCanvas NodesCanvas { get; private set; }
        public MGOverlayPanel OverlayPanel { get; private set; }
        public MGCanvas Surface => NodesCanvas;
        public GraphViewportTransform ViewportTransform { get; } = new();
        public GraphViewportTransform Viewport => ViewportTransform;
        public HashSet<Guid> SelectedNodeIds { get; } = new();
        public HashSet<Guid> SelectedEdgeIds { get; } = new();
        public bool ShowGrid { get; set; } = true;
        public bool AllowZoom { get; set; } = true;
        public bool AllowPan { get; set; } = true;
        public bool SnapToGrid { get; set; }

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
                DefaultControlTemplateName = MGControlTemplateCatalog.GraphViewTemplateName;
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

        private void OnDocumentGraphChanged(object sender, EventArgs e)
            => SynchronizeDocument();
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