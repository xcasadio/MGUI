using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.Shared.Text.Engines
{
    /// <summary>
    /// MonoGame-specific text draw contract used by the concrete rendering backend.
    /// Core UI code should not depend on this interface directly.
    /// </summary>
    public interface IMonoGameTextRenderer
    {
        void DrawText(
            SpriteBatch spriteBatch,
            ResolvedFont font,
            string text,
            Vector2 position,
            Color color,
            Vector2 origin,
            float scale,
            float rotation = 0f,
            float depth = 0f,
            SpriteEffects effects = SpriteEffects.None);
    }
}