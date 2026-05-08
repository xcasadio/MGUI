using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace MGUI.Core.UI
{
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

            if (!Cache.TryGetValue(type, out IReadOnlyList<MGPropertyGridDescriptor> descriptors))
            {
                descriptors = BuildDescriptors(type);
                Cache[type] = descriptors;
            }

            return descriptors;
        }

        internal static bool TryGetEditorKind(Type propertyType, out MGPropertyGridEditorKind editorKind)
        {
            if (propertyType == typeof(bool))
            {
                editorKind = MGPropertyGridEditorKind.Bool;
                return true;
            }

            if (propertyType == typeof(int))
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

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                MethodInfo getter = property.GetMethod;
                if (getter == null || !getter.IsPublic)
                {
                    continue;
                }

                if (!TryGetEditorKind(property.PropertyType, out MGPropertyGridEditorKind editorKind))
                {
                    continue;
                }

                MethodInfo setter = property.SetMethod;
                ReadOnlyAttribute readOnlyAttribute = property.GetCustomAttribute<ReadOnlyAttribute>();
                DisplayNameAttribute displayNameAttribute = property.GetCustomAttribute<DisplayNameAttribute>();
                CategoryAttribute categoryAttribute = property.GetCustomAttribute<CategoryAttribute>();

                bool isReadOnly = setter == null || !setter.IsPublic || readOnlyAttribute?.IsReadOnly == true;

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
    }
}