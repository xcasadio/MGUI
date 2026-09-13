using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Containers;

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
        var lines = BuildLines(children, bounds.Size, orientation, spacing);
        var desiredSize = GetDesiredSize(lines, orientation, spacing);
        List<Rectangle> childBounds = new(children.Count);
        ArrangeInto(children, bounds, orientation, spacing, lines, childBounds);

        return new WrapPanelLayoutResult(desiredSize, childBounds);
    }

    public static void ArrangeInto(IReadOnlyList<WrapPanelChildMeasurement> children, Rectangle bounds, Orientation orientation, int spacing,
        List<Rectangle> childBounds)
    {
        var lines = BuildLines(children, bounds.Size, orientation, spacing);
        ArrangeInto(children, bounds, orientation, spacing, lines, childBounds);
    }

    private static void ArrangeInto(IReadOnlyList<WrapPanelChildMeasurement> children, Rectangle bounds, Orientation orientation, int spacing,
        List<LineInfo> lines, List<Rectangle> childBounds)
    {
        childBounds.Clear();
        for (var i = 0; i < children.Count; i++)
        {
            childBounds.Add(Rectangle.Empty);
        }

        var currentX = bounds.Left;
        var currentY = bounds.Top;
        if (orientation == Orientation.Horizontal)
        {
            foreach (var line in lines)
            {
                var childX = currentX;
                foreach (var childIndex in line.ChildIndices)
                {
                    var child = children[childIndex];
                    childBounds[childIndex] = new Rectangle(childX, currentY, child.Width, child.Height);
                    childX += child.Width + spacing;
                }

                currentY += line.CrossSize + spacing;
            }
        }
        else
        {
            foreach (var line in lines)
            {
                var childY = currentY;
                foreach (var childIndex in line.ChildIndices)
                {
                    var child = children[childIndex];
                    childBounds[childIndex] = new Rectangle(currentX, childY, child.Width, child.Height);
                    childY += child.Height + spacing;
                }

                currentX += line.CrossSize + spacing;
            }
        }
    }

    private static Size GetDesiredSize(List<LineInfo> lines, Orientation orientation, int spacing)
    {
        if (lines.Count == 0)
        {
            return new Size(0, 0);
        }

        var mainSize = 0;
        var crossSize = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
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
        var mainLimit = orientation == Orientation.Horizontal ? availableSize.Width : availableSize.Height;
        var isUnbounded = IsUnbounded(mainLimit);

        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child.IsCollapsed)
            {
                continue;
            }

            var childMain = orientation == Orientation.Horizontal ? child.Width : child.Height;
            var childCross = orientation == Orientation.Horizontal ? child.Height : child.Width;

            if (currentLine == null)
            {
                currentLine = new LineInfo();
                lines.Add(currentLine);
            }

            var candidateMain = currentLine.ChildIndices.Count == 0 ? childMain : currentLine.MainSize + spacing + childMain;
            var shouldWrap = currentLine.ChildIndices.Count > 0 && !isUnbounded && candidateMain > mainLimit;
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