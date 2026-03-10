using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MGUI.Shared.Input.GamePad
{
    public enum GamePadButton
    {
        A,
        B,
        X,
        Y,
        DPadUp,
        DPadDown,
        DPadLeft,
        DPadRight,
        LeftStickUp,
        LeftStickDown,
        LeftStickLeft,
        LeftStickRight,
        LeftShoulder,
        RightShoulder,
        LeftTrigger,
        RightTrigger,
        Start,
        Back
    }

    public class GamePadTracker
    {
        public static readonly ReadOnlyCollection<GamePadButton> AllButtons = Enum.GetValues(typeof(GamePadButton)).Cast<GamePadButton>().ToList().AsReadOnly();

        public InputTracker InputTracker { get; }

        public PlayerIndex PlayerIndex { get; set; } = PlayerIndex.One;
        public GamePadDeadZone DeadZone { get; set; } = GamePadDeadZone.IndependentAxes;
        public float ThumbstickThreshold { get; set; } = 0.5f;
        public float TriggerThreshold { get; set; } = 0.5f;
        public TimeSpan InitialRepeatDelay { get; set; } = TimeSpan.FromMilliseconds(350);
        public TimeSpan RepeatInterval { get; set; } = TimeSpan.FromMilliseconds(120);

        public GamePadState PreviousState { get; private set; }
        public GamePadState CurrentState { get; private set; }
        public bool IsConnected => CurrentState.IsConnected;

        private readonly Dictionary<GamePadButton, bool> _CurrentTriggeredButtons = AllButtons.ToDictionary(x => x, _ => false);
        public IReadOnlyDictionary<GamePadButton, bool> CurrentTriggeredButtons => _CurrentTriggeredButtons;

        private readonly Dictionary<GamePadButton, TimeSpan?> _HeldSince = AllButtons.ToDictionary(x => x, _ => (TimeSpan?)null);
        private readonly Dictionary<GamePadButton, TimeSpan?> _LastRepeatAt = AllButtons.ToDictionary(x => x, _ => (TimeSpan?)null);

        internal GamePadTracker(InputTracker inputTracker)
        {
            InputTracker = inputTracker;
        }

        public bool IsPressed(GamePadButton button) => IsPressed(CurrentState, button, ThumbstickThreshold, TriggerThreshold);
        public bool WasPressed(GamePadButton button) => IsPressed(button) && !IsPressed(PreviousState, button, ThumbstickThreshold, TriggerThreshold);
        public bool WasReleased(GamePadButton button) => !IsPressed(button) && IsPressed(PreviousState, button, ThumbstickThreshold, TriggerThreshold);
        public bool WasTriggered(GamePadButton button) => _CurrentTriggeredButtons[button];
        public bool HasActivity() => _CurrentTriggeredButtons.Values.Any(x => x);

        internal void Update(UpdateBaseArgs BA)
        {
            PreviousState = CurrentState;
            CurrentState = Microsoft.Xna.Framework.Input.GamePad.GetState(PlayerIndex, DeadZone);

            foreach (GamePadButton button in AllButtons)
                _CurrentTriggeredButtons[button] = false;

            if (!CurrentState.IsConnected)
            {
                foreach (GamePadButton button in AllButtons)
                {
                    _HeldSince[button] = null;
                    _LastRepeatAt[button] = null;
                }

                return;
            }

            foreach (GamePadButton button in AllButtons)
            {
                bool isPressed = IsPressed(CurrentState, button, ThumbstickThreshold, TriggerThreshold);
                bool wasPressed = IsPressed(PreviousState, button, ThumbstickThreshold, TriggerThreshold);

                if (isPressed && !wasPressed)
                {
                    _HeldSince[button] = BA.TotalElapsed;
                    _LastRepeatAt[button] = null;
                    _CurrentTriggeredButtons[button] = true;
                }
                else if (!isPressed)
                {
                    _HeldSince[button] = null;
                    _LastRepeatAt[button] = null;
                }
                else if (_HeldSince[button].HasValue && IsRepeatDue(BA.TotalElapsed, _HeldSince[button].Value, _LastRepeatAt[button], InitialRepeatDelay, RepeatInterval))
                {
                    _LastRepeatAt[button] = BA.TotalElapsed;
                    _CurrentTriggeredButtons[button] = true;
                }
            }
        }

        public static bool IsRepeatDue(TimeSpan now, TimeSpan heldSince, TimeSpan? lastRepeatAt, TimeSpan initialRepeatDelay, TimeSpan repeatInterval)
        {
            if (now - heldSince < initialRepeatDelay)
                return false;

            return !lastRepeatAt.HasValue || now - lastRepeatAt.Value >= repeatInterval;
        }

        public static bool IsPressed(GamePadState state, GamePadButton button, float thumbstickThreshold = 0.5f, float triggerThreshold = 0.5f)
            => button switch
            {
                GamePadButton.A => state.Buttons.A == ButtonState.Pressed,
                GamePadButton.B => state.Buttons.B == ButtonState.Pressed,
                GamePadButton.X => state.Buttons.X == ButtonState.Pressed,
                GamePadButton.Y => state.Buttons.Y == ButtonState.Pressed,
                GamePadButton.DPadUp => state.DPad.Up == ButtonState.Pressed,
                GamePadButton.DPadDown => state.DPad.Down == ButtonState.Pressed,
                GamePadButton.DPadLeft => state.DPad.Left == ButtonState.Pressed,
                GamePadButton.DPadRight => state.DPad.Right == ButtonState.Pressed,
                GamePadButton.LeftStickUp => state.ThumbSticks.Left.Y >= thumbstickThreshold,
                GamePadButton.LeftStickDown => state.ThumbSticks.Left.Y <= -thumbstickThreshold,
                GamePadButton.LeftStickLeft => state.ThumbSticks.Left.X <= -thumbstickThreshold,
                GamePadButton.LeftStickRight => state.ThumbSticks.Left.X >= thumbstickThreshold,
                GamePadButton.LeftShoulder => state.Buttons.LeftShoulder == ButtonState.Pressed,
                GamePadButton.RightShoulder => state.Buttons.RightShoulder == ButtonState.Pressed,
                GamePadButton.LeftTrigger => state.Triggers.Left >= triggerThreshold,
                GamePadButton.RightTrigger => state.Triggers.Right >= triggerThreshold,
                GamePadButton.Start => state.Buttons.Start == ButtonState.Pressed,
                GamePadButton.Back => state.Buttons.Back == ButtonState.Pressed,
                _ => false
            };
    }
}