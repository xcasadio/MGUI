using MGUI.Shared.Rendering.Clipping;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Architecture;

public class ClipPipelineStrategyTests
{
    [Fact]
    public void RenderContext_KeepsLegacyRectangleClipShim()
    {
        var method = typeof(MGUI.Shared.Rendering.IUIRenderContext).GetMethod("SetClipTargetTemporary");

        Assert.NotNull(method);
        Assert.Equal(typeof(IDisposable), method!.ReturnType);
    }

    [Fact]
    public void RectangleClip_ResolvesToScissor()
    {
        ClipDefinition definition = ClipDefinition.Rectangle(new Rectangle(10, 20, 100, 40), true, debugName: "Rect");

        ClipResolveResult result = ClipStrategyResolver.Resolve(definition, ClipBackendCapabilities.Default);

        Assert.Equal(ClipStrategy.Scissor, result.Strategy);
        Assert.False(result.UsedFallback);
        Assert.Equal(ClipKind.Rectangle, result.Effective.Kind);
    }

    [Fact]
    public void RoundedRectangle_ResolvesToStencil()
    {
        ClipDefinition definition = ClipDefinition.RoundedRectangle(new Rectangle(0, 0, 120, 60), new ClipCornerRadius(12),
            geometry: new ClipGeometry(new[] { Vector2.Zero, new Vector2(120, 0), new Vector2(0, 60) }, new[] { 0, 1, 2 }),
            debugName: "Rounded");

        ClipResolveResult result = ClipStrategyResolver.Resolve(definition, ClipBackendCapabilities.Default);

        Assert.Equal(ClipStrategy.Stencil, result.Strategy);
        Assert.False(result.UsedFallback);
    }

    [Fact]
    public void ArbitraryGeometry_ResolvesToStencil_WhenAvailable()
    {
        ClipDefinition definition = ClipDefinition.ArbitraryGeometry(new Rectangle(0, 0, 80, 80),
            new ClipGeometry(new[] { Vector2.Zero, new Vector2(80, 0), new Vector2(40, 80) }, new[] { 0, 1, 2 }),
            debugName: "Geometry");

        ClipResolveResult result = ClipStrategyResolver.Resolve(definition, ClipBackendCapabilities.Default);

        Assert.Equal(ClipStrategy.Stencil, result.Strategy);
        Assert.False(result.UsedFallback);
    }

    [Fact]
    public void ArbitraryGeometry_FallsBackToMask_WhenStencilIsUnavailable()
    {
        ClipDefinition definition = ClipDefinition.ArbitraryGeometry(new Rectangle(0, 0, 80, 80),
            new ClipGeometry(new[] { Vector2.Zero, new Vector2(80, 0), new Vector2(40, 80) }, new[] { 0, 1, 2 }),
            debugName: "GeometryMaskFallback");
        ClipBackendCapabilities capabilities = new(true, false, true);

        ClipResolveResult result = ClipStrategyResolver.Resolve(definition, capabilities);

        Assert.Equal(ClipStrategy.Mask, result.Strategy);
        Assert.True(result.UsedFallback);
    }

    [Fact]
    public void RoundedRectangle_FallsBackToScissorWhenAllowed()
    {
        ClipDefinition definition = ClipDefinition.RoundedRectangle(new Rectangle(0, 0, 120, 60), new ClipCornerRadius(12),
            geometry: new ClipGeometry(new[] { Vector2.Zero, new Vector2(120, 0), new Vector2(0, 60) }, new[] { 0, 1, 2 }),
            allowRectangleFallback: true, debugName: "RoundedFallback");
        ClipBackendCapabilities capabilities = new(true, false, false);

        ClipResolveResult result = ClipStrategyResolver.Resolve(definition, capabilities);

        Assert.Equal(ClipStrategy.Scissor, result.Strategy);
        Assert.True(result.UsedFallback);
        Assert.Equal(ClipKind.Rectangle, result.Effective.Kind);
    }

    [Fact]
    public void DiagnosticsSnapshot_FormatsAllCounters()
    {
        ClipDiagnosticsSnapshot diagnostics = new(2, 3, 1, 4, 5, 2);

        string text = diagnostics.ToDebugString();

        Assert.Contains("s:2", text);
        Assert.Contains("st:3", text);
        Assert.Contains("m:1", text);
        Assert.Contains("stencilDepth=4", text);
        Assert.Contains("rent:5", text);
        Assert.Contains("reuse:2", text);
    }
}