using System.Collections.Generic;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace MGUI.Shared.Rendering
{
    /// <summary>High-level draw capabilities consumed by UI code without requiring the concrete renderer type.</summary>
    public interface IUIDrawContext
    {
        public DrawSettings CurrentSettings { get; }

        public void DrawTextureTo(Texture2D Texture, Rectangle? Source, Rectangle Destination);
        public void DrawTextureTo(Texture2D Texture, Rectangle? Source, Rectangle Destination, Color ColorMask);
        public void DrawTextureTo(Texture2D Texture, Rectangle? Source, Rectangle Destination, Color ColorMask,
            Vector2 Origin, float Rotation = 0f, float Depth = 0f, SpriteEffects Effects = SpriteEffects.None);

        public void DrawTextureAt(Texture2D Texture, Rectangle? Source, Vector2 Destination);
        public void DrawTextureAt(Texture2D Texture, Rectangle? Source, Vector2 Destination, Color ColorMask);
        public void DrawTextureAt(Texture2D Texture, Rectangle? Source, Vector2 Destination, Color ColorMask,
            Vector2 Origin, float Rotation = 0f, float ScaleX = 1f, float ScaleY = 1f, float Depth = 0f, SpriteEffects Effects = SpriteEffects.None);

        public void FillRectangle(Vector2 Origin, RectangleF Destination, Color Color, DrawContext? PreferredContext = null);
        public void StrokeRectangle(Vector2 Origin, RectangleF Destination, Color Color, Thickness Thickness, DrawContext? PreferredContext = null);
        public void FillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color Color);
        public void FillTriangle(Vector2 Origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2);
        public void StrokeLineSegment(Vector2 Origin, Vector2 Start, Vector2 End, Color Color, float Thickness = 1.0f, DrawContext? PreferredContext = null);
        public void FillCircle(Vector2 Center, Color Color, float Radius, int NumSides = 32, DrawContext? PreferredContext = null);
        public void StrokeCircle(Vector2 Center, Color Color, float Radius, float Thickness = 1.0f, int NumSides = 32, DrawContext? PreferredContext = null);
    }
}