using MGUI.Backend.MonoGame;
using MGUI.Core.UI;
using MGUI.Editor;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;

namespace MGUI.Editor.Host;

/// <summary>Hosts the XAML editor in its own MonoGame window: a single, chrome-less <see cref="MGWindow"/> that
/// covers the whole client area and follows the game window's size, containing one <see cref="XamlEditorView"/>.</summary>
public class EditorHostGame : Game, IObservableUpdate
{
    private readonly GraphicsDeviceManager _graphics;
    private MGDesktop _desktop;
    private MGWindow _editorWindow;

    public event EventHandler<TimeSpan> PreviewUpdate;
    public event EventHandler<EventArgs> EndUpdate;

    public EditorHostGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "MGUI XAML Editor";
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = 1600;
        _graphics.PreferredBackBufferHeight = 900;
        _graphics.ApplyChanges();

        MonoGameBackendSession<GameRenderHost<EditorHostGame>> backend = MonoGameBackendBootstrap.Create(
            new GameRenderHost<EditorHostGame>(this));
        _desktop = new MGDesktop((IUIDesktopRuntime)backend.Renderer);
        _desktop.LoadDefaultResources();

        _editorWindow = new MGWindow(_desktop, 0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height)
        {
            WindowStyle = WindowStyle.None,
            IsDraggable = false,
            IsUserResizable = false,
            CanCloseWindow = false,
        };

        XamlEditorSession session = new();
        XamlEditorView view = new(_editorWindow, session);
        _editorWindow.SetContent(view.Root);
        _desktop.Windows.Add(_editorWindow);

        Window.ClientSizeChanged += (_, _) =>
        {
            _editorWindow.Left = 0;
            _editorWindow.Top = 0;
            _editorWindow.WindowWidth = Window.ClientBounds.Width;
            _editorWindow.WindowHeight = Window.ClientBounds.Height;
        };

        base.Initialize();
    }

    protected override void Update(GameTime gameTime)
    {
        PreviewUpdate?.Invoke(this, gameTime.TotalGameTime);

        _desktop.Update();

        base.Update(gameTime);

        EndUpdate?.Invoke(this, EventArgs.Empty);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _desktop.Draw();

        base.Draw(gameTime);
    }
}
