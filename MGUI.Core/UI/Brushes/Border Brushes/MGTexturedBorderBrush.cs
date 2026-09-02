using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Linq;
using MGUI.Core.UI.Shapes;
using System.Collections.Generic;

namespace MGUI.Core.UI.Brushes.Border_Brushes
{
    public enum Corner
    {
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft
    }

    public enum Edge
    {
        Left,
        Top,
        Right,
        Bottom
    }

    public readonly record struct TextureTransforms(EdgeTransforms EdgeTransforms = new(), CornerTransforms CornerTransforms = new())
    {
        private static readonly float PI = (float)Math.PI;

        public static TextureTransforms CreateStandardRotated(Edge EdgeBasis = Edge.Left, Corner CornerBasis = Corner.TopLeft)
        {
            EdgeTransforms edgeTransforms = EdgeBasis switch
            {
                Edge.Left => new(0, PI / 2.0f, PI, PI / 2.0f * 3),
                Edge.Top => new(PI / 2.0f * 3, 0, PI / 2.0f, PI),
                Edge.Right => new(PI, PI / 2.0f * 3, 0, PI / 2.0f),
                Edge.Bottom => new(PI / 2.0f, PI, PI / 2.0f * 3, 0),
                _ => throw new NotImplementedException($"Unrecognized {nameof(Edge)}: {EdgeBasis}")
            };

            CornerTransforms cornerTransforms = CornerBasis switch
            {
                Corner.TopLeft => new(0, PI / 2.0f, PI, PI / 2.0f * 3),
                Corner.TopRight => new(PI / 2.0f * 3, 0, PI / 2.0f, PI),
                Corner.BottomRight => new(PI, PI / 2.0f * 3, 0, PI / 2.0f),
                Corner.BottomLeft => new(PI / 2.0f, PI, PI / 2.0f * 3, 0),
                _ => throw new NotImplementedException($"Unrecognized {nameof(Corner)}: {CornerBasis}")
            };

            return new TextureTransforms(edgeTransforms, cornerTransforms);
        }
    }

    public readonly record struct EdgeTransforms(
        float LeftRotation = 0, float TopRotation = 0, float RightRotation = 0, float BottomRotation = 0,
        SpriteEffects LeftReflections = SpriteEffects.None, SpriteEffects TopReflections = SpriteEffects.None,
        SpriteEffects RightReflections = SpriteEffects.None, SpriteEffects BottomReflections = SpriteEffects.None)
    {
        public bool HasLeftRotation => !LeftRotation.IsAlmostZero();
        public bool HasTopRotation => !TopRotation.IsAlmostZero();
        public bool HasRightRotation => !RightRotation.IsAlmostZero();
        public bool HasBottomRotation => !BottomRotation.IsAlmostZero();
    }

    public readonly record struct CornerTransforms(
        float TopLeftRotation = 0, float TopRightRotation = 0, float BottomRightRotation = 0, float BottomLeftRotation = 0,
        SpriteEffects TopLeftReflections = SpriteEffects.None, SpriteEffects TopRightReflections = SpriteEffects.None,
        SpriteEffects BottomRightReflections = SpriteEffects.None, SpriteEffects BottomLeftReflections = SpriteEffects.None)
    {
        public bool HasTopLeftRotation => !TopLeftRotation.IsAlmostZero();
        public bool HasTopRightRotation => !TopRightRotation.IsAlmostZero();
        public bool HasBottomRightRotation => !BottomRightRotation.IsAlmostZero();
        public bool HasBottomLeftRotation => !BottomLeftRotation.IsAlmostZero();
    }

