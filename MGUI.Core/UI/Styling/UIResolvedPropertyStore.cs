namespace MGUI.Core.UI.Styling;

/// <summary>
/// Per-owner store of resolved contributions for the eight <see cref="UIPilotProperty"/> keys backing the
/// seven ADR-0005 pilots (the text foreground pilot is carried by two keys, <see cref="UIPilotProperty.Foreground"/>
/// and <see cref="UIPilotProperty.DefaultTextForeground"/> -- see that enum's remarks). One entry is
/// allocated per written (property, slot) pair; an entry holds a compact, precedence-sorted list of at most
/// eleven contributions (one per <see cref="UIValueSourceKind"/>) and reports the highest-precedence one as
/// the effective value. Has no dependency on <c>MGElement</c> so it can be unit-tested in isolation and
/// reused by any owner type (an element, its border, a text block, ...).
/// </summary>
/// <remarks>
/// Storage choice: a lazily allocated flat array of <c>PropertyCount * SlotCount</c> (8 * 6 = 48) entry
/// slots, indexed directly by <c>(int)property * SlotCount + (int)slot</c>, rather than a
/// <c>Dictionary&lt;(UIPilotProperty, UIValueSlot), Entry&gt;</c>. Steady-state cost: one array reference
/// field on the owner (null until the first <see cref="Set{T}"/>), then one 48-reference array (384 bytes
/// on a 64-bit runtime) plus one small <see cref="Entry{T}"/> object per distinct (property, slot) actually
/// written. A dictionary would pay a per-entry bucket/hash overhead for the same 48-slot address space and
/// a boxed tuple key per lookup; the flat array turns every lookup into one array index with no hashing,
/// no boxing and no allocation once the entry exists, at the fixed, small cost of the unused slots (most
/// owners only ever populate two to six of the 48). Non-pilot properties never touch this type at all, so
/// they carry no cost, matching the "no cost for non-pilot properties" budget in ADR-0005.
/// </remarks>
internal sealed class UIResolvedPropertyStore
{
    private const int PropertyCount = 8; // Number of UIPilotProperty members.
    private const int SlotCount = 6; // Number of UIValueSlot members.

    private static readonly IReadOnlyList<UIValueSourceKind> EmptyKinds = Array.Empty<UIValueSourceKind>();
    private static readonly IReadOnlyList<UIResolvedContribution> EmptyContributions = Array.Empty<UIResolvedContribution>();

    /// <summary>
    /// Type-erased base so <see cref="_entries"/> can hold entries typed for different <c>T</c>s side by
    /// side, while <see cref="Contributions"/> can read the set of contributing kinds without knowing <c>T</c>.
    /// </summary>
    private abstract class Entry
    {
        public abstract Type ValueType { get; }

        public abstract IReadOnlyList<UIValueSourceKind> KindsView { get; }

        /// <summary>Appends this entry's contributions to <paramref name="target"/>, highest precedence
        /// first, boxing each typed value as <c>object</c>. Used only by the type-erased diagnostic
        /// <see cref="UIResolvedPropertyStore.EnumerateContributions"/>.</summary>
        public abstract void CopyContributionsTo(List<UIResolvedContribution> target);
    }

    /// <summary>
    /// One (property, slot) entry: a precedence-sorted (highest first) list of contributions, one per
    /// source kind that is currently set, plus the invalidation kind of the last contribution removed
    /// that emptied the entry (used to answer <see cref="UIResolvedValue{T}.Unset"/> with a meaningful
    /// invalidation instead of always <see cref="UIInvalidationKind.None"/>).
    /// </summary>
    private sealed class Entry<T> : Entry
    {
        public readonly List<UIResolvedValue<T>> Contributions = new();
        public readonly List<UIValueSourceKind> Kinds = new();
        public UIInvalidationKind EmptiedInvalidation = UIInvalidationKind.None;

        public override Type ValueType => typeof(T);

        public override IReadOnlyList<UIValueSourceKind> KindsView => Kinds;

        public override void CopyContributionsTo(List<UIResolvedContribution> target)
        {
            for (var i = 0; i < Contributions.Count; i++)
                target.Add(new UIResolvedContribution(Kinds[i], Contributions[i].Source, Contributions[i].Value));
        }
    }

    private Entry[] _entries;
    private int _entryCount;

    /// <summary>Number of (property, slot) entries allocated so far, including entries that have since
    /// been emptied by <see cref="Unset{T}"/> (an entry is never deallocated once created).</summary>
    public int EntryCount => _entryCount;

    private static int IndexOf(UIPilotProperty property, UIValueSlot slot) => (int)property * SlotCount + (int)slot;

    private static bool ValuesEqual<T>(UIResolvedValue<T> a, UIResolvedValue<T> b, IEqualityComparer<T> comparer)
        => a.IsSet == b.IsSet && (!a.IsSet || comparer.Equals(a.Value, b.Value));

