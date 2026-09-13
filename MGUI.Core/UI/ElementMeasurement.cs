using MonoGame.Extended;

namespace MGUI.Core.UI;

public readonly record struct ElementMeasurement(Size AvailableSize, Thickness RequestedSize, Thickness SharedSize, Thickness ContentSize)
{
    public bool IsAvailableSizeGreaterThan(Size Other) => AvailableSize.Width > Other.Width && AvailableSize.Height > Other.Height;
    public bool IsAvailableSizeGreaterThanOrEqual(Size Other) => AvailableSize.Width >= Other.Width && AvailableSize.Height >= Other.Height;
    public bool IsAvailableSizeLessThan(Size Other) => AvailableSize.Width < Other.Width && AvailableSize.Height < Other.Height;
    public bool IsAvailableSizeLessThanOrEqual(Size Other) => AvailableSize.Width <= Other.Width && AvailableSize.Height <= Other.Height;

    public bool IsRequestedSizeGreaterThan(Size Other) => RequestedSize.Width > Other.Width && RequestedSize.Height > Other.Height;
    public bool IsRequestedSizeGreaterThanOrEqual(Size Other) => RequestedSize.Width >= Other.Width && RequestedSize.Height >= Other.Height;
    public bool IsRequestedSizeLessThan(Size Other) => RequestedSize.Width < Other.Width && RequestedSize.Height < Other.Height;
    public bool IsRequestedSizeLessThanOrEqual(Size Other) => RequestedSize.Width <= Other.Width && RequestedSize.Height <= Other.Height;
}