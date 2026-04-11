using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using MGUI.Shared.Helpers;

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
        public static void DrawCheckMark(IUIDrawContext drawContext, Vector2 origin, Rectangle bounds, Color color, float thickness = 2.0f)
        {
            foreach ((Vector2 v0, Vector2 v1) in GetCheckMarkVertices(bounds).SelectConsecutivePairs(false))
            {
                drawContext.StrokeLineSegment(origin, v0, v1, color, thickness);
            }
        }

        public static IReadOnlyList<Vector2> GetCheckMarkVertices(Rectangle bounds)
        {
            Vector2 topLeft = bounds.TopLeft().ToVector2();
            return new[]
            {
                topLeft + new Vector2(bounds.Width * 0.2f, bounds.Height * 0.5f),
                topLeft + new Vector2(bounds.Width * 0.5f, bounds.Height * 0.8f),
                topLeft + new Vector2(bounds.Width * 0.8f, bounds.Height * 0.12f)
            };
        }

        public static void DrawFilledTriangleArrow(IUIDrawContext drawContext, Vector2 origin, Rectangle bounds, UITriangleArrowDirection direction, Color color)
        {
            drawContext.FillPolygon(origin, GetTriangleArrowVertices(bounds, direction).Select(x => x.ToVector2()), color);
        }

        public static void DrawRadioIndicator(IUIDrawContext drawContext, Vector2 origin, Rectangle bounds, Color borderColor,
            float borderThickness, Color fillColor, Color? overlayColor, bool isChecked, Color checkedColor, int numSides = 32)
        {
            Point center = bounds.Center;
            int radius = bounds.Width / 2;

            drawContext.FillCircle(center.ToVector2() + origin, fillColor, radius - borderThickness / 2.0f, numSides);
            drawContext.StrokeCircle(center.ToVector2() + origin, borderColor, radius, borderThickness, numSides);

            if (overlayColor.HasValue)
            {
                drawContext.FillCircle(center.ToVector2() + origin, overlayColor.Value, radius - borderThickness / 2.0f, numSides);
                drawContext.StrokeCircle(center.ToVector2() + origin, overlayColor.Value, radius, borderThickness, numSides);
            }

            if (isChecked)
            {
                int innerRadius = radius - 4;
                drawContext.FillCircle(center.ToVector2() + origin, checkedColor, innerRadius, numSides);
            }
        }

        public static void DrawRadioBullet(IUIDrawContext drawContext, Vector2 origin, Rectangle bounds, Color ringColor, bool isChecked, Color fillColor, int numSides = 16)
        {
            int diameter = System.Math.Min(bounds.Width, bounds.Height) - 4;
            if (diameter <= 0)
            {
                return;
            }

            Vector2 center = (bounds.Center).ToVector2() + origin;
            float radius = diameter / 2.0f;
            drawContext.StrokeCircle(center, ringColor, radius, 1.0f, numSides);
            if (isChecked)
            {
                float innerRadius = System.Math.Max(1.0f, radius - 3.0f);
                drawContext.FillCircle(center, fillColor, innerRadius, numSides);
            }
        }

        public static void DrawGripDots(IUIDrawContext drawContext, Vector2 origin, Rectangle bounds, bool isVertical,
            int dotSize, int spacing, int dotCount, Color dotColor)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0 || dotSize <= 0 || spacing < 0 || dotCount <= 0)
            {
                return;
            }

            if (isVertical)
            {
                int centerX = bounds.X + bounds.Width / 2;
                int centerY = bounds.Y + bounds.Height / 2;
                int startY = centerY - (dotCount * (dotSize + spacing)) / 2;

                for (int index = 0; index < dotCount; index++)
                {
                    int dotY = startY + index * (dotSize + spacing);
                    Rectangle dotRect = new(centerX - dotSize / 2, dotY, dotSize, dotSize);
                    drawContext.FillRectangle(origin, new MonoGame.Extended.RectangleF(dotRect.X, dotRect.Y, dotRect.Width, dotRect.Height), dotColor);
                }
            }
            else
            {
                int centerX = bounds.X + bounds.Width / 2;
                int centerY = bounds.Y + bounds.Height / 2;
                int startX = centerX - (dotCount * (dotSize + spacing)) / 2;

                for (int index = 0; index < dotCount; index++)
                {
                    int dotX = startX + index * (dotSize + spacing);
                    Rectangle dotRect = new(dotX, centerY - dotSize / 2, dotSize, dotSize);
                    drawContext.FillRectangle(origin, new MonoGame.Extended.RectangleF(dotRect.X, dotRect.Y, dotRect.Width, dotRect.Height), dotColor);
                }
            }
        }

        public static void DrawCloseIcon(IUIDrawContext drawContext, Vector2 origin, Rectangle bounds, Color color, float thickness = 1.5f)
        {
            float centerX = bounds.X + bounds.Width * 0.5f;
            float centerY = bounds.Y + bounds.Height * 0.5f;
            float half = System.Math.Min(bounds.Width, bounds.Height) * 0.375f;
            drawContext.StrokeLineSegment(origin,
                new Vector2(centerX - half, centerY - half), new Vector2(centerX + half, centerY + half),
                color, thickness);
            drawContext.StrokeLineSegment(origin,
                new Vector2(centerX + half, centerY - half), new Vector2(centerX - half, centerY + half),
                color, thickness);
        }

        public static void DrawDockPinIcon(IUIDrawContext drawContext, Vector2 origin, Rectangle bounds, Color color)
        {
            float centerX = bounds.X + bounds.Width * 0.5f;
            float centerY = bounds.Y + bounds.Height * 0.5f;
            int halfSize = System.Math.Max(2, System.Math.Min(bounds.Width, bounds.Height) / 4);
            drawContext.FillRectangle(origin,
                new MonoGame.Extended.RectangleF(centerX - halfSize, centerY - halfSize - 1, halfSize * 2, halfSize * 2),
                color);
            drawContext.StrokeLineSegment(origin,
                new Vector2(centerX, centerY + halfSize - 1), new Vector2(centerX, centerY + halfSize + 3),
                color, 1.5f);
        }

        public static void DrawEllipsisIcon(IUIDrawContext drawContext, Vector2 origin, Rectangle bounds, Color color)
        {
            float centerX = bounds.X + bounds.Width * 0.5f;
            float centerY = bounds.Y + bounds.Height * 0.5f;
            const float dotRadius = 1.5f;
            const float spacing = 5.0f;

            for (int index = -1; index <= 1; index++)
            {
                float dotX = centerX + index * spacing;
                drawContext.FillRectangle(origin,
                    new MonoGame.Extended.RectangleF(dotX - dotRadius, centerY - dotRadius, dotRadius * 2.0f, dotRadius * 2.0f),
                    color);
            }
        }

        public static void DrawWindowStateIcon(IUIDrawContext drawContext, Vector2 origin, Rectangle bounds, bool isRestoredState, Color color)
        {
            float centerX = bounds.X + bounds.Width * 0.5f;
            float centerY = bounds.Y + bounds.Height * 0.5f;

            if (isRestoredState)
            {
                const float halfWidth = 5.0f;
                drawContext.StrokeLineSegment(origin,
                    new Vector2(centerX - halfWidth, centerY),
                    new Vector2(centerX + halfWidth, centerY),
                    color, 1.5f);
            }
            else
            {
                const float halfSize = 5.0f;
                drawContext.StrokeRectangle(origin,
                    new MonoGame.Extended.RectangleF(centerX - halfSize, centerY - halfSize, halfSize * 2.0f, halfSize * 2.0f),
                    color,
                    new MonoGame.Extended.Thickness(1),
                    null);
            }
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