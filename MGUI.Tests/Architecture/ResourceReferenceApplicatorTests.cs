using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Styling;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Portable.Xaml.Markup;
using System;
using System.Reflection;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

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

    [Fact]
    public void Reapplying_Same_Reference_Twice_Does_Not_Duplicate_Updates()
    {
        MGDesktop desktop = CreateHeadlessDesktop();
        MGWindow window = new(desktop, 0, 0, 240, 200);
        desktop.Windows.Add(window);

        MGResources scope = window.GetResources();
        scope.AddStaticResource("Accent", 1);

        CountingTarget target = new();
        UIResourceReferenceConfig config = new(nameof(CountingTarget.Value), "Accent", true);

        Assert.True(UIResourceReferenceApplicator.Apply(window, target, config, scope));
        Assert.True(UIResourceReferenceApplicator.Apply(window, target, config, scope));
        int setCountAfterRegistration = target.SetCount;

        scope.SetStaticResource("Accent", 2);

        Assert.Equal(setCountAfterRegistration + 1, target.SetCount);
        Assert.Equal(2, target.Value);
    }

    [Fact]
    public void RemovingHostFromParent_DetachesSubscriptionFromThatScope()
    {
        MGDesktop desktop = CreateHeadlessDesktop();
        MGWindow window = new(desktop, 0, 0, 240, 200);
        desktop.Windows.Add(window);

        MGStackPanel containerA = new(window, Orientation.Vertical);
        containerA.EnsureResourceScope(UIResourceScope.Subtree).AddStaticResource("Accent", 1);
        window.SetContent(containerA);

        MGTextBlock child = new(window, string.Empty);
        containerA.TryAddChild(child);

        MGResources scopeA = child.GetResources();
        Assert.Same(containerA.LocalResources, scopeA);
        int subscribersBeforeRegistration = scopeA.StaticResourceLookupSubscriberCount;
        MGResources windowScope = window.GetResources();
        int windowSubscribersBeforeRegistration = windowScope.StaticResourceLookupSubscriberCount;

        CountingTarget target = new();
        UIResourceReferenceConfig config = new(nameof(CountingTarget.Value), "Accent", true);
        Assert.True(UIResourceReferenceApplicator.Apply(child, target, config, scopeA));
        Assert.Equal(1, target.Value);
        Assert.Equal(subscribersBeforeRegistration + 1, scopeA.StaticResourceLookupSubscriberCount);

        containerA.TryRemoveChild(child);

        Assert.Equal(subscribersBeforeRegistration, scopeA.StaticResourceLookupSubscriberCount);
        // A removed element must not fall back onto its window's scope either: that would keep a strong handler there for as long as the window lives.
        Assert.Equal(windowSubscribersBeforeRegistration, windowScope.StaticResourceLookupSubscriberCount);

        int setCountAfterDetach = target.SetCount;
        scopeA.SetStaticResource("Accent", 2);

        Assert.Equal(setCountAfterDetach, target.SetCount);
        Assert.Equal(1, target.Value);
    }

    [Fact]
    public void ReparentingUnderAnotherScope_ReResolvesAndStopsFollowingThePreviousChain()
    {
        MGDesktop desktop = CreateHeadlessDesktop();
        MGWindow window = new(desktop, 0, 0, 240, 200);
        desktop.Windows.Add(window);

        MGStackPanel root = new(window, Orientation.Vertical);
        window.SetContent(root);

        MGStackPanel containerA = new(window, Orientation.Vertical);
        containerA.EnsureResourceScope(UIResourceScope.Subtree).AddStaticResource("Accent", 1);
        root.TryAddChild(containerA);

        MGStackPanel containerB = new(window, Orientation.Vertical);
        containerB.EnsureResourceScope(UIResourceScope.Subtree).AddStaticResource("Accent", 100);
        root.TryAddChild(containerB);

        MGTextBlock child = new(window, string.Empty);
        containerA.TryAddChild(child);

        CountingTarget target = new();
        UIResourceReferenceConfig config = new(nameof(CountingTarget.Value), "Accent", true);
        Assert.True(UIResourceReferenceApplicator.Apply(child, target, config, child.GetResources()));
        Assert.Equal(1, target.Value);

        MGResources scopeA = containerA.LocalResources;
        MGResources scopeB = containerB.LocalResources;

        Assert.True(containerA.TryRemoveChild(child));
        Assert.True(containerB.TryAddChild(child));

        Assert.Equal(100, target.Value);

        scopeB.SetStaticResource("Accent", 200);
        Assert.Equal(200, target.Value);

        scopeA.SetStaticResource("Accent", 999);
        Assert.Equal(200, target.Value);
    }

    [Fact]
    public void MGResources_ForwardsStaticResourceLookupChanged_FromParent_AndStopsWhenReparented()
    {
        MGResources root = new(new MGTheme("Arial"));
        MGResources child = new(root, UIResourceScope.Subtree);

        int callCount = 0;
        string lastName = null;
        child.OnStaticResourceLookupChanged += (_, name) =>
        {
            callCount++;
            lastName = name;
        };

        root.AddStaticResource("Accent", 1);
        Assert.Equal(1, callCount);
        Assert.Equal("Accent", lastName);

        root.SetStaticResource("Accent", 2);
        Assert.Equal(2, callCount);

        child.SetParent(null);

        root.SetStaticResource("Accent", 3);
        Assert.Equal(2, callCount);
    }

    private static MGDesktop CreateHeadlessDesktop()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        return new MGDesktop(runtime);
    }

    private sealed class CountingTarget
    {
        private int _value;
        public int SetCount { get; private set; }
        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                SetCount++;
            }
        }
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