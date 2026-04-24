using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.Tests.Integration;

public class EngineOwnedRenderingProofTests
{
    [Fact]
    public void ProofRuntime_DrawTransaction_CanOwnShapesTextAndOffscreenBuffers()
    {
        ProofDesktopRuntime runtime = new(new Rectangle(0, 0, 320, 180));
        ResolvedFont font = runtime.ProofTextEngine.ResolveFont(FontSpec.Normal(runtime.DefaultFontFamily, 14));
        ProofRenderTarget buffer = new("proof-buffer", 128, 64);

        using ProofDrawTransaction transaction = Assert.IsType<ProofDrawTransaction>(runtime.CreateDrawTransaction(DrawSettings.Default, false));
        using (transaction.SetRenderTargetTemporary(buffer, Color.Transparent))
        {
            transaction.FillRectangle(Vector2.Zero, new RectangleF(0, 0, 40, 20), Color.Red);
            transaction.DrawTextViaEngine(font, "Proof", new Vector2(4, 4), Color.White, Vector2.Zero, 1.0f);
        }

        Assert.Contains(transaction.Calls, x => x.Kind == ProofDrawCallKind.SetRenderTarget && x.RenderTargetId == buffer.Id);
        Assert.Contains(transaction.Calls, x => x.Category == ProofDrawCallCategory.Shape && x.RenderTargetId == buffer.Id);
        Assert.Contains(transaction.Calls, x => x.Kind == ProofDrawCallKind.DrawText && x.RenderTargetId == buffer.Id && x.Text == "Proof");
        Assert.True(buffer.RecordedCalls.Count >= 2);
    }

