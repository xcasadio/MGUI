using System.Globalization;
using MGUI.Shared.Helpers;

namespace MGUI.Samples.Features
{
    /// <summary>The <see cref="ImageBrightnessSample"/> window's <c>WindowDataContext</c> (ADR-0021, "Image brightness above 1"):
    /// the brightness shared by the slider (TwoWay) and the image (OneWay), and its text.</summary>
    public sealed class ImageBrightnessViewModel : ViewModelBase
    {
        private float _brightness = 1f;
        /// <summary>Bound to <c>Image.Brightness</c> and, TwoWay, to the slider's value.</summary>
        public float Brightness
        {
            get => _brightness;
            set
            {
                if (_brightness != value)
                {
                    _brightness = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(BrightnessText));
                }
            }
        }

        /// <summary>The brightness with two decimals, for the label next to the slider.</summary>
        public string BrightnessText => _brightness.ToString("F2", CultureInfo.InvariantCulture);
    }
}
