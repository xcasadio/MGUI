using System.Reflection;
using MGUI.Core.UI;
using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.Tests.Architecture;

public class AssetProviderTests
{
    private sealed class FakeAssetProvider : IUIAssetProvider
    {
        public IUIImageResource LoadImage(string assetName) => null!;

        public bool TryLoadImage(string assetName, out IUIImageResource image)
        {
            image = null!;
            return false;
        }
    }

    [Fact]
    public void IUIAssetProvider_ExposesImageLoadingOnly()
    {
        Type contractType = typeof(IUIAssetProvider);
        string[] propertyNames = contractType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .OrderBy(x => x)
            .ToArray();
        string[] methodNames = contractType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(x => !x.IsSpecialName)
            .Select(x => x.Name)
            .OrderBy(x => x)
            .ToArray();

        Assert.Empty(propertyNames);
        Assert.Equal(new[] { nameof(IUIAssetProvider.LoadImage), nameof(IUIAssetProvider.TryLoadImage) }, methodNames);
    }

    [Fact]
    public void MainRenderer_ExposesAssetProviderProperty()
    {
        PropertyInfo? assetProviderProperty = typeof(MainRenderer).GetProperty(nameof(MainRenderer.AssetProvider), BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(assetProviderProperty);
        Assert.Equal(typeof(IUIAssetProvider), assetProviderProperty!.PropertyType);
    }

    [Fact]
    public void MGResources_CanBeConstructedWithMockableAssetProvider()
    {
        FakeAssetProvider assetProvider = new();
        MGTheme theme = new("Arial");

        MGResources resources = new(theme, assetProvider);

        Assert.Same(assetProvider, resources.AssetProvider);
    }

    [Fact]
    public void MGDesktop_DefaultResourceBootstrap_UsesAssetProvider()
    {
        string desktopSource = System.IO.File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGDesktop.cs");

        Assert.Contains("Resources.AssetProvider.LoadImage", desktopSource);
        Assert.Contains("Resources.AssetProvider.TryLoadImage", desktopSource);
    }

    [Fact]
    public void MGTextureData_UsesOpaqueImageResourceContractWithLegacyTextureCompatibility()
    {
        Assert.Equal(typeof(IUIImageResource), typeof(MGTextureData).GetProperty(nameof(MGTextureData.Image))!.PropertyType);
        Assert.Equal(typeof(Texture2D), typeof(MGTextureData).GetProperty(nameof(MGTextureData.Texture))!.PropertyType);
    }
}