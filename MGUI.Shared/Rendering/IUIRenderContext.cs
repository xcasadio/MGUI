using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Rendering.Clipping;

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
        public ClipResolveResult ResolveClip(ClipDefinition Definition);
        public ClipScope PushClipTemporary(ClipDefinition Definition);
        public ClipScope PushRectangleClip(Rectangle? Bounds, bool IntersectWithCurrentClipTarget);
        public IDisposable SetClipTargetTemporary(Rectangle? Bounds, bool IntersectWithCurrentClipTarget);
    }
}