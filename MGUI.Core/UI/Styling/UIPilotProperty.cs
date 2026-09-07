namespace MGUI.Core.UI.Styling
{
    /// <summary>
    /// Identifies one of the seven CLR properties whose effective value is resolved through a
    /// <see cref="UIResolvedPropertyStore"/> (ADR-0005) instead of being written directly by a plain setter.
    /// Every other property on <c>MGElement</c> and its subclasses stays an ordinary C# property, with no
    /// store entry and no runtime cost.
    /// </summary>
    public enum UIPilotProperty
    {
        /// <summary>The element's outer margin (<c>MGElement.Margin</c>).</summary>
        Margin,

        /// <summary>The element's inner padding (<c>MGElement.Padding</c>).</summary>
        Padding,

        /// <summary>The element's minimum height (<c>MGElement.MinHeight</c>).</summary>
        MinHeight,

        /// <summary>The border brush of the element's border (the single real implementation lives on <c>MGBorder</c>).</summary>
        BorderBrush,

        /// <summary>The border thickness of the element's border (the single real implementation lives on <c>MGBorder</c>).</summary>
        BorderThickness,

        /// <summary>The element's background fill (<c>MGElement.BackgroundBrush</c>, a <c>VisualStateFillBrush</c> container).</summary>
        Background,

        /// <summary>The element's text foreground (<c>MGTextBlock.Foreground</c> / <c>MGElement.DefaultTextForeground</c>, a <c>VisualStateSetting&lt;Color?&gt;</c> container).</summary>
        Foreground,
    }
}
