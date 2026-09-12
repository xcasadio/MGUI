using System;
using System.Collections.Generic;

namespace MGUI.Core.UI.Styling
{
    /// <summary>Why <see cref="MGElement.RefreshStyles"/> did not apply a style setter.</summary>
    public enum UIStyleRefreshSkipReason
    {
        /// <summary>The property is not tracked by the resolved value store, so a refresh cannot tell a style value from a local one: the setter is left
        /// untouched (it still applies when the XAML is parsed).</summary>
        NotRefreshable,

        /// <summary>A name listed in the element's style names matches no named style in scope.</summary>
        StyleNotFound,

        /// <summary>The setter value cannot be converted to the property type.</summary>
        InvalidValue,
    }

    /// <summary>A style setter or a style name that <see cref="MGElement.RefreshStyles"/> skipped.</summary>
    /// <param name="Element">The element the setter or the style name was resolved for.</param>
    /// <param name="Name">The property name of the setter, or the style name for <see cref="UIStyleRefreshSkipReason.StyleNotFound"/>.</param>
    /// <param name="Detail">The conversion error for <see cref="UIStyleRefreshSkipReason.InvalidValue"/>, null otherwise.</param>
    public readonly record struct UIStyleRefreshSkip(MGElement Element, string Name, UIStyleRefreshSkipReason Reason, string Detail);

    /// <summary>The outcome of <see cref="MGElement.RefreshStyles"/>.</summary>
    public sealed class UIStyleRefreshResult
    {
        internal UIStyleRefreshResult(int VisitedElements, int StyledElements, int WrittenValues, int ClearedValues, IReadOnlyList<UIStyleRefreshSkip> Skipped)
        {
            this.VisitedElements = VisitedElements;
            this.StyledElements = StyledElements;
            this.WrittenValues = WrittenValues;
            this.ClearedValues = ClearedValues;
            this.Skipped = Skipped ?? Array.Empty<UIStyleRefreshSkip>();
        }

        /// <summary>The elements of the refreshed subtree, the root included.</summary>
        public int VisitedElements { get; }

        /// <summary>The visited elements created from a XAML definition processed for styles, whose styles were resolved again.</summary>
        public int StyledElements { get; }

        /// <summary>The style contributions written to the resolved value store. A write only changes the effective value, and only invalidates the
        /// layout, when the style value is the winner and differs from the current value.</summary>
        public int WrittenValues { get; }

        /// <summary>The style contributions removed because no style sets their property any more.</summary>
        public int ClearedValues { get; }

        /// <summary>The setters and style names that were not applied.</summary>
        public IReadOnlyList<UIStyleRefreshSkip> Skipped { get; }
    }
}
