namespace MGUI.Core.UI.Animation.States;

/// <summary>
/// The named visual states of one element (<c>element.VisualStates</c>; ADR-0007, decision 3). Every frame, after the element's
/// <see cref="MGElement.VisualState"/> is computed, the current name is resolved in the priority order of <see cref="UIVisualStateNames.Priority"/>
/// (Disabled, Checked for an <see cref="IUICheckable"/>, Selected, Pressed, Hover, Focused, Normal): the first state whose condition holds AND that
/// this collection defines wins, so an element with only <c>Normal</c> and <c>Hover</c> shows <c>Hover</c> while pressed. Leaving a state restores
/// the paths the next state does not set, then the next state's setters are applied, so a transition sees one write per path per change.<para/>
/// Allocated with the element's animation slot on first access; an element without states costs nothing per frame.
/// </summary>
public sealed class UIVisualStateCollection : IEnumerable<UIVisualState>
{
    private readonly List<UIVisualState> _States = new();
    private readonly Dictionary<string, object> _Bases = new(StringComparer.OrdinalIgnoreCase);

    internal UIVisualStateCollection(MGElement owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public MGElement Owner { get; }

    public int Count => _States.Count;

    /// <summary>When false, the states are neither resolved nor applied; setting it to false leaves the current state applied until the next enable.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>The name of the state currently applied, null when none.</summary>
    public string Current { get; private set; }

    /// <summary>The state named <paramref name="name"/> (case-insensitive), or null.</summary>
    public UIVisualState this[string name] => _States.Find(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Adds a state, replacing the one of the same name; the current state is re-resolved on the next frame.</summary>
    public void Add(UIVisualState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        int existing = _States.FindIndex(x => string.Equals(x.Name, state.Name, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            if (string.Equals(Current, state.Name, StringComparison.OrdinalIgnoreCase))
            {
                Apply(null);
            }

            _States[existing] = state;
        }
        else
        {
            _States.Add(state);
        }
    }

    /// <summary>Removes the state named <paramref name="name"/>; if it is the current one, its setters are restored.</summary>
    public bool Remove(string name)
    {
        int index = _States.FindIndex(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return false;
        }

        if (string.Equals(Current, name, StringComparison.OrdinalIgnoreCase))
        {
            Apply(null);
        }

        _States.RemoveAt(index);
        return true;
    }

    /// <summary>Restores the current state and removes every state.</summary>
    public void Clear()
    {
        Apply(null);
        _States.Clear();
    }

    /// <summary>Resolves the current name from the element's state and applies it when it changed. Called by <see cref="MGElement.Update"/>.</summary>
    internal void Refresh()
    {
        if (!IsEnabled || _States.Count == 0)
        {
            return;
        }

        string name = Resolve();
        if (!string.Equals(name, Current, StringComparison.OrdinalIgnoreCase))
        {
            Apply(name);
        }
    }

    /// <summary>The name the element would show now, given its <see cref="MGElement.VisualState"/> and checked state, among the defined states.</summary>
    public string Resolve()
    {
        VisualState state = Owner.VisualState;
        if (state.IsDisabled && Has(UIVisualStateNames.Disabled))
        {
            return UIVisualStateNames.Disabled;
        }

        if (Owner is IUICheckable checkable && checkable.IsChecked == true && Has(UIVisualStateNames.Checked))
        {
            return UIVisualStateNames.Checked;
        }

        if (state.IsSelected && Has(UIVisualStateNames.Selected))
        {
            return UIVisualStateNames.Selected;
        }

        if (state.IsPressed && Has(UIVisualStateNames.Pressed))
        {
            return UIVisualStateNames.Pressed;
        }

        if (state.IsPressedOrHovered && Has(UIVisualStateNames.Hover))
        {
            return UIVisualStateNames.Hover;
        }

        if (state.IsFocused && Has(UIVisualStateNames.Focused))
        {
            return UIVisualStateNames.Focused;
        }

        return Has(UIVisualStateNames.Normal) ? UIVisualStateNames.Normal : null;
    }

    private bool Has(string name)
    {
        for (int i = 0; i < _States.Count; i++)
        {
            if (string.Equals(_States[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void Apply(string name)
    {
        UIVisualState previous = Current == null ? null : this[Current];
        UIVisualState next = name == null ? null : this[name];

        if (previous != null)
        {
            for (int i = 0; i < previous.Setters.Count; i++)
            {
                UIVisualStateSetter setter = previous.Setters[i];
                if (next == null || !next.HasPath(setter.Path))
                {
                    setter.Applier.Restore(Owner, _Bases);
                }
            }
        }

        Current = next?.Name;
        if (next != null)
        {
            for (int i = 0; i < next.Setters.Count; i++)
            {
                next.Setters[i].Applier.Apply(Owner, next.Name, _Bases);
            }
        }
    }

    public IEnumerator<UIVisualState> GetEnumerator() => _States.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}