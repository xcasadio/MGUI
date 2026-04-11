using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Linq;
using MGUI.Core.UI.Shapes;

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

        public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
            => Draw(DA, Element, Shape.OuterBounds, Shape.NormalizedBorderThickness);

        public IBorderBrush Copy() => new MGTexturedBorderBrush(EdgeTexture, EdgeColor, CornerTexture, CornerColor, Transforms, Opacity);
    }
}
