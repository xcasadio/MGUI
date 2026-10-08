using System;
using System.IO;
using FontStashSharp;
using MGUI.FontStashSharp;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Xunit;

namespace MGUI.Tests.Text;

/// <summary>
/// The FontStashSharp side of the line box contract (ADR-0023): <see cref="ResolvedFont.LineHeight"/> spans the ink of
/// every <see cref="LineBoxRepertoire"/> character on the resolved font, and the draw origin is zero.
/// Uses <c>Fonts/arial.ttf</c> from the test output, without MatchSpriteFontSizing, so the engine resolves
/// <c>size × FontSizeScale</c> pixels and no graphics device is needed.
/// </summary>
public class FSSLineBoxTests
{
    private static FontStashSharpTextEngine CreateEngine(out byte[] ttfData)
    {
        ttfData = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "arial.ttf"));

        var engine = new FontStashSharpTextEngine();
        var fontSystem = new FontSystem();
        fontSystem.AddFont(ttfData);
        engine.AddFontSystem("Arial", CustomFontStyles.Normal, fontSystem, ttfData);
        return engine;
    }

    /// <summary>The ink of every repertoire character, one by one, on an independent font of the same pixel size.</summary>
    private static (float Top, float Bottom) MeasureRepertoireInk(byte[] ttfData, float pixelSize)
    {
        var fontSystem = new FontSystem();
        fontSystem.AddFont(ttfData);
        SpriteFontBase font = fontSystem.GetFont(pixelSize);

        float top = float.MaxValue;
        float bottom = float.MinValue;
        foreach (char c in LineBoxRepertoire.InkedCharacters)
        {
            Bounds ink = font.TextBounds(c.ToString(), Vector2.Zero);
            if (ink.Y2 <= ink.Y)
            {
                continue;
            }

            top = Math.Min(top, ink.Y);
            bottom = Math.Max(bottom, ink.Y2);
        }

        return (top, bottom);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(14)]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(36)]
    [InlineData(48)]
    public void LineHeight_Spans_The_Ink_Of_Every_Repertoire_Character(int size)
    {
        FontStashSharpTextEngine engine = CreateEngine(out byte[] ttfData);

        ResolvedFont font = engine.ResolveFont(new FontSpec("Arial", size, CustomFontStyles.Normal));
        (float top, float bottom) = MeasureRepertoireInk(ttfData, size * engine.FontSizeScale);

        // Accented capitals rise above the FSS draw position: the line box starts above it.
        Assert.True(top < 0f, $"size {size}: the repertoire ink top {top} should rise above the draw position");
        Assert.Equal(MathF.Ceiling(bottom) - MathF.Floor(top), font.LineHeight);
        Assert.Equal(Vector2.Zero, font.DrawOrigin);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(14)]
    public void Measurement_Heights_Are_The_Line_Box(int size)
    {
        FontStashSharpTextEngine engine = CreateEngine(out _);

        ResolvedFont font = engine.ResolveFont(new FontSpec("Arial", size, CustomFontStyles.Normal));

        Assert.Equal(font.LineHeight, engine.MeasureText(font, "Ag").Y);
        Assert.Equal(font.LineHeight, engine.MeasureText(font, "Été").Y);
        Assert.Equal(font.LineHeight, engine.MeasureGlyph(font, 'g').Height);
        Assert.Equal(font.LineHeight, engine.GetLineHeight(font));
    }

    [Fact]
    public void Fallback_Font_Uses_The_Same_Line_Box()
    {
        FontStashSharpTextEngine engine = CreateEngine(out _);

        ResolvedFont registered = engine.ResolveFont(new FontSpec("Arial", 12, CustomFontStyles.Normal));
        ResolvedFont fallback = engine.ResolveFont(new FontSpec("NotRegistered", 12, CustomFontStyles.Normal));

        Assert.True(fallback.IsFallback);
        Assert.Equal(registered.LineHeight, fallback.LineHeight);
        Assert.Equal(Vector2.Zero, fallback.DrawOrigin);
    }
}
