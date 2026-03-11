using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.Shared.Rendering
{
    /// <summary>Minimal rendering contract exposed to high-level UI layers.</summary>
    public interface IUIRenderContext
    {
        public MainRenderer Renderer { get; }
        public GraphicsDevice GD { get; }
        public DrawSettings CurrentSettings { get; }

        public IDisposable SetRenderTargetTemporary(RenderTarget2D New, Color? ClearColor);
        public IDisposable SetTransformTemporary(Matrix Transform);
        public IDisposable SetClipTargetTemporary(Rectangle? Bounds, bool IntersectWithCurrentClipTarget);
    }
}