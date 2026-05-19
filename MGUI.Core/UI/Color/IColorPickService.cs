using System;

namespace MGUI.Core.UI
{
    public interface IColorPickService
    {
        bool IsSupported { get; }
        bool BeginPick(ColorPickRequest request);
        void CancelPick();
        event EventHandler<ColorPickedEventArgs> ColorPicked;
        event EventHandler ColorPickCancelled;
    }

    public sealed class UnsupportedColorPickService : IColorPickService
    {
        public static UnsupportedColorPickService Instance { get; } = new();

        private UnsupportedColorPickService()
        {
        }

        public bool IsSupported => false;

        public event EventHandler<ColorPickedEventArgs> ColorPicked
        {
            add { }
            remove { }
        }

        public event EventHandler ColorPickCancelled
        {
            add { }
            remove { }
        }

        public bool BeginPick(ColorPickRequest request)
            => false;

        public void CancelPick()
        {
        }
    }
}