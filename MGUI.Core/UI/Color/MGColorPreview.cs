using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System.Diagnostics;

namespace MGUI.Core.UI;

public class MGColorPreview : MGElement
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ColorValue _CurrentValue;
    public ColorValue CurrentValue
    {
        get => _CurrentValue;
        set
        {
            if (_CurrentValue != value)
            {
                _CurrentValue = value;
                NPC(nameof(CurrentValue));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ColorValue _PreviousValue;
    public ColorValue PreviousValue
    {
        get => _PreviousValue;
        set
        {
            if (_PreviousValue != value)
            {
                _PreviousValue = value;
                NPC(nameof(PreviousValue));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _ShowPrevious;
    public bool ShowPrevious
    {
        get => _ShowPrevious;
        set
        {
            if (_ShowPrevious != value)
            {
                _ShowPrevious = value;
                NPC(nameof(ShowPrevious));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _ShowCheckerboard;
    public bool ShowCheckerboard
    {
        get => _ShowCheckerboard;
        set
        {
            if (_ShowCheckerboard != value)
            {
                _ShowCheckerboard = value;
                NPC(nameof(ShowCheckerboard));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _ShowOpaqueComparison;
    public bool ShowOpaqueComparison
    {
        get => _ShowOpaqueComparison;
        set
        {
            if (_ShowOpaqueComparison != value)
            {
                _ShowOpaqueComparison = value;
                NPC(nameof(ShowOpaqueComparison));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _PreviewWidth;
    public int PreviewWidth
    {
        get => _PreviewWidth;
        set
        {
            int actual = Math.Max(0, value);
            if (_PreviewWidth != actual)
            {
                _PreviewWidth = actual;
                LayoutChanged(this, true);
                NPC(nameof(PreviewWidth));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _PreviewHeight;
    public int PreviewHeight
    {
        get => _PreviewHeight;
        set
        {
            int actual = Math.Max(0, value);
            if (_PreviewHeight != actual)
            {
                _PreviewHeight = actual;
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

    public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness SharedSize)
    {
        SharedSize = new(0);
        return new(PreviewWidth, PreviewHeight, 0, 0);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
    {
        Rectangle bounds = ApplyAlignment(LayoutBounds, HorizontalAlignment, VerticalAlignment, new Size(PreviewWidth, PreviewHeight));
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        Rectangle contentBounds = GetContentBounds(bounds, BorderThickness);
        if (ShowCheckerboard)
        {
            DrawCheckerboard(DA, contentBounds);
        }

        Rectangle transparentBounds = GetTransparentComparisonBounds(contentBounds, ShowOpaqueComparison);
        DrawValuePair(DA, transparentBounds, PreviousValue, CurrentValue, ShowPrevious, false);

        Rectangle opaqueBounds = GetOpaqueComparisonBounds(contentBounds, ShowOpaqueComparison);
        if (!opaqueBounds.IsEmpty)
        {
            DrawValuePair(DA, opaqueBounds, PreviousValue, CurrentValue, ShowPrevious, true);
        }

        DrawBorder(DA, bounds);
    }

    internal static Rectangle GetContentBounds(Rectangle bounds, int borderThickness)
    {
        int thickness = Math.Max(0, borderThickness);
        int width = Math.Max(0, bounds.Width - thickness * 2);
        int height = Math.Max(0, bounds.Height - thickness * 2);
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

        int previousWidth = bounds.Width / 2;
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

        int transparentHeight = bounds.Height / 2;
        return new Rectangle(bounds.X, bounds.Y + transparentHeight, bounds.Width, bounds.Height - transparentHeight);
    }

    private void DrawValuePair(ElementDrawArgs DA, Rectangle bounds, ColorValue previousValue, ColorValue currentValue, bool showPrevious, bool forceOpaque)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        Rectangle previousBounds = GetPreviousValueBounds(bounds, showPrevious);
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

        ColorValue actual = forceOpaque ? value.WithAlpha(1f) : value;
        DA.DT.FillRectangle(DA.Offset.ToVector2(), bounds, actual.ToXnaColor() * DA.Opacity);
    }

    private void DrawCheckerboard(ElementDrawArgs DA, Rectangle bounds)
    {
        int cellSize = Math.Max(1, CheckerboardCellSize);
        for (int y = bounds.Top; y < bounds.Bottom; y += cellSize)
        {
            int height = Math.Min(cellSize, bounds.Bottom - y);
            for (int x = bounds.Left; x < bounds.Right; x += cellSize)
            {
                int width = Math.Min(cellSize, bounds.Right - x);
                bool light = ((x - bounds.Left) / cellSize + (y - bounds.Top) / cellSize) % 2 == 0;
                Color color = (light ? CheckerboardLightColor : CheckerboardDarkColor) * DA.Opacity;
                DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(x, y, width, height), color);
            }
        }
    }

    private void DrawBorder(ElementDrawArgs DA, Rectangle bounds)
    {
        int thickness = Math.Max(0, BorderThickness);
        if (thickness <= 0)
        {
            return;
        }

        Color color = BorderColor * DA.Opacity;
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }
}