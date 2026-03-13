using System;
using System.Collections.Generic;

namespace MGUI.Core.UI.Styling
{
    public sealed class MGControlTemplateContext
    {
        public MGElement Owner { get; }
        public MGWindow Window => Owner?.SelfOrParentWindow;
        public bool IsThemeRefresh { get; }

        public MGControlTemplateContext(MGElement Owner, bool IsThemeRefresh = false)
        {
            this.Owner = Owner;
            this.IsThemeRefresh = IsThemeRefresh;
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

        public void ApplyThemeDefault<T>(string Name, T Value, Func<T> GetCurrentValue, Action<T> SetValue, IEqualityComparer<T> Comparer = null)
        {
            if (Owner == null)
            {
                throw new InvalidOperationException($"{nameof(ApplyThemeDefault)} requires a non-null owner.");
            }

            if (GetCurrentValue == null)
            {
                throw new ArgumentNullException(nameof(GetCurrentValue));
            }

            if (SetValue == null)
            {
                throw new ArgumentNullException(nameof(SetValue));
            }

            Comparer ??= EqualityComparer<T>.Default;
            T CurrentValue = GetCurrentValue();
            bool HasPrevious = Owner.TryGetAppliedTemplateDefault(Name, out T PreviousValue);
            if (!IsThemeRefresh || !HasPrevious || Comparer.Equals(CurrentValue, PreviousValue))
            {
                SetValue(Value);
                Owner.SetAppliedTemplateDefault(Name, Value);
            }
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

        public void Apply(MGElement Owner, bool IsThemeRefresh = false)
        {
            if (Owner == null)
            {
                throw new ArgumentNullException(nameof(Owner));
            }

            Apply(new MGControlTemplateContext(Owner, IsThemeRefresh));
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