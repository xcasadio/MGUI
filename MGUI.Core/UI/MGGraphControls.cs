using System.Collections.Generic;
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
        public const string SurfacePartName = "PART_Surface";

        protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
        {
            yield return new(OuterBorderPartName, typeof(MGBorder));
            yield return new(SurfacePartName, typeof(MGCanvas));
        }

        public MGBorder OuterBorder { get; private set; }
        public MGCanvas Surface { get; private set; }
        public GraphDocument Document { get; }
        public GraphViewportTransform Viewport { get; } = new();
        public bool ShowGrid { get; set; } = true;
        public bool AllowZoom { get; set; } = true;
        public bool AllowPan { get; set; } = true;
        public bool SnapToGrid { get; set; }

        public MGGraphView(MGWindow window)
            : this(window, new GraphDocument()) { }

        public MGGraphView(MGWindow window, GraphDocument document)
            : base(window, MGElementType.GraphView)
        {
            using (BeginInitializing())
            {
                Document = document ?? new GraphDocument();
                DefaultControlTemplateName = MGControlTemplateCatalog.GraphViewTemplateName;
            }
        }

        protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure structure)
        {
            OuterBorder = structure.Parts[OuterBorderPartName] as MGBorder;
            Surface = structure.Parts[SurfacePartName] as MGCanvas;
            Surface.CanChangeContent = false;

            using (OuterBorder.AllowChangingContentTemporarily())
            {
                OuterBorder.SetContent(Surface);
            }

            using (AllowChangingContentTemporarily())
            {
                SetContent(OuterBorder);
            }
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

        public MGBorder OuterBorder { get; private set; }
        public MGTextBlock HeaderTextBlock { get; private set; }
        public MGStackPanel PortsPanel { get; private set; }
        public MGContentPresenter BodyPresenter { get; private set; }
        public GraphNodeModel Model { get; }

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

        public MGGraphNode(MGWindow window)
            : this(window, null) { }

        public MGGraphNode(MGWindow window, GraphNodeModel model)
            : base(window, MGElementType.GraphNode)
        {
            using (BeginInitializing())
            {
                Model = model;
                _Title = model?.Title ?? string.Empty;
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

        public MGBorder OuterBorder { get; private set; }
        public MGTextBlock Label { get; private set; }
        public GraphPortModel Model { get; }
        public GraphPortDirection Direction { get; set; }
        public GraphValueType ValueType { get; set; }

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
                _PortName = model?.Name ?? string.Empty;
                Direction = model?.Direction ?? GraphPortDirection.Input;
                ValueType = model?.ValueType ?? GraphValueType.Wildcard;
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