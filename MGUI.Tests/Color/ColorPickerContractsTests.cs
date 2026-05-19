using MGUI.Core.UI;

namespace MGUI.Tests.ColorPicker;

public class ColorPickerContractsTests
{
    [Fact]
    public void Constraints_Defaults_ClampToLdrAndPreserveAlpha()
    {
        ColorPickerConstraints constraints = new();
        ColorValue constrained = constraints.Apply(new ColorValue(2f, -1f, 0.5f, 0.25f, ColorSpaceMode.Linear, true));

        Assert.Equal(new ColorValue(1f, 0f, 0.5f, 0.25f, ColorSpaceMode.Linear), constrained);
        Assert.False(constrained.IsHdr);
    }

    [Fact]
    public void Constraints_DisallowAlpha_ForcesOpaque()
    {
        ColorPickerConstraints constraints = new() { AllowAlpha = false };

        ColorValue constrained = constraints.Apply(new ColorValue(0.1f, 0.2f, 0.3f, 0.4f));

        Assert.Equal(1f, constrained.A);
    }

    [Fact]
    public void Constraints_AllowHdr_UsesConfiguredChannelRange()
    {
        ColorPickerConstraints constraints = new() { AllowHdr = true, MaxChannelValue = 8f };

        ColorValue constrained = constraints.Apply(new ColorValue(4f, 12f, 1f, 1f, ColorSpaceMode.Linear, true));

        Assert.Equal(new ColorValue(4f, 8f, 1f, 1f, ColorSpaceMode.Linear, true), constrained);
        Assert.True(constrained.IsHdr);
    }

    [Fact]
    public void Constraints_ClampIntensity_UsesConfiguredRange()
    {
        ColorPickerConstraints constraints = new() { MinIntensity = 0.5f, MaxIntensity = 2f };

        Assert.Equal(0.5f, constraints.ClampIntensity(0f));
        Assert.Equal(1.5f, constraints.ClampIntensity(1.5f));
        Assert.Equal(2f, constraints.ClampIntensity(4f));
    }

    [Fact]
    public void Options_Defaults_TargetMvpPicker()
    {
        ColorPickerOptions options = new();

        Assert.True(options.ShowAlpha);
        Assert.True(options.ShowTextInput);
        Assert.False(options.IsHdr);
        Assert.Equal(ColorPickerMode.Hsv, options.PickerMode);
        Assert.Equal(ColorValueFormat.HexRgba, options.DisplayFormat);
        Assert.Equal(ColorEditCommitMode.Live, options.CommitMode);
        Assert.NotNull(options.Constraints);
    }

    [Fact]
    public void EventArgs_ExposeInitialPreviewAndCommittedValues()
    {
        ColorValue initial = new(0f, 0f, 0f);
        ColorValue preview = new(0.5f, 0.25f, 0f);
        ColorValue final = new(1f, 0.5f, 0f);

        ColorValueChangingEventArgs changing = new(initial, preview);
        ColorValueChangedEventArgs changed = new(preview, final);

        Assert.Equal(initial, changing.InitialValue);
        Assert.Equal(preview, changing.PreviewValue);
        Assert.Equal(preview, changed.PreviousValue);
        Assert.Equal(final, changed.NewValue);
    }
}