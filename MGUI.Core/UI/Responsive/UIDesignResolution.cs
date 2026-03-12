using MonoGame.Extended;
using System;

namespace MGUI.Core.UI.Responsive
{
    public readonly record struct UIDesignResolution
    {
        public int Width { get; }
        public int Height { get; }

        public UIDesignResolution(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            Width = width;
            Height = height;
        }

        public Size ToSize() => new(Width, Height);

        public static UIDesignResolution HD => new(1920, 1080);
    }
}