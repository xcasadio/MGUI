using Microsoft.Xna.Framework;

namespace MGUI.Shared.Assets
{
    public interface IUIAssetProvider
    {
        IUIImageResource LoadImage(string assetName);
        bool TryLoadImage(string assetName, out IUIImageResource image);

        /// <summary>Resolves a name that is unknown to <c>MGUI.Core.UI.MGResources</c>'s own texture dictionary into an image and an
        /// optional source rectangle within it, so a host whose images are catalogued assets (for example a sprite: a sheet texture
        /// plus a source rectangle) can let markup name them directly via <c>MGUI.Core.UI.MGImage.SourceName</c>, without registering
        /// every name up front through <see cref="TryLoadImage"/>.<para/>
        /// The default implementation resolves nothing, so existing implementers of <see cref="IUIAssetProvider"/> keep compiling
        /// unchanged (ADR-0016, "Host resolution of image names").</summary>
        /// <param name="name">The unresolved name, as referenced by <c>MGImage.SourceName</c>. Never null.</param>
        /// <param name="image">The resolved image, or null if <paramref name="name"/> could not be resolved.</param>
        /// <param name="sourceRect">An optional source rectangle within <paramref name="image"/>, or null to use the whole image.</param>
        bool TryResolveImage(string name, out IUIImageResource image, out Rectangle? sourceRect)
        {
            image = null;
            sourceRect = null;
            return false;
        }

        /// <summary>Creates a new, independent animation instance for a name the host recognizes as an animated image (ADR-0016,
        /// "Animated image sources"), so each <c>MGUI.Core.UI.MGImage</c> whose <c>SourceName</c> names it can play its own phase.<para/>
        /// The default implementation creates nothing, so existing implementers of <see cref="IUIAssetProvider"/> keep compiling
        /// unchanged.</summary>
        /// <param name="name">The name, as referenced by <c>MGImage.SourceName</c>. Never null.</param>
        /// <param name="animatedImage">A new, independent animation instance, or null if <paramref name="name"/> does not name an animation.</param>
        bool TryCreateAnimatedImage(string name, out IUIAnimatedImage animatedImage)
        {
            animatedImage = null;
            return false;
        }

        /// <summary>Creates a new, independent video instance for a name the host recognizes as a video (ADR-0039), so each
        /// <c>MGUI.Core.UI.MGVideo</c> whose <c>SourceName</c> names it can play its own copy.<para/>
        /// The default implementation creates nothing, so existing implementers of <see cref="IUIAssetProvider"/> keep compiling
        /// unchanged.</summary>
        /// <param name="name">The name, as referenced by <c>MGVideo.SourceName</c>. Never null.</param>
        /// <param name="video">A new, independent video instance that the caller disposes, or null if <paramref name="name"/> does not name a video.</param>
        bool TryCreateVideo(string name, out IUIVideo video)
        {
            video = null;
            return false;
        }
    }

}