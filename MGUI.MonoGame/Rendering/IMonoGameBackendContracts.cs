using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.Backend.MonoGame
{
    /// <summary>
    /// Explicit MonoGame-specific runtime surface for integrations that intentionally need host access
    /// or SpriteFont infrastructure. Core UI code should continue to depend on <see cref="IUIDesktopRuntime"/>.
    /// </summary>
    public interface IMonoGameDesktopBackend : IUIDesktopRuntime
    {
        public IRenderHost Host { get; }
        public FontManager FontManager { get; }
    }

    /// <summary>
    /// Explicit MonoGame-specific draw context used by backend text engines that need a live
    /// <see cref="SpriteBatch"/> without depending on the concrete draw transaction type.
    /// </summary>
    public interface IMonoGameDrawContext : IUIDrawTransaction
    {
        public SpriteBatch SpriteBatch { get; }
    }
}