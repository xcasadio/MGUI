using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MGUI.Shared.Input.GamePad;
using System;

namespace MGUI.Shared.Input.Semantic
{
    public enum InputActionSource
    {
        Keyboard,
        Mouse,
        GamePad,
        Programmatic,
    }

    public enum InputActionPhase
    {
        Pressed,
        Released,
        Repeated,
    }

    public readonly record struct InputActionContext(
        InputActionSource Source,
        InputActionPhase Phase,
        TimeSpan Timestamp,
        bool IsRepeat = false,
        Keys? Key = null,
        GamePadButton? GamePadButton = null,
        Point? PointerPosition = null,
        int ScrollDelta = 0);

    public readonly record struct InputActionEvent(InputAction Action, InputActionContext Context);
}