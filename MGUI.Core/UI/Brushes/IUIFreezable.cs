namespace MGUI.Core.UI.Brushes;

/// <summary>WPF/Noesis Freezable semantics (<c>Gui.DependencySystem._Freezable</c>) applied to MGUI's paints: a frozen object is
/// immutable and safely shareable by reference (a theme, a static resource, the <see cref="MGUI.Core.UI.Brushes.FillBrushes.SolidFillBrushes"/>
/// palette); a setter invoked on a frozen instance throws <see cref="InvalidOperationException"/> instead of silently mutating shared state.<para/>
/// Carried by <see cref="MGUI.Core.UI.Brushes.FillBrushes.IFillBrush"/> and <see cref="MGUI.Core.UI.Brushes.BorderBrushes.IBorderBrush"/> as of
/// this slice (ADR-0009, W1) through default interface members so every existing implementer keeps compiling unchanged: a value brush that is
/// still a <see langword="readonly struct"/> today behaves as always frozen (it cannot change in place at all), and every concrete brush is
/// expected to override the three members once it derives from <see cref="UIFreezableBrush"/> (a later slice, W2+).</summary>
public interface IUIFreezable
{
    /// <summary>True once <see cref="Freeze"/> has been called (or, for a brush that has not yet adopted <see cref="UIFreezableBrush"/>,
    /// always: a value type that cannot mutate in place is trivially frozen).</summary>
    bool IsFrozen { get; }

    /// <summary>False for an instance that can never be frozen (a child that refuses freezing propagates this upward): <see cref="Freeze"/>
    /// throws <see cref="InvalidOperationException"/> when this is false.</summary>
    bool CanFreeze { get; }

    /// <summary>Freezes this instance (and, recursively, any nested paint it owns) so it becomes immutable and shareable. Idempotent: freezing
    /// an already-frozen instance does nothing. Throws <see cref="InvalidOperationException"/> when <see cref="CanFreeze"/> is false.</summary>
    void Freeze();
}
