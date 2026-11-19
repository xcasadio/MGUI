using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Clipping;

internal enum ElementClipKind
{
    /// <summary>The element clips nothing.</summary>
    None,
    /// <summary>A rectangle: pushed with <see cref="IUIRenderContext.PushRectangleClip"/>, no <see cref="ClipDefinition"/> is made for it.</summary>
    Rectangle,
    /// <summary>A rounded or geometry clip: pushed with <see cref="IUIRenderContext.PushClipTemporary"/>.</summary>
    Definition
}

/// <summary>What the clip hooks of <see cref="MGElement"/> (<c>GetSelfClipDefinition</c>, <c>GetContentsClipDefinition</c>) return: nothing, a rectangle, or a
/// <see cref="ClipDefinition"/>. A value, so that a rectangle clip, whatever its bounds, costs no object (ADR-0035). A rectangle always intersects with the clip in force,
/// as <see cref="ClipDefinition.Rectangle"/> does when called with its default.</summary>
internal readonly struct ElementClipRequest
{
    private readonly ClipDefinition _Definition;
    private readonly Rectangle _Bounds;

    private ElementClipRequest(ElementClipKind kind, Rectangle bounds, ClipDefinition definition)
    {
        Kind = kind;
        _Bounds = bounds;
        _Definition = definition;
    }

    public ElementClipKind Kind { get; }

    public Rectangle Bounds => _Bounds;

    /// <summary>The definition of a <see cref="ElementClipKind.Definition"/> request, null for the other kinds.</summary>
    public ClipDefinition Definition => _Definition;

    public static ElementClipRequest None => default;

    public static ElementClipRequest Rectangle(Rectangle bounds)
        => new(ElementClipKind.Rectangle, bounds, null);

    /// <summary>A request for <paramref name="definition"/>; none when it is null.</summary>
    public static ElementClipRequest FromDefinition(ClipDefinition definition)
        => definition == null ? default : new(ElementClipKind.Definition, default, definition);

    /// <summary>Pushes the clip. The default handle, whose disposal does nothing, for a request of no clip.</summary>
    public ClipHandle Push(IUIRenderContext context)
        => Kind switch
        {
            ElementClipKind.Rectangle => context.PushRectangleClip(_Bounds, true),
            ElementClipKind.Definition => context.PushClipTemporary(_Definition),
            _ => default
        };
}
