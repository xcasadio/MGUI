using MGUI.Core.UI;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Controls
{
    public class WrapPanelSamples : SampleBase
    {
        public WrapPanelSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Controls)}", "WrapPanel.xaml")
        {
        }
    }
}