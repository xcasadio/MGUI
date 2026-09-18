using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Styling;
using MGUI.Editor.Document;

namespace MGUI.Editor.Properties;

/// <summary>Describes one selected <see cref="XamlDocumentNode"/> to <see cref="MGPropertyGrid"/> (<c>Docs/editor-architecture.md</c>,
/// <c>## Grille de proprietes</c>). Every announced <see cref="PropertyDescriptor.PropertyType"/> is <see cref="string"/>, because
/// <c>MGPropertyGridDescriptorCache.TryGetEditorKind</c> silently drops any descriptor whose <see cref="PropertyDescriptor.PropertyType"/>
/// it does not recognise (an <c>int?</c>, a <c>Thickness?</c>, an enum): announcing <see cref="string"/> is what keeps every row.<para/>
/// Two categories of rows:<list type="bullet">
/// <item>one row per public, read/write, non-<see cref="BrowsableAttribute"/>(false), non-collection instance property of
/// <see cref="Node"/>'s <see cref="XamlDocumentNode.DtoType"/> whose declared type has a <see cref="TypeConverter"/> that converts from
/// <see cref="string"/> -- the value is the attribute string as written in the document, or <see cref="string.Empty"/> when the
/// attribute is absent;</item>
/// <item>when <see cref="RepresentativeElement"/> is not null, one read-only row per entry of
/// <see cref="UIToolingService.CaptureElementDebugView(MGElement)"/>'s <c>ValueOrigins</c>, under the <c>"Resolved (runtime)"</c>
/// category, its descriptor name prefixed <c>Resolved.</c> so it never collides with a DTO row of the same path (<c>Padding</c>).</item>
/// </list>
/// Every row is read-only in this slice (X5); X6 flips the general case using <see cref="IsEditable"/>.<para/>
/// <see cref="Update"/> mutates this same instance for a re-parse or a representative change that keeps the same node identity: the
/// row set built by the constructor never changes shape afterwards, only the values <see cref="PropertyDescriptor.GetValue"/> reads
/// live from <see cref="Node"/> and <see cref="RepresentativeElement"/> change. A different node identity is a brand-new instance,
/// assigned to <see cref="MGPropertyGrid.SelectedObject"/>, which is what makes the grid rebuild
/// (<c>MGPropertyGrid.SelectedObject</c>'s setter rebuilds for any new <see cref="ICustomTypeDescriptor"/> instance).</summary>
public sealed class XamlNodePropertySource : ICustomTypeDescriptor
{
    /// <summary>The category every "Resolved (runtime)" row is filed under.</summary>
    public const string ResolvedCategory = "Resolved (runtime)";

    /// <summary>The prefix a "Resolved (runtime)" row's descriptor <see cref="PropertyDescriptor.Name"/> carries, so it cannot collide
    /// with a DTO row of the same property path (<c>Padding</c> exists on both sides).</summary>
    public const string ResolvedNamePrefix = "Resolved.";

    private readonly PropertyDescriptorCollection _properties;

    /// <summary>The selected document node this instance describes.</summary>
    public XamlDocumentNode Node { get; private set; }

    /// <summary>The element in the preview that represents <see cref="Node"/>, or null when the preview has none currently.</summary>
    public MGElement RepresentativeElement { get; private set; }

