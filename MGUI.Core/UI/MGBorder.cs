using Microsoft.Xna.Framework;
using MGUI.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using System.Diagnostics;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Rendering.Clipping;

namespace MGUI.Core.UI
{
    public class MGBorder : MGSingleContentHost
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private IBorderBrush _BorderBrush;
        public IBorderBrush BorderBrush
        {
            get => _BorderBrush;
            set
            {
                if (_BorderBrush != value)
                {
                    IBorderBrush Previous = BorderBrush;
                    _BorderBrush = value;
                    NPC(nameof(BorderBrush));
                    OnBorderBrushChanged?.Invoke(this, new(Previous, BorderBrush));
                }
            }
        }

        public event EventHandler<EventArgs<IBorderBrush>> OnBorderBrushChanged;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Thickness _BorderThickness;
        public Thickness BorderThickness
        {
            get => _BorderThickness;
            set
            {
                if (!_BorderThickness.Equals(value))
                {
                    Thickness Previous = BorderThickness;
                    _BorderThickness = value;
                    LayoutChanged(this, true);
                    NPC(nameof(BorderThickness));
                    OnBorderThicknessChanged?.Invoke(this, new(Previous, BorderThickness));
                }
            }
        }

        public event EventHandler<EventArgs<Thickness>> OnBorderThicknessChanged;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGCornerRadius _CornerRadius;
        public MGCornerRadius CornerRadius
        {
            get => _CornerRadius;
            set
            {
                if (!_CornerRadius.Equals(value))
                {
                    MGCornerRadius previous = CornerRadius;
                    _CornerRadius = value;
                    LayoutChanged(this, true);
                    NPC(nameof(CornerRadius));
                    OnCornerRadiusChanged?.Invoke(this, new(previous, CornerRadius));
                }
            }
        }

        public event EventHandler<EventArgs<MGCornerRadius>> OnCornerRadiusChanged;

        public bool DrawBackgroundAndOverlay { get; set; } = true;

        public MGBorder(MGWindow Window)
            : this(Window, new(1), MGUniformBorderBrush.Black) { }

        public MGBorder(MGWindow Window, Thickness BorderThickness, IFillBrush BorderBrush)
            : this(Window, BorderThickness, BorderBrush == null ? null : new MGUniformBorderBrush(BorderBrush)) { }

        public MGBorder(MGWindow Window, Thickness BorderThickness, IBorderBrush BorderBrush)
            : base(Window, MGElementType.Border)
        {
            using (BeginInitializing())
            {
                this.BorderBrush = BorderBrush;
                this.BorderThickness = BorderThickness;
                this.CornerRadius = MGCornerRadius.Zero;
            }
        }

        public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness SharedSize)
        {
            SharedSize = new(0);
            return BorderThickness;
        }

        public override MGBorder GetBorder() => this;

        protected override bool CanConsumeSpaceInSingleDimension => true;

        private MGBoxShape CreateBoxShape(Rectangle layoutBounds) => new MGBoxShape(layoutBounds, BorderThickness, CornerRadius).Normalize();

        public override void DrawBackground(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            if (!DrawBackgroundAndOverlay)
            {
                return;
            }

            if (TryGetRoundedBackgroundShapeAndGeometry(LayoutBounds, out MGBoxShape backgroundShape, out MGBoxGeometry backgroundGeometry, true))
            {
                BackgroundBrush.GetUnderlay(DA.VisualState.Primary)?.Draw(DA, this, backgroundShape, backgroundGeometry);

                SecondaryVisualState roundedSecondaryState = DA.VisualState.GetSecondaryState(SpoofIsPressedWhileDrawingBackground, SpoofIsHoveredWhileDrawingBackground);
                BackgroundBrush.GetFillOverlay(roundedSecondaryState)?.Draw(DA, this, backgroundShape, backgroundGeometry);
                return;
            }

            Rectangle backgroundBounds = LayoutBounds.GetCompressed(BorderThickness).GetCompressed(BackgroundRenderPadding);
            BackgroundBrush.GetUnderlay(DA.VisualState.Primary)?.Draw(DA, this, backgroundBounds);

            SecondaryVisualState secondaryState = DA.VisualState.GetSecondaryState(SpoofIsPressedWhileDrawingBackground, SpoofIsHoveredWhileDrawingBackground);
            BackgroundBrush.GetFillOverlay(secondaryState)?.Draw(DA, this, backgroundBounds);
        }

        public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            MGBoxShape boxShape = CreateBoxShape(LayoutBounds);
            MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(boxShape);
            BorderBrush?.Draw(DA, this, boxShape, geometry);

            if (DrawBackgroundAndOverlay)
            {
                BackgroundBrush.GetBorderOverlay(DA.VisualState.Secondary)?.Draw(DA, this, boxShape, geometry);
            }
        }

        internal override ClipDefinition GetContentsClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
        {
            if (!ClipToBounds)
            {
                return null;
            }

            // Rounded border/background paint still uses the box-shape brushes directly.
            // This hook only declares the content clip that should be resolved by the renderer.
            return CreateBorderBackedContentsClipDefinition(DA, layoutBounds, $"{ElementType}.Contents");
        }
    }
}
