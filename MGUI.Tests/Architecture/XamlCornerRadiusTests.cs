using System.ComponentModel;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using XamlBorder = MGUI.Core.UI.XAML.Border;
using XamlButton = MGUI.Core.UI.XAML.Button;
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

    [Fact]
    public void ImplicitBorderStyles_DoNotLeakIntoTemplateBorderChildren()
    {
        Window window = new();
        window.Styles.Add(new Style
        {
            TargetType = MGElementType.Border,
            Setters = { new Setter { Property = nameof(XamlBorder.Padding), Value = "10" } }
        });

        XamlButton button = new();
        XamlBorder contentBorder = new();
        StackPanel content = new();
        content.Children.Add(button);
        content.Children.Add(contentBorder);
        window.Content = content;

        MGResources resources = new(new MGTheme("Arial"));
        typeof(Element)
            .GetMethod("ProcessStyles", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(MGResources) }, null)!
            .Invoke(window, new object[] { resources });

        Assert.Null(button.Border.Padding);
        Assert.Equal(new Thickness(10), contentBorder.Padding);
    }
}