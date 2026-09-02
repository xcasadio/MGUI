using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MGUI.Core.UI.Shapes;

namespace MGUI.Core.UI.Brushes.Border_Brushes
{
    /// <summary>An <see cref="IBorderBrush"/> that draws several nested <see cref="IBorderBrush"/>es in order.<para/>
    /// See also: <see cref="MGUniformBorderBrush"/>, <see cref="MGDockedBorderBrush"/>, <see cref="MGTexturedBorderBrush"/>, <see cref="MGBandedBorderBrush"/>, <see cref="MGHighlightBorderBrush"/></summary>
    public class MGCompositedBorderBrush : IBorderBrush
    {
        public readonly List<IBorderBrush> Brushes;

        public MGCompositedBorderBrush(params IBorderBrush[] Brushes)
        {
            this.Brushes = Brushes.Where(x => x != null).ToList();
        }

        /// <summary>Forwards the per-frame lifecycle call to every nested brush in <see cref="Brushes"/> via <see cref="PaintLifecycle"/>,
        /// deduplicated by reference against every other slot/element that references them for the frame.</summary>
        void IBorderBrush.Update(UpdateBaseArgs UA)
        {
            foreach (IBorderBrush Brush in Brushes)
            {
                PaintLifecycle.Update(Brush, UA);
            }
        }

        public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds, Thickness BT)
        {
            foreach (IBorderBrush Brush in Brushes)
            {
                Brush.Draw(DA, Element, Bounds, BT);
            }
        }

        public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
        {
            foreach (IBorderBrush Brush in Brushes)
            {
                Brush.Draw(DA, Element, Shape, Geometry);
            }
        }

        public IBorderBrush Copy() => new MGCompositedBorderBrush(Brushes.Select(x => x.Copy()).ToArray());
    }
}
