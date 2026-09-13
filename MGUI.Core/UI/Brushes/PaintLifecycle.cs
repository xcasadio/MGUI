using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Rendering;

namespace MGUI.Core.UI.Brushes;

/// <summary>Per-frame dedup registry for stateful paints (fill brushes / border brushes), keyed by object reference.<para/>
/// Owned by <see cref="MGDesktop"/>: cleared once at the start of every <see cref="MGDesktop.Update"/> call, so one
/// <see cref="MGDesktop.Update"/> call == one frame == one reset. See <see cref="PaintLifecycle"/> for the ticking helpers
/// that consult this registry.</summary>
internal sealed class PaintUpdateRegistry : IPaintUpdateRegistry
{
    private readonly HashSet<object> _seen = new(ReferenceEqualityComparer.Instance);

    public bool TryBeginUpdate(object paint) => _seen.Add(paint);

    public void Clear() => _seen.Clear();
}

/// <summary>Routes every <see cref="IFillBrush.Update(UpdateBaseArgs)"/> / <see cref="IBorderBrush.Update(UpdateBaseArgs)"/>
/// call through <see cref="UpdateBaseArgs.PaintRegistry"/> so a paint instance shared by reference across several slots
/// or elements is ticked exactly once per frame instead of once per slot. See "Limites connues" in
/// Docs/drawing-architecture.md.</summary>
internal static class PaintLifecycle
{
    public static void Update(IFillBrush brush, UpdateBaseArgs UA)
    {
        if (brush == null)
        {
            return;
        }

        if (UA.PaintRegistry == null || UA.PaintRegistry.TryBeginUpdate(brush))
        {
            brush.Update(UA);
        }
    }

    public static void Update(IBorderBrush brush, UpdateBaseArgs UA)
    {
        if (brush == null)
        {
            return;
        }

        if (UA.PaintRegistry == null || UA.PaintRegistry.TryBeginUpdate(brush))
        {
            brush.Update(UA);
        }
    }
}