using Microsoft.Xna.Framework.Content;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;

namespace MGUI.Samples.Features
{
    public class ResponsiveLayoutSample : SampleBase
    {
        public ResponsiveLayoutSample(ContentManager content, MGDesktop desktop)
            : base(content, desktop, "Features", "ResponsiveLayout.xaml")
        {
            desktop.ResponsiveSettings = new(
                new MGUI.Core.UI.Responsive.UIDesignResolution(1600, 900),
                minUIScaleFactor: 0.75f,
                maxUIScaleFactor: 1.5f,
                textScaleMultiplier: 1.05f,
                minTextScaleFactor: 0.9f,
                maxTextScaleFactor: 1.8f,
                useDpiScale: false);

            Window.WindowStyle = WindowStyle.None;
            Window.Padding = new(0);
            Window.BorderThickness = new(0);
            Window.IsUserResizable = false;
            SyncToViewport();

            MGButton closeButton = Window.GetElementByName<MGButton>("CloseButton");
            closeButton.AddCommandHandler((btn, e) => Hide());

            desktop.ResponsiveMetricsChanged += (sender, e) => SyncToViewport();
        }

        private void SyncToViewport()
        {
            Window.Left = Desktop.ValidScreenBounds.Left;
            Window.Top = Desktop.ValidScreenBounds.Top;
            Window.WindowWidth = Desktop.ValidScreenBounds.Width;
            Window.WindowHeight = Desktop.ValidScreenBounds.Height;
            Window.ValidateWindowSizeAndPosition();
        }
    }
}