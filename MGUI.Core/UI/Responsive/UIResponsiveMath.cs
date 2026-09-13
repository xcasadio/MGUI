using MonoGame.Extended;

namespace MGUI.Core.UI.Responsive;

public static class UIResponsiveMath
{
    public static int ScaleInt(int value, float scale)
        => (int)Math.Round(value * scale, MidpointRounding.AwayFromZero);

    public static int? ScaleNullableInt(int? value, float scale)
        => value.HasValue ? ScaleInt(value.Value, scale) : null;

    public static Thickness ScaleThickness(Thickness value, float scale)
        => new(
            ScaleInt(value.Left, scale),
            ScaleInt(value.Top, scale),
            ScaleInt(value.Right, scale),
            ScaleInt(value.Bottom, scale));

    public static Size ScaleSize(Size value, float scale)
        => new(ScaleInt(value.Width, scale), ScaleInt(value.Height, scale));

    public static float ClampPositive(float value, float min, float max)
        => Math.Clamp(value, min, max);

    /// <summary>Scales a spacing value (a gutter owned by a container, e.g. <c>MGStackPanel.Spacing</c>) by <paramref name="scale"/>,
    /// rounding with <see cref="ScaleInt"/> but guaranteeing that any non-zero design-space spacing keeps at least 1 pixel after scaling
    /// down. A zero spacing always stays zero.</summary>
    public static int ScaleSpacing(int value, float scale)
        => value <= 0 ? value : Math.Max(1, ScaleInt(value, scale));

    /// <summary>Scales a gridline margin owned by a container (e.g. <c>MGGrid.GridLineMargin</c>) by <paramref name="scale"/>.<br/>
    /// When the design-space gutter between two gridline margins (<paramref name="designSpacing"/> - 2 * <paramref name="margin"/>) is
    /// positive - i.e. a gridline is actually visible in design space - the scaled margin is capped so that at least 1 pixel of that
    /// gutter survives at the already-resolved <paramref name="resolvedSpacing"/>: <c>min(ScaleInt(margin, scale), max(0, (resolvedSpacing - 1) / 2))</c>
    /// using integer division. Otherwise (the author already broke the documented invariant in design space) the margin is scaled
    /// plainly with <see cref="ScaleInt"/>, and callers are expected to clamp the resulting fill to be &gt;= 0.</summary>
    public static int ScaleGridLineMargin(int margin, int designSpacing, int resolvedSpacing, float scale)
        => designSpacing - 2 * margin > 0
            ? Math.Min(ScaleInt(margin, scale), Math.Max(0, (resolvedSpacing - 1) / 2))
            : ScaleInt(margin, scale);
}