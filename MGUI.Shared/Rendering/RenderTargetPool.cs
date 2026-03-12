using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace MGUI.Shared.Rendering
{
    internal readonly record struct RenderTargetPoolKey(int Width, int Height, SurfaceFormat SurfaceFormat, DepthFormat DepthFormat,
        int MultiSampleCount, RenderTargetUsage Usage);

    internal sealed class RenderTargetPool
    {
        private const int MaxTotalPooledTargets = 16;
        private const int MaxTargetsPerKey = 4;

        private readonly Dictionary<RenderTargetPoolKey, Stack<RenderTarget2D>> _Available = new();
        private int _TotalPooledTargets;

        public RenderTarget2D Rent(GraphicsDevice graphicsDevice, int width, int height, bool preserveContents)
        {
            RenderTargetPoolKey key = CreateKey(width, height, preserveContents);
            if (_Available.TryGetValue(key, out Stack<RenderTarget2D> stack))
            {
                while (stack.Count > 0)
                {
                    RenderTarget2D target = stack.Pop();
                    _TotalPooledTargets--;
                    if (!target.IsDisposed)
                    {
                        return target;
                    }
                }
            }

            return Helpers.RenderUtils.CreateRenderTarget(graphicsDevice, width, height, preserveContents);
        }

        public void Return(RenderTarget2D renderTarget)
        {
            if (renderTarget == null || renderTarget.IsDisposed)
            {
                return;
            }

            RenderTargetPoolKey key = CreateKey(renderTarget.Width, renderTarget.Height,
                renderTarget.RenderTargetUsage == RenderTargetUsage.PreserveContents);
            if (!_Available.TryGetValue(key, out Stack<RenderTarget2D> stack))
            {
                stack = new Stack<RenderTarget2D>();
                _Available[key] = stack;
            }

            if (stack.Count >= MaxTargetsPerKey || _TotalPooledTargets >= MaxTotalPooledTargets)
            {
                renderTarget.Dispose();
                return;
            }

            stack.Push(renderTarget);
            _TotalPooledTargets++;
        }

        private static RenderTargetPoolKey CreateKey(int width, int height, bool preserveContents)
            => new(width, height, SurfaceFormat.Color, DepthFormat.Depth24, 0,
                preserveContents ? RenderTargetUsage.PreserveContents : RenderTargetUsage.DiscardContents);
    }
}