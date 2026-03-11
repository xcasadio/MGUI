using Microsoft.Xna.Framework.Input;

namespace MGUI.Shared.Input
{
    /// <summary>Provides raw device states independently from the render host.</summary>
    public interface IRawInputSource
    {
        public MouseState GetMouseState();
        public KeyboardState GetKeyboardState();
    }
}