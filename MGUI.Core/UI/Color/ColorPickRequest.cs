namespace MGUI.Core.UI
{
    public sealed class ColorPickRequest
    {
        public bool PreserveAlpha { get; set; }
        public bool PickFromScreen { get; set; }
        public bool PickFromMGUIOnly { get; set; } = true;
        public ColorSpaceMode OutputColorSpace { get; set; } = ColorSpaceMode.Srgb;
        public ColorValue? CurrentValue { get; set; }
    }
}