using MGUI.Core.UI.Shapes;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace MGUI.Core.UI.Brushes.FillBrushes;

/// <summary>An <see cref="IFillBrush"/> that fills its bounds with a <see cref="Texture2D"/>. Freezable (ADR-0009): a sealed
/// mutable class deriving from <see cref="UIFreezableBrush"/> whose setters throw once frozen; <see cref="Copy"/> always returns an
/// unfrozen instance. <see cref="Source"/> is shared by reference on <see cref="Copy"/> (the underlying texture data, not owned by
/// this brush).</summary>
public sealed class MGTextureFillBrush : UIFreezableBrush, IFillBrush
{
    private MGTextureData _Source;
    public MGTextureData Source
    {
        get => _Source;
        set => SetProperty(ref _Source, value);
    }

    private Stretch _Stretch;
    public Stretch Stretch
    {
        get => _Stretch;
        set => SetProperty(ref _Stretch, value);
    }

    private Color _Color;
    public Color Color
    {
        get => _Color;
        set => SetProperty(ref _Color, value);
    }

    private bool _Tile;
    /// <summary>If true, the texture is drawn at its natural pixel size (<see cref="MGTextureData.RenderSize"/>, or the frame's cell size when
    /// <see cref="FrameGrid"/> is set) and tiled repeatedly to fill the entire bounds. When true, <see cref="Stretch"/> is ignored.<para/>
    /// Default value: false</summary>
    public bool Tile
    {
        get => _Tile;
        set => SetProperty(ref _Tile, value);
    }

    private MGSpriteSheetGrid? _FrameGrid;
    /// <summary>When set, this brush draws one cell of a sprite sheet (ADR-0011, decision C4) instead of the whole of <see cref="Source"/>:
    /// <see cref="FrameIndex"/> selects the cell inside <see cref="MGTextureData.SourceRect"/> (or the whole image when it has none), which
    /// becomes the effective source rectangle of every drawing path, and the cell's <see cref="MGSpriteSheetGrid.CellSize"/> becomes the
    /// natural size (still overridden by an explicit <see cref="MGTextureData.RenderSizeOverride"/>). Null (the default) leaves every path
    /// exactly as it behaves without a grid. Validated on set (<see cref="MGSpriteSheetGrid.Validate"/>); throws when frozen.</summary>
    public MGSpriteSheetGrid? FrameGrid
    {
        get => _FrameGrid;
        set
        {
            ThrowIfFrozen();
            value?.Validate();
            SetProperty(ref _FrameGrid, value);
        }
    }

    private int _FrameIndex;
    /// <summary>The frame of <see cref="FrameGrid"/> this brush currently draws, clamped to <c>[0, FrameGrid.Value.EffectiveFrameCount - 1]</c>
    /// by <see cref="MGSpriteSheetGrid.GetFrameRectangle(int, Rectangle)"/> when a grid is set; ignored otherwise. Must not be negative.
    /// Default value: 0.</summary>
    public int FrameIndex
    {
        get => _FrameIndex;
        set
        {
            ThrowIfFrozen();
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(FrameIndex)} cannot be negative.");
            }

