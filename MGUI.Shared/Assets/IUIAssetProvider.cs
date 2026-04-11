using MGUI.Shared.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using System;

namespace MGUI.Shared.Assets
{
    public interface IUIAssetProvider
    {
        ContentManager Content { get; }
        FontManager FontManager { get; }

        IUIImageResource LoadImage(string assetName);
        bool TryLoadImage(string assetName, out IUIImageResource image);
        Texture2D LoadTexture(string assetName);
        bool TryLoadTexture(string assetName, out Texture2D texture);
    }

    public class RendererAssetProvider : IUIAssetProvider
    {
        public ContentManager Content { get; }
        public FontManager FontManager { get; }

        public RendererAssetProvider(ContentManager Content, FontManager FontManager)
        {
            this.Content = Content ?? throw new ArgumentNullException(nameof(Content));
            this.FontManager = FontManager ?? throw new ArgumentNullException(nameof(FontManager));
        }

        public IUIImageResource LoadImage(string assetName)
            => new MonoGameImageResource(LoadTexture(assetName));

        public bool TryLoadImage(string assetName, out IUIImageResource image)
        {
            if (TryLoadTexture(assetName, out Texture2D texture))
            {
                image = new MonoGameImageResource(texture);
                return true;
            }

            image = null;
            return false;
        }

        public Texture2D LoadTexture(string assetName)
            => Content.Load<Texture2D>(assetName);

        public bool TryLoadTexture(string assetName, out Texture2D texture)
        {
            try
            {
                texture = LoadTexture(assetName);
                return true;
            }
            catch
            {
                texture = null;
                return false;
            }
        }
    }
}