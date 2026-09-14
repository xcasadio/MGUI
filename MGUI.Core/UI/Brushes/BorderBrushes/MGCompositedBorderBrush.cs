using System.Collections;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Brushes.BorderBrushes;

/// <summary>An <see cref="IBorderBrush"/> that draws several nested <see cref="IBorderBrush"/>es in order.<para/>
/// See also: <see cref="MGUniformBorderBrush"/>, <see cref="MGDockedBorderBrush"/>, <see cref="MGTexturedBorderBrush"/>, <see cref="MGBandedBorderBrush"/>, <see cref="MGHighlightBorderBrush"/><para/>
/// Freezable (ADR-0009): a sealed mutable class deriving from <see cref="UIFreezableBrush"/>. <see cref="Freeze"/> freezes every nested
/// brush in <see cref="Brushes"/> and, once frozen, the list itself refuses <see cref="ICollection{T}.Add(T)"/>/<see cref="IList{T}.Remove"/>/etc
/// (<see cref="ThrowIfFrozen"/>), same pattern as <see cref="FillBrushes.MGCompositedFillBrush"/>. <see cref="CanFreeze"/> is false when any
/// nested brush cannot freeze.</summary>
public sealed class MGCompositedBorderBrush : UIFreezableBrush, IBorderBrush
{
    /// <summary>See <see cref="FillBrushes.MGCompositedFillBrush"/>'s own nested <c>GuardedList</c> for the rationale (duplicated here
    /// rather than shared, since the two composites implement different brush interfaces).</summary>
    private sealed class GuardedList : IList<IBorderBrush>
    {
        private readonly MGCompositedBorderBrush _Owner;
        private readonly List<IBorderBrush> _Inner;

        public GuardedList(MGCompositedBorderBrush Owner, IEnumerable<IBorderBrush> Items)
        {
            _Owner = Owner;
            _Inner = Items.ToList();
        }

        public IBorderBrush this[int index]
        {
            get => _Inner[index];
            set { _Owner.ThrowIfFrozen(nameof(Brushes)); _Inner[index] = value; }
        }

        public int Count => _Inner.Count;
        public bool IsReadOnly => _Owner.IsFrozen;

        public void Add(IBorderBrush item) { _Owner.ThrowIfFrozen(nameof(Brushes)); _Inner.Add(item); }
        public void Clear() { _Owner.ThrowIfFrozen(nameof(Brushes)); _Inner.Clear(); }
        public bool Contains(IBorderBrush item) => _Inner.Contains(item);
        public void CopyTo(IBorderBrush[] array, int arrayIndex) => _Inner.CopyTo(array, arrayIndex);
        public IEnumerator<IBorderBrush> GetEnumerator() => _Inner.GetEnumerator();
        public int IndexOf(IBorderBrush item) => _Inner.IndexOf(item);
        public void Insert(int index, IBorderBrush item) { _Owner.ThrowIfFrozen(nameof(Brushes)); _Inner.Insert(index, item); }
        public bool Remove(IBorderBrush item) { _Owner.ThrowIfFrozen(nameof(Brushes)); return _Inner.Remove(item); }
        public void RemoveAt(int index) { _Owner.ThrowIfFrozen(nameof(Brushes)); _Inner.RemoveAt(index); }
        IEnumerator IEnumerable.GetEnumerator() => _Inner.GetEnumerator();
    }

    private readonly GuardedList _Brushes;
    public IList<IBorderBrush> Brushes => _Brushes;

    public MGCompositedBorderBrush(params IBorderBrush[] Brushes)
    {
        _Brushes = new GuardedList(this, Brushes.Where(x => x != null));
    }

    /// <summary>False when any nested brush in <see cref="Brushes"/> cannot itself freeze (ADR-0009).</summary>
    public override bool CanFreeze => _Brushes.All(x => x is not IUIFreezable freezable || freezable.CanFreeze);

    /// <summary>Freezes every nested brush in <see cref="Brushes"/> that implements <see cref="IUIFreezable"/> (ADR-0009).</summary>
    protected override void OnFreeze()
    {
        foreach (var Brush in _Brushes)
        {
            if (Brush is IUIFreezable freezable)
            {
                freezable.Freeze();
            }
        }
    }

    /// <summary>Forwards the per-frame lifecycle call to every nested brush in <see cref="Brushes"/> via <see cref="PaintLifecycle"/>,
    /// deduplicated by reference against every other slot/element that references them for the frame.</summary>
    void IBorderBrush.Update(UpdateBaseArgs UA)
    {
        foreach (var Brush in _Brushes)
        {
            PaintLifecycle.Update(Brush, UA);
        }
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds, Thickness BT)
    {
        foreach (var Brush in _Brushes)
        {
            Brush.Draw(DA, Element, Bounds, BT);
        }
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
    {
        foreach (var Brush in _Brushes)
        {
            Brush.Draw(DA, Element, Shape, Geometry);
        }
    }

    public IBorderBrush Copy() => new MGCompositedBorderBrush(_Brushes.Select(x => x.Copy()).ToArray());

    /// <summary>Value equality (ADR-0009): two composited border brushes are equal when they hold the same number of nested brushes,
    /// each equal by value at the same position (<see cref="UIBrushEquality.ValueEquals(IBorderBrush, IBorderBrush)"/>), regardless of
    /// frozen state or instance identity.</summary>
    public bool ValueEquals(IBorderBrush other) => other is MGCompositedBorderBrush c
        && c._Brushes.Count == _Brushes.Count
        && _Brushes.Zip(c._Brushes, UIBrushEquality.ValueEquals).All(x => x);

    public override bool Equals(object obj) => ValueEquals(obj as IBorderBrush);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var Brush in _Brushes)
        {
            hash.Add(Brush);
        }
        return hash.ToHashCode();
    }
}