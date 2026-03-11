using System.ComponentModel;
using MGUI.Core.UI;
using XamlBorder = MGUI.Core.UI.XAML.Border;
using XamlCornerRadius = MGUI.Core.UI.XAML.CornerRadius;
using XamlRectangle = MGUI.Core.UI.XAML.Rectangle;

namespace MGUI.Tests.Architecture;

public class XamlCornerRadiusTests
{
    [Fact]
    public void CornerRadiusConverter_ParsesUniformAndPerCornerSyntax()
    {
        var converter = TypeDescriptor.GetConverter(typeof(XamlCornerRadius));

        var uniform = (XamlCornerRadius)converter.ConvertFromInvariantString("6")!;
        var perCorner = (XamlCornerRadius)converter.ConvertFromInvariantString("1,2,3,4")!;

        Assert.Equal(new MGCornerRadius(6), uniform.ToCornerRadius());
        Assert.Equal(new MGCornerRadius(1, 2, 3, 4), perCorner.ToCornerRadius());
    }

    [Fact]
    public void XamlBorderAndRectangle_ExposeCornerRadiusProperty()
    {
        Assert.Equal(typeof(XamlCornerRadius?), typeof(XamlBorder).GetProperty("CornerRadius")!.PropertyType);
        Assert.Equal(typeof(XamlCornerRadius?), typeof(XamlRectangle).GetProperty("CornerRadius")!.PropertyType);
    }
}