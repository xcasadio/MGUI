using MGUI.Shared.Input.Keyboard;

namespace MGUI.Shared.Rendering
{
    /// <summary>Opt-in contract for a render host that can relay a native text-input source (e.g. <see cref="Microsoft.Xna.Framework.GameWindow.TextInput"/>)
    /// into an <see cref="IKeyboardTextInputSink"/>, so AZERTY/dead-keys/IME work without falling back to the hard-coded US-QWERTY key-to-character mapping.<para/>
    /// This interface is intentionally NOT part of <see cref="IRenderHost"/>: it is only implemented by hosts that actually own a native text-input source
    /// (see <see cref="GameRenderHost{TObservableGame}"/>). Hosts that don't (e.g. <see cref="DelegateRenderHost"/>) require the consumer to relay
    /// text input manually, since there is no MGUI-side <see cref="Microsoft.Xna.Framework.GameWindow"/> to subscribe to.</summary>
    public interface ITextInputHost
    {
        /// <summary>Subscribes to this host's native text-input source and forwards every received character to <paramref name="sink"/>.<br/>
        /// Calling this again replaces any previously attached sink.</summary>
        void AttachTextInputSink(IKeyboardTextInputSink sink);

        /// <summary>Unsubscribes from this host's native text-input source. Safe to call even if no sink is currently attached.</summary>
        void DetachTextInputSink();
    }
}
