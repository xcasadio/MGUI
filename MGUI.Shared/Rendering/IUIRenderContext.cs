using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Rendering.Clipping;

namespace MGUI.Shared.Rendering
{
    /// <summary>Minimal rendering contract exposed to high-level UI layers.</summary>
    public interface IUIRenderContext : IUIDrawContext
    {
        public IUIDesktopRuntime Renderer { get; }
        public GraphicsDevice GraphicsDevice { get; }

        public IDisposable SetDrawSettingsTemporary(DrawSettings Settings, DrawContext? PreferredContext = null);
        public IDisposable SetRenderTargetTemporary(RenderTarget2D New, Color? ClearColor);
        public IDisposable SetTransformTemporary(Matrix Transform);
        public ClipResolveResult ResolveClip(ClipDefinition Definition);
        public ClipScope PushClipTemporary(ClipDefinition Definition);
        public ClipScope PushRectangleClip(Rectangle? Bounds, bool IntersectWithCurrentClipTarget);

        // Compatibility shim for existing rectangle-only callers during migration.
        // New code should prefer PushRectangleClip(...) or PushClipTemporary(...).
        public IDisposable SetClipTargetTemporary(Rectangle? Bounds, bool IntersectWithCurrentClipTarget);
    }
}