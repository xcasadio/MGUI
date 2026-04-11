using MGUI.Shared.Helpers;
using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Core.UI
{
    /// <param name="RenderSizeOverride">The <see cref="Size"/> to use for the destination <see cref="Rectangle"/> when drawing this texture via <see cref="Draw(IUIDrawContext, Point, Color?, float)"/>.<para/>
    /// See also: <see cref="RenderSize"/></param>
    public readonly record struct MGTextureData(IUIImageResource Image, Rectangle? SourceRect = null, float Opacity = 1f, Size? RenderSizeOverride = null)
    {
        public MGTextureData(Texture2D Texture, Rectangle? SourceRect = null, float Opacity = 1f, Size? RenderSizeOverride = null)
            : this(new MonoGameImageResource(Texture), SourceRect, Opacity, RenderSizeOverride) { }

        public Texture2D Texture => Image.GetTexture2D();

        /// <summary>The actual size this texture will be drawn at if using <see cref="Draw(IUIDrawContext, Point, Microsoft.Xna.Framework.Color?, float)"/></summary>
        public Size RenderSize => RenderSizeOverride ?? SourceRect?.Size.AsSize() ?? new Size(Image.Width, Image.Height);

        private static Rectangle GetRectangle(Point Topleft, Size Size) => new(Topleft.X, Topleft.Y, Size.Width, Size.Height);

        public void Draw(IUIDrawContext Context, Point Position, Color? Color = null, float Opacity = 1f)
            => Draw(Context, GetRectangle(Position, RenderSize), Color, Opacity);

        public void Draw(IUIDrawContext Context, Rectangle Destination, Color? Color = null, float Opacity = 1f)
            => Context.DrawTextureTo(Image, SourceRect, Destination, (Color ?? Microsoft.Xna.Framework.Color.White) * this.Opacity * Opacity);

        public void Draw(DrawTransaction DT, Point Position, Color? Color = null, float Opacity = 1f)
            => Draw((IUIDrawContext)DT, Position, Color, Opacity);

        public void Draw(DrawTransaction DT, Rectangle Destination, Color? Color = null, float Opacity = 1f)
            => Draw((IUIDrawContext)DT, Destination, Color, Opacity);
    }
}
