using System;
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

        /// <summary>Measures <paramref name="text"/> without requiring a <see cref="string"/>, for text kept in a reused buffer.<para/>
        /// The default implementation builds a string and calls <see cref="MeasureText(ResolvedFont, string)"/>: an engine overrides it to
        /// measure without allocating. The result is the one the string overload gives for the same characters.</summary>
        Vector2 MeasureText(ResolvedFont font, ReadOnlySpan<char> text) => MeasureText(font, text.ToString());

        GlyphMetrics MeasureGlyph(ResolvedFont font, char c);
        float GetLineHeight(ResolvedFont font);
        float GetSpaceWidth(ResolvedFont font);
        void InvalidateCache();
    }
}