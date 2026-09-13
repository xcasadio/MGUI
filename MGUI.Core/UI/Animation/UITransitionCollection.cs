namespace MGUI.Core.UI.Animation;

/// <summary>
/// The transitions attached to one <see cref="MGElement"/> (<c>element.Transitions</c>; S6, ADR-0006). Adding a transition resolves its
/// target, reads the current value and subscribes to changes; removing it unsubscribes and keeps the current value. One transition per
/// property path: adding a second one for the same path replaces the first. Allocated on first access with the element's animation slot.
/// </summary>
public sealed class UITransitionCollection : IEnumerable<UITransition>
{
    private readonly List<UITransition> _items = new();

    internal UITransitionCollection(MGElement owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <summary>The element that owns these transitions.</summary>
    public MGElement Owner { get; }

    public int Count => _items.Count;

    /// <summary>The transition attached for <paramref name="property"/>, or null.</summary>
    public UITransition this[string property]
        => _items.Find(x => string.Equals(x.Property, property, StringComparison.OrdinalIgnoreCase));

    /// <summary>Attaches <paramref name="transition"/> to the element, replacing any transition already attached for the same path.</summary>
    /// <exception cref="ArgumentException">The path is unknown.</exception>
    /// <exception cref="InvalidOperationException">The transition is attached elsewhere, or its target is not observable.</exception>
    public void Add(UITransition transition)
    {
        if (transition == null)
        {
            throw new ArgumentNullException(nameof(transition));
        }

        UITransition existing = this[transition.Property];
        if (existing != null)
        {
            if (ReferenceEquals(existing, transition))
            {
                return;
            }

            Remove(existing);
        }

        transition.Attach(Owner);
        _items.Add(transition);
    }

    /// <summary>Detaches <paramref name="transition"/>; a running interpolation stops and keeps the current value.</summary>
    public bool Remove(UITransition transition)
    {
        if (transition == null || !_items.Remove(transition))
        {
            return false;
        }

        transition.Detach();
        return true;
    }

    /// <summary>Detaches the transition attached for <paramref name="property"/>, if any.</summary>
    public bool Remove(string property) => Remove(this[property]);

    /// <summary>Detaches every transition.</summary>
    public void Clear()
    {
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            UITransition transition = _items[i];
            _items.RemoveAt(i);
            transition.Detach();
        }
    }

    public IEnumerator<UITransition> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}