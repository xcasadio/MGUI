using System;
using System.Collections.Generic;
using MGUI.Backend.MonoGame;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using Xunit.Abstractions;

namespace MGUI.Tests.Integration;

/// <summary>What <see cref="DrawTransaction"/> allocates around the stencil clip and the temporary changes of settings, render target, transform and
/// effect, on a real GPU: nothing (ADR-0031). Written to the test output and asserted. Left as a number that is not asserted: a temporary transform
/// whose value is new at each call. The mask clip is not measured: with the capabilities of <see cref="ClipBackendCapabilities.Default"/>
/// the strategy resolver always prefers the stencil, so <see cref="DrawTransaction"/> never reaches it.</summary>
[Collection(GpuDeviceCollection.Name)]
public class ClipScopeRemainingAllocationMeasurements
{
    private const int WarmupPairs = 50;
    private const int MeasuredPairs = 500;

    private readonly ITestOutputHelper _Output;

    public ClipScopeRemainingAllocationMeasurements(ITestOutputHelper output)
    {
        _Output = output;
    }

    [GpuFact]
    public void TheStencilClipAndTheTemporaryChanges_AllocateNothing()
    {
        (long stencil, long settings, long transform, long nestedStencil, long stencilUnderTransform) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = new(device, 128, 128, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            device.SetRenderTarget(target);
            device.Clear(ClearOptions.Target | ClearOptions.Stencil, Color.Black, 1.0f, 0);

            MainRenderer renderer = MonoGameBackendBootstrap.Create(new GameRenderHost<HeadlessGame>(GpuDeviceHost.Instance.Game)).Renderer;
            using DrawTransaction dt = (DrawTransaction)renderer.BeginDrawTransaction(DrawSettings.Default, DeferBegin: true, DrawContext.Primitives);

            ClipGeometry geometry = new(
                new List<Vector2> { new(10, 10), new(100, 10), new(10, 100) },
                new List<int> { 0, 1, 2 });
            ClipDefinition stencilDefinition = ClipDefinition.ArbitraryGeometry(new Rectangle(10, 10, 90, 90), geometry);
            Matrix transform = Matrix.CreateTranslation(1, 2, 0);
            DrawSettings changed = dt.CurrentSettings with { BlendType = BlendType.Opaque };

            long Measure(Action pair)
            {
                for (int i = 0; i < WarmupPairs; i++)
                {
                    pair();
                }

                long before = AllocationWindow.Start();
                for (int i = 0; i < MeasuredPairs; i++)
                {
                    pair();
                }
                return GC.GetAllocatedBytesForCurrentThread() - before;
            }

            long stencilBytes = Measure(() => dt.PushClipTemporary(stencilDefinition).Dispose());
            long settingsBytes = Measure(() => dt.SetDrawSettingsTemporary(changed).Dispose());
            long transformBytes = Measure(() => dt.SetTransformTemporary(transform).Dispose());
            ClipDefinition innerDefinition = ClipDefinition.ArbitraryGeometry(new Rectangle(10, 10, 40, 40),
                new ClipGeometry(new List<Vector2> { new(10, 10), new(50, 10), new(10, 50) }, new List<int> { 0, 1, 2 }));
            long nestedBytes = Measure(() =>
            {
                using (dt.PushClipTemporary(stencilDefinition))
                using (dt.PushClipTemporary(innerDefinition))
                {
                }
            });
            long underTransformBytes = Measure(() =>
            {
                using (dt.SetTransformTemporary(transform))
                using (dt.PushClipTemporary(stencilDefinition))
                {
                }
            });

            dt.Dispose();
            device.SetRenderTarget(null);
            return (stencilBytes, settingsBytes, transformBytes, nestedBytes, underTransformBytes);
        });

