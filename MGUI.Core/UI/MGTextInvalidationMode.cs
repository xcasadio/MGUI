namespace MGUI.Core.UI;

/// <summary>Describes how a text content update is allowed to invalidate an <see cref="MGTextBlock"/>.</summary>
public enum MGTextInvalidationMode
{
    /// <summary>
    /// The rendered content changes, but the caller guarantees the existing text footprint remains valid.
    /// This mode must only be used for stable, already-reserved labels where line count, wrapping and desired size cannot change.
    /// </summary>
    ContentOnly,

    /// <summary>
    /// The text block may refresh its internal parsed lines against the current layout bounds, but should not invalidate parent layout.
    /// Use this for stable-width/status labels whose reserved footprint already covers the possible line shape.
    /// </summary>
    ReflowLocal,

    /// <summary>
    /// The text update may change desired size and must invalidate parent layout.
    /// This is the safe default for ordinary labels, wrapping text and controls that have not opted into a stable footprint.
    /// </summary>
    RelayoutParent
}