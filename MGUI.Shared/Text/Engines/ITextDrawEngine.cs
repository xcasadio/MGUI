using System;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;

namespace MGUI.Shared.Text.Engines
{
    /// <summary>
    /// Backend-neutral text draw contract used by concrete runtimes to render already-resolved fonts.
    /// Core UI code should continue to depend on <see cref="ITextMeasurementEngine"/> and route draw calls through <see cref="IUIDrawContext"/>.
    /// </summary>
    public interface ITextDrawEngine
    {
        void DrawText(
            IUIDrawContext drawContext,
            ResolvedFont font,
            string text,
            Vector2 position,
            Color color,
            Vector2 origin,
            float scale,
            float rotation = 0f,
            float depth = 0f,
            UIDrawFlip flip = UIDrawFlip.None);

        /// <summary>Draws <paramref name="text"/> without requiring a <see cref="string"/>, for text kept in a reused buffer.<para/>
        /// The default implementation builds a string and calls the string overload: an engine overrides it to draw without allocating.</summary>
        void DrawText(
            IUIDrawContext drawContext,
            ResolvedFont font,
            ReadOnlySpan<char> text,
            Vector2 position,
            Color color,
            Vector2 origin,
            float scale,
            float rotation = 0f,
            float depth = 0f,
            UIDrawFlip flip = UIDrawFlip.None)
            => DrawText(drawContext, font, text.ToString(), position, color, origin, scale, rotation, depth, flip);
    }
}
