namespace MGUI.Core.UI.Styling;

/// <summary>
/// Identifies which part of a <see cref="UIPilotProperty"/>'s value a resolved contribution applies to.
/// A scalar pilot (Margin, Padding, MinHeight, BorderBrush, BorderThickness) only ever uses
/// <see cref="Whole"/>. A container pilot (Background, Foreground) uses <see cref="Whole"/> for
/// whole-object replacement of the container instance, and the remaining members for writes that
/// target one sub-field of the container (a <c>VisualStateSetting&lt;T&gt;</c> / <c>VisualStateFillBrush</c>).
/// </summary>
public enum UIValueSlot
{
    /// <summary>The whole value: the only slot used by scalar pilots, and the whole-object
    /// replacement slot of a container pilot (e.g. assigning a brand new <c>VisualStateFillBrush</c>).</summary>
    Whole,

    /// <summary>The container's <c>NormalValue</c> sub-field.</summary>
    Normal,

    /// <summary>The container's <c>SelectedValue</c> sub-field.</summary>
    Selected,

    /// <summary>The container's <c>DisabledValue</c> sub-field.</summary>
    Disabled,

    /// <summary>The container's <c>FocusedValue</c> sub-field.</summary>
    Focused,

    /// <summary>The container's <c>FocusedColor</c> sub-field.</summary>
    FocusedColor,
}