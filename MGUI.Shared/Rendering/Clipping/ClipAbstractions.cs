using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace MGUI.Shared.Rendering.Clipping
{
    public enum ClipKind
    {
        None,
        Rectangle,
        RoundedRectangle,
        ArbitraryGeometry
    }

    public enum ClipStrategy
    {
        None,
        Scissor,
        Stencil,
        Mask
    }

    public enum ClipStrategyPreference
    {
        Default,
        PreferScissor,
        PreferStencil,
        PreferMask
    }

    public readonly record struct ClipCornerRadius(int TopLeft, int TopRight, int BottomRight, int BottomLeft)
    {
        public static readonly ClipCornerRadius Zero = new(0, 0, 0, 0);
        public bool IsZero => TopLeft == 0 && TopRight == 0 && BottomRight == 0 && BottomLeft == 0;

        public ClipCornerRadius(int uniformRadius)
            : this(uniformRadius, uniformRadius, uniformRadius, uniformRadius)
        {
        }
    }

    public readonly record struct ClipGeometry(IReadOnlyList<Vector2> Vertices, IReadOnlyList<int> Indices)
    {
        public bool IsEmpty => Vertices == null || Indices == null || Vertices.Count == 0 || Indices.Count == 0;
    }

    public readonly record struct ClipShape(Rectangle Bounds, ClipCornerRadius CornerRadius, ClipGeometry? Geometry)
    {
        public static ClipShape Rectangle(Rectangle bounds) => new(bounds, ClipCornerRadius.Zero, null);
        public static ClipShape RoundedRectangle(Rectangle bounds, ClipCornerRadius cornerRadius, ClipGeometry? geometry = null)
            => new(bounds, cornerRadius, geometry);
        public static ClipShape FromGeometry(Rectangle bounds, ClipGeometry geometry)
            => new(bounds, ClipCornerRadius.Zero, geometry);
    }

    public sealed record class ClipDefinition(
        ClipKind Kind,
        ClipShape Shape,
        bool IntersectWithCurrentClip = true,
        ClipStrategyPreference StrategyPreference = ClipStrategyPreference.Default,
        bool AllowRectangleFallback = false,
        string DebugName = null)
    {
        public static ClipDefinition None(bool intersectWithCurrentClip = false, string debugName = null)
            => new(ClipKind.None, ClipShape.Rectangle(Microsoft.Xna.Framework.Rectangle.Empty), intersectWithCurrentClip, ClipStrategyPreference.Default, false, debugName);

        public static ClipDefinition Rectangle(Rectangle bounds, bool intersectWithCurrentClip = true,
            ClipStrategyPreference strategyPreference = ClipStrategyPreference.Default, string debugName = null)
            => new(ClipKind.Rectangle, ClipShape.Rectangle(bounds), intersectWithCurrentClip, strategyPreference, false, debugName);

        public static ClipDefinition RoundedRectangle(Rectangle bounds, ClipCornerRadius cornerRadius, ClipGeometry? geometry = null,
            bool intersectWithCurrentClip = true, ClipStrategyPreference strategyPreference = ClipStrategyPreference.Default,
            bool allowRectangleFallback = false, string debugName = null)
            => new(ClipKind.RoundedRectangle, ClipShape.RoundedRectangle(bounds, cornerRadius, geometry), intersectWithCurrentClip,
                strategyPreference, allowRectangleFallback, debugName);

        public static ClipDefinition ArbitraryGeometry(Rectangle bounds, ClipGeometry geometry, bool intersectWithCurrentClip = true,
            ClipStrategyPreference strategyPreference = ClipStrategyPreference.Default, bool allowRectangleFallback = false,
            string debugName = null)
            => new(ClipKind.ArbitraryGeometry, ClipShape.FromGeometry(bounds, geometry), intersectWithCurrentClip,
                strategyPreference, allowRectangleFallback, debugName);
    }

    public readonly record struct ClipResolveResult(ClipDefinition Requested, ClipDefinition Effective, ClipStrategy Strategy, bool UsedFallback,
        int? StencilDepth = null);

    public sealed class ClipScope : IDisposable
    {
        public ClipResolveResult Resolution { get; }
        private readonly Action _DisposeAction;
        private bool _IsDisposed;

        public ClipScope(ClipResolveResult resolution, Action disposeAction)
        {
            Resolution = resolution;
            _DisposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
        }

        public void Dispose()
        {
            if (!_IsDisposed)
            {
                _IsDisposed = true;
                _DisposeAction();
            }
        }
    }
}