            SetProperty(ref _FrameIndex, value);
        }
    }

    /// <param name="SourceName">The name of the <see cref="MGTextureData"/> in <see cref="MGResources.Textures"/> that should be drawn by this <see cref="MGTextureFillBrush"/>.<para/>
    /// See also: <see cref="MGElement.GetResources"/>, <see cref="MGResources.Textures"/>, <see cref="MGResources.AddTexture(string, MGTextureData)"/></param>
    public MGTextureFillBrush(MGDesktop Desktop, string SourceName, Stretch Stretch = Stretch.Fill, Color? Color = null, bool Tile = false)
    {
        if (Desktop == null)
        {
            throw new ArgumentNullException(nameof(Desktop));
        }

        if (SourceName == null)
        {
            throw new ArgumentNullException(nameof(SourceName));
        }

        if (!Desktop.Resources.TryGetTexture(SourceName, out var Source))
        {
            throw new InvalidOperationException($"No Texture was found with the name '{SourceName}' in {nameof(MGResources)}.{nameof(MGResources.Textures)}.");
        }

        _Source = Source;
        _Stretch = Stretch;
        _Color = Color ?? Microsoft.Xna.Framework.Color.White;
        _Tile = Tile;
    }

    public MGTextureFillBrush(MGTextureData Source, Stretch Stretch = Stretch.Fill, Color? Color = null, bool Tile = false)
    {
        _Source = Source;
        _Stretch = Stretch;
        _Color = Color ?? Microsoft.Xna.Framework.Color.White;
        _Tile = Tile;
    }

    /// <summary>The natural size used by every drawing path (ADR-0011, decision C4): <see cref="MGTextureData.RenderSize"/> without a
    /// <see cref="FrameGrid"/> (unchanged), or <see cref="MGTextureData.RenderSizeOverride"/> when set, else the grid's
    /// <see cref="MGSpriteSheetGrid.CellSize"/>, when one is set.</summary>
    private Size EffectiveNaturalSize => FrameGrid is { } grid ? Source.RenderSizeOverride ?? new Size(grid.CellSize.X, grid.CellSize.Y) : Source.RenderSize;

    private int UnstretchedWidth => EffectiveNaturalSize.Width;
    private int UnstretchedHeight => EffectiveNaturalSize.Height;
    private double UnstretchedAspectRatio => UnstretchedHeight == 0 ? 1.0 : UnstretchedWidth * 1.0 / UnstretchedHeight;

    /// <summary>The source region <see cref="FrameGrid"/> operates over: <see cref="MGTextureData.SourceRect"/>, or the whole image when it
    /// has none.</summary>
    private Rectangle SourceRegion => Source.SourceRect ?? new Rectangle(0, 0, Source.Image.Width, Source.Image.Height);

    /// <summary>The effective source rectangle used by the tiled paths and the rounded path (both always need a concrete <see cref="Rectangle"/>,
    /// never null): <see cref="SourceRegion"/> without a grid (unchanged), or the current frame's cell (<see cref="MGSpriteSheetGrid.GetFrameRectangle(int, Rectangle)"/>)
    /// with one.</summary>
    private Rectangle EffectiveFullSource => FrameGrid is { } grid ? grid.GetFrameRectangle(FrameIndex, SourceRegion) : SourceRegion;

    /// <summary>The effective source rectangle used by the non-tiled rectangle path, which passes null through to
    /// <see cref="IUIDrawContext.DrawTextureTo(IUIImageResource, Rectangle?, Rectangle, Color)"/> exactly as today when there is no grid (so a
    /// null <see cref="MGTextureData.SourceRect"/> keeps meaning "the whole texture" to the renderer, byte-for-byte unchanged).</summary>
    private Rectangle? EffectiveSourceRectOrNull => FrameGrid is { } grid ? grid.GetFrameRectangle(FrameIndex, SourceRegion) : Source.SourceRect;

    private static int GetWidthByAspectRatio(int Height, double AspectRatio) => (int)Math.Round(Height * AspectRatio, MidpointRounding.ToEven);
    private static int GetHeightByAspectRatio(int Width, double AspectRatio) => (int)Math.Round(Width * 1 / AspectRatio, MidpointRounding.ToEven);

    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
    {
        if (DA.Opacity > 0 && !DA.Opacity.IsAlmostZero())
        {
            var drawColor = Color * DA.Opacity * Source.Opacity;

            if (Tile)
            {
                // Draw the texture at its natural size, tiling it to fill the bounds
                var tileW = UnstretchedWidth;
                var tileH = UnstretchedHeight;
                if (tileW > 0 && tileH > 0)
                {
                    var fullSrc = EffectiveFullSource;
                    for (var y = Bounds.Top; y < Bounds.Bottom; y += tileH)
                    {
                        for (var x = Bounds.Left; x < Bounds.Right; x += tileW)
                        {
                            var drawW = Math.Min(tileW, Bounds.Right - x);
                            var drawH = Math.Min(tileH, Bounds.Bottom - y);
                            Rectangle dest = new(x, y, drawW, drawH);
                            var src = drawW < tileW || drawH < tileH
                                ? new Rectangle(fullSrc.X, fullSrc.Y, Math.Min(drawW, fullSrc.Width), Math.Min(drawH, fullSrc.Height))
                                : fullSrc;
                            DA.Context.DrawTextureTo(Source.Image, src, dest.GetTranslated(DA.Offset), drawColor);
                        }
                    }
                }
                return;
            }

            var Destination = GetStretchedDestination(Bounds);
            DA.Context.DrawTextureTo(Source.Image, EffectiveSourceRectOrNull, Destination.GetTranslated(DA.Offset), drawColor);
        }
    }

    /// <summary>Destination rectangle of the (non-tiled) texture inside <paramref name="Bounds"/> according to <see cref="Stretch"/>.
    /// Shared by the rectangle path and the rounded path so both agree on placement.</summary>
    private Rectangle GetStretchedDestination(Rectangle Bounds)
    {
        var AspectRatio = UnstretchedAspectRatio;
        if (Stretch == Stretch.None)
        {
            return MGElement.ApplyAlignment(Bounds, HorizontalAlignment.Center, VerticalAlignment.Center, new Size(UnstretchedWidth, UnstretchedHeight));
        }
        else if (Stretch == Stretch.Uniform)
        {
            var AvailableWidth = Bounds.Width;
            var AvailableHeight = Bounds.Height;
            var ConsumedWidth = Math.Min(AvailableWidth, GetWidthByAspectRatio(AvailableHeight, AspectRatio));
            var ConsumedHeight = Math.Min(AvailableHeight, GetHeightByAspectRatio(AvailableWidth, AspectRatio));
            return MGElement.ApplyAlignment(Bounds, HorizontalAlignment.Center, VerticalAlignment.Center, new Size(ConsumedWidth, ConsumedHeight));
        }
        else if (Stretch == Stretch.UniformToFill)
        {
            var AvailableWidth = Bounds.Width;
            var AvailableHeight = Bounds.Height;
            var ConsumedWidth = Math.Max(AvailableWidth, GetWidthByAspectRatio(AvailableHeight, AspectRatio));
            var ConsumedHeight = Math.Max(AvailableHeight, GetHeightByAspectRatio(AvailableWidth, AspectRatio));
            return MGElement.ApplyAlignment(Bounds, HorizontalAlignment.Center, VerticalAlignment.Center, new Size(ConsumedWidth, ConsumedHeight));
        }
        else if (Stretch == Stretch.Fill)
        {
            return Bounds;
        }
        else
        {
            throw new NotImplementedException($"Unrecognized {nameof(Stretch)}: {Stretch}");
        }
    }

    /// <summary>Rounded path: projects the texture onto the fill mesh of <paramref name="Geometry"/> so that the paint follows the rounded silhouette.<para/>
    /// Mapping rules (owned by the paint, the geometry is never rebuilt here):<br/>
    /// - <see cref="Stretch.Fill"/> and <see cref="Stretch.UniformToFill"/>: UVs projected from the stretched destination, exact;<br/>
    /// - <see cref="Stretch.Uniform"/> and <see cref="Stretch.None"/>: same projection, clipped to the destination rectangle (screen space) so the
    ///   uncovered part of the shape stays empty exactly like the rectangle path;<br/>
    /// - <see cref="Tile"/> with the whole texture: UVs in tile units with a wrap sampler;<br/>
    /// - <see cref="Tile"/> with a sub-rectangle of an atlas: a wrap sampler would repeat the whole atlas, so each tile of the same grid
    ///   as the rectangle path (including the clamped source rect of a partial edge tile) is emitted as its own textured quad, clipped to
    ///   <see cref="MGBoxGeometry.OuterContour"/> (<see cref="MGConvexPolygonClipper"/>) and sampled with the clamp sampler, no wrap;<br/>
    /// When <see cref="MGBoxGeometry.UsesRectangleFastPath"/> is true the legacy rectangle rendering is used unchanged.</summary>
    public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
    {
        if (Geometry.UsesRectangleFastPath || !Geometry.HasFillMesh)
        {
            //  Rectangle fast path: identical to the legacy rectangle rendering.
            Draw(DA, Element, Shape.OuterBounds);
            return;
        }

        if (DA.Opacity <= 0 || DA.Opacity.IsAlmostZero())
        {
            return;
        }

        var image = Source.Image;
        if (image == null || image.IsDisposed)
        {
            return;
        }

        var bounds = Shape.OuterBounds;
        var drawColor = Color * DA.Opacity * Source.Opacity;
        var origin = DA.Offset.ToVector2();
        var fullSource = EffectiveFullSource;
        var vertices = Geometry.Vertices;

        if (Tile)
        {
            var tileW = UnstretchedWidth;
            var tileH = UnstretchedHeight;
            if (tileW <= 0 || tileH <= 0)
            {
                return;
            }

            var isWholeTexture = fullSource.X == 0 && fullSource.Y == 0 && fullSource.Width == image.Width && fullSource.Height == image.Height;
            if (isWholeTexture)
            {
                var tileUVs = new Vector2[vertices.Count];
                for (var i = 0; i < tileUVs.Length; i++)
                {
                    var vertex = vertices[i];
                    tileUVs[i] = new Vector2((vertex.X - bounds.Left) / tileW, (vertex.Y - bounds.Top) / tileH);
                }

                using (DA.Context.SetDrawSettingsTemporary(DA.Context.CurrentSettings with { SamplerType = SamplerType.LinearWrap }))
                {
                    DA.Context.FillTexturedRoundedRectangle(origin, Geometry, image, tileUVs, drawColor);
                }

                return;
            }

            //  Atlas sub-rectangle (see Docs/drawing-architecture.md): a wrap sampler would repeat the whole atlas, so tile by
            //  textured quads clipped to the silhouette instead - same tile grid and partial-edge source clamping as the rectangle path.
            DrawTiledAtlasSubRectangle(DA, Geometry, image, bounds, fullSource, tileW, tileH, origin, drawColor);
            return;
        }

        var destination = GetStretchedDestination(bounds);
        if (destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }

        var inverseImageWidth = 1f / image.Width;
        var inverseImageHeight = 1f / image.Height;
        var uvs = new Vector2[vertices.Count];
        for (var i = 0; i < uvs.Length; i++)
        {
            var vertex = vertices[i];
            var u = (vertex.X - destination.Left) / destination.Width;
            var v = (vertex.Y - destination.Top) / destination.Height;
            uvs[i] = new Vector2((fullSource.X + u * fullSource.Width) * inverseImageWidth, (fullSource.Y + v * fullSource.Height) * inverseImageHeight);
        }

        var destinationCoversBounds = destination.Left <= bounds.Left && destination.Top <= bounds.Top && destination.Right >= bounds.Right && destination.Bottom >= bounds.Bottom;
        if (destinationCoversBounds)
        {
            DA.Context.FillTexturedRoundedRectangle(origin, Geometry, image, uvs, drawColor);
            return;
        }

        //  Stretch.Uniform / Stretch.None: only the destination rectangle is painted, like the rectangle path. The clip is consumed in screen space
        //  (see MGRatingControl); Element is null only for unit tests that draw the brush without an element, hence the translated fallback.
        var clipLayout = Rectangle.Intersect(destination, bounds);
        var clipScreen = Element != null
            ? Element.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, clipLayout)
            : clipLayout.GetTranslated(DA.Offset);
        using (DA.Context.PushRectangleClip(clipScreen, true))
        {
            DA.Context.FillTexturedRoundedRectangle(origin, Geometry, image, uvs, drawColor);
        }
    }

    /// <summary>Tiles <paramref name="image"/>'s <paramref name="fullSource"/> sub-rectangle over <paramref name="bounds"/> using the exact same
    /// tile grid and partial-edge source clamping as the rectangle path (<see cref="Draw(ElementDrawArgs, MGElement, Rectangle)"/>), but emits
    /// each tile as a textured quad clipped against <see cref="MGBoxGeometry.OuterContour"/> (<see cref="MGConvexPolygonClipper.ClipToConvexPolygon"/>)
    /// instead of a plain <c>DrawTextureTo</c> call, so a tile that falls under a rounded corner is cut to the silhouette. Each clipped tile's UVs
    /// come from its own (possibly clamped) source rectangle mapped over its own (possibly clamped) destination rectangle
    /// (<see cref="MGConvexPolygonClipper.InterpolateRectUV"/>), so the top-left of an unclamped tile always lands on <paramref name="fullSource"/>'s
    /// top-left UV and the bottom-right on its bottom-right UV - no wrap sampler is used. All tiles share one image and color mask, so they batch
    /// into as few <see cref="IUIDrawContext.DrawTexturedTriangleList"/> calls as fit under <see cref="short.MaxValue"/> vertices each.</summary>
    private void DrawTiledAtlasSubRectangle(ElementDrawArgs DA, MGBoxGeometry Geometry, IUIImageResource image, Rectangle bounds, Rectangle fullSource,
        int tileW, int tileH, Vector2 origin, Color drawColor)
    {
        var outerContour = Geometry.OuterContour;
        if (outerContour.Count < 3)
        {
            return;
        }

        var inverseImageWidth = 1f / image.Width;
        var inverseImageHeight = 1f / image.Height;

        List<Vector2> quadPolygon = new(4);
        List<Vector2> clipped = new(8);
        List<Vector2> scratch = new(8);
        List<Vector2> batchVertices = new();
        List<Vector2> batchUVs = new();
        List<int> batchIndices = new();

        void Flush()
        {
            if (batchVertices.Count > 0)
            {
                DA.Context.DrawTexturedTriangleList(origin, image, batchVertices, batchUVs, batchIndices, drawColor);
                batchVertices.Clear();
                batchUVs.Clear();
                batchIndices.Clear();
            }
        }

        for (var y = bounds.Top; y < bounds.Bottom; y += tileH)
        {
            for (var x = bounds.Left; x < bounds.Right; x += tileW)
            {
                var drawW = Math.Min(tileW, bounds.Right - x);
                var drawH = Math.Min(tileH, bounds.Bottom - y);
                Rectangle dest = new(x, y, drawW, drawH);
                var src = drawW < tileW || drawH < tileH
                    ? new Rectangle(fullSource.X, fullSource.Y, Math.Min(drawW, fullSource.Width), Math.Min(drawH, fullSource.Height))
                    : fullSource;

                quadPolygon.Clear();
                quadPolygon.Add(new Vector2(dest.Left, dest.Top));
                quadPolygon.Add(new Vector2(dest.Right, dest.Top));
                quadPolygon.Add(new Vector2(dest.Right, dest.Bottom));
                quadPolygon.Add(new Vector2(dest.Left, dest.Bottom));

                MGConvexPolygonClipper.ClipToConvexPolygon(quadPolygon, outerContour, clipped, scratch);
                if (clipped.Count < 3)
                {
                    continue;
                }

                if (batchVertices.Count + clipped.Count > short.MaxValue)
                {
                    Flush();
                }

                Vector2 uvTopLeft = new(src.Left * inverseImageWidth, src.Top * inverseImageHeight);
                Vector2 uvBottomRight = new(src.Right * inverseImageWidth, src.Bottom * inverseImageHeight);

                var baseIndex = batchVertices.Count;
                foreach (var vertex in clipped)
                {
                    batchVertices.Add(vertex);
                    batchUVs.Add(MGConvexPolygonClipper.InterpolateRectUV(vertex, dest, uvTopLeft, uvBottomRight));
                }
                MGConvexPolygonClipper.AppendFanTriangles(baseIndex, clipped.Count, batchIndices);
            }
        }

        Flush();
    }

    public IFillBrush Copy() => new MGTextureFillBrush(Source, Stretch, Color, Tile) { FrameGrid = FrameGrid, FrameIndex = FrameIndex };

    /// <summary>Value equality (ADR-0009): two texture brushes are equal when <see cref="Source"/> (a record struct: this compares its
    /// <see cref="MGTextureData.Image"/> by reference, the same underlying texture data, plus its other fields by value), <see cref="Stretch"/>,
    /// <see cref="Color"/>, <see cref="Tile"/>, <see cref="FrameGrid"/> and <see cref="FrameIndex"/> (ADR-0011, decision C4) all match,
    /// regardless of frozen state or instance identity.</summary>
    public bool ValueEquals(IFillBrush other) => other is MGTextureFillBrush t
        && t.Source.Equals(Source) && t.Stretch == Stretch && t.Color == Color && t.Tile == Tile
        && t.FrameGrid.Equals(FrameGrid) && t.FrameIndex == FrameIndex;

    /// <summary>Decision taken during delivery (ADR-0009): see <see cref="MGSolidFillBrush.Equals(object)"/> for the rationale
    /// (by-value <see cref="object.Equals(object)"/>/<see cref="GetHashCode"/>, applied consistently to every converted fill brush).</summary>
    public override bool Equals(object obj) => ValueEquals(obj as IFillBrush);
    public override int GetHashCode() => HashCode.Combine(Source, Stretch, Color, Tile, FrameGrid, FrameIndex);
}