    private static InvalidOperationException TypeMismatch(UIPilotProperty property, UIValueSlot slot, Type existing, Type requested)
        => new($"Resolved property entry for ({property}, {slot}) was first written as {existing} and cannot be read or written as {requested}.");

    private Entry<T> GetOrCreateEntry<T>(UIPilotProperty property, UIValueSlot slot, int index)
    {
        _entries ??= new Entry[PropertyCount * SlotCount];
        var existing = _entries[index];
        if (existing == null)
        {
            Entry<T> created = new();
            _entries[index] = created;
            _entryCount++;
            return created;
        }

        if (existing is Entry<T> typed)
            return typed;

        throw TypeMismatch(property, slot, existing.ValueType, typeof(T));
    }

    private Entry<T> GetExistingEntry<T>(UIPilotProperty property, UIValueSlot slot, int index)
    {
        var existing = _entries?[index];
        if (existing == null)
            return null;

        if (existing is Entry<T> typed)
            return typed;

        throw TypeMismatch(property, slot, existing.ValueType, typeof(T));
    }

    private static int FindInsertionIndex<T>(List<UIResolvedValue<T>> contributions, UIValuePrecedence precedence)
    {
        for (var i = 0; i < contributions.Count; i++)
        {
            if (contributions[i].Source.Precedence < precedence)
                return i;
        }
        return contributions.Count;
    }

    /// <summary>
    /// Records (or replaces) the contribution of <paramref name="source"/>.Kind for (<paramref name="property"/>,
    /// <paramref name="slot"/>). Equal precedence (the same kind written twice) replaces the previous
    /// contribution: last writer wins among equals. <paramref name="effective"/> is always the
    /// highest-precedence set contribution after the write; <paramref name="effectiveChanged"/> is true
    /// only when that effective VALUE actually changed (per <paramref name="comparer"/>) — writing the
    /// same value from a lower source, or the current effective value again from a higher source, both
    /// report false. The entry is typed by its first <see cref="Set{T}"/> call; a later call with a
    /// different <c>T</c> on the same (property, slot) throws <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>True when this call added a brand new contribution kind to the entry; false when it
    /// replaced an existing contribution of the same kind.</returns>
    public bool Set<T>(UIPilotProperty property, UIValueSlot slot, T value, UIValueResolutionSource source, IEqualityComparer<T> comparer, out bool effectiveChanged, out UIResolvedValue<T> effective)
    {
        comparer ??= EqualityComparer<T>.Default;
        var index = IndexOf(property, slot);
        var entry = GetOrCreateEntry<T>(property, slot, index);

        var oldEffective = entry.Contributions.Count > 0
            ? entry.Contributions[0]
            : UIResolvedValue<T>.Unset(entry.EmptiedInvalidation);

        UIResolvedValue<T> contribution = new(value, source);
        var existingIndex = entry.Kinds.IndexOf(source.Kind);
        bool added;
        if (existingIndex >= 0)
        {
            entry.Contributions[existingIndex] = contribution;
            added = false;
        }
        else
        {
            var insertAt = FindInsertionIndex(entry.Contributions, source.Precedence);
            entry.Contributions.Insert(insertAt, contribution);
            entry.Kinds.Insert(insertAt, source.Kind);
            added = true;
        }

        effective = entry.Contributions[0];
        effectiveChanged = !ValuesEqual(oldEffective, effective, comparer);
        return added;
    }

    /// <summary>
    /// Removes the contribution of <paramref name="kind"/> for (<paramref name="property"/>,
    /// <paramref name="slot"/>) if present. <paramref name="effective"/> becomes the new winner (the next
    /// highest-precedence remaining contribution), or <see cref="UIResolvedValue{T}.Unset"/> (carrying the
    /// invalidation of the contribution that was just removed) when the entry becomes empty —
    /// <paramref name="effectiveChanged"/> is false in that fall-back case: the caller keeps its current
    /// CLR value and raises no notification, exactly like a dynamic resource that no longer resolves.
    /// </summary>
    /// <returns>True when a contribution of <paramref name="kind"/> was actually present and removed.</returns>
    public bool Unset<T>(UIPilotProperty property, UIValueSlot slot, UIValueSourceKind kind, IEqualityComparer<T> comparer, out bool effectiveChanged, out UIResolvedValue<T> effective)
    {
        comparer ??= EqualityComparer<T>.Default;
        var index = IndexOf(property, slot);
        var entry = GetExistingEntry<T>(property, slot, index);
        if (entry == null)
        {
            effective = UIResolvedValue<T>.Unset(UIInvalidationKind.None);
            effectiveChanged = false;
            return false;
        }

        var oldEffective = entry.Contributions.Count > 0
            ? entry.Contributions[0]
            : UIResolvedValue<T>.Unset(entry.EmptiedInvalidation);

        var existingIndex = entry.Kinds.IndexOf(kind);
        if (existingIndex < 0)
        {
            effective = oldEffective;
            effectiveChanged = false;
            return false;
        }

        var removed = entry.Contributions[existingIndex];
        entry.Contributions.RemoveAt(existingIndex);
        entry.Kinds.RemoveAt(existingIndex);

        if (entry.Contributions.Count == 0)
        {
            // The entry has no remaining contribution: the documented fall-back applies (the caller
            // keeps its current CLR value and raises no notification), so effectiveChanged is always
            // false here, even though the winner transitions from set to unset.
            entry.EmptiedInvalidation = removed.Source.Invalidation;
            effective = UIResolvedValue<T>.Unset(entry.EmptiedInvalidation);
            effectiveChanged = false;
        }
        else
        {
            effective = entry.Contributions[0];
            effectiveChanged = !ValuesEqual(oldEffective, effective, comparer);
        }

        return true;
    }

