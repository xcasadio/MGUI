using Microsoft.Xna.Framework;
using MGUI.Shared.Rendering.Clipping;

namespace MGUI.Core.UI.Shapes;

internal static class ClipDefinitionBoxShapeExtensions
{
    public static ClipCornerRadius ToClipCornerRadius(this MGCornerRadius cornerRadius)
        => new(cornerRadius.TopLeft, cornerRadius.TopRight, cornerRadius.BottomRight, cornerRadius.BottomLeft);

    public static ClipGeometry ToClipGeometry(this MGBoxGeometry geometry)
        => new(geometry.Vertices, geometry.FillIndices);

    public static ClipGeometry ToClipGeometry(this MGBoxGeometry geometry, Point offset)
    {
        if (offset == Point.Zero)
        {
            return geometry.ToClipGeometry();
        }

        IReadOnlyList<Vector2> translatedVertices = geometry.Vertices
            .Select(vertex => vertex + offset.ToVector2())
            .ToArray();
        return new ClipGeometry(translatedVertices, geometry.FillIndices);
    }
}