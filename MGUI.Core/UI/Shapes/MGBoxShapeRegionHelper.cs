using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Shapes;

internal static class MGBoxShapeRegionHelper
{
    public static MGBoxShape CreateSubShape(Rectangle hostBounds, MGCornerRadius hostCornerRadius, Rectangle regionBounds)
    {
        MGCornerRadius regionCornerRadius = new(
            regionBounds.Left <= hostBounds.Left && regionBounds.Top <= hostBounds.Top ? hostCornerRadius.TopLeft : 0,
            regionBounds.Right >= hostBounds.Right && regionBounds.Top <= hostBounds.Top ? hostCornerRadius.TopRight : 0,
            regionBounds.Right >= hostBounds.Right && regionBounds.Bottom >= hostBounds.Bottom ? hostCornerRadius.BottomRight : 0,
            regionBounds.Left <= hostBounds.Left && regionBounds.Bottom >= hostBounds.Bottom ? hostCornerRadius.BottomLeft : 0);

        return new MGBoxShape(regionBounds, new Thickness(0), regionCornerRadius).Normalize();
    }
}