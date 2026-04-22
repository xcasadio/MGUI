using MGUI.Core.UI.Shapes;
using MonoGame.Extended;

namespace MGUI.Core.UI
{
    public abstract class MGVertexShapeElementBase : MGShapeElementBase
    {
        private Size _GeometrySize;
        protected Size GeometrySize
        {
            get => _GeometrySize;
            set => _GeometrySize = value;
        }

        protected override bool StrokeThicknessAffectsLayout => true;

        protected MGVertexShapeElementBase(MGWindow window, MGElementType elementType)
            : base(window, elementType)
        {
            GeometrySize = new Size(0, 0);
        }

        internal MGPointShapePlacement GetPlacement(Microsoft.Xna.Framework.Rectangle layoutBounds)
            => MGVectorShapeHelper.CreatePlacement(layoutBounds, HorizontalAlignment, VerticalAlignment, GeometrySize,
                HasVisibleStroke ? StrokeThickness : 0f);

        public override Thickness MeasureSelfOverride(Size availableSize, out Thickness sharedSize)
        {
            sharedSize = new Thickness(0);
            Size desiredSize = MGVectorShapeHelper.GetDesiredSize(GeometrySize, HasVisibleStroke ? StrokeThickness : 0f);
            return new Thickness(desiredSize.Width, desiredSize.Height, 0, 0);
        }
    }
}