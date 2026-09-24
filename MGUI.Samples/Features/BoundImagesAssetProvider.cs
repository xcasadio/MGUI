using System;
using System.IO;
using MGUI.Backend.MonoGame;
using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;

namespace MGUI.Samples.Features
{
    /// <summary>Decorates the sample app's own <see cref="IUIAssetProvider"/> (ADR-0016, "Host resolution of image
    /// names" and "Animated image sources") so <see cref="BoundImagesSample"/> can name two host-resolved sources --
    /// a static sprite cut from the "AngryMeteor" sheet already loaded by <see cref="MGUI.Core.UI.MGDesktop.LoadDefaultResources"/>,
    /// and a small animation cycling through four cells of that same sheet -- without registering them through
    /// <c>MGResources.AddTexture</c> up front. Every other name is forwarded to the wrapped provider unchanged, so
    /// nothing else the sample app loads is affected.</summary>
    internal sealed class BoundImagesAssetProvider : IUIAssetProvider
    {
        /// <summary>The name <see cref="MGUI.Core.UI.MGImage.SourceName"/> uses for the host-resolved static sprite.</summary>
        public const string HostSpriteName = "BoundImages.HostSprite";

        /// <summary>The name <see cref="MGUI.Core.UI.MGImage.SourceName"/> uses for the host-resolved animation.</summary>
        public const string AnimatedSpriteName = "BoundImages.Anim";

        //  Same grid as MGDesktop.LoadDefaultResources' "Sample Icons" region: 16x16 cells, 1px spacing, 6px top margin.
        private const int CellSize = 16;
        private const int CellSpacing = 1;
        private const int TopMargin = 6;

        private static Rectangle CellRect(int column, int row) =>
            new(column * (CellSize + CellSpacing), TopMargin + row * (CellSize + CellSpacing), CellSize, CellSize);

        //  The four arrow cells used by AnimationDemo.xaml's "FramesTarget" (row/column only, see MGDesktop.LoadDefaultResources).
        private static readonly Rectangle[] AnimationFrames =
        {
            CellRect(0, 0), // ArrowRightGreen
            CellRect(1, 0), // ArrowDownGreen
            CellRect(0, 1), // ArrowLeftGreen
            CellRect(1, 1), // ArrowUpGreen
        };

        //  The "Backpack" cell: a single static sprite distinct from the animation's own frames.
        private static readonly Rectangle HostSpriteRect = CellRect(11, 1);

        private readonly IUIAssetProvider _inner;
        private IUIImageResource _sheet;

        public BoundImagesAssetProvider(IUIAssetProvider inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        private IUIImageResource Sheet => _sheet ??= _inner.LoadImage(Path.Combine("Icons", "AngryMeteor_MilitaryIconsSet"));

        public IUIImageResource LoadImage(string assetName) => _inner.LoadImage(assetName);

        public bool TryLoadImage(string assetName, out IUIImageResource image) => _inner.TryLoadImage(assetName, out image);

        public bool TryResolveImage(string name, out IUIImageResource image, out Rectangle? sourceRect)
        {
            if (name == HostSpriteName)
            {
                image = Sheet;
                sourceRect = HostSpriteRect;
                return true;
            }

            return _inner.TryResolveImage(name, out image, out sourceRect);
        }

        public bool TryCreateAnimatedImage(string name, out IUIAnimatedImage animatedImage)
        {
            if (name == AnimatedSpriteName)
            {
                animatedImage = new ArrowCycleAnimatedImage(Sheet, AnimationFrames);
                return true;
            }

            return _inner.TryCreateAnimatedImage(name, out animatedImage);
        }

        /// <summary>A four-frame, looping animation over fixed cells of one shared sheet (ADR-0016, "Animated image
        /// sources"): advances and reads its current frame allocating nothing. Does not own <see cref="_sheet"/> --
        /// disposing an instance never disposes the shared sheet texture, since every <see cref="MGUI.Core.UI.MGImage"/>
        /// that names <see cref="AnimatedSpriteName"/> gets its own instance over the same sheet.</summary>
        private sealed class ArrowCycleAnimatedImage : IUIAnimatedImage
        {
            private static readonly TimeSpan FrameDuration = TimeSpan.FromMilliseconds(200);

            private readonly IUIImageResource _sheet;
            private readonly Rectangle[] _frames;
            private TimeSpan _elapsed;

            public ArrowCycleAnimatedImage(IUIImageResource sheet, Rectangle[] frames)
            {
                _sheet = sheet;
                _frames = frames;
            }

            private int CurrentFrameIndex
            {
                get
                {
                    long loopTicks = FrameDuration.Ticks * _frames.Length;
                    long ticksIntoLoop = _elapsed.Ticks % loopTicks;
                    if (ticksIntoLoop < 0)
                    {
                        ticksIntoLoop += loopTicks;
                    }

                    return (int)(ticksIntoLoop / FrameDuration.Ticks);
                }
            }

            public void Advance(TimeSpan elapsed) => _elapsed += elapsed;
            public void Restart(TimeSpan startOffset) => _elapsed = startOffset;
            public IUIImageResource CurrentImage => _sheet;
            public Rectangle? CurrentSourceRect => _frames[CurrentFrameIndex];
            public Point CurrentDrawOffset => Point.Zero;
            public void Dispose() { }
        }
    }

    /// <summary>Decorates an <see cref="IMonoGameDesktopBackend"/> so <see cref="MGUI.Core.UI.MGDesktop"/> resolves image
    /// names through a <see cref="BoundImagesAssetProvider"/> instead of the app's own provider directly, without
    /// otherwise changing how the sample app loads its resources: every other member, including
    /// <see cref="IMonoGameDesktopBackend.Host"/> (checked elsewhere via <c>Desktop.Runtime is IMonoGameDesktopBackend</c>,
    /// see <c>PerformanceTest.xaml.cs</c>, <c>SampleHUD.xaml.cs</c>, <c>Controls/ListBox.xaml.cs</c>) and
    /// <see cref="IMonoGameDesktopBackend.FontManager"/>, is forwarded unchanged.</summary>
    internal sealed class BoundImagesRuntime : IMonoGameDesktopBackend
    {
        private readonly IMonoGameDesktopBackend _inner;

        public BoundImagesRuntime(IMonoGameDesktopBackend inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            AssetProvider = new BoundImagesAssetProvider(inner.AssetProvider);
        }

        public IUIAssetProvider AssetProvider { get; }

        public IRenderHost Host => _inner.Host;
        public MGUI.Shared.Text.FontManager FontManager => _inner.FontManager;

        public MGUI.Shared.Input.InputTracker Input => _inner.Input;
        public string DefaultFontFamily => _inner.DefaultFontFamily;
        public IUISurface Surface => _inner.Surface;
        public MGUI.Shared.Text.Engines.ITextMeasurementEngine TextEngine
        {
            get => _inner.TextEngine;
            set => _inner.TextEngine = value;
        }
        public event EventHandler<MGUI.Shared.Helpers.EventArgs<MGUI.Shared.Text.Engines.ITextMeasurementEngine>> TextEngineChanged
        {
            add => _inner.TextEngineChanged += value;
            remove => _inner.TextEngineChanged -= value;
        }
        public event EventHandler<EventArgs> EndUpdate
        {
            add => _inner.EndUpdate += value;
            remove => _inner.EndUpdate -= value;
        }
        public UpdateBaseArgs UpdateArgs => _inner.UpdateArgs;

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings Settings, bool DeferBegin) => _inner.CreateDrawTransaction(Settings, DeferBegin);
        public void RegisterView(IUIView View) => _inner.RegisterView(View);
    }
}
