using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Focus;

public class NumericUpDownInputTests
{
    [Theory]
    [InlineData(Keys.Up, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.Increase)]
    [InlineData(Keys.Down, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.Decrease)]
    [InlineData(Keys.PageUp, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.IncreaseLarge)]
    [InlineData(Keys.PageDown, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.DecreaseLarge)]
    [InlineData(Keys.Home, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.SetMinimum)]
    [InlineData(Keys.End, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.SetMaximum)]
    [InlineData(Keys.Enter, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.CommitText)]
    public void GetKeyboardAdjustmentAction_MapsExpectedKeys(Keys key, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction expected)
    {
        MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction actual = MGUI.Core.UI.MGNumericUpDown.GetKeyboardAdjustmentAction(key);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetKeyboardAdjustmentAction_UnknownKey_ReturnsNone()
    {
        MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction actual = MGUI.Core.UI.MGNumericUpDown.GetKeyboardAdjustmentAction(Keys.A);

        Assert.Equal(MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.None, actual);
    }

    [Theory]
    [InlineData(MGUI.Core.UI.UINavigationAction.MoveUp, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.Increase)]
    [InlineData(MGUI.Core.UI.UINavigationAction.MoveDown, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.Decrease)]
    [InlineData(MGUI.Core.UI.UINavigationAction.Increment, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.Increase)]
    [InlineData(MGUI.Core.UI.UINavigationAction.Decrement, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.Decrease)]
    [InlineData(MGUI.Core.UI.UINavigationAction.PageUp, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.IncreaseLarge)]
    [InlineData(MGUI.Core.UI.UINavigationAction.PageDown, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.DecreaseLarge)]
    [InlineData(MGUI.Core.UI.UINavigationAction.Home, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.SetMinimum)]
    [InlineData(MGUI.Core.UI.UINavigationAction.End, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.SetMaximum)]
    [InlineData(MGUI.Core.UI.UINavigationAction.Submit, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.CommitText)]
    public void GetNavigationAdjustmentAction_MapsExpectedActions(MGUI.Core.UI.UINavigationAction action, MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction expected)
    {
        MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction actual = MGUI.Core.UI.MGNumericUpDown.GetNavigationAdjustmentAction(action);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetNavigationAdjustmentAction_UnsupportedAction_ReturnsNone()
    {
        MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction actual = MGUI.Core.UI.MGNumericUpDown.GetNavigationAdjustmentAction(MGUI.Core.UI.UINavigationAction.MoveLeft);

        Assert.Equal(MGUI.Core.UI.MGNumericUpDown.NumericAdjustmentAction.None, actual);
    }
}