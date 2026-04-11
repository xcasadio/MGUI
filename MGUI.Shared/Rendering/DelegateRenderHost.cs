using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.Shared.Rendering
{
    /// <summary>
    /// Lightweight MonoGame host adapter driven by explicit delegates and notifications instead of a concrete <see cref="Game"/> subtype.
    /// </summary>
    public sealed class DelegateRenderHost : IRenderHost
    {
        private readonly Func<Rectangle> _GetBounds;
        private readonly IServiceProvider _Services;

        public GraphicsDevice GraphicsDevice { get; }

        public event EventHandler<TimeSpan> PreviewUpdate;
        public event EventHandler<EventArgs> EndUpdate;

        public DelegateRenderHost(GraphicsDevice GraphicsDevice, Func<Rectangle> GetBounds, IServiceProvider Services = null)
        {
            this.GraphicsDevice = GraphicsDevice ?? throw new ArgumentNullException(nameof(GraphicsDevice));
            _GetBounds = GetBounds ?? throw new ArgumentNullException(nameof(GetBounds));
            _Services = Services;
        }

        public Rectangle GetBounds() => _GetBounds();

        public object GetService(Type serviceType) => _Services?.GetService(serviceType);

        public void NotifyPreviewUpdate(TimeSpan totalElapsed)
            => PreviewUpdate?.Invoke(this, totalElapsed);

        public void NotifyEndUpdate()
            => EndUpdate?.Invoke(this, EventArgs.Empty);
    }
}