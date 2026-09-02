using Microsoft.Xna.Framework;
using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Semantic;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace MGUI.Tests.Tooling;

public class StableDiagnosticIdTests
{
    [Fact]
    public void GetStableDiagnosticId_IsStableAcrossEquivalentTrees()
    {
        ToolingTree first = CreateTree();
        ToolingTree second = CreateTree();

        Assert.NotEqual(first.Window.UniqueId, second.Window.UniqueId);
        Assert.Equal("desktop/window:main-window", UIToolingService.GetStableDiagnosticId(first.Window));
        Assert.Equal(UIToolingService.GetStableDiagnosticId(first.Window), UIToolingService.GetStableDiagnosticId(second.Window));
        Assert.Equal(UIToolingService.GetStableDiagnosticId(first.Scope), UIToolingService.GetStableDiagnosticId(second.Scope));
        Assert.Equal(UIToolingService.GetStableDiagnosticId(first.Popup), UIToolingService.GetStableDiagnosticId(second.Popup));
        Assert.Equal(UIToolingService.GetStableDiagnosticId(first.PopupContent), UIToolingService.GetStableDiagnosticId(second.PopupContent));
        Assert.Equal(UIToolingService.GetStableDiagnosticId(first.Window.TemplateParts[MGWindow.TitleBarPartName]), UIToolingService.GetStableDiagnosticId(second.Window.TemplateParts[MGWindow.TitleBarPartName]));

        Assert.Equal("desktop/window:main-window/stackpanel:inspector-scope", UIToolingService.GetStableDiagnosticId(first.Scope));
        Assert.Equal("desktop/window:main-window/window:inspector-popup", UIToolingService.GetStableDiagnosticId(first.Popup));
        Assert.Equal("desktop/window:main-window/window:inspector-popup/border:popup-content", UIToolingService.GetStableDiagnosticId(first.PopupContent));
        Assert.Equal("desktop/window:main-window/part:part-titlebar", UIToolingService.GetStableDiagnosticId(first.Window.TemplateParts[MGWindow.TitleBarPartName]));
    }

    [Fact]
    public void GetStableDiagnosticId_UsesDedicatedOverlayRootSegment()
    {
        ToolingTree tree = CreateTree();

        Assert.Equal("desktop/overlay-window/overlayhost:desktopoverlay", UIToolingService.GetStableDiagnosticId(tree.Desktop.OverlayHost));
    }

    [Fact]
    public void CaptureVisualTree_PopulatesStableAndRuntimeIds()
    {
        ToolingTree tree = CreateTree();
        UIVisualTreeSnapshot snapshot = UIToolingService.CaptureVisualTree(tree.Window);
        UIVisualTreeSnapshot popupSnapshot = UIToolingService.CaptureVisualTree(tree.Popup);

        Assert.Equal(UIToolingService.GetStableDiagnosticId(tree.Window), snapshot.DiagnosticId);
        Assert.Equal(UIToolingService.GetStableDiagnosticId(tree.Window), snapshot.WindowDiagnosticId);
        Assert.Equal(tree.Window.UniqueId, snapshot.RuntimeUniqueId);

        string popupContentId = UIToolingService.GetStableDiagnosticId(tree.PopupContent);
        UIVisualTreeSnapshot popupContentSnapshot = FindSnapshot(popupSnapshot, popupContentId);

        Assert.Equal(UIToolingService.GetStableDiagnosticId(tree.Popup), popupSnapshot.DiagnosticId);
        Assert.Equal(UIToolingService.GetStableDiagnosticId(tree.Popup), popupSnapshot.WindowDiagnosticId);
        Assert.Equal(tree.Popup.UniqueId, popupSnapshot.RuntimeUniqueId);
        Assert.Equal(popupContentId, popupContentSnapshot.DiagnosticId);
        Assert.Equal(UIToolingService.GetStableDiagnosticId(tree.Popup), popupContentSnapshot.WindowDiagnosticId);
        Assert.Equal(tree.PopupContent.UniqueId, popupContentSnapshot.RuntimeUniqueId);
    }