    /// <summary>
    /// Reads the current winner for (<paramref name="property"/>, <paramref name="slot"/>). Returns
    /// false (with <paramref name="winner"/> set to <see cref="UIResolvedValue{T}.Unset"/>) for an entry
    /// that was never written, or that has since been emptied by <see cref="Unset{T}"/>.
    /// </summary>
    public bool TryGetWinner<T>(UIPilotProperty property, UIValueSlot slot, out UIResolvedValue<T> winner)
    {
        var index = IndexOf(property, slot);
        var entry = GetExistingEntry<T>(property, slot, index);
        if (entry == null)
        {
            winner = UIResolvedValue<T>.Unset(UIInvalidationKind.None);
            return false;
        }

        if (entry.Contributions.Count == 0)
        {
            winner = UIResolvedValue<T>.Unset(entry.EmptiedInvalidation);
            return false;
        }

        winner = entry.Contributions[0];
        return true;
    }

    /// <summary>
    /// Reads the specific contribution of <paramref name="kind"/> for (<paramref name="property"/>,
    /// <paramref name="slot"/>), regardless of whether it is the current winner.
    /// </summary>
    public bool TryGetContribution<T>(UIPilotProperty property, UIValueSlot slot, UIValueSourceKind kind, out UIResolvedValue<T> contribution)
    {
        var index = IndexOf(property, slot);
        var entry = GetExistingEntry<T>(property, slot, index);
        if (entry != null)
        {
            var existingIndex = entry.Kinds.IndexOf(kind);
            if (existingIndex >= 0)
            {
                contribution = entry.Contributions[existingIndex];
                return true;
            }
        }

        contribution = UIResolvedValue<T>.Unset(UIInvalidationKind.None);
        return false;
    }

    /// <summary>
    /// True once (<paramref name="property"/>, <paramref name="slot"/>) has been written, even if every contribution
    /// has since been removed by <see cref="Unset{T}"/> (an entry is never deallocated): the CLR value of an emptied
    /// entry may be a value kept after its last contribution was removed rather than the owner's own default.
    /// </summary>
    public bool IsWritten(UIPilotProperty property, UIValueSlot slot) => _entries?[IndexOf(property, slot)] != null;

    /// <summary>
    /// Lists the source kinds currently contributing to (<paramref name="property"/>, <paramref name="slot"/>),
    /// highest precedence first. Returns a shared empty list (no allocation) for an entry that was never
    /// written or that has since been emptied.
    /// </summary>
    public IReadOnlyList<UIValueSourceKind> Contributions(UIPilotProperty property, UIValueSlot slot)
    {
        if (_entries == null)
            return EmptyKinds;

        var entry = _entries[IndexOf(property, slot)];
        return entry?.KindsView ?? EmptyKinds;
    }

    /// <summary>
    /// Type-erased diagnostic view of every contribution currently recorded for (<paramref name="property"/>,
    /// <paramref name="slot"/>), highest precedence first, each value boxed as <c>object</c>. Unlike
    /// <see cref="TryGetWinner{T}"/>, this is the raw store content: it never performs any read-time
    /// fall-back (no <c>Inherited</c>/<c>Theme</c> synthesis, no dormancy substitution). Returns a shared
    /// empty list (no allocation) for an entry that was never written or that has since been emptied.
    /// </summary>
    public IReadOnlyList<UIResolvedContribution> EnumerateContributions(UIPilotProperty property, UIValueSlot slot)
    {
        if (_entries == null)
            return EmptyContributions;

        var entry = _entries[IndexOf(property, slot)];
        if (entry == null || entry.KindsView.Count == 0)
            return EmptyContributions;

        List<UIResolvedContribution> result = new();
        entry.CopyContributionsTo(result);
        return result;
    }
}