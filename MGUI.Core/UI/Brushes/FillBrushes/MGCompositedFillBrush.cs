using MGUI.Core.UI.Shapes;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Brushes.FillBrushes;

/// <summary>An <see cref="IFillBrush"/> that draws several nested <see cref="IFillBrush"/>es in order.<para/>
/// Especially useful when compositing multiple transparent colors, in cases where you still need to keep track of each individual Color used instead of reducing it down to a single value.</summary>
public class MGCompositedFillBrush : IFillBrush
{
    public readonly List<IFillBrush> Brushes;

    public MGCompositedFillBrush(params IFillBrush[] Brushes)
    {
        this.Brushes = Brushes.Where(x => x != null).ToList();
    }

    /// <summary>Forwards the per-frame lifecycle call to every nested brush in <see cref="Brushes"/> via <see cref="PaintLifecycle"/>,
    /// deduplicated by reference against every other slot/element that references them for the frame.</summary>
    public void Update(UpdateBaseArgs UA)
    {
        foreach (IFillBrush Brush in Brushes)
        {
            PaintLifecycle.Update(Brush, UA);
        }
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
    {
        foreach (IFillBrush Brush in Brushes)
        {
            Brush.Draw(DA, Element, Bounds);
        }
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
    {
        foreach (IFillBrush Brush in Brushes)
        {
            Brush.Draw(DA, Element, Shape, Geometry);
        }
    }

    public IFillBrush Copy() => new MGCompositedFillBrush(Brushes.Select(x => x.Copy()).ToArray());
}