    public readonly struct MGTexturedBorderBrush : IBorderBrush
    {
        private static UIDrawFlip ToDrawFlip(SpriteEffects effects)
        {
            UIDrawFlip result = UIDrawFlip.None;

            if ((effects & SpriteEffects.FlipHorizontally) != 0)
            {
                result |= UIDrawFlip.Horizontal;
            }

            if ((effects & SpriteEffects.FlipVertically) != 0)
            {
                result |= UIDrawFlip.Vertical;
            }

            return result;
        }

        public readonly MGTextureData EdgeTexture;
        public readonly Color EdgeColor;
        public readonly MGTextureData CornerTexture;
        public readonly Color CornerColor;
        public readonly float Opacity;
        public readonly TextureTransforms Transforms;

        public MGTexturedBorderBrush()
        {
            EdgeTexture = default;
            EdgeColor = Color.White;
            CornerTexture = default;
            CornerColor = Color.White;
            Opacity = 1.0f;
            Transforms = new();
        }

        public MGTexturedBorderBrush(MGDesktop Desktop, string EdgeTextureName, string CornerTextureName, Color? EdgeColor = null, Color? CornerColor = null, TextureTransforms? Transforms = null, float Opacity = 1.0f)
            : this(Desktop.Resources.Textures[EdgeTextureName], EdgeColor, Desktop.Resources.Textures[CornerTextureName], CornerColor, Transforms, Opacity) { }

        public MGTexturedBorderBrush(IUIImageResource EdgeTexture, IUIImageResource CornerTexture, TextureTransforms? Transforms = null, float Opacity = 1.0f)
            : this(new MGTextureData(EdgeTexture), null, new MGTextureData(CornerTexture), null, Transforms, Opacity) { }

        public MGTexturedBorderBrush(MGTextureData EdgeTexture, Color? EdgeColor, MGTextureData CornerTexture, Color? CornerColor,
            TextureTransforms? Transforms = null, float Opacity = 1.0f)
        {
            this.EdgeTexture = EdgeTexture;
            this.EdgeColor = EdgeColor ?? Color.White;
            this.CornerTexture = CornerTexture;
            this.CornerColor = CornerColor ?? Color.White;
            this.Opacity = Opacity;
            this.Transforms = Transforms ?? new();
        }

        public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds, Thickness BT)
        {
            IUIDrawContext drawContext = DA.DT;
            float opacity = DA.Opacity * this.Opacity;
            IUIImageResource edgeImage = EdgeTexture.Image;
            IUIImageResource cornerImage = CornerTexture.Image;

            if (edgeImage?.IsDisposed == false)
            {
                Bounds = Bounds.GetTranslated(DA.Offset);

                EdgeTransforms edgeTransforms = Transforms.EdgeTransforms;
                Color edgeColor = this.EdgeColor * opacity * EdgeTexture.Opacity;
                int sourceWidth = EdgeTexture.RenderSize.Width;
                int sourceHeight = EdgeTexture.RenderSize.Height;
                Vector2 origin = new(sourceWidth / 2f, sourceHeight / 2f);
                const float scaleOffset = 0.0001f;

                Rectangle leftBounds = new(Bounds.Left, Bounds.Top + BT.Top, BT.Left, Bounds.Height - BT.Height);
                if (!edgeTransforms.HasLeftRotation)
                {
                    drawContext.DrawTextureTo(edgeImage, EdgeTexture.SourceRect, leftBounds, edgeColor, Vector2.Zero, 0, 0, ToDrawFlip(edgeTransforms.LeftReflections));
                }
                else
                {
                    Rectangle rotatedLeftBounds = leftBounds.CreateTransformed(Matrix.CreateRotationZ(edgeTransforms.LeftRotation));
                    Vector2 scale = new(Math.Abs(rotatedLeftBounds.Width) / (float)sourceWidth + scaleOffset, Math.Abs(rotatedLeftBounds.Height) / (float)sourceHeight + scaleOffset);
                    drawContext.DrawTextureAt(edgeImage, EdgeTexture.SourceRect, leftBounds.Center.ToVector2(), edgeColor, origin, edgeTransforms.LeftRotation, scale.X, scale.Y, 0, ToDrawFlip(edgeTransforms.LeftReflections));
                }

                Rectangle topBounds = new(Bounds.Left + BT.Left, Bounds.Top, Bounds.Width - BT.Width, BT.Top);
                if (!edgeTransforms.HasTopRotation)
                {
                    drawContext.DrawTextureTo(edgeImage, EdgeTexture.SourceRect, topBounds, edgeColor, Vector2.Zero, 0, 0, ToDrawFlip(edgeTransforms.TopReflections));
                }
                else
                {
                    Rectangle rotatedTopBounds = topBounds.CreateTransformed(Matrix.CreateRotationZ(edgeTransforms.TopRotation));
                    Vector2 scale = new(Math.Abs(rotatedTopBounds.Width) / (float)sourceWidth + scaleOffset, Math.Abs(rotatedTopBounds.Height) / (float)sourceHeight + scaleOffset);
                    drawContext.DrawTextureAt(edgeImage, EdgeTexture.SourceRect, topBounds.Center.ToVector2(), edgeColor, origin, edgeTransforms.TopRotation, scale.X, scale.Y, 0, ToDrawFlip(edgeTransforms.TopReflections));
                }

                Rectangle rightBounds = new(Bounds.Right - BT.Right, Bounds.Top + BT.Top, BT.Right, Bounds.Height - BT.Height);
                if (!edgeTransforms.HasRightRotation)
                {
                    drawContext.DrawTextureTo(edgeImage, EdgeTexture.SourceRect, rightBounds, edgeColor, Vector2.Zero, 0, 0, ToDrawFlip(edgeTransforms.RightReflections));
                }
                else
                {
                    Rectangle rotatedRightBounds = rightBounds.CreateTransformed(Matrix.CreateRotationZ(edgeTransforms.RightRotation));
                    Vector2 scale = new(Math.Abs(rotatedRightBounds.Width) / (float)sourceWidth + scaleOffset, Math.Abs(rotatedRightBounds.Height) / (float)sourceHeight + scaleOffset);
                    drawContext.DrawTextureAt(edgeImage, EdgeTexture.SourceRect, rightBounds.Center.ToVector2(), edgeColor, origin, edgeTransforms.RightRotation, scale.X, scale.Y, 0, ToDrawFlip(edgeTransforms.RightReflections));
                }

                Rectangle bottomBounds = new(Bounds.Left + BT.Left, Bounds.Bottom - BT.Bottom, Bounds.Width - BT.Width, BT.Bottom);
                if (!edgeTransforms.HasBottomRotation)
                {
                    drawContext.DrawTextureTo(edgeImage, EdgeTexture.SourceRect, bottomBounds, edgeColor, Vector2.Zero, 0, 0, ToDrawFlip(edgeTransforms.BottomReflections));
                }
                else
                {
                    Rectangle rotatedBottomBounds = bottomBounds.CreateTransformed(Matrix.CreateRotationZ(edgeTransforms.BottomRotation));
                    Vector2 scale = new(Math.Abs(rotatedBottomBounds.Width) / (float)sourceWidth + scaleOffset, Math.Abs(rotatedBottomBounds.Height) / (float)sourceHeight + scaleOffset);
                    drawContext.DrawTextureAt(edgeImage, EdgeTexture.SourceRect, bottomBounds.Center.ToVector2(), edgeColor, origin, edgeTransforms.BottomRotation, scale.X, scale.Y, 0, ToDrawFlip(edgeTransforms.BottomReflections));
                }
            }

            if (cornerImage?.IsDisposed == false)
            {
                CornerTransforms cornerTransforms = Transforms.CornerTransforms;
                Color cornerColor = this.CornerColor * opacity * CornerTexture.Opacity;
                int sourceWidth = CornerTexture.RenderSize.Width;
                int sourceHeight = CornerTexture.RenderSize.Height;
                Vector2 origin = new(sourceWidth / 2f, sourceHeight / 2f);
                bool isUniformThickness = BT.Sides().All(x => x == BT.Left);

                if (isUniformThickness)
                {
                    Rectangle topLeftBounds = new Rectangle(Bounds.Left, Bounds.Top, BT.Left, BT.Top).GetTranslated(BT.Left / 2, BT.Top / 2);
                    drawContext.DrawTextureTo(cornerImage, CornerTexture.SourceRect, topLeftBounds, cornerColor, origin, cornerTransforms.TopLeftRotation, 0, ToDrawFlip(cornerTransforms.TopLeftReflections));

                    Rectangle topRightBounds = new Rectangle(Bounds.Right - BT.Right, Bounds.Top, BT.Right, BT.Top).GetTranslated(BT.Right / 2, BT.Top / 2);
                    drawContext.DrawTextureTo(cornerImage, CornerTexture.SourceRect, topRightBounds, cornerColor, origin, cornerTransforms.TopRightRotation, 0, ToDrawFlip(cornerTransforms.TopRightReflections));

                    Rectangle bottomRightBounds = new Rectangle(Bounds.Right - BT.Right, Bounds.Bottom - BT.Bottom, BT.Right, BT.Bottom).GetTranslated(BT.Right / 2, BT.Bottom / 2);
                    drawContext.DrawTextureTo(cornerImage, CornerTexture.SourceRect, bottomRightBounds, cornerColor, origin, cornerTransforms.BottomRightRotation, 0, ToDrawFlip(cornerTransforms.BottomRightReflections));

                    Rectangle bottomLeftBounds = new Rectangle(Bounds.Left, Bounds.Bottom - BT.Bottom, BT.Left, BT.Bottom).GetTranslated(BT.Left / 2, BT.Bottom / 2);
                    drawContext.DrawTextureTo(cornerImage, CornerTexture.SourceRect, bottomLeftBounds, cornerColor, origin, cornerTransforms.BottomLeftRotation, 0, ToDrawFlip(cornerTransforms.BottomLeftReflections));
                }
                else
                {
                    const float scaleOffset = 0.0001f;

                    Rectangle topLeftBounds = new(Bounds.Left, Bounds.Top, BT.Left, BT.Top);
                    if (!cornerTransforms.HasTopLeftRotation)
                    {
                        drawContext.DrawTextureTo(cornerImage, CornerTexture.SourceRect, topLeftBounds, cornerColor, Vector2.Zero, 0, 0, ToDrawFlip(cornerTransforms.TopLeftReflections));
                    }
                    else
                    {
                        Rectangle rotatedTopLeftBounds = topLeftBounds.CreateTransformed(Matrix.CreateRotationZ(cornerTransforms.TopLeftRotation));
                        Vector2 scale = new(Math.Abs(rotatedTopLeftBounds.Width) / (float)sourceWidth + scaleOffset, Math.Abs(rotatedTopLeftBounds.Height) / (float)sourceHeight + scaleOffset);
                        drawContext.DrawTextureAt(cornerImage, CornerTexture.SourceRect, topLeftBounds.Center.ToVector2(), cornerColor, origin, cornerTransforms.TopLeftRotation, scale.X, scale.Y, 0, ToDrawFlip(cornerTransforms.TopLeftReflections));
                    }

                    Rectangle topRightBounds = new(Bounds.Right - BT.Right, Bounds.Top, BT.Right, BT.Top);
                    if (!cornerTransforms.HasTopRightRotation)
                    {
                        drawContext.DrawTextureTo(cornerImage, CornerTexture.SourceRect, topRightBounds, cornerColor, Vector2.Zero, 0, 0, ToDrawFlip(cornerTransforms.TopRightReflections));
                    }
                    else
                    {
                        Rectangle rotatedTopRightBounds = topRightBounds.CreateTransformed(Matrix.CreateRotationZ(cornerTransforms.TopRightRotation));
                        Vector2 scale = new(Math.Abs(rotatedTopRightBounds.Width) / (float)sourceWidth + scaleOffset, Math.Abs(rotatedTopRightBounds.Height) / (float)sourceHeight + scaleOffset);
                        drawContext.DrawTextureAt(cornerImage, CornerTexture.SourceRect, topRightBounds.Center.ToVector2(), cornerColor, origin, cornerTransforms.TopRightRotation, scale.X, scale.Y, 0, ToDrawFlip(cornerTransforms.TopRightReflections));
                    }

                    Rectangle bottomRightBounds = new(Bounds.Right - BT.Right, Bounds.Bottom - BT.Bottom, BT.Right, BT.Bottom);
                    if (!cornerTransforms.HasBottomRightRotation)
                    {
                        drawContext.DrawTextureTo(cornerImage, CornerTexture.SourceRect, bottomRightBounds, cornerColor, Vector2.Zero, 0, 0, ToDrawFlip(cornerTransforms.BottomRightReflections));
                    }
                    else
                    {
                        Rectangle rotatedBottomRightBounds = bottomRightBounds.CreateTransformed(Matrix.CreateRotationZ(cornerTransforms.BottomRightRotation));
                        Vector2 scale = new(Math.Abs(rotatedBottomRightBounds.Width) / (float)sourceWidth + scaleOffset, Math.Abs(rotatedBottomRightBounds.Height) / (float)sourceHeight + scaleOffset);
                        drawContext.DrawTextureAt(cornerImage, CornerTexture.SourceRect, bottomRightBounds.Center.ToVector2(), cornerColor, origin, cornerTransforms.BottomRightRotation, scale.X, scale.Y, 0, ToDrawFlip(cornerTransforms.BottomRightReflections));
                    }

                    Rectangle bottomLeftBounds = new(Bounds.Left, Bounds.Bottom - BT.Bottom, BT.Left, BT.Bottom);
                    if (!cornerTransforms.HasBottomLeftRotation)
                    {
                        drawContext.DrawTextureTo(cornerImage, CornerTexture.SourceRect, bottomLeftBounds, cornerColor, Vector2.Zero, 0, 0, ToDrawFlip(cornerTransforms.BottomLeftReflections));
                    }
                    else
                    {
                        Rectangle rotatedBottomLeftBounds = bottomLeftBounds.CreateTransformed(Matrix.CreateRotationZ(cornerTransforms.BottomLeftRotation));
                        Vector2 scale = new(Math.Abs(rotatedBottomLeftBounds.Width) / (float)sourceWidth + scaleOffset, Math.Abs(rotatedBottomLeftBounds.Height) / (float)sourceHeight + scaleOffset);
                        drawContext.DrawTextureAt(cornerImage, CornerTexture.SourceRect, bottomLeftBounds.Center.ToVector2(), cornerColor, origin, cornerTransforms.BottomLeftRotation, scale.X, scale.Y, 0, ToDrawFlip(cornerTransforms.BottomLeftReflections));
                    }
                }
            }
        }

