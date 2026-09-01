using MGUI.Core.UI;
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
using System.Reflection;

namespace MGUI.Tests.Controls;

/// <summary>
/// Covers the D3 migration: <see cref="MGListBox{TItemType}"/> used to subscribe to
/// <c>GetDesktop().Runtime.EndUpdate</c> in its constructor to reset <see cref="MGListBox{TItemType}.PressedItem"/>
/// at the end of an update tick, rooting the list box for the lifetime of the runtime. It now resets that state
/// from its own <see cref="MGElement.OnBeginUpdate"/> event (self-subscription, no external root) at the start of
/// its next update tick instead, which is observably equivalent since the frame sequence is Update -> Draw -> EndUpdate:
/// the pressed visual still survives the Draw call of the tick it was set on, and is cleared before the next Draw either way.
/// </summary>
public class MGListBoxPressedItemTests
{
    [Fact]
    public void PressedItem_DoesNotPersist_AcrossUpdateTick_AfterReleaseIsPending()
    {
        ListBoxHarness harness = CreateHarness();
        MGListBox<string> listBox = new(harness.Window);
        listBox.SetItemsSource(new List<string> { "a", "b", "c" });
        harness.Window.SetContent(listBox);

        // Simulate the state left behind by a mouse press followed by a release (LMBReleasedInside /
        // ReleasedOutside both just flag IsPressedItemInvalidationPending = true and let the next update
        // tick perform the actual reset).
        MGListBoxItem<string> firstItem = listBox.ListBoxItems[0];
        SetPrivateProperty(listBox, "PressedItem", firstItem);
        firstItem.ContentPresenter.SpoofIsPressedWhileDrawingBackground = true;
        SetPrivateProperty(listBox, "IsPressedItemInvalidationPending", true);

        Assert.NotNull(listBox.PressedItem);

        harness.Desktop.Update();

        Assert.Null(listBox.PressedItem);
        Assert.False(firstItem.ContentPresenter.SpoofIsPressedWhileDrawingBackground);
    }

    [Fact]
    public void PressedItem_NotReset_UntilInvalidationIsPending()
    {
        ListBoxHarness harness = CreateHarness();
        MGListBox<string> listBox = new(harness.Window);
        listBox.SetItemsSource(new List<string> { "a", "b", "c" });
        harness.Window.SetContent(listBox);

        MGListBoxItem<string> firstItem = listBox.ListBoxItems[0];
        SetPrivateProperty(listBox, "PressedItem", firstItem);
        // IsPressedItemInvalidationPending intentionally left false, mimicking the moment right after a press
        // (before any release), when the pressed item must still be observable by lower-priority handlers.

        harness.Desktop.Update();

        Assert.Same(firstItem, listBox.PressedItem);
    }

    private static void SetPrivateProperty(object instance, string propertyName, object value)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        property.SetValue(instance, value);
    }

    private static ListBoxHarness CreateHarness()
    {
        ListBoxTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default));

        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 24, 24, 400, 260)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0)
        };

        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();

        return new(runtime, desktop, window);
    }

    private readonly record struct ListBoxHarness(ListBoxTestRuntime Runtime, MGDesktop Desktop, MGWindow Window);

    private sealed class ListBoxTestRuntime : IUIDesktopRuntime
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

        public ListBoxTestRuntime(Rectangle surfaceBounds)
        {
            Surface = new ListBoxTestSurface(surfaceBounds, new ListBoxTestRenderTarget(surfaceBounds.Width, surfaceBounds.Height));
            AssetProvider = new ListBoxTestAssetProvider();
            _textEngine = new ListBoxTestTextEngine(DefaultFontFamily);
        }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings Settings, bool DeferBegin)
            => new ListBoxTestNoOpDrawTransaction(this, Settings ?? DrawSettings.Default);

        public void ApplyFrame(UpdateBaseArgs updateArgs)
        {
            UpdateArgs = updateArgs;
            Input.Update(updateArgs);
        }

        public void RegisterView(IUIView View)
        {
        }
    }

    private sealed class ListBoxTestNoOpDrawTransaction : IUIDrawTransaction
    {
        private Rectangle? _currentClipBounds;

        public DrawSettings CurrentSettings { get; private set; }
        public IUIDesktopRuntime Renderer { get; }
        public Rectangle? CurrentClipBounds => _currentClipBounds;

        public ListBoxTestNoOpDrawTransaction(IUIDesktopRuntime renderer, DrawSettings settings)
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

        public void SetDrawSettings(DrawSettings Settings)
        {
            CurrentSettings = Settings ?? DrawSettings.Default;
        }

        public IDisposable SetDrawSettingsTemporary(DrawSettings Settings)
        {
            DrawSettings previous = CurrentSettings;
            SetDrawSettings(Settings);
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

    private sealed class ListBoxTestSurface : IUISurface
    {
        private readonly Rectangle _bounds;
        private readonly IUIRenderTarget _renderTarget;

        public ListBoxTestSurface(Rectangle bounds, IUIRenderTarget renderTarget)
        {
            _bounds = bounds;
            _renderTarget = renderTarget;
        }

        public Rectangle GetBounds() => _bounds;

        public IUIRenderTarget GetRenderTarget() => _renderTarget;
    }

    private sealed class ListBoxTestAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, ListBoxTestImageResource> _images = new(StringComparer.OrdinalIgnoreCase);

        public IUIImageResource LoadImage(string assetName)
        {
            if (!_images.TryGetValue(assetName, out ListBoxTestImageResource image))
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

    private class ListBoxTestImageResource : IUIImageResource
    {
        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed { get; private set; }

        public ListBoxTestImageResource(string id, int width, int height)
        {
            Id = id;
            Width = width;
            Height = height;
        }
    }

    private sealed class ListBoxTestRenderTarget : ListBoxTestImageResource, IUIRenderTarget
    {
        public ListBoxTestRenderTarget(int width, int height)
            : base("list-box-test-render-target", width, height)
        {
        }
    }

    private sealed class ListBoxTestTextEngine : ITextMeasurementEngine
    {
        private readonly string _defaultFontFamily;

        public ListBoxTestTextEngine(string defaultFontFamily)
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
