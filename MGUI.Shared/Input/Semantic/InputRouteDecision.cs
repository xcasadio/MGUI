namespace MGUI.Shared.Input.Semantic
{
    public readonly record struct InputCaptureResult(string ContextName, bool IsHandled, string Reason)
    {
        public static InputCaptureResult Handled(string contextName, string reason)
            => new(contextName, true, reason);

        public static InputCaptureResult Ignored(string contextName, string reason)
            => new(contextName, false, reason);
    }

    public readonly record struct InputRouteDecision(InputActionEvent ActionEvent, InputCaptureResult Result)
    {
        public bool IsHandled => Result.IsHandled;

        public static InputRouteDecision Unhandled(InputActionEvent actionEvent)
            => new(actionEvent, InputCaptureResult.Ignored(string.Empty, "Unhandled"));
    }
}