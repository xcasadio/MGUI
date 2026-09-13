using MGUI.Shared.Input.Semantic;

namespace MGUI.Core.UI.InputRouting;

/// <summary>
/// A reusable, selectively-active HUD input context: it must consume only the actions that
/// target an actual HUD widget, never ambient input such as movement, and it must fall through
/// completely while inactive. Meant to sit between a <see cref="MGUIInputContext"/> (higher
/// priority) and a <see cref="GameplayInputContext"/> (lower priority) so an integrator can
/// compose UI &gt; HUD &gt; gameplay without writing its own context type.
/// </summary>
public class HudInputContext : IInputContext
{
    private readonly Func<bool> _isActive;
    private readonly Func<InputActionEvent, bool> _tryHandle;

    public string Name { get; }
    public int Priority { get; }
    public bool IsActive => _isActive();

    public HudInputContext(string name, Func<InputActionEvent, bool> tryHandle, Func<bool> isActive, int priority = 50)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A context name is required.", nameof(name));
        }

        Name = name;
        Priority = priority;
        _tryHandle = tryHandle ?? throw new ArgumentNullException(nameof(tryHandle));
        _isActive = isActive ?? throw new ArgumentNullException(nameof(isActive));
    }

    public bool TryHandle(InputActionEvent actionEvent, out InputCaptureResult result)
    {
        if (_tryHandle(actionEvent))
        {
            result = InputCaptureResult.Handled(Name, "Handled by HUD context");
            return true;
        }

        result = InputCaptureResult.Ignored(Name, "HUD context ignored the action");
        return false;
    }
}