        /// <summary>Rounded path: keeps the texture layout of the rectangle path (corner texture on the thickness-sized corner blocks, edge textures on
        /// the edges between them) and lets the rounded ring cut that layout, using <see cref="MGBoxGeometry.OuterContour"/>,
        /// <see cref="MGBoxGeometry.InnerContour"/> and <see cref="MGBoxGeometry.BorderRingIndices"/>.<para/>
        /// Rules (the geometry is never rebuilt here): ring quad i lies between outer contour vertices i and i+1; every quad is clipped against each
        /// layout rectangle of the rectangle path (a quad may contribute a piece to several regions), the pieces are fan-triangulated and emitted per
        /// region, so the seams follow the layout lines rather than the ring quads. Rotations and reflections of <see cref="Transforms"/> are applied
        /// around each rectangle's centre exactly like the rectangle path fits the rotated sprite. The part of the ring deeper than the thickness inside
        /// a corner square (when the radius exceeds the thickness) belongs to no layout rectangle: it is clipped separately and painted with the corner
        /// texture using clamped coordinates, so the pieces partition the ring exactly, without hole or overlap.<para/>
        /// Falls back to the rectangle path, on purpose and documented (Docs/Tasks/drawing-tasks.md, Tache 4), when the geometry uses the rectangle fast
        /// path or when it has no ring mesh (a border thickness consuming the whole box leaves no inner contour, see MGBoxGeometryBuilder.BuildBorderRingIndices).</summary>
        public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
        {
            Thickness thickness = Shape.NormalizedBorderThickness;
            if (Geometry.UsesRectangleFastPath || !Geometry.HasBorderRingMesh || thickness.IsEmpty())
            {
                //  Rectangle fast path (identical legacy rendering), or documented fallback when the ring mesh is empty (Tache 4).
                Draw(DA, Element, Shape.OuterBounds, thickness);
                return;
            }

            IUIImageResource edgeImage = EdgeTexture.Image;
            IUIImageResource cornerImage = CornerTexture.Image;
            bool drawEdges = edgeImage?.IsDisposed == false;
            bool drawCorners = cornerImage?.IsDisposed == false;
            if (!drawEdges && !drawCorners)
            {
                return;
            }

            float opacity = DA.Opacity * this.Opacity;
            Rectangle bounds = Shape.OuterBounds;
            IReadOnlyList<Vector2> vertices = Geometry.Vertices;
            IReadOnlyList<int> ringIndices = Geometry.BorderRingIndices;
            int outerCount = Geometry.OuterContourVertexCount;
            int quadCount = ringIndices.Count / 6;
            if (outerCount < 2 || quadCount == 0)
            {
                return;
            }

            MGCornerRadius radius = Shape.NormalizedCornerRadius;
            Vector2 origin = DA.Offset.ToVector2();
            List<Vector2> quadPolygon = new(4);
            List<Vector2> clipped = new(8);
            List<Vector2> scratch = new(8);
            List<Vector2> regionVertices = new();
            List<Vector2> regionUVs = new();
            List<int> regionIndices = new();

            if (drawCorners)
            {
                Color cornerColor = this.CornerColor * opacity * CornerTexture.Opacity;
                CornerTransforms corners = Transforms.CornerTransforms;
                //  Same corner blocks as the rectangle path (see Draw(..., Rectangle, Thickness)); the ring is clipped against them.
                //  The part of the ring that lies deeper than the thickness inside a corner square belongs to no layout rectangle:
                //  it is painted with the corner texture too (clamped coordinates), so the ring never shows a hole.
                EmitRegion(DA, new RectangleF(bounds.Left, bounds.Top, thickness.Left, thickness.Top),
                    new RectangleF(bounds.Left + thickness.Left, bounds.Top + thickness.Top, radius.TopLeft - thickness.Left, radius.TopLeft - thickness.Top),
                    corners.TopLeftRotation, corners.TopLeftReflections, CornerTexture, cornerImage, cornerColor);
                EmitRegion(DA, new RectangleF(bounds.Right - thickness.Right, bounds.Top, thickness.Right, thickness.Top),
                    new RectangleF(bounds.Right - radius.TopRight, bounds.Top + thickness.Top, radius.TopRight - thickness.Right, radius.TopRight - thickness.Top),
                    corners.TopRightRotation, corners.TopRightReflections, CornerTexture, cornerImage, cornerColor);
                EmitRegion(DA, new RectangleF(bounds.Right - thickness.Right, bounds.Bottom - thickness.Bottom, thickness.Right, thickness.Bottom),
                    new RectangleF(bounds.Right - radius.BottomRight, bounds.Bottom - radius.BottomRight, radius.BottomRight - thickness.Right, radius.BottomRight - thickness.Bottom),
                    corners.BottomRightRotation, corners.BottomRightReflections, CornerTexture, cornerImage, cornerColor);
                EmitRegion(DA, new RectangleF(bounds.Left, bounds.Bottom - thickness.Bottom, thickness.Left, thickness.Bottom),
                    new RectangleF(bounds.Left + thickness.Left, bounds.Bottom - radius.BottomLeft, radius.BottomLeft - thickness.Left, radius.BottomLeft - thickness.Bottom),
                    corners.BottomLeftRotation, corners.BottomLeftReflections, CornerTexture, cornerImage, cornerColor);
            }

            if (drawEdges)
            {
                Color edgeColor = this.EdgeColor * opacity * EdgeTexture.Opacity;
                EdgeTransforms edges = Transforms.EdgeTransforms;
                //  Same edge rectangles as the rectangle path: they run between the corner blocks, and the arcs cut through them.
                EmitRegion(DA, new RectangleF(bounds.Left + thickness.Left, bounds.Top, bounds.Width - thickness.Width, thickness.Top), RectangleF.Empty,
                    edges.TopRotation, edges.TopReflections, EdgeTexture, edgeImage, edgeColor);
                EmitRegion(DA, new RectangleF(bounds.Right - thickness.Right, bounds.Top + thickness.Top, thickness.Right, bounds.Height - thickness.Height), RectangleF.Empty,
                    edges.RightRotation, edges.RightReflections, EdgeTexture, edgeImage, edgeColor);
                EmitRegion(DA, new RectangleF(bounds.Left + thickness.Left, bounds.Bottom - thickness.Bottom, bounds.Width - thickness.Width, thickness.Bottom), RectangleF.Empty,
                    edges.BottomRotation, edges.BottomReflections, EdgeTexture, edgeImage, edgeColor);
                EmitRegion(DA, new RectangleF(bounds.Left, bounds.Top + thickness.Top, thickness.Left, bounds.Height - thickness.Height), RectangleF.Empty,
                    edges.LeftRotation, edges.LeftReflections, EdgeTexture, edgeImage, edgeColor);
            }

            //  Clips every ring quad against the layout rectangle (and the deeper corner zone, when any), fans the pieces into triangles and
            //  emits them in one call. Texture coordinates always come from the layout rectangle, so a piece in the deeper zone is clamped.
            void EmitRegion(ElementDrawArgs args, RectangleF layoutRect, RectangleF deeperRect, float rotation, SpriteEffects reflections,
                MGTextureData textureData, IUIImageResource image, Color color)
            {
                if (layoutRect.Width <= 0 || layoutRect.Height <= 0)
                {
                    return;
                }

                regionVertices.Clear();
                regionUVs.Clear();
                regionIndices.Clear();
                bool hasDeeperZone = deeperRect.Width > 0 && deeperRect.Height > 0;

                for (int quad = 0; quad < quadCount; quad++)
                {
                    //  Ring quad i = outer i, outer i+1, inner i+1, inner i (BuildBorderRingIndices emits (o, o2, i2) then (i2, i, o)).
                    int first = quad * 6;
                    quadPolygon.Clear();
                    quadPolygon.Add(vertices[ringIndices[first]]);
                    quadPolygon.Add(vertices[ringIndices[first + 1]]);
                    quadPolygon.Add(vertices[ringIndices[first + 2]]);
                    quadPolygon.Add(vertices[ringIndices[first + 4]]);

                    AppendClippedPolygon(layoutRect);
                    if (hasDeeperZone)
                    {
                        AppendClippedPolygon(deeperRect);
                    }
                }

                if (regionIndices.Count >= 3)
                {
                    args.Context.DrawTexturedTriangleList(origin, image, regionVertices.ToArray(), regionUVs.ToArray(), regionIndices.ToArray(), color);
                }

                void AppendClippedPolygon(RectangleF clipRect)
                {
                    Rectangle integerClipRect = new((int)MathF.Round(clipRect.X), (int)MathF.Round(clipRect.Y),
                        (int)MathF.Round(clipRect.Width), (int)MathF.Round(clipRect.Height));
                    MGConvexPolygonClipper.ClipToRectangle(quadPolygon, integerClipRect, clipped, scratch);
                    if (clipped.Count < 3 || Math.Abs(SignedArea(clipped)) < 1e-4f)
                    {
                        return;
                    }

                    int baseIndex = regionVertices.Count;
                    foreach (Vector2 vertex in clipped)
                    {
                        regionVertices.Add(vertex);
                        regionUVs.Add(GetTextureCoordinate(vertex, layoutRect, rotation, reflections, textureData, image));
                    }

                    MGConvexPolygonClipper.AppendFanTriangles(baseIndex, clipped.Count, regionIndices);
                }
            }
        }

