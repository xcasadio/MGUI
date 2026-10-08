using System.Collections.Generic;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace MGUI.Tests.Text;

/// <summary>
/// The SpriteFont side of the line box contract (ADR-0023): the line box of a baked size spans the ink of every
/// <see cref="LineBoxRepertoire"/> glyph of every style, and <see cref="SpriteFontTextEngine"/> measures lines by it.
/// The fonts are built in memory (no texture: measurement never reads it), so no graphics device is needed.
/// </summary>
public class SpriteFontLineBoxTests
{
    private const string Family = "LineBoxSynthetic";
    private const int BakedSize = 30;

    private readonly record struct GlyphSpec(char Character, int CropY, int InkHeight);

    /// <summary>Builds a SpriteFont whose glyph ink starts <c>CropY</c> rows below the top of the line and is <c>InkHeight</c> rows tall.</summary>
    private static SpriteFont CreateFont(params GlyphSpec[] glyphs)
    {
        var bounds = new List<Rectangle>();
        var cropping = new List<Rectangle>();
        var characters = new List<char>();
        var kerning = new List<Vector3>();
        foreach (GlyphSpec glyph in glyphs)
        {
            bounds.Add(new Rectangle(0, 0, 4, glyph.InkHeight));
            cropping.Add(new Rectangle(0, glyph.CropY, 5, 20));
            characters.Add(glyph.Character);
            kerning.Add(new Vector3(0, 5, 0));
        }

        return new SpriteFont(null, bounds, cropping, characters, 20, 0, kerning, null);
    }

    /// <summary>Normal: a space with a 1x1 bound at the bottom of the cell, a capital, a descender, an accented capital rising
    /// above the line, and a glyph outside the repertoire reaching far above and below. Bold rises higher and reaches lower.</summary>
    private static FontSet CreateFontSet()
    {
        SpriteFont normal = CreateFont(
            new GlyphSpec(' ', 22, 1),
            new GlyphSpec('A', 2, 10),
            new GlyphSpec('g', 5, 10),
            new GlyphSpec('É', -2, 14),
            new GlyphSpec('Ж', -10, 40));
        SpriteFont bold = CreateFont(
            new GlyphSpec(' ', 22, 1),
            new GlyphSpec('A', -3, 12),
            new GlyphSpec('g', 6, 11));

        return new FontSet(Family, new Dictionary<SpriteFont, FontMetadata>
        {
            [normal] = new FontMetadata(BakedSize, false, false),
            [bold] = new FontMetadata(BakedSize, true, false),
        });
    }

    private static SpriteFontTextEngine CreateEngine()
    {
        var fontManager = new FontManager(new ContentManager(new GameServiceContainer()), Family);
        fontManager.AddFontSet(CreateFontSet());
        return new SpriteFontTextEngine(fontManager);
    }

    [Fact]
    public void LineBox_Spans_The_Ink_Of_Every_Repertoire_Glyph_Of_Every_Style()
    {
        FontSet fontSet = CreateFontSet();

        // Top: bold 'A' at -3. Bottom: bold 'g' at 6 + 11. The space's 1x1 bound (22..23) and U+0416 (outside) are left out.
        Assert.Equal(new FontLineBox(-3, 17), fontSet.LineBoxes[BakedSize]);
        Assert.Equal(20, fontSet.LineBoxes[BakedSize].Height);
    }

    [Fact]
    public void LineBox_Falls_Back_To_The_Line_Spacing_Without_Inked_Repertoire_Glyphs()
    {
        SpriteFont blanks = CreateFont(new GlyphSpec(' ', 22, 1), new GlyphSpec('Ж', -10, 40));
        var fontSet = new FontSet(Family, new Dictionary<SpriteFont, FontMetadata> { [blanks] = new FontMetadata(BakedSize, false, false) });

        Assert.Equal(new FontLineBox(0, blanks.LineSpacing), fontSet.LineBoxes[BakedSize]);
    }

    [Theory]
    [InlineData(BakedSize, CustomFontStyles.Normal)]
    [InlineData(10, CustomFontStyles.Normal)]
    [InlineData(10, CustomFontStyles.Bold)]
    public void Engine_Measures_Lines_By_The_Line_Box_At_The_Exact_Scale(int size, CustomFontStyles style)
    {
        SpriteFontTextEngine engine = CreateEngine();

        ResolvedFont font = engine.ResolveFont(new FontSpec(Family, size, style));

        Assert.False(font.IsFallback);
        Assert.Equal(BakedSize, font.ActualSize);
        Assert.Equal(size / (float)BakedSize, font.ExactScale, 5);
        Assert.Equal(20 * font.ExactScale, font.LineHeight, 4);
        Assert.Equal(Vector2.Zero, font.DrawOrigin);
        Assert.Equal(font.LineHeight, engine.MeasureText(font, "Ag").Y, 4);
        Assert.Equal(font.LineHeight, engine.MeasureGlyph(font, 'g').Height, 4);
        Assert.Equal(font.LineHeight, engine.GetLineHeight(font), 4);
    }
}
