using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Brushes.FillBrushes;

/// <summary>An <see cref="IFillBrush"/> that linearly interpolates between 2 colors based on the <see cref="MGProgressBar"/>'s <see cref="MGProgressBar.ValuePercent"/> to determine what color to fill the bounds with.
/// Freezable (ADR-0009, W3): a sealed mutable class deriving from <see cref="UIFreezableBrush"/> whose setters throw once frozen; <see cref="Copy"/>
/// always returns an unfrozen instance sharing the same <see cref="ProgressBar"/> reference (the brush observes it, it does not own it).</summary>
public sealed class MGProgressBarGradientBrush : UIFreezableBrush, IFillBrush
{
    private MGProgressBar _ProgressBar;
    /// <summary><see cref="UIFreezableBrush.ThrowIfFrozen"/> is called explicitly first (fix round, ADR-0009 W3-fix) so a frozen instance
    /// reports the frozen error instead of <see cref="ArgumentNullException"/> when the caller passes <see langword="null"/>.</summary>
    public MGProgressBar ProgressBar
    {
        get => _ProgressBar;
        set { ThrowIfFrozen(); SetProperty(ref _ProgressBar, value ?? throw new ArgumentNullException(nameof(value))); }
    }

    private Color _MinimumValueColor;
    public Color MinimumValueColor
    {
        get => _MinimumValueColor;
        set => SetProperty(ref _MinimumValueColor, value);
    }

    private Color _MiddleValueColor;
    public Color MiddleValueColor
    {
        get => _MiddleValueColor;
        set => SetProperty(ref _MiddleValueColor, value);
    }

    private Color _MaximumValueColor;
    public Color MaximumValueColor
    {
        get => _MaximumValueColor;
        set => SetProperty(ref _MaximumValueColor, value);
    }

    public MGProgressBarGradientBrush(MGProgressBar ProgressBar)
        : this(ProgressBar, Color.Red, Color.Yellow, new Color(0, 255, 0)) { }

    /// <param name="MinimumValueColor">The color to use when <paramref name="ProgressBar"/>'s <see cref="MGProgressBar.ValuePercent"/> is 0.0</param>
    /// <param name="MiddleValueColor">The color to use when <paramref name="ProgressBar"/>'s <see cref="MGProgressBar.ValuePercent"/> is 50.0</param>
    /// <param name="MaximumValueColor">The color to use when <paramref name="ProgressBar"/>'s <see cref="MGProgressBar.ValuePercent"/> is 100.0</param>
    public MGProgressBarGradientBrush(MGProgressBar ProgressBar, Color MinimumValueColor, Color MiddleValueColor, Color MaximumValueColor)
    {
        _ProgressBar = ProgressBar ?? throw new ArgumentNullException(nameof(ProgressBar));
        _MinimumValueColor = MinimumValueColor;
        _MiddleValueColor = MiddleValueColor;
        _MaximumValueColor = MaximumValueColor;
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
    {
        if (ProgressBar != null && DA.Opacity > 0 && !DA.Opacity.IsAlmostZero())
        {
            DA.Context.FillRectangle(DA.Offset.ToVector2(), Bounds, GetFillColor(DA.Opacity));
        }
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
    {
        if (ProgressBar == null || DA.Opacity <= 0 || DA.Opacity.IsAlmostZero())
        {
            return;
        }

        DA.Context.FillRoundedRectangle(DA.Offset.ToVector2(), Geometry, GetFillColor(DA.Opacity));
    }

    private Color GetFillColor(float opacity)
    {
        var progress = ProgressBar.ValuePercent / 100f;

        float min;
        float max;
        Color c1;
        Color c2;
        if (progress > 0.5f)
        {
            min = 0.5f;
            max = 1.0f;
            c1 = MiddleValueColor;
            c2 = MaximumValueColor;
        }
        else
        {
            min = 0f;
            max = 0.5f;
            c1 = MinimumValueColor;
            c2 = MiddleValueColor;
        }

        progress = (progress - min) / (max - min);
        var alpha = (int)(c1.A * (1.0f - progress) + c2.A * progress);
        return new Color(Color.Lerp(c1, c2, progress), alpha) * opacity;
    }

    public override string ToString() => $"{nameof(MGProgressBarGradientBrush)}: {MinimumValueColor} - {MaximumValueColor}";

    public IFillBrush Copy() => new MGProgressBarGradientBrush(ProgressBar, MinimumValueColor, MiddleValueColor, MaximumValueColor);

    /// <summary>Value equality (ADR-0009, W3): two progress-bar gradient brushes are equal when they observe the SAME <see cref="MGProgressBar"/>
    /// (reference identity: this brush's whole purpose is to read that specific control's live value, so a different instance is a different
    /// brush even with identical colours) and all three colours match, regardless of frozen state or instance identity.</summary>
    public bool ValueEquals(IFillBrush other) => other is MGProgressBarGradientBrush p
        && ReferenceEquals(p.ProgressBar, ProgressBar)
        && p.MinimumValueColor == MinimumValueColor && p.MiddleValueColor == MiddleValueColor && p.MaximumValueColor == MaximumValueColor;

    /// <summary>Decision taken during delivery (ADR-0009, W3): see <see cref="MGSolidFillBrush.Equals(object)"/> for the rationale
    /// (by-value <see cref="object.Equals(object)"/>/<see cref="GetHashCode"/>, applied consistently to every converted fill brush).</summary>
    public override bool Equals(object obj) => ValueEquals(obj as IFillBrush);
    public override int GetHashCode() => HashCode.Combine(ProgressBar, MinimumValueColor, MiddleValueColor, MaximumValueColor);
}