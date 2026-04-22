namespace MGUI.Core.UI.Adorners;

public class MGAdornerLayer : Containers.MGOverlayPanel
{
    public MGAdornerLayer(MGWindow window)
        : base(window)
    {
        using (BeginInitializing())
        {
            IsHitTestVisible = false;
            ClipToBounds = false;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
        }
    }

    public bool TryAddAdorner(MGAdorner adorner, double? zIndex = null)
        => TryAddChild(adorner, default, zIndex);

    public bool TryRemoveAdorner(MGAdorner adorner)
        => TryRemoveChild(adorner);
}