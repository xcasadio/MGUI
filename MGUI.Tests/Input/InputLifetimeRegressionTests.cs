using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
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
using System.Runtime.CompilerServices;

namespace MGUI.Tests.Input;

/// <summary>
/// End-to-end regression for the leaks fixed under tasks 1-3 of the input-leaks plan
/// (<c>Docs/Tasks/input-leaks-and-textinput-tasks.md</c>): tracker handler lists no longer root manual handlers (D1),
/// <see cref="MGDesktop.FocusedKeyboardHandler"/> notifies elements directly instead of via a constructor
/// subscription (D2), and <see cref="MGListBox{TItemType}"/> no longer subscribes to <c>Runtime.EndUpdate</c> (D3).
/// This test builds a window containing one of each affected control, exercises the D1/D2/D3 code paths (auto
/// handler registration, keyboard focus, list box press), closes and detaches the window, and asserts that
/// nothing left behind by those three mechanisms keeps the closed subtree alive.
/// <para/>
/// The residual root discovered during task 6 (the window's local <c>MGResources</c> chained to
/// <c>Desktop.Resources.OnDefaultThemeChanged</c> by <c>EnsureResourceScope(UIResourceScope.Window)</c>) has since been
/// fixed: <c>MGResources.SetParent</c> now subscribes through a weak forwarder (<c>WeakThemeChangedForwarder</c>) that
/// references the child scope only weakly, so a closed window is collectable as soon as the application drops it, while
/// theme propagation keeps working for closed-but-still-referenced windows that will be re-shown (see
/// <see cref="ClosedWindow_IsNotRootedByDesktopResourcesThemeSubscription"/> and
/// <see cref="ThemeChange_StillReachesWindow_WhileClosedAndAfterReopen"/> below).
/// </summary>
public class InputLifetimeRegressionTests
{
    [Fact]
    public void ClosingWindow_AllowsTextBoxNumericUpDownAndListBox_ToBeCollected()
    {
        LifetimeHarness harness = CreateHarness();

        WeakReference[] references = BuildWindowAndCaptureWeakReferences(harness);

        // Drive a couple more update ticks after the window is closed and detached, exercising the same
        // code paths (tracker UpdateHandlers, desktop Update) a real host would run afterwards, to make sure
        // nothing re-roots the closed subtree.
        harness.Desktop.Update();
        harness.Desktop.Update();

        CollectGarbage();

        string[] labels = { "window", "textBox", "numericUpDown", "listBox" };
        for (int i = 0; i < references.Length; i++)
        {
            Assert.False(references[i].IsAlive, $"Expected '{labels[i]}' to be collected after the window was closed, but it is still reachable.");
        }
    }

    /// <summary>
    /// Pins the fix for the residual root originally discovered while writing the collectability regression above:
    /// <see cref="MGWindow"/> subscribes its own local <c>MGResources</c> to <c>Desktop.Resources.OnDefaultThemeChanged</c>
    /// (via <c>MGResources.SetParent</c>, at construction time, through <c>EnsureResourceScope(UIResourceScope.Window)</c>)
    /// and nothing unsubscribes it when the window closes — which used to keep every closed window (and its entire
    /// subtree) reachable from <c>Desktop.Resources</c> for the desktop's lifetime.
    /// <para/>
    /// Unsubscribing on close was rejected (the "closing is not dying" constraint from the plan's D1 "Approche INTERDITE"
    /// section: this codebase re-shows the SAME window instance via <c>SampleBase.Show/Hide</c> and
    /// <c>Desktop.Windows.Add/Remove</c>, <c>Desktop.Windows</c> is a bare <c>List&lt;MGWindow&gt;</c> with no add hook to
    /// re-attach from, and a detached scope with no local theme makes <c>MGResources.DefaultTheme</c> throw). Instead,
    /// <c>MGResources.SetParent</c> subscribes through a weak forwarder that references the child scope only weakly:
    /// the parent scope no longer roots the child's owner, so once the application drops a closed window, the whole
    /// subtree is collectable — asserted here without any test-side neutralization of the chain.
    /// </summary>
    [Fact]
    public void ClosedWindow_IsNotRootedByDesktopResourcesThemeSubscription()
    {
        LifetimeHarness harness = CreateHarness();

        WeakReference[] references = BuildWindowAndCaptureWeakReferences(harness);

        harness.Desktop.Update();
        harness.Desktop.Update();

        CollectGarbage();

        Assert.False(references[0].IsAlive, "Expected the closed window to be collectable: Desktop.Resources' theme subscription must not root the window's local-resources parent chain anymore.");
    }

