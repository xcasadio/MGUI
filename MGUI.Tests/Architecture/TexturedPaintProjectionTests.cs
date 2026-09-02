using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>Textured paints on rounded geometry: <see cref="MGTextureFillBrush"/> and <see cref="MGTexturedBorderBrush"/> project their textures onto
/// <see cref="MGBoxGeometry"/> through <see cref="IUIDrawContext.DrawTexturedTriangleList"/>, while the rectangle fast path and the documented
/// fallbacks keep the legacy rectangle draw calls.</summary>
public class TexturedPaintProjectionTests
{
    private const float UvTolerance = 1e-6f;

    [Fact]
    public void IUIDrawContext_ExposesDrawTexturedTriangleList()
    {
        var method = typeof(IUIDrawContext).GetMethod(nameof(IUIDrawContext.DrawTexturedTriangleList),
            new[] { typeof(Vector2), typeof(IUIImageResource), typeof(IReadOnlyList<Vector2>), typeof(IReadOnlyList<Vector2>), typeof(IReadOnlyList<int>), typeof(Color) });

        Assert.NotNull(method);
    }

    #region MGTextureFillBrush

    [Fact]
    public void TextureFillBrush_RoundedShape_ProjectsOntoFillMesh()
    {
        Recorder recorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(80, 40, 0, 12);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        MGTextureFillBrush brush = new(new MGTextureData(recorder.Image(64, 64)), Stretch.Fill);

        brush.Draw(recorder.Args(), null!, shape, geometry);

        GraphTexturedTriangleListCall call = Assert.Single(recorder.Transaction.TexturedTriangleListCalls);
        Assert.Empty(recorder.Transaction.DrawTextureToCalls);
        Assert.Empty(recorder.Transaction.DrawTextureAtCalls);
        Assert.Equal(geometry.Vertices, call.Vertices);
        Assert.Equal(geometry.FillIndices, call.Indices);
        AssertUVsInUnitSquare(call);

        //  No overflow beyond the rounded silhouette: every emitted vertex, pulled 0.25px toward the centre (arc vertices sit exactly on the arc in float), is inside the shape.
        Vector2 center = shape.OuterBounds.Center.ToVector2();
        foreach (Vector2 vertex in call.Vertices)
        {
            Vector2 inward = vertex + Vector2.Normalize(center - vertex) * 0.25f;
            Assert.True(shape.Contains(inward), $"vertex {vertex} lies outside the rounded shape");
        }

        //  Stretch over the bounds: leftmost vertex -> u = 0, rightmost -> u = 1, topmost -> v = 0, bottommost -> v = 1.
        Assert.Equal(0f, call.TextureCoordinates[IndexOfMin(call.Vertices, v => v.X)].X, 3);
        Assert.Equal(1f, call.TextureCoordinates[IndexOfMax(call.Vertices, v => v.X)].X, 3);
        Assert.Equal(0f, call.TextureCoordinates[IndexOfMin(call.Vertices, v => v.Y)].Y, 3);
        Assert.Equal(1f, call.TextureCoordinates[IndexOfMax(call.Vertices, v => v.Y)].Y, 3);
    }

    [Fact]
    public void TextureFillBrush_RectangleFastPath_KeepsRectangleDrawCall()
    {
        Recorder rounded = Recorder.Create();
        Recorder legacy = Recorder.Create();
        MGTextureData texture = new(rounded.Image(64, 64));
        MGBoxShape shape = RoundedShape(80, 40, 0, 0);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        MGTextureFillBrush brush = new(texture, Stretch.Fill);

        Assert.True(geometry.UsesRectangleFastPath);
        brush.Draw(rounded.Args(), null!, shape, geometry);
        brush.Draw(legacy.Args(), null!, shape.OuterBounds);

        Assert.Empty(rounded.Transaction.TexturedTriangleListCalls);
        Assert.Equal(legacy.Transaction.DrawTextureToCalls, rounded.Transaction.DrawTextureToCalls);
        Assert.Single(rounded.Transaction.DrawTextureToCalls);
    }

