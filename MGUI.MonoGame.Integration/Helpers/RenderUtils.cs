using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.VectorDraw;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Shared.Helpers
{
    public static class RenderUtils
    {
        /// <param name="BlendState">Required: <see cref="PrimitiveBatch.Begin(ref Matrix, ref Matrix, BlendState)"/> assigns the device blend state itself
        /// (falling back to <see cref="BlendState.NonPremultiplied"/> when null), which would discard the state resolved by the caller.</param>
        public static void Begin(this PrimitiveBatch @this, Matrix Projection, Matrix View, BlendState BlendState)
            => @this.Begin(ref Projection, ref View, BlendState ?? throw new ArgumentNullException(nameof(BlendState)));

        /// <param name="PreserveContents">If true, the render target content will be preserved even if it is slow or requires extra memory.</param>
        public static RenderTarget2D CreateRenderTarget(GraphicsDevice GD, Rectangle Dimensions, bool PreserveContents)
            => CreateRenderTarget(GD, Dimensions.Width, Dimensions.Height, PreserveContents);

        /// <param name="PreserveContents">If true, the render target content will be preserved even if it is slow or requires extra memory.</param>
        public static RenderTarget2D CreateRenderTarget(GraphicsDevice GD, int Width, int Height, bool PreserveContents)
            => new(GD, Width, Height, false, SurfaceFormat.Color, DepthFormat.Depth24, 0, PreserveContents ? RenderTargetUsage.PreserveContents : RenderTargetUsage.DiscardContents);
    }
}
