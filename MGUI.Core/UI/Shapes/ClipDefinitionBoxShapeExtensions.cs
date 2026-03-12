using MGUI.Shared.Rendering.Clipping;

namespace MGUI.Core.UI.Shapes
{
    internal static class ClipDefinitionBoxShapeExtensions
    {
        public static ClipCornerRadius ToClipCornerRadius(this MGCornerRadius cornerRadius)
            => new(cornerRadius.TopLeft, cornerRadius.TopRight, cornerRadius.BottomRight, cornerRadius.BottomLeft);

        public static ClipGeometry ToClipGeometry(this MGBoxGeometry geometry)
            => new(geometry.Vertices, geometry.FillIndices);
    }
}