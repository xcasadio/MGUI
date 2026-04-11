using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Diagnostics;

namespace MGUI.Shared.Rendering
{
    [DebuggerStepThrough]
    public readonly record struct DrawBaseArgs(TimeSpan TS, IUIRenderContext Context, float Opacity)
    {
        public IUIDrawTransaction DT => Context as IUIDrawTransaction;
        public DrawBaseArgs SetOpacity(float Value) => new(TS, Context, Value);
        public DrawBaseArgs MultiplyOpacity(float Scalar) => new(TS, Context, Opacity * Scalar);
    }

    [DebuggerStepThrough]
    public class DrawBaseEventArgs : EventArgs
    {
        public readonly DrawBaseArgs BA;
        public TimeSpan TS => BA.TS;
        public IUIRenderContext Context => BA.Context;
        public IUIDrawTransaction DT => BA.DT;
        public float Opacity => BA.Opacity;

        public DrawBaseEventArgs(DrawBaseArgs BA)
        {
            this.BA = BA;
        }
    }

    [DebuggerStepThrough]
    public readonly record struct UpdateBaseArgs(TimeSpan TotalElapsed, TimeSpan FrameElapsed, MouseState MouseState, KeyboardState KeyboardState)
    {
        public UpdateBaseArgs GetTranslated(int MouseXOffset, int MouseYOffset)
        {
            MouseState MS = MouseState;
            MouseState Translated = new(MS.X + MouseXOffset, MS.Y + MouseYOffset, MS.ScrollWheelValue, MS.LeftButton, MS.MiddleButton, MS.RightButton, MS.XButton1, MS.XButton2);
            return new(TotalElapsed, FrameElapsed, Translated, KeyboardState);
        }
    }

    [DebuggerStepThrough]
    public class UpdateBaseEventArgs : EventArgs
    {
        public readonly UpdateBaseArgs BA;

        public TimeSpan TotalElapsed => BA.TotalElapsed;
        public TimeSpan FrameElapsed => BA.FrameElapsed;
        public MouseState MouseState => BA.MouseState;
        public KeyboardState KeyboardState => BA.KeyboardState;

        public UpdateBaseEventArgs(UpdateBaseArgs BA)
        {
            this.BA = BA;
        }
    }
}