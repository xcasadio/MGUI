using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Helpers;

namespace MGUI.Core.UI.Adorners;

internal static class MGAdornerGeometryHelper
{
    public const int ResizeHandleCount = 8;

    public static Rectangle ResolveTargetBounds(Rectangle sourceBounds, Thickness margin)
        => sourceBounds.GetExpanded(margin);

    public static int ResolveGuideCoordinate(Rectangle targetBounds, MGGuideAxis axis, MGGuideAlignment alignment)
        => axis switch
        {
            MGGuideAxis.Vertical => alignment switch
            {
                MGGuideAlignment.Start => targetBounds.Left,
                MGGuideAlignment.Center => targetBounds.Left + (targetBounds.Width / 2),
                MGGuideAlignment.End => targetBounds.Right,
                _ => targetBounds.Left + (targetBounds.Width / 2),
            },
            MGGuideAxis.Horizontal => alignment switch
            {
                MGGuideAlignment.Start => targetBounds.Top,
                MGGuideAlignment.Center => targetBounds.Top + (targetBounds.Height / 2),
                MGGuideAlignment.End => targetBounds.Bottom,
                _ => targetBounds.Top + (targetBounds.Height / 2),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null),
        };

    public static void FillResizeHandleBounds(Rectangle targetBounds, int handleSize, Span<Rectangle> destination)
    {
        if (destination.Length < ResizeHandleCount)
        {
            throw new ArgumentException($"Destination must contain at least {ResizeHandleCount} slots.", nameof(destination));
        }

        var size = Math.Max(1, handleSize);
        var half = size / 2;

        var left = targetBounds.Left - half;
        var centerX = targetBounds.Left + (targetBounds.Width / 2) - half;
        var right = targetBounds.Right - half;

        var top = targetBounds.Top - half;
        var centerY = targetBounds.Top + (targetBounds.Height / 2) - half;
        var bottom = targetBounds.Bottom - half;

        destination[0] = new(left, top, size, size);
        destination[1] = new(centerX, top, size, size);
        destination[2] = new(right, top, size, size);
        destination[3] = new(left, centerY, size, size);
        destination[4] = new(right, centerY, size, size);
        destination[5] = new(left, bottom, size, size);
        destination[6] = new(centerX, bottom, size, size);
        destination[7] = new(right, bottom, size, size);
    }
}