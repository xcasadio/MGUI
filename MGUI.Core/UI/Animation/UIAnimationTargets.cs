using System.Collections.Concurrent;

namespace MGUI.Core.UI.Animation;

/// <summary>
/// The closed registry of animatable property paths (ADR-0006, decision 5), keyed by path, case-insensitive. The framework registers its
/// targets when the type is first used (<c>Opacity</c>, <c>RenderTransform.*</c>, <c>RenderScale</c>, the layout pilots and the solid colours,
/// slices S4 and S5); an application registers its own with <see cref="Register{T}"/> (the last registration for a path wins).
/// <see cref="UIPropertyAnimation{T}"/> resolves its <c>Property</c> here when it starts, with an explicit error listing the known paths.
/// Thread-safe; lookups do not allocate.
/// </summary>
public static class UIAnimationTargets
{
    /// <summary>One registered target with what is known about it at registration time, so a lookup never needs reflection.</summary>
    private sealed record Entry(object Target, Type ValueType, Type OwnerType);

    private static readonly ConcurrentDictionary<string, Entry> Registry = new(StringComparer.OrdinalIgnoreCase);

    static UIAnimationTargets()
    {
        Targets.UIBuiltInAnimationTargets.RegisterAll();
    }

    /// <summary>Registers (or replaces) the target addressed by <c>target.Path</c>.</summary>
    public static void Register<T>(IUIAnimationTarget<T> target)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (string.IsNullOrWhiteSpace(target.Path))
        {
            throw new ArgumentException("A target path is required.", nameof(target));
        }

        Registry[target.Path] = new Entry(target, typeof(T), target.RequiredOwnerType);
    }

    /// <summary>True if a target is registered under <paramref name="path"/>, whatever its value type.</summary>
    public static bool IsRegistered(string path) => path != null && Registry.ContainsKey(path);

    /// <summary>The registered paths, sorted.</summary>
    public static IReadOnlyList<string> Paths => Registry.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>The value type of the target registered under <paramref name="path"/>, or null when the path is unknown.</summary>
    public static Type GetValueType(string path)
        => path != null && Registry.TryGetValue(path, out var entry) ? entry.ValueType : null;

    /// <summary>The element type required by the target registered under <paramref name="path"/> (<see cref="IUIAnimationTarget{T}.RequiredOwnerType"/>),
    /// null when the path accepts any element, and null when the path is unknown (use <see cref="IsRegistered"/> to tell the two apart).
    /// Case-insensitive, like every other lookup here; never throws.</summary>
    public static Type GetOwnerType(string path)
        => path != null && Registry.TryGetValue(path, out var entry) ? entry.OwnerType : null;

    /// <summary>True when <paramref name="element"/> may be animated on <paramref name="path"/>: the path is registered and its owner type
    /// (<see cref="GetOwnerType"/>) is either null (any element) or an ancestor of <paramref name="element"/>'s type. False, without throwing,
    /// for an unknown path. Allocation-free; used by the tooling debug view and directly testable.</summary>
    public static bool IsApplicable(string path, MGElement element)
    {
        if (path == null || element == null || !Registry.TryGetValue(path, out var entry))
        {
            return false;
        }

        return entry.OwnerType == null || entry.OwnerType.IsInstanceOfType(element);
    }

    /// <summary>Retrieves the target registered under <paramref name="path"/> for <typeparamref name="T"/>, or false when the path is unknown.</summary>
    /// <exception cref="InvalidOperationException">The path is registered for another value type.</exception>
    public static bool TryGet<T>(string path, out IUIAnimationTarget<T> target)
    {
        if (path != null && Registry.TryGetValue(path, out var entry))
        {
            if (entry.Target is IUIAnimationTarget<T> typed)
            {
                target = typed;
                return true;
            }

            throw new InvalidOperationException(
                $"The animation target '{path}' animates values of type '{GetValueType(path)?.Name}', not '{typeof(T).Name}'.");
        }

        target = null;
        return false;
    }

    /// <summary>Retrieves the target registered under <paramref name="path"/> for <typeparamref name="T"/>.</summary>
    /// <exception cref="ArgumentException">The path is unknown; the message lists the registered paths.</exception>
    /// <exception cref="InvalidOperationException">The path is registered for another value type.</exception>
    public static IUIAnimationTarget<T> Resolve<T>(string path)
    {
        if (TryGet(path, out IUIAnimationTarget<T> target))
        {
            return target;
        }

        throw new ArgumentException(
            $"Unknown animation target '{path}'. Registered paths: {string.Join(", ", Paths)}. " +
            $"Register a custom one with {nameof(UIAnimationTargets)}.{nameof(Register)}(...).", nameof(path));
    }
}