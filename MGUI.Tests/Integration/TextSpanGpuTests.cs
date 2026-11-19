using System;
using System.Collections.Generic;
using System.IO;
using FontStashSharp;
using MGUI.Backend.MonoGame;
using MGUI.FontStashSharp;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace MGUI.Tests.Integration;

/// <summary>
/// Real-GPU proof of the <see cref="ReadOnlySpan{T}"/> text overloads of both text engines and of <see cref="DrawTransaction"/>: a span of
/// characters measures and draws exactly like the string with the same characters, and neither the measure nor the draw allocates.<para/>
/// The draws go through <see cref="DrawTransaction.DrawTextViaEngine(ResolvedFont, ReadOnlySpan{char}, Vector2, Color, Vector2, float, float, float, UIDrawFlip)"/>,
/// so one test covers the transaction and the engine behind it. All GPU work happens through <see cref="GpuDeviceHost"/> on its single thread.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class TextSpanGpuTests
{
    private const int TargetWidth = 128;
    private const int TargetHeight = 32;
    private const int WarmupIterations = 400;
    private const int MeasuredIterations = 200;
    private const string Sample = "Value 10.42";

    public enum Engine
    {
        SpriteFont,
        FontStashSharp
    }

    [GpuTheory]
    [InlineData(Engine.SpriteFont)]
    [InlineData(Engine.FontStashSharp)]
    public void MeasureText_OfASpan_GivesTheSizeOfTheSameString(Engine engine)
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            Fixture fixture = Fixture.Create(engine);
            char[] buffer = ("##" + Sample + "##").ToCharArray();

            Vector2 expected = fixture.TextEngine.MeasureText(fixture.Font, Sample);
            Vector2 actual = fixture.TextEngine.MeasureText(fixture.Font, buffer.AsSpan(2, Sample.Length));

            Assert.True(expected.X > 0);
            Assert.Equal(expected, actual);
            Assert.Equal(Vector2.Zero, fixture.TextEngine.MeasureText(fixture.Font, ReadOnlySpan<char>.Empty));
        });
    }

    [GpuTheory]
    [InlineData(Engine.SpriteFont)]
    [InlineData(Engine.FontStashSharp)]
    public void MeasureText_OfASpan_AllocatesNothing(Engine engine)
    {
        long allocated = GpuDeviceHost.Instance.Invoke(() =>
        {
            Fixture fixture = Fixture.Create(engine);
            char[] buffer = Sample.ToCharArray();
            for (int i = 0; i < WarmupIterations; i++)
            {
                fixture.TextEngine.MeasureText(fixture.Font, buffer.AsSpan());
            }

            long before = AllocationWindow.Start();
            for (int i = 0; i < MeasuredIterations; i++)
            {
                fixture.TextEngine.MeasureText(fixture.Font, buffer.AsSpan());
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        });

        Assert.Equal(0, allocated);
    }

    [GpuTheory]
    [InlineData(Engine.SpriteFont)]
    [InlineData(Engine.FontStashSharp)]
    public void DrawTextViaEngine_OfASpan_DrawsThePixelsOfTheSameString(Engine engine)
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            Fixture fixture = Fixture.Create(engine);
            char[] buffer = Sample.ToCharArray();

            Color[] expected = fixture.Render(dt => dt.DrawTextViaEngine(fixture.Font, Sample, new Vector2(2, 2), Color.White, fixture.Font.DrawOrigin, fixture.Font.ExactScale));
            Color[] actual = fixture.Render(dt => dt.DrawTextViaEngine(fixture.Font, buffer.AsSpan(), new Vector2(2, 2), Color.White, fixture.Font.DrawOrigin, fixture.Font.ExactScale));

            Assert.Contains(expected, x => x != Color.Black);
            Assert.Equal(expected, actual);
        });
    }

    [GpuTheory]
    [InlineData(Engine.SpriteFont)]
    [InlineData(Engine.FontStashSharp)]
    public void DrawTextViaEngine_OfASpan_AllocatesNothing(Engine engine)
    {
        long allocated = GpuDeviceHost.Instance.Invoke(() =>
        {
            Fixture fixture = Fixture.Create(engine);
            char[] buffer = Sample.ToCharArray();
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = new(device, TargetWidth, TargetHeight, false, SurfaceFormat.Color, DepthFormat.None);
            device.SetRenderTarget(target);
            try
            {
                //  A first transaction grows the sprite batch to more items than the measured one draws, and rasterizes the glyphs.
                using (DrawTransaction warmup = fixture.CreateTransaction())
                {
                    for (int i = 0; i < WarmupIterations; i++)
                    {
                        warmup.DrawTextViaEngine(fixture.Font, buffer.AsSpan(), new Vector2(2, 2), Color.White, fixture.Font.DrawOrigin, fixture.Font.ExactScale);
                    }
                }

                using DrawTransaction dt = fixture.CreateTransaction();
                //  The first draw begins the batch: it is outside the measured window.
                dt.DrawTextViaEngine(fixture.Font, buffer.AsSpan(), new Vector2(2, 2), Color.White, fixture.Font.DrawOrigin, fixture.Font.ExactScale);

                long before = AllocationWindow.Start();
                for (int i = 0; i < MeasuredIterations; i++)
                {
                    dt.DrawTextViaEngine(fixture.Font, buffer.AsSpan(), new Vector2(2, 2), Color.White, fixture.Font.DrawOrigin, fixture.Font.ExactScale);
                }
                return GC.GetAllocatedBytesForCurrentThread() - before;
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        Assert.Equal(0, allocated);
    }

    private sealed class Fixture
    {
        private readonly MainRenderer Renderer;

        public ITextMeasurementEngine TextEngine => Renderer.TextEngine;
        public ResolvedFont Font { get; }

        private Fixture(MainRenderer renderer, ResolvedFont font)
        {
            Renderer = renderer;
            Font = font;
        }

        public static Fixture Create(Engine engine)
        {
            MainRenderer renderer = MonoGameBackendBootstrap.Create(new GameRenderHost<HeadlessGame>(GpuDeviceHost.Instance.Game)).Renderer;
            string family;
            if (engine == Engine.SpriteFont)
            {
                family = "SpanTestFont";
                SpriteFont spriteFont = CreateSpriteFont(GpuDeviceHost.Instance.GraphicsDevice);
                renderer.FontManager.AddFontSet(new FontSet(family, new Dictionary<SpriteFont, FontMetadata> { [spriteFont] = new FontMetadata(12, false, false) }));
                renderer.TextEngine = new SpriteFontTextEngine(renderer.FontManager);
            }
            else
            {
                family = "Arial";
                byte[] ttfData = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "arial.ttf"));
                FontStashSharpTextEngine fontStashEngine = new();
                FontSystem fontSystem = new();
                fontSystem.AddFont(ttfData);
                fontStashEngine.AddFontSystem(family, CustomFontStyles.Normal, fontSystem, ttfData);
                renderer.TextEngine = fontStashEngine;
            }

            ResolvedFont font = renderer.TextEngine.ResolveFont(FontSpec.Normal(family, 12));
            Assert.True(font.IsAvailable);
            return new Fixture(renderer, font);
        }

        public DrawTransaction CreateTransaction()
            => (DrawTransaction)Renderer.BeginDrawTransaction(DrawSettings.Default, DeferBegin: true, DrawContext.Sprites);

        public Color[] Render(Action<DrawTransaction> draw)
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = new(device, TargetWidth, TargetHeight, false, SurfaceFormat.Color, DepthFormat.None);
            device.SetRenderTarget(target);
            device.Clear(Color.Black);
            using (DrawTransaction dt = CreateTransaction())
            {
                draw(dt);
            }

            device.SetRenderTarget(null);
            Color[] pixels = new Color[TargetWidth * TargetHeight];
            target.GetData(pixels);
            return pixels;
        }

        /// <summary>A SpriteFont built by hand, so the test needs no content pipeline: every printable ASCII character is a white 6x10 block,
        /// except the space, which is empty.</summary>
        private static SpriteFont CreateSpriteFont(GraphicsDevice device)
        {
            Texture2D texture = new(device, 16, 16);
            Color[] texels = new Color[16 * 16];
            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 6; x++)
                {
                    texels[y * 16 + x] = Color.White;
                }
            }
            texture.SetData(texels);

            List<Rectangle> glyphBounds = new();
            List<Rectangle> cropping = new();
            List<char> characters = new();
            List<Vector3> kerning = new();
            for (char c = ' '; c <= '~'; c++)
            {
                characters.Add(c);
                glyphBounds.Add(c == ' ' ? new Rectangle(8, 0, 6, 10) : new Rectangle(0, 0, 6, 10));
                cropping.Add(new Rectangle(0, 0, 6, 10));
                kerning.Add(new Vector3(0, 6, 1));
            }

            return new SpriteFont(texture, glyphBounds, cropping, characters, 12, 0f, kerning, '?');
        }
    }
}
