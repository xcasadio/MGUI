using System;
using System.IO;
using FontStashSharp;
using MGUI.FontStashSharp;
using MGUI.Shared.Text;
using Xunit;

namespace MGUI.Tests.Text;

/// <summary>
/// Covers <see cref="FontStashSharpTextEngine.RemoveStaticFont"/>, the inverse of
/// <see cref="FontStashSharpTextEngine.AddStaticFont"/>. A caller that frees a bitmap font must be able to
/// take it out of a text engine that outlives it, or the engine would keep resolving the family to a font
/// whose texture is gone.
/// <para/>
/// A real <c>StaticSpriteFont</c> needs a <c>Texture2D</c>, hence a graphics device; like the engine's own
/// bitmap-font registration tests, these register a fixed-size <see cref="SpriteFontBase"/> rasterized on the
/// CPU from a TTF instead. What is under test is the family/style registration, not BMFont parsing.
/// </summary>
public class FontStashSharpStaticFontRemovalTests
{
    private const string StaticFamily = "font3";

    private static FontStashSharpTextEngine CreateEngine(out SpriteFontBase staticFont)
    {
        string ttfPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "arial.ttf");
        byte[] ttfData = File.ReadAllBytes(ttfPath);

        var engine = new FontStashSharpTextEngine();
        var fontSystem = new FontSystem();
        fontSystem.AddFont(ttfData);
        engine.AddFontSystem("Arial", CustomFontStyles.Normal, fontSystem, ttfData);

        staticFont = fontSystem.GetFont(40);
        return engine;
    }

    private static ResolvedFont ResolveStatic(FontStashSharpTextEngine engine)
        => engine.ResolveFont(new FontSpec(StaticFamily, 16, CustomFontStyles.Normal));

    [Fact]
    public void RemoveStaticFont_ThenResolveFont_FallsBack()
    {
        var engine = CreateEngine(out SpriteFontBase staticFont);
        engine.AddStaticFont(StaticFamily, CustomFontStyles.Normal, staticFont);
        Assert.False(ResolveStatic(engine).IsFallback);

        Assert.True(engine.RemoveStaticFont(StaticFamily, CustomFontStyles.Normal));

        Assert.True(ResolveStatic(engine).IsFallback);
    }

    [Fact]
    public void RemoveStaticFont_ForAFamilyNeverRegistered_ReturnsFalse()
    {
        var engine = CreateEngine(out _);

        Assert.False(engine.RemoveStaticFont(StaticFamily, CustomFontStyles.Normal));
    }

    [Fact]
    public void RemoveStaticFont_InvalidatesAResolutionAlreadyCached()
    {
        var engine = CreateEngine(out SpriteFontBase staticFont);
        engine.AddStaticFont(StaticFamily, CustomFontStyles.Normal, staticFont);

        // The same spec, resolved once so the engine caches it, then again after the removal: a cache that
        // survived the removal would hand back the removed font.
        ResolvedFont before = ResolveStatic(engine);
        Assert.False(before.IsFallback);
        Assert.Equal(staticFont.LineHeight, before.LineHeight);

        engine.RemoveStaticFont(StaticFamily, CustomFontStyles.Normal);

        ResolvedFont after = ResolveStatic(engine);
        Assert.True(after.IsFallback);
    }

    [Fact]
    public void RemoveStaticFont_OnlyRemovesTheGivenStyle()
    {
        var engine = CreateEngine(out SpriteFontBase staticFont);
        engine.AddStaticFont(StaticFamily, CustomFontStyles.Normal, staticFont);
        engine.AddStaticFont(StaticFamily, CustomFontStyles.Bold, staticFont);

        Assert.True(engine.RemoveStaticFont(StaticFamily, CustomFontStyles.Bold));

        Assert.False(ResolveStatic(engine).IsFallback);
    }

    [Fact]
    public void AddStaticFont_AfterARemoval_RegistersTheFamilyAgain()
    {
        var engine = CreateEngine(out SpriteFontBase staticFont);
        engine.AddStaticFont(StaticFamily, CustomFontStyles.Normal, staticFont);
        engine.RemoveStaticFont(StaticFamily, CustomFontStyles.Normal);

        engine.AddStaticFont(StaticFamily, CustomFontStyles.Normal, staticFont);

        Assert.False(ResolveStatic(engine).IsFallback);
    }
}
