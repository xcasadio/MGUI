using MGUI.Core.UI;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;
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

public class DataGridLiteTests
{
    [Fact]
    public void SelectRow_UpdatesSelectedItemAndIndex()
    {
        DataGridHarness harness = CreateHarness(16, 220);

        Assert.True(harness.Grid.SelectRow(4));
        Assert.Equal(4, harness.Grid.SelectedRowIndex);
        Assert.True(harness.Grid.TryGetSelectedItem(out GridRow selected));
        Assert.Equal(harness.Rows[4].Pid, selected.Pid);
    }

    [Fact]
    public void SortByColumnIndex_ReordersRowsUsingConfiguredKey()
    {
        DataGridHarness harness = CreateHarness(12, 220);
        GridRow expected = harness.Rows.OrderByDescending(x => x.CpuPercent).First();

        Assert.True(harness.Grid.SortByColumnIndex(2, SortDirection.Descending));

        Assert.Equal(expected.Pid, harness.Grid.RowItems[0].Data.Pid);
        Assert.Equal(SortDirection.Descending, harness.Grid.ActiveSortColumn?.CurrentSortDirection);
    }

    [Fact]
    public void EnsureRowVisible_MovesVerticalOffset()
    {
        DataGridHarness harness = CreateHarness(40, 180);

        Assert.Equal(0f, harness.Grid.ScrollViewer.VerticalOffset);
        Assert.True(harness.Grid.EnsureRowVisible(39));
        Assert.True(harness.Grid.ScrollViewer.VerticalOffset > 0f);
    }

    [Fact]
    public void DataGridXaml_CreatesDataGridRuntime()
    {
        DataGridTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default));

        MGDesktop desktop = new(runtime);
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:System=""clr-namespace:System;assembly=mscorlib"" Width=""320"" Height=""180"">
    <DataGrid Name=""Grid"" ItemType=""{x:Type System:String}"" />
</Window>";

        MGWindow window = MGUIXamlParser.LoadRootWindow(desktop, xaml, false, true);

        Assert.NotNull(window.GetElementByName<MGDataGrid<string>>("Grid"));
    }

    private static DataGridHarness CreateHarness(int itemCount, int windowHeight)
    {
        DataGridTestRuntime runtime = new(new Rectangle(0, 0, 960, 720));
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default));

        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 24, 24, 760, windowHeight)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0)
        };

        MGDataGrid<GridRow> grid = new(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        grid.AddTextColumn(new ListViewColumnWidth(72), "PID", row => row.Pid, row => row.Pid, HorizontalAlignment.Right);
        grid.AddTextColumn(new ListViewColumnWidth(2.0), "Process", row => row.Name, row => row.Name);
        grid.AddTextColumn(new ListViewColumnWidth(96), "CPU %", row => row.CpuPercent.ToString("F1"), row => row.CpuPercent, HorizontalAlignment.Right);

        List<GridRow> rows = Enumerable.Range(0, itemCount)
            .Select(index => new GridRow(4200 + index, $"Worker-{index:D2}", Math.Round(((index * 13) % 97) + (index % 2) * 0.5, 1)))
            .ToList();
        grid.SetItemsSource(rows);

        window.SetContent(grid);
        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();

        return new(runtime, desktop, window, grid, rows);
    }

    private readonly record struct GridRow(int Pid, string Name, double CpuPercent);

    private readonly record struct DataGridHarness(DataGridTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, MGDataGrid<GridRow> Grid, List<GridRow> Rows);

    private sealed class DataGridTestRuntime : IUIDesktopRuntime
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

        public DataGridTestRuntime(Rectangle surfaceBounds)
        {
            Surface = new DataGridTestSurface(surfaceBounds, new DataGridTestRenderTarget(surfaceBounds.Width, surfaceBounds.Height));
            AssetProvider = new DataGridTestAssetProvider();
            _textEngine = new DataGridTestTextEngine(DefaultFontFamily);
        }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings Settings, bool DeferBegin)
            => new DataGridNoOpDrawTransaction(this, Settings ?? DrawSettings.Default);

        public void ApplyFrame(UpdateBaseArgs updateArgs)
        {
            UpdateArgs = updateArgs;
            Input.Update(updateArgs);
        }

        public void RegisterView(IUIView View)
        {
        }
    }

    private sealed class DataGridNoOpDrawTransaction : IUIDrawTransaction
    {
        private Rectangle? _currentClipBounds;

        public DrawSettings CurrentSettings { get; private set; }
        public IUIDesktopRuntime Renderer { get; }
        public Rectangle? CurrentClipBounds => _currentClipBounds;

        public DataGridNoOpDrawTransaction(IUIDesktopRuntime renderer, DrawSettings settings)
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

    private sealed class DataGridTestSurface : IUISurface
    {
        private readonly Rectangle _bounds;
        private readonly IUIRenderTarget _renderTarget;

        public DataGridTestSurface(Rectangle bounds, IUIRenderTarget renderTarget)
        {
            _bounds = bounds;
            _renderTarget = renderTarget;
        }

        public Rectangle GetBounds() => _bounds;

        public IUIRenderTarget GetRenderTarget() => _renderTarget;
    }

    private sealed class DataGridTestAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, DataGridTestImageResource> _images = new(StringComparer.OrdinalIgnoreCase);

        public IUIImageResource LoadImage(string assetName)
        {
            if (!_images.TryGetValue(assetName, out DataGridTestImageResource image))
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

    private class DataGridTestImageResource : IUIImageResource
    {
        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed { get; private set; }

        public DataGridTestImageResource(string id, int width, int height)
        {
            Id = id;
            Width = width;
            Height = height;
        }
    }

    private sealed class DataGridTestRenderTarget : DataGridTestImageResource, IUIRenderTarget
    {
        public DataGridTestRenderTarget(int width, int height)
            : base("datagrid-render-target", width, height)
        {
        }
    }

    private sealed class DataGridTestTextEngine : ITextMeasurementEngine
    {
        private readonly string _defaultFontFamily;

        public DataGridTestTextEngine(string defaultFontFamily)
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