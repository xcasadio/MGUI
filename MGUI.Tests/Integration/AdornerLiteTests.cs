using MGUI.Core.UI;
using MGUI.Core.UI.Adorners;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System;
using System.Collections.Generic;

namespace MGUI.Tests.Integration;

public class AdornerLiteTests
{
    [Fact]
    public void DockPreviewOverlay_ShowAndHide_UseExplicitBounds()
    {
        AdornerTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default));

        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 24, 24, 400, 260) { WindowStyle = WindowStyle.None };
        MGDockPreviewOverlay preview = new(window);

        preview.Show(new Rectangle(30, 40, 150, 70));

        Assert.True(preview.IsPreviewVisible);
        Assert.Equal(Visibility.Visible, preview.Visibility);
        Assert.True(preview.TryGetAdornedBounds(out Rectangle previewBounds));
        Assert.Equal(new Rectangle(30, 40, 150, 70), previewBounds);

        preview.Hide();

        Assert.False(preview.IsPreviewVisible);
        Assert.Equal(Visibility.Collapsed, preview.Visibility);
    }

    [Fact]
    public void BoundsAdorner_TargetElement_FollowsActualLayoutBounds()
    {
        AdornerHarness harness = CreateHarness();

        Assert.True(harness.BoundsAdorner.TryGetAdornedBounds(out Rectangle adornedBounds));
        Assert.Equal(harness.Target.ActualLayoutBounds, adornedBounds);
    }

    [Fact]
    public void AdornerLayer_DefaultsToNonInteractiveOverlay()
    {
        AdornerTestRuntime runtime = new(new Rectangle(0, 0, 320, 200));
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default));

        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 320, 200) { WindowStyle = WindowStyle.None };
        MGAdornerLayer layer = new(window);

        Assert.False(layer.IsHitTestVisible);
        Assert.False(layer.ClipToBounds);
        Assert.Equal(HorizontalAlignment.Stretch, layer.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Stretch, layer.VerticalAlignment);
    }

    private static AdornerHarness CreateHarness()
    {
        AdornerTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default));

        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 32, 32, 480, 320)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0)
        };

        MGOverlayPanel root = new(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(0)
        };

        MGRectangle target = new(window, 160, 80, new Color(44, 54, 73), 0, Color.Transparent)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(40, 28, 0, 0),
            Name = "SelectionTarget"
        };

        MGAdornerLayer layer = new(window);
        MGBoundsAdorner boundsAdorner = new(window)
        {
            TargetElement = target,
            BorderThickness = 2,
            BorderColor = new Color(0, 122, 204, 255),
            FillColor = Color.Transparent
        };

        Assert.True(layer.TryAddAdorner(boundsAdorner, 10));
        Assert.True(root.TryAddChild(target));
        Assert.True(root.TryAddChild(layer, default, 100));

        window.SetContent(root);
        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();

        return new(runtime, desktop, window, root, target, layer, boundsAdorner);
    }

    private readonly record struct AdornerHarness(
        AdornerTestRuntime Runtime,
        MGDesktop Desktop,
        MGWindow Window,
        MGOverlayPanel Root,
        MGRectangle Target,
        MGAdornerLayer Layer,
        MGBoundsAdorner BoundsAdorner);

    private sealed class AdornerTestRuntime : IUIDesktopRuntime
    {
        private ITextMeasurementEngine _textEngine;

        public InputTracker Input { get; } = new();
        public string DefaultFontFamily { get; } = "TestSans";
        public IUISurface Surface { get; }
        public IUIAssetProvider AssetProvider { get; }
        public UpdateBaseArgs UpdateArgs { get; private set; } = new(TimeSpan.Zero, TimeSpan.Zero, default, default);

        public event EventHandler<EventArgs<ITextMeasurementEngine>>? TextEngineChanged;
        public event EventHandler<EventArgs>? EndUpdate
        {
            add { }
            remove { }
        }

        public ITextMeasurementEngine TextEngine
        {
            get => _textEngine;
            set
            {
                ITextMeasurementEngine previous = _textEngine;
                _textEngine = value ?? throw new ArgumentNullException(nameof(value));
                TextEngineChanged?.Invoke(this, new(previous, _textEngine));
            }
        }

        public AdornerTestRuntime(Rectangle surfaceBounds)
        {
            Surface = new AdornerTestSurface(surfaceBounds, new AdornerTestRenderTarget(surfaceBounds.Width, surfaceBounds.Height));
            AssetProvider = new AdornerTestAssetProvider();
            _textEngine = new AdornerTestTextEngine(DefaultFontFamily);
        }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings Settings, bool DeferBegin)
            => new AdornerNoOpDrawTransaction(this, Settings ?? DrawSettings.Default);

        public void ApplyFrame(UpdateBaseArgs updateArgs)
        {
            UpdateArgs = updateArgs;
            Input.Update(updateArgs);
        }

        public void RegisterView(IUIView View)
        {
        }
    }

    private sealed class AdornerNoOpDrawTransaction : IUIDrawTransaction
    {
        private Rectangle? _currentClipBounds;

        public DrawSettings CurrentSettings { get; private set; }
        public IUIDesktopRuntime Renderer { get; }
        public Rectangle? CurrentClipBounds => _currentClipBounds;

        public AdornerNoOpDrawTransaction(IUIDesktopRuntime renderer, DrawSettings settings)
        {
            Renderer = renderer;
            CurrentSettings = settings;
        }

        public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask)
        {
        }

        public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask,
            Vector2 Origin, float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None)
        {
        }

        public void DrawTextureAt(IUIImageResource Texture, Rectangle? Source, Vector2 Destination, Color ColorMask,
            Vector2 Origin, float Rotation = 0f, float ScaleX = 1f, float ScaleY = 1f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None)
        {
        }

        public void DrawTextViaEngine(ResolvedFont Font, string Text, Vector2 Position, Color Color, Vector2 Origin, float Scale,
            float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None)
        {
        }

        public void FillRectangle(Vector2 Origin, RectangleF Destination, Color Color)
        {
        }

        public void FillPoint(Vector2 Center, Color Color, float Width)
        {
        }

        public void StrokeRectangle(Vector2 Origin, RectangleF Destination, Color Color, Thickness Thickness)
        {
        }

        public void StrokeAndFillRectangle(Vector2 Origin, RectangleF Destination, Color StrokeColor, Color FillColor, Thickness StrokeThickness)
        {
        }

        public void FillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color Color)
        {
        }

        public void StrokeAndFillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color StrokeColor, Color FillColor, float StrokeThickness = 1.0f)
        {
        }

        public void FillTriangle(Vector2 Origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2)
        {
        }

        public void FillQuadrilateralLinearClamp(Vector2 Origin, Vector2 topLeft, Color topLeftColor, Vector2 topRight, Color topRightColor,
            Vector2 bottomRight, Color bottomRightColor, Vector2 bottomLeft, Color bottomLeftColor)
        {
        }

        public void StrokeLineSegment(Vector2 Origin, Vector2 Start, Vector2 End, Color Color, float Thickness = 1.0f)
        {
        }

        public void FillCircle(Vector2 Center, Color Color, float Radius, int NumSides = 32)
        {
        }

        public void StrokeCircle(Vector2 Center, Color Color, float Radius, float Thickness = 1.0f, int NumSides = 32)
        {
        }

        public void StrokeAndFillCircle(Vector2 Center, Color StrokeColor, Color FillColor, float Radius, float StrokeThickness = 1.0f, int NumSides = 32)
        {
        }

        public IDisposable SetDrawSettingsTemporary(DrawSettings Settings)
        {
            DrawSettings previous = CurrentSettings;
            CurrentSettings = Settings ?? DrawSettings.Default;
            return new DisposableAction(() => CurrentSettings = previous);
        }

        public IDisposable SetRenderTargetTemporary(IUIRenderTarget New, Color? ClearColor)
            => new DisposableAction(() => { });

        public IDisposable SetTransformTemporary(Matrix Transform)
            => SetDrawSettingsTemporary(CurrentSettings with { Transform = Transform });

        public ClipResolveResult ResolveClip(ClipDefinition Definition)
        {
            ClipDefinition effective = Definition ?? ClipDefinition.None();
            return new(effective, effective, ClipStrategy.Scissor, false);
        }

        public ClipScope PushClipTemporary(ClipDefinition Definition)
        {
            ClipResolveResult resolution = ResolveClip(Definition);
            Rectangle? previous = _currentClipBounds;
            _currentClipBounds = resolution.Effective.Kind == ClipKind.None ? null : resolution.Effective.Shape.Bounds;
            return new(resolution, () => _currentClipBounds = previous);
        }

        public ClipScope PushRectangleClip(Rectangle? Bounds, bool IntersectWithCurrentClipTarget)
        {
            ClipDefinition definition = Bounds.HasValue
                ? ClipDefinition.Rectangle(Bounds.Value, IntersectWithCurrentClipTarget)
                : ClipDefinition.None(IntersectWithCurrentClipTarget);
            return PushClipTemporary(definition);
        }

        public IDisposable SetClipTargetTemporary(Rectangle? Bounds, bool IntersectWithCurrentClipTarget)
            => PushRectangleClip(Bounds, IntersectWithCurrentClipTarget);

        public void Dispose()
        {
        }
    }

    private sealed class AdornerTestSurface : IUISurface
    {
        private readonly Rectangle _bounds;
        private readonly IUIRenderTarget _renderTarget;

        public AdornerTestSurface(Rectangle bounds, IUIRenderTarget renderTarget)
        {
            _bounds = bounds;
            _renderTarget = renderTarget;
        }

        public Rectangle GetBounds() => _bounds;

        public IUIRenderTarget GetRenderTarget() => _renderTarget;
    }

    private sealed class AdornerTestAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, AdornerTestImageResource> _images = new(StringComparer.OrdinalIgnoreCase);

        public IUIImageResource LoadImage(string assetName)
        {
            if (!_images.TryGetValue(assetName, out AdornerTestImageResource image))
            {
                image = new(assetName, 16, 16);
                _images[assetName] = image;
            }

            return image;
        }

        public bool TryLoadImage(string assetName, out IUIImageResource image)
        {
            image = LoadImage(assetName);
            return true;
        }
    }

    private class AdornerTestImageResource : IUIImageResource
    {
        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed { get; private set; }

        public AdornerTestImageResource(string id, int width, int height)
        {
            Id = id;
            Width = width;
            Height = height;
        }
    }

    private sealed class AdornerTestRenderTarget : AdornerTestImageResource, IUIRenderTarget
    {
        public AdornerTestRenderTarget(int width, int height)
            : base("adorner-render-target", width, height)
        {
        }
    }

    private sealed class AdornerTestTextEngine : ITextMeasurementEngine
    {
        private readonly string _defaultFontFamily;

        public AdornerTestTextEngine(string defaultFontFamily)
        {
            _defaultFontFamily = defaultFontFamily;
        }

        public ResolvedFont ResolveFont(FontSpec spec)
        {
            int size = Math.Max(1, spec.Size);
            FontSpec effectiveSpec = string.IsNullOrWhiteSpace(spec.Family)
                ? FontSpec.Normal(_defaultFontFamily, size)
                : spec;

            return new ResolvedFont(effectiveSpec, size, 1.0f, 1.0f, size, Math.Max(1.0f, size * 0.5f), Vector2.Zero, false, new object());
        }

        public Vector2 MeasureText(ResolvedFont font, string text)
        {
            float width = (text?.Length ?? 0) * Math.Max(font.SpaceWidth, 1.0f);
            return new(width, font.LineHeight);
        }

        public GlyphMetrics MeasureGlyph(ResolvedFont font, char c)
            => new(0.0f, Math.Max(font.SpaceWidth, 1.0f), 0.0f, font.LineHeight);

        public float GetLineHeight(ResolvedFont font) => font.LineHeight;

        public float GetSpaceWidth(ResolvedFont font) => font.SpaceWidth;

        public void InvalidateCache()
        {
        }

        public int GetIndexOfPreviousGlyph(ResolvedFont font, string text, int startIndex)
            => Math.Max(0, startIndex - 1);

        public int GetIndexOfNextGlyph(ResolvedFont font, string text, int startIndex)
            => Math.Min(text?.Length ?? 0, startIndex + 1);

        public int GetIndexAtPosition(ResolvedFont font, string text, float x)
        {
            float width = Math.Max(font.SpaceWidth, 1.0f);
            return Math.Clamp((int)(x / width), 0, text?.Length ?? 0);
        }

        public float GetCaretOffset(ResolvedFont font, string text, int index)
            => Math.Clamp(index, 0, text?.Length ?? 0) * Math.Max(font.SpaceWidth, 1.0f);
    }

    private sealed class DisposableAction : IDisposable
    {
        private readonly Action _disposeAction;
        private bool _disposed;

        public DisposableAction(Action disposeAction)
        {
            _disposeAction = disposeAction;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _disposeAction();
        }
    }
}