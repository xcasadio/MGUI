using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace MGUI.Core.UI.Shapes
{
    public readonly record struct MGBoxGeometry(
        MGBoxShape Shape,
        IReadOnlyList<Vector2> OuterContour,
        IReadOnlyList<Vector2> InnerContour,
        int CornerSegmentCount,
        bool UsesRectangleFastPath)
    {
        public bool HasInnerContour => InnerContour.Count > 0;
        public (IReadOnlyList<Vector2> OuterContour, IReadOnlyList<Vector2> InnerContour)? BorderRing
            => HasInnerContour ? (OuterContour, InnerContour) : null;
    }
}