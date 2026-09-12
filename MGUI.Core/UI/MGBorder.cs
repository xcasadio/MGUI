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
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI
{
    public class MGBorder : MGSingleContentHost
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private IBorderBrush _BorderBrush;
        public IBorderBrush BorderBrush
        {
            get => _BorderBrush;
            set => SetBorderBrush(value, UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
        }

        /// <summary>Tagged write of <see cref="BorderBrush"/> (ADR-0005): records <paramref name="source"/>'s contribution
        /// in the resolved value store (reference equality, matching <see cref="IBorderBrush"/>'s lack of an
        /// <see cref="object.Equals(object)"/> override) and applies the winning value only if it changed.</summary>
        internal void SetBorderBrush(IBorderBrush value, UIValueResolutionSource source)
        {
            ResolvedValues.Set(UIPilotProperty.BorderBrush, UIValueSlot.Whole, value, source, System.Collections.Generic.ReferenceEqualityComparer.Instance, out bool effectiveChanged, out UIResolvedValue<IBorderBrush> effective);
            if (effectiveChanged)
                ApplyBorderBrushEffective(effective.Value);
        }

        private void ApplyBorderBrushEffective(IBorderBrush value)
        {
            if (_BorderBrush != value)
            {
                IBorderBrush Previous = BorderBrush;
                _BorderBrush = value;
                NPC(nameof(BorderBrush));
                OnBorderBrushChanged?.Invoke(this, new(Previous, BorderBrush));
            }
        }

        public event EventHandler<EventArgs<IBorderBrush>> OnBorderBrushChanged;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Thickness _BorderThickness;
        public Thickness BorderThickness
        {
            get => _BorderThickness;
            set => SetBorderThickness(value, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        }

        /// <summary>Tagged write of <see cref="BorderThickness"/> (ADR-0005): records <paramref name="source"/>'s
        /// contribution in the resolved value store and applies the winning value only if it changed.</summary>
        internal void SetBorderThickness(Thickness value, UIValueResolutionSource source)
        {
            ResolvedValues.Set(UIPilotProperty.BorderThickness, UIValueSlot.Whole, value, source, EqualityComparer<Thickness>.Default, out bool effectiveChanged, out UIResolvedValue<Thickness> effective);
            if (effectiveChanged)
                ApplyBorderThicknessEffective(effective.Value);
        }

        private void ApplyBorderThicknessEffective(Thickness value)
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

        public event EventHandler<EventArgs<Thickness>> OnBorderThicknessChanged;

        /// <summary>Removes the contribution of <paramref name="kind"/> for <see cref="UIPilotProperty.BorderBrush"/> or
        /// <see cref="UIPilotProperty.BorderThickness"/> and, for any other pilot, falls back to the base (Margin,
        /// Padding, MinHeight) behaviour. See <see cref="MGElement.ClearPilotSource"/>.</summary>
        internal override void ClearPilotSource(UIPilotProperty property, UIValueSlot slot, UIValueSourceKind kind)
        {
            switch (property)
            {
                case UIPilotProperty.BorderBrush:
                    if (ResolvedValues.Unset(property, slot, kind, System.Collections.Generic.ReferenceEqualityComparer.Instance, out bool brushChanged, out UIResolvedValue<IBorderBrush> brush) && brushChanged)
                        ApplyBorderBrushEffective(brush.Value);
                    break;
                case UIPilotProperty.BorderThickness:
                    if (ResolvedValues.Unset(property, slot, kind, EqualityComparer<Thickness>.Default, out bool thicknessChanged, out UIResolvedValue<Thickness> thickness) && thicknessChanged)
                        ApplyBorderThicknessEffective(thickness.Value);
                    break;
                default:
                    base.ClearPilotSource(property, slot, kind);
                    break;
            }
        }

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

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsShapeAwareHitTestEnabled;
        /// <summary>If true and <see cref="CornerRadius"/> is not zero, mouse hit testing follows the rounded silhouette of this border
        /// (<see cref="MGBoxShape.Contains(Vector2)"/>) instead of its rectangular bounds, so the rounded-off corners no longer react to the mouse.<para/>
        /// Opt-in: the default value is false, which keeps the framework-wide rectangular hit test. Controls that embed an <see cref="MGBorder"/>
        /// are not affected unless this flag is set on that border.</summary>
        public bool IsShapeAwareHitTestEnabled
        {
            get => _IsShapeAwareHitTestEnabled;
            set
            {
                if (_IsShapeAwareHitTestEnabled != value)
                {
                    _IsShapeAwareHitTestEnabled = value;
                    NPC(nameof(IsShapeAwareHitTestEnabled));
                }
            }
        }

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
                SetBorderBrush(BorderBrush, UIValueResolutionSource.Default(UIInvalidationKind.Draw));
                SetBorderThickness(BorderThickness, UIValueResolutionSource.Default(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
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

        /// <summary>Rectangular test on <see cref="MGElement.ActualLayoutBounds"/> first (unchanged behaviour), then, only when
        /// <see cref="IsShapeAwareHitTestEnabled"/> is true and <see cref="CornerRadius"/> is not zero, the analytic rounded-box test of
        /// <see cref="MGBoxShape.Contains(Vector2)"/> on the same shape that <see cref="DrawSelf"/> paints (same pattern as <see cref="MGEllipse"/>).</summary>
        protected internal override bool ContainsUnscaledInputPoint(Vector2 unscaledScreenPosition)
        {
            if (!base.ContainsUnscaledInputPoint(unscaledScreenPosition))
            {
                return false;
            }

            if (!IsShapeAwareHitTestEnabled || CornerRadius.IsZero)
            {
                return true;
            }

            Vector2 layoutPoint = ConvertCoordinateSpace(CoordinateSpace.UnscaledScreen, CoordinateSpace.Layout, unscaledScreenPosition);
            return CreateBoxShape(LayoutBounds).Contains(layoutPoint);
        }

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
                BackgroundBrush.DrawFillOverlay(DA, roundedSecondaryState, this, backgroundShape, backgroundGeometry);
                return;
            }

            Rectangle backgroundBounds = LayoutBounds.GetCompressed(BorderThickness).GetCompressed(BackgroundRenderPadding);
            BackgroundBrush.GetUnderlay(DA.VisualState.Primary)?.Draw(DA, this, backgroundBounds);

            SecondaryVisualState secondaryState = DA.VisualState.GetSecondaryState(SpoofIsPressedWhileDrawingBackground, SpoofIsHoveredWhileDrawingBackground);
            BackgroundBrush.DrawFillOverlay(DA, secondaryState, this, backgroundBounds);
        }

        public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            MGBoxShape boxShape = CreateBoxShape(LayoutBounds);
            MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(boxShape);
            BorderBrush?.Draw(DA, this, boxShape, geometry);

            if (DrawBackgroundAndOverlay)
            {
                BackgroundBrush.DrawBorderOverlay(DA, DA.VisualState.Secondary, this, boxShape, geometry);
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
