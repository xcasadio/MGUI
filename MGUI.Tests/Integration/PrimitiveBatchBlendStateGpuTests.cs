using MGUI.Backend.MonoGame;
using MGUI.Core.UI;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Integration;

/// <summary>Real-GPU regression coverage for the MonoGame.Extended 3.8.0 -&gt; 6.1.1 bump (commit 61fc475): in 6.1.1,
/// <c>PrimitiveBatch.Begin(ref Matrix, ref Matrix, BlendState)</c> assigns <c>_device.BlendState = blendState ??
/// BlendState.NonPremultiplied</c> itself, which used to silently discard the blend state <see cref="DrawTransaction"/>
/// had already resolved and applied via <c>ApplyPrimitiveDeviceStates()</c>. The fix
/// (<c>MGUI.MonoGame.Integration/Helpers/RenderUtils.cs</c>, <c>MGUI.MonoGame.Integration/Rendering/DrawTransaction.cs</c>)
/// forwards the resolved <see cref="BlendState"/> through explicitly. These tests exercise the real
/// <see cref="DrawTransaction"/> -&gt; <c>PrimitiveBatch</c> -&gt; GPU pipeline (no mocks) and fail against the
/// pre-fix code (forwarding a null blend state paints the clip centre white and blends the half-white fill to ~63).<para/>
/// All GPU work happens through <see cref="GpuDeviceHost"/> on its single dedicated thread; see that class for why.</summary>
[Collection(GpuDeviceCollection.Name)]
public class PrimitiveBatchBlendStateGpuTests
{
    private static MainRenderer CreateRenderer(HeadlessGame game)
        => MonoGameBackendBootstrap.Create(new GameRenderHost<HeadlessGame>(game)).Renderer;

    /// <summary>The regression as originally reported: a RoundedRectangle clip resolves to the Stencil strategy
    /// (<see cref="ClipStrategyResolver"/>: <see cref="ClipKind.RoundedRectangle"/> -&gt; <see cref="ClipStrategy.Stencil"/>),
    /// whose clip geometry is drawn through the PRIMITIVES path with <see cref="BlendType.ColorWriteDisable"/>. With the
    /// override, that geometry painted visible white instead of only touching the stencil buffer, and the matching
    /// restore-decrement pass on scope disposal painted white over the clipped content - "Rounded Shapes" in
    /// MGUI.Samples rendered as a solid white rectangle with no content.</summary>
    [GpuFact]
    public void RoundedStencilClip_PrimitivesFillStaysInsideRoundedBounds_AndClipGeometryNeverPaintsVisibleWhite()
    {
        const int TargetSize = 80;
        Rectangle clipBounds = new(10, 10, 60, 60);
        const int CornerRadius = 20;
        Color background = Color.CornflowerBlue;
        Color fill = Color.Red;

        Color[] pixels = GpuDeviceHost.Instance.Invoke(()
            => RunRoundedClipScene(GpuDeviceHost.Instance.Game, GpuDeviceHost.Instance.GraphicsDevice, TargetSize, clipBounds, CornerRadius, background, fill, out _));

        // Center of the clip: well inside the rounded region on every side -> painted by the primitives fill.
        Assert.Equal(fill, pixels[40 * TargetSize + 40]);

        // 1px inside the clip bounds' top-left corner: with a corner radius this large, still outside the rounded
        // arc (distance from the arc center exceeds the radius) -> must stay background, not fill and not stencil-clip white.
        Assert.Equal(background, pixels[11 * TargetSize + 11]);

        // Outside the clip bounds entirely -> background.
        Assert.Equal(background, pixels[5 * TargetSize + 5]);

        // The regression painted the whole target solid white: assert no pixel is pure white anywhere.
        foreach (Color pixel in pixels)
        {
            Assert.NotEqual(Color.White, pixel);
        }
    }