    [Fact]
    public void MGDesktop_UIView_CanDelegateShapesTextAndSurfaceBufferToProofBackend()
    {
        ProofDesktopRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        runtime.AdvanceFrame(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));

        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 24, 32, 260, 140)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0)
        };
        MGBorder border = new(window, new Thickness(2), new MGUniformBorderBrush(Color.CornflowerBlue))
        {
            Padding = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        border.SetContent(new MGTextBlock(window, "Engine-owned proof", Color.White, 18));
        window.SetContent(border);
        desktop.Windows.Add(window);

        desktop.Update();

        using ProofDrawTransaction transaction = Assert.IsType<ProofDrawTransaction>(runtime.CreateDrawTransaction(DrawSettings.Default, false));
        desktop.View.Draw(transaction, 1.0f);

        Assert.Contains(runtime.RegisteredViews, x => ReferenceEquals(x, desktop.View));
        Assert.NotNull(runtime.SurfaceRenderTarget);
        Assert.Contains(transaction.Calls, x => x.Kind == ProofDrawCallKind.SetRenderTarget && x.RenderTargetId == runtime.SurfaceRenderTarget!.Id);
        Assert.Contains(transaction.Calls, x => x.Category == ProofDrawCallCategory.Shape && x.RenderTargetId == runtime.SurfaceRenderTarget!.Id);
        Assert.Contains(transaction.Calls, x => x.Kind == ProofDrawCallKind.DrawText && x.RenderTargetId == runtime.SurfaceRenderTarget!.Id && x.Text == "Engine-owned proof");
        Assert.True(runtime.SurfaceRenderTarget.RecordedCalls.Count >= 2);
    }

    private enum ProofDrawCallCategory
    {
        State,
        Shape,
        Text,
        Image,
        Clip,
    }

    private enum ProofDrawCallKind
    {
        SetRenderTarget,
        SetDrawSettings,
        SetClip,
        DrawTextureTo,
        DrawTextureAt,
        DrawText,
        FillRectangle,
        FillPoint,
        StrokeRectangle,
        StrokeAndFillRectangle,
        FillPolygon,
        StrokeAndFillPolygon,
        FillTriangle,
        FillQuadrilateralLinearClamp,
        StrokeLineSegment,
        FillCircle,
        StrokeCircle,
        StrokeAndFillCircle,
    }

    private sealed record ProofDrawCall(
        ProofDrawCallKind Kind,
        ProofDrawCallCategory Category,
        string? RenderTargetId,
        string? AssetId = null,
        string? Text = null,
        Rectangle? Rectangle = null,
        RectangleF? RectangleF = null,
        int? VertexCount = null,
        Color? ClearColor = null,
        Rectangle? ClipBounds = null);

    private sealed class ProofDesktopRuntime : IUIDesktopRuntime
    {
        private ITextMeasurementEngine _textEngine;

        public InputTracker Input { get; } = new();
        public string DefaultFontFamily { get; } = "ProofSans";
        public IUISurface Surface { get; }
        public IUIAssetProvider AssetProvider { get; }
        public ProofTextEngine ProofTextEngine { get; }
        public List<IUIView> RegisteredViews { get; } = new();
        public List<ProofDrawCall> Calls { get; } = new();
        public ProofRenderTarget SurfaceRenderTarget { get; }
        public UpdateBaseArgs UpdateArgs { get; private set; }

        public event EventHandler<EventArgs<ITextMeasurementEngine>>? TextEngineChanged;
        public event EventHandler<EventArgs>? EndUpdate;

        public ITextMeasurementEngine TextEngine
        {
            get => _textEngine;
            set
            {
                if (value is not ITextDrawEngine)
                {
                    throw new ArgumentException($"{nameof(TextEngine)} must also implement {nameof(ITextDrawEngine)}.", nameof(value));
                }

                ITextMeasurementEngine previous = _textEngine;
                _textEngine = value ?? throw new ArgumentNullException(nameof(value));
                TextEngineChanged?.Invoke(this, new(previous, _textEngine));
            }
        }

        public ProofDesktopRuntime(Rectangle surfaceBounds)
        {
            SurfaceRenderTarget = new("surface-buffer", surfaceBounds.Width, surfaceBounds.Height);
            Surface = new ProofSurface(surfaceBounds, SurfaceRenderTarget);
            AssetProvider = new ProofAssetProvider();
            ProofTextEngine = new ProofTextEngine();
            UpdateArgs = new(TimeSpan.Zero, TimeSpan.Zero, default, default);
            _textEngine = ProofTextEngine;
        }

        public void AdvanceFrame(TimeSpan totalElapsed, TimeSpan frameElapsed)
        {
            UpdateArgs = new(totalElapsed, frameElapsed, default, default);
            Input.Update(UpdateArgs);
            EndUpdate?.Invoke(this, EventArgs.Empty);
        }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings Settings, bool DeferBegin)
            => new ProofDrawTransaction(this, Settings ?? DrawSettings.Default);

        public void RegisterView(IUIView View)
        {
            if (View != null && !RegisteredViews.Contains(View))
            {
                RegisteredViews.Add(View);
            }
        }
    }

    private sealed class ProofDrawTransaction : IUIDrawTransaction
    {
        private readonly ProofDesktopRuntime _runtime;
        private ProofRenderTarget? _currentRenderTarget;
        private Rectangle? _currentClipBounds;

        public List<ProofDrawCall> Calls { get; } = new();
        public DrawSettings CurrentSettings { get; private set; }
        public IUIDesktopRuntime Renderer => _runtime;
        public Rectangle? CurrentClipBounds => _currentClipBounds;

        public ProofDrawTransaction(ProofDesktopRuntime runtime, DrawSettings settings)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            CurrentSettings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask)
            => Record(ProofDrawCallKind.DrawTextureTo, ProofDrawCallCategory.Image, assetId: DescribeImage(Texture), rectangle: Destination);

        public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask,
            Vector2 Origin, float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None)
            => Record(ProofDrawCallKind.DrawTextureTo, ProofDrawCallCategory.Image, assetId: DescribeImage(Texture), rectangle: Destination);

        public void DrawTextureAt(IUIImageResource Texture, Rectangle? Source, Vector2 Destination, Color ColorMask,
            Vector2 Origin, float Rotation = 0f, float ScaleX = 1f, float ScaleY = 1f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None)
            => Record(ProofDrawCallKind.DrawTextureAt, ProofDrawCallCategory.Image, assetId: DescribeImage(Texture), rectangle: new Rectangle((int)Destination.X, (int)Destination.Y, Texture.Width, Texture.Height));

        public void DrawTextViaEngine(ResolvedFont Font, string Text, Vector2 Position, Color Color, Vector2 Origin, float Scale,
            float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None)
        {
            if (string.IsNullOrEmpty(Text) || Font?.IsAvailable != true)
            {
                return;
            }

            if (_runtime.TextEngine is not ITextDrawEngine drawEngine)
            {
                throw new InvalidOperationException($"{nameof(ProofDesktopRuntime.TextEngine)} must implement {nameof(ITextDrawEngine)}.");
            }

            drawEngine.DrawText(this, Font, Text, Position, Color, Origin, Scale, Rotation, Depth, Flip);
        }

        public void FillRectangle(Vector2 Origin, RectangleF Destination, Color Color)
            => Record(ProofDrawCallKind.FillRectangle, ProofDrawCallCategory.Shape, rectangleF: Destination);

        public void FillPoint(Vector2 Center, Color Color, float Width)
            => Record(ProofDrawCallKind.FillPoint, ProofDrawCallCategory.Shape);

        public void StrokeRectangle(Vector2 Origin, RectangleF Destination, Color Color, Thickness Thickness)
            => Record(ProofDrawCallKind.StrokeRectangle, ProofDrawCallCategory.Shape, rectangleF: Destination);

        public void StrokeAndFillRectangle(Vector2 Origin, RectangleF Destination, Color StrokeColor, Color FillColor, Thickness StrokeThickness)
            => Record(ProofDrawCallKind.StrokeAndFillRectangle, ProofDrawCallCategory.Shape, rectangleF: Destination);

        public void FillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color Color)
            => Record(ProofDrawCallKind.FillPolygon, ProofDrawCallCategory.Shape, vertexCount: Vertices?.Count() ?? 0);

        public void StrokeAndFillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color StrokeColor, Color FillColor, float StrokeThickness = 1.0f)
            => Record(ProofDrawCallKind.StrokeAndFillPolygon, ProofDrawCallCategory.Shape, vertexCount: Vertices?.Count() ?? 0);

        public void FillTriangle(Vector2 Origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2)
            => Record(ProofDrawCallKind.FillTriangle, ProofDrawCallCategory.Shape, vertexCount: 3);

        public void FillQuadrilateralLinearClamp(Vector2 Origin, Vector2 topLeft, Color topLeftColor, Vector2 topRight, Color topRightColor,
            Vector2 bottomRight, Color bottomRightColor, Vector2 bottomLeft, Color bottomLeftColor)
            => Record(ProofDrawCallKind.FillQuadrilateralLinearClamp, ProofDrawCallCategory.Shape, vertexCount: 4);

        public void StrokeLineSegment(Vector2 Origin, Vector2 Start, Vector2 End, Color Color, float Thickness = 1.0f)
            => Record(ProofDrawCallKind.StrokeLineSegment, ProofDrawCallCategory.Shape, vertexCount: 2);

        public void FillCircle(Vector2 Center, Color Color, float Radius, int NumSides = 32)
            => Record(ProofDrawCallKind.FillCircle, ProofDrawCallCategory.Shape);

        public void StrokeCircle(Vector2 Center, Color Color, float Radius, float Thickness = 1.0f, int NumSides = 32)
            => Record(ProofDrawCallKind.StrokeCircle, ProofDrawCallCategory.Shape);

        public void StrokeAndFillCircle(Vector2 Center, Color StrokeColor, Color FillColor, float Radius, float StrokeThickness = 1.0f, int NumSides = 32)
            => Record(ProofDrawCallKind.StrokeAndFillCircle, ProofDrawCallCategory.Shape);

        public IDisposable SetDrawSettingsTemporary(DrawSettings Settings)
        {
            DrawSettings previous = CurrentSettings;
            CurrentSettings = Settings ?? throw new ArgumentNullException(nameof(Settings));
            Record(ProofDrawCallKind.SetDrawSettings, ProofDrawCallCategory.State);

            return new DisposableAction(() => CurrentSettings = previous);
        }

        public IDisposable SetRenderTargetTemporary(IUIRenderTarget New, Color? ClearColor)
        {
            ProofRenderTarget next = New as ProofRenderTarget ?? throw new InvalidOperationException("Proof backend requires proof render targets.");
            ProofRenderTarget? previous = _currentRenderTarget;
            _currentRenderTarget = next;
            Record(ProofDrawCallKind.SetRenderTarget, ProofDrawCallCategory.State, renderTargetId: next.Id, clearColor: ClearColor);

            return new DisposableAction(() =>
            {
                _currentRenderTarget = previous;
                Record(ProofDrawCallKind.SetRenderTarget, ProofDrawCallCategory.State, renderTargetId: previous?.Id, clearColor: null);
            });
        }

        public IDisposable SetTransformTemporary(Matrix Transform)
            => SetDrawSettingsTemporary(CurrentSettings with { Transform = Transform });

        public ClipResolveResult ResolveClip(ClipDefinition Definition)
            => ClipStrategyResolver.Resolve(Definition ?? ClipDefinition.None(), ClipBackendCapabilities.Default);

        public ClipScope PushClipTemporary(ClipDefinition Definition)
        {
            ClipResolveResult resolution = ResolveClip(Definition);
            Rectangle? previous = _currentClipBounds;
            Rectangle? next = resolution.Strategy == ClipStrategy.None ? null : resolution.Effective.Shape.Bounds;
            if (next.HasValue && previous.HasValue && resolution.Effective.IntersectWithCurrentClip)
            {
                next = Rectangle.Intersect(previous.Value, next.Value);
            }

            _currentClipBounds = next;
            Record(ProofDrawCallKind.SetClip, ProofDrawCallCategory.Clip, clipBounds: _currentClipBounds);

            return new ClipScope(resolution, () =>
            {
                _currentClipBounds = previous;
                Record(ProofDrawCallKind.SetClip, ProofDrawCallCategory.Clip, clipBounds: _currentClipBounds);
            });
        }

        public ClipScope PushRectangleClip(Rectangle? Bounds, bool IntersectWithCurrentClipTarget)
            => PushClipTemporary(Bounds.HasValue ? ClipDefinition.Rectangle(Bounds.Value, IntersectWithCurrentClipTarget) : ClipDefinition.None(false));

        public IDisposable SetClipTargetTemporary(Rectangle? Bounds, bool IntersectWithCurrentClipTarget)
            => PushRectangleClip(Bounds, IntersectWithCurrentClipTarget);

        public void Dispose()
        {
        }

        internal void RecordText(string text)
            => Record(ProofDrawCallKind.DrawText, ProofDrawCallCategory.Text, text: text);

        private void Record(ProofDrawCallKind kind, ProofDrawCallCategory category, string? renderTargetId = null, string? assetId = null,
            string? text = null, Rectangle? rectangle = null, RectangleF? rectangleF = null, int? vertexCount = null, Color? clearColor = null,
            Rectangle? clipBounds = null)
        {
            string? actualRenderTargetId = renderTargetId ?? _currentRenderTarget?.Id;
            ProofDrawCall call = new(kind, category, actualRenderTargetId, assetId, text, rectangle, rectangleF, vertexCount, clearColor, clipBounds);
            Calls.Add(call);
            _runtime.Calls.Add(call);
            _currentRenderTarget?.Record(call);
        }

        private static string? DescribeImage(IUIImageResource image)
            => image switch
            {
                ProofImageResource proofImage => proofImage.Id,
                null => null,
                _ => image.GetType().FullName,
            };
    }

    private sealed class ProofTextEngine : ITextEngine
    {
        private readonly Dictionary<FontSpec, ResolvedFont> _cache = new();

        public ResolvedFont ResolveFont(FontSpec spec)
        {
            if (_cache.TryGetValue(spec, out ResolvedFont? cached) && cached != null)
            {
                return cached;
            }

            float lineHeight = Math.Max(spec.Size + 2, 8);
            float spaceWidth = Math.Max(spec.Size * 0.5f, 4f);
            ResolvedFont resolved = new(spec, spec.Size, 1.0f, 1.0f, lineHeight, spaceWidth, Vector2.Zero, false, new object());
            _cache[spec] = resolved;
            return resolved;
        }

        public Vector2 MeasureText(ResolvedFont font, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Vector2.Zero;
            }

            float glyphWidth = Math.Max(font.ActualSize * 0.6f, 6f);
            return new Vector2(text.Length * glyphWidth, GetLineHeight(font));
        }

        public GlyphMetrics MeasureGlyph(ResolvedFont font, char c)
        {
            float glyphWidth = Math.Max(font.ActualSize * 0.6f, 6f);
            return new GlyphMetrics(0f, glyphWidth, 0f, GetLineHeight(font));
        }

        public float GetLineHeight(ResolvedFont font) => font.LineHeight;

        public float GetSpaceWidth(ResolvedFont font) => font.SpaceWidth;

        public void DrawText(IUIDrawContext drawContext, ResolvedFont font, string text, Vector2 position, Color color, Vector2 origin,
            float scale, float rotation = 0f, float depth = 0f, UIDrawFlip flip = UIDrawFlip.None)
        {
            if (drawContext is not ProofDrawTransaction proofTransaction)
            {
                throw new InvalidOperationException("Proof text engine requires the proof draw transaction.");
            }

            proofTransaction.RecordText(text);
        }

        public void InvalidateCache() => _cache.Clear();
    }

    private sealed class ProofSurface : IUISurface
    {
        private readonly Rectangle _bounds;
        private readonly ProofRenderTarget _renderTarget;

        public ProofSurface(Rectangle bounds, ProofRenderTarget renderTarget)
        {
            _bounds = bounds;
            _renderTarget = renderTarget;
        }

        public Rectangle GetBounds() => _bounds;

        public IUIRenderTarget GetRenderTarget() => _renderTarget;
    }

    private sealed class ProofAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, ProofImageResource> _images = new(StringComparer.OrdinalIgnoreCase);

        public IUIImageResource LoadImage(string assetName)
        {
            if (!_images.TryGetValue(assetName, out ProofImageResource? image))
            {
                image = new ProofImageResource(assetName, 32, 32);
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

    private class ProofImageResource : IUIImageResource
    {
        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed { get; private set; }

        public ProofImageResource(string id, int width, int height)
        {
            Id = id;
            Width = width;
            Height = height;
        }
    }

    private sealed class ProofRenderTarget : ProofImageResource, IUIRenderTarget
    {
        public List<ProofDrawCall> RecordedCalls { get; } = new();

        public ProofRenderTarget(string id, int width, int height)
            : base(id, width, height)
        {
        }

        public void Record(ProofDrawCall call)
        {
            if (call.Category is ProofDrawCallCategory.Shape or ProofDrawCallCategory.Text or ProofDrawCallCategory.Image)
            {
                RecordedCalls.Add(call);
            }
        }
    }

    private sealed class DisposableAction : IDisposable
    {
        private readonly Action _disposeAction;
        private bool _isDisposed;

        public DisposableAction(Action disposeAction)
        {
            _disposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _disposeAction();
            }
        }
    }
}