    [Fact]
    public void CaptureDesktopSnapshot_IncludesNestedWindowsAndActiveOverlayState()
    {
        ToolingTree tree = CreateTree();

        MGTextBox focusProbe = new(tree.Window)
        {
            Name = "Focus Probe"
        };
        focusProbe.IsFocusable = true;
        Assert.True(tree.Scope.TryAddChild(focusProbe));

        SetFocusedKeyboardHandler(tree.Desktop, focusProbe);

        UIDesktopDiagnosticSnapshot focusSnapshot = UIToolingService.CaptureDesktopSnapshot(tree.Desktop);
        Assert.Equal(UIToolingService.GetStableDiagnosticId(focusProbe), focusSnapshot.FocusedElementDiagnosticId);

        UIVisualTreeSnapshot focusProbeSnapshot = FindSnapshot(focusSnapshot.Windows[0].VisualTree, UIToolingService.GetStableDiagnosticId(focusProbe));
        Assert.True(focusProbeSnapshot.HasKeyboardFocus);

        MGTextBox overlayInput = new(tree.Desktop.OverlayHost.SelfOrParentWindow)
        {
            Name = "Overlay Input"
        };
        overlayInput.IsFocusable = true;

        MGOverlay overlay = tree.Desktop.OverlayHost.AddOverlay(overlayInput);
        overlay.Name = "Blocking Overlay";
        overlay.IsOpen = true;

        tree.Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(48), TimeSpan.FromMilliseconds(16), default, default));
        tree.Desktop.Update();

        UIDesktopDiagnosticSnapshot snapshot = UIToolingService.CaptureDesktopSnapshot(tree.Desktop);

        Assert.Equal("desktop", snapshot.DesktopDiagnosticId);
        Assert.Equal(UIToolingService.GetStableDiagnosticId(overlay), snapshot.ActiveOverlayDiagnosticId);
        Assert.Contains(UIToolingService.GetStableDiagnosticId(overlay), snapshot.OpenOverlayDiagnosticIds);
        Assert.Single(snapshot.Windows);
        Assert.Single(snapshot.Windows[0].NestedWindows);
        Assert.Equal(UIToolingService.GetStableDiagnosticId(tree.Popup), snapshot.Windows[0].NestedWindows[0].DiagnosticId);
    }

    [Fact]
    public void RenderDesktopSnapshot_ProducesReadableArtifact()
    {
        ToolingTree tree = CreateTree();

        string artifact = UIToolingService.RenderDesktopSnapshot(UIToolingService.CaptureDesktopSnapshot(tree.Desktop));

        Assert.Contains("desktop: desktop", artifact);
        Assert.Contains("windows:", artifact);
        Assert.Contains(UIToolingService.GetStableDiagnosticId(tree.Window), artifact);
    }

    [Fact]
    public void ReplayFrames_ReplaysRawInputAndSemanticActions()
    {
        ToolingTree tree = CreateReplayTree();
        string firstId = UIToolingService.GetStableDiagnosticId(tree.ReplayFirstInput!);

        SetFocusedKeyboardHandler(tree.Desktop, tree.ReplayFirstInput!);

        UIInputReplayFrame[] frames = new[]
        {
            new UIInputReplayFrame(
                "initial",
                new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16),
                    new MouseState(12, 18, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
                    new KeyboardState(Keys.Tab)),
                Array.Empty<InputActionEvent>()),
            new UIInputReplayFrame(
                "navigate-next",
                new UpdateBaseArgs(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16),
                    new MouseState(18, 24, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
                    new KeyboardState(Keys.Tab)),
                new[]
                {
                    new InputActionEvent(
                        InputAction.NavigateNext,
                        new InputActionContext(InputActionSource.Keyboard, InputActionPhase.Pressed, TimeSpan.FromMilliseconds(32), Key: Keys.Tab))
                },
                    firstId)
        };

        UIInputReplayResult result = UIToolingService.ReplayFrames(tree.Desktop, frames, tree.Runtime.ApplyFrame);

        Assert.Equal(2, result.Steps.Count);
        Assert.Equal(new Point(12, 18), result.Steps[0].Snapshot.Input.MousePosition);
        Assert.Contains(nameof(Keys.Tab), result.Steps[0].Snapshot.Input.PressedKeys);
                Assert.Null(result.Steps[0].Snapshot.FocusedElementDiagnosticId);
                Assert.Equal(firstId, result.Steps[1].Snapshot.FocusedElementDiagnosticId);
                Assert.Contains(firstId, result.Steps[1].Artifact);
    }

    private static UIVisualTreeSnapshot FindSnapshot(UIVisualTreeSnapshot root, string diagnosticId)
    {
        if (root.DiagnosticId == diagnosticId)
        {
            return root;
        }

        for (int i = 0; i < root.Children.Count; i++)
        {
            UIVisualTreeSnapshot? match = FindSnapshotOrDefault(root.Children[i], diagnosticId);
            if (match != null)
            {
                return match;
            }
        }

        throw new InvalidOperationException($"Unable to find snapshot '{diagnosticId}'.");
    }

    private static UIVisualTreeSnapshot? FindSnapshotOrDefault(UIVisualTreeSnapshot root, string diagnosticId)
    {
        if (root.DiagnosticId == diagnosticId)
        {
            return root;
        }

        for (int i = 0; i < root.Children.Count; i++)
        {
            UIVisualTreeSnapshot? match = FindSnapshotOrDefault(root.Children[i], diagnosticId);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static void SetFocusedKeyboardHandler(MGDesktop desktop, MGElement element)
    {
        PropertyInfo focusedKeyboardHandler = typeof(MGDesktop).GetProperty(nameof(MGDesktop.FocusedKeyboardHandler), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        focusedKeyboardHandler.SetValue(desktop, element);
    }

    private static ToolingTree CreateTree()
    {
        ToolingTestRuntime runtime = new(new Rectangle(0, 0, 1280, 720));
        MGDesktop desktop = new(runtime);

        MGWindow window = new(desktop, 20, 30, 420, 260) { Name = "Main Window" };
        desktop.Windows.Add(window);

        MGStackPanel scope = new(window, Orientation.Vertical) { Name = "Inspector Scope" };
        MGBorder previewPane = new(window) { Name = "Preview Pane" };
        Assert.True(scope.TryAddChild(previewPane));
        window.SetContent(scope);

        MGWindow popup = new(window, 48, 40, 180, 120) { Name = "Inspector Popup" };
        MGBorder popupContent = new(popup) { Name = "Popup Content" };
        popup.SetContent(popupContent);
        window.AddNestedWindow(popup);

        Assert.True(window.TemplateParts.ContainsKey(MGWindow.TitleBarPartName));

        return new(runtime, desktop, window, scope, popup, popupContent, null, null);
    }

    private static ToolingTree CreateReplayTree()
    {
        ToolingTree tree = CreateTree();
        MGTextBox firstInput = new(tree.Window) { Name = "Replay First" };
        MGTextBox secondInput = new(tree.Window) { Name = "Replay Second" };
        firstInput.IsFocusable = true;
        secondInput.IsFocusable = true;
        Assert.True(tree.Scope.TryAddChild(firstInput));
        Assert.True(tree.Scope.TryAddChild(secondInput));
        tree.Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), default, default));
        tree.Desktop.Update();
        return tree with { ReplayFirstInput = firstInput, ReplaySecondInput = secondInput };
    }

    private sealed record ToolingTree(
        ToolingTestRuntime Runtime,
        MGDesktop Desktop,
        MGWindow Window,
        MGStackPanel Scope,
        MGWindow Popup,
        MGBorder PopupContent,
        MGTextBox? ReplayFirstInput,
        MGTextBox? ReplaySecondInput);

    private sealed class ToolingTestRuntime : IUIDesktopRuntime
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

        public ToolingTestRuntime(Rectangle surfaceBounds)
        {
            Surface = new ToolingTestSurface(surfaceBounds, new ToolingTestRenderTarget(surfaceBounds.Width, surfaceBounds.Height));
            AssetProvider = new ToolingTestAssetProvider();
            _textEngine = new ToolingTestTextEngine(DefaultFontFamily);
        }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings Settings, bool DeferBegin)
            => new ToolingNoOpDrawTransaction(this, Settings ?? DrawSettings.Default);

        public void ApplyFrame(UpdateBaseArgs updateArgs)
        {
            UpdateArgs = updateArgs;
            Input.Update(updateArgs);
        }

        public void RegisterView(IUIView View)
        {
        }
    }

    private sealed class ToolingNoOpDrawTransaction : IUIDrawTransaction
    {
        private Rectangle? _currentClipBounds;

        public DrawSettings CurrentSettings { get; private set; }
        public IUIDesktopRuntime Renderer { get; }
        public Rectangle? CurrentClipBounds => _currentClipBounds;

        public ToolingNoOpDrawTransaction(IUIDesktopRuntime renderer, DrawSettings settings)
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

    private sealed class ToolingTestSurface : IUISurface
    {
        private readonly Rectangle _bounds;
        private readonly IUIRenderTarget _renderTarget;

        public ToolingTestSurface(Rectangle bounds, IUIRenderTarget renderTarget)
        {
            _bounds = bounds;
            _renderTarget = renderTarget;
        }

        public Rectangle GetBounds() => _bounds;

        public IUIRenderTarget GetRenderTarget() => _renderTarget;
    }

    private sealed class ToolingTestAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, ToolingTestImageResource> _images = new(StringComparer.OrdinalIgnoreCase);

        public IUIImageResource LoadImage(string assetName)
        {
            if (!_images.TryGetValue(assetName, out ToolingTestImageResource? image))
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

    private class ToolingTestImageResource : IUIImageResource
    {
        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed { get; private set; }

        public ToolingTestImageResource(string id, int width, int height)
        {
            Id = id;
            Width = width;
            Height = height;
        }
    }

    private sealed class ToolingTestRenderTarget : ToolingTestImageResource, IUIRenderTarget
    {
        public ToolingTestRenderTarget(int width, int height)
            : base("tooling-render-target", width, height)
        {
        }
    }

    private sealed class ToolingTestTextEngine : ITextMeasurementEngine
    {
        private readonly string _defaultFontFamily;

        public ToolingTestTextEngine(string defaultFontFamily)
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