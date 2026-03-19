using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Core.UI
{
    public enum UITriangleArrowDirection
    {
        Up,
        Down,
        Left,
        Right,
    }

    public static class UISymbolDrawing
    {
        public static void DrawFilledTriangleArrow(DrawTransaction drawTransaction, Vector2 origin, Rectangle bounds, UITriangleArrowDirection direction, Color color)
        {
            drawTransaction.FillPolygon(origin, GetTriangleArrowVertices(bounds, direction).Select(x => x.ToVector2()), color);
        }

        public static IReadOnlyList<Point> GetTriangleArrowVertices(Rectangle bounds, UITriangleArrowDirection direction)
        {
            return direction switch
            {
                UITriangleArrowDirection.Up => new[]
                {
                    new Point(bounds.Left, bounds.Bottom), new Point(bounds.Right, bounds.Bottom), new Point(bounds.Center.X, bounds.Top)
                },
                UITriangleArrowDirection.Down => new[]
                {
                    new Point(bounds.Left, bounds.Top), new Point(bounds.Right, bounds.Top), new Point(bounds.Center.X, bounds.Bottom)
                },
                UITriangleArrowDirection.Left => new[]
                {
                    new Point(bounds.Right, bounds.Top), new Point(bounds.Right, bounds.Bottom), new Point(bounds.Left, bounds.Center.Y)
                },
                UITriangleArrowDirection.Right => new[]
                {
                    new Point(bounds.Left, bounds.Top), new Point(bounds.Left, bounds.Bottom), new Point(bounds.Right, bounds.Center.Y)
                },
                _ => new[]
                {
                    new Point(bounds.Left, bounds.Top), new Point(bounds.Right, bounds.Top), new Point(bounds.Center.X, bounds.Bottom)
                },
            };
        }
    }
}