#if UseWPF
using System.Windows.Markup;
#else
using Portable.Xaml.Markup;
#endif

namespace MGUI.Core.UI.XAML;

[ContentProperty(nameof(Setters))]
public class Style
{
    public MGElementType TargetType { get; set; }
    //public Type TargetType { get; set; }
    public List<Setter> Setters { get; set; } = new();

    /// <summary>The transitions the style attaches to its elements (ADR-0007, decision 5): <c>&lt;Style.Transitions&gt;&lt;Transition Property="Opacity" Duration="0.2" /&gt;&lt;/Style.Transitions&gt;</c>.
    /// Merged into the element after its setters; the element's own <see cref="Element.Transitions"/> win per path.</summary>
    public List<Transition> Transitions { get; set; } = new();

    /// <summary>The named visual states the style gives its elements (ADR-0007, decision 5): <c>&lt;Style.VisualStates&gt;&lt;VisualStateDefinition Name="Hover"&gt;...&lt;/VisualStateDefinition&gt;&lt;/Style.VisualStates&gt;</c>.
    /// Merged into the element after its setters; the element's own <see cref="Element.VisualStates"/> win per name.</summary>
    public List<VisualStateDefinition> VisualStates { get; set; } = new();

    /// <summary>True when the style carries transitions or visual states.</summary>
    internal bool HasAnimation => Transitions.Count > 0 || VisualStates.Count > 0;

    /// <summary>True when the style has anything to apply: setters, transitions or visual states.</summary>
    internal bool HasContent => Setters.Count > 0 || HasAnimation;

    /// <summary>If null, this style will affect all elements of the <see cref="TargetType"/>.<br/>
    /// Otherwise, this style will only affect elements of the <see cref="TargetType"/> that also have this name in their <see cref="Element.StyleNames"/><para/>
    /// This value should never contain commas, because commas are used to delimit multiple style names in <see cref="Element.StyleNames"/></summary>
    public string Name { get; set; }

    /// <summary>If true (the default), this style applies to all elements of <see cref="TargetType"/> in the visual tree,
    /// including those that are internal components of a complex element (e.g. the <see cref="MGBorder"/> component inside an <see cref="MGCheckBox"/>).<br/>
    /// If false, the style only applies to elements that are explicitly declared in the XAML, not to internally-generated component elements.<para/>
    /// Default value: true</summary>
    public bool AffectsComponents { get; set; } = true;
}

public class Setter
{
    public string Property { get; set; }
    public object Value { get; set; }
}