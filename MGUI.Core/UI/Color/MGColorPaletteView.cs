using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Input.Mouse;
using System;
using System.Diagnostics;

namespace MGUI.Core.UI
{
    public class MGColorPaletteView : MGElement
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGColorPalette _Palette;
        public MGColorPalette Palette
        {
            get => _Palette;
            set
            {
                if (!ReferenceEquals(_Palette, value))
                {
                    _Palette = value;
                    SelectedSwatch = null;
                    LayoutChanged(this, true);
                    NPC(nameof(Palette));
                }
            }
        }

        public MGColorSwatch SelectedSwatch { get; private set; }
        public MGColorPicker TargetPicker { get; private set; }
        public int Columns { get; set; } = 8;
        public int SwatchSize { get; set; } = 18;
        public int Spacing { get; set; } = 4;
        public int BorderThickness { get; set; } = 1;
        public int CheckerboardCellSize { get; set; } = 4;
        public Color BorderColor { get; set; } = Color.Black;
        public Color SelectedBorderColor { get; set; } = Color.Yellow;
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

        public bool SelectSwatch(MGColorSwatch swatch)
        {
            if (swatch == null || Palette?.Swatches.Contains(swatch) != true)
            {
                return false;
            }

            SelectedSwatch = swatch;
            ApplyToTargetPicker(swatch.Value);
            SwatchSelected?.Invoke(this, new ColorSwatchSelectedEventArgs(swatch));
            NPC(nameof(SelectedSwatch));
            return true;
        }

        public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness SharedSize)
        {
            SharedSize = new(0);
            int count = Palette?.Swatches.Count ?? 0;
            int columns = Math.Max(1, Columns);
            int rows = count == 0 ? 0 : (int)Math.Ceiling(count / (double)columns);
            int width = columns * SwatchSize + Math.Max(0, columns - 1) * Spacing + BorderThickness * 2;
            int height = rows * SwatchSize + Math.Max(0, rows - 1) * Spacing + BorderThickness * 2;
            return new(width, height, 0, 0);
        }

        public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            Rectangle bounds = ApplyAlignment(LayoutBounds, HorizontalAlignment, VerticalAlignment, GetDesiredSize());
            for (int index = 0; index < (Palette?.Swatches.Count ?? 0); index++)
            {
                Rectangle swatchBounds = GetSwatchBounds(bounds, index, Columns, SwatchSize, Spacing, BorderThickness);
                DrawCheckerboard(DA, swatchBounds);
                DA.DT.FillRectangle(DA.Offset.ToVector2(), swatchBounds, Palette.Swatches[index].Value.ToXnaColor() * DA.Opacity);
                DrawRectangleBorder(DA, swatchBounds, ReferenceEquals(Palette.Swatches[index], SelectedSwatch) ? SelectedBorderColor : BorderColor);
            }
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
}