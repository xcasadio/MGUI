using MGUI.Core.UI;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVector4 = Microsoft.Xna.Framework.Vector4;
using System.ComponentModel;

namespace MGUI.Tests.PropertyGrid;

public class PropertyGridDescriptorTests
{
    private sealed class SupportedPropertyGridModel
    {
        [Category("Transform")]
        [DisplayName("Position X")]
        public int PositionX { get; set; }

        [Category("Rendering")]
        public bool Visible { get; set; }

        public float Opacity { get; set; }
        public double Rotation { get; set; }
        public string Name { get; set; } = string.Empty;
        public XnaColor Tint { get; set; }
        public XnaVector4 Emissive { get; set; }

        public object? UnsupportedObject { get; set; }

        [ReadOnly(true)]
        public string ReadOnlyName { get; set; } = string.Empty;

        public string GetOnlyName => "ReadOnly";
    }

    [Fact]
    public void DescriptorCache_Maps_Supported_Types_To_Expected_Editors()
    {
        Assert.True(MGPropertyGridDescriptorCache.TryGetEditorKind(typeof(bool), out MGPropertyGridEditorKind boolKind));
        Assert.True(MGPropertyGridDescriptorCache.TryGetEditorKind(typeof(int), out MGPropertyGridEditorKind intKind));
        Assert.True(MGPropertyGridDescriptorCache.TryGetEditorKind(typeof(float), out MGPropertyGridEditorKind floatKind));
        Assert.True(MGPropertyGridDescriptorCache.TryGetEditorKind(typeof(double), out MGPropertyGridEditorKind doubleKind));
        Assert.True(MGPropertyGridDescriptorCache.TryGetEditorKind(typeof(string), out MGPropertyGridEditorKind stringKind));
        Assert.True(MGPropertyGridDescriptorCache.TryGetEditorKind(typeof(XnaColor), out MGPropertyGridEditorKind colorKind));
        Assert.True(MGPropertyGridDescriptorCache.TryGetEditorKind(typeof(XnaVector4), out MGPropertyGridEditorKind vectorColorKind));

        Assert.Equal(MGPropertyGridEditorKind.Bool, boolKind);
        Assert.Equal(MGPropertyGridEditorKind.Int, intKind);
        Assert.Equal(MGPropertyGridEditorKind.Float, floatKind);
        Assert.Equal(MGPropertyGridEditorKind.Double, doubleKind);
        Assert.Equal(MGPropertyGridEditorKind.String, stringKind);
        Assert.Equal(MGPropertyGridEditorKind.Color, colorKind);
        Assert.Equal(MGPropertyGridEditorKind.Color, vectorColorKind);
    }

    [Fact]
    public void DescriptorCache_Excludes_Unsupported_Types()
    {
        MGPropertyGridDescriptorCache.Clear();

        IReadOnlyList<MGPropertyGridDescriptor> descriptors = MGPropertyGridDescriptorCache.GetDescriptors(typeof(SupportedPropertyGridModel));

        Assert.DoesNotContain(descriptors, x => x.Name == nameof(SupportedPropertyGridModel.UnsupportedObject));
    }

    [Fact]
    public void DescriptorCache_Reuses_Cached_Descriptor_List_Per_Type()
    {
        MGPropertyGridDescriptorCache.Clear();

        IReadOnlyList<MGPropertyGridDescriptor> first = MGPropertyGridDescriptorCache.GetDescriptors(typeof(SupportedPropertyGridModel));
        IReadOnlyList<MGPropertyGridDescriptor> second = MGPropertyGridDescriptorCache.GetDescriptors(typeof(SupportedPropertyGridModel));

        Assert.Same(first, second);
    }

    [Fact]
    public void DescriptorCache_Uses_Category_DisplayName_And_ReadOnly_Metadata()
    {
        MGPropertyGridDescriptorCache.Clear();

        IReadOnlyList<MGPropertyGridDescriptor> descriptors = MGPropertyGridDescriptorCache.GetDescriptors(typeof(SupportedPropertyGridModel));

        MGPropertyGridDescriptor positionX = Assert.Single(descriptors, x => x.Name == nameof(SupportedPropertyGridModel.PositionX));
        MGPropertyGridDescriptor readOnlyName = Assert.Single(descriptors, x => x.Name == nameof(SupportedPropertyGridModel.ReadOnlyName));
        MGPropertyGridDescriptor getOnlyName = Assert.Single(descriptors, x => x.Name == nameof(SupportedPropertyGridModel.GetOnlyName));

        Assert.Equal("Transform", positionX.Category);
        Assert.Equal("Position X", positionX.DisplayName);
        Assert.True(readOnlyName.IsReadOnly);
        Assert.Null(readOnlyName.Setter);
        Assert.True(getOnlyName.IsReadOnly);
        Assert.Null(getOnlyName.Setter);
    }

    [Fact]
    public void ColorAdapter_RoundTrips_XnaColor_And_Vector4()
    {
        XnaColor color = new(51, 102, 204, 128);

        Assert.True(PropertyGridColorAdapter.TryToColorValue(color, out ColorValue colorValue));
        object roundTripColor = PropertyGridColorAdapter.ToPropertyValue(colorValue, typeof(XnaColor));

        Assert.Equal(color, roundTripColor);

        XnaVector4 vector = new(0.2f, 0.4f, 0.8f, 0.5f);
        Assert.True(PropertyGridColorAdapter.TryToColorValue(vector, out ColorValue vectorValue));
        object roundTripVector = PropertyGridColorAdapter.ToPropertyValue(vectorValue, typeof(XnaVector4));

        Assert.Equal(vector, roundTripVector);
    }
}