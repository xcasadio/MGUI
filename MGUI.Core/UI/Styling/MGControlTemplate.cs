using System;

namespace MGUI.Core.UI.Styling
{
    public sealed class MGControlTemplateContext
    {
        public MGElement Owner { get; }
        public MGWindow Window => Owner?.SelfOrParentWindow;

        public MGControlTemplateContext(MGElement Owner)
        {
            this.Owner = Owner;
        }

        public bool TryGetPart(string Name, out MGElement Part)
        {
            if (Owner?.TryGetTemplatePart(Name, out Part) == true)
            {
                return true;
            }

            Part = null;
            return false;
        }

        public T GetRequiredPart<T>(string Name) where T : MGElement
        {
            if (!TryGetPart(Name, out MGElement Part) || Part is not T TypedPart)
            {
                throw new InvalidOperationException($"Template part '{Name}' was not found on '{Owner?.GetType().Name ?? nameof(MGElement)}'.");
            }

            return TypedPart;
        }
    }

    public sealed class MGControlTemplate
    {
        public string Name { get; }
        private Action<MGControlTemplateContext> ApplyAction { get; }

        public MGControlTemplate(string Name, Action<MGControlTemplateContext> ApplyAction)
        {
            this.Name = Name ?? throw new ArgumentNullException(nameof(Name));
            this.ApplyAction = ApplyAction ?? throw new ArgumentNullException(nameof(ApplyAction));
        }

        public void Apply(MGElement Owner)
        {
            if (Owner == null)
            {
                throw new ArgumentNullException(nameof(Owner));
            }

            Apply(new MGControlTemplateContext(Owner));
        }

        public void Apply(MGControlTemplateContext Context)
        {
            if (Context == null)
            {
                throw new ArgumentNullException(nameof(Context));
            }

            ApplyAction(Context);
        }
    }
}