using MGUI.Core.UI;

namespace MGUI.Tests.ColorPicker;

public class ColorEditCommitModeTests
{
    [Fact]
    public void Live_PreviewCommitsImmediately()
    {
        MGColorPickerModel model = new(new ColorValue(1f, 0f, 0f, 1f), new ColorPickerConstraints())
        {
            CommitMode = ColorEditCommitMode.Live,
        };
        List<string> events = new();
        model.ValueChanging += (sender, e) => events.Add($"changing:{e.PreviewValue.ToHex(ColorValueFormat.HexRgba)}");
        model.ValueChanged += (sender, e) => events.Add($"changed:{e.NewValue.ToHex(ColorValueFormat.HexRgba)}");

        model.BeginEdit();
        model.SetHue(120f);

        Assert.Equal(new ColorValue(0f, 1f, 0f, 1f), model.Value);
        Assert.Equal(model.Value, model.CommittedValue);
        Assert.Equal(new[] { "changing:#00FF00FF", "changed:#00FF00FF" }, events);
    }

    [Fact]
    public void OnMouseRelease_PreviewsThenCommitsOnce()
    {
        MGColorPickerModel model = new(new ColorValue(1f, 0f, 0f, 1f), new ColorPickerConstraints())
        {
            CommitMode = ColorEditCommitMode.OnMouseRelease,
        };
        int changingCount = 0;
        int changedCount = 0;
        int committedCount = 0;
        model.ValueChanging += (sender, e) => changingCount++;
        model.ValueChanged += (sender, e) => changedCount++;
        model.EditCommitted += (sender, e) => committedCount++;

        model.BeginEdit();
        model.SetHue(120f);
        model.SetAlpha(0.5f);

        Assert.Equal(2, changingCount);
        Assert.Equal(0, changedCount);
        Assert.NotEqual(model.Value, model.CommittedValue);

        Assert.True(model.CommitEdit());

        Assert.Equal(1, changedCount);
        Assert.Equal(1, committedCount);
        Assert.Equal(model.Value, model.CommittedValue);
    }

    [Fact]
    public void ExplicitOkCancel_CancelRestoresInitialValue()
    {
        ColorValue initial = new(1f, 0f, 0f, 1f);
        MGColorPickerModel model = new(initial, new ColorPickerConstraints())
        {
            CommitMode = ColorEditCommitMode.ExplicitOkCancel,
        };
        bool cancelled = false;
        int changedCount = 0;
        model.ValueChanged += (sender, e) => changedCount++;
        model.EditCancelled += (sender, e) => cancelled = true;

        model.BeginEdit();
        model.SetHue(120f);

        Assert.Equal(new ColorValue(0f, 1f, 0f, 1f), model.Value);
        Assert.True(model.CancelEdit());

        Assert.True(cancelled);
        Assert.Equal(0, changedCount);
        Assert.Equal(initial, model.Value);
        Assert.Equal(initial, model.CommittedValue);
    }

    [Fact]
    public void ExplicitOkCancel_CommitRaisesChangedAndCommitted()
    {
        MGColorPickerModel model = new(new ColorValue(1f, 0f, 0f, 1f), new ColorPickerConstraints())
        {
            CommitMode = ColorEditCommitMode.ExplicitOkCancel,
        };
        List<string> events = new();
        model.EditStarted += (sender, e) => events.Add("started");
        model.ValueChanging += (sender, e) => events.Add("changing");
        model.ValueChanged += (sender, e) => events.Add("changed");
        model.EditCommitted += (sender, e) => events.Add("committed");

        model.BeginEdit();
        model.SetHue(120f);
        model.CommitEdit();

        Assert.Equal(new[] { "started", "changing", "changed", "committed" }, events);
        Assert.Equal(model.Value, model.CommittedValue);
    }
}