    /// <summary>Runs entirely inside <see cref="GpuDeviceHost"/>'s dedicated thread. Broken out so the "which strategy did
    /// this clip actually resolve to" and "how many stencil clips were pushed" assertions can run against the live
    /// <see cref="DrawTransaction"/>/<see cref="ClipManager"/> state before it's torn down.</summary>
    private static Color[] RunRoundedClipScene(HeadlessGame game, GraphicsDevice device, int targetSize, Rectangle clipBounds,
        int cornerRadius, Color background, Color fill, out ClipDiagnosticsSnapshot diagnostics)
    {
        using RenderTarget2D target = new(device, targetSize, targetSize, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        device.SetRenderTarget(target);
        device.Clear(ClearOptions.Target | ClearOptions.Stencil, background, 1.0f, 0);

        MainRenderer renderer = CreateRenderer(game);
        using DrawTransaction dt = (DrawTransaction)renderer.CreateDrawTransaction(DrawSettings.Default, DeferBegin: true, DrawContext.Primitives);

        MGBoxShape clipShape = new(clipBounds, new Thickness(0), new MGCornerRadius(cornerRadius));
        MGBoxGeometry clipGeometry = MGBoxGeometryBuilder.Build(clipShape, 24);
        ClipDefinition clipDefinition = ClipDefinition.RoundedRectangle(clipBounds, new ClipCornerRadius(cornerRadius),
            geometry: clipGeometry.ToClipGeometry(), intersectWithCurrentClip: false, debugName: "GpuStencilClipRegressionTest");

        ClipScope scope = dt.PushClipTemporary(clipDefinition);
        Assert.Equal(ClipStrategy.Stencil, scope.Resolution.Strategy);

        dt.FillRectangle(Vector2.Zero, new RectangleF(0, 0, targetSize, targetSize), fill, DrawContext.Primitives);

        scope.Dispose();

        diagnostics = dt.ClipDiagnostics;
        Assert.Equal(1, diagnostics.StencilClipCount);

        dt.Dispose();
        device.SetRenderTarget(null);

        Color[] pixels = new Color[targetSize * targetSize];
        target.GetData(pixels);
        return pixels;
    }

    /// <summary>General coverage beyond the rounded-clip regression: the default <see cref="BlendType.AlphaBlend"/> must
    /// actually reach the GPU for a PRIMITIVES fill. AlphaBlend expects premultiplied source colors
    /// (<see cref="Color.White"/> * 0.5f premultiplies every channel, including alpha, to ~127); blended over an opaque
    /// black background that yields ~127-128 per RGB channel. Pre-fix, <c>PrimitiveBatch.Begin</c> silently forced
    /// <see cref="BlendState.NonPremultiplied"/> instead, which re-multiplies RGB by alpha and yields ~63.</summary>
    [GpuFact]
    public void DefaultAlphaBlend_ReachesGpu_ForPrimitivesFill()
    {
        const int TargetSize = 32;
        Color premultipliedHalfWhite = Color.White * 0.5f;

        Color[] pixels = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;

            using RenderTarget2D target = new(device, TargetSize, TargetSize, false, SurfaceFormat.Color, DepthFormat.None);
            device.SetRenderTarget(target);
            device.Clear(Color.Black);

            MainRenderer renderer = CreateRenderer(GpuDeviceHost.Instance.Game);
            using DrawTransaction dt = (DrawTransaction)renderer.CreateDrawTransaction(DrawSettings.Default, DeferBegin: true, DrawContext.Primitives);

            dt.FillRectangle(Vector2.Zero, new RectangleF(0, 0, TargetSize, TargetSize), premultipliedHalfWhite, DrawContext.Primitives);
            dt.Dispose();

            device.SetRenderTarget(null);
            Color[] result = new Color[TargetSize * TargetSize];
            target.GetData(result);
            return result;
        });

        Color center = pixels[(TargetSize / 2) * TargetSize + TargetSize / 2];
        Assert.InRange(center.R, 125, 130);
        Assert.InRange(center.G, 125, 130);
        Assert.InRange(center.B, 125, 130);
    }
}
