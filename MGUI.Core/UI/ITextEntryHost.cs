using Microsoft.Xna.Framework.Input;
using System;

namespace MGUI.Core.UI
{
    /// <summary>Implemented by controls that host text entry (e.g. <see cref="MGTextBox"/>), so that
    /// <see cref="MGDesktop"/> and the focus/navigation infrastructure can apply the same text-entry
    /// protections (gameplay input not captured while editing, internal navigation keys preserved
    /// instead of stolen, focus cleaned up when the control becomes readonly) to third-party controls
    /// outside of <see cref="MGUI.Core"/> that implement this interface.</summary>
    public interface ITextEntryHost
    {
        /// <summary>Whether this control is currently readonly (not accepting edits).</summary>
        bool IsReadonly { get; }

        /// <summary>Invoked when <see cref="IsReadonly"/> changes.</summary>
        event EventHandler<bool> ReadonlyChanged;

        /// <summary>Whether the given <paramref name="key"/> should be preserved for this control's own
        /// text-entry handling instead of being consumed by external navigation/shortcut handling.</summary>
        bool ShouldPreserveTextEntryKey(Keys key);
    }
}
