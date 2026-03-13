using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MGUI.Core.UI.Styling
{
    internal static class UIResourceReferenceApplicator
    {
        private const string DynamicResourceSubscriptionsMetadataKey = "DynamicResourceSubscriptions";

        public static bool Apply(MGElement HostElement, object TargetObject, UIResourceReferenceConfig Config, MGResources Resources)
        {
            if (TargetObject == null || string.IsNullOrWhiteSpace(Config.TargetPath) || string.IsNullOrWhiteSpace(Config.ResourceName))
            {
                return false;
            }

            Resources ??= HostElement?.GetResources();
            if (Resources == null || !TryResolveTarget(TargetObject, Config.TargetPath, out object PropertyOwner, out PropertyInfo Property))
            {
                return false;
            }

            if (!TryResolveResourceValue(HostElement, Resources, Config.ResourceName, Property.PropertyType, out object ResolvedValue))
            {
                return false;
            }

            try
            {
                Property.SetValue(PropertyOwner, ResolvedValue);
                RegisterDynamicSubscription(HostElement, TargetObject, Config, Resources);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceReference WARN] Failed to apply resource '{Config.ResourceName}' to '{TargetObject.GetType().Name}.{Config.TargetPath}': {ex.Message}");
                return false;
            }
        }

        private static void RegisterDynamicSubscription(MGElement HostElement, object TargetObject, UIResourceReferenceConfig Config, MGResources Resources)
        {
            if (!Config.IsDynamic || Resources == null)
            {
                return;
            }

            string SubscriptionKey = string.Join("|", RuntimeHelpers.GetHashCode(TargetObject), Config.TargetPath, Config.ResourceName);
            HashSet<string> SubscriptionKeys = null;
            if (HostElement != null)
            {
                if (!HostElement.Metadata.TryGetValue(DynamicResourceSubscriptionsMetadataKey, out object Existing))
                {
                    Existing = new HashSet<string>(StringComparer.Ordinal);
                    HostElement.Metadata.Add(DynamicResourceSubscriptionsMetadataKey, Existing);
                }

                SubscriptionKeys = Existing as HashSet<string>;
            }

            if (SubscriptionKeys != null && !SubscriptionKeys.Add(SubscriptionKey))
            {
                return;
            }

            void Refresh(MGResources Sender, string ResourceName)
            {
                if (string.Equals(ResourceName, Config.ResourceName, StringComparison.Ordinal))
                {
                    _ = Apply(HostElement, TargetObject, Config, Resources);
                }
            }

            foreach (MGResources Scope in EnumerateSelfAndAncestors(Resources))
            {
                Scope.OnStaticResourceAdded += (_, e) => Refresh(Scope, e.Name);
                Scope.OnStaticResourceChanged += (_, e) => Refresh(Scope, e.Name);
                Scope.OnStaticResourceRemoved += (_, e) => Refresh(Scope, e.Name);
            }
        }

        private static IEnumerable<MGResources> EnumerateSelfAndAncestors(MGResources Resources)
        {
            for (MGResources Current = Resources; Current != null; Current = Current.Parent)
            {
                yield return Current;
            }
        }

        private static bool TryResolveTarget(object RootObject, string TargetPath, out object PropertyOwner, out PropertyInfo Property)
        {
            PropertyOwner = RootObject;
            Property = null;

            string[] Segments = TargetPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (!Segments.Any())
            {
                return false;
            }

            for (int i = 0; i < Segments.Length; i++)
            {
                PropertyInfo CurrentProperty = PropertyOwner?.GetType().GetProperty(Segments[i], BindingFlags.Instance | BindingFlags.Public);
                if (CurrentProperty == null)
                {
                    PropertyOwner = null;
                    return false;
                }

                if (i == Segments.Length - 1)
                {
                    Property = CurrentProperty;
                    return Property.CanWrite;
                }

                PropertyOwner = CurrentProperty.GetValue(PropertyOwner);
                if (PropertyOwner == null)
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryResolveResourceValue(MGElement HostElement, MGResources Resources, string ResourceName, Type TargetType, out object Result)
        {
            if (!Resources.TryGetStaticResource(ResourceName, out object RawValue))
            {
                Result = null;
                return false;
            }

            Result = ConvertValue(HostElement, RawValue, TargetType);
            return true;
        }

        private static object ConvertValue(MGElement HostElement, object Value, Type TargetType)
        {
            if (Value == null)
            {
                return null;
            }

            Type ActualTargetType = Nullable.GetUnderlyingType(TargetType) ?? TargetType;
            Type SourceType = Value.GetType();
            if (SourceType.IsAssignableTo(ActualTargetType))
            {
                return Value;
            }

            if (HostElement != null && Value is FillBrush FillBrush && typeof(IFillBrush).IsAssignableFrom(ActualTargetType))
            {
                return FillBrush.ToFillBrush(HostElement.GetDesktop(), HostElement);
            }

            if (HostElement != null && Value is BorderBrush BorderBrush && typeof(IBorderBrush).IsAssignableFrom(ActualTargetType))
            {
                return BorderBrush.ToBorderBrush(HostElement.GetDesktop(), HostElement);
            }

            if (Value is XAMLColor XamlColor && ActualTargetType == typeof(Color))
            {
                return XamlColor.ToXNAColor();
            }

            if (Value is Thickness XamlThickness && ActualTargetType == typeof(MonoGame.Extended.Thickness))
            {
                return XamlThickness.ToThickness();
            }

            if (Value is CornerRadius XamlCornerRadius && ActualTargetType == typeof(MGCornerRadius))
            {
                return XamlCornerRadius.ToCornerRadius();
            }

            if (HostElement != null && Value is Element XamlElement && typeof(MGElement).IsAssignableFrom(ActualTargetType))
            {
                MethodInfo ToElementMethod = typeof(Element).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .First(x => x.Name == nameof(Element.ToElement) && x.IsGenericMethodDefinition && x.GetParameters().Length == 3)
                    .MakeGenericMethod(ActualTargetType);
                return ToElementMethod.Invoke(XamlElement, new object[] { HostElement.SelfOrParentWindow, HostElement, null });
            }

            TypeConverter TargetConverter = TypeDescriptor.GetConverter(ActualTargetType);
            if (TargetConverter?.CanConvertFrom(SourceType) == true)
            {
                return TargetConverter.ConvertFrom(null, CultureInfo.CurrentCulture, Value);
            }

            TypeConverter SourceConverter = TypeDescriptor.GetConverter(SourceType);
            if (SourceConverter?.CanConvertTo(ActualTargetType) == true)
            {
                return SourceConverter.ConvertTo(null, CultureInfo.CurrentCulture, Value, ActualTargetType);
            }

            if (Value is IConvertible)
            {
                return Convert.ChangeType(Value, ActualTargetType, CultureInfo.CurrentCulture);
            }

            return Value;
        }
    }
}