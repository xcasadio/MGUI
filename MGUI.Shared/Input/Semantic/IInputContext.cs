namespace MGUI.Shared.Input.Semantic
{
    public interface IInputContext
    {
        string Name { get; }
        int Priority { get; }
        bool IsActive { get; }

        bool TryHandle(InputActionEvent actionEvent, out InputCaptureResult result);
    }
}