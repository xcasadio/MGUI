namespace MGUI.Core.UI
{
    public sealed class ColorPickerOptions
    {
        public ColorValue InitialValue { get; set; } = new(1f, 1f, 1f, 1f);
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
        public ColorSpaceMode StorageColorSpace { get; set; } = ColorSpaceMode.Srgb;
        public ColorSpaceMode DisplayColorSpace { get; set; } = ColorSpaceMode.Srgb;
        public bool StoreAsLinear
        {
            get => StorageColorSpace == ColorSpaceMode.Linear;
            set => StorageColorSpace = value ? ColorSpaceMode.Linear : ColorSpaceMode.Srgb;
        }
        public bool DisplayAsSrgb
        {
            get => DisplayColorSpace == ColorSpaceMode.Srgb;
            set => DisplayColorSpace = value ? ColorSpaceMode.Srgb : StorageColorSpace;
        }
        public ColorPickerConstraints Constraints { get; set; } = new();
        public IColorEditTransaction EditTransaction { get; set; } = NoOpColorEditTransaction.Instance;
        public IColorPickService ColorPickService { get; set; } = UnsupportedColorPickService.Instance;
    }
}