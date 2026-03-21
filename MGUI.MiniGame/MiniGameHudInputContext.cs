using System;
using MGUI.Shared.Input.Semantic;

namespace MGUI.MiniGame;

internal sealed class MiniGameHudInputContext : IInputContext
{
    private readonly Func<bool> _isActive;
    private readonly Func<InputActionEvent, bool> _tryHandle;

    public string Name => "MiniGame.HUD";
    public int Priority { get; }
    public bool IsActive => _isActive();

    public MiniGameHudInputContext(Func<InputActionEvent, bool> tryHandle, int priority = 50, Func<bool> isActive = null)
    {
        _tryHandle = tryHandle ?? throw new ArgumentNullException(nameof(tryHandle));
        _isActive = isActive ?? (() => true);
        Priority = priority;
    }

    public bool TryHandle(InputActionEvent actionEvent, out InputCaptureResult result)
    {
        bool handled = _tryHandle(actionEvent);
        result = handled
            ? InputCaptureResult.Handled(Name, "Handled by mini-game HUD context")
            : InputCaptureResult.Ignored(Name, "HUD context ignored the action");
        return handled;
    }
}