    /// <summary>Atlas sub-rectangle tiled on a rounded shape (see Docs/drawing-architecture.md): a wrap sampler would repeat the whole atlas, so each tile of the
    /// rectangle path's own grid is clipped to the silhouette and drawn as its own textured quad (clamp sampler) instead of falling back to the
    /// rectangle draw calls.</summary>
    [Fact]
    public void TextureFillBrush_TileWithAtlasSourceRect_ClipsTileQuadsToSilhouette()
    {
        Recorder recorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(160, 80, 0, 12);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        GraphTestImageResource atlas = recorder.Image(64, 64);
        Rectangle subRect = new(0, 0, 32, 32);
        MGTextureFillBrush brush = new(new MGTextureData(atlas, subRect), Stretch.Fill, null, Tile: true);

        brush.Draw(recorder.Args(), null!, shape, geometry);

        List<GraphTexturedTriangleListCall> calls = recorder.Transaction.TexturedTriangleListCalls;
        Assert.NotEmpty(calls);
        Assert.Empty(recorder.Transaction.DrawTextureToCalls);
        Assert.Empty(recorder.Transaction.DrawTextureAtCalls);
        Assert.All(calls, call => Assert.True(ReferenceEquals(call.Texture, atlas)));
        //  No wrap sampler: the atlas path never leaves the transaction's default (clamp) sampler.
        Assert.All(calls, call => Assert.NotEqual(SamplerType.LinearWrap, call.SamplerType));

        //  Sub-rect UVs only: the whole atlas is 64x64 but the source is its top-left 32x32 quadrant, so every UV stays inside [0, 0.5].
        Vector2 uvTopLeft = new(subRect.Left / (float)atlas.Width, subRect.Top / (float)atlas.Height);
        Vector2 uvBottomRight = new(subRect.Right / (float)atlas.Width, subRect.Bottom / (float)atlas.Height);

        Vector2 center = shape.OuterBounds.Center.ToVector2();
        foreach (GraphTexturedTriangleListCall call in calls)
        {
            Assert.Equal(call.Vertices.Length, call.TextureCoordinates.Length);
            Assert.True(call.Indices.Length >= 3 && call.Indices.Length % 3 == 0);
            for (int i = 0; i < call.Vertices.Length; i++)
            {
                Vector2 vertex = call.Vertices[i];
                Vector2 inward = vertex + Vector2.Normalize(center - vertex) * 0.25f;
                Assert.True(shape.Contains(inward), $"vertex {vertex} lies outside the rounded shape");

                Vector2 uv = call.TextureCoordinates[i];
                Assert.InRange(uv.X, uvTopLeft.X - 1e-4f, uvBottomRight.X + 1e-4f);
                Assert.InRange(uv.Y, uvTopLeft.Y - 1e-4f, uvBottomRight.Y + 1e-4f);
            }
        }
    }

    /// <summary>Orientation pinning and partial-edge clamping for the atlas-tile path: the first (unclipped) tile's own top-left and bottom-right
    /// vertices map exactly to the sub-rect's top-left/bottom-right UVs, and a partial tile at the right edge samples a source rect clamped by
    /// the same ratio the rectangle path (<see cref="MGTextureFillBrush.Draw(ElementDrawArgs, MGElement, Rectangle)"/>) would clamp to.</summary>
    [Fact]
    public void TextureFillBrush_TileWithAtlasSourceRect_PinsOrientationAndClampsPartialEdgeTile()
    {
        Recorder recorder = Recorder.Create();
        //  Only the bottom corners are rounded (radius 8, starting at y=52): the top edge and the right edge above y=52 stay straight, so
        //  neither the first tile (top-left) nor the partial right-edge tile in the top row (y=0..32) is touched by the rounding at all -
        //  only the tile grid's own partial-edge source clamping is exercised here.
        MGBoxShape shape = new(new Rectangle(0, 0, 100, 60), new Thickness(0), new MGCornerRadius(0, 0, 8, 8));
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        Assert.False(geometry.UsesRectangleFastPath);
        GraphTestImageResource atlas = recorder.Image(64, 64);
        Rectangle subRect = new(0, 0, 32, 32);
        MGTextureFillBrush brush = new(new MGTextureData(atlas, subRect), Stretch.Fill, null, Tile: true);

        brush.Draw(recorder.Args(), null!, shape, geometry);

        List<GraphTexturedTriangleListCall> calls = recorder.Transaction.TexturedTriangleListCalls;
        Vector2 uvTopLeft = new(subRect.Left / (float)atlas.Width, subRect.Top / (float)atlas.Height);
        Vector2 uvBottomRight = new(subRect.Right / (float)atlas.Width, subRect.Bottom / (float)atlas.Height);

        //  First tile (0,0)-(32,32): its top-left corner is the shape's own sharp corner, its bottom-right corner is fully interior - neither is
        //  touched by clipping, so both map exactly like an unclamped stretch would.
        GraphTexturedTriangleListCall firstTileCall = calls.Single(call => call.Vertices.Contains(new Vector2(0, 0)));
        int topLeftIndex = Array.IndexOf(firstTileCall.Vertices, new Vector2(0, 0));
        Assert.Equal(uvTopLeft, firstTileCall.TextureCoordinates[topLeftIndex]);
        int bottomRightIndex = Array.IndexOf(firstTileCall.Vertices, new Vector2(32, 32));
        Assert.True(bottomRightIndex >= 0, "the first tile must keep its own unclipped bottom-right corner at (32,32)");
        Assert.Equal(uvBottomRight, firstTileCall.TextureCoordinates[bottomRightIndex]);

        //  Partial right-edge tile in the top row (x=96..100, y=0..32): drawW = 4 < tileW = 32, so its source rect is clamped to the same
        //  4px slice of the sub-rect the rectangle path would clamp to (Math.Min(drawW, subRect.Width)), not scaled or wrapped. Vertex (100,0)
        //  is reached by no other tile so it is unambiguous; vertex (96,0) is also the neighboring full tile's top-right corner (emitted
        //  earlier, in raster order), so LastIndexOf resolves to this tile's own copy of that shared edge vertex.
        GraphTexturedTriangleListCall partialTileCall = calls.Single(call => call.Vertices.Contains(new Vector2(100, 0)) && call.Vertices.Contains(new Vector2(96, 0)));
        float expectedPartialU = (subRect.Left + Math.Min(4, subRect.Width)) / (float)atlas.Width;
        int partialRightIndex = Array.IndexOf(partialTileCall.Vertices, new Vector2(100, 0));
        Assert.Equal(expectedPartialU, partialTileCall.TextureCoordinates[partialRightIndex].X, 4);
        int partialLeftIndex = Array.LastIndexOf(partialTileCall.Vertices, new Vector2(96, 0));
        Assert.Equal(uvTopLeft.X, partialTileCall.TextureCoordinates[partialLeftIndex].X, 4);
    }

