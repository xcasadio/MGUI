using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System.Collections.Generic;

namespace MGUI.Core.UI.Containers
{
    internal readonly record struct WrapPanelChildMeasurement(int Width, int Height, bool IsCollapsed = false)
    {
        public Size Size => new(Width, Height);
    }

    internal sealed class WrapPanelLayoutResult
    {
        public Size DesiredSize { get; }
        public IReadOnlyList<Rectangle> ChildBounds { get; }

        public WrapPanelLayoutResult(Size desiredSize, IReadOnlyList<Rectangle> childBounds)
        {
            DesiredSize = desiredSize;
            ChildBounds = childBounds;
        }
    }

    internal static class MGWrapPanelLayoutEngine
    {
        private sealed class LineInfo
        {
            public int MainSize { get; set; }
            public int CrossSize { get; set; }
            public List<int> ChildIndices { get; } = new();
        }

        private static bool IsUnbounded(int value) => value >= int.MaxValue / 4;

        public static Size Measure(IReadOnlyList<WrapPanelChildMeasurement> children, Size availableSize, Orientation orientation, int spacing)
        {
            return GetDesiredSize(BuildLines(children, availableSize, orientation, spacing), orientation, spacing);
        }

        public static WrapPanelLayoutResult Arrange(IReadOnlyList<WrapPanelChildMeasurement> children, Rectangle bounds, Orientation orientation, int spacing)
        {
            List<LineInfo> lines = BuildLines(children, bounds.Size, orientation, spacing);
            Size desiredSize = GetDesiredSize(lines, orientation, spacing);
            List<Rectangle> childBounds = new(children.Count);
            for (int i = 0; i < children.Count; i++)
            {
                childBounds.Add(Rectangle.Empty);
            }

            int currentX = bounds.Left;
            int currentY = bounds.Top;
            if (orientation == Orientation.Horizontal)
            {
                foreach (LineInfo line in lines)
                {
                    int childX = currentX;
                    foreach (int childIndex in line.ChildIndices)
                    {
                        WrapPanelChildMeasurement child = children[childIndex];
                        childBounds[childIndex] = new Rectangle(childX, currentY, child.Width, child.Height);
                        childX += child.Width + spacing;
                    }

                    currentY += line.CrossSize + spacing;
                }
            }
            else
            {
                foreach (LineInfo line in lines)
                {
                    int childY = currentY;
                    foreach (int childIndex in line.ChildIndices)
                    {
                        WrapPanelChildMeasurement child = children[childIndex];
                        childBounds[childIndex] = new Rectangle(currentX, childY, child.Width, child.Height);
                        childY += child.Height + spacing;
                    }

                    currentX += line.CrossSize + spacing;
                }
            }

            return new WrapPanelLayoutResult(desiredSize, childBounds);
        }

        private static Size GetDesiredSize(List<LineInfo> lines, Orientation orientation, int spacing)
        {
            if (lines.Count == 0)
            {
                return Size.Zero;
            }

            int mainSize = 0;
            int crossSize = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                LineInfo line = lines[i];
                if (line.MainSize > mainSize)
                {
                    mainSize = line.MainSize;
                }

                crossSize += line.CrossSize;
                if (i > 0)
                {
                    crossSize += spacing;
                }
            }

            return orientation == Orientation.Horizontal
                ? new Size(mainSize, crossSize)
                : new Size(crossSize, mainSize);
        }

        private static List<LineInfo> BuildLines(IReadOnlyList<WrapPanelChildMeasurement> children, Size availableSize, Orientation orientation, int spacing)
        {
            List<LineInfo> lines = new();
            LineInfo currentLine = null;
            int mainLimit = orientation == Orientation.Horizontal ? availableSize.Width : availableSize.Height;
            bool isUnbounded = IsUnbounded(mainLimit);

            for (int i = 0; i < children.Count; i++)
            {
                WrapPanelChildMeasurement child = children[i];
                if (child.IsCollapsed)
                {
                    continue;
                }

                int childMain = orientation == Orientation.Horizontal ? child.Width : child.Height;
                int childCross = orientation == Orientation.Horizontal ? child.Height : child.Width;

                if (currentLine == null)
                {
                    currentLine = new LineInfo();
                    lines.Add(currentLine);
                }

                int candidateMain = currentLine.ChildIndices.Count == 0 ? childMain : currentLine.MainSize + spacing + childMain;
                bool shouldWrap = currentLine.ChildIndices.Count > 0 && !isUnbounded && candidateMain > mainLimit;
                if (shouldWrap)
                {
                    currentLine = new LineInfo();
                    lines.Add(currentLine);
                    candidateMain = childMain;
                }

                currentLine.ChildIndices.Add(i);
                currentLine.MainSize = candidateMain;
                if (childCross > currentLine.CrossSize)
                {
                    currentLine.CrossSize = childCross;
                }
            }

            return lines;
        }
    }
}