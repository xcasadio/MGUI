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

    /// <summary>A clip that stays in force until it is disposed. A scope made with <see cref="ClipScope(ClipResolveResult, Action)"/> is used once.<para/>
    /// A backend that pushes clips every frame can keep its scopes and re-arm each one after its <see cref="Dispose"/> (see <see cref="ClipScope(Action)"/>,
    /// <see cref="Reset(ClipResolveResult)"/> and <see cref="ResetRectangle(Rectangle, bool)"/>), so that a push allocates nothing. A reference to a scope
    /// that was disposed and re-armed since designates the new clip: whoever disposes a scope drops its reference.</summary>
    public sealed class ClipScope : IDisposable
    {
        private ClipResolveResult _Resolution;
        private bool _IsResolutionPending;
        private Rectangle _PendingBounds;
        private bool _PendingIntersectWithCurrentClip;
        private readonly Action _DisposeAction;
        private bool _IsDisposed;

        /// <summary>What was asked and how it is satisfied. For a scope armed with <see cref="ResetRectangle(Rectangle, bool)"/> it is built the first time it is read.</summary>
        public ClipResolveResult Resolution
        {
            get
            {
                if (_IsResolutionPending)
                {
                    ClipDefinition rectangle = ClipDefinition.Rectangle(_PendingBounds, _PendingIntersectWithCurrentClip);
                    _Resolution = new ClipResolveResult(rectangle, rectangle, ClipStrategy.Scissor, false);
                    _IsResolutionPending = false;
                }

                return _Resolution;
            }
        }

        public ClipScope(ClipResolveResult resolution, Action disposeAction)
        {
            _Resolution = resolution;
            _DisposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
        }

        /// <summary>Creates a scope that is not armed: it holds no clip until <see cref="Reset(ClipResolveResult)"/> or <see cref="ResetRectangle(Rectangle, bool)"/>
        /// arms it, and <paramref name="disposeAction"/> runs on the first <see cref="Dispose"/> after each arming.</summary>
        public ClipScope(Action disposeAction)
        {
            _DisposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
            _IsDisposed = true;
        }

        /// <summary>Arms a disposed scope again with a new <see cref="Resolution"/>.</summary>
        /// <exception cref="InvalidOperationException">The scope is armed: it was not disposed since it was created or last armed.</exception>
        public void Reset(ClipResolveResult resolution)
        {
            ThrowIfArmed();
            _Resolution = resolution;
            _IsResolutionPending = false;
            _IsDisposed = false;
        }

        /// <summary>Arms a disposed scope again with the resolution of a scissor clip on <paramref name="bounds"/>, without creating the <see cref="ClipDefinition"/>
        /// that <see cref="Resolution"/> will hold: that one is built if and when <see cref="Resolution"/> is read.</summary>
        /// <exception cref="InvalidOperationException">The scope is armed: it was not disposed since it was created or last armed.</exception>
        public void ResetRectangle(Rectangle bounds, bool intersectWithCurrentClip)
        {
            ThrowIfArmed();
            _PendingBounds = bounds;
            _PendingIntersectWithCurrentClip = intersectWithCurrentClip;
            _IsResolutionPending = true;
            _IsDisposed = false;
        }

        /// <summary>Runs the dispose action the first time it is called since the scope was created or armed; later calls do nothing.</summary>
        public void Dispose()
        {
            if (!_IsDisposed)
            {
                _IsDisposed = true;
                _DisposeAction();
            }
        }

        private void ThrowIfArmed()
        {
            if (!_IsDisposed)
            {
                throw new InvalidOperationException("A clip scope can only be re-armed after it was disposed.");
            }
        }
    }
}