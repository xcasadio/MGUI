using System;
using System.Collections.Generic;
using MGUI.Shared.Rendering.Clipping;
using Microsoft.Xna.Framework;
using Xunit;

namespace MGUI.Tests.Integration;

/// <summary>The lifetime rules of a clip handle and of the slots a backend keeps for its clips (ADR-0031): a clip is restored once, by the first
/// dispose of any copy of its handle; a handle kept after that releases nothing, even when its slot holds another clip; the resolution of a
/// rectangle clip is built when it is read.</summary>
public class ClipSlotsTests
{
    private sealed class RecordingRestorer : IClipSlotRestorer
    {
        public List<ClipSlotState> Restored { get; } = new();
        public bool Records { get; set; } = true;
        public int RestoreCount { get; private set; }

        public void RestoreClip(in ClipSlotState State)
        {
            RestoreCount++;
            if (Records)
            {
                Restored.Add(State);
            }
        }
    }

    private static ClipResolveResult NoneResolution()
    {
        ClipDefinition none = ClipDefinition.None();
        return new ClipResolveResult(none, none, ClipStrategy.None, false);
    }

    [Fact]
    public void AClip_IsRestoredOnce_WithTheStateItWasPushedWith()
    {
        RecordingRestorer restorer = new();
        ClipSlots slots = new(restorer);
        ClipResolveResult resolution = NoneResolution() with { UsedFallback = true };

        ClipHandle handle = slots.Push(ClipSlotState.Scissor(resolution, new Rectangle(1, 2, 3, 4), true, true));

        Assert.False(handle.IsDefault);
        Assert.True(handle.IsActive);
        Assert.Equal(resolution, handle.Resolution);
        Assert.Equal(1, slots.InForceCount);

        ClipHandle copy = handle;
        handle.Dispose();
        handle.Dispose();
        copy.Dispose();

        Assert.Equal(1, restorer.RestoreCount);
        Assert.Equal(0, slots.InForceCount);
        ClipSlotState restored = Assert.Single(restorer.Restored);
        Assert.Equal(ClipStrategy.Scissor, restored.Strategy);
        Assert.Equal(new Rectangle(1, 2, 3, 4), restored.PreviousScissorBounds);
        Assert.True(restored.PreviousScissorState);
        Assert.True(restored.ScissorChanged);
        Assert.False(handle.IsActive);
        Assert.False(copy.TryGetResolution(out _));
        Assert.Throws<InvalidOperationException>(() => copy.Resolution);
    }

    [Fact]
    public void AHandleKeptAfterItsDispose_DoesNotReleaseTheClipThatUsesItsSlotNext()
    {
        RecordingRestorer restorer = new();
        ClipSlots slots = new(restorer);

        ClipHandle stale = slots.Push(ClipSlotState.ScissorRectangle(new Rectangle(0, 0, 10, 10), true, default, false, true));
        stale.Dispose();
        ClipHandle next = slots.Push(ClipSlotState.ScissorRectangle(new Rectangle(5, 5, 20, 20), true, default, false, true));

        stale.Dispose();

        Assert.Equal(1, restorer.RestoreCount);
        Assert.Equal(1, slots.InForceCount);
        Assert.True(next.IsActive);
        Assert.False(stale.IsActive);
        Assert.Equal(new Rectangle(5, 5, 20, 20), next.Resolution.Effective.Shape.Bounds);

        next.Dispose();
        Assert.Equal(2, restorer.RestoreCount);
    }

    [Fact]
    public void Clips_AreReleasedInAnyOrder_EachWithItsOwnState()
    {
        RecordingRestorer restorer = new();
        ClipSlots slots = new(restorer);

        ClipHandle a = slots.Push(ClipSlotState.Scissor(NoneResolution(), new Rectangle(1, 0, 0, 0), false, true));
        ClipHandle b = slots.Push(ClipSlotState.Scissor(NoneResolution(), new Rectangle(2, 0, 0, 0), false, true));
        a.Dispose();
        ClipHandle c = slots.Push(ClipSlotState.Scissor(NoneResolution(), new Rectangle(3, 0, 0, 0), false, true));
        c.Dispose();
        b.Dispose();

        Assert.Equal(new[] { 1, 3, 2 }, restorer.Restored.ConvertAll(state => state.PreviousScissorBounds.X));
    }

