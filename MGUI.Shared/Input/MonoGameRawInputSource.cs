using Microsoft.Xna.Framework.Input;

namespace MGUI.Shared.Input
{
    /// <summary>Default MonoGame raw-input adapter used by renderers that need current device states.</summary>
    public sealed class MonoGameRawInputSource : IRawInputSource
    {
        public MouseState GetMouseState() => Microsoft.Xna.Framework.Input.Mouse.GetState();
        public KeyboardState GetKeyboardState() => Microsoft.Xna.Framework.Input.Keyboard.GetState();
    }
}