using System;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.Shared.Assets
{
    public class MonoGameImageResource : IUIImageResource
    {
        public Texture2D Texture { get; }
        public int Width => Texture.Width;
        public int Height => Texture.Height;
        public bool IsDisposed => Texture.IsDisposed;

        public MonoGameImageResource(Texture2D Texture)
        {
            this.Texture = Texture ?? throw new ArgumentNullException(nameof(Texture));
        }
    }

    public sealed class MonoGameRenderTarget : MonoGameImageResource, IUIRenderTarget
    {
        public RenderTarget2D RenderTarget { get; }

        public MonoGameRenderTarget(RenderTarget2D renderTarget)
            : base(renderTarget)
        {
            RenderTarget = renderTarget ?? throw new ArgumentNullException(nameof(renderTarget));
        }
    }

    public static class UIImageResourceExtensions
    {
        public static Texture2D GetTexture2D(this IUIImageResource Image)
        {
            if (Image is MonoGameImageResource monoGameImage)
            {
                return monoGameImage.Texture;
            }

            throw new InvalidOperationException($"{nameof(IUIImageResource)} implementation '{Image?.GetType().FullName ?? "null"}' is not compatible with the MonoGame backend.");
        }

        public static RenderTarget2D GetRenderTarget2D(this IUIRenderTarget renderTarget)
        {
            if (renderTarget is MonoGameRenderTarget monoGameRenderTarget)
            {
                return monoGameRenderTarget.RenderTarget;
            }

            throw new InvalidOperationException($"{nameof(IUIRenderTarget)} implementation '{renderTarget?.GetType().FullName ?? "null"}' is not compatible with the MonoGame backend.");
        }
    }
}