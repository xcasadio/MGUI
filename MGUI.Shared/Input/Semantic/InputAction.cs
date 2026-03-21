using System;

namespace MGUI.Shared.Input.Semantic
{
    public enum InputAction
    {
        Submit,
        Cancel,
        NavigateNext,
        NavigatePrevious,
        NavigateUp,
        NavigateDown,
        NavigateLeft,
        NavigateRight,
        NavigateHome,
        NavigateEnd,
        NavigatePageUp,
        NavigatePageDown,
        OpenContext,
        Increment,
        Decrement,
        ShoulderPrevious,
        ShoulderNext,
        GameplayPrimary,
        GameplaySecondary,
        GameplayCyclePrevious,
        GameplayCycleNext,
        GameplayQuickUse,
        GameplayToggleHelp,
        Pause,
    }

    public static class InputActionExtensions
    {
        public static bool IsUIAction(this InputAction action)
            => action switch
            {
                InputAction.GameplayPrimary => false,
                InputAction.GameplaySecondary => false,
                InputAction.GameplayCyclePrevious => false,
                InputAction.GameplayCycleNext => false,
                InputAction.GameplayQuickUse => false,
                InputAction.GameplayToggleHelp => false,
                InputAction.Pause => false,
                _ => true,
            };

        public static bool IsGameplayAction(this InputAction action)
            => !action.IsUIAction();
    }
}