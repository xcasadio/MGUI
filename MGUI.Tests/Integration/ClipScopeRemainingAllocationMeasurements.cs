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
