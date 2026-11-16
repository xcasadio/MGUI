using System;

namespace MGUI.Shared.Rendering.Clipping
{
    /// <summary>What a <see cref="ClipHandle"/> calls to read or end the clip it designates.</summary>
    public interface IClipHandleOwner
    {
        /// <summary>Restores the clip that was in force before the use (<paramref name="Slot"/>, <paramref name="Generation"/>). Does nothing when that use
        /// is over: released before, or ended by a re-arm of the owner.</summary>
        public void ReleaseClip(int Slot, int Generation);

        /// <summary>True while the use (<paramref name="Slot"/>, <paramref name="Generation"/>) is not over. Allocates nothing.</summary>
        public bool IsClipInForce(int Slot, int Generation);

        /// <summary>What was asked and how it is satisfied, for a use that is not over. False when it is over.</summary>
        public bool TryGetClipResolution(int Slot, int Generation, out ClipResolveResult Resolution);
    }

    /// <summary>A clip that stays in force until the handle is disposed (ADR-0031).<para/>
    /// A value: pushing and disposing one allocates nothing, and copies designate the same clip. Only the first <see cref="Dispose"/> of a clip restores;
    /// a later one, on any copy, does nothing, even after the backend reused the slot for another clip.<para/>
    /// The default handle designates no clip and its <see cref="Dispose"/> does nothing, so <c>using (definition == null ? default : context.PushClipTemporary(definition))</c>
    /// is valid. Keep the handle typed as the struct: stored as an <see cref="IDisposable"/> it is boxed.</summary>
    public readonly struct ClipHandle : IDisposable
    {
        private readonly IClipHandleOwner _Owner;
        private readonly int _Slot;
        private readonly int _Generation;

        public ClipHandle(IClipHandleOwner Owner, int Slot, int Generation)
        {
            _Owner = Owner ?? throw new ArgumentNullException(nameof(Owner));
            _Slot = Slot;
            _Generation = Generation;
        }

        /// <summary>True for the default handle, which designates no clip.</summary>
        public bool IsDefault => _Owner == null;

        /// <summary>True while the clip is in force: the handle is not the default one and its clip was not released.</summary>
        public bool IsActive => _Owner != null && _Owner.IsClipInForce(_Slot, _Generation);

        /// <summary>What was asked and how it is satisfied. False for the default handle and for a clip that was released.</summary>
        public bool TryGetResolution(out ClipResolveResult Resolution)
        {
            if (_Owner == null)
            {
                Resolution = default;
                return false;
            }

            return _Owner.TryGetClipResolution(_Slot, _Generation, out Resolution);
        }

        /// <summary>What was asked and how it is satisfied.</summary>
        /// <exception cref="InvalidOperationException">The handle is the default one, or its clip was released.</exception>
        public ClipResolveResult Resolution
        {
            get
            {
                if (!TryGetResolution(out ClipResolveResult resolution))
                {
                    throw new InvalidOperationException("The clip of this handle is not in force: the handle is the default one, or the clip was released.");
                }

                return resolution;
            }
        }

        public void Dispose() => _Owner?.ReleaseClip(_Slot, _Generation);
    }
}