        _Output.WriteLine($"stencil clip push+dispose, {MeasuredPairs} pairs: {stencil} bytes ({stencil / MeasuredPairs} per pair)");
        _Output.WriteLine($"SetDrawSettingsTemporary + dispose, {MeasuredPairs} pairs: {settings} bytes ({settings / MeasuredPairs} per pair)");
        _Output.WriteLine($"SetTransformTemporary + dispose, {MeasuredPairs} pairs: {transform} bytes ({transform / MeasuredPairs} per pair)");
        _Output.WriteLine($"two nested stencil clips: {nestedStencil} bytes; a stencil clip under a temporary transform: {stencilUnderTransform} bytes");
        Assert.Equal(0, stencil);
        Assert.Equal(0, settings);
        Assert.Equal(0, transform);
        Assert.Equal(0, nestedStencil);
        Assert.Equal(0, stencilUnderTransform);
    }

    /// <summary>A temporary change of settings or of render target allocates nothing: its handle is a value, and what it restores is kept in a
    /// slot of the transaction (ADR-0031). Called through <see cref="IUIRenderContext"/> where the interface has the method: a handle returned as
    /// an <see cref="IDisposable"/> would be boxed.</summary>
    [GpuFact]
    public void ATemporaryChangeOfSettingsOrOfRenderTarget_AllocatesNothing()
    {
        (long settings, long nested, long renderTarget) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = new(device, 128, 128, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            using RenderTarget2D other = new(device, 64, 64, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            device.SetRenderTarget(target);

            MainRenderer renderer = MonoGameBackendBootstrap.Create(new GameRenderHost<HeadlessGame>(GpuDeviceHost.Instance.Game)).Renderer;
            using DrawTransaction dt = (DrawTransaction)renderer.BeginDrawTransaction(DrawSettings.Default, DeferBegin: true, DrawContext.Primitives);
            IUIRenderContext context = dt;
            DrawSettings changed = dt.CurrentSettings with { BlendType = BlendType.Opaque };
            DrawSettings changedAgain = dt.CurrentSettings with { BlendType = BlendType.Additive };

            long settingsBytes = MeasurePairs(() => context.SetDrawSettingsTemporary(changed).Dispose());
            long nestedBytes = MeasurePairs(() =>
            {
                using (context.SetDrawSettingsTemporary(changed))
                using (context.SetDrawSettingsTemporary(changedAgain))
                {
                }
            });
            long renderTargetBytes = MeasurePairs(() => dt.SetRenderTargetTemporary(other, null).Dispose());

            dt.Dispose();
            device.SetRenderTarget(null);
            return (settingsBytes, nestedBytes, renderTargetBytes);
        });

        _Output.WriteLine($"SetDrawSettingsTemporary + dispose: {settings} bytes; two nested: {nested} bytes; SetRenderTargetTemporary + dispose: {renderTarget} bytes ({MeasuredPairs} pairs each)");
        Assert.Equal(0, settings);
        Assert.Equal(0, nested);
        Assert.Equal(0, renderTarget);
    }

    /// <summary>Only the first dispose of a temporary change restores: a second one, or one on a copy kept after the slot was used again, changes
    /// nothing (ADR-0031).</summary>
    [GpuFact]
    public void AHandleOfAChangeThatIsOver_RestoresNothing()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = new(device, 128, 128, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            using RenderTarget2D other = new(device, 64, 64, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            device.SetRenderTarget(target);

            MainRenderer renderer = MonoGameBackendBootstrap.Create(new GameRenderHost<HeadlessGame>(GpuDeviceHost.Instance.Game)).Renderer;
            using DrawTransaction dt = (DrawTransaction)renderer.BeginDrawTransaction(DrawSettings.Default, DeferBegin: true, DrawContext.Primitives);
            IUIRenderContext context = dt;
            DrawSettings initial = dt.CurrentSettings;
            DrawSettings first = initial with { BlendType = BlendType.Opaque };
            DrawSettings second = initial with { BlendType = BlendType.Additive };

            DrawStateHandle stale = context.SetDrawSettingsTemporary(first);
            Assert.Same(first, dt.CurrentSettings);
            stale.Dispose();
            Assert.Same(initial, dt.CurrentSettings);

            DrawStateHandle live = context.SetDrawSettingsTemporary(second);
            stale.Dispose();
            Assert.Same(second, dt.CurrentSettings);
            live.Dispose();
            live.Dispose();
            Assert.Same(initial, dt.CurrentSettings);

            //  Released out of order: each handle restores what was in force when it was made.
            DrawStateHandle outer = context.SetDrawSettingsTemporary(first);
            DrawStateHandle inner = context.SetDrawSettingsTemporary(second);
            outer.Dispose();
            Assert.Same(initial, dt.CurrentSettings);
            inner.Dispose();
            Assert.Same(first, dt.CurrentSettings);
            dt.SetDrawSettings(initial);

            DrawStateHandle staleTarget = dt.SetRenderTargetTemporary(other, null);
            Assert.Same(other, device.GetRenderTargets()[0].RenderTarget);
            staleTarget.Dispose();
            Assert.Same(target, device.GetRenderTargets()[0].RenderTarget);
            DrawStateHandle liveTarget = dt.SetRenderTargetTemporary(other, null);
            staleTarget.Dispose();
            Assert.Same(other, device.GetRenderTargets()[0].RenderTarget);
            liveTarget.Dispose();
            Assert.Same(target, device.GetRenderTargets()[0].RenderTarget);

            dt.Dispose();
            device.SetRenderTarget(null);
        });
    }

    /// <summary>A temporary transform or effect that keeps its value allocates nothing: the transaction finds the settings it derived for it
    /// (ADR-0031). A transform whose value is new derives one <see cref="DrawSettings"/>: that number is written, not asserted.</summary>
    [GpuFact]
    public void ATemporaryTransformOrEffectThatKeepsItsValue_AllocatesNothing()
    {
        (long transform, long nested, long effect, long changing) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = new(device, 128, 128, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            device.SetRenderTarget(target);

            MainRenderer renderer = MonoGameBackendBootstrap.Create(new GameRenderHost<HeadlessGame>(GpuDeviceHost.Instance.Game)).Renderer;
            using DrawTransaction dt = (DrawTransaction)renderer.BeginDrawTransaction(DrawSettings.Default, DeferBegin: true, DrawContext.Primitives);
            IUIRenderContext context = dt;
            Matrix outer = Matrix.CreateTranslation(1, 2, 0);
            Matrix inner = Matrix.CreateScale(2);
            using BasicEffect basicEffect = new(device);

            long transformBytes = MeasurePairs(() => context.SetTransformTemporary(outer).Dispose());
            long nestedBytes = MeasurePairs(() =>
            {
                using (context.SetTransformTemporary(outer))
                using (context.SetTransformTemporary(inner))
                {
                }
            });
            long effectBytes = MeasurePairs(() => dt.SetEffectTemporary(basicEffect).Dispose());
            int step = 0;
            long changingBytes = MeasurePairs(() => context.SetTransformTemporary(Matrix.CreateTranslation(++step, 0, 0)).Dispose());

            dt.Dispose();
            device.SetRenderTarget(null);
            return (transformBytes, nestedBytes, effectBytes, changingBytes);
        });

        _Output.WriteLine($"SetTransformTemporary + dispose: {transform} bytes; two nested: {nested} bytes; SetEffectTemporary + dispose: {effect} bytes; " +
                          $"a transform whose value is new each time: {changing} bytes ({changing / MeasuredPairs} per pair), {MeasuredPairs} pairs each");
        Assert.Equal(0, transform);
        Assert.Equal(0, nested);
        Assert.Equal(0, effect);
    }

    /// <summary>Squares drawn under nested temporary transforms and a temporary effect land where they land with settings derived at each
    /// change, the first time (settings derived and kept) and the second (settings found again); and not where they land without transform.</summary>
    [GpuFact]
    public void DrawingUnderTemporaryTransformsAndEffect_DrawsThePixelsOfSettingsDerivedAtEachChange()
    {
        (string kept, string keptAgain, string derived, string untransformed) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using SpriteEffect effect = new(device);
            MainRenderer renderer = MonoGameBackendBootstrap.Create(new GameRenderHost<HeadlessGame>(GpuDeviceHost.Instance.Game)).Renderer;
            return (DrawSquares(renderer, effect, 1), DrawSquares(renderer, effect, 1), DrawSquares(renderer, effect, 2), DrawSquares(renderer, effect, 3));
        });

        Assert.Equal(derived, kept);
        Assert.Equal(derived, keptAgain);
        Assert.NotEqual(untransformed, kept);
    }

    /// <summary>Kind 1: temporary changes; kind 2: settings derived at each change; kind 3: no transform and no effect.</summary>
    private static string DrawSquares(MainRenderer renderer, Effect effect, int kind)
    {
        GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
        using RenderTarget2D target = new(device, 128, 128, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        device.SetRenderTarget(target);
        device.Clear(ClearOptions.Target | ClearOptions.Stencil, Color.Black, 1.0f, 0);
        using (DrawTransaction dt = (DrawTransaction)renderer.BeginDrawTransaction(DrawSettings.Default, DeferBegin: false, DrawContext.Sprites))
        {
            Matrix outer = Matrix.CreateTranslation(20, 10, 0);
            Matrix inner = Matrix.CreateScale(2, 2, 1) * outer;
            if (kind == 1)
            {
                using (dt.SetTransformTemporary(outer))
                {
                    dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(0, 0, 10, 10));
                    using (dt.SetTransformTemporary(inner))
                    {
                        dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(10, 10, 10, 10));
                    }

                    using (dt.SetEffectTemporary(effect))
                    {
                        dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(40, 0, 10, 10));
                    }

                    dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(0, 30, 10, 10));
                }

                dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(0, 60, 10, 10));
            }
            else if (kind == 2)
            {
                DrawSettings initial = dt.CurrentSettings;
                dt.SetDrawSettings(initial with { Transform = outer });
                dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(0, 0, 10, 10));
                dt.SetDrawSettings(initial with { Transform = inner });
                dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(10, 10, 10, 10));
                dt.SetDrawSettings(initial with { Transform = outer, BackendEffect = effect });
                dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(40, 0, 10, 10));
                dt.SetDrawSettings(initial with { Transform = outer });
                dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(0, 30, 10, 10));
                dt.SetDrawSettings(initial);
                dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(0, 60, 10, 10));
            }
            else
            {
                dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(0, 0, 10, 10));
                dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(10, 10, 10, 10));
                dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(40, 0, 10, 10));
                dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(0, 30, 10, 10));
                dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(0, 60, 10, 10));
            }
        }

        device.SetRenderTarget(null);
        Color[] pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        System.Text.StringBuilder text = new(pixels.Length);
        foreach (Color pixel in pixels)
        {
            text.Append(pixel == Color.Black ? '.' : pixel == Color.White ? '#' : '?');
        }

        return text.ToString();
    }

    /// <summary>Two nested stencil clips cut what is drawn inside them to their intersection, the first time (settings derived and kept) and
    /// the second (settings found again), and leave no clip behind.</summary>
    [GpuFact]
    public void TwoNestedStencilClips_CutToTheirIntersection_TheSecondTimeAsTheFirst()
    {
        Color background = Color.Black;
        (Color[] first, Color[] second) = GpuDeviceHost.Instance.Invoke(() =>
        {
            MainRenderer renderer = MonoGameBackendBootstrap.Create(new GameRenderHost<HeadlessGame>(GpuDeviceHost.Instance.Game)).Renderer;
            return (DrawNestedStencilClips(renderer), DrawNestedStencilClips(renderer));
        });

        foreach (Color[] pixels in new[] { first, second })
        {
            Assert.Equal(Color.White, pixels[20 * 128 + 50]);       // inside both clips
            Assert.Equal(background, pixels[20 * 128 + 20]);        // inside the outer clip only
            Assert.Equal(background, pixels[100 * 128 + 100]);      // outside both
            Assert.Equal(Color.White, pixels[61 * 128 + 15]);       // drawn under the outer clip alone, after the inner one
            Assert.Equal(Color.White, pixels[121 * 128 + 121]);     // drawn after both clips
        }

        Assert.Equal(first, second);
    }

    private static Color[] DrawNestedStencilClips(MainRenderer renderer)
    {
        GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
        using RenderTarget2D target = new(device, 128, 128, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        device.SetRenderTarget(target);
        device.Clear(ClearOptions.Target | ClearOptions.Stencil, Color.Black, 1.0f, 0);
        using (DrawTransaction dt = (DrawTransaction)renderer.BeginDrawTransaction(DrawSettings.Default with { RasterizerType = RasterizerType.Solid },
                   DeferBegin: false, DrawContext.Sprites))
        {
            ClipDefinition outer = ClipDefinition.ArbitraryGeometry(new Rectangle(10, 10, 100, 100),
                new ClipGeometry(new List<Vector2> { new(10, 10), new(110, 10), new(10, 110) }, new List<int> { 0, 1, 2 }));
            ClipDefinition inner = ClipDefinition.ArbitraryGeometry(new Rectangle(40, 10, 70, 100),
                new ClipGeometry(new List<Vector2> { new(40, 10), new(110, 10), new(40, 110) }, new List<int> { 0, 1, 2 }));

            using (ClipHandle outerClip = dt.PushClipTemporary(outer))
            {
                Assert.Equal(ClipStrategy.Stencil, outerClip.Resolution.Strategy);
                Assert.Equal(1, outerClip.Resolution.StencilDepth);
                using (ClipHandle innerClip = dt.PushClipTemporary(inner))
                {
                    Assert.Equal(2, innerClip.Resolution.StencilDepth);
                    dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(0, 0, 128, 128));
                }

                //  Back under the outer clip alone: a square that the inner clip would have cut.
                dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(14, 60, 4, 4));
            }

            //  No clip any more.
            dt.DrawTextureTo(dt.WhitePixel, null, new Rectangle(120, 120, 4, 4));
        }

        device.SetRenderTarget(null);
        Color[] pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return pixels;
    }

    private static long MeasurePairs(Action pair)
    {
        for (int i = 0; i < WarmupPairs; i++)
        {
            pair();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredPairs; i++)
        {
            pair();
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
