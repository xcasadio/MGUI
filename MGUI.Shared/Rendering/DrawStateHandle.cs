using System;

namespace MGUI.Shared.Rendering
{
    /// <summary>What a <see cref="DrawStateHandle"/> calls to end the temporary change it designates.</summary>
    public interface IDrawStateHandleOwner
    {
        /// <summary>Restores what the use (<paramref name="Slot"/>, <paramref name="Generation"/>) changed. Does nothing when that use is over:
        /// released before, or ended by a re-arm of the owner.</summary>
        public void ReleaseDrawState(int Slot, int Generation);
    }

    /// <summary>A temporary change of draw state (settings, render target, transform, effect) that stays in force until the handle is disposed (ADR-0031).<para/>
    /// A value: creating and disposing one allocates nothing, and copies designate the same change. Only the first <see cref="Dispose"/> of a change
    /// restores; a later one, on any copy, does nothing, even after the backend reused the slot for another change.<para/>
    /// The default handle designates no change and its <see cref="Dispose"/> does nothing, so <c>using (condition ? context.Set...Temporary(...) : default)</c>
    /// is valid. Keep the handle typed as the struct: stored as an <see cref="IDisposable"/> it is boxed.</summary>
    public readonly struct DrawStateHandle : IDisposable
    {
        private readonly IDrawStateHandleOwner _Owner;
        private readonly int _Slot;
        private readonly int _Generation;

        public DrawStateHandle(IDrawStateHandleOwner Owner, int Slot, int Generation)
        {
            _Owner = Owner ?? throw new ArgumentNullException(nameof(Owner));
            _Slot = Slot;
            _Generation = Generation;
        }

        /// <summary>True for the default handle, which designates no change.</summary>
        public bool IsDefault => _Owner == null;

        public void Dispose() => _Owner?.ReleaseDrawState(_Slot, _Generation);
    }
}
