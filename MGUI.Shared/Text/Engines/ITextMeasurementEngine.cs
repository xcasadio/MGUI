using Microsoft.Xna.Framework;

namespace MGUI.Shared.Text.Engines
{
    /// <summary>
    /// Backend-neutral contract for font resolution and text measurement.
    /// Core UI code should depend on this interface rather than backend draw details.
    /// </summary>
    public interface ITextMeasurementEngine
    {
        ResolvedFont ResolveFont(FontSpec spec);
        Vector2 MeasureText(ResolvedFont font, string text);
        GlyphMetrics MeasureGlyph(ResolvedFont font, char c);
        float GetLineHeight(ResolvedFont font);
        float GetSpaceWidth(ResolvedFont font);
        void InvalidateCache();
    }
}