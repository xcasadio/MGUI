using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.Shared.Helpers
{
    /// <summary>Creates a 1x1 pixel texture of a solid color</summary>
    public class SolidColorTexture : Texture2D
    {
        private Color _color;
        public Color Color
        {
            get { return _color; }
            set
            {
                if (value != _color)
                {
                    _color = value;
                    SetData(new Color[] { _color });
                }
            }
        }

        public SolidColorTexture(GraphicsDevice graphicsDevice) : base(graphicsDevice, 1, 1) { }
        public SolidColorTexture(GraphicsDevice graphicsDevice, Color color)
            : base(graphicsDevice, 1, 1)
        {
            Color = color;
        }

        public static implicit operator Color(SolidColorTexture tex) => tex.Color;
    }
}