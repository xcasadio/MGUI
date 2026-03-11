using MGUI.Core.UI;

namespace MGUI.Tests.Architecture;

public class ResourceSeparationTests
{
    [Fact]
    public void MGResources_ExposesDefinitionAndRuntimeViews()
    {
        MGResources resources = new(new MGTheme("Arial"));

        Assert.NotNull(resources.Definitions);
        Assert.NotNull(resources.RuntimeCache);
    }

    [Fact]
    public void DefinitionView_ExposesNamedDefinitions()
    {
        MGResources resources = new(new MGTheme("Arial"));

        Assert.Same(resources.Themes, resources.Definitions.Themes);
        Assert.Same(resources.Styles, resources.Definitions.Styles);
        Assert.Same(resources.Commands, resources.Definitions.Commands);
        Assert.Same(resources.StaticResources, resources.Definitions.StaticResources);
    }

    [Fact]
    public void RuntimeCacheView_ExposesTextureCache()
    {
        MGResources resources = new(new MGTheme("Arial"));

        Assert.Same(resources.Textures, resources.RuntimeCache.Textures);
    }
}