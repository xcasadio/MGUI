using MGUI.Core.UI.DataBinding;
using MGUI.Core.UI.Styling;
using System.Reflection;
using MGUI.Core.UI.DataBinding;

#if UseWPF
using System.Windows.Markup;
#else
using Portable.Xaml.Markup;
#endif

namespace MGUI.Core.UI.XAML;

public abstract class ResourceReferenceExtension : MarkupExtension
{
    public string ResourceName { get; set; }

    protected abstract bool IsDynamic { get; }

    protected ResourceReferenceExtension() { }
    protected ResourceReferenceExtension(string ResourceName)
    {
        this.ResourceName = ResourceName;
    }

    public override object ProvideValue(IServiceProvider Provider)
    {
        IProvideValueTarget ProvideValueTarget = Provider as IProvideValueTarget ?? Provider?.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;

        object RawTargetObject = ProvideValueTarget?.TargetObject;
        object RawTargetProperty = ProvideValueTarget?.TargetProperty;
        if (RawTargetObject == null || RawTargetProperty == null)
        {
            RawTargetObject = Provider?.GetType().GetProperty(nameof(IProvideValueTarget.TargetObject))?.GetValue(Provider);
            RawTargetProperty = Provider?.GetType().GetProperty(nameof(IProvideValueTarget.TargetProperty))?.GetValue(Provider);
        }

        if (RawTargetProperty is PropertyInfo TargetProperty && RawTargetObject is XAMLBindableBase TargetObject)
        {
            TargetObject.ResourceReferences.Add(new UIResourceReferenceConfig(TargetProperty.Name, ResourceName, IsDynamic));
            return GetDefaultValue(TargetProperty.PropertyType);
        }

        throw new NotImplementedException($"Cannot provide a value when the underlying Type is unknown. {GetType().Name} is only supported on XAML bindable objects.");
    }

    private static object GetDefaultValue(Type Type) => Type.IsValueType ? Activator.CreateInstance(Type) : null;
}

public class StaticResource : ResourceReferenceExtension
{
    protected override bool IsDynamic => false;

    public StaticResource() { }
    public StaticResource(string ResourceName) : base(ResourceName) { }
}

public class DynamicResource : ResourceReferenceExtension
{
    protected override bool IsDynamic => true;

    public DynamicResource() { }
    public DynamicResource(string ResourceName) : base(ResourceName) { }
}