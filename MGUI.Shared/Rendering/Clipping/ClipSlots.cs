using System;
using Microsoft.Xna.Framework;

namespace MGUI.Shared.Rendering.Clipping
{
    /// <summary>What a backend keeps for one clip in force: what the clip is, and what its release restores. A backend fills the fields of the
    /// strategy it used and reads them back in <see cref="IClipSlotRestorer.RestoreClip(in ClipSlotState)"/>.</summary>
    public struct ClipSlotState
    {
        /// <summary>How the clip was pushed, which says what its release restores.</summary>
        public ClipStrategy Strategy;

        /// <summary>What was asked and how it is satisfied. Not set while <see cref="IsRectangleResolutionPending"/> is true.</summary>
        public ClipResolveResult Resolution;

        /// <summary>True for a scissor clip pushed from bounds alone: its <see cref="Resolution"/> is built from <see cref="RectangleBounds"/> and
        /// <see cref="RectangleIntersectsWithCurrentClip"/> if and when a handle reads it.</summary>
        public bool IsRectangleResolutionPending;
        public Rectangle RectangleBounds;
        public bool RectangleIntersectsWithCurrentClip;

        /// <summary>Scissor: the scissor rectangle and scissor test that were in force before the clip.</summary>
        public Rectangle PreviousScissorBounds;
        public bool PreviousScissorState;

        /// <summary>Scissor: false when the push changed nothing, so that the release has nothing to restore.</summary>
        public bool ScissorChanged;

        /// <summary>Stencil: the handle of the settings that read the stencil while the clip is in force, and the stencil depths before and inside the clip.
        /// The geometry to draw again on release is the one of <see cref="Resolution"/>.</summary>
        public DrawStateHandle StencilReadScope;
        public int ParentStencilDepth;
        public int ChildStencilDepth;

        /// <summary>A callback to run on release, for a strategy that does not keep what it restores in the fields above.</summary>
        public Action Restore;

        /// <summary>A clip that changes nothing.</summary>
        public static ClipSlotState None(ClipResolveResult Resolution)
            => new() { Strategy = ClipStrategy.None, Resolution = Resolution };

        /// <summary>A scissor clip with a known resolution.</summary>
        public static ClipSlotState Scissor(ClipResolveResult Resolution, Rectangle PreviousScissorBounds, bool PreviousScissorState, bool ScissorChanged)
            => new()
            {
                Strategy = ClipStrategy.Scissor,
                Resolution = Resolution,
                PreviousScissorBounds = PreviousScissorBounds,
                PreviousScissorState = PreviousScissorState,
                ScissorChanged = ScissorChanged
            };

        /// <summary>A scissor clip pushed from bounds alone, whose resolution is built only if it is read.</summary>
        public static ClipSlotState ScissorRectangle(Rectangle Bounds, bool IntersectWithCurrentClip, Rectangle PreviousScissorBounds, bool PreviousScissorState,
            bool ScissorChanged)
            => new()
            {
                Strategy = ClipStrategy.Scissor,
                IsRectangleResolutionPending = true,
                RectangleBounds = Bounds,
                RectangleIntersectsWithCurrentClip = IntersectWithCurrentClip,
                PreviousScissorBounds = PreviousScissorBounds,
                PreviousScissorState = PreviousScissorState,
                ScissorChanged = ScissorChanged
            };

        /// <summary>A stencil clip: its release disposes <paramref name="StencilReadScope"/>, draws the geometry of <paramref name="Resolution"/> again to
        /// take it off the stencil, and goes back to <paramref name="ParentStencilDepth"/>.</summary>
        public static ClipSlotState Stencil(ClipResolveResult Resolution, DrawStateHandle StencilReadScope, int ParentStencilDepth, int ChildStencilDepth)
            => new()
            {
                Strategy = ClipStrategy.Stencil,
                Resolution = Resolution,
                StencilReadScope = StencilReadScope,
                ParentStencilDepth = ParentStencilDepth,
                ChildStencilDepth = ChildStencilDepth
            };

        /// <summary>A clip of <paramref name="Strategy"/> whose release runs <paramref name="Restore"/>.</summary>
        public static ClipSlotState WithCallback(ClipStrategy Strategy, ClipResolveResult Resolution, Action Restore)
            => new() { Strategy = Strategy, Resolution = Resolution, Restore = Restore ?? throw new ArgumentNullException(nameof(Restore)) };
    }

    /// <summary>What <see cref="ClipSlots"/> calls when a clip is released.</summary>
    public interface IClipSlotRestorer
    {
        /// <summary>Restores what was in force before the clip of <paramref name="State"/>. Its slot is already free: the restore may push clips.</summary>
        public void RestoreClip(in ClipSlotState State);
    }

    /// <summary>The clips in force of a backend and the owner of their handles (ADR-0031): one slot per clip, released in any order, each release
    /// restoring through <see cref="IClipSlotRestorer"/>. Pushing allocates only when more clips are in force at once than ever before.</summary>
    public sealed class ClipSlots : IClipHandleOwner
    {
        private readonly GenerationSlots<ClipSlotState> _Slots = new();
        private readonly IClipSlotRestorer _Restorer;

        public ClipSlots(IClipSlotRestorer Restorer)
        {
            _Restorer = Restorer ?? throw new ArgumentNullException(nameof(Restorer));
        }

        /// <summary>How many clips are in force.</summary>
        public int InForceCount => _Slots.InUseCount;

        /// <summary>Records a clip that the backend just put in force and returns its handle.</summary>
        public ClipHandle Push(in ClipSlotState State)
        {
            int slot = _Slots.Acquire(State, out int generation);
            return new ClipHandle(this, slot, generation);
        }

        /// <summary>Forgets every clip in force without restoring anything: the handles handed out so far release nothing.</summary>
        public void ReleaseAll() => _Slots.ReleaseAll();

        public void ReleaseClip(int Slot, int Generation)
        {
            if (_Slots.TryRelease(Slot, Generation, out ClipSlotState state))
            {
                _Restorer.RestoreClip(in state);
            }
        }

        public bool IsClipInForce(int Slot, int Generation) => _Slots.IsLive(Slot, Generation);

        public bool TryGetClipResolution(int Slot, int Generation, out ClipResolveResult Resolution)
        {
            if (!_Slots.TryGet(Slot, Generation, out ClipSlotState state))
            {
                Resolution = default;
                return false;
            }

            if (state.IsRectangleResolutionPending)
            {
                ClipDefinition rectangle = ClipDefinition.Rectangle(state.RectangleBounds, state.RectangleIntersectsWithCurrentClip);
                state.Resolution = new ClipResolveResult(rectangle, rectangle, ClipStrategy.Scissor, false);
                state.IsRectangleResolutionPending = false;
                _Slots.TrySet(Slot, Generation, state);
            }

            Resolution = state.Resolution;
            return true;
        }
    }
}
