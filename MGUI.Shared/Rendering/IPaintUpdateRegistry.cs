using System;

namespace MGUI.Shared.Rendering
{
    /// <summary>Tracks which paint objects (fill brushes / border brushes) have already been ticked during the current
    /// <see cref="UpdateBaseArgs"/> lifecycle, so that a paint shared by reference across several consumers is only
    /// advanced once per frame instead of once per consumer.<para/>
    /// Keyed by object reference: two distinct instances that happen to be equal are tracked independently.</summary>
    public interface IPaintUpdateRegistry
    {
        /// <summary>Attempts to claim <paramref name="paint"/> for this frame.<para/>
        /// Returns <see langword="true"/> the first time a given paint instance is passed in since the registry was
        /// last cleared (meaning the caller should proceed to tick it), and <see langword="false"/> on every
        /// subsequent call this frame for that same instance (meaning it has already been ticked).</summary>
        bool TryBeginUpdate(object paint);
    }
}
