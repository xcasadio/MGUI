using Microsoft.Xna.Framework.Input;
using MGUI.Shared.Input.GamePad;

namespace MGUI.Shared.Input.Semantic
{
    public static class InputActionMapper
    {
        public static bool TryMapKeyboardAction(Keys key, bool isShiftDown, out InputAction action)
        {
            switch (key)
            {
                case Keys.Tab:
                    action = isShiftDown ? InputAction.NavigatePrevious : InputAction.NavigateNext;
                    return true;
                case Keys.Enter:
                case Keys.Space:
                    action = InputAction.Submit;
                    return true;
                case Keys.Escape:
                    action = InputAction.Cancel;
                    return true;
                case Keys.Up:
                    action = InputAction.NavigateUp;
                    return true;
                case Keys.Down:
                    action = InputAction.NavigateDown;
                    return true;
                case Keys.Left:
                    action = InputAction.NavigateLeft;
                    return true;
                case Keys.Right:
                    action = InputAction.NavigateRight;
                    return true;
                case Keys.Home:
                    action = InputAction.NavigateHome;
                    return true;
                case Keys.End:
                    action = InputAction.NavigateEnd;
                    return true;
                case Keys.PageUp:
                    action = InputAction.NavigatePageUp;
                    return true;
                case Keys.PageDown:
                    action = InputAction.NavigatePageDown;
                    return true;
                case Keys.Apps:
                    action = InputAction.OpenContext;
                    return true;
                default:
                    action = default;
                    return false;
            }
        }

        public static bool TryMapGamePadAction(GamePadButton button, out InputAction action)
        {
            switch (button)
            {
                case GamePadButton.A:
                    action = InputAction.Submit;
                    return true;
                case GamePadButton.B:
                case GamePadButton.Back:
                    action = InputAction.Cancel;
                    return true;
                case GamePadButton.DPadUp:
                case GamePadButton.LeftStickUp:
                    action = InputAction.NavigateUp;
                    return true;
                case GamePadButton.DPadDown:
                case GamePadButton.LeftStickDown:
                    action = InputAction.NavigateDown;
                    return true;
                case GamePadButton.DPadLeft:
                case GamePadButton.LeftStickLeft:
                    action = InputAction.NavigateLeft;
                    return true;
                case GamePadButton.DPadRight:
                case GamePadButton.LeftStickRight:
                    action = InputAction.NavigateRight;
                    return true;
                case GamePadButton.LeftShoulder:
                    action = InputAction.ShoulderPrevious;
                    return true;
                case GamePadButton.RightShoulder:
                    action = InputAction.ShoulderNext;
                    return true;
                case GamePadButton.LeftTrigger:
                    action = InputAction.Decrement;
                    return true;
                case GamePadButton.RightTrigger:
                    action = InputAction.Increment;
                    return true;
                case GamePadButton.X:
                    action = InputAction.OpenContext;
                    return true;
                case GamePadButton.Start:
                    action = InputAction.Pause;
                    return true;
                default:
                    action = default;
                    return false;
            }
        }
    }
}