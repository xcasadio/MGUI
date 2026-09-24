using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace MGUI.Core.UI.DataBinding;

/// <summary>ADR-0016: compiles and caches allocation-free, reflection-free property accessors, keyed by
/// (owner <see cref="Type"/>, property name) rather than by object instance, so the cache never grows per bound
/// instance and every accessor for an already-seen (type, property name) pair is reused without compiling
/// anything new.<para/>
/// A typed getter (<c>Func&lt;object, T&gt;</c>) and a typed setter (<c>Action&lt;object, T&gt;</c>) are each
/// compiled once via <see cref="Expression.Lambda"/> for a given (owner type, <see cref="PropertyInfo"/>): the
/// object parameter is cast to the owner type inside the compiled body, so reading or writing a value-typed
/// property never boxes at the call site -- the value flows through the delegate's own generic parameter, not
/// through <see cref="object"/>. <see cref="GetOrBuildCopy"/> additionally combines a getter and a setter of the
/// same exact CLR property type into a single non-generic <c>Action&lt;object,object&gt;</c> push delegate,
/// cached per delegate pair so the same (source type, source property) -&gt; (target type, target property)
/// combination is only combined once, no matter how many bindings share it.</summary>
internal static class TypedAccessorCache
{
    //  Replaces DataBinding's former instance-keyed PropertyInfo cache (ADR-0016): keyed by Type, so it is bounded
    //  by the number of distinct CLR types seen, never by the number of bound object instances.
    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo> PropertyCache = new();

    public static PropertyInfo GetProperty(Type type, string propertyName)
    {
        if (type == null || string.IsNullOrEmpty(propertyName))
        {
            return null;
        }

        return PropertyCache.GetOrAdd((type, propertyName), static key => key.Type.GetProperty(key.Name));
    }

    private static readonly ConcurrentDictionary<(Type Type, string Name), Delegate> GetterCache = new();
    private static readonly ConcurrentDictionary<(Type Type, string Name), Delegate> SetterCache = new();

    private static readonly MethodInfo BuildGetterMethod =
        typeof(TypedAccessorCache).GetMethod(nameof(BuildGetter), BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly MethodInfo BuildSetterMethod =
        typeof(TypedAccessorCache).GetMethod(nameof(BuildSetter), BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly MethodInfo CombineMethod =
        typeof(TypedAccessorCache).GetMethod(nameof(Combine), BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>Returns a cached (or newly compiled, the first time this (owner type, property) pair is seen)
    /// <c>Func&lt;object, T&gt;</c> where <c>T</c> is <paramref name="property"/>'s exact <see cref="PropertyInfo.PropertyType"/>
    /// (e.g. <c>Thickness</c>, <c>int?</c>, <c>Color?</c>, or any other struct/enum/reference type). Callers that
    /// know which <c>T</c> they expect pattern-match the returned <see cref="Delegate"/>, e.g.
    /// <c>GetOrBuildGetter(...) is Func&lt;object, Thickness&gt; getter</c>, which is a reference-type cast, not boxing.</summary>
    public static Delegate GetOrBuildGetter(Type ownerType, PropertyInfo property)
    {
        if (ownerType == null || property == null || !property.CanRead)
        {
            return null;
        }

        return GetterCache.GetOrAdd((ownerType, property.Name), _ =>
        {
            var method = BuildGetterMethod.MakeGenericMethod(property.PropertyType);
            return (Delegate)method.Invoke(null, new object[] { ownerType, property });
        });
    }

    /// <summary>Same as <see cref="GetOrBuildGetter"/>, for a <c>Action&lt;object, T&gt;</c> setter.</summary>
    public static Delegate GetOrBuildSetter(Type ownerType, PropertyInfo property)
    {
        if (ownerType == null || property == null || !property.CanWrite)
        {
            return null;
        }

        return SetterCache.GetOrAdd((ownerType, property.Name), _ =>
        {
            var method = BuildSetterMethod.MakeGenericMethod(property.PropertyType);
            return (Delegate)method.Invoke(null, new object[] { ownerType, property });
        });
    }

    private static Func<object, T> BuildGetter<T>(Type ownerType, PropertyInfo property)
    {
        ParameterExpression objParam = Expression.Parameter(typeof(object), "obj");
        UnaryExpression typedObj = Expression.Convert(objParam, ownerType);
        MemberExpression propExpr = Expression.Property(typedObj, property);
        return Expression.Lambda<Func<object, T>>(propExpr, objParam).Compile();
    }

    private static Action<object, T> BuildSetter<T>(Type ownerType, PropertyInfo property)
    {
        ParameterExpression objParam = Expression.Parameter(typeof(object), "obj");
        ParameterExpression valueParam = Expression.Parameter(typeof(T), "value");
        UnaryExpression typedObj = Expression.Convert(objParam, ownerType);
        BinaryExpression assign = Expression.Assign(Expression.Property(typedObj, property), valueParam);
        return Expression.Lambda<Action<object, T>>(assign, objParam, valueParam).Compile();
    }

    private static readonly ConcurrentDictionary<(Delegate Getter, Delegate Setter), Action<object, object>> CopyCache = new();

    /// <summary>Builds (or reuses) a single allocation-free <c>Action&lt;object,object&gt;</c> that reads
    /// <paramref name="sourceProperty"/> off a source object and writes it directly to <paramref name="targetProperty"/>
    /// on a target object, with no boxing and no reflection at push time. Requires both properties to share the
    /// exact same <see cref="PropertyInfo.PropertyType"/> (no widening, no Nullable-unwrapping): callers that need a
    /// conversion must use the existing reflection + converter path instead. Returns <see langword="null"/> when the
    /// types don't match, or either property can't be read/written.</summary>
    public static Action<object, object> GetOrBuildCopy(Type sourceOwnerType, PropertyInfo sourceProperty, Type targetOwnerType, PropertyInfo targetProperty)
    {
        if (sourceProperty == null || targetProperty == null || sourceProperty.PropertyType != targetProperty.PropertyType ||
            !sourceProperty.CanRead || !targetProperty.CanWrite)
        {
            return null;
        }

        Delegate getter = GetOrBuildGetter(sourceOwnerType, sourceProperty);
        Delegate setter = GetOrBuildSetter(targetOwnerType, targetProperty);
        if (getter == null || setter == null)
        {
            return null;
        }

        return CopyCache.GetOrAdd((getter, setter), key =>
        {
            var method = CombineMethod.MakeGenericMethod(sourceProperty.PropertyType);
            return (Action<object, object>)method.Invoke(null, new object[] { key.Getter, key.Setter });
        });
    }

    private static Action<object, object> Combine<T>(Delegate getterObj, Delegate setterObj)
    {
        var getter = (Func<object, T>)getterObj;
        var setter = (Action<object, T>)setterObj;
        return (Source, Target) => setter(Target, getter(Source));
    }
}
