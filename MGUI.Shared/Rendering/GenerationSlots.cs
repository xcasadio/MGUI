using System;

namespace MGUI.Shared.Rendering
{
    /// <summary>The slots a backend keeps for what its handles designate (<see cref="DrawStateHandle"/>, <see cref="Clipping.ClipHandle"/>): each slot holds
    /// the state of one use (what to restore) and a generation (ADR-0031).<para/>
    /// <see cref="Acquire(TState, out int)"/> hands out a slot number and the generation of that use; the pair is live until
    /// <see cref="TryRelease(int, int, out TState)"/> or <see cref="ReleaseAll"/> ends the use, which changes the generation of the slot. A pair that is not
    /// live designates nothing, whatever the slot was used for since. Slots are released in any order.<para/>
    /// Acquiring allocates only when more slots are in use at once than ever before.</summary>
    public sealed class GenerationSlots<TState>
    {
        private TState[] _States;
        private int[] _Generations;
        private bool[] _IsInUse;
        private int[] _FreeSlots;
        private int _FreeCount;
        private int _Count;

        public GenerationSlots(int InitialCapacity = 8)
        {
            if (InitialCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(InitialCapacity));
            }

            _States = new TState[InitialCapacity];
            _Generations = new int[InitialCapacity];
            _IsInUse = new bool[InitialCapacity];
            _FreeSlots = new int[InitialCapacity];
        }

        /// <summary>How many slots are in use.</summary>
        public int InUseCount { get; private set; }

        /// <summary>Takes a slot for <paramref name="State"/> and returns its number; <paramref name="Generation"/> is never 0.</summary>
        public int Acquire(TState State, out int Generation)
        {
            int slot;
            if (_FreeCount > 0)
            {
                _FreeCount--;
                slot = _FreeSlots[_FreeCount];
            }
            else
            {
                if (_Count == _States.Length)
                {
                    Grow();
                }

                slot = _Count;
                _Count++;
                _Generations[slot] = 1;
            }

            _States[slot] = State;
            _IsInUse[slot] = true;
            InUseCount++;
            Generation = _Generations[slot];
            return slot;
        }

        /// <summary>True while the use that <see cref="Acquire(TState, out int)"/> gave this pair for has not been released.</summary>
        public bool IsLive(int Slot, int Generation)
            => (uint)Slot < (uint)_Count && _IsInUse[Slot] && _Generations[Slot] == Generation;

        /// <summary>Reads the state of a live pair. False, and a default <paramref name="State"/>, for a pair that is not live.</summary>
        public bool TryGet(int Slot, int Generation, out TState State)
        {
            if (!IsLive(Slot, Generation))
            {
                State = default;
                return false;
            }

            State = _States[Slot];
            return true;
        }

        /// <summary>Replaces the state of a live pair. False, and nothing changed, for a pair that is not live.</summary>
        public bool TrySet(int Slot, int Generation, TState State)
        {
            if (!IsLive(Slot, Generation))
            {
                return false;
            }

            _States[Slot] = State;
            return true;
        }

        /// <summary>Ends the use of a live pair and gives its state back; the slot is free before this returns, so the caller may acquire while it
        /// restores. False, and nothing changed, for a pair that is not live: one released before, or one of a use that <see cref="ReleaseAll"/> ended.</summary>
        public bool TryRelease(int Slot, int Generation, out TState State)
        {
            if (!IsLive(Slot, Generation))
            {
                State = default;
                return false;
            }

            State = _States[Slot];
            Free(Slot);
            return true;
        }

        /// <summary>Ends every use without restoring anything: every pair handed out so far stops being live.</summary>
        public void ReleaseAll()
        {
            for (int slot = 0; slot < _Count; slot++)
            {
                if (_IsInUse[slot])
                {
                    Free(slot);
                }
            }
        }

        private void Free(int Slot)
        {
            _States[Slot] = default;
            _IsInUse[Slot] = false;

            //  0 is never a generation, so that a default handle designates no use even of slot 0.
            int next = unchecked(_Generations[Slot] + 1);
            _Generations[Slot] = next == 0 ? 1 : next;

            _FreeSlots[_FreeCount] = Slot;
            _FreeCount++;
            InUseCount--;
        }

        private void Grow()
        {
            int capacity = _States.Length * 2;
            Array.Resize(ref _States, capacity);
            Array.Resize(ref _Generations, capacity);
            Array.Resize(ref _IsInUse, capacity);
            Array.Resize(ref _FreeSlots, capacity);
        }
    }
}
