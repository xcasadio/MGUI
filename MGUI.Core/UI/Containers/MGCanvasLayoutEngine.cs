using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System.Collections.Generic;

namespace MGUI.Core.UI.Containers
{
    internal readonly record struct CanvasChildMeasurement(int Width, int Height, int? Left = null, int? Top = null, int? Right = null, int? Bottom = null, bool IsCollapsed = false);

    internal sealed class CanvasLayoutResult
    {
        public Size DesiredSize { get; }
        public IReadOnlyList<Rectangle> ChildBounds { get; }

        public CanvasLayoutResult(Size desiredSize, IReadOnlyList<Rectangle> childBounds)
        {
            DesiredSize = desiredSize;
            ChildBounds = childBounds;
        }
    }

    internal static class MGCanvasLayoutEngine
    {
        public static Size Measure(IReadOnlyList<CanvasChildMeasurement> children)
        {
            int width = 0;
            int height = 0;

            foreach (CanvasChildMeasurement child in children)
            {
                if (child.IsCollapsed)
                {
                    continue;
                }

                int childLeft = ResolveDesiredOffset(child.Left, child.Right);
                int childTop = ResolveDesiredOffset(child.Top, child.Bottom);
                width = System.Math.Max(width, childLeft + child.Width);
                height = System.Math.Max(height, childTop + child.Height);
            }

            return new Size(width, height);
        }

        public static CanvasLayoutResult Arrange(IReadOnlyList<CanvasChildMeasurement> children, Rectangle bounds)
        {
            List<Rectangle> childBounds = new(children.Count);
            for (int i = 0; i < children.Count; i++)
            {
                childBounds.Add(Rectangle.Empty);
            }

            for (int i = 0; i < children.Count; i++)
            {
                CanvasChildMeasurement child = children[i];
                if (child.IsCollapsed)
                {
                    continue;
                }

                int x = ResolveArrangeOffset(bounds.Left, bounds.Width, child.Width, child.Left, child.Right);
                int y = ResolveArrangeOffset(bounds.Top, bounds.Height, child.Height, child.Top, child.Bottom);
                childBounds[i] = new Rectangle(x, y, child.Width, child.Height);
            }

            return new CanvasLayoutResult(Measure(children), childBounds);
        }

        private static int ResolveDesiredOffset(int? primary, int? secondary)
        {
            if (primary.HasValue)
            {
                return System.Math.Max(0, primary.Value);
            }

            if (secondary.HasValue)
            {
                return System.Math.Max(0, secondary.Value);
            }

            return 0;
        }

        private static int ResolveArrangeOffset(int origin, int availableSize, int childSize, int? primary, int? secondary)
        {
            if (primary.HasValue)
            {
                return origin + primary.Value;
            }

            if (secondary.HasValue)
            {
                return origin + System.Math.Max(0, availableSize - childSize - secondary.Value);
            }

            return origin;
        }
    }
}