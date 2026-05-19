using MGUI.Core.UI;

namespace MGUI.Tests.ColorPicker;

public class ColorFieldTests
{
    [Fact]
    public void TrySetValue_ClearsMixedAndRaisesChanged()
    {
        MGColorFieldModel model = new(new ColorValue(1f, 0f, 0f, 1f)) { IsMixed = true };
        ColorFieldValueChangedEventArgs? changed = null;
        model.ValueChanged += (sender, e) => changed = e;

        Assert.True(model.TrySetValue(new ColorValue(0f, 1f, 0f, 1f)));

        Assert.False(model.IsMixed);
        Assert.Equal(new ColorValue(0f, 1f, 0f, 1f), model.Value);
        Assert.NotNull(changed);
    }

    [Fact]
    public void TrySetValue_FromMixedRaisesChangedEvenWhenFallbackMatches()
    {
        ColorValue fallback = new(1f, 0f, 0f, 1f);
        MGColorFieldModel model = new(fallback);
        model.SetMixedValue(fallback);
        int changedCount = 0;
        model.ValueChanged += (sender, e) => changedCount++;

        Assert.True(model.TrySetValue(fallback));

        Assert.False(model.IsMixed);
        Assert.Equal(fallback, model.Value);
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void SetMixedValue_DoesNotCommitValue()
    {
        MGColorFieldModel model = new(new ColorValue(1f, 0f, 0f, 1f));
        int changedCount = 0;
        model.ValueChanged += (sender, e) => changedCount++;

        model.SetMixedValue(new ColorValue(0f, 1f, 0f, 1f));

        Assert.True(model.IsMixed);
        Assert.Equal(0, changedCount);
        Assert.Equal("Mixed", model.GetDisplayText(ColorValueFormat.HexRgba));
    }

    [Fact]
    public void TrySetValue_RejectsNullWhenNotAllowed()
    {
        MGColorFieldModel model = new(new ColorValue(1f, 0f, 0f, 1f)) { AllowNull = false };

        Assert.False(model.TrySetValue(null));

        Assert.Equal(new ColorValue(1f, 0f, 0f, 1f), model.Value);
    }

    [Fact]
    public void TrySetValue_AllowsNullWhenAllowed()
    {
        MGColorFieldModel model = new(new ColorValue(1f, 0f, 0f, 1f)) { AllowNull = true };

        Assert.True(model.TrySetValue(null));

        Assert.Null(model.Value);
        Assert.Equal("Null", model.GetDisplayText(ColorValueFormat.HexRgba));
    }

    [Fact]
    public void ResetToDefault_AppliesDefaultValue()
    {
        MGColorFieldModel model = new(new ColorValue(1f, 0f, 0f, 1f))
        {
            DefaultValue = new ColorValue(0f, 0f, 1f, 1f),
        };

        Assert.True(model.ResetToDefault());

        Assert.Equal(new ColorValue(0f, 0f, 1f, 1f), model.Value);
    }

    [Fact]
    public void IsReadOnly_BlocksEditsAndReset()
    {
        MGColorFieldModel model = new(new ColorValue(1f, 0f, 0f, 1f))
        {
            DefaultValue = new ColorValue(0f, 0f, 1f, 1f),
            IsReadOnly = true,
        };

        Assert.False(model.TrySetValue(new ColorValue(0f, 1f, 0f, 1f)));
        Assert.False(model.ResetToDefault());
        Assert.Equal(new ColorValue(1f, 0f, 0f, 1f), model.Value);
    }
}