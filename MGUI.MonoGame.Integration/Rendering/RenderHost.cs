using MGUI.Shared.Input.Keyboard;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Diagnostics;

namespace MGUI.Shared.Rendering
{
    public interface IRenderViewport
    {
        Rectangle GetBounds();
    }

    public interface IObservableUpdate
    {
        public event EventHandler<TimeSpan> PreviewUpdate;
        public event EventHandler<EventArgs> EndUpdate;
    }

    /// <summary>For a concrete implementation, consider using <see cref="GameRenderHost{TObservableGame}"/></summary>
    public interface IRenderHost : IRenderViewport, IObservableUpdate, IServiceProvider
    {
        public GraphicsDevice GraphicsDevice { get; }
    }

    public class GameRenderHost<TObservableGame> : IRenderHost, ITextInputHost, IDisposable
        where TObservableGame : Game, IObservableUpdate
    {
        public TObservableGame Game { get; }

        public Rectangle GetBounds() => new(0, 0, Game.Window.ClientBounds.Width, Game.Window.ClientBounds.Height);

        public GraphicsDevice GraphicsDevice => Game.GraphicsDevice;

        public object GetService(Type serviceType) => Game.Services.GetService(serviceType);

        public event EventHandler<TimeSpan> PreviewUpdate;
        public event EventHandler<EventArgs> EndUpdate;

        private Rectangle PreviousClientBounds;

        private readonly EventHandler<TimeSpan> _onGamePreviewUpdate;
        private readonly EventHandler<EventArgs> _onGameEndUpdate;
        private readonly EventHandler<EventArgs> _onClientSizeChanged;

        private IKeyboardTextInputSink _textInputSink;
        private EventHandler<TextInputEventArgs> _onGameTextInput;

        public GameRenderHost(TObservableGame game)
        {
            Game = game;

            _onGamePreviewUpdate = (sender, e) => PreviewUpdate?.Invoke(Game, e);
            _onGameEndUpdate = (sender, e) => EndUpdate?.Invoke(Game, e);
            _onClientSizeChanged = (sender, e) =>
            {
                if (GraphicsDevice.ScissorRectangle == PreviousClientBounds)
                {
                    GraphicsDevice.ScissorRectangle = GetBounds();
                }

                PreviousClientBounds = GetBounds();
            };

            Game.PreviewUpdate += _onGamePreviewUpdate;
            Game.EndUpdate += _onGameEndUpdate;

            PreviousClientBounds = GetBounds();
            Game.Window.ClientSizeChanged += _onClientSizeChanged;
        }

        /// <summary>Subscribes to <see cref="Game"/>'s <see cref="GameWindow.TextInput"/> and forwards every received character to <paramref name="sink"/>
        /// via <see cref="IKeyboardTextInputSink.QueueTextInput(char, Keys)"/>. Calling this again replaces the previously attached sink.</summary>
        public void AttachTextInputSink(IKeyboardTextInputSink sink)
        {
            ArgumentNullException.ThrowIfNull(sink);

            DetachTextInputSink();

            _textInputSink = sink;
            _onGameTextInput = (sender, e) => OnTextInput(e);
            Game.Window.TextInput += _onGameTextInput;
        }

        public void DetachTextInputSink()
        {
            if (_onGameTextInput != null)
            {
                Game.Window.TextInput -= _onGameTextInput;
                _onGameTextInput = null;
            }

            _textInputSink = null;
        }

        /// <summary>Exposed internally so the TextInput -> <see cref="IKeyboardTextInputSink"/> wiring can be exercised without a real <see cref="Game"/>.</summary>
        internal void OnTextInput(TextInputEventArgs e) => _textInputSink?.QueueTextInput(e.Character, e.Key);

        public void Dispose()
        {
            Game.PreviewUpdate -= _onGamePreviewUpdate;
            Game.EndUpdate -= _onGameEndUpdate;
            Game.Window.ClientSizeChanged -= _onClientSizeChanged;
            DetachTextInputSink();
            Debug.WriteLine($"[Dispose] {GetType().Name} unsubscribed 4 event handlers");
        }
    }
}