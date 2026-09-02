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

namespace MGUI.Tests.Focus;

/// <summary>
/// Covers the D2 migration: <see cref="MGElement.OnKeyboardFocusChanged(bool)"/> is invoked directly by
/// <see cref="MGDesktop.FocusedKeyboardHandler"/>'s setter instead of via a constructor subscription to
/// <see cref="MGDesktop.FocusedKeyboardHandlerChanged"/>, for <see cref="MGTextBox"/> and <see cref="MGNumericUpDown"/>.
/// </summary>
public class KeyboardFocusNotificationTests
{
    [Fact]
    public void TextBox_GainingFocus_WithNonEmptySelection_UpdatesFormattedTextWithFocusedColors()
    {
        FocusNotificationHarness harness = CreateHarness();
        MGTextBox textBox = new(harness.Window) { Text = "abcdef" };
        textBox.CurrentSelection = new MGTextBox.TextSelection(0, textBox.Text.Length);

        SetFocusedKeyboardHandler(harness.Desktop, null);
        string unfocusedFormattedText = textBox.FormattedText;

        SetFocusedKeyboardHandler(harness.Desktop, textBox);
        string focusedFormattedText = textBox.FormattedText;

        Assert.NotEqual(unfocusedFormattedText, focusedFormattedText);
    }

    [Fact]
    public void TextBox_LosingFocus_WithNonEmptySelection_UpdatesFormattedTextWithUnfocusedColors()
    {
        FocusNotificationHarness harness = CreateHarness();
        MGTextBox textBox = new(harness.Window) { Text = "abcdef" };
        textBox.CurrentSelection = new MGTextBox.TextSelection(0, textBox.Text.Length);

        SetFocusedKeyboardHandler(harness.Desktop, textBox);
        string focusedFormattedText = textBox.FormattedText;

        SetFocusedKeyboardHandler(harness.Desktop, null);
        string unfocusedFormattedText = textBox.FormattedText;

        Assert.NotEqual(focusedFormattedText, unfocusedFormattedText);
    }

    // NumericUpDown re-parses Text live on every keystroke (HandleTextChanged), so Value already tracks
    // uncommitted text as it's typed. CommitPendingText's distinct, observable effect is reformatting Text
    // to its canonical string (SyncTextFromValue), e.g. "42" -> "42.00" for a control with DecimalPlaces=2.
    // That reformat is what must happen on focus LOSS and must NOT happen on focus GAIN.

    [Fact]
    public void NumericUpDown_LosingFocus_WithPendingText_ResyncsTextToCanonicalFormat()
    {
        FocusNotificationHarness harness = CreateHarness();
        MGNumericUpDown numericUpDown = new(harness.Window, 0, 100, 0, decimalPlaces: 2);

        SetFocusedKeyboardHandler(harness.Desktop, numericUpDown);
        numericUpDown.SetText("42");
        Assert.Equal(42, numericUpDown.Value);
        Assert.Equal("42", numericUpDown.Text);

        SetFocusedKeyboardHandler(harness.Desktop, null);

        Assert.Equal(42, numericUpDown.Value);
        Assert.Equal("42.00", numericUpDown.Text);
    }

    [Fact]
    public void NumericUpDown_GainingFocus_DoesNotResyncPendingText()
    {
        FocusNotificationHarness harness = CreateHarness();
        MGNumericUpDown other = new(harness.Window, 0, 100, 0);
        MGNumericUpDown numericUpDown = new(harness.Window, 0, 100, 0, decimalPlaces: 2);

        SetFocusedKeyboardHandler(harness.Desktop, other);
        numericUpDown.SetText("42");
        Assert.Equal(42, numericUpDown.Value);
        Assert.Equal("42", numericUpDown.Text);

        SetFocusedKeyboardHandler(harness.Desktop, numericUpDown);

        Assert.Equal(42, numericUpDown.Value);
        Assert.Equal("42", numericUpDown.Text);
    }

    [Fact]
    public void FocusProbeTextBox_ReceivesGainedThenLost_InCorrectOrder_IncludingTransitionToNull()
    {
        FocusNotificationHarness harness = CreateHarness();
        FocusProbeTextBox first = new(harness.Window);
        FocusProbeTextBox second = new(harness.Window);

        SetFocusedKeyboardHandler(harness.Desktop, first);
        Assert.Equal(1, first.GainedCount);
        Assert.Equal(0, first.LostCount);

        // Transition between two arbitrary elements notifies both.
        SetFocusedKeyboardHandler(harness.Desktop, second);
        Assert.Equal(1, first.LostCount);
        Assert.Equal(1, second.GainedCount);

        // Transition to null (clear of focus) still notifies the previously-focused element.
        SetFocusedKeyboardHandler(harness.Desktop, null);
        Assert.Equal(1, second.LostCount);
        Assert.Equal(1, second.GainedCount);
    }

