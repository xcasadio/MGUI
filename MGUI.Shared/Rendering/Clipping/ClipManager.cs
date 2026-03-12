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
                _ => throw new NotSupportedException($"Clip strategy '{resolution.Strategy}' is not available until the corresponding backend is installed.")
            };
        }
    }
}