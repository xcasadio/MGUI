namespace MGUI.Shared.Assets
{
    public interface IUIAssetProvider
    {
        IUIImageResource LoadImage(string assetName);
        bool TryLoadImage(string assetName, out IUIImageResource image);
    }

}