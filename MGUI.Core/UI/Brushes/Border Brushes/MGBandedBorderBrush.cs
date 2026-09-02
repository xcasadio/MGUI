using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MGUI.Core.UI.Shapes;

namespace MGUI.Core.UI.Brushes.Border_Brushes
{
    /// <param name="ThicknessWeight">Determines how much thickness this band will be drawn with.<para/>
    /// EX: If drawing the entire <see cref="MGBandedBorderBrush"/> with <see cref="Thickness"/>=10, and there are 2 bands with weights of 0.4 and 0.6,<br/>
    /// the 1st band is drawn with Floor(10*(0.4/(0.4+0.6))=4, 2nd band is drawn with Floor(10*(0.6/(0.4+0.6))=6 thickness.<para/>
    /// (Weights do not need to sum to 1.0)<para/>
    /// Warning - the actual thickness is always rounded down, so the total rendered size of the border may end up less than the <see cref="MGBorder.BorderThickness"/> it was drawn with.</param>
    public readonly record struct MGBorderBand(IBorderBrush Brush, double ThicknessWeight);

    /// <summary>An <see cref="IBorderBrush"/> that draws several nested <see cref="IBorderBrush"/>es starting from the outside and moving inwards.<para/>
    /// See also: <see cref="MGUniformBorderBrush"/>, <see cref="MGDockedBorderBrush"/>, <see cref="MGTexturedBorderBrush"/>, <see cref="MGHighlightBorderBrush"/>, <see cref="MGCompositedBorderBrush"/></summary>
    public readonly struct MGBandedBorderBrush : IBorderBrush
    {
        public readonly ReadOnlyCollection<MGBorderBand> Bands;

        public MGBandedBorderBrush(IList<Color> Colors, IList<double> Weights)
        {
            if (Colors.Count != Weights.Count)
            {
                throw new InvalidOperationException($"{nameof(MGBandedBorderBrush)}.ctor: There must be exactly 1 weight per color");
            }

            List<MGBorderBand> Bands = new();
            for (int i = 0; i < Colors.Count; i++)
            {
                Color Color = Colors[i];
                double Weight = Weights[i];
                Bands.Add(new(Color.AsFillBrush().AsUniformBorderBrush(), Weight));
            }

            this.Bands = Bands.AsReadOnly();
        }

        /// <param name="Bands">The first band is drawn on the outer edge. Last band is drawn most inwards.</param>
        public MGBandedBorderBrush(params MGBorderBand[] Bands)
        {
            this.Bands = Bands.ToList().AsReadOnly();
        }

        public MGBandedBorderBrush()
        {
            Bands = new List<MGBorderBand>().AsReadOnly();
        }

        /// <summary>Forwards the per-frame lifecycle call to every band's <see cref="MGBorderBand.Brush"/> via <see cref="PaintLifecycle"/>,
        /// deduplicated by reference against every other slot/element that references them for the frame.</summary>
        void IBorderBrush.Update(UpdateBaseArgs UA)
        {
            foreach (MGBorderBand Band in Bands)
            {
                PaintLifecycle.Update(Band.Brush, UA);
            }
        }

        public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds, Thickness BT)
        {
            if (!Bands.Any())
            {
                return;
            }

            double TotalWeight = Bands.Sum(x => x.ThicknessWeight);

            Rectangle RemainingBounds = Bounds;
            foreach (MGBorderBand Band in Bands)
            {
                double PercentageThickness = Band.ThicknessWeight / TotalWeight;
                Thickness BandThickness = new(
                    (int)(BT.Left * PercentageThickness), (int)(BT.Top * PercentageThickness), 
                    (int)(BT.Right * PercentageThickness), (int)(BT.Bottom * PercentageThickness));

                Band.Brush.Draw(DA, Element, RemainingBounds, BandThickness);

                RemainingBounds = RemainingBounds.GetCompressed(BandThickness);
            }
        }

        public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
        {
            if (!Bands.Any())
            {
                return;
            }

            double totalWeight = Bands.Sum(x => x.ThicknessWeight);
            MGBoxShape remainingShape = Shape.Normalize();
            Thickness totalThickness = remainingShape.NormalizedBorderThickness;

            foreach (MGBorderBand band in Bands)
            {
                Thickness remainingThickness = remainingShape.NormalizedBorderThickness;
                if (remainingThickness.IsEmpty())
                {
                    break;
                }

                double percentageThickness = band.ThicknessWeight / totalWeight;
                Thickness bandThickness = new(
                    Math.Min(remainingThickness.Left, (int)(totalThickness.Left * percentageThickness)),
                    Math.Min(remainingThickness.Top, (int)(totalThickness.Top * percentageThickness)),
                    Math.Min(remainingThickness.Right, (int)(totalThickness.Right * percentageThickness)),
                    Math.Min(remainingThickness.Bottom, (int)(totalThickness.Bottom * percentageThickness)));

                if (bandThickness.IsEmpty())
                {
                    continue;
                }

                MGBoxShape bandShape = new(remainingShape.OuterBounds, bandThickness, remainingShape.NormalizedCornerRadius);
                MGBoxGeometry bandGeometry = MGBoxGeometryBuilder.Build(bandShape, Geometry.CornerSegmentCount);
                band.Brush.Draw(DA, Element, bandGeometry.Shape, bandGeometry);

                Thickness nextThickness = new(
                    Math.Max(0, remainingThickness.Left - bandGeometry.Shape.NormalizedBorderThickness.Left),
                    Math.Max(0, remainingThickness.Top - bandGeometry.Shape.NormalizedBorderThickness.Top),
                    Math.Max(0, remainingThickness.Right - bandGeometry.Shape.NormalizedBorderThickness.Right),
                    Math.Max(0, remainingThickness.Bottom - bandGeometry.Shape.NormalizedBorderThickness.Bottom));

                remainingShape = new MGBoxShape(bandGeometry.Shape.InnerBounds, nextThickness, bandGeometry.Shape.InnerCornerRadius).Normalize();
            }
        }

        public IBorderBrush Copy() => new MGBandedBorderBrush(Bands.Select(x => new MGBorderBand(x.Brush.Copy(), x.ThicknessWeight)).ToArray());
    }
}
