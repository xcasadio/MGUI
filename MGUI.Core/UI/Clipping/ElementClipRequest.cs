using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Clipping;

internal enum ElementClipKind
{
    Rectangle
}

internal readonly record struct ElementClipRequest(ElementClipKind Kind, Rectangle Bounds, bool IntersectWithCurrentClipTarget)
{
    public static ElementClipRequest Rectangle(Rectangle bounds, bool intersectWithCurrentClipTarget)
        => new(ElementClipKind.Rectangle, bounds, intersectWithCurrentClipTarget);

    public IDisposable Push(IUIRenderContext context)
        => Kind switch
        {
            ElementClipKind.Rectangle => context.SetClipTargetTemporary(Bounds, IntersectWithCurrentClipTarget),
            _ => throw new NotImplementedException($"Unrecognized {nameof(ElementClipKind)}: {Kind}")
        };
}