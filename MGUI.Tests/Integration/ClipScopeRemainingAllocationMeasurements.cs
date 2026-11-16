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

/// <summary>What <see cref="DrawTransaction"/> still allocates around clips, written to the test output and asserted nowhere: the stencil clip, which
/// keeps closures and a <see cref="ClipResolveResult"/> copy per push, and the temporary transform change, which derives a <see cref="DrawSettings"/> per call.
/// The temporary changes of settings and of render target allocate nothing, which is asserted (ADR-0031). The mask clip is not measured: with the capabilities of <see cref="ClipBackendCapabilities.Default"/>
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
    public void WhatTheStencilClipAndTheTemporaryChangesStillAllocate()
    {
        (long stencil, long settings, long transform) = GpuDeviceHost.Instance.Invoke(() =>
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

            dt.Dispose();
            device.SetRenderTarget(null);
            return (stencilBytes, settingsBytes, transformBytes);
        });

        _Output.WriteLine($"stencil clip push+dispose, {MeasuredPairs} pairs: {stencil} bytes ({stencil / MeasuredPairs} per pair)");
        _Output.WriteLine($"SetDrawSettingsTemporary + dispose, {MeasuredPairs} pairs: {settings} bytes ({settings / MeasuredPairs} per pair)");
        _Output.WriteLine($"SetTransformTemporary + dispose, {MeasuredPairs} pairs: {transform} bytes ({transform / MeasuredPairs} per pair)");
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
