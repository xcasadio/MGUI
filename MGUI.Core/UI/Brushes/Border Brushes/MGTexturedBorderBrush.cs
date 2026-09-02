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

        private enum BorderRegion
        {
            TopLeft,
            TopRight,
            BottomRight,
            BottomLeft,
            Top,
            Right,
            Bottom,
            Left,
        }

        private const float RegionTolerance = 0.01f;

        /// <summary>Rounded path: maps the edge texture on the straight segments of the border ring and the corner texture on its arcs, using
        /// <see cref="MGBoxGeometry.OuterContour"/>, <see cref="MGBoxGeometry.InnerContour"/> and <see cref="MGBoxGeometry.BorderRingIndices"/>.<para/>
        /// Rules (the geometry is never rebuilt here): ring quad i lies between outer contour vertices i and i+1; a quad whose two outer vertices belong
        /// to the same corner square (r x r, inclusive) is a corner quad, any other quad belongs to the side nearest to its outer midpoint. Each region has
        /// a rectangle (corner square, or the straight edge between the arcs, as thick as the border) onto which the texture is stretched; rotations and
        /// reflections of <see cref="Transforms"/> are applied around that rectangle's centre exactly like the rectangle path fits the rotated sprite.<para/>
        /// Falls back to the rectangle path, on purpose and documented (Docs/Tasks/drawing-tasks.md, Tache 4), when the geometry uses the rectangle fast
        /// path or when it has no ring mesh (a border thickness reaching the corner radius collapses the inner arc, see MGBoxGeometryBuilder.BuildBorderRingIndices).</summary>
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
            MGCornerRadius radius = Shape.NormalizedCornerRadius;
            IReadOnlyList<Vector2> vertices = Geometry.Vertices;
            IReadOnlyList<int> ringIndices = Geometry.BorderRingIndices;
            int outerCount = Geometry.OuterContourVertexCount;
            int quadCount = ringIndices.Count / 6;
            if (outerCount < 2 || quadCount == 0)
            {
                return;
            }

            BorderRegion[] regionOfQuad = new BorderRegion[quadCount];
            for (int quad = 0; quad < quadCount; quad++)
            {
                Vector2 start = vertices[quad];
                Vector2 end = vertices[(quad + 1) % outerCount];
                BorderRegion? startCorner = GetCorner(start, bounds, radius);
                BorderRegion? endCorner = GetCorner(end, bounds, radius);
                regionOfQuad[quad] = startCorner.HasValue && startCorner == endCorner
                    ? startCorner.Value
                    : GetNearestEdge((start + end) / 2f, bounds);
            }

            Vector2 origin = DA.Offset.ToVector2();
            int[] remap = new int[vertices.Count];
            List<Vector2> regionVertices = new();
            List<Vector2> regionUVs = new();
            List<int> regionIndices = new();

            if (drawCorners)
            {
                Color cornerColor = this.CornerColor * opacity * CornerTexture.Opacity;
                CornerTransforms corners = Transforms.CornerTransforms;
                EmitRegion(DA, BorderRegion.TopLeft, new RectangleF(bounds.Left, bounds.Top, radius.TopLeft, radius.TopLeft), corners.TopLeftRotation, corners.TopLeftReflections, CornerTexture, cornerImage, cornerColor);
                EmitRegion(DA, BorderRegion.TopRight, new RectangleF(bounds.Right - radius.TopRight, bounds.Top, radius.TopRight, radius.TopRight), corners.TopRightRotation, corners.TopRightReflections, CornerTexture, cornerImage, cornerColor);
                EmitRegion(DA, BorderRegion.BottomRight, new RectangleF(bounds.Right - radius.BottomRight, bounds.Bottom - radius.BottomRight, radius.BottomRight, radius.BottomRight), corners.BottomRightRotation, corners.BottomRightReflections, CornerTexture, cornerImage, cornerColor);
                EmitRegion(DA, BorderRegion.BottomLeft, new RectangleF(bounds.Left, bounds.Bottom - radius.BottomLeft, radius.BottomLeft, radius.BottomLeft), corners.BottomLeftRotation, corners.BottomLeftReflections, CornerTexture, cornerImage, cornerColor);
            }

            if (drawEdges)
            {
                Color edgeColor = this.EdgeColor * opacity * EdgeTexture.Opacity;
                EdgeTransforms edges = Transforms.EdgeTransforms;
                EmitRegion(DA, BorderRegion.Top, new RectangleF(bounds.Left + radius.TopLeft, bounds.Top, bounds.Width - radius.TopLeft - radius.TopRight, thickness.Top), edges.TopRotation, edges.TopReflections, EdgeTexture, edgeImage, edgeColor);
                EmitRegion(DA, BorderRegion.Right, new RectangleF(bounds.Right - thickness.Right, bounds.Top + radius.TopRight, thickness.Right, bounds.Height - radius.TopRight - radius.BottomRight), edges.RightRotation, edges.RightReflections, EdgeTexture, edgeImage, edgeColor);
                EmitRegion(DA, BorderRegion.Bottom, new RectangleF(bounds.Left + radius.BottomLeft, bounds.Bottom - thickness.Bottom, bounds.Width - radius.BottomLeft - radius.BottomRight, thickness.Bottom), edges.BottomRotation, edges.BottomReflections, EdgeTexture, edgeImage, edgeColor);
                EmitRegion(DA, BorderRegion.Left, new RectangleF(bounds.Left, bounds.Top + radius.TopLeft, thickness.Left, bounds.Height - radius.TopLeft - radius.BottomLeft), edges.LeftRotation, edges.LeftReflections, EdgeTexture, edgeImage, edgeColor);
            }

            void EmitRegion(ElementDrawArgs args, BorderRegion region, RectangleF regionRect, float rotation, SpriteEffects reflections,
                MGTextureData textureData, IUIImageResource image, Color color)
            {
                Array.Fill(remap, -1);
                regionVertices.Clear();
                regionUVs.Clear();
                regionIndices.Clear();

                for (int quad = 0; quad < quadCount; quad++)
                {
                    if (regionOfQuad[quad] != region)
                    {
                        continue;
                    }

                    for (int k = 0; k < 6; k++)
                    {
                        int vertexIndex = ringIndices[quad * 6 + k];
                        if (remap[vertexIndex] < 0)
                        {
                            remap[vertexIndex] = regionVertices.Count;
                            Vector2 vertex = vertices[vertexIndex];
                            regionVertices.Add(vertex);
                            regionUVs.Add(GetTextureCoordinate(vertex, regionRect, rotation, reflections, textureData, image));
                        }

                        regionIndices.Add(remap[vertexIndex]);
                    }
                }

                if (regionIndices.Count >= 3)
                {
                    args.Context.DrawTexturedTriangleList(origin, image, regionVertices.ToArray(), regionUVs.ToArray(), regionIndices.ToArray(), color);
                }
            }
        }

        /// <summary>Inclusive membership of an outer contour vertex to a corner square (r x r), with a small tolerance for the float arc points.</summary>
        private static BorderRegion? GetCorner(Vector2 vertex, Rectangle bounds, MGCornerRadius radius)
        {
            if (radius.TopLeft > 0 && vertex.X <= bounds.Left + radius.TopLeft + RegionTolerance && vertex.Y <= bounds.Top + radius.TopLeft + RegionTolerance)
            {
                return BorderRegion.TopLeft;
            }

            if (radius.TopRight > 0 && vertex.X >= bounds.Right - radius.TopRight - RegionTolerance && vertex.Y <= bounds.Top + radius.TopRight + RegionTolerance)
            {
                return BorderRegion.TopRight;
            }

            if (radius.BottomRight > 0 && vertex.X >= bounds.Right - radius.BottomRight - RegionTolerance && vertex.Y >= bounds.Bottom - radius.BottomRight - RegionTolerance)
            {
                return BorderRegion.BottomRight;
            }

            if (radius.BottomLeft > 0 && vertex.X <= bounds.Left + radius.BottomLeft + RegionTolerance && vertex.Y >= bounds.Bottom - radius.BottomLeft - RegionTolerance)
            {
                return BorderRegion.BottomLeft;
            }

            return null;
        }

        /// <summary>Side whose line is nearest to <paramref name="point"/> (the midpoint of a quad's outer vertices).</summary>
        private static BorderRegion GetNearestEdge(Vector2 point, Rectangle bounds)
        {
            float top = Math.Abs(point.Y - bounds.Top);
            float right = Math.Abs(point.X - bounds.Right);
            float bottom = Math.Abs(point.Y - bounds.Bottom);
            float left = Math.Abs(point.X - bounds.Left);

            BorderRegion nearest = BorderRegion.Top;
            float distance = top;
            if (right < distance) { nearest = BorderRegion.Right; distance = right; }
            if (bottom < distance) { nearest = BorderRegion.Bottom; distance = bottom; }
            if (left < distance) { nearest = BorderRegion.Left; }
            return nearest;
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
