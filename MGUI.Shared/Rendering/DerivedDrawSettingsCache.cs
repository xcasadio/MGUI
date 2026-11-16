using Microsoft.Xna.Framework;

namespace MGUI.Shared.Rendering
{
    /// <summary>The settings a draw transaction derived for its temporary transforms and effects and for its stencil clips, kept so that the same change finds its settings
    /// again instead of creating them (ADR-0031): a transform or an effect that keeps its value from frame to frame allocates nothing.<para/>
    /// An entry is found by the settings it derives from and by the value that replaces their transform or effect. Equal settings are the same
    /// settings (ADR-0029), so the settings of a new frame find the entries of the frame before.<para/>
    /// Holds at most <see cref="Capacity"/> entries; a new one replaces the oldest. A value that is new at each use (a transform animated every
    /// frame) therefore still creates one <see cref="DrawSettings"/> per new value.</summary>
    public sealed class DerivedDrawSettingsCache
    {
        public const int Capacity = 32;

        private enum EntryKind
        {
            Transform,
            Effect,
            Stencil
        }

        private struct Entry
        {
            public DrawSettings Source;
            public DrawSettings Derived;
            public Matrix Transform;
            public object Effect;
            public EntryKind Kind;
            public BlendType BlendType;
            public DepthStencilType DepthStencilType;
            public int StencilReference;
        }

        private readonly Entry[] _Entries = new Entry[Capacity];
        private int _Count;
        private int _Next;

        /// <summary><paramref name="Source"/> with another transform; <paramref name="Source"/> itself when it already has that transform.</summary>
        public DrawSettings WithTransform(DrawSettings Source, Matrix Transform)
        {
            if (Source.Transform == Transform)
            {
                return Source;
            }

            for (int i = 0; i < _Count; i++)
            {
                ref Entry entry = ref _Entries[i];
                if (entry.Kind == EntryKind.Transform && entry.Transform == Transform && IsSameSettings(entry.Source, Source))
                {
                    return entry.Derived;
                }
            }

            DrawSettings derived = Source with { Transform = Transform };
            Add(new Entry { Source = Source, Derived = derived, Kind = EntryKind.Transform, Transform = Transform });
            return derived;
        }

        /// <summary><paramref name="Source"/> with another backend effect; <paramref name="Source"/> itself when it already has that effect.</summary>
        public DrawSettings WithEffect(DrawSettings Source, object Effect)
        {
            if (ReferenceEquals(Source.BackendEffect, Effect))
            {
                return Source;
            }

            for (int i = 0; i < _Count; i++)
            {
                ref Entry entry = ref _Entries[i];
                if (entry.Kind == EntryKind.Effect && ReferenceEquals(entry.Effect, Effect) && IsSameSettings(entry.Source, Source))
                {
                    return entry.Derived;
                }
            }

            DrawSettings derived = Source with { BackendEffect = Effect };
            Add(new Entry { Source = Source, Derived = derived, Kind = EntryKind.Effect, Effect = Effect });
            return derived;
        }

        /// <summary><paramref name="Source"/> with another blend, depth and stencil test and stencil reference: the three settings a stencil clip
        /// derives at each depth (write, read, restore).</summary>
        public DrawSettings WithStencil(DrawSettings Source, BlendType BlendType, DepthStencilType DepthStencilType, int StencilReference)
        {
            for (int i = 0; i < _Count; i++)
            {
                ref Entry entry = ref _Entries[i];
                if (entry.Kind == EntryKind.Stencil && entry.StencilReference == StencilReference && entry.DepthStencilType == DepthStencilType
                    && entry.BlendType == BlendType && IsSameSettings(entry.Source, Source))
                {
                    return entry.Derived;
                }
            }

            DrawSettings derived = Source with { BlendType = BlendType, DepthStencilType = DepthStencilType, StencilReference = StencilReference };
            Add(new Entry
            {
                Source = Source,
                Derived = derived,
                Kind = EntryKind.Stencil,
                BlendType = BlendType,
                DepthStencilType = DepthStencilType,
                StencilReference = StencilReference
            });
            return derived;
        }

        /// <summary>Forgets every entry, and with them the effects and settings they hold.</summary>
        public void Clear()
        {
            System.Array.Clear(_Entries, 0, _Count);
            _Count = 0;
            _Next = 0;
        }

        private static bool IsSameSettings(DrawSettings Kept, DrawSettings Source)
            => ReferenceEquals(Kept, Source) || Kept.Equals(Source);

        private void Add(Entry Entry)
        {
            if (_Count < Capacity)
            {
                _Entries[_Count] = Entry;
                _Count++;
                return;
            }

            _Entries[_Next] = Entry;
            _Next = (_Next + 1) % Capacity;
        }
    }
}
