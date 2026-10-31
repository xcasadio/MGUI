using MGUI.Core.UI;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features
{
    /// <summary>Demonstrates ADR-0024 ("Image blend mode"): the same image over the same background, with no <c>BlendType</c>, then
    /// <c>Additive</c>, then <c>Subtractive</c>.</summary>
    public class ImageBlendModeSample : SampleBase
    {
        public ImageBlendModeSample(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, "Features", "ImageBlendMode.xaml")
        {
        }
    }
}
