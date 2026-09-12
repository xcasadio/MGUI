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

        /// <summary>The pilot values the XAML definition of the template declared on the elements of this structure (backlog task 15), applied again
        /// after the template's defaults by <c>MGElement.ApplyControlTemplate</c>. Empty for a structure created by code.</summary>
        internal IReadOnlyList<UITemplateDeclaredValue> DeclaredValues => _DeclaredValues;

        private readonly Dictionary<string, MGElement> _Parts;
        private readonly List<MGElement> _DetachedRoots;
        private readonly List<UITemplateDeclaredValue> _DeclaredValues = new();

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

        internal void AddDeclaredValue(UITemplateDeclaredValue Value)
        {
            if (Value.Target != null)
            {
                _DeclaredValues.Add(Value);
            }
        }
    }

    public sealed class MGControlTemplateContext
    {
        public MGElement Owner { get; }
        public MGWindow Window => Owner?.SelfOrParentWindow;
        public bool IsThemeRefresh { get; }

        /// <summary>True when the owner instantiated a new template structure just before this application, such as a theme change that maps the
        /// control to a template with another structure: every part is a new element that holds the values of its construction. A part default
        /// is then applied even during a theme refresh, and the initialisation a template does once per structure runs again.</summary>
        public bool IsStructureRebuilt { get; }

        public MGControlTemplateContext(MGElement Owner, bool IsThemeRefresh = false, bool IsStructureRebuilt = false)
        {
            this.Owner = Owner;
            this.IsThemeRefresh = IsThemeRefresh;
            this.IsStructureRebuilt = IsStructureRebuilt;
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

        /// <summary>Tagged overload of <see cref="ApplyThemeDefault{T}(string, T, Func{T}, Action{T}, IEqualityComparer{T})"/> (ADR-0005):
        /// <paramref name="SetValue"/> receives the exact <see cref="UIValueResolutionSource"/> (<c>Template</c>) this call
        /// records, so the target can route the write through its tagged setter instead of its public one.</summary>
        public void ApplyThemeDefault<T>(string Name, T Value, Func<T> GetCurrentValue, Action<T, UIValueResolutionSource> SetValue,
            UIInvalidationKind Invalidation = UIInvalidationKind.Draw, IEqualityComparer<T> Comparer = null)
            => ApplyTemplateValue(Name, Value, GetCurrentValue, SetValue, Invalidation, Comparer);

        /// <summary>Overload of <see cref="ApplyThemeDefault{T}(string, T, Func{T}, Action{T}, IEqualityComparer{T})"/> for a value that is not a pilot
        /// property but still changes layout (a min width, a panel spacing, an indent, an alignment): <paramref name="Invalidation"/> is recorded with the
        /// value and requested from the owner when an application changes it. See <see cref="UIThemeValueInvalidation"/> (backlog task 7).</summary>
        public void ApplyThemeDefault<T>(string Name, T Value, Func<T> GetCurrentValue, Action<T> SetValue,
            UIInvalidationKind Invalidation, IEqualityComparer<T> Comparer = null)
            => ApplyTemplateValue(Name, Value, GetCurrentValue, SetValue, Invalidation, Comparer);

        public void ApplyTemplateValue<T>(string Name, T Value, Func<T> GetCurrentValue, Action<T> SetValue,
            UIInvalidationKind Invalidation = UIInvalidationKind.Draw, IEqualityComparer<T> Comparer = null)
            => ApplyTemplateValueCore(Name, Value, GetCurrentValue, SetValue == null ? null : (v, _) => SetValue(v), Invalidation, Comparer, UIValueSourceKind.Template);

        /// <summary>Tagged overload of <see cref="ApplyTemplateValue{T}(string, T, Func{T}, Action{T}, UIInvalidationKind, IEqualityComparer{T})"/>
        /// (ADR-0005): <paramref name="SetValue"/> receives the exact <see cref="UIValueResolutionSource"/> (<c>Template</c>) this
        /// call records, so the target can route the write through its tagged setter (e.g. <c>SetPadding(value, source)</c>)
        /// instead of its public one.</summary>
        public void ApplyTemplateValue<T>(string Name, T Value, Func<T> GetCurrentValue, Action<T, UIValueResolutionSource> SetValue,
            UIInvalidationKind Invalidation = UIInvalidationKind.Draw, IEqualityComparer<T> Comparer = null)
            => ApplyTemplateValueCore(Name, Value, GetCurrentValue, SetValue, Invalidation, Comparer, UIValueSourceKind.Template);

        /// <summary>Tagged variant of <see cref="ApplyTemplateValue{T}(string, T, Func{T}, Action{T, UIValueResolutionSource}, UIInvalidationKind, IEqualityComparer{T})"/>
        /// (ADR-0005/S7a) for defaults that target the CONTROL ITSELF rather than one of its template parts. The control's own chrome
        /// (its own Padding/Margin/MinHeight, or the border its public <c>BorderBrush</c>/<c>BorderThickness</c> facade writes, e.g. the
        /// <see cref="MGBorder"/> returned by an owner's <c>GetBorder()</c>) is applied by the template constructor before any XAML
        /// attribute or style has had a chance to run, so a plain <c>Template</c> (precedence 60) write would outrank a later
        /// <c>ImplicitStyle</c>/<c>ExplicitStyle</c> (40/50) even though a style is meant to win over the control's own baked-in defaults.
        /// Recording the value as <see cref="UIValueResolutionSource.Theme"/> (precedence 20) instead keeps it below styles while still
        /// outranking <c>Inherited</c>/<c>DefaultValue</c>. Defaults that target a PART of the template (anything other than the owner or
        /// the owner's own border) must keep using <see cref="ApplyTemplateValue{T}(string, T, Func{T}, Action{T, UIValueResolutionSource}, UIInvalidationKind, IEqualityComparer{T})"/>
        /// so they stay <c>Template</c>.<para/>
        /// The owner survives a rebuild of its template structure, so this default keeps its theme-refresh guard on a rebuilt structure
        /// (<see cref="IsStructureRebuilt"/>): a value the application set on the owner is not overwritten.</summary>
        public void ApplyOwnerThemeDefault<T>(string Name, T Value, Func<T> GetCurrentValue, Action<T, UIValueResolutionSource> SetValue,
            UIInvalidationKind Invalidation = UIInvalidationKind.Draw, IEqualityComparer<T> Comparer = null)
            => ApplyTemplateValueCore(Name, Value, GetCurrentValue, SetValue, Invalidation, Comparer, UIValueSourceKind.Theme);

        /// <summary>Untagged overload of <see cref="ApplyOwnerThemeDefault{T}(string, T, Func{T}, Action{T, UIValueResolutionSource}, UIInvalidationKind, IEqualityComparer{T})"/>
        /// for a default of the control itself that is not a pilot property (a selection color, an indent, a content alignment, a template name): no store
        /// records the value, but the call marks it as the owner's, so a rebuilt structure does not re-apply it over a value the application set.</summary>
        public void ApplyOwnerThemeDefault<T>(string Name, T Value, Func<T> GetCurrentValue, Action<T> SetValue,
            UIInvalidationKind Invalidation = UIInvalidationKind.Draw, IEqualityComparer<T> Comparer = null)
            => ApplyTemplateValueCore(Name, Value, GetCurrentValue, SetValue == null ? null : (v, _) => SetValue(v), Invalidation, Comparer, UIValueSourceKind.Theme);

        /// <summary>Shared implementation behind every <see cref="ApplyThemeDefault{T}(string, T, Func{T}, Action{T}, IEqualityComparer{T})"/>/
        /// <see cref="ApplyTemplateValue{T}(string, T, Func{T}, Action{T}, UIInvalidationKind, IEqualityComparer{T})"/>/
        /// <see cref="ApplyOwnerThemeDefault{T}(string, T, Func{T}, Action{T, UIValueResolutionSource}, UIInvalidationKind, IEqualityComparer{T})"/>
        /// overload: the has-previous/equals theme-refresh guard and <c>_AppliedTemplateDefaults</c> bookkeeping (ADR-0005/S3), which a rebuilt
        /// structure bypasses for part defaults (<see cref="IsStructureRebuilt"/>). <paramref name="SourceKind"/> selects which resolution source the write is
        /// recorded and tagged under: <see cref="UIValueSourceKind.Template"/> for the existing part-targeting overloads, or
        /// <see cref="UIValueSourceKind.Theme"/> for <see cref="ApplyOwnerThemeDefault{T}(string, T, Func{T}, Action{T, UIValueResolutionSource}, UIInvalidationKind, IEqualityComparer{T})"/> (ADR-0005/S7a).</summary>
        private void ApplyTemplateValueCore<T>(string Name, T Value, Func<T> GetCurrentValue, Action<T, UIValueResolutionSource> SetValue,
            UIInvalidationKind Invalidation, IEqualityComparer<T> Comparer, UIValueSourceKind SourceKind)
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
            // The record describes the value applied to the target of the previous application. A part default (Template) targets a part that a
            // rebuilt structure replaced, whose current value is the one of its construction: its record cannot tell a value set since. The owner
            // and its values survive the rebuild (MGElement.ApplyControlTemplate carries the owner's values held by a replaced part).
            bool TargetWasReplaced = IsStructureRebuilt && SourceKind == UIValueSourceKind.Template;
            if (!IsThemeRefresh || !HasPrevious || TargetWasReplaced || Comparer.Equals(CurrentValue, PreviousValue.Value))
            {
                UIValueResolutionSource Source = SourceKind == UIValueSourceKind.Theme
                    ? UIValueResolutionSource.Theme(Invalidation, Name)
                    : UIValueResolutionSource.Template(Invalidation, Name);
                SetValue(Value, Source);
                Owner.SetAppliedTemplateDefault(Name, new UIResolvedValue<T>(Value, Source));

                // A theme refresh that re-applies an unchanged value, as a render-only theme change does for every layout value, must not
                // invalidate layout (backlog task 7).
                if ((Invalidation & (UIInvalidationKind.Measure | UIInvalidationKind.Arrange | UIInvalidationKind.Structure)) != 0
                    && (!IsThemeRefresh || !Comparer.Equals(CurrentValue, Value)))
                {
                    Owner.InvalidateTemplateValue(Invalidation);
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

        /// <summary>The template that defines the structure this template creates and attaches: the template itself, or the base of a variant
        /// created by <see cref="CreateStructureVariant"/>. An element switching between two templates with the same structure template keeps its
        /// instantiated structure (<c>MGElement.ApplyControlTemplate</c>).</summary>
        internal MGControlTemplate StructureTemplate { get; }

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
            StructureTemplate = this;
        }

        private MGControlTemplate(string Name, MGControlTemplate BaseTemplate, Action<MGControlTemplateContext> ApplyDefaults)
            : this(Name, BaseTemplate.CreateStructureAction, BaseTemplate.AttachStructureAction, ApplyDefaults)
        {
            StructureTemplate = BaseTemplate.StructureTemplate;
        }

        /// <summary>Creates a template named <paramref name="Name"/> that creates and attaches the structure of <paramref name="BaseTemplate"/>
        /// and applies <paramref name="ApplyDefaults"/>, such as a XAML <c>BasedOn</c> variant that declares no root.</summary>
        internal static MGControlTemplate CreateStructureVariant(string Name, MGControlTemplate BaseTemplate, Action<MGControlTemplateContext> ApplyDefaults)
            => new(Name, BaseTemplate ?? throw new ArgumentNullException(nameof(BaseTemplate)), ApplyDefaults);

        public void Apply(MGElement Owner, bool IsThemeRefresh = false, bool IsStructureRebuilt = false)
        {
            if (Owner == null)
            {
                throw new ArgumentNullException(nameof(Owner));
            }

            Apply(new MGControlTemplateContext(Owner, IsThemeRefresh, IsStructureRebuilt));
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
        /// Theme refreshes should not call this unless the structure template (<see cref="StructureTemplate"/>) actually changed.</summary>
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