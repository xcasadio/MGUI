using System.Runtime.CompilerServices;
using MGUI.Shared.Helpers;

namespace MGUI.Core.UI.Brushes;

/// <summary>Abstract base for a mutable, notifying, freezable paint (ADR-0009, W1): the value brushes, the composites and the highlight border brush derive from it
/// and it establishes the contract every paint of MGUI.Core adopts. Derives from <see cref="ViewModelBase"/> so a
/// property change raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> without allocating (cached
/// <see cref="System.ComponentModel.PropertyChangedEventArgs"/> per property name).<para/>
/// A frozen instance never raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> again: it cannot change, so there
/// is nothing to notify. <c>Copy</c> is expected to keep returning an unfrozen, independent copy (the WPF <c>Clone</c> equivalent;
/// <c>Copy</c> keeps its existing name on <see cref="MGUI.Core.UI.Brushes.FillBrushes.IFillBrush"/>/<see cref="MGUI.Core.UI.Brushes.BorderBrushes.IBorderBrush"/>).</summary>
public abstract class UIFreezableBrush : ViewModelBase, IUIFreezable
{
    private bool _IsFrozen;
    public bool IsFrozen => _IsFrozen;

    /// <summary>True by default; a subclass that owns a child which cannot freeze (or that otherwise refuses freezing) overrides this to
    /// false so <see cref="Freeze"/> throws instead of leaving the instance in a half-frozen state.</summary>
    public virtual bool CanFreeze => true;

    /// <summary>Freezes this instance, then invokes <see cref="OnFreeze"/> so a subclass can recursively freeze the children it owns.
    /// Idempotent (a second call does nothing). Throws <see cref="InvalidOperationException"/> when <see cref="CanFreeze"/> is false;
    /// <see cref="IsFrozen"/> stays false in that case.</summary>
    public void Freeze()
    {
        if (_IsFrozen)
        {
            return;
        }

        if (!CanFreeze)
        {
            throw new InvalidOperationException($"{GetType().Name} cannot be frozen ({nameof(CanFreeze)} is false).");
        }

        _IsFrozen = true;
        OnFreeze();
    }

    /// <summary>Called once, immediately after this instance transitions to frozen, so a subclass can freeze the nested paints it owns.
    /// Default implementation does nothing (a brush with no nested paint does not need to override it).</summary>
    protected virtual void OnFreeze() { }

    /// <summary>Throws <see cref="InvalidOperationException"/> naming this type and <paramref name="property"/> when <see cref="IsFrozen"/>
    /// is true. Every notifying setter of a subclass calls this before writing its backing field.</summary>
    /// <param name="property">Defaults to the caller's member name, so a setter does not need to spell out its own property name.</param>
    protected void ThrowIfFrozen([CallerMemberName] string property = null)
    {
        if (_IsFrozen)
        {
            throw new InvalidOperationException($"Cannot set {GetType().Name}.{property}: the instance is frozen.");
        }
    }

    /// <summary>Shared shape for a notifying, frozen-aware property setter: throws via <see cref="ThrowIfFrozen(string)"/> when frozen,
    /// then writes <paramref name="field"/> and raises <see cref="ViewModelBase.NotifyPropertyChanged(string)"/> only when
    /// <paramref name="value"/> differs from the current value (<see cref="EqualityComparer{T}.Default"/>), same as every other
    /// notifying property in the codebase. Returns whether the value actually changed.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string name = null)
        => SetProperty(ref field, value, EqualityComparer<T>.Default, name);

    /// <summary>Same contract as <see cref="SetProperty{T}(ref T, T, string)"/>, but compares with <paramref name="comparer"/> instead of
    /// <see cref="EqualityComparer{T}.Default"/> (fix round, ADR-0009 W3-fix): a brush-typed <typeparamref name="T"/> property that nests
    /// another <see cref="IUIFreezable"/> paint (e.g. <c>MGPaddedFillBrush.Brush</c>, <c>MGDockedBorderBrush.Left</c>) must accept a
    /// distinct-but-value-equal instance as a real change -- the same identity-sensitive rule <see cref="UIBrushEquality.ForSlots{T}"/>
    /// already applies to <see cref="VisualStateSetting{TDataType}"/>'s slots, and for the same reason: the caller may mutate that
    /// distinct instance afterwards (an element-owned brush, W4), so the property must already hold the exact instance for the mutation to
    /// reach it.</summary>
    protected bool SetProperty<T>(ref T field, T value, IEqualityComparer<T> comparer, [CallerMemberName] string name = null)
    {
        ThrowIfFrozen(name);

        if (comparer.Equals(field, value))
        {
            return false;
        }

        field = value;
        NotifyPropertyChanged(name);
        return true;
    }
}
