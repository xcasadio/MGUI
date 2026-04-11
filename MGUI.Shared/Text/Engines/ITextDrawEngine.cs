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
    }
}