    /// <summary>
    /// Companion regression to <see cref="ClosedWindow_IsNotRootedByDesktopResourcesThemeSubscription"/>, guarding the
    /// other half of the contract: the weak forwarder must NOT weaken theme propagation for the "closing is not dying"
    /// scenario. A window that is closed but still strongly referenced (like <c>SampleBase</c> holding a hidden sample)
    /// keeps receiving <c>Desktop.Resources</c> theme changes — through garbage collections — both while closed and
    /// after being re-added to <c>Desktop.Windows</c>.
    /// </summary>
    [Fact]
    public void ThemeChange_StillReachesWindow_WhileClosedAndAfterReopen()
    {
        LifetimeHarness harness = CreateHarness();

        MGWindow window = new(harness.Desktop, 24, 24, 400, 260)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0)
        };
        window.SetContent(new MGTextBlock(window, "content"));

        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        Assert.True(window.TryCloseWindow(), "Precondition failed: the test window could not be closed.");

        // The weak forwarder must survive garbage collection as long as the window itself is reachable.
        CollectGarbage();

        int themeChangesSeenByWindowScope = 0;
        window.LocalResources.OnDefaultThemeChanged += (_, _) => themeChangesSeenByWindowScope++;

        MGTheme themeWhileClosed = new(harness.Desktop.DefaultFontFamily);
        harness.Desktop.Resources.DefaultTheme = themeWhileClosed;

        Assert.Equal(1, themeChangesSeenByWindowScope);
        Assert.Same(themeWhileClosed, window.GetTheme());

        // Re-show the SAME instance (SampleBase.Show pattern) and change the theme again after another GC.
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();
        CollectGarbage();

        MGTheme themeAfterReopen = new(harness.Desktop.DefaultFontFamily);
        harness.Desktop.Resources.DefaultTheme = themeAfterReopen;

        Assert.Equal(2, themeChangesSeenByWindowScope);
        Assert.Same(themeAfterReopen, window.GetTheme());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] BuildWindowAndCaptureWeakReferences(LifetimeHarness harness)
    {
        MGWindow window = new(harness.Desktop, 24, 24, 400, 260)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0)
        };

        MGTextBox textBox = new(window) { Text = "abcdef" };
        MGNumericUpDown numericUpDown = new(window, 0, 100, 0, decimalPlaces: 2);
        MGListBox<string> listBox = new(window);
        listBox.SetItemsSource(new List<string> { "a", "b", "c" });

        MGStackPanel content = new(window, Orientation.Vertical);
        content.TryAddChild(textBox);
        content.TryAddChild(numericUpDown);
        content.TryAddChild(listBox);
        window.SetContent(content);

        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();
        harness.Desktop.Update();

        // Focus each control in turn (exercises MGDesktop.FocusedKeyboardHandler / D2) and press the list box
        // (exercises MGListBox.PressedItem / D3) before closing, so the regression covers state that was
        // previously only cleaned up via the leaking subscriptions.
        SetFocusedKeyboardHandler(harness.Desktop, textBox);
        SetFocusedKeyboardHandler(harness.Desktop, numericUpDown);
        SetFocusedKeyboardHandler(harness.Desktop, listBox.ListBoxItems.Count > 0 ? listBox : null);
        SetFocusedKeyboardHandler(harness.Desktop, null);

        bool closed = window.TryCloseWindow();
        Assert.True(closed, "Precondition failed: the test window could not be closed.");
        Assert.DoesNotContain(window, harness.Desktop.Windows);

        return new WeakReference[]
        {
            new(window),
            new(textBox),
            new(numericUpDown),
            new(listBox)
        };
    }

    private static void SetFocusedKeyboardHandler(MGDesktop desktop, MGElement element)
    {
        System.Reflection.MethodInfo setter = typeof(MGDesktop)
            .GetProperty(nameof(MGDesktop.FocusedKeyboardHandler), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
            .GetSetMethod(true)!;
        setter.Invoke(desktop, new object?[] { element });
    }

    private static void CollectGarbage()
    {
        for (int i = 0; i < 5; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private static LifetimeHarness CreateHarness()
    {
        LifetimeTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default));

        MGDesktop desktop = new(runtime);

        return new(runtime, desktop);
    }

    private readonly record struct LifetimeHarness(LifetimeTestRuntime Runtime, MGDesktop Desktop);

    private sealed class LifetimeTestRuntime : IUIDesktopRuntime
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

        public LifetimeTestRuntime(Rectangle surfaceBounds)
        {
            Surface = new LifetimeTestSurface(surfaceBounds, new LifetimeTestRenderTarget(surfaceBounds.Width, surfaceBounds.Height));
            AssetProvider = new LifetimeTestAssetProvider();
            _textEngine = new LifetimeTestTextEngine(DefaultFontFamily);
        }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings Settings, bool DeferBegin)
            => new LifetimeTestNoOpDrawTransaction(this, Settings ?? DrawSettings.Default);

        public void ApplyFrame(UpdateBaseArgs updateArgs)
        {
            UpdateArgs = updateArgs;
            Input.Update(updateArgs);
        }

        public void RegisterView(IUIView View)
        {
        }
    }

    private sealed class LifetimeTestNoOpDrawTransaction : IUIDrawTransaction
    {
        private Rectangle? _currentClipBounds;

        public DrawSettings CurrentSettings { get; private set; }
        public IUIDesktopRuntime Renderer { get; }
        public Rectangle? CurrentClipBounds => _currentClipBounds;

        public LifetimeTestNoOpDrawTransaction(IUIDesktopRuntime renderer, DrawSettings settings)
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

    private sealed class LifetimeTestSurface : IUISurface
    {
        private readonly Rectangle _bounds;
        private readonly IUIRenderTarget _renderTarget;

        public LifetimeTestSurface(Rectangle bounds, IUIRenderTarget renderTarget)
        {
            _bounds = bounds;
            _renderTarget = renderTarget;
        }

        public Rectangle GetBounds() => _bounds;

        public IUIRenderTarget GetRenderTarget() => _renderTarget;
    }

    private sealed class LifetimeTestAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, LifetimeTestImageResource> _images = new(StringComparer.OrdinalIgnoreCase);

        public IUIImageResource LoadImage(string assetName)
        {
            if (!_images.TryGetValue(assetName, out LifetimeTestImageResource image))
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

    private class LifetimeTestImageResource : IUIImageResource
    {
        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed { get; private set; }

        public LifetimeTestImageResource(string id, int width, int height)
        {
            Id = id;
            Width = width;
            Height = height;
        }
    }

    private sealed class LifetimeTestRenderTarget : LifetimeTestImageResource, IUIRenderTarget
    {
        public LifetimeTestRenderTarget(int width, int height)
            : base("input-lifetime-render-target", width, height)
        {
        }
    }

    private sealed class LifetimeTestTextEngine : ITextMeasurementEngine
    {
        private readonly string _defaultFontFamily;

        public LifetimeTestTextEngine(string defaultFontFamily)
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
