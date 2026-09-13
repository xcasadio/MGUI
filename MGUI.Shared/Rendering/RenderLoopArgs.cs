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
        /// <summary>Per-frame dedup registry for stateful paints (fill brushes / border brushes). Not part of the positional
        /// constructor so every existing positional call site keeps compiling unchanged; defaults to <see langword="null"/>,
        /// in which case paints are ticked unconditionally (no dedup). Set by <see cref="MGUI.Core.UI.MGDesktop.Update"/>
        /// via a <see langword="with"/> expression once per frame.</summary>
        public IPaintUpdateRegistry PaintRegistry { get; init; }

        /// <summary>The animation clock's delta for this frame (U10), already scaled by <c>TimeScale</c> and zero while the clock is
        /// paused: what a stateful paint (<see cref="MGUI.Core.UI.Brushes.BorderBrushes.MGHighlightBorderBrush"/>) advances by instead of
        /// <see cref="FrameElapsed"/>, so it follows the same pause/speed as the engine's animations. Not part of the positional
        /// constructor, same reason as <see cref="PaintRegistry"/>; defaults to <see langword="null"/> (a host that does not provide it,
        /// or builds its own <see cref="UpdateBaseArgs"/>, keeps the wall-clock <see cref="FrameElapsed"/> behaviour). Set by
        /// <see cref="MGUI.Core.UI.MGDesktop.Update"/> via the same <see langword="with"/> expression as <see cref="PaintRegistry"/>,
        /// after <c>Animations.Update</c> advances the clock for the frame.</summary>
        public TimeSpan? AnimationDeltaTime { get; init; }

        public UpdateBaseArgs GetTranslated(int MouseXOffset, int MouseYOffset)
        {
            MouseState MS = MouseState;
            MouseState Translated = new(MS.X + MouseXOffset, MS.Y + MouseYOffset, MS.ScrollWheelValue, MS.LeftButton, MS.MiddleButton, MS.RightButton, MS.XButton1, MS.XButton2);
            return this with { MouseState = Translated };
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