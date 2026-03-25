using MGUI.Core.UI;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Controls
{
    public class CanvasSamples : SampleBase
    {
        public CanvasSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Controls)}", "Canvas.xaml")
        {
        }
    }
}