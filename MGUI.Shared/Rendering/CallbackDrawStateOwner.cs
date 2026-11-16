using System;

namespace MGUI.Shared.Rendering
{
    /// <summary>An owner of <see cref="DrawStateHandle"/> that restores by running a callback: for a backend or a test double whose temporary
    /// changes are not on a hot path. The callback is what allocates; the handle and its slot do not (ADR-0031).</summary>
    public sealed class CallbackDrawStateOwner : IDrawStateHandleOwner
    {
        private readonly GenerationSlots<Action> _Slots = new();

        /// <summary>Returns a handle whose first <see cref="DrawStateHandle.Dispose"/> runs <paramref name="Restore"/>.</summary>
        public DrawStateHandle Push(Action Restore)
        {
            ArgumentNullException.ThrowIfNull(Restore);

            int slot = _Slots.Acquire(Restore, out int generation);
            return new DrawStateHandle(this, slot, generation);
        }

        /// <summary>Ends every change in force without running its callback: the handles handed out so far restore nothing.</summary>
        public void ReleaseAll() => _Slots.ReleaseAll();

        public void ReleaseDrawState(int Slot, int Generation)
        {
            if (_Slots.TryRelease(Slot, Generation, out Action restore))
            {
                restore();
            }
        }
    }
}
