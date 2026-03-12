using System.ComponentModel;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.XAML;

namespace MGUI.Tests.Architecture;

public class XamlGridDefinitionAliasTests
{
    [Fact]
    public void ColumnDefinition_ExposesWidthAliasThatMapsToLength()
    {
        var converter = TypeDescriptor.GetConverter(typeof(GridLength));
        GridLength star = (GridLength)converter.ConvertFromInvariantString("2*")!;

        ColumnDefinition definition = new() { Width = star };

        Assert.Equal(star, definition.Length);
        Assert.Equal(star, definition.Width);
    }

    [Fact]
    public void RowDefinition_ExposesHeightAliasThatMapsToLength()
    {
        var converter = TypeDescriptor.GetConverter(typeof(GridLength));
        GridLength auto = (GridLength)converter.ConvertFromInvariantString("Auto")!;

        RowDefinition definition = new() { Height = auto };

        Assert.Equal(auto, definition.Length);
        Assert.Equal(auto, definition.Height);
    }
}