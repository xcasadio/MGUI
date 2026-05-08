using MGUI.Core.UI;
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

        Assert.Equal(MGPropertyGridEditorKind.Bool, boolKind);
        Assert.Equal(MGPropertyGridEditorKind.Int, intKind);
        Assert.Equal(MGPropertyGridEditorKind.Float, floatKind);
        Assert.Equal(MGPropertyGridEditorKind.Double, doubleKind);
        Assert.Equal(MGPropertyGridEditorKind.String, stringKind);
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
}