        private static float SignedArea(List<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Count];
                area += a.X * b.Y - b.X * a.Y;
            }

            return area / 2f;
        }

        /// <summary>Normalized texture coordinate of <paramref name="vertex"/> for a region stretched over <paramref name="regionRect"/>:
        /// rotation about the rectangle centre with the same fitted size as the rectangle path (bounding box of the rotated rectangle), then the
        /// <see cref="SpriteEffects"/> reflections, clamped to [0,1], then mapped into the texture's source rectangle.</summary>
        private static Vector2 GetTextureCoordinate(Vector2 vertex, RectangleF regionRect, float rotation, SpriteEffects reflections, MGTextureData textureData, IUIImageResource image)
        {
            Vector2 local;
            if (rotation.IsAlmostZero())
            {
                local = new Vector2(
                    regionRect.Width > 0 ? (vertex.X - regionRect.X) / regionRect.Width : 0f,
                    regionRect.Height > 0 ? (vertex.Y - regionRect.Y) / regionRect.Height : 0f);
            }
            else
            {
                RectangleF rotated = regionRect.CreateTransformedF(Matrix.CreateRotationZ(rotation));
                float fittedWidth = Math.Abs(rotated.Width) > 0 ? Math.Abs(rotated.Width) : regionRect.Width;
                float fittedHeight = Math.Abs(rotated.Height) > 0 ? Math.Abs(rotated.Height) : regionRect.Height;
                Vector2 center = new(regionRect.Center.X, regionRect.Center.Y);
                Vector2 delta = vertex - center;
                float cos = (float)Math.Cos(rotation);
                float sin = (float)Math.Sin(rotation);
                //  Undo the sprite rotation to find where the vertex falls inside the fitted, unrotated sprite.
                Vector2 unrotated = new(delta.X * cos + delta.Y * sin, -delta.X * sin + delta.Y * cos);
                local = new Vector2(
                    fittedWidth > 0 ? 0.5f + unrotated.X / fittedWidth : 0f,
                    fittedHeight > 0 ? 0.5f + unrotated.Y / fittedHeight : 0f);
            }

            if ((reflections & SpriteEffects.FlipHorizontally) != 0)
            {
                local.X = 1f - local.X;
            }

            if ((reflections & SpriteEffects.FlipVertically) != 0)
            {
                local.Y = 1f - local.Y;
            }

            local.X = Math.Clamp(local.X, 0f, 1f);
            local.Y = Math.Clamp(local.Y, 0f, 1f);

            Rectangle source = textureData.SourceRect ?? new Rectangle(0, 0, image.Width, image.Height);
            return new Vector2((source.X + local.X * source.Width) / image.Width, (source.Y + local.Y * source.Height) / image.Height);
        }

        public IBorderBrush Copy() => new MGTexturedBorderBrush(EdgeTexture, EdgeColor, CornerTexture, CornerColor, Transforms, Opacity);
    }
}
