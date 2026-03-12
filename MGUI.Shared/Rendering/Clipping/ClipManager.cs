using System;

namespace MGUI.Shared.Rendering.Clipping
{
    internal sealed class ClipManager
    {
        private readonly DrawTransaction _Owner;

        public ClipManager(DrawTransaction owner)
        {
            _Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public ClipResolveResult Resolve(ClipDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            return definition.Kind switch
            {
                ClipKind.None => new(definition, definition, ClipStrategy.None, false),
                ClipKind.Rectangle => new(definition, definition, ClipStrategy.Scissor, false),
                ClipKind.RoundedRectangle or ClipKind.ArbitraryGeometry when definition.AllowRectangleFallback
                    => new(definition, ClipDefinition.Rectangle(definition.Shape.Bounds, definition.IntersectWithCurrentClip, debugName: definition.DebugName),
                        ClipStrategy.Scissor, true),
                ClipKind.RoundedRectangle => new(definition, definition, ClipStrategy.Stencil, false),
                ClipKind.ArbitraryGeometry => new(definition, definition, ClipStrategy.Mask, false),
                _ => throw new NotImplementedException($"Unrecognized {nameof(ClipKind)}: {definition.Kind}")
            };
        }

        public ClipScope Push(ClipDefinition definition)
        {
            ClipResolveResult resolution = Resolve(definition);

            return resolution.Strategy switch
            {
                ClipStrategy.None => new(resolution, () => { }),
                ClipStrategy.Scissor => _Owner.PushRectangleClipCore(resolution.Effective.Shape.Bounds, resolution.Effective.IntersectWithCurrentClip, resolution),
                ClipStrategy.Stencil => PushStencil(resolution),
                _ => throw new NotSupportedException($"Clip strategy '{resolution.Strategy}' is not available until the corresponding backend is installed.")
            };
        }

        private int _StencilDepth;
        private const int MaxStencilDepth = 255;

        private ClipScope PushStencil(ClipResolveResult resolution)
        {
            ClipGeometry geometry = resolution.Effective.Shape.Geometry ?? throw new InvalidOperationException(
                $"Clip '{resolution.Effective.DebugName ?? resolution.Effective.Kind.ToString()}' requires clip geometry for stencil rendering.");

            if (_StencilDepth == 0)
            {
                _Owner.ClearStencil(0);
            }

            if (_StencilDepth >= MaxStencilDepth)
            {
                throw new InvalidOperationException($"Maximum stencil clip nesting depth of {MaxStencilDepth} was exceeded.");
            }

            int parentDepth = _StencilDepth;
            int childDepth = _StencilDepth + 1;

            using (_Owner.SetDrawSettingsTemporary(_Owner.CurrentSettings with
            {
                BlendType = BlendType.ColorWriteDisable,
                DepthStencilType = DepthStencilType.StencilWriteIncrement,
                StencilReference = parentDepth,
            }))
            {
                _Owner.DrawClipGeometry(geometry);
            }

            IDisposable stencilReadScope = _Owner.SetDrawSettingsTemporary(_Owner.CurrentSettings with
            {
                DepthStencilType = DepthStencilType.StencilReadEqual,
                StencilReference = childDepth,
            });
            _StencilDepth = childDepth;

            ClipResolveResult effectiveResolution = resolution with { StencilDepth = childDepth };

            return new ClipScope(effectiveResolution, () =>
            {
                stencilReadScope.Dispose();

                using (_Owner.SetDrawSettingsTemporary(_Owner.CurrentSettings with
                {
                    BlendType = BlendType.ColorWriteDisable,
                    DepthStencilType = DepthStencilType.StencilRestoreDecrement,
                    StencilReference = childDepth,
                }))
                {
                    _Owner.DrawClipGeometry(geometry);
                }

                _StencilDepth = parentDepth;
            });
        }
    }
}