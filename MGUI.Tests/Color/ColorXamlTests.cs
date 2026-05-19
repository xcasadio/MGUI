using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using System.ComponentModel;
using XamlColorField = MGUI.Core.UI.XAML.ColorField;
using XamlColorPaletteView = MGUI.Core.UI.XAML.ColorPaletteView;
using XamlElement = MGUI.Core.UI.XAML.Element;

namespace MGUI.Tests.ColorPicker;

public class ColorXamlTests
{
    [Fact]
    public void ColorValueStringConverter_ParsesHexRgba()
    {
        TypeConverter converter = TypeDescriptor.GetConverter(typeof(XAMLColorValue));

        XAMLColorValue value = (XAMLColorValue)converter.ConvertFromInvariantString("#3366CC80")!;

        Assert.Equal(new ColorValue(51f / 255f, 102f / 255f, 204f / 255f, 128f / 255f), value.ToColorValue());
    }

    [Fact]
    public void XamlParser_ParsesColorFieldElement()
    {
        XamlElement parsed = XAMLParser.ParseElementDefinition(
            XamlDocumentSource.FromString("<ColorField Value=\"#3366CC80\" ShowAlpha=\"True\" DisplayFormat=\"HexRgba\" />"),
            null,
            true,
            true);

        XamlColorField field = Assert.IsType<XamlColorField>(parsed);
        Assert.True(field.Value.HasValue);
        Assert.Equal(ColorValueFormat.HexRgba, field.DisplayFormat);
    }

    [Fact]
    public void XamlParser_ParsesColorPaletteViewElement()
    {
        XamlElement parsed = XAMLParser.ParseElementDefinition(
            XamlDocumentSource.FromString("<ColorPaletteView PaletteName=\"Project\" CommaSeparatedColors=\"#FF0000FF,#00FF00FF\" Columns=\"2\" />"),
            null,
            true,
            true);

        XamlColorPaletteView paletteView = Assert.IsType<XamlColorPaletteView>(parsed);
        Assert.Equal("Project", paletteView.PaletteName);
        Assert.Equal(2, paletteView.Columns);
    }
}