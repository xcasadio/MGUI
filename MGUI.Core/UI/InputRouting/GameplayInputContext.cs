using MGUI.Shared.Input.Semantic;

namespace MGUI.Core.UI.InputRouting;

public class GameplayInputContext : IInputContext
{
    private readonly Func<bool> _isActive;
    private readonly Func<InputActionEvent, bool> _tryHandle;

    public string Name { get; }
    public int Priority { get; }
    public bool IsActive => _isActive();

    public GameplayInputContext(string name, Func<InputActionEvent, bool> tryHandle, int priority = 0, Func<bool> isActive = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A context name is required.", nameof(name));
        }

        Name = name;
        Priority = priority;
        _tryHandle = tryHandle ?? throw new ArgumentNullException(nameof(tryHandle));
        _isActive = isActive ?? (() => true);
    }

    public bool TryHandle(InputActionEvent actionEvent, out InputCaptureResult result)
    {
        if (_tryHandle(actionEvent))
        {
            result = InputCaptureResult.Handled(Name, "Handled by gameplay fallback");
            return true;
        }

        result = InputCaptureResult.Ignored(Name, "Gameplay fallback ignored the action");
        return false;
    }
}