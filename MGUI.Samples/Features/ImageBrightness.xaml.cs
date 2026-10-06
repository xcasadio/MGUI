using MGUI.Core.UI;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features
{
    /// <summary>Demonstrates ADR-0021 ("Image brightness above 1", G11 of the CasaEngine XAML-screens audit): one image whose
    /// <c>Brightness</c> is bound to a view model, driven by a slider from 0.5 to 2, next to the same image left at 1.</summary>
    public class ImageBrightnessSample : SampleBase
    {
        public ImageBrightnessViewModel ViewModel { get; } = new();

        public ImageBrightnessSample(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, "Features", "ImageBrightness.xaml")
        {
            Window.WindowDataContext = ViewModel;
        }
    }
}
