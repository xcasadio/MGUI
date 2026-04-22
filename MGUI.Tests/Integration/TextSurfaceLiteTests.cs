using MGUI.Core.UI;
using MGUI.Core.UI.Text;
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
using System.Linq;

namespace MGUI.Tests.Integration;

public class TextSurfaceLiteTests
{
    [Fact]
    public void SetTextRuns_OverridesParsedTextUntilCleared()
    {
        TextSurfaceHarness harness = CreateHarness(window => new MGTextBlock(window, "[color=Red]Alpha[/color]"));
        MGTextBlock textBlock = (MGTextBlock)harness.Content;

        Assert.False(textBlock.HasExplicitRuns);
        Assert.Equal("Alpha", FlattenText(textBlock));

        textBlock.SetTextRuns(new MGTextRun[]
        {
            new MGTextRunText("Beta [b]literal[/b]", new MGTextRunConfig(false), null, null)
        });

        Assert.True(textBlock.HasExplicitRuns);
        Assert.Equal("Beta [b]literal[/b]", FlattenText(textBlock));

        textBlock.ClearTextRuns();

        Assert.False(textBlock.HasExplicitRuns);
        Assert.Equal("Alpha", FlattenText(textBlock));
    }

    [Fact]
    public void TextLogView_AppendEntry_TrimsOldestEntriesAndBuildsFormattedRuns()
    {
        TextSurfaceHarness harness = CreateHarness(window => new MGTextLogView(window, 2)
        {
            ShowTimestamps = false,
            AllowsInlineFormatting = true
        });
        MGTextLogView logView = (MGTextLogView)harness.Content;

        logView.AppendEntry("First");
        logView.AppendEntry("[color=Yellow]Second[/color]", "WARN");
        logView.AppendEntry("Third", "INFO");

        Assert.Equal(2, logView.Entries.Count);
        Assert.Equal("[color=Yellow]Second[/color]", logView.Entries[0].Message);

        MGTextBlock renderedEntry = Assert.IsType<MGTextBlock>(logView.ItemTemplate(logView.Entries[0]));
        Assert.True(renderedEntry.AllowsInlineFormatting);
        Assert.Equal("(WARN) Second", FlattenText(renderedEntry));
    }

    [Fact]
    public void ChatBox_AllowsMessageInlineFormatting_RebuildsMessageTemplate()
    {
        TextSurfaceHarness harness = CreateHarness(window => new MGChatBox(window));
        MGChatBox chatBox = (MGChatBox)harness.Content;

        chatBox.SendMessage("[color=Orange]Alert[/color]");
        chatBox.AllowsMessageInlineFormatting = true;

        MGChatBoxMessage renderedMessage = Assert.IsType<MGChatBoxMessage>(chatBox.MessagesContainer.ItemTemplate(chatBox.Messages[0]));
        Assert.True(renderedMessage.MessageTextBlock.AllowsInlineFormatting);
        Assert.Equal("Alert", FlattenText(renderedMessage.MessageTextBlock));
    }

    private static string FlattenText(MGTextBlock textBlock)
        => string.Concat(textBlock.Runs.OfType<MGTextRunText>().Select(x => x.Text));

    private static TextSurfaceHarness CreateHarness(Func<MGWindow, MGElement> contentFactory)
    {
        TextSurfaceTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default));

        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 24, 24, 480, 260)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0)
        };

        MGElement content = contentFactory(window);
        window.SetContent(content);
        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();

        return new(runtime, desktop, window, content);
    }

    private readonly record struct TextSurfaceHarness(TextSurfaceTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, MGElement Content);

    private sealed class TextSurfaceTestRuntime : IUIDesktopRuntime
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

        public TextSurfaceTestRuntime(Rectangle surfaceBounds)
        {
            Surface = new TextSurfaceTestSurface(surfaceBounds, new TextSurfaceTestRenderTarget(surfaceBounds.Width, surfaceBounds.Height));
            AssetProvider = new TextSurfaceTestAssetProvider();
            _textEngine = new TextSurfaceTestTextEngine(DefaultFontFamily);
        }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings Settings, bool DeferBegin)
            => new TextSurfaceNoOpDrawTransaction(this, Settings ?? DrawSettings.Default);

        public void ApplyFrame(UpdateBaseArgs updateArgs)
        {
            UpdateArgs = updateArgs;
            Input.Update(updateArgs);
        }

        public void RegisterView(IUIView View)
        {
        }
    }

    private sealed class TextSurfaceNoOpDrawTransaction : IUIDrawTransaction
    {
        private Rectangle? _currentClipBounds;

        public DrawSettings CurrentSettings { get; private set; }
        public IUIDesktopRuntime Renderer { get; }
        public Rectangle? CurrentClipBounds => _currentClipBounds;

        public TextSurfaceNoOpDrawTransaction(IUIDesktopRuntime renderer, DrawSettings settings)
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

    private sealed class TextSurfaceTestSurface : IUISurface
    {
        private readonly Rectangle _bounds;
        private readonly IUIRenderTarget _renderTarget;

        public TextSurfaceTestSurface(Rectangle bounds, IUIRenderTarget renderTarget)
        {
            _bounds = bounds;
            _renderTarget = renderTarget;
        }

        public Rectangle GetBounds() => _bounds;

        public IUIRenderTarget GetRenderTarget() => _renderTarget;
    }

    private sealed class TextSurfaceTestAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, TextSurfaceTestImageResource> _images = new(StringComparer.OrdinalIgnoreCase);

        public IUIImageResource LoadImage(string assetName)
        {
            if (!_images.TryGetValue(assetName, out TextSurfaceTestImageResource? image))
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

    private class TextSurfaceTestImageResource : IUIImageResource
    {
        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed { get; private set; }

        public TextSurfaceTestImageResource(string id, int width, int height)
        {
            Id = id;
            Width = width;
            Height = height;
        }
    }

    private sealed class TextSurfaceTestRenderTarget : TextSurfaceTestImageResource, IUIRenderTarget
    {
        public TextSurfaceTestRenderTarget(int width, int height)
            : base("text-surface-render-target", width, height)
        {
        }
    }

    private sealed class TextSurfaceTestTextEngine : ITextMeasurementEngine
    {
        private readonly string _defaultFontFamily;

        public TextSurfaceTestTextEngine(string defaultFontFamily)
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