using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MGUI.Shared.Helpers;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;

namespace MGUI.Core.UI.Brushes.Fill_Brushes;

/// <summary>An <see cref="IFillBrush"/> that fills its bounds with a <see cref="Texture2D"/></summary>
public readonly struct MGTextureFillBrush : IFillBrush
{
    public readonly MGTextureData Source;
    public readonly Stretch Stretch;
    public readonly Color Color;
    /// <summary>If true, the texture is drawn at its natural pixel size (<see cref="MGTextureData.RenderSize"/>) and tiled
    /// repeatedly to fill the entire bounds. When true, <see cref="Stretch"/> is ignored.<para/>
    /// Default value: false</summary>
    public readonly bool Tile;

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

        if (!Desktop.Resources.TryGetTexture(SourceName, out MGTextureData Source))
        {
            throw new InvalidOperationException($"No Texture was found with the name '{SourceName}' in {nameof(MGResources)}.{nameof(MGResources.Textures)}.");
        }

        this.Source = Source;
        this.Stretch = Stretch;
        this.Color = Color ?? Microsoft.Xna.Framework.Color.White;
        this.Tile = Tile;
    }

    public MGTextureFillBrush(MGTextureData Source, Stretch Stretch = Stretch.Fill, Color? Color = null, bool Tile = false)
    {
        this.Source = Source;
        this.Stretch = Stretch;
        this.Color = Color ?? Microsoft.Xna.Framework.Color.White;
        this.Tile = Tile;
    }

    private int UnstretchedWidth => Source.RenderSize.Width;
    private int UnstretchedHeight => Source.RenderSize.Height;
    private double UnstretchedAspectRatio => UnstretchedHeight == 0 ? 1.0 : UnstretchedWidth * 1.0 / UnstretchedHeight;

    private static int GetWidthByAspectRatio(int Height, double AspectRatio) => (int)Math.Round(Height * AspectRatio, MidpointRounding.ToEven);
    private static int GetHeightByAspectRatio(int Width, double AspectRatio) => (int)Math.Round(Width * 1 / AspectRatio, MidpointRounding.ToEven);

    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
    {
        if (DA.Opacity > 0 && !DA.Opacity.IsAlmostZero())
        {
            Color drawColor = Color * DA.Opacity * Source.Opacity;

            if (Tile)
            {
                // Draw the texture at its natural size, tiling it to fill the bounds
                int tileW = UnstretchedWidth;
                int tileH = UnstretchedHeight;
                if (tileW > 0 && tileH > 0)
                {
                    Rectangle fullSrc = Source.SourceRect ?? new Rectangle(0, 0, Source.Image.Width, Source.Image.Height);
                    for (int y = Bounds.Top; y < Bounds.Bottom; y += tileH)
                    {
                        for (int x = Bounds.Left; x < Bounds.Right; x += tileW)
                        {
                            int drawW = Math.Min(tileW, Bounds.Right - x);
                            int drawH = Math.Min(tileH, Bounds.Bottom - y);
                            Rectangle dest = new(x, y, drawW, drawH);
                            Rectangle src = drawW < tileW || drawH < tileH
                                ? new Rectangle(fullSrc.X, fullSrc.Y, Math.Min(drawW, fullSrc.Width), Math.Min(drawH, fullSrc.Height))
                                : fullSrc;
                            DA.Context.DrawTextureTo(Source.Image, src, dest.GetTranslated(DA.Offset), drawColor);
                        }
                    }
                }
                return;
            }

            Rectangle Destination = GetStretchedDestination(Bounds);
            DA.Context.DrawTextureTo(Source.Image, Source.SourceRect, Destination.GetTranslated(DA.Offset), drawColor);
        }
    }

    /// <summary>Destination rectangle of the (non-tiled) texture inside <paramref name="Bounds"/> according to <see cref="Stretch"/>.
    /// Shared by the rectangle path and the rounded path so both agree on placement.</summary>
    private Rectangle GetStretchedDestination(Rectangle Bounds)
    {
        double AspectRatio = UnstretchedAspectRatio;
        if (Stretch == Stretch.None)
        {
            return MGElement.ApplyAlignment(Bounds, HorizontalAlignment.Center, VerticalAlignment.Center, new Size(UnstretchedWidth, UnstretchedHeight));
        }
        else if (Stretch == Stretch.Uniform)
        {
            int AvailableWidth = Bounds.Width;
            int AvailableHeight = Bounds.Height;
            int ConsumedWidth = Math.Min(AvailableWidth, GetWidthByAspectRatio(AvailableHeight, AspectRatio));
            int ConsumedHeight = Math.Min(AvailableHeight, GetHeightByAspectRatio(AvailableWidth, AspectRatio));
            return MGElement.ApplyAlignment(Bounds, HorizontalAlignment.Center, VerticalAlignment.Center, new Size(ConsumedWidth, ConsumedHeight));
        }
        else if (Stretch == Stretch.UniformToFill)
        {
            int AvailableWidth = Bounds.Width;
            int AvailableHeight = Bounds.Height;
            int ConsumedWidth = Math.Max(AvailableWidth, GetWidthByAspectRatio(AvailableHeight, AspectRatio));
            int ConsumedHeight = Math.Max(AvailableHeight, GetHeightByAspectRatio(AvailableWidth, AspectRatio));
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

        IUIImageResource image = Source.Image;
        if (image == null || image.IsDisposed)
        {
            return;
        }

        Rectangle bounds = Shape.OuterBounds;
        Color drawColor = Color * DA.Opacity * Source.Opacity;
        Vector2 origin = DA.Offset.ToVector2();
        Rectangle fullSource = Source.SourceRect ?? new Rectangle(0, 0, image.Width, image.Height);
        IReadOnlyList<Vector2> vertices = Geometry.Vertices;

        if (Tile)
        {
            int tileW = UnstretchedWidth;
            int tileH = UnstretchedHeight;
            if (tileW <= 0 || tileH <= 0)
            {
                return;
            }

            bool isWholeTexture = fullSource.X == 0 && fullSource.Y == 0 && fullSource.Width == image.Width && fullSource.Height == image.Height;
            if (isWholeTexture)
            {
                Vector2[] tileUVs = new Vector2[vertices.Count];
                for (int i = 0; i < tileUVs.Length; i++)
                {
                    Vector2 vertex = vertices[i];
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

        Rectangle destination = GetStretchedDestination(bounds);
        if (destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }

        float inverseImageWidth = 1f / image.Width;
        float inverseImageHeight = 1f / image.Height;
        Vector2[] uvs = new Vector2[vertices.Count];
        for (int i = 0; i < uvs.Length; i++)
        {
            Vector2 vertex = vertices[i];
            float u = (vertex.X - destination.Left) / destination.Width;
            float v = (vertex.Y - destination.Top) / destination.Height;
            uvs[i] = new Vector2((fullSource.X + u * fullSource.Width) * inverseImageWidth, (fullSource.Y + v * fullSource.Height) * inverseImageHeight);
        }

        bool destinationCoversBounds = destination.Left <= bounds.Left && destination.Top <= bounds.Top && destination.Right >= bounds.Right && destination.Bottom >= bounds.Bottom;
        if (destinationCoversBounds)
        {
            DA.Context.FillTexturedRoundedRectangle(origin, Geometry, image, uvs, drawColor);
            return;
        }

        //  Stretch.Uniform / Stretch.None: only the destination rectangle is painted, like the rectangle path. The clip is consumed in screen space
        //  (see MGRatingControl); Element is null only for unit tests that draw the brush without an element, hence the translated fallback.
        Rectangle clipLayout = Rectangle.Intersect(destination, bounds);
        Rectangle clipScreen = Element != null
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
        IReadOnlyList<Vector2> outerContour = Geometry.OuterContour;
        if (outerContour.Count < 3)
        {
            return;
        }

        float inverseImageWidth = 1f / image.Width;
        float inverseImageHeight = 1f / image.Height;

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

        for (int y = bounds.Top; y < bounds.Bottom; y += tileH)
        {
            for (int x = bounds.Left; x < bounds.Right; x += tileW)
            {
                int drawW = Math.Min(tileW, bounds.Right - x);
                int drawH = Math.Min(tileH, bounds.Bottom - y);
                Rectangle dest = new(x, y, drawW, drawH);
                Rectangle src = drawW < tileW || drawH < tileH
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

                int baseIndex = batchVertices.Count;
                foreach (Vector2 vertex in clipped)
                {
                    batchVertices.Add(vertex);
                    batchUVs.Add(MGConvexPolygonClipper.InterpolateRectUV(vertex, dest, uvTopLeft, uvBottomRight));
                }
                MGConvexPolygonClipper.AppendFanTriangles(baseIndex, clipped.Count, batchIndices);
            }
        }

        Flush();
    }

    public IFillBrush Copy() => new MGTextureFillBrush(Source, Stretch, Color, Tile);
}