    [Fact]
    public void TextureFillBrush_TileWithFullTexture_UsesWrapSamplerAndTileUVs()
    {
        Recorder recorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(160, 80, 0, 12);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        MGTextureFillBrush brush = new(new MGTextureData(recorder.Image(64, 64)), Stretch.Fill, null, Tile: true);

        brush.Draw(recorder.Args(), null!, shape, geometry);

        GraphTexturedTriangleListCall call = Assert.Single(recorder.Transaction.TexturedTriangleListCalls);
        Assert.Empty(recorder.Transaction.DrawTextureToCalls);
        Assert.Equal(SamplerType.LinearWrap, call.SamplerType);
        //  The wrap sampler is scoped to the call: the transaction's settings are restored afterwards.
        Assert.Equal(DrawSettings.Default.SamplerType, recorder.Transaction.CurrentSettings.SamplerType);
        Assert.NotEqual(SamplerType.LinearWrap, recorder.Transaction.CurrentSettings.SamplerType);
        Assert.True(call.TextureCoordinates.Max(uv => uv.X) > 1f, "a 64px tile over a 160px wide shape must wrap past u = 1");
        Assert.Equal(2.5f, call.TextureCoordinates[IndexOfMax(call.Vertices, v => v.X)].X, 3);
    }

    [Theory]
    [InlineData(Stretch.Uniform)]
    [InlineData(Stretch.None)]
    public void TextureFillBrush_PartialDestination_PushesScreenSpaceClipAroundDestination(Stretch stretch)
    {
        //  Layout and Screen spaces must actually differ, otherwise a clip expressed in the wrong space would go unnoticed: scale the window.
        Harness harness = Harness.Create(scale: 1.5f);
        MGBorder element = new(harness.Window);
        harness.Show(element);
        Recorder recorder = Recorder.Create(harness.Runtime);
        Rectangle bounds = new(10, 10, 160, 40);
        MGBoxShape shape = new(bounds, new Thickness(0), new MGCornerRadius(12));
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        MGTextureFillBrush brush = new(new MGTextureData(recorder.Image(64, 64)), stretch);
        Rectangle destination = stretch == Stretch.Uniform
            ? new Rectangle(bounds.Center.X - 20, bounds.Top, 40, 40)
            : new Rectangle(bounds.Center.X - 32, bounds.Center.Y - 32, 64, 64);
        Rectangle layoutClip = Rectangle.Intersect(destination, bounds);
        Rectangle expectedScreenClip = element.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, layoutClip);

        brush.Draw(recorder.Args(Point.Zero), element, shape, geometry);

