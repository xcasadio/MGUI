namespace MGUI.Core.UI.Containers;

public class MGResponsiveRoot : MGOverlayPanel
{
    public MGResponsiveRoot(MGWindow window)
        : base(window)
    {
        using (BeginInitializing())
        {
            UseResponsiveLayout = true;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            ClipToBounds = true;
        }
    }
}