    [Fact]
    public void ARectangleClip_BuildsTheResolutionOfARectangleDefinition_WhenItIsRead()
    {
        ClipSlots slots = new(new RecordingRestorer());
        Rectangle bounds = new(3, 4, 50, 60);

        ClipHandle handle = slots.Push(ClipSlotState.ScissorRectangle(bounds, true, default, false, true));

        ClipDefinition expected = ClipDefinition.Rectangle(bounds, true);
        Assert.Equal(new ClipResolveResult(expected, expected, ClipStrategy.Scissor, false),
            ClipStrategyResolver.Resolve(expected, ClipBackendCapabilities.Default));
        Assert.Equal(ClipStrategyResolver.Resolve(expected, ClipBackendCapabilities.Default), handle.Resolution);
        Assert.Same(handle.Resolution.Requested, handle.Resolution.Effective);
        //  Built once: a second read gives the definition of the first.
        Assert.Same(handle.Resolution.Requested, handle.Resolution.Requested);

        handle.Dispose();
        ClipHandle next = slots.Push(ClipSlotState.ScissorRectangle(new Rectangle(1, 1, 2, 2), false, default, false, true));
        Assert.Equal(new Rectangle(1, 1, 2, 2), next.Resolution.Effective.Shape.Bounds);
        Assert.False(next.Resolution.Effective.IntersectWithCurrentClip);
    }

    [Fact]
    public void AClipWithACallback_RunsItOnce()
    {
        int restored = 0;
        CallbackClipHandleOwner owner = new();
        ClipResolveResult resolution = NoneResolution();

        ClipHandle handle = owner.Push(resolution, () => restored++);
        Assert.Equal(resolution, handle.Resolution);
        Assert.Equal(1, owner.InForceCount);

        handle.Dispose();
        handle.Dispose();

        Assert.Equal(1, restored);
        Assert.Equal(0, owner.InForceCount);
        Assert.Throws<ArgumentNullException>(() => owner.Push(resolution, null));
    }

    [Fact]
    public void ReleaseAll_ForgetsTheClipsInForce_WithoutRestoringThem()
    {
        RecordingRestorer restorer = new();
        ClipSlots slots = new(restorer);
        ClipHandle left = slots.Push(ClipSlotState.None(NoneResolution()));

        slots.ReleaseAll();
        ClipHandle next = slots.Push(ClipSlotState.None(NoneResolution()));
        left.Dispose();

        Assert.Equal(0, restorer.RestoreCount);
        Assert.False(left.IsActive);
        Assert.True(next.IsActive);
        Assert.Equal(1, slots.InForceCount);
    }

    [Fact]
    public void ClipSlots_NeedARestorer()
    {
        Assert.Throws<ArgumentNullException>(() => new ClipSlots(null));
    }

    [Fact]
    public void PushingAndReleasingRectangleClips_AllocatesNothing_OnceTheSlotsExist()
    {
        RecordingRestorer restorer = new() { Records = false };
        ClipSlots slots = new(restorer);
        ClipResolveResult none = NoneResolution();
        for (int i = 0; i < 4; i++)
        {
            PushAndRelease(slots, none);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < 1000; i++)
        {
            PushAndRelease(slots, none);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(3012, restorer.RestoreCount);
    }

    private static void PushAndRelease(ClipSlots slots, ClipResolveResult none)
    {
        using (slots.Push(ClipSlotState.ScissorRectangle(new Rectangle(0, 0, 10, 10), true, default, true, true)))
        using (slots.Push(ClipSlotState.None(none)))
        using (ClipHandle inner = slots.Push(ClipSlotState.ScissorRectangle(new Rectangle(2, 2, 4, 4), true, default, true, false)))
        {
            Assert.True(inner.IsActive);
        }
    }
}
