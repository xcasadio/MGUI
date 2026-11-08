using Microsoft.Xna.Framework;

namespace MGUI.Shared.Assets
{
    /// <summary>A host-created, per-element handle to a playing video (ADR-0026).<para/>
    /// Like <see cref="IUIAnimatedImage"/>, it advances by elapsed wall-clock time via <see cref="Advance(TimeSpan)"/> and exposes its
    /// current frame. Unlike an animation it plays once: <see cref="IsEnded"/> turns true when the last frame has been shown and the last
    /// frame stays displayed.<para/>
    /// Each <c>MGUI.Core.UI.MGVideo</c> whose <c>SourceName</c> resolves to a video gets its own instance from
    /// <see cref="IUIAssetProvider.TryCreateVideo(string, out IUIVideo)"/>, and disposes it when that element's source changes or when the
    /// element is disposed. The decoding, the pixels and the clock are the host's: MGUI only advances the handle, draws
    /// <see cref="CurrentImage"/> and reports the end.</summary>
    public interface IUIVideo : IDisposable
    {
        /// <summary>Advances the video by the given amount of wall-clock time, possibly changing the current frame and <see cref="IsEnded"/>.</summary>
        /// <param name="elapsed">The amount of wall-clock time that passed since the previous call.</param>
        void Advance(TimeSpan elapsed);

        /// <summary>Restarts the video from its first frame and clears <see cref="IsEnded"/>.</summary>
        void Restart();

        /// <summary>The image to draw for the current frame, or null if nothing should be drawn. A host that updates one texture in place
        /// returns the same instance on every frame, which keeps the layout of the element untouched.</summary>
        IUIImageResource CurrentImage { get; }

        /// <summary>An optional source rectangle within <see cref="CurrentImage"/> to draw for the current frame, or null to draw the whole image.</summary>
        Rectangle? CurrentSourceRect { get; }

        /// <summary>True once the video has played to its end (or can no longer play). The last frame stays available through <see cref="CurrentImage"/>.</summary>
        bool IsEnded { get; }
    }
}
