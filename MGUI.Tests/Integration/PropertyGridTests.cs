using MGUI.Core.UI;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace MGUI.Tests.Integration;

public class PropertyGridTests
{
    [Fact]
    public void PropertyGridXaml_CreatesRuntime()
    {
        PropertyGridTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default));

        MGDesktop desktop = new(runtime);
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""320"" Height=""180""><PropertyGrid Name=""Inspector"" /></Window>";

        MGWindow window = MGUIXamlParser.LoadRootWindow(desktop, xaml, false, true);

        Assert.NotNull(window.GetElementByName<MGPropertyGrid>("Inspector"));
    }

    [Fact]
    public void RefreshVisibleValues_DoesNotOverwrite_TextBeingEdited()
    {
        PropertyGridHarness harness = CreateHarness(new InspectableEntity { PositionX = 12, Name = "Player", Visible = true, Opacity = 0.5f });
        MGTextBox positionTextBox = GetTextBoxEditor(harness.Grid, nameof(InspectableEntity.PositionX));

        SetFocusedKeyboardHandler(harness.Desktop, positionTextBox);
        positionTextBox.SetText("12.");

        harness.Entity.PositionX = 99;
        harness.Grid.RefreshVisibleValues();

        Assert.Equal("12.", positionTextBox.Text);
        Assert.Equal(99, harness.Entity.PositionX);
    }

    [Fact]
    public void TextEditor_Commits_OnFocusLoss()
    {
        PropertyGridHarness harness = CreateHarness(new InspectableEntity { PositionX = 12, Name = "Player", Visible = true, Opacity = 0.5f });
        MGTextBox positionTextBox = GetTextBoxEditor(harness.Grid, nameof(InspectableEntity.PositionX));

        SetFocusedKeyboardHandler(harness.Desktop, positionTextBox);
        positionTextBox.SetText("42");
        SetFocusedKeyboardHandler(harness.Desktop, null);

        Assert.Equal(42, harness.Entity.PositionX);
        Assert.Equal("42", positionTextBox.Text);
    }

    [Fact]
    public void SelectedObject_SameType_ReusesExistingRows()
    {
        PropertyGridHarness harness = CreateHarness(new InspectableEntity { PositionX = 12, Name = "Player", Visible = true, Opacity = 0.5f });
        IReadOnlyList<object> initialRowRoots = GetRowRootElements(harness.Grid);

        harness.Grid.SelectedObject = new InspectableEntity { PositionX = 88, Name = "Enemy", Visible = false, Opacity = 0.25f };

        IReadOnlyList<object> updatedRowRoots = GetRowRootElements(harness.Grid);
        MGTextBox positionTextBox = GetTextBoxEditor(harness.Grid, nameof(InspectableEntity.PositionX));

        Assert.Equal(initialRowRoots.Count, updatedRowRoots.Count);
        for (int index = 0; index < initialRowRoots.Count; index++)
        {
            Assert.Same(initialRowRoots[index], updatedRowRoots[index]);
        }

        Assert.Equal("88", positionTextBox.Text);
    }

    [Fact]
    public void CategoryCollapseState_Persists_Across_TypeRebuild_WhenNameMatches()
    {
        PropertyGridHarness harness = CreateHarness(new InspectableEntity { PositionX = 12, Name = "Player", Visible = true, Opacity = 0.5f });
        object initialCategory = GetCategoryView(harness.Grid, "Transform");

        SetCategoryCollapsed(initialCategory, true);
        harness.Grid.SelectedObject = new AlternateInspectableEntity { PositionX = 7, Name = "Other", Visible = false };

        object rebuiltCategory = GetCategoryView(harness.Grid, "Transform");

        Assert.True(GetCategoryCollapsed(rebuiltCategory));
    }

    private static PropertyGridHarness CreateHarness(InspectableEntity entity)
    {
        PropertyGridTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default));

        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 24, 24, 440, 300)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0)
        };

        MGPropertyGrid grid = new(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LabelColumnWidth = 160,
            SelectedObject = entity,
        };

        window.SetContent(grid);
        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();

        return new(runtime, desktop, window, grid, entity);
    }

    private static IReadOnlyList<object> GetRowRootElements(MGPropertyGrid grid)
    {
        List<object> result = new();
        foreach (object categoryView in GetCategoryViews(grid))
        {
            IList rows = (IList)categoryView.GetType().GetProperty("Rows", BindingFlags.Public | BindingFlags.Instance)!.GetValue(categoryView)!;
            foreach (object row in rows)
            {
                result.Add(row.GetType().GetProperty("Root", BindingFlags.Public | BindingFlags.Instance)!.GetValue(row)!);
            }
        }

        return result;
    }

    private static IEnumerable GetCategoryViews(MGPropertyGrid grid)
        => (IEnumerable)typeof(MGPropertyGrid).GetField("_CategoryViews", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(grid)!;

    private static object GetCategoryView(MGPropertyGrid grid, string categoryName)
    {
        foreach (object categoryView in GetCategoryViews(grid))
        {
            string name = (string)categoryView.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)!.GetValue(categoryView)!;
            if (name == categoryName)
            {
                return categoryView;
            }
        }

        throw new InvalidOperationException($"No category view named '{categoryName}' was found.");
    }

    private static MGTextBox GetTextBoxEditor(MGPropertyGrid grid, string descriptorName)
    {
        foreach (object categoryView in GetCategoryViews(grid))
        {
            IList rows = (IList)categoryView.GetType().GetProperty("Rows", BindingFlags.Public | BindingFlags.Instance)!.GetValue(categoryView)!;
            foreach (object row in rows)
            {
                object descriptor = row.GetType().GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Instance)!.GetValue(row)!;
                string name = (string)descriptor.GetType().GetProperty(nameof(MGPropertyGridDescriptor.Name), BindingFlags.Public | BindingFlags.Instance)!.GetValue(descriptor)!;
                if (name == descriptorName)
                {
                    object editor = row.GetType().GetProperty("Editor", BindingFlags.Public | BindingFlags.Instance)!.GetValue(row)!;
                    return (MGTextBox)editor.GetType().GetField("TextBox", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(editor)!;
                }
            }
        }

        throw new InvalidOperationException($"No text editor was found for descriptor '{descriptorName}'.");
    }

    private static void SetFocusedKeyboardHandler(MGDesktop desktop, MGElement? element)
    {
        MethodInfo setter = typeof(MGDesktop).GetProperty(nameof(MGDesktop.FocusedKeyboardHandler), BindingFlags.Public | BindingFlags.Instance)!.GetSetMethod(true)!;
        setter.Invoke(desktop, new object?[] { element });
    }

    private static void SetCategoryCollapsed(object categoryView, bool value)
        => categoryView.GetType().GetProperty("IsCollapsed", BindingFlags.Public | BindingFlags.Instance)!.SetValue(categoryView, value);

    private static bool GetCategoryCollapsed(object categoryView)
        => (bool)categoryView.GetType().GetProperty("IsCollapsed", BindingFlags.Public | BindingFlags.Instance)!.GetValue(categoryView)!;

    private sealed class InspectableEntity
    {
        [Category("Transform")]
        public int PositionX { get; set; }

        [Category("Rendering")]
        public bool Visible { get; set; }

        [Category("Rendering")]
        public float Opacity { get; set; }

        [Category("Identity")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class AlternateInspectableEntity
    {
        [Category("Transform")]
        public int PositionX { get; set; }

        [Category("Rendering")]
        public bool Visible { get; set; }

        [Category("Identity")]
        public string Name { get; set; } = string.Empty;
    }

    private readonly record struct PropertyGridHarness(PropertyGridTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, MGPropertyGrid Grid, InspectableEntity Entity);

    private sealed class PropertyGridTestRuntime : IUIDesktopRuntime
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

        public PropertyGridTestRuntime(Rectangle surfaceBounds)
        {
            Surface = new PropertyGridTestSurface(surfaceBounds, new PropertyGridTestRenderTarget(surfaceBounds.Width, surfaceBounds.Height));
            AssetProvider = new PropertyGridTestAssetProvider();
            _textEngine = new PropertyGridTestTextEngine(DefaultFontFamily);
        }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings Settings, bool DeferBegin)
            => new PropertyGridNoOpDrawTransaction(this, Settings ?? DrawSettings.Default);

        public void ApplyFrame(UpdateBaseArgs updateArgs)
        {
            UpdateArgs = updateArgs;
            Input.Update(updateArgs);
        }

        public void RegisterView(IUIView View)
        {
        }
    }

    private sealed class PropertyGridNoOpDrawTransaction : IUIDrawTransaction
    {
        private Rectangle? _currentClipBounds;

        public DrawSettings CurrentSettings { get; private set; }
        public IUIDesktopRuntime Renderer { get; }
        public Rectangle? CurrentClipBounds => _currentClipBounds;

        public PropertyGridNoOpDrawTransaction(IUIDesktopRuntime renderer, DrawSettings settings)
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

    private sealed class PropertyGridTestSurface : IUISurface
    {
        private readonly Rectangle _bounds;
        private readonly IUIRenderTarget _renderTarget;

        public PropertyGridTestSurface(Rectangle bounds, IUIRenderTarget renderTarget)
        {
            _bounds = bounds;
            _renderTarget = renderTarget;
        }

        public Rectangle GetBounds() => _bounds;

        public IUIRenderTarget GetRenderTarget() => _renderTarget;
    }

    private sealed class PropertyGridTestAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, PropertyGridTestImageResource> _images = new(StringComparer.OrdinalIgnoreCase);

        public IUIImageResource LoadImage(string assetName)
        {
            if (!_images.TryGetValue(assetName, out PropertyGridTestImageResource image))
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

    private class PropertyGridTestImageResource : IUIImageResource
    {
        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed { get; private set; }

        public PropertyGridTestImageResource(string id, int width, int height)
        {
            Id = id;
            Width = width;
            Height = height;
        }
    }

    private sealed class PropertyGridTestRenderTarget : PropertyGridTestImageResource, IUIRenderTarget
    {
        public PropertyGridTestRenderTarget(int width, int height)
            : base("propertygrid-render-target", width, height)
        {
        }
    }

    private sealed class PropertyGridTestTextEngine : ITextMeasurementEngine
    {
        private readonly string _defaultFontFamily;

        public PropertyGridTestTextEngine(string defaultFontFamily)
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