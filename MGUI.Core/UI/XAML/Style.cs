using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if UseWPF
using System.Windows.Markup;
#else
using Portable.Xaml.Markup;
#endif

namespace MGUI.Core.UI.XAML
{
    [ContentProperty(nameof(Setters))]
    public class Style
    {
        public MGElementType TargetType { get; set; }
        //public Type TargetType { get; set; }
        public List<Setter> Setters { get; set; } = new();

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
}
