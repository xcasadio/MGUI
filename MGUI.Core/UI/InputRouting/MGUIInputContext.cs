using MGUI.Shared.Input.Semantic;
using System;

namespace MGUI.Core.UI.InputRouting
{
    public class MGUIInputContext : IInputContext
    {
        private MGDesktop Desktop { get; }

        public string Name => "MGUI.UI";
        public int Priority { get; }
        public bool IsActive => Desktop != null;

        public MGUIInputContext(MGDesktop desktop, int priority = 100)
        {
            Desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
            Priority = priority;
        }

        public bool TryHandle(InputActionEvent actionEvent, out InputCaptureResult result)
        {
            if (actionEvent.Action.IsUIAction())
            {
                bool handled = Desktop.TryHandleInputAction(actionEvent);
                result = InputCaptureResult.Handled(Name, handled ? "Handled by MGDesktop" : "Reserved for UI routing");
                return true;
            }

            if (Desktop.ShouldCaptureGameplayInput())
            {
                result = InputCaptureResult.Handled(Name, "Gameplay blocked by active UI capture state");
                return true;
            }

            result = InputCaptureResult.Ignored(Name, "Gameplay allowed to fall through");
            return false;
        }
    }
}