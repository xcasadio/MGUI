using Microsoft.Xna.Framework.Input;
using MGUI.Shared.Input.GamePad;
using MGUI.Shared.Input.Semantic;

namespace MGUI.Tests.Architecture;

public class InputSemanticMappingTests
{
    [Fact]
    public void Keyboard_Tab_Maps_To_NavigateNext_And_ShiftTab_To_NavigatePrevious()
    {
        Assert.True(InputActionMapper.TryMapKeyboardAction(Keys.Tab, false, out InputAction nextAction));
        Assert.True(InputActionMapper.TryMapKeyboardAction(Keys.Tab, true, out InputAction previousAction));

        Assert.Equal(InputAction.NavigateNext, nextAction);
        Assert.Equal(InputAction.NavigatePrevious, previousAction);
    }

    [Fact]
    public void Keyboard_Maps_Core_UI_Actions()
    {
        Assert.True(InputActionMapper.TryMapKeyboardAction(Keys.Enter, false, out InputAction submitAction));
        Assert.True(InputActionMapper.TryMapKeyboardAction(Keys.Escape, false, out InputAction cancelAction));
        Assert.True(InputActionMapper.TryMapKeyboardAction(Keys.Apps, false, out InputAction contextAction));

        Assert.Equal(InputAction.Submit, submitAction);
        Assert.Equal(InputAction.Cancel, cancelAction);
        Assert.Equal(InputAction.OpenContext, contextAction);
    }

    [Fact]
    public void GamePad_Maps_Core_UI_Actions_And_Pause()
    {
        Assert.True(InputActionMapper.TryMapGamePadAction(GamePadButton.A, out InputAction submitAction));
        Assert.True(InputActionMapper.TryMapGamePadAction(GamePadButton.Back, out InputAction cancelAction));
        Assert.True(InputActionMapper.TryMapGamePadAction(GamePadButton.RightTrigger, out InputAction incrementAction));
        Assert.True(InputActionMapper.TryMapGamePadAction(GamePadButton.Start, out InputAction pauseAction));

        Assert.Equal(InputAction.Submit, submitAction);
        Assert.Equal(InputAction.Cancel, cancelAction);
        Assert.Equal(InputAction.Increment, incrementAction);
        Assert.Equal(InputAction.Pause, pauseAction);
    }

    [Fact]
    public void InputActionContext_Preserves_Source_Phase_And_Repeat_Metadata()
    {
        InputActionContext context = new(InputActionSource.Keyboard, InputActionPhase.Repeated, TimeSpan.FromMilliseconds(12), true, Keys.Down);
        InputActionEvent actionEvent = new(InputAction.NavigateDown, context);

        Assert.Equal(InputActionSource.Keyboard, actionEvent.Context.Source);
        Assert.Equal(InputActionPhase.Repeated, actionEvent.Context.Phase);
        Assert.True(actionEvent.Context.IsRepeat);
        Assert.Equal(Keys.Down, actionEvent.Context.Key);
        Assert.Equal(InputAction.NavigateDown, actionEvent.Action);
    }
}