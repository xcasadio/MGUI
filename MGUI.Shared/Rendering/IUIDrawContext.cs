using System.Collections.Generic;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Shared.Rendering
{
    /// <summary>High-level draw capabilities consumed by UI code without requiring the concrete renderer type.</summary>
    public interface IUIDrawContext
    {
        public DrawSettings CurrentSettings { get; }

        public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask);
        public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask,
            Vector2 Origin, float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None);

        public void DrawTextureAt(IUIImageResource Texture, Rectangle? Source, Vector2 Destination, Color ColorMask,
            Vector2 Origin, float Rotation = 0f, float ScaleX = 1f, float ScaleY = 1f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None);
        public void DrawTextViaEngine(ResolvedFont Font, string Text, Vector2 Position, Color Color, Vector2 Origin, float Scale,
            float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None);

        public void FillRectangle(Vector2 Origin, RectangleF Destination, Color Color);
        public void FillPoint(Vector2 Center, Color Color, float Width);
        public void StrokeRectangle(Vector2 Origin, RectangleF Destination, Color Color, Thickness Thickness);
        public void StrokeAndFillRectangle(Vector2 Origin, RectangleF Destination, Color StrokeColor, Color FillColor, Thickness StrokeThickness);
        public void FillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color Color);
        public void StrokeAndFillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color StrokeColor, Color FillColor, float StrokeThickness = 1.0f);
        public void FillTriangle(Vector2 Origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2);
        /// <summary>Draws an indexed triangle list textured with <paramref name="Texture"/>.<para/>
        /// <paramref name="Vertices"/> are in the same space as the other primitives (<paramref name="Origin"/> is added to each vertex);
        /// <paramref name="TextureCoordinates"/> holds exactly one normalized (0..1 over the whole texture) coordinate per vertex;
        /// <paramref name="Indices"/> references <paramref name="Vertices"/> three by three.<para/>
        /// Nothing is drawn when the texture is null or disposed, when there is no vertex, or when fewer than three indices are supplied.
        /// The current <see cref="DrawSettings.SamplerType"/> applies (use a Wrap sampler to tile).</summary>
        public void DrawTexturedTriangleList(Vector2 Origin, IUIImageResource Texture, IReadOnlyList<Vector2> Vertices, IReadOnlyList<Vector2> TextureCoordinates,
            IReadOnlyList<int> Indices, Color ColorMask);
        public void FillQuadrilateralLinearClamp(Vector2 Origin, Vector2 topLeft, Color topLeftColor, Vector2 topRight, Color topRightColor,
            Vector2 bottomRight, Color bottomRightColor, Vector2 bottomLeft, Color bottomLeftColor);
        public void StrokeLineSegment(Vector2 Origin, Vector2 Start, Vector2 End, Color Color, float Thickness = 1.0f);
        public void FillCircle(Vector2 Center, Color Color, float Radius, int NumSides = 32);
        public void StrokeCircle(Vector2 Center, Color Color, float Radius, float Thickness = 1.0f, int NumSides = 32);
        public void StrokeAndFillCircle(Vector2 Center, Color StrokeColor, Color FillColor, float Radius, float StrokeThickness = 1.0f, int NumSides = 32);
    }
}