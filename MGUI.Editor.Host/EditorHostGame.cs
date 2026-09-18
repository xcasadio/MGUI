using FontStashSharp;
using MGUI.Backend.MonoGame;
using MGUI.Core.UI;
using MGUI.Editor;
using MGUI.FontStashSharp;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;
using System.IO;

namespace MGUI.Editor.Host;

/// <summary>Hosts the XAML editor in its own MonoGame window: a single, chrome-less <see cref="MGWindow"/> that
/// covers the whole client area and follows the game window's size, containing one <see cref="XamlEditorView"/>.
/// The desktop uses the built-in Dark theme and a FontStashSharp text engine loaded with the tracked Arial
/// TrueType fonts, falling back to the renderer's default SpriteFont engine if that setup fails.</summary>
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

        _desktop.Resources.DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, _desktop.DefaultFontFamily);
        InitializeFontStashSharpTextEngine((IMonoGameDesktopBackend)backend.Renderer);

        _editorWindow = new MGWindow(_desktop, 0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height)
        {
            WindowStyle = WindowStyle.None,
            IsDraggable = false,
            IsUserResizable = false,
            CanCloseWindow = false,
        };
        //  WindowStyle.None makes a window's background transparent (chrome-less HUD windows); the editor window must stay opaque, otherwise the
        //  game's clear colour shows through every docking area that paints no background of its own (tab strips, preview and diagnostics panes).
        //  A background written after WindowStyle.None replaces its transparent values.
        _editorWindow.BackgroundBrush = _desktop.Theme.GetBackgroundBrush(MGElementType.Window);

        XamlEditorSession session = new();
        XamlEditorView view = new(_editorWindow, session);
        _editorWindow.SetContent(view.CreateDockHost());
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

    /// <summary>Loads a FontStashSharp engine for the desktop's default font family (Normal, Bold, Italic,
    /// from the tracked Arial TrueType fonts) and calibrates it against <paramref name="renderer"/>'s
    /// <see cref="FontManager"/>, then makes it the desktop's text engine. If any step fails, the reason
    /// is logged and the renderer's default SpriteFont engine is left in place.</summary>
    private void InitializeFontStashSharpTextEngine(IMonoGameDesktopBackend renderer)
    {
        try
        {
            string family = _desktop.DefaultFontFamily;
            string ttfDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"Content\Fonts\ttf"));

            FontStashSharpTextEngine engine = new();

            byte[] arialBytes = File.ReadAllBytes(Path.Combine(ttfDir, "arial.ttf"));
            FontSystem arialNormal = new();
            arialNormal.AddFont(arialBytes);
            engine.AddFontSystem(family, CustomFontStyles.Normal, arialNormal, arialBytes);

            FontSystem arialBold = new();
            arialBold.AddFont(File.ReadAllBytes(Path.Combine(ttfDir, "arialbd.ttf")));
            engine.AddFontSystem(family, CustomFontStyles.Bold, arialBold);

            FontSystem arialItalic = new();
            arialItalic.AddFont(File.ReadAllBytes(Path.Combine(ttfDir, "ariali.ttf")));
            engine.AddFontSystem(family, CustomFontStyles.Italic, arialItalic);

            engine.MatchSpriteFontSizing(renderer.FontManager);

            _desktop.TextEngine = engine;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FontStashSharp text engine init failed: {ex.Message}");
        }
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
