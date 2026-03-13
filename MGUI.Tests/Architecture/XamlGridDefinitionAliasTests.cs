using System.ComponentModel;
using GridLength = MGUI.Core.UI.Containers.Grids.GridLength;
using XamlColumnDefinition = MGUI.Core.UI.XAML.ColumnDefinition;
using XamlRowDefinition = MGUI.Core.UI.XAML.RowDefinition;

namespace MGUI.Tests.Architecture;

public class XamlGridDefinitionAliasTests
{
    [Fact]
    public void ColumnDefinition_ExposesWidthAliasThatMapsToLength()
    {
        var converter = TypeDescriptor.GetConverter(typeof(GridLength));
        GridLength star = (GridLength)converter.ConvertFromInvariantString("2*")!;

        XamlColumnDefinition definition = new() { Width = star };

        Assert.Equal(star, definition.Length);
        Assert.Equal(star, definition.Width);
    }

    [Fact]
    public void RowDefinition_ExposesHeightAliasThatMapsToLength()
    {
        var converter = TypeDescriptor.GetConverter(typeof(GridLength));
        GridLength auto = (GridLength)converter.ConvertFromInvariantString("Auto")!;

        XamlRowDefinition definition = new() { Height = auto };

        Assert.Equal(auto, definition.Length);
        Assert.Equal(auto, definition.Height);
    }
}