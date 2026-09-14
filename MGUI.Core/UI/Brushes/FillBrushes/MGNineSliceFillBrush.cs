using MGUI.Core.UI.Shapes;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Brushes.FillBrushes;

/// <summary>An <see cref="IFillBrush"/> that draws a nine-sliced (also called a nine-patch) texture to the destination bounds using a customizable margin to control how each patch scales to the bounds.<para/>
/// See also: <see href="https://en.wikipedia.org/wiki/9-slice_scaling"/></summary>
public sealed class MGNineSliceFillBrush : UIFreezableBrush, IFillBrush
{
    private Thickness _TargetMargin;
    public Thickness TargetMargin
    {
        get => _TargetMargin;
        set => SetProperty(ref _TargetMargin, value);
    }

    private MGTextureData _TopLeft;
    public MGTextureData TopLeft
    {
        get => _TopLeft;
        set => SetProperty(ref _TopLeft, value);
    }

    private MGTextureData _TopCenter;
    public MGTextureData TopCenter
    {
        get => _TopCenter;
        set => SetProperty(ref _TopCenter, value);
    }

    private MGTextureData _TopRight;
    public MGTextureData TopRight
    {
        get => _TopRight;
        set => SetProperty(ref _TopRight, value);
    }

    private MGTextureData _MiddleLeft;
    public MGTextureData MiddleLeft
    {
        get => _MiddleLeft;
        set => SetProperty(ref _MiddleLeft, value);
    }

    private MGTextureData _MiddleCenter;
    public MGTextureData MiddleCenter
    {
        get => _MiddleCenter;
        set => SetProperty(ref _MiddleCenter, value);
    }

    private MGTextureData _MiddleRight;
    public MGTextureData MiddleRight
    {
        get => _MiddleRight;
        set => SetProperty(ref _MiddleRight, value);
    }

    private MGTextureData _BottomLeft;
    public MGTextureData BottomLeft
    {
        get => _BottomLeft;
        set => SetProperty(ref _BottomLeft, value);
    }

    private MGTextureData _BottomCenter;
    public MGTextureData BottomCenter
    {
        get => _BottomCenter;
        set => SetProperty(ref _BottomCenter, value);
    }

    private MGTextureData _BottomRight;
    public MGTextureData BottomRight
    {
        get => _BottomRight;
        set => SetProperty(ref _BottomRight, value);
    }