    public XamlNodePropertySource(XamlDocumentNode node, MGElement representativeElement)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        RepresentativeElement = representativeElement;
        _properties = BuildProperties(Node, RepresentativeElement);
    }

    /// <summary>Mutates this same instance for a re-parse or a representative change that keeps the same node identity (same
    /// <see cref="XamlDocumentNode.Ordinal"/>): the set of rows built by the constructor is not rebuilt, only the values the existing
    /// descriptors read live from <see cref="Node"/> and <see cref="RepresentativeElement"/> change.</summary>
    public void Update(XamlDocumentNode node, MGElement representativeElement)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        RepresentativeElement = representativeElement;
    }

    /// <summary><see langword="true"/> when <paramref name="propertyName"/> is declared as an attribute of <see cref="Node"/> (or is not
    /// declared anywhere yet), <see langword="false"/> when it is permanently not editable because the document declares it as a child
    /// element: either explicit property-element syntax (a child node whose <see cref="XamlDocumentNode.IsPropertyElement"/> is true and
    /// whose name is <c>&lt;Owner&gt;.&lt;Property&gt;</c>), or, for the DTO's content property, content supplied as a plain child
    /// object element. Every row is read-only in this slice regardless; X6 uses this to flip the general case.</summary>
    public bool IsEditable(string propertyName)
    {
        if (propertyName == null)
        {
            throw new ArgumentNullException(nameof(propertyName));
        }

        foreach (var attribute in Node.Attributes)
        {
            if (string.Equals(attribute.Name, propertyName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        var propertyElementName = $"{Node.LocalName}.{propertyName}";
        foreach (var child in Node.Children)
        {
            if (child.IsPropertyElement && string.Equals(child.LocalName, propertyElementName, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (Node.DtoType != null && string.Equals(GetContentPropertyName(Node.DtoType), propertyName, StringComparison.Ordinal))
        {
            foreach (var child in Node.Children)
            {
                if (!child.IsPropertyElement)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static PropertyDescriptorCollection BuildProperties(XamlDocumentNode node, MGElement representativeElement)
    {
        List<PropertyDescriptor> descriptors = new();

        if (node.DtoType != null)
        {
            foreach (var property in node.DtoType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!IsEligibleDtoProperty(property))
                {
                    continue;
                }

                var category = property.GetCustomAttribute<CategoryAttribute>()?.Category;
                var displayName = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
                descriptors.Add(new DtoRowDescriptor(
                    property,
                    string.IsNullOrWhiteSpace(category) ? "Misc" : category,
                    string.IsNullOrWhiteSpace(displayName) ? property.Name : displayName));
            }
        }

        if (representativeElement != null)
        {
            var view = UIToolingService.CaptureElementDebugView(representativeElement);
            foreach (var origin in view.ValueOrigins)
            {
                descriptors.Add(new ResolvedRowDescriptor(origin.PropertyPath));
            }
        }

        return new PropertyDescriptorCollection(descriptors.ToArray(), true);
    }

    private static bool IsEligibleDtoProperty(PropertyInfo property)
    {
        if (property.GetIndexParameters().Length > 0)
        {
            return false;
        }

        var getter = property.GetMethod;
        var setter = property.SetMethod;
        if (getter is not { IsPublic: true } || setter is not { IsPublic: true })
        {
            return false;
        }

        if (property.GetCustomAttribute<BrowsableAttribute>() is { Browsable: false })
        {
            return false;
        }

        if (IsCollectionOrDictionary(property.PropertyType))
        {
            return false;
        }

        return TypeDescriptor.GetConverter(property.PropertyType).CanConvertFrom(typeof(string));
    }

    private static bool IsCollectionOrDictionary(Type type)
        => type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    /// <summary>The name of <paramref name="dtoType"/>'s content property (the WPF/Portable.Xaml <c>ContentPropertyAttribute</c>,
    /// e.g. <c>Content</c> on <see cref="MGUI.Core.UI.XAML.Containers.SingleContentHost"/>), walking base types since the attribute is
    /// inherited. Read through <see cref="MemberInfo.GetCustomAttributesData"/> by attribute type name so this class needs no
    /// compile-time reference to whichever markup assembly declares it.</summary>
    private static string GetContentPropertyName(Type dtoType)
    {
        for (var type = dtoType; type != null; type = type.BaseType)
        {
            foreach (var data in type.GetCustomAttributesData())
            {
                if (data.AttributeType.Name == "ContentPropertyAttribute" && data.ConstructorArguments.Count > 0)
                {
                    return data.ConstructorArguments[0].Value as string;
                }
            }
        }

        return null;
    }

    private static string FormatSource(UIValueResolutionSource source)
        => string.IsNullOrEmpty(source.Name) ? source.Kind.ToString() : $"{source.Kind} '{source.Name}'";

    /// <summary>One row built from a DTO property: value = the attribute string as written in the document (never null), category and
    /// display name from the DTO property, <see cref="PropertyType"/> always announced as <see cref="string"/> regardless of the
    /// property's real type (kept on <see cref="_property"/> for X6's validation, never announced).</summary>
    private sealed class DtoRowDescriptor : PropertyDescriptor
    {
        private readonly PropertyInfo _property;

        public DtoRowDescriptor(PropertyInfo property, string category, string displayName)
            : base(property.Name, new Attribute[] { new CategoryAttribute(category), new DisplayNameAttribute(displayName) })
        {
            _property = property;
        }

        public override Type ComponentType => typeof(XamlNodePropertySource);
        public override bool IsReadOnly => true;
        public override Type PropertyType => typeof(string);

        public override object GetValue(object component)
        {
            var source = (XamlNodePropertySource)component;
            foreach (var attribute in source.Node.Attributes)
            {
                if (string.Equals(attribute.Name, _property.Name, StringComparison.Ordinal))
                {
                    return attribute.Value;
                }
            }

            return string.Empty;
        }

        public override void SetValue(object component, object value)
            => throw new NotSupportedException($"'{_property.Name}' is read-only: writing arrives in a later slice.");

        public override bool CanResetValue(object component) => false;
        public override void ResetValue(object component) { }
        public override bool ShouldSerializeValue(object component) => false;
    }

    /// <summary>One row of the read-only "Resolved (runtime)" category: value = the repository's own one-line rendering of a
    /// <see cref="UIValueOriginView"/> (<see cref="UIToolingService.RenderElementDebugView"/>'s own shape), captured live from
    /// <see cref="XamlNodePropertySource.RepresentativeElement"/> on every <see cref="GetValue"/>.</summary>
    private sealed class ResolvedRowDescriptor : PropertyDescriptor
    {
        private readonly string _propertyPath;

        public ResolvedRowDescriptor(string propertyPath)
            : base(ResolvedNamePrefix + propertyPath, new Attribute[] { new CategoryAttribute(ResolvedCategory), new DisplayNameAttribute(propertyPath) })
        {
            _propertyPath = propertyPath;
        }

        public override Type ComponentType => typeof(XamlNodePropertySource);
        public override bool IsReadOnly => true;
        public override Type PropertyType => typeof(string);

        public override object GetValue(object component)
        {
            var source = (XamlNodePropertySource)component;
            var element = source.RepresentativeElement;
            if (element == null)
            {
                return string.Empty;
            }

            var view = UIToolingService.CaptureElementDebugView(element);
            foreach (var origin in view.ValueOrigins)
            {
                if (string.Equals(origin.PropertyPath, _propertyPath, StringComparison.Ordinal))
                {
                    var effective = origin.EffectiveValue ?? "<none>";
                    var resolvedSource = origin.IsResolved ? FormatSource(origin.Source) : "<not resolved>";
                    return $"{effective} <- {resolvedSource}";
                }
            }

            return string.Empty;
        }

        public override void SetValue(object component, object value) => throw new NotSupportedException($"'{Name}' is read-only.");
        public override bool CanResetValue(object component) => false;
        public override void ResetValue(object component) { }
        public override bool ShouldSerializeValue(object component) => false;
    }

    //  ICustomTypeDescriptor: every member delegates to TypeDescriptor.GetX(this, true) except GetProperties(), the whole point of
    //  this class, and GetProperties(Attribute[]), which must return the same thing (X6's validation does not filter by attribute).
    public AttributeCollection GetAttributes() => TypeDescriptor.GetAttributes(this, true);
    public string GetClassName() => TypeDescriptor.GetClassName(this, true);
    public string GetComponentName() => TypeDescriptor.GetComponentName(this, true);
    public TypeConverter GetConverter() => TypeDescriptor.GetConverter(this, true);
    public EventDescriptor GetDefaultEvent() => TypeDescriptor.GetDefaultEvent(this, true);
    public PropertyDescriptor GetDefaultProperty() => TypeDescriptor.GetDefaultProperty(this, true);
    public object GetEditor(Type editorBaseType) => TypeDescriptor.GetEditor(this, editorBaseType, true);
    public EventDescriptorCollection GetEvents() => TypeDescriptor.GetEvents(this, true);
    public EventDescriptorCollection GetEvents(Attribute[] attributes) => TypeDescriptor.GetEvents(this, attributes, true);
    public PropertyDescriptorCollection GetProperties() => _properties;
    public PropertyDescriptorCollection GetProperties(Attribute[] attributes) => _properties;
    public object GetPropertyOwner(PropertyDescriptor pd) => this;
}
