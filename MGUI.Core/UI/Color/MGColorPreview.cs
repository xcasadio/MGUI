using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System.Diagnostics;

namespace MGUI.Core.UI;

public class MGColorPreview : MGElement
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ColorValue _currentValue;
    public ColorValue CurrentValue
    {
        get => _currentValue;
        set
        {
            if (_currentValue != value)
            {
                _currentValue = value;
                NPC(nameof(CurrentValue));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ColorValue _previousValue;
    public ColorValue PreviousValue
    {
        get => _previousValue;
        set
        {
            if (_previousValue != value)
            {
                _previousValue = value;
                NPC(nameof(PreviousValue));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _showPrevious;
    public bool ShowPrevious
    {
        get => _showPrevious;
        set
        {
            if (_showPrevious != value)
            {
                _showPrevious = value;
                NPC(nameof(ShowPrevious));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _showCheckerboard;
    public bool ShowCheckerboard
    {
        get => _showCheckerboard;
        set
        {
            if (_showCheckerboard != value)
            {
                _showCheckerboard = value;
                NPC(nameof(ShowCheckerboard));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _showOpaqueComparison;
    public bool ShowOpaqueComparison
    {
        get => _showOpaqueComparison;
        set
        {
            if (_showOpaqueComparison != value)
            {
                _showOpaqueComparison = value;
                NPC(nameof(ShowOpaqueComparison));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _previewWidth;
    public int PreviewWidth
    {
        get => _previewWidth;
        set
        {
            var actual = Math.Max(0, value);
            if (_previewWidth != actual)
            {
                _previewWidth = actual;
                LayoutChanged(this, true);
                NPC(nameof(PreviewWidth));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _previewHeight;
    public int PreviewHeight
    {
        get => _previewHeight;
        set
        {
            var actual = Math.Max(0, value);
            if (_previewHeight != actual)
            {
                _previewHeight = actual;
                LayoutChanged(this, true);
                NPC(nameof(PreviewHeight));
            }
        }
    }

    public int CheckerboardCellSize { get; set; } = 4;
    public Color CheckerboardLightColor { get; set; } = new(210, 210, 210);
    public Color CheckerboardDarkColor { get; set; } = new(130, 130, 130);
    public Color BorderColor { get; set; } = Color.Black;
    public int BorderThickness { get; set; } = 1;

    public MGColorPreview(MGWindow window)
        : this(window, 48, 24)
    {
    }

    public MGColorPreview(MGWindow window, int width, int height)
        : base(window, MGElementType.ColorPreview)
    {
        using (BeginInitializing())
        {
            CurrentValue = new ColorValue(1f, 1f, 1f, 1f);
            PreviousValue = new ColorValue(0f, 0f, 0f, 1f);
            ShowPrevious = false;
            ShowCheckerboard = true;
            ShowOpaqueComparison = false;
            PreviewWidth = width;
            PreviewHeight = height;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Center;
        }
    }

    public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness sharedSize)
    {
        sharedSize = new(0);
        return new(PreviewWidth, PreviewHeight, 0, 0);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        var bounds = ApplyAlignment(layoutBounds, HorizontalAlignment, VerticalAlignment, new Size(PreviewWidth, PreviewHeight));
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var contentBounds = GetContentBounds(bounds, BorderThickness);
        if (ShowCheckerboard)
        {
            DrawCheckerboard(DA, contentBounds);
        }

        var transparentBounds = GetTransparentComparisonBounds(contentBounds, ShowOpaqueComparison);
        DrawValuePair(DA, transparentBounds, PreviousValue, CurrentValue, ShowPrevious, false);

        var opaqueBounds = GetOpaqueComparisonBounds(contentBounds, ShowOpaqueComparison);
        if (!opaqueBounds.IsEmpty)
        {
            DrawValuePair(DA, opaqueBounds, PreviousValue, CurrentValue, ShowPrevious, true);
        }

        DrawBorder(DA, bounds);
    }

    internal static Rectangle GetContentBounds(Rectangle bounds, int borderThickness)
    {
        var thickness = Math.Max(0, borderThickness);
        var width = Math.Max(0, bounds.Width - thickness * 2);
        var height = Math.Max(0, bounds.Height - thickness * 2);
        return new Rectangle(bounds.X + thickness, bounds.Y + thickness, width, height);
    }

    internal static Rectangle GetPreviousValueBounds(Rectangle bounds, bool showPrevious)
        => showPrevious ? new Rectangle(bounds.X, bounds.Y, bounds.Width / 2, bounds.Height) : Rectangle.Empty;

    internal static Rectangle GetCurrentValueBounds(Rectangle bounds, bool showPrevious)
    {
        if (!showPrevious)
        {
            return bounds;
        }

        var previousWidth = bounds.Width / 2;
        return new Rectangle(bounds.X + previousWidth, bounds.Y, bounds.Width - previousWidth, bounds.Height);
    }

    internal static Rectangle GetTransparentComparisonBounds(Rectangle bounds, bool showOpaqueComparison)
        => showOpaqueComparison ? new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height / 2) : bounds;

    internal static Rectangle GetOpaqueComparisonBounds(Rectangle bounds, bool showOpaqueComparison)
    {
        if (!showOpaqueComparison)
        {
            return Rectangle.Empty;
        }

        var transparentHeight = bounds.Height / 2;
        return new Rectangle(bounds.X, bounds.Y + transparentHeight, bounds.Width, bounds.Height - transparentHeight);
    }

    private void DrawValuePair(ElementDrawArgs DA, Rectangle bounds, ColorValue previousValue, ColorValue currentValue, bool showPrevious, bool forceOpaque)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var previousBounds = GetPreviousValueBounds(bounds, showPrevious);
        if (!previousBounds.IsEmpty)
        {
            DrawColor(DA, previousBounds, previousValue, forceOpaque);
        }

        DrawColor(DA, GetCurrentValueBounds(bounds, showPrevious), currentValue, forceOpaque);
    }

    private void DrawColor(ElementDrawArgs DA, Rectangle bounds, ColorValue value, bool forceOpaque)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var actual = forceOpaque ? value.WithAlpha(1f) : value;
        DA.DT.FillRectangle(DA.Offset.ToVector2(), bounds, actual.ToXnaColor() * DA.Opacity);
    }

    private void DrawCheckerboard(ElementDrawArgs DA, Rectangle bounds)
    {
        var cellSize = Math.Max(1, CheckerboardCellSize);
        for (var y = bounds.Top; y < bounds.Bottom; y += cellSize)
        {
            var height = Math.Min(cellSize, bounds.Bottom - y);
            for (var x = bounds.Left; x < bounds.Right; x += cellSize)
            {
                var width = Math.Min(cellSize, bounds.Right - x);
                var light = ((x - bounds.Left) / cellSize + (y - bounds.Top) / cellSize) % 2 == 0;
                var color = (light ? CheckerboardLightColor : CheckerboardDarkColor) * DA.Opacity;
                DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(x, y, width, height), color);
            }
        }
    }

    private void DrawBorder(ElementDrawArgs DA, Rectangle bounds)
    {
        var thickness = Math.Max(0, BorderThickness);
        if (thickness <= 0)
        {
            return;
        }

        var color = BorderColor * DA.Opacity;
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }
}