    /// <param name="Source">The texture that will be divided up into 9 rectangular regions.</param>
    /// <param name="TargetMargin">Determines the size of each slice when rendering the texture to the destination bounds.<para/>
    /// EX: If margin.Left=10 and margin.Top=16, the top-left region of the source texture will be drawn to the topleft 10x16 pixels of the destination bounds whenever this brush is rendered.</param>
    /// <param name="SourceMargin">Optional. Determines how the source texture is divided up into 9 rectangular regions.<para/>
    /// If <see langword="null"/>, each region of the source texture is assumed to be equally-sized (and thus its dimensions should be an exact multiple of 3)</param>
    public MGNineSliceFillBrush(Thickness TargetMargin, MGTextureData Source, Thickness? SourceMargin = null)
    {
        _TargetMargin = TargetMargin;

        var Image = Source.Image;
        if (Image == null)
        {
            throw new ArgumentNullException(nameof(Source));
        }

        var Bounds = Source.SourceRect ?? new Rectangle(0, 0, Image.Width, Image.Height);

        //  Validate the source margin
        Thickness Margin;
        if (SourceMargin.HasValue)
        {
            if (SourceMargin.Value.Sides().Any(x => x <= 0))
            {
                throw new InvalidDataException($"Invalid {nameof(SourceMargin)}. All sides must have a value greater than zero. Actual value: {SourceMargin.Value}");
            }

            Margin = SourceMargin.Value;
        }
        else
        {
            if (Bounds.Width % 3 != 0 || Bounds.Height % 3 != 0)
            {
                throw new InvalidDataException($"Invalid input texture dimensions. " +
                                               $"The source texture must be evenly-divisible by 3 to calculate each of the 9 regions. Actual dimensions: {Bounds.Width}x{Bounds.Height}");
            }
            Margin = new(Bounds.Width / 3, Bounds.Height / 3);
        }

        var LeftColumnSize = Margin.Left;
        var RightColumnSize = Margin.Right;
        var CenterColumnSize = Bounds.Width - LeftColumnSize - RightColumnSize;

        var TopRowSize = Margin.Top;
        var BottomRowSize = Margin.Bottom;
        var CenterRowSize = Bounds.Height - TopRowSize - BottomRowSize;

        //  Compute the top row regions
        _TopLeft = new(Image, new(Bounds.Left, Bounds.Top, LeftColumnSize, TopRowSize), Source.Opacity, Source.RenderSizeOverride);
        _TopCenter = new(Image, new(Bounds.Left + LeftColumnSize, Bounds.Top, CenterColumnSize, TopRowSize), Source.Opacity, Source.RenderSizeOverride);
        _TopRight = new(Image, new(Bounds.Left + LeftColumnSize + CenterColumnSize, Bounds.Top, RightColumnSize, TopRowSize), Source.Opacity, Source.RenderSizeOverride);

        //  Compute the center row regions
        _MiddleLeft = new(Image, new(Bounds.Left, Bounds.Top + TopRowSize, LeftColumnSize, CenterRowSize), Source.Opacity, Source.RenderSizeOverride);
        _MiddleCenter = new(Image, new(Bounds.Left + LeftColumnSize, Bounds.Top + TopRowSize, CenterColumnSize, CenterRowSize), Source.Opacity, Source.RenderSizeOverride);
        _MiddleRight = new(Image, new(Bounds.Left + LeftColumnSize + CenterColumnSize, Bounds.Top + TopRowSize, RightColumnSize, CenterRowSize), Source.Opacity, Source.RenderSizeOverride);

        //  Compute the bottom row regions
        _BottomLeft = new(Image, new(Bounds.Left, Bounds.Top + TopRowSize + CenterRowSize, LeftColumnSize, BottomRowSize), Source.Opacity, Source.RenderSizeOverride);
        _BottomCenter = new(Image, new(Bounds.Left + LeftColumnSize, Bounds.Top + TopRowSize + CenterRowSize, CenterColumnSize, BottomRowSize), Source.Opacity, Source.RenderSizeOverride);
        _BottomRight = new(Image, new(Bounds.Left + LeftColumnSize + CenterColumnSize, Bounds.Top + TopRowSize + CenterRowSize, RightColumnSize, BottomRowSize), Source.Opacity, Source.RenderSizeOverride);
    }

    /// <param name="TargetMargin">Determines the size of each slice when rendering the texture to the destination bounds.<para/>
    /// EX: If margin.Left=10 and margin.Top=16, the top-left region of the source texture will be drawn to the topleft 10x16 pixels of the destination bounds whenever this brush is rendered.</param>
    public MGNineSliceFillBrush(Thickness TargetMargin,
        MGTextureData TopLeft, MGTextureData TopCenter, MGTextureData TopRight,
        MGTextureData MiddleLeft, MGTextureData MiddleCenter, MGTextureData MiddleRight,
        MGTextureData BottomLeft, MGTextureData BottomCenter, MGTextureData BottomRight)
    {
        _TargetMargin = TargetMargin;

        _TopLeft = TopLeft;
        _TopCenter = TopCenter;
        _TopRight = TopRight;
        _MiddleLeft = MiddleLeft;
        _MiddleCenter = MiddleCenter;
        _MiddleRight = MiddleRight;
        _BottomLeft = BottomLeft;
        _BottomCenter = BottomCenter;
        _BottomRight = BottomRight;
    }

    public IFillBrush Copy() => new MGNineSliceFillBrush(TargetMargin, TopLeft, TopCenter, TopRight, MiddleLeft, MiddleCenter, MiddleRight, BottomLeft, BottomCenter, BottomRight);

