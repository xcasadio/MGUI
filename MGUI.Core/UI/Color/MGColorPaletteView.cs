using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Input.Mouse;
using System.Diagnostics;

namespace MGUI.Core.UI;

public class MGColorPaletteView : MGElement
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private MGColorPalette _palette;
    public MGColorPalette Palette
    {
        get => _palette;
        set
        {
            if (!ReferenceEquals(_palette, value))
            {
                _palette = value;
                SelectedSwatch = null;
                LayoutChanged(this, true);
                NPC(nameof(Palette));
            }
        }
    }

    public MGColorSwatch SelectedSwatch { get; private set; }
    public int FocusedSwatchIndex { get; private set; } = -1;
    public MGColorPicker TargetPicker { get; private set; }
    public int Columns { get; set; } = 8;
    public int SwatchSize { get; set; } = 18;
    public int Spacing { get; set; } = 4;
    public int BorderThickness { get; set; } = 1;
    public int CheckerboardCellSize { get; set; } = 4;
    public Color BorderColor { get; set; } = Color.Black;
    public Color SelectedBorderColor { get; set; } = Color.Yellow;
    public Color FocusedBorderColor { get; set; } = Color.White;
    public Color CheckerboardLightColor { get; set; } = new(210, 210, 210);
    public Color CheckerboardDarkColor { get; set; } = new(130, 130, 130);

    public event EventHandler<ColorSwatchSelectedEventArgs> SwatchSelected;

    public MGColorPaletteView(MGWindow window)
        : this(window, new MGColorPalette("Palette"))
    {
    }

    public MGColorPaletteView(MGWindow window, MGColorPalette palette)
        : base(window, MGElementType.ColorPaletteView)
    {
        using (BeginInitializing())
        {
            Palette = palette;
            IsFocusable = true;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            MouseHandler.LMBReleasedInside += OnReleasedInside;
        }
    }

    public void BindPicker(MGColorPicker picker)
        => TargetPicker = picker;

    public void SetEnginePresets(MGColorPresetCategory? category = null, string paletteName = "Engine Presets")
        => Palette = MGColorEnginePresets.CreatePalette(paletteName, category);

    public bool SelectSwatch(MGColorSwatch swatch)
    {
        if (swatch == null || Palette?.Swatches.Contains(swatch) != true)
        {
            return false;
        }

        SelectedSwatch = swatch;
        FocusedSwatchIndex = Palette.Swatches.IndexOf(swatch);
        ApplyToTargetPicker(swatch.Value);
        SwatchSelected?.Invoke(this, new ColorSwatchSelectedEventArgs(swatch));
        NPC(nameof(SelectedSwatch));
        return true;
    }

    public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness sharedSize)
    {
        sharedSize = new(0);
        int count = Palette?.Swatches.Count ?? 0;
        int columns = Math.Max(1, Columns);
        int rows = count == 0 ? 0 : (int)Math.Ceiling(count / (double)columns);
        int width = columns * SwatchSize + Math.Max(0, columns - 1) * Spacing + BorderThickness * 2;
        int height = rows * SwatchSize + Math.Max(0, rows - 1) * Spacing + BorderThickness * 2;
        return new(width, height, 0, 0);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        Rectangle bounds = ApplyAlignment(layoutBounds, HorizontalAlignment, VerticalAlignment, GetDesiredSize());
        for (int index = 0; index < (Palette?.Swatches.Count ?? 0); index++)
        {
            Rectangle swatchBounds = GetSwatchBounds(bounds, index, Columns, SwatchSize, Spacing, BorderThickness);
            DrawCheckerboard(DA, swatchBounds);
            DA.DT.FillRectangle(DA.Offset.ToVector2(), swatchBounds, Palette.Swatches[index].Value.ToXnaColor() * DA.Opacity);
            DrawRectangleBorder(DA, swatchBounds, ReferenceEquals(Palette.Swatches[index], SelectedSwatch) ? SelectedBorderColor : BorderColor);
            if (index == FocusedSwatchIndex && !ReferenceEquals(Palette.Swatches[index], SelectedSwatch))
            {
                DrawRectangleBorder(DA, new Rectangle(swatchBounds.X + 2, swatchBounds.Y + 2, Math.Max(0, swatchBounds.Width - 4), Math.Max(0, swatchBounds.Height - 4)), FocusedBorderColor);
            }
        }
    }

    public override bool TryHandleNavigationAction(UINavigationAction action)
    {
        int count = Palette?.Swatches.Count ?? 0;
        if (count == 0)
        {
            return false;
        }

        if (action == UINavigationAction.Submit)
        {
            int submitIndex = FocusedSwatchIndex >= 0 ? FocusedSwatchIndex : SelectedSwatch == null ? 0 : Palette.Swatches.IndexOf(SelectedSwatch);
            return submitIndex >= 0 && submitIndex < count && SelectSwatch(Palette.Swatches[submitIndex]);
        }

        if (action is not (UINavigationAction.MoveLeft or UINavigationAction.MoveRight or UINavigationAction.MoveUp or UINavigationAction.MoveDown or UINavigationAction.MoveNext or UINavigationAction.MovePrevious or UINavigationAction.Home or UINavigationAction.End or UINavigationAction.PageUp or UINavigationAction.PageDown))
        {
            return false;
        }

        int nextIndex = GetNavigationIndex(FocusedSwatchIndex, count, Columns, action);
        if (nextIndex == FocusedSwatchIndex)
        {
            return false;
        }

        FocusedSwatchIndex = nextIndex;
        NPC(nameof(FocusedSwatchIndex));
        return true;
    }

    internal static Rectangle GetSwatchBounds(Rectangle bounds, int index, int columns, int swatchSize, int spacing, int borderThickness)
    {
        int actualColumns = Math.Max(1, columns);
        int row = index / actualColumns;
        int column = index % actualColumns;
        int x = bounds.X + borderThickness + column * (swatchSize + spacing);
        int y = bounds.Y + borderThickness + row * (swatchSize + spacing);
        return new Rectangle(x, y, Math.Max(0, swatchSize), Math.Max(0, swatchSize));
    }

    internal static int? GetSwatchIndexFromPoint(Point point, Rectangle bounds, int count, int columns, int swatchSize, int spacing, int borderThickness)
    {
        int actualColumns = Math.Max(1, columns);
        for (int index = 0; index < count; index++)
        {
            if (GetSwatchBounds(bounds, index, actualColumns, swatchSize, spacing, borderThickness).Contains(point))
            {
                return index;
            }
        }

        return null;
    }

    internal static int GetNavigationIndex(int currentIndex, int count, int columns, UINavigationAction action)
    {
        if (count <= 0)
        {
            return -1;
        }

        int actualColumns = Math.Max(1, columns);
        int normalized = currentIndex < 0 || currentIndex >= count ? 0 : currentIndex;
        return action switch
        {
            UINavigationAction.MoveLeft or UINavigationAction.MovePrevious => Math.Max(0, normalized - 1),
            UINavigationAction.MoveRight or UINavigationAction.MoveNext => Math.Min(count - 1, normalized + 1),
            UINavigationAction.MoveUp => Math.Max(0, normalized - actualColumns),
            UINavigationAction.MoveDown => Math.Min(count - 1, normalized + actualColumns),
            UINavigationAction.Home => 0,
            UINavigationAction.End => count - 1,
            UINavigationAction.PageUp => Math.Max(0, normalized - actualColumns * 3),
            UINavigationAction.PageDown => Math.Min(count - 1, normalized + actualColumns * 3),
            _ => normalized,
        };
    }

    private Size GetDesiredSize()
    {
        int count = Palette?.Swatches.Count ?? 0;
        int columns = Math.Max(1, Columns);
        int rows = count == 0 ? 0 : (int)Math.Ceiling(count / (double)columns);
        return new Size(
            columns * SwatchSize + Math.Max(0, columns - 1) * Spacing + BorderThickness * 2,
            rows * SwatchSize + Math.Max(0, rows - 1) * Spacing + BorderThickness * 2);
    }

    private void OnReleasedInside(object sender, BaseMouseReleasedEventArgs e)
    {
        Point layoutPoint = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
        Rectangle bounds = ApplyAlignment(LayoutBounds, HorizontalAlignment, VerticalAlignment, GetDesiredSize());
        int? index = GetSwatchIndexFromPoint(layoutPoint, bounds, Palette?.Swatches.Count ?? 0, Columns, SwatchSize, Spacing, BorderThickness);
        if (index.HasValue && SelectSwatch(Palette.Swatches[index.Value]))
        {
            e.SetHandledBy(this, false);
        }
    }

    private void ApplyToTargetPicker(ColorValue value)
    {
        if (TargetPicker == null)
        {
            return;
        }

        TargetPicker.BeginEdit();
        TargetPicker.Model.PreviewValue(value);
        if (TargetPicker.CommitMode != ColorEditCommitMode.ExplicitOkCancel)
        {
            TargetPicker.CommitEdit();
        }
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
                DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(x, y, width, height), (light ? CheckerboardLightColor : CheckerboardDarkColor) * DA.Opacity);
            }
        }
    }

    private void DrawRectangleBorder(ElementDrawArgs DA, Rectangle bounds, Color color)
    {
        int thickness = Math.Max(1, BorderThickness);
        Color actual = color * DA.Opacity;
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), actual);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), actual);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), actual);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), actual);
    }
}