using System;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Core.UI.Styling
{
    public readonly record struct MGControlTemplatePartRequirement(string Name, Type PartType, bool IsRequired = true);

    /// <summary>Represents the visual structure materialized by a <see cref="MGControlTemplate"/>.
    /// The structure phase creates elements and names the parts that the owner will later attach and consume.</summary>
    public sealed class MGControlTemplateStructure
    {
        public MGElement Root { get; }
        public IReadOnlyDictionary<string, MGElement> Parts => _Parts;
        public IReadOnlyList<MGElement> DetachedRoots => _DetachedRoots;

        private readonly Dictionary<string, MGElement> _Parts;
        private readonly List<MGElement> _DetachedRoots;

        public MGControlTemplateStructure(MGElement Root)
            : this(Root, null, null)
        {
        }

        public MGControlTemplateStructure(MGElement Root, IReadOnlyDictionary<string, MGElement> Parts)
            : this(Root, Parts, null)
        {
        }

        public MGControlTemplateStructure(MGElement Root, IReadOnlyDictionary<string, MGElement> Parts, IReadOnlyList<MGElement> DetachedRoots)
        {
            this.Root = Root;
            _Parts = Parts == null ? new(StringComparer.Ordinal) : new(Parts, StringComparer.Ordinal);
            _DetachedRoots = DetachedRoots == null ? new() : new(DetachedRoots.Where(x => x != null));
        }

        public void AddPart(string Name, MGElement Part)
        {
            if (!string.IsNullOrWhiteSpace(Name) && Part != null)
            {
                _Parts[Name] = Part;
            }
        }

        public bool TryGetPart(string Name, out MGElement Part) => _Parts.TryGetValue(Name, out Part);
    }

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
                string availableParts = Owner?.TemplateParts?.Any() == true
                    ? string.Join(", ", Owner.TemplateParts.Select(x => $"{x.Key}:{x.Value?.GetType().Name ?? nameof(MGElement)}"))
                    : "<none>";
                string actualType = Part?.GetType().Name ?? "<missing>";
                throw new InvalidOperationException(
                    $"Template part '{Name}' expected type '{typeof(T).Name}' on '{Owner?.GetType().Name ?? nameof(MGElement)}', but resolved '{actualType}'. Available parts: {availableParts}.");
            }

            return TypedPart;
        }

        public void ApplyThemeDefault<T>(string Name, T Value, Func<T> GetCurrentValue, Action<T> SetValue, IEqualityComparer<T> Comparer = null)
            => ApplyTemplateValue(Name, Value, GetCurrentValue, SetValue, UIInvalidationKind.Draw, Comparer);

        public void ApplyTemplateValue<T>(string Name, T Value, Func<T> GetCurrentValue, Action<T> SetValue,
            UIInvalidationKind Invalidation = UIInvalidationKind.Draw, IEqualityComparer<T> Comparer = null)
        {
            if (Owner == null)
            {
                throw new InvalidOperationException($"{nameof(ApplyTemplateValue)} requires a non-null owner.");
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
            bool HasPrevious = Owner.TryGetAppliedTemplateDefault(Name, out UIResolvedValue<T> PreviousValue);
            if (!IsThemeRefresh || !HasPrevious || Comparer.Equals(CurrentValue, PreviousValue.Value))
            {
                SetValue(Value);
                Owner.SetAppliedTemplateDefault(Name, new UIResolvedValue<T>(Value, UIValueResolutionSource.Template(Invalidation, Name)));

                if ((Invalidation & (UIInvalidationKind.Measure | UIInvalidationKind.Arrange | UIInvalidationKind.Structure)) != 0)
                {
                    Owner.InvalidateLayout();
                }
            }
        }
    }

    public sealed class MGControlTemplate
    {
        public string Name { get; }
        private Func<MGControlTemplateContext, MGControlTemplateStructure> CreateStructureAction { get; }
        private Action<MGControlTemplateContext, MGControlTemplateStructure> AttachStructureAction { get; }
        private Action<MGControlTemplateContext> ApplyDefaultsAction { get; }

        /// <summary>True when the template can create a visual structure instead of only applying defaults to pre-existing parts.</summary>
        public bool SupportsStructure => CreateStructureAction != null;

        /// <summary>True when the template can attach a created structure to the owner.
        /// Attachment is intentionally separate from structure creation so templates can be parsed or cached without mutating the live visual tree.</summary>
        public bool SupportsAttachment => AttachStructureAction != null;

        public MGControlTemplate(string Name, Action<MGControlTemplateContext> ApplyAction)
            : this(Name, null, null, ApplyAction)
        {
        }

        public MGControlTemplate(string Name,
            Func<MGControlTemplateContext, MGControlTemplateStructure> CreateStructure,
            Action<MGControlTemplateContext, MGControlTemplateStructure> AttachStructure,
            Action<MGControlTemplateContext> ApplyDefaults)
        {
            this.Name = Name ?? throw new ArgumentNullException(nameof(Name));
            CreateStructureAction = CreateStructure;
            AttachStructureAction = AttachStructure;
            ApplyDefaultsAction = ApplyDefaults ?? throw new ArgumentNullException(nameof(ApplyDefaults));
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

            ApplyDefaults(Context);
        }

        /// <summary>Creates the template structure for a control instance.
        /// This phase must be side-effect free with regard to the owner's live visual tree.</summary>
        public MGControlTemplateStructure CreateStructure(MGControlTemplateContext Context)
        {
            if (Context == null)
            {
                throw new ArgumentNullException(nameof(Context));
            }

            return CreateStructureAction?.Invoke(Context);
        }

        /// <summary>Attaches a previously created structure to the owner.
        /// Theme refreshes should not call this unless the template identity actually changed.</summary>
        public void AttachStructure(MGControlTemplateContext Context, MGControlTemplateStructure Structure)
        {
            if (Context == null)
            {
                throw new ArgumentNullException(nameof(Context));
            }

            if (Structure == null)
            {
                throw new ArgumentNullException(nameof(Structure));
            }

            AttachStructureAction?.Invoke(Context, Structure);
        }

        /// <summary>Applies chrome defaults onto the owner and its parts.
        /// This phase is safe to re-run during theme refreshes and remains the compatibility path for the existing catalog.</summary>
        public void ApplyDefaults(MGControlTemplateContext Context)
        {
            if (Context == null)
            {
                throw new ArgumentNullException(nameof(Context));
            }

            ApplyDefaultsAction(Context);
        }
    }
}