using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Brushes.FillBrushes;

/// <summary>A regular grid of equally-sized cells inside a texture region (ADR-0011, decision C4), used by <see cref="MGTextureFillBrush.FrameGrid"/>
/// to pick one cell of a sprite sheet as the effective source rectangle for a given <see cref="MGTextureFillBrush.FrameIndex"/>.<para/>
/// Frames are numbered left-to-right, then top-to-bottom (row-major), starting at 0. Example, the AngryMeteor sample sheet's four green arrow
/// icons: cells of 16 px, spacing 1 px, top margin 6 px, left margin 0 - <c>new MGSpriteSheetGrid(2, 2, new Point(16, 16), new Point(1, 1), new Point(0, 6))</c>
/// numbers them 0 (right arrow, row 0 column 0), 1 (down arrow, row 0 column 1), 2 (left arrow, row 1 column 0), 3 (up arrow, row 1 column 1).</summary>
/// <param name="Columns">The number of cells per row. Must be at least 1.</param>
/// <param name="Rows">The number of rows of cells. Must be at least 1.</param>
/// <param name="CellSize">The pixel size of one cell. Both axes must be at least 1.</param>
/// <param name="Spacing">The pixel gap between adjacent cells, on each axis. Defaults to <see cref="Point.Zero"/>. Must be non-negative.</param>
/// <param name="Margin">The pixel offset from the region's top-left corner to the first cell's top-left corner, on each axis. Defaults to
/// <see cref="Point.Zero"/>. Must be non-negative.</param>
/// <param name="FrameCount">The number of frames actually populated, when fewer than <c>Columns * Rows</c> (e.g. the last row of the sheet is
/// only partially filled). Must be in <c>[1, Columns * Rows]</c> when set. Defaults to <c>Columns * Rows</c> (<see cref="EffectiveFrameCount"/>).</param>
public readonly record struct MGSpriteSheetGrid(int Columns, int Rows, Point CellSize, Point Spacing = default, Point Margin = default, int? FrameCount = null)
{
    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> when this grid's values are not a valid grid (ADR-0011, decision C4):
    /// <see cref="Columns"/>/<see cref="Rows"/> at least 1, <see cref="CellSize"/> at least 1 on both axes, <see cref="Spacing"/>/<see cref="Margin"/>
    /// non-negative on both axes, and <see cref="FrameCount"/> (when set) in <c>[1, Columns * Rows]</c>.</summary>
    public void Validate()
    {
        if (Columns < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Columns), Columns, $"{nameof(Columns)} must be at least 1.");
        }

        if (Rows < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Rows), Rows, $"{nameof(Rows)} must be at least 1.");
        }

        if (CellSize.X < 1 || CellSize.Y < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(CellSize), CellSize, $"{nameof(CellSize)} must be at least 1 on both axes.");
        }

        if (Spacing.X < 0 || Spacing.Y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Spacing), Spacing, $"{nameof(Spacing)} must be non-negative on both axes.");
        }

        if (Margin.X < 0 || Margin.Y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Margin), Margin, $"{nameof(Margin)} must be non-negative on both axes.");
        }

        if (FrameCount.HasValue && (FrameCount.Value < 1 || FrameCount.Value > Columns * Rows))
        {
            throw new ArgumentOutOfRangeException(nameof(FrameCount), FrameCount, $"{nameof(FrameCount)} must be between 1 and {nameof(Columns)} * {nameof(Rows)} ({Columns * Rows}) when set.");
        }
    }

    /// <summary>The number of frames this grid actually exposes: <see cref="FrameCount"/> when set, else <c>Columns * Rows</c>.</summary>
    public int EffectiveFrameCount => FrameCount ?? Columns * Rows;

    /// <summary>The pixel rectangle of frame <paramref name="frameIndex"/> (clamped to <c>[0, EffectiveFrameCount - 1]</c>) inside
    /// <paramref name="region"/> (the brush's own source rectangle, or the whole image when it has none). Row-major: column = index % Columns,
    /// row = index / Columns.</summary>
    public Rectangle GetFrameRectangle(int frameIndex, Rectangle region)
    {
        int clampedIndex = Math.Clamp(frameIndex, 0, EffectiveFrameCount - 1);
        int column = clampedIndex % Columns;
        int row = clampedIndex / Columns;
        int x = region.X + Margin.X + column * (CellSize.X + Spacing.X);
        int y = region.Y + Margin.Y + row * (CellSize.Y + Spacing.Y);
        return new Rectangle(x, y, CellSize.X, CellSize.Y);
    }
}
