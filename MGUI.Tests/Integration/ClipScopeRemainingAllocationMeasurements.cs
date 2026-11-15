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
/// keeps closures and a <see cref="ClipResolveResult"/> copy per push, and the temporary settings and transform changes, which each create a
/// <c>TemporaryChange</c> (ADR-0027 leaves them out). The mask clip is not measured: with the capabilities of <see cref="ClipBackendCapabilities.Default"/>
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
            using DrawTransaction dt = (DrawTransaction)renderer.CreateDrawTransaction(DrawSettings.Default, DeferBegin: true, DrawContext.Primitives);

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
}
