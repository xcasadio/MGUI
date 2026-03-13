using MGUI.Core.UI;
using MGUI.Core.UI.Styling;
using MGUI.Core.UI.XAML;
using Portable.Xaml.Markup;
using System;
using System.Reflection;

namespace MGUI.Tests.Architecture;

public class ResourceReferenceApplicatorTests
{
    [Fact]
    public void Static_Resource_Is_Applied_Once()
    {
        MGResources resources = new(new MGTheme("Arial"));
        resources.AddStaticResource("Accent", 5);

        TestTarget target = new();
        UIResourceReferenceConfig config = new(nameof(TestTarget.Value), "Accent", false);

        Assert.True(UIResourceReferenceApplicator.Apply(null, target, config, resources));
        Assert.Equal(5, target.Value);

        resources.SetStaticResource("Accent", 9);

        Assert.Equal(5, target.Value);
    }

    [Fact]
    public void Dynamic_Resource_Reapplies_On_Override_And_Fallback_Changes()
    {
        MGResources desktop = new(new MGTheme("Arial"));
        desktop.AddStaticResource("Accent", 5);

        MGResources subtree = new(desktop, UIResourceScope.Subtree);
        TestTarget target = new();
        UIResourceReferenceConfig config = new(nameof(TestTarget.Value), "Accent", true);

        Assert.True(UIResourceReferenceApplicator.Apply(null, target, config, subtree));
        Assert.Equal(5, target.Value);

        desktop.SetStaticResource("Accent", 6);
        Assert.Equal(6, target.Value);

        subtree.SetStaticResource("Accent", 7);
        Assert.Equal(7, target.Value);

        Assert.True(subtree.RemoveStaticResource("Accent"));
        Assert.Equal(6, target.Value);
    }

    [Fact]
    public void Dynamic_Resource_Can_Update_Nested_Target_Path()
    {
        MGResources resources = new(new MGTheme("Arial"));
        resources.AddStaticResource("Accent", 5);

        TestTarget target = new();
        UIResourceReferenceConfig config = new($"{nameof(TestTarget.Nested)}.{nameof(TestNestedTarget.Value)}", "Accent", true);

        Assert.True(UIResourceReferenceApplicator.Apply(null, target, config, resources));
        Assert.Equal(5, target.Nested.Value);

        resources.SetStaticResource("Accent", 8);
        Assert.Equal(8, target.Nested.Value);
    }

    [Fact]
    public void Markup_Extension_Stores_Resource_Reference_On_Xaml_Target()
    {
        SolidFillBrush brush = new();
        DynamicResource resource = new("Accent");
        PropertyInfo? targetProperty = typeof(SolidFillBrush).GetProperty(nameof(SolidFillBrush.Color));
        Assert.NotNull(targetProperty);

        object provided = resource.ProvideValue(new TestServiceProvider(brush, targetProperty!));

        Assert.Equal(new XAMLColor(), Assert.IsType<XAMLColor>(provided));
        UIResourceReferenceConfig reference = Assert.Single(brush.ResourceReferences);
        Assert.Equal(nameof(SolidFillBrush.Color), reference.TargetPath);
        Assert.Equal("Accent", reference.ResourceName);
        Assert.True(reference.IsDynamic);
    }

    private sealed class TestTarget
    {
        public int Value { get; set; }
        public TestNestedTarget Nested { get; } = new();
    }

    private sealed class TestNestedTarget
    {
        public int Value { get; set; }
    }

    private sealed class TestServiceProvider : IServiceProvider, IProvideValueTarget
    {
        public object TargetObject { get; }
        public object TargetProperty { get; }

        public TestServiceProvider(object targetObject, PropertyInfo targetProperty)
        {
            TargetObject = targetObject;
            TargetProperty = targetProperty;
        }

        public object GetService(Type serviceType)
            => serviceType == typeof(IProvideValueTarget) || serviceType?.FullName == typeof(IProvideValueTarget).FullName ? this : null!;
    }
}