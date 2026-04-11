using System;
using Microsoft.Xna.Framework;
namespace MGUI.Shared.Rendering
{
    /// <summary>Default UI surface backed by the main backbuffer bounds exposed by an <see cref="IRenderHost"/>.</summary>
    public sealed class BackBufferSurface : IUISurface
    {
        private readonly IRenderHost Host;

        public BackBufferSurface(IRenderHost Host)
        {
            this.Host = Host ?? throw new ArgumentNullException(nameof(Host));
        }

        public Rectangle GetBounds() => Host.GetBounds();
        public IUIRenderTarget GetRenderTarget() => null;
    }
}