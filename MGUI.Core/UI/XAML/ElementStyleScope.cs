using System;
using System.Collections.Generic;

namespace MGUI.Core.UI.XAML
{
    /// <summary>Backlog task 10: what <see cref="Element.ProcessStyles(MGResources)"/> resolved for one XAML definition, kept by the elements that
    /// definition creates so that <see cref="MGElement.RefreshStyles"/> can resolve their styles again without the parse model.<para/>
    /// Immutable and shared by every element created from the same definition.</summary>
    internal sealed class ElementStyleScope
    {
        public ElementStyleScope(Type DefinitionType, MGElementType ElementType, string StyleNames, bool IsStyleable, bool UsesResourceStyles,
            IReadOnlyList<Style> InlineStyles, IReadOnlyCollection<string> StyledPropertyNames)
        {
            this.DefinitionType = DefinitionType ?? throw new ArgumentNullException(nameof(DefinitionType));
            this.ElementType = ElementType;
            this.StyleNames = StyleNames;
            this.IsStyleable = IsStyleable;
            this.UsesResourceStyles = UsesResourceStyles;
            this.InlineStyles = InlineStyles ?? Array.Empty<Style>();
            this.StyledPropertyNames = StyledPropertyNames;
        }

        /// <summary>The XAML definition type, which decides the properties a setter can reach (a <see cref="TextBlock"/>'s foreground, a border facade).</summary>
        public Type DefinitionType { get; }

        /// <summary>The type that implicit and named styles must target, as matched at parse time.</summary>
        public MGElementType ElementType { get; }

        /// <summary>The comma-delimited names of the named styles applied to the element, see <see cref="Element.StyleNames"/>.</summary>
        public string StyleNames { get; }

        /// <summary>See <see cref="Element.IsStyleable"/>.</summary>
        public bool IsStyleable { get; }

        /// <summary>False below a definition whose <see cref="Element.InheritsParentStyles"/> is false: at parse time that subtree saw neither the
        /// resource styles nor the inline styles of its ancestors.</summary>
        public bool UsesResourceStyles { get; }

        /// <summary>The inline styles in scope (<see cref="Element.Styles"/> of the definition and of its ancestors), outermost first.</summary>
        public IReadOnlyList<Style> InlineStyles { get; }

        /// <summary>The properties that the parse styled on this definition itself (not the ones an owner's border facade forwarded to it), null when none.</summary>
        public IReadOnlyCollection<string> StyledPropertyNames { get; }
    }
}
