using System;

namespace MGUI.Shared.Rendering.Clipping
{
    /// <summary>An owner of <see cref="ClipHandle"/> that restores by running a callback: for a backend or a test double whose clips are not on a
    /// hot path. The callback is what allocates; the handle and its slot do not (ADR-0031).</summary>
    public sealed class CallbackClipHandleOwner : IClipSlotRestorer
    {
        private readonly ClipSlots _Slots;

        public CallbackClipHandleOwner()
        {
            _Slots = new ClipSlots(this);
        }

        /// <summary>How many clips are in force.</summary>
        public int InForceCount => _Slots.InForceCount;

        /// <summary>Returns a handle that reports <paramref name="Resolution"/> and whose first <see cref="ClipHandle.Dispose"/> runs <paramref name="Restore"/>.</summary>
        public ClipHandle Push(ClipResolveResult Resolution, Action Restore)
            => _Slots.Push(ClipSlotState.WithCallback(Resolution.Strategy, Resolution, Restore));

        /// <summary>Forgets every clip in force without running its callback: the handles handed out so far release nothing.</summary>
        public void ReleaseAll() => _Slots.ReleaseAll();

        void IClipSlotRestorer.RestoreClip(in ClipSlotState State) => State.Restore();
    }
}
