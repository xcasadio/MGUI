using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Helpers;
using System;

namespace MGUI.Core.UI
{
    public readonly record struct MGBoxShape(Rectangle OuterBounds, Thickness BorderThickness, MGCornerRadius CornerRadius)
    {
        public bool HasBorder => !NormalizedBorderThickness.IsEmpty();
        public bool HasRoundedCorners => !NormalizedCornerRadius.IsZero;
        public bool IsNormalized => BorderThickness.Equals(NormalizedBorderThickness) && CornerRadius.Equals(NormalizedCornerRadius);

        public Thickness NormalizedBorderThickness => NormalizeBorderThickness(OuterBounds, BorderThickness);
        public MGCornerRadius NormalizedCornerRadius => NormalizeCornerRadius(OuterBounds, CornerRadius);
        public Rectangle InnerBounds => GetInnerBounds();
        public MGCornerRadius InnerCornerRadius => GetInnerCornerRadius();

        public MGBoxShape Normalize() => new(OuterBounds, NormalizedBorderThickness, NormalizedCornerRadius);

        public Rectangle GetInnerBounds()
        {
            Thickness thickness = NormalizedBorderThickness;

            int left = Math.Min(OuterBounds.Right, OuterBounds.Left + thickness.Left);
            int top = Math.Min(OuterBounds.Bottom, OuterBounds.Top + thickness.Top);
            int right = Math.Max(OuterBounds.Left, OuterBounds.Right - thickness.Right);
            int bottom = Math.Max(OuterBounds.Top, OuterBounds.Bottom - thickness.Bottom);

            return new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        }

        public MGCornerRadius GetInnerCornerRadius()
        {
            Thickness thickness = NormalizedBorderThickness;
            MGCornerRadius radius = NormalizedCornerRadius;

            MGCornerRadius inner = new(
                GetInnerCornerValue(radius.TopLeft, thickness.Left, thickness.Top),
                GetInnerCornerValue(radius.TopRight, thickness.Top, thickness.Right),
                GetInnerCornerValue(radius.BottomRight, thickness.Right, thickness.Bottom),
                GetInnerCornerValue(radius.BottomLeft, thickness.Bottom, thickness.Left));

            return NormalizeCornerRadius(InnerBounds, inner);
        }

        private static int GetInnerCornerValue(int outerRadius, int adjacentSide1, int adjacentSide2)
            => Math.Max(0, outerRadius - Math.Max(adjacentSide1, adjacentSide2));

        private static Thickness NormalizeBorderThickness(Rectangle bounds, Thickness thickness)
        {
            Thickness nonNegative = new(
                Math.Max(0, thickness.Left),
                Math.Max(0, thickness.Top),
                Math.Max(0, thickness.Right),
                Math.Max(0, thickness.Bottom));

            Size maxSize = new(Math.Max(0, bounds.Width), Math.Max(0, bounds.Height));
            return nonNegative.Clamp(Size.Empty, maxSize);
        }

        private static MGCornerRadius NormalizeCornerRadius(Rectangle bounds, MGCornerRadius radius)
        {
            int topLeft = Math.Max(0, radius.TopLeft);
            int topRight = Math.Max(0, radius.TopRight);
            int bottomRight = Math.Max(0, radius.BottomRight);
            int bottomLeft = Math.Max(0, radius.BottomLeft);

            int width = Math.Max(0, bounds.Width);
            int height = Math.Max(0, bounds.Height);

            double scale = 1.0;
            scale = GetConstraintScale(scale, width, topLeft + topRight);
            scale = GetConstraintScale(scale, width, bottomLeft + bottomRight);
            scale = GetConstraintScale(scale, height, topLeft + bottomLeft);
            scale = GetConstraintScale(scale, height, topRight + bottomRight);

            if (scale >= 1.0)
            {
                return new MGCornerRadius(topLeft, topRight, bottomRight, bottomLeft);
            }

            return new MGCornerRadius(
                (int)Math.Floor(topLeft * scale),
                (int)Math.Floor(topRight * scale),
                (int)Math.Floor(bottomRight * scale),
                (int)Math.Floor(bottomLeft * scale));
        }

        private static double GetConstraintScale(double currentScale, int availableLength, int requestedLength)
        {
            if (requestedLength <= 0)
            {
                return currentScale;
            }

            return Math.Min(currentScale, availableLength / (double)requestedLength);
        }
    }
}