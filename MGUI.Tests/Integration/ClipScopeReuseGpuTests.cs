using System;
using System.Collections.Generic;
using MGUI.Backend.MonoGame;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace MGUI.Tests.Integration;

/// <summary>
/// Real-GPU proof that <see cref="DrawTransaction"/> pushes and pops its clips without allocating (ADR-0027, then the handles of ADR-0031), and that neither
/// changed anything observable: the scissor rectangle and the rasterizer state after every push and every dispose of a nested sequence are the
/// ones the transaction produced before scopes were reused (frozen from the code before the change).<para/>
/// All GPU work happens through <see cref="GpuDeviceHost"/> on its single thread.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class ClipScopeReuseGpuTests
{
    private const int WarmupPairs = 50;
    private const int MeasuredPairs = 500;

    private static readonly Rectangle[] Rectangles =
    {
        new(10, 10, 100, 100),
        new(20, 30, 40, 50),
        new(0, 0, 128, 64),
        new(64, 64, 60, 60),
    };

    private static DrawTransaction CreateTransaction(RasterizerType rasterizer)
    {
        MainRenderer renderer = MonoGameBackendBootstrap.Create(new GameRenderHost<HeadlessGame>(GpuDeviceHost.Instance.Game)).Renderer;
        GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
        device.ScissorRectangle = new Rectangle(0, 0, 128, 128);
        return (DrawTransaction)renderer.BeginDrawTransaction(DrawSettings.Default with { RasterizerType = rasterizer }, DeferBegin: true, DrawContext.Sprites);
    }

    /// <summary>The state a clip leaves behind: forcing a begin and an end makes the sprite batch apply its rasterizer state to the device.</summary>
    private static string Snapshot(DrawTransaction dt, string label)
    {
        dt.ForceBeginDraw(DrawContext.Sprites);
        dt.ForceEndDraw(DrawContext.Sprites);
        GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
        Rectangle scissor = device.ScissorRectangle;
        return $"{label}: scissor={scissor.X},{scissor.Y},{scissor.Width},{scissor.Height} settings={dt.CurrentSettings.RasterizerType} deviceScissorTest={device.RasterizerState.ScissorTestEnable}";
    }

    /// <summary>Four nested levels: a rectangle, a rectangle intersected with it, no clip, a rectangle on top of no clip - then the four disposals.</summary>
    private static List<string> RunNestedSequence(DrawTransaction dt)
    {
        List<string> trace = new() { Snapshot(dt, "start") };

        ClipHandle level1 = dt.PushRectangleClip(new Rectangle(10, 10, 100, 100), true);
        trace.Add(Snapshot(dt, "push1 rect"));
        ClipHandle level2 = dt.PushClipTemporary(ClipDefinition.Rectangle(new Rectangle(50, 50, 100, 100), true));
        trace.Add(Snapshot(dt, "push2 rect intersect"));
        ClipHandle level3 = dt.PushRectangleClip(null, false);
        trace.Add(Snapshot(dt, "push3 no clip"));
        ClipHandle level4 = dt.PushRectangleClip(new Rectangle(20, 20, 30, 30), true);
        trace.Add(Snapshot(dt, "push4 rect over no clip"));

        level4.Dispose();
        trace.Add(Snapshot(dt, "dispose4"));
        level3.Dispose();
        trace.Add(Snapshot(dt, "dispose3"));
        level2.Dispose();
        trace.Add(Snapshot(dt, "dispose2"));
        level1.Dispose();
        trace.Add(Snapshot(dt, "dispose1"));
        return trace;
    }

    /// <summary>The same four levels, with a no-clip definition and a replacing (not intersecting) rectangle, on a transaction that starts without scissor test.</summary>
    private static List<string> RunNestedSequenceFromNoScissor(DrawTransaction dt)
    {
        List<string> trace = new() { Snapshot(dt, "start") };

        ClipHandle level1 = dt.PushClipTemporary(ClipDefinition.None());
        trace.Add(Snapshot(dt, "push1 none definition"));
        ClipHandle level2 = dt.PushRectangleClip(new Rectangle(40, 40, 80, 80), true);
        trace.Add(Snapshot(dt, "push2 rect"));
        ClipHandle level3 = dt.PushRectangleClip(new Rectangle(0, 0, 50, 200), true);
        trace.Add(Snapshot(dt, "push3 rect intersect"));
        ClipHandle level4 = dt.PushRectangleClip(new Rectangle(5, 5, 20, 20), false);
        trace.Add(Snapshot(dt, "push4 rect replace"));

        level4.Dispose();
        trace.Add(Snapshot(dt, "dispose4"));
        level3.Dispose();
        trace.Add(Snapshot(dt, "dispose3"));
        level2.Dispose();
        trace.Add(Snapshot(dt, "dispose2"));
        level1.Dispose();
        trace.Add(Snapshot(dt, "dispose1"));
        return trace;
    }

    [GpuFact]
    public void NestedClips_FromScissorTest_LeaveTheStateTheyLeftBeforeScopesWereReused()
    {
        List<string> trace = GpuDeviceHost.Instance.Invoke(() =>
        {
            using DrawTransaction dt = CreateTransaction(RasterizerType.SolidScissorTest);
            return RunNestedSequence(dt);
        });

        Assert.Equal(Expected_FromScissorTest, trace);
    }

    [GpuFact]
    public void NestedClips_FromNoScissorTest_LeaveTheStateTheyLeftBeforeScopesWereReused()
    {
        List<string> trace = GpuDeviceHost.Instance.Invoke(() =>
        {
            using DrawTransaction dt = CreateTransaction(RasterizerType.Solid);
            return RunNestedSequenceFromNoScissor(dt);
        });

        Assert.Equal(Expected_FromNoScissorTest, trace);
    }

    [GpuFact]
    public void NestedClips_RunTwiceOnTheSameTransaction_GiveTheSameStateTheSecondTime()
    {
        (List<string> first, List<string> second) = GpuDeviceHost.Instance.Invoke(() =>
        {
            using DrawTransaction dt = CreateTransaction(RasterizerType.SolidScissorTest);
            return (RunNestedSequence(dt), RunNestedSequence(dt));
        });

        Assert.Equal(Expected_FromScissorTest, first);
        Assert.Equal(Expected_FromScissorTest, second);
    }

    [GpuFact]
    public void PushRectangleClip_WithBounds_AllocatesNothing()
    {
        long allocated = GpuDeviceHost.Instance.Invoke(() =>
        {
            using DrawTransaction dt = CreateTransaction(RasterizerType.SolidScissorTest);
            for (int i = 0; i < WarmupPairs; i++)
            {
                dt.PushRectangleClip(Rectangles[i % Rectangles.Length], true).Dispose();
            }

            long before = AllocationWindow.Start();
            for (int i = 0; i < MeasuredPairs; i++)
            {
                dt.PushRectangleClip(Rectangles[i % Rectangles.Length], true).Dispose();
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        });

        Assert.Equal(0, allocated);
    }

    [GpuFact]
    public void PushRectangleClip_WithNull_AllocatesNothing()
    {
        long allocated = GpuDeviceHost.Instance.Invoke(() =>
        {
            using DrawTransaction dt = CreateTransaction(RasterizerType.SolidScissorTest);
            for (int i = 0; i < WarmupPairs; i++)
            {
                dt.PushRectangleClip(null, false).Dispose();
            }

            long before = AllocationWindow.Start();
            for (int i = 0; i < MeasuredPairs; i++)
            {
                dt.PushRectangleClip(null, false).Dispose();
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        });

        Assert.Equal(0, allocated);
    }

    [GpuFact]
    public void PushClipTemporary_WithARectangleDefinition_AllocatesNothing()
    {
        long allocated = GpuDeviceHost.Instance.Invoke(() =>
        {
            using DrawTransaction dt = CreateTransaction(RasterizerType.SolidScissorTest);
            ClipDefinition[] definitions = new ClipDefinition[Rectangles.Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                definitions[i] = ClipDefinition.Rectangle(Rectangles[i], true);
            }

            for (int i = 0; i < WarmupPairs; i++)
            {
                dt.PushClipTemporary(definitions[i % definitions.Length]).Dispose();
            }

            long before = AllocationWindow.Start();
            for (int i = 0; i < MeasuredPairs; i++)
            {
                dt.PushClipTemporary(definitions[i % definitions.Length]).Dispose();
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        });

        Assert.Equal(0, allocated);
    }

    [GpuFact]
    public void PushClipTemporary_WithTheNoneStrategy_AllocatesNothing()
    {
        long allocated = GpuDeviceHost.Instance.Invoke(() =>
        {
            using DrawTransaction dt = CreateTransaction(RasterizerType.SolidScissorTest);
            ClipDefinition none = ClipDefinition.None();
            for (int i = 0; i < WarmupPairs; i++)
            {
                dt.PushClipTemporary(none).Dispose();
            }

            long before = AllocationWindow.Start();
            for (int i = 0; i < MeasuredPairs; i++)
            {
                dt.PushClipTemporary(none).Dispose();
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        });

        Assert.Equal(0, allocated);
    }

    [GpuFact]
    public void ANestedSequenceOfClips_AllocatesNothing()
    {
        long allocated = GpuDeviceHost.Instance.Invoke(() =>
        {
            using DrawTransaction dt = CreateTransaction(RasterizerType.SolidScissorTest);
            for (int i = 0; i < WarmupPairs; i++)
            {
                PushAndPopFourLevels(dt);
            }

            long before = AllocationWindow.Start();
            for (int i = 0; i < MeasuredPairs; i++)
            {
                PushAndPopFourLevels(dt);
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        });

        Assert.Equal(0, allocated);
    }

    private static void PushAndPopFourLevels(DrawTransaction dt)
    {
        ClipHandle level1 = dt.PushRectangleClip(Rectangles[0], true);
        ClipHandle level2 = dt.PushRectangleClip(Rectangles[1], true);
        ClipHandle level3 = dt.PushRectangleClip(null, false);
        ClipHandle level4 = dt.PushRectangleClip(Rectangles[3], true);
        level4.Dispose();
        level3.Dispose();
        level2.Dispose();
        level1.Dispose();
    }

    [GpuFact]
    public void ARectangleScope_ReportsTheResolutionOfTheRectangleDefinition_AndIsCounted()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using DrawTransaction dt = CreateTransaction(RasterizerType.SolidScissorTest);
            Rectangle bounds = new(12, 14, 50, 60);

            using ClipHandle scope = dt.PushRectangleClip(bounds, true);
            Assert.Equal(dt.ResolveClip(ClipDefinition.Rectangle(bounds, true)), scope.Resolution);
            Assert.Equal(ClipStrategy.Scissor, scope.Resolution.Strategy);
            Assert.Equal(1, dt.ClipDiagnostics.ScissorClipCount);

            using ClipHandle definitionScope = dt.PushClipTemporary(ClipDefinition.Rectangle(new Rectangle(1, 2, 3, 4), false));
            Assert.Equal(new Rectangle(1, 2, 3, 4), definitionScope.Resolution.Effective.Shape.Bounds);
            Assert.False(definitionScope.Resolution.Effective.IntersectWithCurrentClip);
            Assert.Equal(2, dt.ClipDiagnostics.ScissorClipCount);

            using ClipHandle noClip = dt.PushRectangleClip(null, false);
            Assert.Equal(ClipStrategy.None, noClip.Resolution.Strategy);
            Assert.Equal(ClipKind.None, noClip.Resolution.Requested.Kind);
            Assert.Equal(2, dt.ClipDiagnostics.ScissorClipCount);
        });
    }

    [GpuFact]
    public void ClipsDisposedOutOfOrder_KeepTheirOwnResolution_AndRestoreWhatTheySaw()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using DrawTransaction dt = CreateTransaction(RasterizerType.SolidScissorTest);

            ClipHandle a = dt.PushRectangleClip(Rectangles[0], true);
            ClipHandle b = dt.PushRectangleClip(Rectangles[1], true);
            a.Dispose();
            ClipHandle c = dt.PushRectangleClip(Rectangles[2], true);

            Assert.Equal(Rectangles[1], b.Resolution.Effective.Shape.Bounds);
            Assert.Equal(Rectangles[2], c.Resolution.Effective.Shape.Bounds);

            c.Dispose();
            b.Dispose();
            // Each scope restores what it saw when it was pushed, as before: b was pushed under a.
            Assert.Equal(Rectangles[0], GpuDeviceHost.Instance.GraphicsDevice.ScissorRectangle);
        });
    }

    /// <summary>A handle kept after its dispose releases nothing, even when the next clip uses its slot (ADR-0031). With the re-armed scope
    /// objects of ADR-0027 the kept reference was the next clip, and its dispose released it.</summary>
    [GpuFact]
    public void AHandleKeptAfterItsDispose_DoesNotReleaseTheNextClip()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using DrawTransaction dt = CreateTransaction(RasterizerType.SolidScissorTest);
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            Rectangle start = device.ScissorRectangle;

            ClipHandle stale = dt.PushRectangleClip(Rectangles[0], true);
            stale.Dispose();
            Assert.Equal(start, device.ScissorRectangle);

            ClipHandle next = dt.PushRectangleClip(Rectangles[1], false);
            Assert.Equal(Rectangles[1], device.ScissorRectangle);

            stale.Dispose();
            Assert.Equal(Rectangles[1], device.ScissorRectangle);
            Assert.True(next.IsActive);
            Assert.False(stale.IsActive);
            Assert.Equal(Rectangles[1], next.Resolution.Effective.Shape.Bounds);

            next.Dispose();
            next.Dispose();
            Assert.Equal(start, device.ScissorRectangle);
        });
    }

    // Frozen from the transaction as it was before ClipScope became reusable.
    private static readonly List<string> Expected_FromScissorTest = new()
    {
        "start: scissor=0,0,128,128 settings=SolidScissorTest deviceScissorTest=True",
        "push1 rect: scissor=10,10,100,100 settings=SolidScissorTest deviceScissorTest=True",
        "push2 rect intersect: scissor=50,50,60,60 settings=SolidScissorTest deviceScissorTest=True",
        "push3 no clip: scissor=0,0,128,128 settings=Solid deviceScissorTest=False",
        "push4 rect over no clip: scissor=20,20,30,30 settings=SolidScissorTest deviceScissorTest=True",
        "dispose4: scissor=0,0,128,128 settings=Solid deviceScissorTest=False",
        "dispose3: scissor=50,50,60,60 settings=SolidScissorTest deviceScissorTest=True",
        "dispose2: scissor=10,10,100,100 settings=SolidScissorTest deviceScissorTest=True",
        "dispose1: scissor=0,0,128,128 settings=SolidScissorTest deviceScissorTest=True",
    };

    private static readonly List<string> Expected_FromNoScissorTest = new()
    {
        "start: scissor=0,0,128,128 settings=Solid deviceScissorTest=False",
        "push1 none definition: scissor=0,0,128,128 settings=Solid deviceScissorTest=False",
        "push2 rect: scissor=40,40,80,80 settings=SolidScissorTest deviceScissorTest=True",
        "push3 rect intersect: scissor=40,40,10,80 settings=SolidScissorTest deviceScissorTest=True",
        "push4 rect replace: scissor=5,5,20,20 settings=SolidScissorTest deviceScissorTest=True",
        "dispose4: scissor=40,40,10,80 settings=SolidScissorTest deviceScissorTest=True",
        "dispose3: scissor=40,40,80,80 settings=SolidScissorTest deviceScissorTest=True",
        "dispose2: scissor=0,0,128,128 settings=Solid deviceScissorTest=False",
        "dispose1: scissor=0,0,128,128 settings=Solid deviceScissorTest=False",
    };
}
