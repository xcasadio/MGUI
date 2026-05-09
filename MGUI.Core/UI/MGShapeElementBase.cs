using Microsoft.Xna.Framework;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Rendering.Clipping;
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
                bool isCurrentSolidFill = FillBrush is MGSolidFillBrush solidFill && solidFill.Color == value;
                if (_Fill != value || !isCurrentSolidFill)
                {
                    _Fill = value;
                    SetFillBrushCore(value.AsFillBrush(), updateLegacyColor: false);
                    NPC(nameof(Fill));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private IFillBrush _FillBrush;
        public IFillBrush FillBrush
        {
            get => _FillBrush;
            set => SetFillBrushCore(value, updateLegacyColor: true);
        }

        protected bool HasVisibleStroke => Stroke != Color.Transparent && StrokeThickness > 0f;
        protected bool HasVisibleFill => FillBrush switch
        {
            null => false,
            MGSolidFillBrush solid => solid.Color != Color.Transparent,
            _ => true,
        };
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

        protected bool TryGetSolidFillColor(float opacity, out Color fillColor)
        {
            if (FillBrush is MGSolidFillBrush solidFill)
            {
                fillColor = solidFill.Color * opacity;
                return fillColor != Color.Transparent;
            }

            fillColor = Color.Transparent;
            return false;
        }

        protected void DrawClippedFillBrush(ElementDrawArgs DA, Rectangle bounds, ClipDefinition clipDefinition)
        {
            if (FillBrush == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            using ClipScope _ = DA.Context.PushClipTemporary(clipDefinition);
            FillBrush.Draw(DA, this, bounds);
        }

        private void SetFillBrushCore(IFillBrush value, bool updateLegacyColor)
        {
            IFillBrush actualValue = value ?? new MGSolidFillBrush(Color.Transparent);
            bool fillBrushChanged = !Equals(_FillBrush, actualValue);
            if (fillBrushChanged)
            {
                _FillBrush = actualValue;
            }

            if (updateLegacyColor && actualValue is MGSolidFillBrush solidFill && _Fill != solidFill.Color)
            {
                _Fill = solidFill.Color;
                NPC(nameof(Fill));
            }
            else if (fillBrushChanged)
            {
                NPC(nameof(Fill));
            }

            if (fillBrushChanged)
            {
                NPC(nameof(FillBrush));
            }
        }
    }
}