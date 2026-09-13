using System.ComponentModel;
using System.Reflection;

namespace MGUI.Core.UI;

internal static class MGPropertyGridDescriptorCache
{
    private static readonly Dictionary<Type, IReadOnlyList<MGPropertyGridDescriptor>> Cache = new();

    internal static void Clear()
    {
        Cache.Clear();
    }

    internal static IReadOnlyList<MGPropertyGridDescriptor> GetDescriptors(Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (!Cache.TryGetValue(type, out var descriptors))
        {
            descriptors = BuildDescriptors(type);
            Cache[type] = descriptors;
        }

        return descriptors;
    }

    internal static IReadOnlyList<MGPropertyGridDescriptor> GetDescriptors(object instance)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        if (instance is ICustomTypeDescriptor)
        {
            return BuildDescriptors(TypeDescriptor.GetProperties(instance));
        }

        return GetDescriptors(instance.GetType());
    }

    internal static bool TryGetEditorKind(Type propertyType, out MGPropertyGridEditorKind editorKind)
    {
        if (PropertyGridColorAdapter.IsSupportedColorType(propertyType))
        {
            editorKind = MGPropertyGridEditorKind.Color;
            return true;
        }

        if (propertyType == typeof(bool))
        {
            editorKind = MGPropertyGridEditorKind.Bool;
            return true;
        }

        if (propertyType == typeof(byte)
            || propertyType == typeof(sbyte)
            || propertyType == typeof(short)
            || propertyType == typeof(ushort)
            || propertyType == typeof(int)
            || propertyType == typeof(uint)
            || propertyType == typeof(long)
            || propertyType == typeof(ulong))
        {
            editorKind = MGPropertyGridEditorKind.Int;
            return true;
        }

        if (propertyType == typeof(float))
        {
            editorKind = MGPropertyGridEditorKind.Float;
            return true;
        }

        if (propertyType == typeof(double))
        {
            editorKind = MGPropertyGridEditorKind.Double;
            return true;
        }

        if (propertyType == typeof(string))
        {
            editorKind = MGPropertyGridEditorKind.String;
            return true;
        }

        editorKind = default;
        return false;
    }

    private static IReadOnlyList<MGPropertyGridDescriptor> BuildDescriptors(Type type)
    {
        List<MGPropertyGridDescriptor> result = new();

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var getter = property.GetMethod;
            if (getter == null || !getter.IsPublic)
            {
                continue;
            }

            if (!TryGetEditorKind(property.PropertyType, out var editorKind))
            {
                continue;
            }

            var setter = property.SetMethod;
            var readOnlyAttribute = property.GetCustomAttribute<ReadOnlyAttribute>();
            var displayNameAttribute = property.GetCustomAttribute<DisplayNameAttribute>();
            var categoryAttribute = property.GetCustomAttribute<CategoryAttribute>();

            var isReadOnly = setter == null || !setter.IsPublic || readOnlyAttribute?.IsReadOnly == true;

            result.Add(new MGPropertyGridDescriptor
            {
                Name = property.Name,
                DisplayName = string.IsNullOrWhiteSpace(displayNameAttribute?.DisplayName) ? property.Name : displayNameAttribute.DisplayName,
                Category = string.IsNullOrWhiteSpace(categoryAttribute?.Category) ? "Misc" : categoryAttribute.Category,
                PropertyType = property.PropertyType,
                EditorKind = editorKind,
                Getter = instance => property.GetValue(instance),
                Setter = isReadOnly ? null : (instance, value) => property.SetValue(instance, value),
                IsReadOnly = isReadOnly,
            });
        }

        return result;
    }

    private static IReadOnlyList<MGPropertyGridDescriptor> BuildDescriptors(PropertyDescriptorCollection properties)
    {
        List<MGPropertyGridDescriptor> result = new();

        foreach (PropertyDescriptor property in properties)
        {
            if (property == null || !TryGetEditorKind(property.PropertyType, out var editorKind))
            {
                continue;
            }

            var isReadOnly = property.IsReadOnly;
            result.Add(new MGPropertyGridDescriptor
            {
                Name = property.Name,
                DisplayName = string.IsNullOrWhiteSpace(property.DisplayName) ? property.Name : property.DisplayName,
                Category = string.IsNullOrWhiteSpace(property.Category) ? "Misc" : property.Category,
                PropertyType = property.PropertyType,
                EditorKind = editorKind,
                Getter = instance => property.GetValue(instance),
                Setter = isReadOnly ? null : (instance, value) => property.SetValue(instance, value),
                IsReadOnly = isReadOnly,
            });
        }

        return result;
    }
}