    [Fact]
    public void ConstructingManyTextBoxes_DoesNotGrowFocusedKeyboardHandlerChangedInvocationList()
    {
        FocusNotificationHarness harness = CreateHarness();

        int InvocationCount()
        {
            FieldInfo backingField = typeof(MGDesktop).GetField(nameof(MGDesktop.FocusedKeyboardHandlerChanged), BindingFlags.NonPublic | BindingFlags.Instance)!;
            Delegate backingDelegate = (Delegate)backingField.GetValue(harness.Desktop)!;
            return backingDelegate?.GetInvocationList().Length ?? 0;
        }

        int before = InvocationCount();

        for (int i = 0; i < 25; i++)
        {
            _ = new MGTextBox(harness.Window);
            _ = new MGNumericUpDown(harness.Window, 0, 100, 0);
        }

        int after = InvocationCount();

        Assert.Equal(before, after);
    }

    private sealed class FocusProbeTextBox : MGTextBox
    {
        public int GainedCount { get; private set; }
        public int LostCount { get; private set; }

        public FocusProbeTextBox(MGWindow window) : base(window) { }

        protected internal override void OnKeyboardFocusChanged(bool gained)
        {
            base.OnKeyboardFocusChanged(gained);
            if (gained)
            {
                GainedCount++;
            }
            else
            {
                LostCount++;
            }
        }
    }

    private static FocusNotificationHarness CreateHarness()
    {
        FocusNotificationTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
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

    private static void SetFocusedKeyboardHandler(MGDesktop desktop, MGElement? element)
    {
        MethodInfo setter = typeof(MGDesktop).GetProperty(nameof(MGDesktop.FocusedKeyboardHandler), BindingFlags.Public | BindingFlags.Instance)!.GetSetMethod(true)!;
        setter.Invoke(desktop, new object?[] { element });
    }

    private readonly record struct FocusNotificationHarness(FocusNotificationTestRuntime Runtime, MGDesktop Desktop, MGWindow Window);

    private sealed class FocusNotificationTestRuntime : IUIDesktopRuntime
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

        public FocusNotificationTestRuntime(Rectangle surfaceBounds)
        {
            Surface = new FocusNotificationTestSurface(surfaceBounds, new FocusNotificationTestRenderTarget(surfaceBounds.Width, surfaceBounds.Height));
            AssetProvider = new FocusNotificationTestAssetProvider();
            _textEngine = new FocusNotificationTestTextEngine(DefaultFontFamily);
        }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings Settings, bool DeferBegin)
            => new FocusNotificationNoOpDrawTransaction(this, Settings ?? DrawSettings.Default);

        public void ApplyFrame(UpdateBaseArgs updateArgs)
        {
            UpdateArgs = updateArgs;
            Input.Update(updateArgs);
        }

        public void RegisterView(IUIView View)
        {
        }
    }

    private sealed class FocusNotificationNoOpDrawTransaction : IUIDrawTransaction
    {
        private Rectangle? _currentClipBounds;

        public DrawSettings CurrentSettings { get; private set; }
        public IUIDesktopRuntime Renderer { get; }
        public Rectangle? CurrentClipBounds => _currentClipBounds;

        public FocusNotificationNoOpDrawTransaction(IUIDesktopRuntime renderer, DrawSettings settings)
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

        public void DrawTexturedTriangleList(Vector2 Origin, IUIImageResource Texture, IReadOnlyList<Vector2> Vertices, IReadOnlyList<Vector2> TextureCoordinates,
            IReadOnlyList<int> Indices, Color ColorMask) { }

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

    private sealed class FocusNotificationTestSurface : IUISurface
    {
        private readonly Rectangle _bounds;
        private readonly IUIRenderTarget _renderTarget;

        public FocusNotificationTestSurface(Rectangle bounds, IUIRenderTarget renderTarget)
        {
            _bounds = bounds;
            _renderTarget = renderTarget;
        }

        public Rectangle GetBounds() => _bounds;

        public IUIRenderTarget GetRenderTarget() => _renderTarget;
    }

    private sealed class FocusNotificationTestAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, FocusNotificationTestImageResource> _images = new(StringComparer.OrdinalIgnoreCase);

        public IUIImageResource LoadImage(string assetName)
        {
            if (!_images.TryGetValue(assetName, out FocusNotificationTestImageResource image))
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

    private class FocusNotificationTestImageResource : IUIImageResource
    {
        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed { get; private set; }

        public FocusNotificationTestImageResource(string id, int width, int height)
        {
            Id = id;
            Width = width;
            Height = height;
        }
    }

    private sealed class FocusNotificationTestRenderTarget : FocusNotificationTestImageResource, IUIRenderTarget
    {
        public FocusNotificationTestRenderTarget(int width, int height)
            : base("focus-notification-render-target", width, height)
        {
        }
    }

    private sealed class FocusNotificationTestTextEngine : ITextMeasurementEngine
    {
        private readonly string _defaultFontFamily;

        public FocusNotificationTestTextEngine(string defaultFontFamily)
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
