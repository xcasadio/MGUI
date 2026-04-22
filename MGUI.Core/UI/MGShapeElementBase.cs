using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;

namespace MGUI.Core.UI
{
    public abstract class MGShapeElementBase : MGElement
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Color _Stroke;
        public Color Stroke
        {
            get => _Stroke;
            set
            {
                if (_Stroke != value)
                {
                    _Stroke = value;
                    NPC(nameof(Stroke));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private float _StrokeThickness;
        public float StrokeThickness
        {
            get => _StrokeThickness;
            set
            {
                float actualValue = Math.Max(0f, value);
                if (!_StrokeThickness.Equals(actualValue))
                {
                    _StrokeThickness = actualValue;
                    if (StrokeThicknessAffectsLayout)
                    {
                        LayoutChanged(this, true);
                    }

                    NPC(nameof(StrokeThickness));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Color _Fill;
        public Color Fill
        {
            get => _Fill;
            set
            {
                if (_Fill != value)
                {
                    _Fill = value;
                    NPC(nameof(Fill));
                }
            }
        }

        protected bool HasVisibleStroke => Stroke != Color.Transparent && StrokeThickness > 0f;
        protected bool HasVisibleFill => Fill != Color.Transparent;
        protected virtual bool StrokeThicknessAffectsLayout => false;

        protected MGShapeElementBase(MGWindow window, MGElementType elementType)
            : base(window, elementType)
        {
            using (BeginInitializing())
            {
                Stroke = Color.White;
                StrokeThickness = 1f;
                Fill = Color.Transparent;
                HorizontalAlignment = HorizontalAlignment.Center;
                VerticalAlignment = VerticalAlignment.Center;
            }
        }
    }
}