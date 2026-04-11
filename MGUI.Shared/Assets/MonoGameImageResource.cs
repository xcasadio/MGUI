using System;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.Shared.Assets
{
    public sealed class MonoGameImageResource : IUIImageResource
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
    }
}