    /// <summary>Value equality (ADR-0009, W3): two nine-slice brushes are equal when <see cref="TargetMargin"/> and all nine patches
    /// match (each patch a record struct: compares its <see cref="MGTextureData.Image"/> by reference, the same underlying texture, plus
    /// its other fields by value), regardless of frozen state or instance identity.</summary>
    public bool ValueEquals(IFillBrush other) => other is MGNineSliceFillBrush n
        && n.TargetMargin.Equals(TargetMargin)
        && n.TopLeft.Equals(TopLeft) && n.TopCenter.Equals(TopCenter) && n.TopRight.Equals(TopRight)
        && n.MiddleLeft.Equals(MiddleLeft) && n.MiddleCenter.Equals(MiddleCenter) && n.MiddleRight.Equals(MiddleRight)
        && n.BottomLeft.Equals(BottomLeft) && n.BottomCenter.Equals(BottomCenter) && n.BottomRight.Equals(BottomRight);

    /// <summary>Decision taken during delivery (ADR-0009, W3): see <see cref="MGSolidFillBrush.Equals(object)"/> for the rationale
    /// (by-value <see cref="object.Equals(object)"/>/<see cref="GetHashCode"/>, applied consistently to every converted fill brush).</summary>
    public override bool Equals(object obj) => ValueEquals(obj as IFillBrush);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TargetMargin);
        hash.Add(TopLeft);
        hash.Add(TopCenter);
        hash.Add(TopRight);
        hash.Add(MiddleLeft);
        hash.Add(MiddleCenter);
        hash.Add(MiddleRight);
        hash.Add(BottomLeft);
        hash.Add(BottomCenter);
        hash.Add(BottomRight);
        return hash.ToHashCode();
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
    {
        IUIRenderContext DT = DA.DT;

        Bounds = Bounds.GetTranslated(DA.Offset);

        var LeftColumnSize = TargetMargin.Left;
        var RightColumnSize = TargetMargin.Right;
        var CenterColumnSize = Bounds.Width - LeftColumnSize - RightColumnSize;

        var TopRowSize = TargetMargin.Top;
        var BottomRowSize = TargetMargin.Bottom;
        var CenterRowSize = Bounds.Height - TopRowSize - BottomRowSize;

        //  Draw the top row
        if (TopRowSize > 0)
        {
            if (LeftColumnSize > 0)
            {
                TopLeft.Draw(DT, new Rectangle(Bounds.Left, Bounds.Top, LeftColumnSize, TopRowSize), null, DA.Opacity);
            }

            if (CenterColumnSize > 0)
            {
                TopCenter.Draw(DT, new Rectangle(Bounds.Left + LeftColumnSize, Bounds.Top, CenterColumnSize, TopRowSize), null, DA.Opacity);
            }

            if (RightColumnSize > 0)
            {
                TopRight.Draw(DT, new Rectangle(Bounds.Left + LeftColumnSize + CenterColumnSize, Bounds.Top, RightColumnSize, TopRowSize), null, DA.Opacity);
            }
        }

        //  Draw the center row
        if (CenterRowSize > 0)
        {
            if (LeftColumnSize > 0)
            {
                MiddleLeft.Draw(DT, new Rectangle(Bounds.Left, Bounds.Top + TopRowSize, LeftColumnSize, CenterRowSize), null, DA.Opacity);
            }

            if (CenterColumnSize > 0)
            {
#if NEVER   // trying to tile the texture but LinearWrap isn't working unless I draw the entire texture, using the destination's width/height as the SourceRect
            // But the problem is I only want to tile the center region of the input texture, not the entire thing...
                    using (DT.SetDrawSettingsTemporary(DT.CurrentSettings with { SamplerType = SamplerType.LinearWrap }))
                    {
                        Rectangle Destination = new Rectangle(Bounds.Left + LeftColumnSize, Bounds.Top + TopRowSize, CenterColumnSize, CenterRowSize);

                        Rectangle ActualSourceRect = MiddleCenter.SourceRect.Value;
                        Rectangle? SourceRect = new Rectangle(ActualSourceRect.X, ActualSourceRect.Y, Destination.Width, Destination.Height); // MiddleCenter.SourceRect;
                        DT.DrawTextureTo(MiddleCenter.Image, SourceRect, Destination, Color.White * MiddleCenter.Opacity * DA.Opacity);
                    }
#else
                MiddleCenter.Draw(DT, new Rectangle(Bounds.Left + LeftColumnSize, Bounds.Top + TopRowSize, CenterColumnSize, CenterRowSize), null, DA.Opacity);
#endif
            }
            if (RightColumnSize > 0)
            {
                MiddleRight.Draw(DT, new Rectangle(Bounds.Left + LeftColumnSize + CenterColumnSize, Bounds.Top + TopRowSize, RightColumnSize, CenterRowSize), null, DA.Opacity);
            }
        }

        //  Draw the bottom row
        if (BottomRowSize > 0)
        {
            if (LeftColumnSize > 0)
            {
                BottomLeft.Draw(DT, new Rectangle(Bounds.Left, Bounds.Top + TopRowSize + CenterRowSize, LeftColumnSize, BottomRowSize), null, DA.Opacity);
            }

            if (CenterColumnSize > 0)
            {
                BottomCenter.Draw(DT, new Rectangle(Bounds.Left + LeftColumnSize, Bounds.Top + TopRowSize + CenterRowSize, CenterColumnSize, BottomRowSize), null, DA.Opacity);
            }

            if (RightColumnSize > 0)
            {
                BottomRight.Draw(DT, new Rectangle(Bounds.Left + LeftColumnSize + CenterColumnSize, Bounds.Top + TopRowSize + CenterRowSize, RightColumnSize, BottomRowSize), null, DA.Opacity);
            }
        }
    }

    /// <summary>Rounded path: keeps the same 9 destination rectangles as the rectangle path (split from <see cref="Shape"/>'s <see cref="MGBoxShape.OuterBounds"/>
    /// by <see cref="TargetMargin"/>), but clips each one against <see cref="MGBoxGeometry.OuterContour"/> before drawing it, so a patch that falls
    /// under a rounded corner is cut to the silhouette instead of overhanging it. Each patch's UVs come from its own source rectangle
    /// (<see cref="MGConvexPolygonClipper.InterpolateRectUV"/> against the *destination* rectangle), so a pixel on a straight edge - unclipped -
    /// samples exactly what the rectangle path would draw there. Patches that share an image and color mask are batched into a single
    /// <see cref="IUIDrawContext.DrawTexturedTriangleList"/> call, split only when a batch would exceed <see cref="short.MaxValue"/> vertices.</summary>
    public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
    {
        if (Geometry.UsesRectangleFastPath)
        {
            Draw(DA, Element, Shape.OuterBounds);
            return;
        }

        var bounds = Shape.OuterBounds;

        var LeftColumnSize = TargetMargin.Left;
        var RightColumnSize = TargetMargin.Right;
        var CenterColumnSize = bounds.Width - LeftColumnSize - RightColumnSize;

        var TopRowSize = TargetMargin.Top;
        var BottomRowSize = TargetMargin.Bottom;
        var CenterRowSize = bounds.Height - TopRowSize - BottomRowSize;

        var origin = DA.Offset.ToVector2();
        var outerContour = Geometry.OuterContour;

        List<Vector2> quadPolygon = new(4);
        List<Vector2> clipped = new(8);
        List<Vector2> scratch = new(8);
        List<PatchBatch> batches = new();

        //  Clips one patch's destination rectangle against the rounded outer silhouette, fan-triangulates the surviving piece (empty when the
        //  patch falls entirely outside the silhouette), and appends it to a batch sharing its image and color mask.
        void EmitPatch(MGTextureData patch, Rectangle destination)
        {
            if (destination.Width <= 0 || destination.Height <= 0)
            {
                return;
            }

            var image = patch.Image;
            if (image == null || image.IsDisposed)
            {
                return;
            }

            quadPolygon.Clear();
            quadPolygon.Add(new Vector2(destination.Left, destination.Top));
            quadPolygon.Add(new Vector2(destination.Right, destination.Top));
            quadPolygon.Add(new Vector2(destination.Right, destination.Bottom));
            quadPolygon.Add(new Vector2(destination.Left, destination.Bottom));

            MGConvexPolygonClipper.ClipToConvexPolygon(quadPolygon, outerContour, clipped, scratch);
            if (clipped.Count < 3)
            {
                return;
            }

            var color = Color.White * patch.Opacity * DA.Opacity;

            var batch = batches.Find(b => ReferenceEquals(b.Image, image) && b.Color == color && b.Vertices.Count + clipped.Count <= short.MaxValue);
            if (batch == null)
            {
                batch = new PatchBatch(image, color);
                batches.Add(batch);
            }

            var sourceRect = patch.SourceRect ?? new Rectangle(0, 0, image.Width, image.Height);
            var inverseImageWidth = 1f / image.Width;
            var inverseImageHeight = 1f / image.Height;
            Vector2 uvTopLeft = new(sourceRect.Left * inverseImageWidth, sourceRect.Top * inverseImageHeight);
            Vector2 uvBottomRight = new(sourceRect.Right * inverseImageWidth, sourceRect.Bottom * inverseImageHeight);

            var baseIndex = batch.Vertices.Count;
            foreach (var vertex in clipped)
            {
                batch.Vertices.Add(vertex);
                batch.TextureCoordinates.Add(MGConvexPolygonClipper.InterpolateRectUV(vertex, destination, uvTopLeft, uvBottomRight));
            }
            MGConvexPolygonClipper.AppendFanTriangles(baseIndex, clipped.Count, batch.Indices);
        }

        //  Top row
        if (TopRowSize > 0)
        {
            if (LeftColumnSize > 0)
            {
                EmitPatch(TopLeft, new Rectangle(bounds.Left, bounds.Top, LeftColumnSize, TopRowSize));
            }
            if (CenterColumnSize > 0)
            {
                EmitPatch(TopCenter, new Rectangle(bounds.Left + LeftColumnSize, bounds.Top, CenterColumnSize, TopRowSize));
            }
            if (RightColumnSize > 0)
            {
                EmitPatch(TopRight, new Rectangle(bounds.Left + LeftColumnSize + CenterColumnSize, bounds.Top, RightColumnSize, TopRowSize));
            }
        }

        //  Center row
        if (CenterRowSize > 0)
        {
            if (LeftColumnSize > 0)
            {
                EmitPatch(MiddleLeft, new Rectangle(bounds.Left, bounds.Top + TopRowSize, LeftColumnSize, CenterRowSize));
            }
            if (CenterColumnSize > 0)
            {
                EmitPatch(MiddleCenter, new Rectangle(bounds.Left + LeftColumnSize, bounds.Top + TopRowSize, CenterColumnSize, CenterRowSize));
            }
            if (RightColumnSize > 0)
            {
                EmitPatch(MiddleRight, new Rectangle(bounds.Left + LeftColumnSize + CenterColumnSize, bounds.Top + TopRowSize, RightColumnSize, CenterRowSize));
            }
        }

        //  Bottom row
        if (BottomRowSize > 0)
        {
            if (LeftColumnSize > 0)
            {
                EmitPatch(BottomLeft, new Rectangle(bounds.Left, bounds.Top + TopRowSize + CenterRowSize, LeftColumnSize, BottomRowSize));
            }
            if (CenterColumnSize > 0)
            {
                EmitPatch(BottomCenter, new Rectangle(bounds.Left + LeftColumnSize, bounds.Top + TopRowSize + CenterRowSize, CenterColumnSize, BottomRowSize));
            }
            if (RightColumnSize > 0)
            {
                EmitPatch(BottomRight, new Rectangle(bounds.Left + LeftColumnSize + CenterColumnSize, bounds.Top + TopRowSize + CenterRowSize, RightColumnSize, BottomRowSize));
            }
        }

        foreach (var batch in batches)
        {
            DA.Context.DrawTexturedTriangleList(origin, batch.Image, batch.Vertices, batch.TextureCoordinates, batch.Indices, batch.Color);
        }
    }

    private sealed class PatchBatch
    {
        public PatchBatch(IUIImageResource Image, Color Color)
        {
            this.Image = Image;
            this.Color = Color;
        }

        public IUIImageResource Image { get; }
        public Color Color { get; }
        public List<Vector2> Vertices { get; } = new();
        public List<Vector2> TextureCoordinates { get; } = new();
        public List<int> Indices { get; } = new();
    }
}