using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.Shared.Rendering
{
    /// <summary>Represents the logical UI surface currently targeted for composition.</summary>
    public interface IUISurface
    {
        public Rectangle GetBounds();
        public RenderTarget2D GetRenderTarget();
    }
}