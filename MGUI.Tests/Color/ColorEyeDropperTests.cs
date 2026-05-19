using MGUI.Core.UI;

namespace MGUI.Tests.ColorPicker;

public class ColorEyeDropperTests
{
    [Fact]
    public void UnsupportedColorPickService_IsNotSupportedAndDoesNotBeginPick()
    {
        IColorPickService service = UnsupportedColorPickService.Instance;
        bool picked = false;
        bool cancelled = false;
        service.ColorPicked += (sender, e) => picked = true;
        service.ColorPickCancelled += (sender, e) => cancelled = true;

        Assert.False(service.IsSupported);
        Assert.False(service.BeginPick(new ColorPickRequest()));
        service.CancelPick();
        Assert.False(picked);
        Assert.False(cancelled);
    }

    [Fact]
    public void ColorPickRequest_DefaultsToMGUIOnlySrgbPick()
    {
        ColorPickRequest request = new();

        Assert.False(request.PreserveAlpha);
        Assert.False(request.PickFromScreen);
        Assert.True(request.PickFromMGUIOnly);
        Assert.Equal(ColorSpaceMode.Srgb, request.OutputColorSpace);
        Assert.Null(request.CurrentValue);
    }

    [Fact]
    public void ColorPickerOptions_UsesUnsupportedServiceByDefault()
    {
        ColorPickerOptions options = new();

        Assert.Same(UnsupportedColorPickService.Instance, options.ColorPickService);
        Assert.False(options.ColorPickService.IsSupported);
    }
}
