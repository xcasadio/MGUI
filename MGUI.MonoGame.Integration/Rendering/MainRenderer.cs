using MGUI.Shared.Helpers;
using MGUI.Shared.Assets;
using MGUI.Shared.Input;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using MGUI.Backend.MonoGame;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.VectorDraw;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Shared.Rendering
{
    public sealed class MainRenderer : IMonoGameDesktopBackend
    {
        /// <summary>MonoGame-specific compatibility surface. Core UI code should continue to use <see cref="IUIDesktopRuntime"/>.</summary>
        public IRenderHost Host { get; }
        public IRawInputSource RawInputSource { get; }
        public IUISurface Surface { get; }
        private List<IUIView> MutableViews { get; } = new();
        public IReadOnlyList<IUIView> Views => MutableViews;

        internal GraphicsDevice GraphicsDevice => Host.GraphicsDevice;
        internal SpriteBatch SpriteBatch { get; }
        internal PrimitiveBatch PrimitiveBatch { get; }
        /// <summary>Shared textured effect used by <see cref="DrawTransaction.DrawTexturedTriangleList"/>: same projection/view conventions as <see cref="PrimitiveBatch"/>,
        /// texture and vertex colour enabled, no lighting. Owned like <see cref="PrimitiveBatch"/> (one per renderer).</summary>
        internal BasicEffect TexturedPrimitiveEffect { get; }
        internal RenderTargetPool RenderTargetPool { get; } = new();

        internal ContentManager Content { get; }

        /// <summary>MonoGame-specific compatibility surface. Core UI code should continue to use <see cref="ITextMeasurementEngine"/>.</summary>
        public FontManager FontManager { get; }
        public string DefaultFontFamily => FontManager.DefaultFontFamily;
        public IUIAssetProvider AssetProvider { get; }

        private ITextMeasurementEngine _textEngine;
        /// <summary>
        /// The active text measurement engine used for font resolution and layout.
        /// Defaults to a <see cref="SpriteFontTextEngine"/> wrapping <see cref="FontManager"/>.
        /// At runtime, assigned engines must also implement <see cref="ITextDrawEngine"/>
        /// so the backend can render resolved fonts with its own draw implementation.
        /// </summary>
        public ITextMeasurementEngine TextEngine
        {
            get => _textEngine;
            set
            {
                if (value is not ITextDrawEngine)
                {
                    throw new ArgumentException($"{nameof(TextEngine)} must also implement {nameof(ITextDrawEngine)} for the active rendering backend.", nameof(value));
                }

                ITextMeasurementEngine previous = TextEngine;
                if (_textEngine != null && !ReferenceEquals(_textEngine, value))
                {
                    _textEngine.InvalidateCache();
                }

                _textEngine = value ?? throw new ArgumentNullException(nameof(value));
                TextEngineChanged?.Invoke(this, new EventArgs<ITextMeasurementEngine>(previous, TextEngine));
            }
        }

        internal ITextDrawEngine GetTextRenderer() => (ITextDrawEngine)TextEngine;

        /// <summary>Invoked when <see cref="TextEngine"/> is set to a new value</summary>
        public event EventHandler<EventArgs<ITextMeasurementEngine>> TextEngineChanged;
        public event EventHandler<EventArgs> EndUpdate;

        public InputTracker Input { get; }
        public UpdateBaseArgs UpdateArgs { get; private set; }

        private TimeSpan PreviousUpdateTimeSpan = TimeSpan.Zero;

        internal Rectangle GetViewport(int Margin) => Surface.GetBounds().GetCompressed(Margin);

        IUIDrawTransaction IUIDesktopRuntime.BeginDrawTransaction(DrawSettings Settings, bool DeferBegin)
            => BeginDrawTransaction(Settings, DeferBegin);

        /// <summary>Opens the draw transaction of this renderer. A renderer has one transaction, created at the first call and re-armed at each
        /// later one (ADR-0032): the object returned is the same from call to call, in the state of a new transaction, and it is not to be used
        /// after its <see cref="IDisposable.Dispose"/>. If the re-arm or the begin of the batch throws, the transaction stays closed and the next
        /// call succeeds.</summary>
        /// <exception cref="InvalidOperationException">The transaction of this renderer is already open: dispose it first.</exception>
        public IUIDrawTransaction BeginDrawTransaction(DrawSettings Settings, bool DeferBegin)
            => BeginDrawTransaction(Settings, DeferBegin, DrawContext.Sprites);

        private DrawTransaction _DrawTransaction;
        private bool _IsDrawTransactionOpen;

        /// <summary>The settings the transactions of this renderer derived for temporary transforms and effects, kept from one transaction to the next (ADR-0031).</summary>
        internal DerivedDrawSettingsCache DerivedSettings { get; } = new();

        /// <summary>The three settings a stencil clip derives at each depth, kept from one transaction to the next (ADR-0031).</summary>
        internal DerivedDrawSettingsCache StencilSettings { get; } = new();

        internal IUIDrawTransaction BeginDrawTransaction(DrawSettings Settings, bool DeferBegin, DrawContext DefaultContext)
        {
            ArgumentNullException.ThrowIfNull(Settings);

            if (_IsDrawTransactionOpen)
            {
                throw new InvalidOperationException(
                    $"The draw transaction of this {nameof(MainRenderer)} is already open. A renderer has one transaction and one draw at a time: " +
                    "dispose the transaction returned by the previous BeginDrawTransaction call (or let the draw that holds it finish) before asking for another.");
            }

            if (_DrawTransaction == null)
            {
                //  Created with the begin deferred, then begun here: a begin that throws must not lose the object.
                _DrawTransaction = new DrawTransaction(this, Settings, true, DefaultContext);
                if (!DeferBegin)
                {
                    _DrawTransaction.ForceBeginDraw(DefaultContext);
                }
            }
            else
            {
                _DrawTransaction.Rearm(Settings, DeferBegin, DefaultContext);
            }

            _IsDrawTransactionOpen = true;
            return _DrawTransaction;
        }

        /// <summary>Called by <see cref="DrawTransaction.Dispose"/>, whether the end of its batch succeeded or threw.</summary>
        internal void CloseDrawTransaction(DrawTransaction Transaction)
        {
            if (ReferenceEquals(Transaction, _DrawTransaction))
            {
                _IsDrawTransactionOpen = false;
            }
        }

        internal RenderTargetLease RentRenderTarget(int width, int height, bool preserveContents)
            => RenderTargetPool.Rent(GraphicsDevice, width, height, preserveContents);

        internal void ReturnRenderTarget(RenderTarget2D renderTarget)
            => RenderTargetPool.Return(renderTarget);

        public void RegisterView(IUIView View)
        {
            if (View == null)
            {
                throw new ArgumentNullException(nameof(View));
            }

            if (!MutableViews.Contains(View))
            {
                MutableViews.Add(View);
            }
        }

        public bool UnregisterView(IUIView View) => View != null && MutableViews.Remove(View);

        public void UpdateViews()
        {
            foreach (IUIView View in MutableViews)
            {
                View.Update();
            }
        }

        //  Kept: DrawSettings.Default builds a new object at each read.
        private readonly DrawSettings DefaultDrawSettings = DrawSettings.Default;

        public void DrawViews(float Opacity = 1.0f, DrawSettings InitialDrawSettings = null)
        {
            using DrawTransaction DT = (DrawTransaction)BeginDrawTransaction(InitialDrawSettings ?? DefaultDrawSettings, false, DrawContext.Sprites);
            foreach (IUIView View in MutableViews)
            {
                View.Draw(DT, Opacity);
            }
        }

        private static IRawInputSource ResolveInputSource(IRenderHost Host, IRawInputSource RawInputSource)
        {
            if (RawInputSource != null)
            {
                return RawInputSource;
            }

            if (Host is IRawInputSource HostInputSource)
            {
                return HostInputSource;
            }

            throw new ArgumentNullException(nameof(RawInputSource),
                $"{nameof(MainRenderer)} requires an explicit {nameof(IRawInputSource)} when the supplied {nameof(IRenderHost)} does not implement {nameof(IRawInputSource)}.");
        }

        /// <param name="Host">Consider using an instance of <see cref="GameRenderHost{TObservableGame}"/> to quickly create an implementation of <see cref="IRenderHost"/></param>
        /// <param name="RawInputSource">Optional explicit raw-input provider. If null, <paramref name="Host"/> must implement <see cref="IRawInputSource"/>.</param>
        public MainRenderer(IRenderHost Host, IRawInputSource RawInputSource = null, IUISurface Surface = null, IUIAssetProvider AssetProvider = null)
        {
            this.Host = Host;
            this.RawInputSource = ResolveInputSource(Host, RawInputSource);
            this.Surface = Surface ?? new BackBufferSurface(Host);
            SpriteBatch = new(GraphicsDevice);
            PrimitiveBatch = new(GraphicsDevice, 1024);
            TexturedPrimitiveEffect = new(GraphicsDevice) { TextureEnabled = true, VertexColorEnabled = true, LightingEnabled = false };
            Content = new(Host, "Content");
            FontManager = new(Content, "Arial");
            this.AssetProvider = AssetProvider ?? new RendererAssetProvider(Content, FontManager);
            TextEngine = new SpriteFontTextEngine(FontManager);
            Input = new();

            ScrollMarker = Content.Load<Texture2D>(Path.Combine("Icons", "ScrollMarker"));

            Host.PreviewUpdate += (sender, e) =>
            {
                UpdateArgs = new(e, e.Subtract(PreviousUpdateTimeSpan), this.RawInputSource.GetMouseState(), this.RawInputSource.GetKeyboardState());
                PreviousUpdateTimeSpan = e;
                Input.Update(UpdateArgs);
            };

            Host.EndUpdate += (sender, e) =>
            {
                Input.Mouse.UpdateHandlers();
                Input.Keyboard.UpdateHandlers();
                EndUpdate?.Invoke(this, EventArgs.Empty);
            };
        }

        #region Textures
        public readonly Texture2D ScrollMarker;

        #region Solid Color
        private readonly Dictionary<Color, SolidColorTexture> SolidColorTextures = new();

        public SolidColorTexture GetOrCreateSolidColorTexture(Color Color)
        {
            if (!SolidColorTextures.TryGetValue(Color, out SolidColorTexture Result))
            {
                Result = new(GraphicsDevice, Color);
                SolidColorTextures.Add(Color, Result);
            }

            return Result;
        }
        #endregion Solid Color

        #region Circles
        private const int MinimumCircleTextureRadius = 32;
        private const int MaximumCircleTextureRadius = 1024;

        private readonly Dictionary<int, Texture2D> CircleTextures = new();

        /// <summary>Returns a circle texture whose radius is at least as big as <paramref name="DesiredRadius"/>.<para/>
        /// Attempts to round <paramref name="DesiredRadius"/> up to the nearest power of 2 to prevent generating too many textures.</summary>
        /// <param name="MinimumRadius">If null, uses <see cref="Math.Floor(double)"/> of <paramref name="DesiredRadius"/>.<para/>
        /// The absolute minimum radius used is <see cref="MinimumCircleTextureRadius"/></param>
        /// <param name="MaximumRadius">If null, uses <see cref="GeneralUtils.NextPowerOf2(float)"/> of <paramref name="DesiredRadius"/>.<para/>
        /// The absolute maximum radius used is <see cref="MaximumCircleTextureRadius"/></param>
        public Texture2D GetOrCreateWhiteCircleTexture(float DesiredRadius, int? MinimumRadius = null, int? MaximumRadius = null)
        {
            MaximumRadius = Math.Clamp(MaximumRadius ?? GeneralUtils.NextPowerOf2(DesiredRadius), MinimumCircleTextureRadius, MaximumCircleTextureRadius);
            MinimumRadius = Math.Clamp(MinimumRadius ?? (int)Math.Floor(DesiredRadius), MinimumCircleTextureRadius, MaximumRadius.Value);

            //  A loop, not LINQ: this runs for every circle and point drawn, and the query was 504 bytes per call.
            Texture2D BestMatch = null;
            float BestDifference = float.MaxValue;
            foreach (KeyValuePair<int, Texture2D> Entry in CircleTextures)
            {
                if (Entry.Value == null || Entry.Value.IsDisposed)
                {
                    continue;
                }

                if (Entry.Key >= MinimumRadius && Entry.Key <= MaximumRadius)
                {
                    float Difference = Math.Abs(DesiredRadius - Entry.Key);
                    if (Difference < BestDifference)
                    {
                        BestDifference = Difference;
                        BestMatch = Entry.Value;
                    }
                }
            }

            if (BestMatch != null)
            {
                return BestMatch;
            }
            else
            {
                DesiredRadius = Math.Min(DesiredRadius, MaximumRadius.Value);
                int ActualRadius = Math.Clamp(GeneralUtils.NextPowerOf2(DesiredRadius), MinimumRadius.Value, MaximumRadius.Value);
                Texture2D Circle = TextureUtils.CreateCircleTexture(SpriteBatch, ActualRadius, Color.White, true);
                CircleTextures[ActualRadius] = Circle;
                return Circle;
            }
        }

        private void ClearDisposedCircleTextures()
        {
            IEnumerable<int> InvalidKeys = CircleTextures.Where(x => x.Value == null || x.Value.IsDisposed).Select(x => x.Key);
            if (InvalidKeys.Any())
            {
                foreach (int Key in InvalidKeys)
                {
                    CircleTextures.Remove(Key);
                }
            }
        }
        #endregion Circles
        #endregion Textures
    }
}
