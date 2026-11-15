using System;
using MGUI.Shared.Rendering.Clipping;
using Microsoft.Xna.Framework;
using Xunit;

namespace MGUI.Tests.Integration;

/// <summary>The lifetime rules of <see cref="ClipScope"/> (ADR-0027): a scope made with the original constructor behaves as it always did, and a scope can be re-armed once it was disposed.</summary>
public class ClipScopeTests
{
    private static ClipResolveResult NoneResolution()
    {
        ClipDefinition none = ClipDefinition.None();
        return new ClipResolveResult(none, none, ClipStrategy.None, false);
    }

    [Fact]
    public void AScopeMadeWithTheOriginalConstructor_RunsItsActionOnceAndKeepsItsResolution()
    {
        int disposed = 0;
        ClipResolveResult resolution = NoneResolution();
        ClipScope scope = new(resolution, () => disposed++);

        Assert.Equal(resolution, scope.Resolution);

        scope.Dispose();
        scope.Dispose();

        Assert.Equal(1, disposed);
        Assert.Equal(resolution, scope.Resolution);
    }

    [Fact]
    public void TheOriginalConstructor_RefusesANullAction()
    {
        Assert.Throws<ArgumentNullException>(() => new ClipScope(NoneResolution(), null));
        Assert.Throws<ArgumentNullException>(() => new ClipScope(null));
    }

    [Fact]
    public void ASecondDispose_WithoutARearm_DoesNothing()
    {
        int disposed = 0;
        ClipScope scope = new(NoneResolution(), () => disposed++);

        Assert.Throws<InvalidOperationException>(() => scope.Reset(NoneResolution()));

        scope.Dispose();
        scope.Dispose();
        scope.Dispose();

        Assert.Equal(1, disposed);
    }

    [Fact]
    public void AScopeRearmedAfterItsDispose_RunsItsActionAgainOnce_WithTheNewResolution()
    {
        int disposed = 0;
        ClipScope scope = new(NoneResolution(), () => disposed++);
        scope.Dispose();
        Assert.Equal(1, disposed);

        ClipResolveResult second = NoneResolution() with { UsedFallback = true };
        scope.Reset(second);
        Assert.Equal(second, scope.Resolution);

        scope.Dispose();
        scope.Dispose();

        Assert.Equal(2, disposed);
    }

    [Fact]
    public void AScopeThatIsNotArmed_RunsNothingWhenDisposed_UntilItIsArmed()
    {
        int disposed = 0;
        ClipScope scope = new(() => disposed++);

        scope.Dispose();
        Assert.Equal(0, disposed);

        scope.Reset(NoneResolution());
        scope.Dispose();
        scope.Dispose();

        Assert.Equal(1, disposed);
    }

    [Fact]
    public void ResetRectangle_BuildsTheResolutionOfARectangleDefinition_WhenItIsRead()
    {
        int disposed = 0;
        ClipScope scope = new(() => disposed++);
        Rectangle bounds = new(3, 4, 50, 60);

        scope.ResetRectangle(bounds, true);

        ClipDefinition expected = ClipDefinition.Rectangle(bounds, true);
        Assert.Equal(new ClipResolveResult(expected, expected, ClipStrategy.Scissor, false),
            ClipStrategyResolver.Resolve(expected, ClipBackendCapabilities.Default));
        Assert.Equal(ClipStrategyResolver.Resolve(expected, ClipBackendCapabilities.Default), scope.Resolution);
        Assert.Same(scope.Resolution.Requested, scope.Resolution.Effective);

        scope.Dispose();
        Assert.Equal(1, disposed);

        scope.ResetRectangle(new Rectangle(1, 1, 2, 2), false);
        Assert.Equal(new Rectangle(1, 1, 2, 2), scope.Resolution.Effective.Shape.Bounds);
        Assert.False(scope.Resolution.Effective.IntersectWithCurrentClip);
    }

    [Fact]
    public void ResetRectangle_WhileTheScopeIsArmed_Throws()
    {
        ClipScope scope = new(NoneResolution(), () => { });

        Assert.Throws<InvalidOperationException>(() => scope.ResetRectangle(new Rectangle(0, 0, 1, 1), true));
    }
}
