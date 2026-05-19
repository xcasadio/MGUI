namespace MGUI.Core.UI
{
    public sealed class ColorPickerOptions
    {
        public bool ShowAlpha { get; set; } = true;
        public bool ShowEyeDropper { get; set; } = false;
        public bool IsHdr { get; set; } = false;
        public bool AllowNull { get; set; } = false;
        public bool ShowTextInput { get; set; } = true;
        public bool ShowIntensity { get; set; } = false;
        public bool UseExposureSlider { get; set; } = false;
        public bool ShowToneMappedPreview { get; set; } = false;
        public ColorPickerMode PickerMode { get; set; } = ColorPickerMode.Hsv;
        public ColorValueFormat DisplayFormat { get; set; } = ColorValueFormat.HexRgba;
        public ColorEditCommitMode CommitMode { get; set; } = ColorEditCommitMode.Live;
        public ColorPickerConstraints Constraints { get; set; } = new();
    }
}