        GraphTexturedTriangleListCall call = Assert.Single(recorder.Transaction.TexturedTriangleListCalls);
        Assert.NotEqual(layoutClip, expectedScreenClip);
        Assert.Equal(expectedScreenClip, call.ClipBounds);
        Assert.Null(recorder.Transaction.CurrentClipBounds);
    }

    #endregion

    #region MGTexturedBorderBrush

    [Fact]
    public void TexturedBorderBrush_RoundedShape_MapsEdgesAndCorners()
    {
        Recorder recorder = Recorder.Create();
        GraphTestImageResource edgeImage = recorder.Image(32, 8);
        GraphTestImageResource cornerImage = recorder.Image(8, 8);
        //  Thickness 8, radius 12: the 8x8 corner blocks overlap the arcs substantially and the inner radius (4) keeps a ring mesh.
        MGBoxShape shape = RoundedShape(80, 40, 8, 12);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        MGTexturedBorderBrush brush = new(edgeImage, cornerImage);

        Assert.True(geometry.HasBorderRingMesh);
        brush.Draw(recorder.Args(), null!, shape, geometry);

        List<GraphTexturedTriangleListCall> calls = recorder.Transaction.TexturedTriangleListCalls;
        Assert.Empty(recorder.Transaction.DrawTextureToCalls);
        Assert.Empty(recorder.Transaction.DrawTextureAtCalls);
        Assert.Equal(8, calls.Count);
        Assert.All(calls, call => Assert.True(ReferenceEquals(call.Texture, edgeImage) || ReferenceEquals(call.Texture, cornerImage)));
        Assert.Equal(4, calls.Count(call => ReferenceEquals(call.Texture, edgeImage)));
        Assert.Equal(4, calls.Count(call => ReferenceEquals(call.Texture, cornerImage)));
        Assert.All(calls, AssertUVsInUnitSquare);
        Assert.All(calls, call => Assert.Equal(Color.White, call.ColorMask));

        //  The layout rectangles partition the ring: the clipped pieces cover exactly the ring area, no hole and no overlap (float rounding aside).
        float ringArea = MeshArea(geometry.Vertices.ToArray(), geometry.BorderRingIndices.ToArray());
        Assert.InRange(calls.Sum(call => MeshArea(call.Vertices, call.Indices)), ringArea - 0.05f, ringArea + 0.05f);

        //  Top edge, rotation 0: the texture runs from u = 0 at the left corner block to u = 1 at the right one, and never leaves its rectangle.
        GraphTexturedTriangleListCall topEdge = calls.Where(call => ReferenceEquals(call.Texture, edgeImage)).MinBy(call => call.Vertices.Average(v => v.Y));
        Assert.Equal(0f, topEdge.TextureCoordinates[IndexOfMin(topEdge.Vertices, v => v.X)].X, 2);
        Assert.Equal(1f, topEdge.TextureCoordinates[IndexOfMax(topEdge.Vertices, v => v.X)].X, 2);
        Assert.All(topEdge.Vertices, v => Assert.InRange(v.Y, shape.OuterBounds.Top - 0.01f, shape.OuterBounds.Top + 8 + 0.01f));
        Assert.All(topEdge.Vertices, v => Assert.InRange(v.X, shape.OuterBounds.Left + 8 - 0.01f, shape.OuterBounds.Right - 8 + 0.01f));
    }

    [Fact]
    public void TexturedBorderBrush_RoundedShape_KeepsRectangleLayoutCutByTheArcs()
    {
        //  Radius (12) larger than the thickness (8): the layout is the rectangle one (8x8 corner blocks, edges between them) and the arcs cut it.
        Recorder recorder = Recorder.Create();
        GraphTestImageResource edgeImage = recorder.Image(32, 8);
        GraphTestImageResource cornerImage = recorder.Image(8, 8);
        MGBoxShape shape = RoundedShape(80, 40, 8, 12);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        Rectangle bounds = shape.OuterBounds;

        new MGTexturedBorderBrush(edgeImage, cornerImage).Draw(recorder.Args(), null!, shape, geometry);

        List<GraphTexturedTriangleListCall> calls = recorder.Transaction.TexturedTriangleListCalls;
        GraphTexturedTriangleListCall topEdge = calls.Where(call => ReferenceEquals(call.Texture, edgeImage)).MinBy(call => call.Vertices.Average(v => v.Y));
        GraphTexturedTriangleListCall leftEdge = calls.Where(call => ReferenceEquals(call.Texture, edgeImage)).MinBy(call => call.Vertices.Average(v => v.X));

        //  Edge textures reach into the arcs beyond their straight segment (x < 12 for the top edge, y < 12 for the left edge)...
        Assert.Contains(topEdge.Vertices, v => v.X < 12f - 0.01f);
        Assert.Contains(leftEdge.Vertices, v => v.Y < 12f - 0.01f);
        //  ...but never into the corner blocks: the cut follows the layout lines, not the ring quads.
        Assert.All(topEdge.Vertices, v => Assert.True(v.X >= 8f - 0.01f, $"top edge vertex {v} inside the corner block"));
        Assert.All(leftEdge.Vertices, v => Assert.True(v.Y >= 8f - 0.01f, $"left edge vertex {v} inside the corner block"));

        foreach (GraphTexturedTriangleListCall corner in calls.Where(call => ReferenceEquals(call.Texture, cornerImage)))
        {
            Vector2 centroid = new(corner.Vertices.Average(v => v.X), corner.Vertices.Average(v => v.Y));
            float cornerX = centroid.X < bounds.Center.X ? bounds.Left : bounds.Right;
            float cornerY = centroid.Y < bounds.Center.Y ? bounds.Top : bounds.Bottom;
            //  Every corner piece stays inside its 12x12 corner square...
            Assert.All(corner.Vertices, v => Assert.True(Math.Abs(v.X - cornerX) <= 12f + 0.01f && Math.Abs(v.Y - cornerY) <= 12f + 0.01f));
            //  ...covers the 8x8 block where the ring passes through it...
            Assert.Contains(corner.Vertices, v => Math.Abs(v.X - cornerX) <= 8f + 0.01f && Math.Abs(v.Y - cornerY) <= 8f + 0.01f);
            //  ...and also the sliver of ring deeper than the thickness near the diagonal, so the ring has no hole (clamped coordinates there).
            Assert.Contains(corner.Vertices, v => Math.Abs(v.X - cornerX) > 8f + 0.01f && Math.Abs(v.Y - cornerY) > 8f + 0.01f);
            AssertUVsInUnitSquare(corner);
        }

        //  The rectangle layout is also what the rectangle path draws: same edge/corner rectangles, so a texture designed for the square
        //  border keeps its placement and is merely cut by the rounded silhouette.
        float ringArea = MeshArea(geometry.Vertices.ToArray(), geometry.BorderRingIndices.ToArray());
        Assert.InRange(calls.Sum(call => MeshArea(call.Vertices, call.Indices)), ringArea - 0.05f, ringArea + 0.05f);
    }

    [Fact]
    public void TexturedBorderBrush_Transforms_RotateAndFlipUVs()
    {
        Recorder rotated = Recorder.Create();
        GraphTestImageResource edgeImage = rotated.Image(32, 8);
        GraphTestImageResource cornerImage = rotated.Image(8, 8);
        MGBoxShape shape = RoundedShape(80, 40, 4, 12);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);

        //  Standard preset: the top edge is rotated by a multiple of PI/2, so its u axis now runs across the thickness and v along the edge.
        new MGTexturedBorderBrush(edgeImage, cornerImage, TextureTransforms.CreateStandardRotated(Edge.Left, Corner.TopLeft)).Draw(rotated.Args(), null!, shape, geometry);
        List<GraphTexturedTriangleListCall> calls = rotated.Transaction.TexturedTriangleListCalls;
        Assert.All(calls, AssertUVsInUnitSquare);
        GraphTexturedTriangleListCall topEdge = calls.Where(call => ReferenceEquals(call.Texture, edgeImage)).MinBy(call => call.Vertices.Average(v => v.Y));
        float leftV = topEdge.TextureCoordinates[IndexOfMin(topEdge.Vertices, v => v.X)].Y;
        float rightV = topEdge.TextureCoordinates[IndexOfMax(topEdge.Vertices, v => v.X)].Y;
        Assert.True(Math.Abs(rightV - leftV) > 0.9f, "a quarter-turn rotation must map the edge length onto the v axis");

        //  Free angle: still defined, no exception, UVs stay in the unit square.
        Recorder free = Recorder.Create();
        TextureTransforms freeTransforms = new(new EdgeTransforms(0.4f, 0.4f, 0.4f, 0.4f), new CornerTransforms(0.4f, 0.4f, 0.4f, 0.4f));
        new MGTexturedBorderBrush(free.Image(32, 8), free.Image(8, 8), freeTransforms).Draw(free.Args(), null!, shape, geometry);
        Assert.NotEmpty(free.Transaction.TexturedTriangleListCalls);
        Assert.All(free.Transaction.TexturedTriangleListCalls, AssertUVsInUnitSquare);

        //  Reflection: flipping the top edge horizontally swaps u between the left and right ends.
        Recorder flipped = Recorder.Create();
        TextureTransforms flipTransforms = new(new EdgeTransforms(TopReflections: SpriteEffects.FlipHorizontally), new CornerTransforms());
        new MGTexturedBorderBrush(flipped.Image(32, 8), flipped.Image(8, 8), flipTransforms).Draw(flipped.Args(), null!, shape, geometry);
        GraphTexturedTriangleListCall flippedTop = flipped.Transaction.TexturedTriangleListCalls.Where(call => call.Texture.Width == 32).MinBy(call => call.Vertices.Average(v => v.Y));
        Assert.Equal(1f, flippedTop.TextureCoordinates[IndexOfMin(flippedTop.Vertices, v => v.X)].X, 2);
        Assert.Equal(0f, flippedTop.TextureCoordinates[IndexOfMax(flippedTop.Vertices, v => v.X)].X, 2);
    }

    [Fact]
    public void TexturedBorderBrush_NullImages_DrawsNothing()
    {
        Recorder recorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(80, 40, 4, 12);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);

        new MGTexturedBorderBrush().Draw(recorder.Args(), null!, shape, geometry);

        Assert.Empty(recorder.Transaction.TexturedTriangleListCalls);
        Assert.Empty(recorder.Transaction.DrawTextureToCalls);
        Assert.Empty(recorder.Transaction.DrawTextureAtCalls);
    }

    [Fact]
    public void TexturedBorderBrush_WithoutRingMesh_KeepsDocumentedRectanglePath()
    {
        Recorder recorder = Recorder.Create();
        //  Residual case (see Docs/drawing-architecture.md, Limites connues): the border thickness consumes the whole box, so InnerBounds is empty, there is no inner
        //  contour, and BuildBorderRingIndices still returns no ring (documented in the code). A single corner whose thickness merely
        //  reaches its radius no longer triggers this fallback - see ThicknessReachingCornerRadius_NowHasRingMeshAndDrawsTexturedTriangles.
        MGBoxShape shape = RoundedShape(80, 40, 40, 12);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        MGTexturedBorderBrush brush = new(recorder.Image(32, 8), recorder.Image(8, 8));

        Assert.False(geometry.UsesRectangleFastPath);
        Assert.True(shape.Normalize().InnerBounds.Width <= 0 || shape.Normalize().InnerBounds.Height <= 0);
        Assert.False(geometry.HasBorderRingMesh);
        brush.Draw(recorder.Args(), null!, shape, geometry);

        Assert.Empty(recorder.Transaction.TexturedTriangleListCalls);
        Assert.NotEmpty(recorder.Transaction.DrawTextureToCalls);
    }

    /// <summary>Docs/drawing-architecture.md, Limites connues: a border thickness reaching the corner radius used to collapse that corner's inner arc to a single point,
    /// leaving <see cref="MGBoxGeometryBuilder.BuildBorderRingIndices"/> with mismatched contour counts and no ring mesh, so this scenario used
    /// to fall back to the rectangle path (see the previous version of <see cref="TexturedBorderBrush_WithoutRingMesh_KeepsDocumentedRectanglePath"/>).
    /// The inner contour now repeats the collapsed point to match the outer count, so the ring mesh exists and the textured border brush
    /// (and the simpler <see cref="DrawTransactionBoxShapeExtensions.DrawTexturedBorderRing"/> extension) project onto it instead.</summary>
    [Fact]
    public void TexturedBorderBrush_ThicknessReachingCornerRadius_NowHasRingMeshAndDrawsTexturedTriangles()
    {
        Recorder recorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(80, 40, 4, 4);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        MGTexturedBorderBrush brush = new(recorder.Image(32, 8), recorder.Image(8, 8));

        Assert.Equal(geometry.OuterContour.Count, geometry.InnerContour.Count);
        Assert.True(geometry.HasBorderRingMesh);

        brush.Draw(recorder.Args(), null!, shape, geometry);

        List<GraphTexturedTriangleListCall> calls = recorder.Transaction.TexturedTriangleListCalls;
        Assert.Empty(recorder.Transaction.DrawTextureToCalls);
        Assert.NotEmpty(calls);
        Assert.All(calls, AssertUVsInUnitSquare);
        Assert.All(calls, call => Assert.All(call.TextureCoordinates, uv => Assert.True(float.IsFinite(uv.X) && float.IsFinite(uv.Y))));
        Assert.All(calls, call => Assert.All(call.Vertices, v => Assert.True(float.IsFinite(v.X) && float.IsFinite(v.Y))));

        //  The simpler single-texture DrawTexturedBorderRing extension (no production caller yet, but exposed for future paints)
        //  also emits now that the ring mesh exists, with one UV per vertex and finite coordinates throughout.
        Recorder ringRecorder = Recorder.Create();
        GraphTestImageResource ringTexture = ringRecorder.Image(16, 16);
        Vector2[] textureCoordinates = geometry.Vertices.Select(_ => new Vector2(0.5f, 0.5f)).ToArray();

        ringRecorder.Transaction.DrawTexturedBorderRing(Vector2.Zero, geometry, ringTexture, textureCoordinates, Color.White);

        GraphTexturedTriangleListCall ringCall = Assert.Single(ringRecorder.Transaction.TexturedTriangleListCalls);
        Assert.Equal(geometry.Vertices.Count, ringCall.Vertices.Length);
        Assert.Equal(geometry.BorderRingIndices, ringCall.Indices);
        Assert.All(ringCall.TextureCoordinates, uv => Assert.True(float.IsFinite(uv.X) && float.IsFinite(uv.Y)));
    }

    [Fact]
    public void TexturedBorderBrush_RectangleFastPath_KeepsRectangleDrawCalls()
    {
        Recorder rounded = Recorder.Create();
        Recorder legacy = Recorder.Create();
        GraphTestImageResource edgeImage = rounded.Image(32, 8);
        GraphTestImageResource cornerImage = rounded.Image(8, 8);
        MGBoxShape shape = RoundedShape(80, 40, 4, 0);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        MGTexturedBorderBrush brush = new(edgeImage, cornerImage);

        brush.Draw(rounded.Args(), null!, shape, geometry);
        brush.Draw(legacy.Args(), null!, shape.OuterBounds, shape.NormalizedBorderThickness);

        Assert.Empty(rounded.Transaction.TexturedTriangleListCalls);
        Assert.NotEmpty(rounded.Transaction.DrawTextureToCalls);
        Assert.Equal(legacy.Transaction.DrawTextureToCalls, rounded.Transaction.DrawTextureToCalls);
        Assert.Equal(legacy.Transaction.DrawTextureAtCalls, rounded.Transaction.DrawTextureAtCalls);
    }

    #endregion

    #region MGNineSliceFillBrush

    [Fact]
    public void NineSliceFillBrush_RectangleFastPath_KeepsRectangleDrawCalls()
    {
        Recorder rounded = Recorder.Create();
        Recorder legacy = Recorder.Create();
        MGTextureData source = new(rounded.Image(30, 30));
        MGBoxShape shape = RoundedShape(100, 60, 0, 0);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        MGNineSliceFillBrush brush = new(new Thickness(10, 10, 10, 10), source);

        Assert.True(geometry.UsesRectangleFastPath);
        brush.Draw(rounded.Args(), null!, shape, geometry);
        brush.Draw(legacy.Args(), null!, shape.OuterBounds);

        Assert.Empty(rounded.Transaction.TexturedTriangleListCalls);
        Assert.Equal(legacy.Transaction.DrawTextureToCalls, rounded.Transaction.DrawTextureToCalls);
        Assert.Equal(9, rounded.Transaction.DrawTextureToCalls.Count);
    }

    /// <summary>Rounded path: only the top-right and bottom-left corners are rounded, so the top-left and bottom-right corners of the shape
    /// stay sharp - their patches keep the shape's exact outer corner, which pins the UV orientation below. Each of the 9 patches gets its own
    /// image with a source rectangle that is a strict subset of it, so a UV landing outside that subset would prove a mismapped patch, and 9
    /// distinct images never share a batch, so one recorded call maps to exactly one patch.</summary>
    [Fact]
    public void NineSliceFillBrush_RoundedShape_ProjectsEachPatchOntoTheSilhouetteWithItsOwnSourceRectangle()
    {
        Recorder recorder = Recorder.Create();
        MGBoxShape shape = new(new Rectangle(0, 0, 100, 60), new Thickness(0, 0, 0, 0), new MGCornerRadius(0, 8, 0, 8));
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        Assert.False(geometry.UsesRectangleFastPath);

        Rectangle patchSource = new(5, 5, 20, 20);
        GraphTestImageResource topLeftImage = recorder.Image(40, 40);
        GraphTestImageResource topCenterImage = recorder.Image(40, 40);
        GraphTestImageResource topRightImage = recorder.Image(40, 40);
        GraphTestImageResource middleLeftImage = recorder.Image(40, 40);
        GraphTestImageResource middleCenterImage = recorder.Image(40, 40);
        GraphTestImageResource middleRightImage = recorder.Image(40, 40);
        GraphTestImageResource bottomLeftImage = recorder.Image(40, 40);
        GraphTestImageResource bottomCenterImage = recorder.Image(40, 40);
        GraphTestImageResource bottomRightImage = recorder.Image(40, 40);

        MGNineSliceFillBrush brush = new(new Thickness(10, 10, 10, 10),
            new MGTextureData(topLeftImage, patchSource), new MGTextureData(topCenterImage, patchSource), new MGTextureData(topRightImage, patchSource),
            new MGTextureData(middleLeftImage, patchSource), new MGTextureData(middleCenterImage, patchSource), new MGTextureData(middleRightImage, patchSource),
            new MGTextureData(bottomLeftImage, patchSource), new MGTextureData(bottomCenterImage, patchSource), new MGTextureData(bottomRightImage, patchSource));

        brush.Draw(recorder.Args(), null!, shape, geometry);

        List<GraphTexturedTriangleListCall> calls = recorder.Transaction.TexturedTriangleListCalls;
        Assert.Empty(recorder.Transaction.DrawTextureToCalls);
        Assert.Empty(recorder.Transaction.DrawTextureAtCalls);
        Assert.Equal(9, calls.Count);

        GraphTexturedTriangleListCall FindCall(IUIImageResource image) => calls.Single(c => ReferenceEquals(c.Texture, image));

        Vector2 uvTopLeft = new(5f / 40f, 5f / 40f);
        Vector2 uvBottomRight = new(25f / 40f, 25f / 40f);

        //  No overflow beyond the rounded silhouette, and every UV stays inside the patch's own source rectangle (not the whole [0,1] texture).
        Vector2 center = shape.OuterBounds.Center.ToVector2();
        foreach (GraphTexturedTriangleListCall call in calls)
        {
            Assert.Equal(call.Vertices.Length, call.TextureCoordinates.Length);
            Assert.True(call.Indices.Length >= 3 && call.Indices.Length % 3 == 0);
            for (int i = 0; i < call.Vertices.Length; i++)
            {
                Vector2 vertex = call.Vertices[i];
                Vector2 inward = vertex + Vector2.Normalize(center - vertex) * 0.25f;
                Assert.True(shape.Contains(inward), $"vertex {vertex} lies outside the rounded shape");

                Vector2 uv = call.TextureCoordinates[i];
                Assert.InRange(uv.X, uvTopLeft.X - 1e-4f, uvBottomRight.X + 1e-4f);
                Assert.InRange(uv.Y, uvTopLeft.Y - 1e-4f, uvBottomRight.Y + 1e-4f);
            }
        }

        //  Orientation pinning: the unrounded top-left corner survives clipping exactly, and maps to the top-left patch's own source top-left UV.
        GraphTexturedTriangleListCall topLeftCall = FindCall(topLeftImage);
        int topLeftVertexIndex = Array.IndexOf(topLeftCall.Vertices, new Vector2(shape.OuterBounds.Left, shape.OuterBounds.Top));
        Assert.True(topLeftVertexIndex >= 0, "the top-left patch must keep the shape's sharp top-left corner");
        Assert.Equal(uvTopLeft, topLeftCall.TextureCoordinates[topLeftVertexIndex]);

        //  ...and the unrounded bottom-right corner maps to the bottom-right patch's own source bottom-right UV.
        GraphTexturedTriangleListCall bottomRightCall = FindCall(bottomRightImage);
        int bottomRightVertexIndex = Array.IndexOf(bottomRightCall.Vertices, new Vector2(shape.OuterBounds.Right, shape.OuterBounds.Bottom));
        Assert.True(bottomRightVertexIndex >= 0, "the bottom-right patch must keep the shape's sharp bottom-right corner");
        Assert.Equal(uvBottomRight, bottomRightCall.TextureCoordinates[bottomRightVertexIndex]);

        //  A vertex on a straight edge shared with the rectangle path (TopCenter sits clear of both rounded corners, so it is entirely
        //  unclipped): its own destination top-left corner maps to its source top-left UV, exactly like a full, unclamped stretch would.
        GraphTexturedTriangleListCall topCenterCall = FindCall(topCenterImage);
        int topCenterVertexIndex = Array.IndexOf(topCenterCall.Vertices, new Vector2(10, 0));
        Assert.True(topCenterVertexIndex >= 0);
        Assert.Equal(uvTopLeft, topCenterCall.TextureCoordinates[topCenterVertexIndex]);
    }

    [Fact]
    public void NineSliceFillBrush_RoundedShape_BatchesPatchesSharingOneImage()
    {
        Recorder recorder = Recorder.Create();
        MGBoxShape shape = new(new Rectangle(0, 0, 100, 60), new Thickness(0, 0, 0, 0), new MGCornerRadius(0, 8, 0, 8));
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        //  Divisible by 3: automatic 10x10 source regions, the same image and opacity on all 9 patches.
        MGTextureData source = new(recorder.Image(30, 30));
        MGNineSliceFillBrush brush = new(new Thickness(10, 10, 10, 10), source);

        brush.Draw(recorder.Args(), null!, shape, geometry);

        //  All 9 patches share one image and the same color mask, so they batch into a single draw call.
        GraphTexturedTriangleListCall call = Assert.Single(recorder.Transaction.TexturedTriangleListCalls);
        Assert.Empty(recorder.Transaction.DrawTextureToCalls);
        Assert.Empty(recorder.Transaction.DrawTextureAtCalls);
        Assert.NotEmpty(call.Vertices);

        //  Stretch reaches its full extent: the rightmost vertex (right column, source x = 20..30 of a 30px-wide image) hits u = 1,
        //  and the leftmost (left column, source x = 0..10) hits u = 0.
        Assert.Equal(1f, call.TextureCoordinates[IndexOfMax(call.Vertices, v => v.X)].X, 3);
        Assert.Equal(0f, call.TextureCoordinates[IndexOfMin(call.Vertices, v => v.X)].X, 3);
    }

    /// <summary>A margin of 0 on one side collapses that column/row to zero width/height: like the rectangle path, the shape-aware path must
    /// skip that patch entirely rather than emit a degenerate (zero-area) triangle list for it.</summary>
    [Fact]
    public void NineSliceFillBrush_RoundedShape_SkipsPatchesInACollapsedColumn()
    {
        Recorder recorder = Recorder.Create();
        MGBoxShape shape = new(new Rectangle(0, 0, 100, 60), new Thickness(0, 0, 0, 0), new MGCornerRadius(0, 8, 0, 8));
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);

        GraphTestImageResource topRightImage = recorder.Image(10, 10);
        GraphTestImageResource middleRightImage = recorder.Image(10, 10);
        GraphTestImageResource bottomRightImage = recorder.Image(10, 10);
        MGTextureData other = new(recorder.Image(10, 10));

        //  Right margin 0: RightColumnSize collapses to 0, so the right column's 3 patches must be skipped.
        MGNineSliceFillBrush brush = new(new Thickness(10, 10, 0, 10),
            other, other, new MGTextureData(topRightImage),
            other, other, new MGTextureData(middleRightImage),
            other, other, new MGTextureData(bottomRightImage));

        brush.Draw(recorder.Args(), null!, shape, geometry);

        List<GraphTexturedTriangleListCall> calls = recorder.Transaction.TexturedTriangleListCalls;
        Assert.NotEmpty(calls);
        Assert.DoesNotContain(calls, c => ReferenceEquals(c.Texture, topRightImage) || ReferenceEquals(c.Texture, middleRightImage) || ReferenceEquals(c.Texture, bottomRightImage));
        //  The remaining 6 patches share one image and opacity, so they still batch into a single call.
        Assert.Single(calls);
    }

    #endregion

    #region Helpers

    private static MGBoxShape RoundedShape(int width, int height, int thickness, int radius)
        => new(new Rectangle(0, 0, width, height), new Thickness(thickness), new MGCornerRadius(radius));

    private static void AssertUVsInUnitSquare(GraphTexturedTriangleListCall call)
    {
        Assert.Equal(call.Vertices.Length, call.TextureCoordinates.Length);
        foreach (Vector2 uv in call.TextureCoordinates)
        {
            Assert.InRange(uv.X, -UvTolerance, 1f + UvTolerance);
            Assert.InRange(uv.Y, -UvTolerance, 1f + UvTolerance);
        }
    }

    private static float MeshArea(Vector2[] vertices, int[] indices)
    {
        float area = 0f;
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            Vector2 a = vertices[indices[i]];
            Vector2 b = vertices[indices[i + 1]];
            Vector2 c = vertices[indices[i + 2]];
            area += Math.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) / 2f;
        }

        return area;
    }

    private static int IndexOfMin(Vector2[] vertices, Func<Vector2, float> selector)
    {
        int index = 0;
        for (int i = 1; i < vertices.Length; i++)
        {
            if (selector(vertices[i]) < selector(vertices[index]))
            {
                index = i;
            }
        }

        return index;
    }

    private static int IndexOfMax(Vector2[] vertices, Func<Vector2, float> selector)
    {
        int index = 0;
        for (int i = 1; i < vertices.Length; i++)
        {
            if (selector(vertices[i]) > selector(vertices[index]))
            {
                index = i;
            }
        }

        return index;
    }

    private readonly record struct Recorder(GraphTestRuntime Runtime, GraphNoOpDrawTransaction Transaction)
    {
        private static int _imageCounter;

        public static Recorder Create(GraphTestRuntime? runtime = null)
        {
            runtime ??= new GraphTestRuntime(new Rectangle(0, 0, 960, 540));
            return new(runtime, new GraphNoOpDrawTransaction(runtime, DrawSettings.Default));
        }

        public GraphTestImageResource Image(int width, int height) => new($"tex-{System.Threading.Interlocked.Increment(ref _imageCounter)}", width, height);

        public ElementDrawArgs Args() => Args(Point.Zero);

        public ElementDrawArgs Args(Point offset)
            => new(new DrawBaseArgs(TimeSpan.Zero, Transaction, 1f), new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), offset);
    }

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window)
    {
        public static Harness Create(float scale = 1f)
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
            runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 24, 24, 480, 260)
            {
                WindowStyle = WindowStyle.None,
                Padding = new Thickness(0),
                Scale = scale,
            };
            return new(runtime, desktop, window);
        }

        public void Show(MGElement element)
        {
            Window.SetContent(element);
            Desktop.Windows.Add(Window);
            Desktop.Update();
            Desktop.Update();
        }
    }

    #endregion
}
