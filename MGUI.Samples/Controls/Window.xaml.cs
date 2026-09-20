using MGUI.Core.UI;
using Microsoft.Xna.Framework.Content;
using MonoGame.Extended;

namespace MGUI.Samples.Controls
{
    public class WindowSamples : SampleBase
    {
        private static readonly Thickness PlacementMargin = new(16);

        public WindowSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Controls)}", "Window.xaml")
        {
            //  The sample moves itself: the clearest demonstration of screen placement is a window that
            //  re-places itself while you watch, including the way an aligned window is kept inside the view.
            BindPlacement("btnPlaceTopLeft", HorizontalAlignment.Left, VerticalAlignment.Top);
            BindPlacement("btnPlaceCenter", HorizontalAlignment.Center, VerticalAlignment.Center);
            BindPlacement("btnPlaceBottomRight", HorizontalAlignment.Right, VerticalAlignment.Bottom);
            BindPlacement("btnPlaceStretch", HorizontalAlignment.Stretch, VerticalAlignment.Top);
            BindPlacement("btnPlaceNone", null, null);
        }

        private void BindPlacement(string ButtonName, HorizontalAlignment? Horizontal, VerticalAlignment? Vertical)
        {
            if (!Window.TryGetElementByName(ButtonName, out MGButton Button))
            {
                return;
            }

            Button.AddCommandHandler((_, _) =>
            {
                Window.Margin = Horizontal.HasValue || Vertical.HasValue ? PlacementMargin : new Thickness(0);
                Window.ScreenHorizontalAlignment = Horizontal;
                Window.ScreenVerticalAlignment = Vertical;
            });
        }
    }
}
