using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Text;

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

}