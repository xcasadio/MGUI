using Microsoft.Xna.Framework;
namespace MGUI.Shared.Rendering
{
    /// <summary>Represents the logical UI surface currently targeted for composition.</summary>
    public interface IUISurface
    {
        public Rectangle GetBounds();
        public IUIRenderTarget GetRenderTarget();
    }
}