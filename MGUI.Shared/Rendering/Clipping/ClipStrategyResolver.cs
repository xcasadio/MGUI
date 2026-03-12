using System;

namespace MGUI.Shared.Rendering.Clipping
{
    internal readonly record struct ClipBackendCapabilities(bool SupportsScissor, bool SupportsStencil, bool SupportsMask)
    {
        public static readonly ClipBackendCapabilities Default = new(true, true, true);
    }

    internal static class ClipStrategyResolver
    {
        public static ClipResolveResult Resolve(ClipDefinition definition, ClipBackendCapabilities capabilities)
        {
            ArgumentNullException.ThrowIfNull(definition);

            return definition.Kind switch
            {
                ClipKind.None => new(definition, definition, ClipStrategy.None, false),
                ClipKind.Rectangle when capabilities.SupportsScissor => new(definition, definition, ClipStrategy.Scissor, false),
                ClipKind.RoundedRectangle when capabilities.SupportsStencil => new(definition, definition, ClipStrategy.Stencil, false),
                ClipKind.ArbitraryGeometry when capabilities.SupportsMask => new(definition, definition, ClipStrategy.Mask, false),
                ClipKind.RoundedRectangle or ClipKind.ArbitraryGeometry when definition.AllowRectangleFallback && capabilities.SupportsScissor
                    => new(definition, ClipDefinition.Rectangle(definition.Shape.Bounds, definition.IntersectWithCurrentClip, debugName: definition.DebugName),
                        ClipStrategy.Scissor, true),
                ClipKind.RoundedRectangle when capabilities.SupportsMask
                    => new(definition, definition, ClipStrategy.Mask, true),
                ClipKind.ArbitraryGeometry when capabilities.SupportsStencil
                    => new(definition, definition, ClipStrategy.Stencil, true),
                _ => throw new NotSupportedException($"No clip backend can satisfy clip '{definition.DebugName ?? definition.Kind.ToString()}' with the current renderer capabilities.")
            };
        }
    }
}