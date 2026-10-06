using MGUI.Core.UI;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Samples.Controls
{
    public class SliderSamples : SampleBase
    {
        private MGSlider Slider1 { get; }
        private MGTextBlock TextBlock1 { get; }
        private MGSlider Slider2 { get; }
        private MGTextBlock TextBlock2 { get; }
        private MGSlider Slider3 { get; }
        private MGTextBlock TextBlock3 { get; }

        public SliderSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Controls)}", "Slider.xaml")
        {
            Slider1 = Window.GetElementByName<MGSlider>("Slider1");
            TextBlock1 = Window.GetElementByName<MGTextBlock>("TextBlock1");
            UpdateTextBlock1Text();
            Slider1.ValueChangedNonAlloc += (sender, e) => { UpdateTextBlock1Text(); };

            Slider2 = Window.GetElementByName<MGSlider>("Slider2");
            TextBlock2 = Window.GetElementByName<MGTextBlock>("TextBlock2");
            UpdateTextBlock2Text();
            Slider2.ValueChangedNonAlloc += (sender, e) => { UpdateTextBlock2Text(); };

            Slider3 = Window.GetElementByName<MGSlider>("Slider3");
            TextBlock3 = Window.GetElementByName<MGTextBlock>("TextBlock3");
            UpdateTextBlock3Text();
            Slider3.ValueChangedNonAlloc += (sender, e) => { UpdateTextBlock3Text(); };
        }

        private void UpdateTextBlock1Text() => TextBlock1.SetText(Slider1.Value.ToString("#.00"));
        private void UpdateTextBlock2Text() => TextBlock2.SetText(Slider2.Value.ToString("#.00"));
        private void UpdateTextBlock3Text() => TextBlock3.SetText(Slider3.Value.ToString("0"));
    }
}
