using Microsoft.Xna.Framework;

namespace MGUI.Shared.Assets
{
    /// <summary>A host-created, per-image handle to a playing image animation (ADR-0016, "Animated image sources").<para/>
    /// Advances by elapsed wall-clock time via <see cref="Advance(TimeSpan)"/> and exposes its current frame: an image, an
    /// optional source rectangle within it, and a per-frame draw offset (for frames authored with different pivots).<para/>
    /// Each <c>MGUI.Core.UI.MGImage</c> whose <c>SourceName</c> resolves to an animation gets its own instance from
    /// <see cref="IUIAssetProvider.TryCreateAnimatedImage(string, out IUIAnimatedImage)"/>, and disposes it when that
    /// image's source changes.</summary>
    public interface IUIAnimatedImage : IDisposable
    {
        /// <summary>Advances the animation by the given amount of wall-clock time, possibly changing the current frame.</summary>
        /// <param name="elapsed">The amount of wall-clock time that passed since the previous call.</param>
        void Advance(TimeSpan elapsed);

        /// <summary>Restarts the animation at the given offset from its beginning, discarding whatever elapsed time
        /// <see cref="Advance(TimeSpan)"/> previously accumulated.</summary>
        /// <param name="startOffset">The point in the animation's timeline to restart at.</param>
        void Restart(TimeSpan startOffset);

        /// <summary>The image to draw for the current frame, or null if nothing should be drawn.</summary>
        IUIImageResource CurrentImage { get; }

        /// <summary>An optional source rectangle within <see cref="CurrentImage"/> to draw for the current frame, or null to draw the whole image.</summary>
        Rectangle? CurrentSourceRect { get; }

        /// <summary>A per-frame draw offset, in pixels, added to the element's draw position -- for frames authored with different pivots.<para/>
        /// Default expected value when a frame has no pivot offset: <see cref="Point.Zero"/></summary>
        Point CurrentDrawOffset { get; }
    }
}
