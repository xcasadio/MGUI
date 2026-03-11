using System.Reflection;
using MGUI.Core.UI;
using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Tests.Architecture;

public class AssetProviderTests
{
    private sealed class FakeAssetProvider : IUIAssetProvider
    {
        public ContentManager Content => null!;
        public MGUI.Shared.Text.FontManager FontManager => null!;

        public Microsoft.Xna.Framework.Graphics.Texture2D LoadTexture(string assetName) => null!;

        public bool TryLoadTexture(string assetName, out Microsoft.Xna.Framework.Graphics.Texture2D texture)
        {
            texture = null!;
            return false;
        }
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

        Assert.Contains("Resources.AssetProvider.LoadTexture", desktopSource);
        Assert.Contains("Resources.AssetProvider.TryLoadTexture", desktopSource);
    }
}