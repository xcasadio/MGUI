using System;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Shared.Input.Semantic
{
    public class InputRouter
    {
        private sealed record RegisteredContext(IInputContext Context, long RegistrationOrder);

        private readonly List<RegisteredContext> _Contexts = new();
        private long _NextRegistrationOrder;

        public IReadOnlyList<IInputContext> Contexts => _Contexts.Select(x => x.Context).ToList();

        public void RegisterContext(IInputContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_Contexts.Any(x => ReferenceEquals(x.Context, context)))
            {
                return;
            }

            _Contexts.Add(new(context, _NextRegistrationOrder++));
        }

        public bool UnregisterContext(IInputContext context)
        {
            if (context == null)
            {
                return false;
            }

            int index = _Contexts.FindIndex(x => ReferenceEquals(x.Context, context));
            if (index < 0)
            {
                return false;
            }

            _Contexts.RemoveAt(index);
            return true;
        }

        public InputRouteDecision Route(InputActionEvent actionEvent)
        {
            foreach (RegisteredContext entry in _Contexts
                .Where(x => x.Context.IsActive)
                .OrderByDescending(x => x.Context.Priority)
                .ThenBy(x => x.RegistrationOrder))
            {
                if (entry.Context.TryHandle(actionEvent, out InputCaptureResult result))
                {
                    InputCaptureResult normalizedResult = result.IsHandled
                        ? result
                        : InputCaptureResult.Handled(entry.Context.Name, "Handled");
                    return new(actionEvent, normalizedResult);
                }
            }

            return InputRouteDecision.Unhandled(actionEvent);
        }
    }
}