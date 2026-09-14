using System.Collections;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Brushes.FillBrushes;

/// <summary>An <see cref="IFillBrush"/> that draws several nested <see cref="IFillBrush"/>es in order.<para/>
/// Especially useful when compositing multiple transparent colors, in cases where you still need to keep track of each individual Color used instead of reducing it down to a single value.<para/>
/// Freezable (ADR-0009, W3): a sealed mutable class deriving from <see cref="UIFreezableBrush"/>. <see cref="Freeze"/> freezes every nested
/// brush in <see cref="Brushes"/> and, once frozen, the list itself refuses <see cref="ICollection{T}.Add(T)"/>/<see cref="IList{T}.Remove"/>/etc
/// (<see cref="ThrowIfFrozen"/>) so a frozen composite cannot be mutated through its list either. <see cref="CanFreeze"/> is false when any
/// nested brush cannot freeze.</summary>
public sealed class MGCompositedFillBrush : UIFreezableBrush, IFillBrush
{
    /// <summary>Backing list wrapping the actual <see cref="List{T}"/>: every mutating member throws via <see cref="UIFreezableBrush.ThrowIfFrozen"/>
    /// once the owning <see cref="MGCompositedFillBrush"/> is frozen, so the previously-mutable public field cannot bypass the freeze contract.</summary>
    private sealed class GuardedList : IList<IFillBrush>
    {
        private readonly MGCompositedFillBrush _Owner;
        private readonly List<IFillBrush> _Inner;

        public GuardedList(MGCompositedFillBrush Owner, IEnumerable<IFillBrush> Items)
        {
            _Owner = Owner;
            _Inner = Items.ToList();
        }

        public IFillBrush this[int index]
        {
            get => _Inner[index];
            set { _Owner.ThrowIfFrozen(nameof(Brushes)); _Inner[index] = value; }
        }

        public int Count => _Inner.Count;
        public bool IsReadOnly => _Owner.IsFrozen;

        public void Add(IFillBrush item) { _Owner.ThrowIfFrozen(nameof(Brushes)); _Inner.Add(item); }
        public void Clear() { _Owner.ThrowIfFrozen(nameof(Brushes)); _Inner.Clear(); }
        public bool Contains(IFillBrush item) => _Inner.Contains(item);
        public void CopyTo(IFillBrush[] array, int arrayIndex) => _Inner.CopyTo(array, arrayIndex);
        public IEnumerator<IFillBrush> GetEnumerator() => _Inner.GetEnumerator();
        public int IndexOf(IFillBrush item) => _Inner.IndexOf(item);
        public void Insert(int index, IFillBrush item) { _Owner.ThrowIfFrozen(nameof(Brushes)); _Inner.Insert(index, item); }
        public bool Remove(IFillBrush item) { _Owner.ThrowIfFrozen(nameof(Brushes)); return _Inner.Remove(item); }
        public void RemoveAt(int index) { _Owner.ThrowIfFrozen(nameof(Brushes)); _Inner.RemoveAt(index); }
        IEnumerator IEnumerable.GetEnumerator() => _Inner.GetEnumerator();
    }

    private readonly GuardedList _Brushes;
    public IList<IFillBrush> Brushes => _Brushes;

    public MGCompositedFillBrush(params IFillBrush[] Brushes)
    {
        _Brushes = new GuardedList(this, Brushes.Where(x => x != null));
    }

    /// <summary>False when any nested brush in <see cref="Brushes"/> cannot itself freeze (ADR-0009, W3): a composite cannot be safely
    /// frozen while one of its children refuses.</summary>
    public override bool CanFreeze => _Brushes.All(x => x is not IUIFreezable freezable || freezable.CanFreeze);

    /// <summary>Freezes every nested brush in <see cref="Brushes"/> that implements <see cref="IUIFreezable"/> (ADR-0009, W3).</summary>
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
    public void Update(UpdateBaseArgs UA)
    {
        foreach (var Brush in _Brushes)
        {
            PaintLifecycle.Update(Brush, UA);
        }
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
    {
        foreach (var Brush in _Brushes)
        {
            Brush.Draw(DA, Element, Bounds);
        }
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
    {
        foreach (var Brush in _Brushes)
        {
            Brush.Draw(DA, Element, Shape, Geometry);
        }
    }

    public IFillBrush Copy() => new MGCompositedFillBrush(_Brushes.Select(x => x.Copy()).ToArray());

    /// <summary>Value equality (ADR-0009, W3): two composited brushes are equal when they hold the same number of nested brushes, each
    /// equal by value at the same position (<see cref="UIBrushEquality.ValueEquals(IFillBrush, IFillBrush)"/>), regardless of frozen state
    /// or instance identity. Overrides <see cref="object.Equals(object)"/>/<see cref="GetHashCode"/> for consistency with the other
    /// converted brushes; unlike them, this class was already a mutable reference type before this slice (never used as a hash key).</summary>
    public bool ValueEquals(IFillBrush other) => other is MGCompositedFillBrush c
        && c._Brushes.Count == _Brushes.Count
        && _Brushes.Zip(c._Brushes, UIBrushEquality.ValueEquals).All(x => x);

    public override bool Equals(object obj) => ValueEquals(obj as IFillBrush);
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
