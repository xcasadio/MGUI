using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace MGUI.Core.UI.Shapes
{
    public readonly record struct MGBoxGeometry(
        MGBoxShape Shape,
        IReadOnlyList<Vector2> OuterContour,
        IReadOnlyList<Vector2> InnerContour,
        IReadOnlyList<Vector2> Vertices,
        IReadOnlyList<int> FillIndices,
        IReadOnlyList<int> BorderRingIndices,
        int CornerSegmentCount,
        bool UsesRectangleFastPath)
    {
        public bool HasInnerContour => InnerContour.Count > 0;
        public bool HasFillMesh => FillIndices.Count > 0;
        public bool HasBorderRingMesh => BorderRingIndices.Count > 0;
        public int OuterContourVertexCount => OuterContour.Count;
        public int InnerContourVertexCount => InnerContour.Count;
        public (IReadOnlyList<Vector2> OuterContour, IReadOnlyList<Vector2> InnerContour)? BorderRing
            => HasInnerContour ? (OuterContour, InnerContour) : null;
    }
}