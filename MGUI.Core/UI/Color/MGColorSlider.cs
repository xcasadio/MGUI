using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Mouse;
using System.Diagnostics;

namespace MGUI.Core.UI;

public class MGColorSlider : MGElement
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ColorSliderChannel _channel;
    public ColorSliderChannel Channel
    {
        get => _channel;
        set
        {
            if (_channel != value)
            {
                _channel = value;
                ApplyDefaultRangeForChannel(value);
                NotifyPropertyChanged(nameof(Channel));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ColorValue _baseColor;
    public ColorValue BaseColor
    {
        get => _baseColor;
        set
        {
            if (_baseColor != value)
            {
                _baseColor = value;
                NotifyPropertyChanged(nameof(BaseColor));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private float _minimum;
    public float Minimum
    {
        get => _minimum;
        set => SetRange(value, Maximum);
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private float _maximum;
    public float Maximum
    {
        get => _maximum;
        set => SetRange(Minimum, value);
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private float _value;
    public float Value
    {
        get => _value;
        set => SetValue(value);
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Orientation _orientation;
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation != value)
            {
                _orientation = value;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(Orientation));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _sliderWidth;
    public int SliderWidth
    {
        get => _sliderWidth;
        set
        {
            var actual = Math.Max(0, value);
            if (_sliderWidth != actual)
            {
                _sliderWidth = actual;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(SliderWidth));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _sliderHeight;
    public int SliderHeight
    {
        get => _sliderHeight;
        set
        {
            var actual = Math.Max(0, value);
            if (_sliderHeight != actual)
            {
                _sliderHeight = actual;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(SliderHeight));
            }
        }
    }

    public bool ShowCheckerboard { get; set; }
    public int CheckerboardCellSize { get; set; } = 4;
    public Color CheckerboardLightColor { get; set; } = new(210, 210, 210);
    public Color CheckerboardDarkColor { get; set; } = new(130, 130, 130);
    public Color BorderColor { get; set; } = Color.Black;
    public int BorderThickness { get; set; } = 1;
    public Color ThumbColor { get; set; } = Color.White;
    public Color ThumbBorderColor { get; set; } = Color.Black;
    public int ThumbThickness { get; set; } = 3;

    public event EventHandler<EventArgs<float>> ValueChanged;
    public event EventHandler<EventArgs<float>> ValueChanging;
    public event EventHandler DragStarted;
    public event EventHandler DragCompleted;

    private bool IsDragging;
    private Color[] GradientCache;
    private int GradientCacheLength;
    private ColorSliderChannel GradientCacheChannel;
    private ColorValue GradientCacheBaseColor;

    public MGColorSlider(MGWindow window, ColorSliderChannel channel)
        : this(window, channel, Orientation.Horizontal)
    {
    }

    public MGColorSlider(MGWindow window, ColorSliderChannel channel, Orientation orientation)
        : base(window, MGElementType.ColorSlider)
    {
        using (BeginInitializing())
        {
            IsFocusable = true;
            BaseColor = new ColorValue(1f, 1f, 1f, 1f);
            Orientation = orientation;
            SliderWidth = orientation == Orientation.Horizontal ? 160 : 18;
            SliderHeight = orientation == Orientation.Horizontal ? 18 : 160;
            Channel = channel;
            ShowCheckerboard = channel == ColorSliderChannel.Alpha;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Center;

            MouseHandler.DragStartCondition = DragStartCondition.MousePressed;
            MouseHandler.LMBPressedInside += (sender, e) =>
            {
                e.SetHandledBy(this, false);
                SetValueFromScreenPosition(e.Position, preview: false);
            };
            MouseHandler.DragStart += (sender, e) =>
            {
                if (e.IsLMB)
                {
                    IsDragging = true;
                    DragStarted?.Invoke(this, EventArgs.Empty);
                    e.SetHandledBy(this, false);
                }
            };
            MouseHandler.Dragged += (sender, e) =>
            {
                if (e.IsLMB && IsDragging)
                {
                    SetValueFromScreenPosition(e.Position, preview: true);
                }
            };
            MouseHandler.DragEnd += (sender, e) =>
            {
                if (e.IsLMB && IsDragging)
                {
                    IsDragging = false;
                    DragCompleted?.Invoke(this, EventArgs.Empty);
                }
            };
        }
    }

    public void SetRange(float minimum, float maximum)
    {
        if (minimum > maximum)
        {
            throw new ArgumentException($"{nameof(Minimum)} cannot be greater than {nameof(Maximum)}.");
        }

        var minimumChanged = _minimum != minimum;
        var maximumChanged = _maximum != maximum;
        if (!minimumChanged && !maximumChanged)
        {
            return;
        }

        _minimum = minimum;
        _maximum = maximum;
        _ = SetValue(Value);
        if (minimumChanged)
        {
            NotifyPropertyChanged(nameof(Minimum));
        }

        if (maximumChanged)
        {
            NotifyPropertyChanged(nameof(Maximum));
        }
    }

    public float SetValue(float desiredValue)
    {
        var actual = Math.Clamp(desiredValue, Minimum, Maximum);
        if (!_value.Equals(actual))
        {
            var previous = _value;
            _value = actual;
            NotifyPropertyChanged(nameof(Value));
            ValueChanged?.Invoke(this, new EventArgs<float>(previous, _value));
        }

        return actual;
    }

    public override bool TryHandleNavigationAction(UINavigationAction action)
    {
        var step = Math.Max((Maximum - Minimum) / 100f, 0.01f);
        var largeStep = step * 10f;
        return action switch
        {
            UINavigationAction.MoveLeft when Orientation == Orientation.Horizontal => TryAdjustValue(-step),
            UINavigationAction.MoveRight when Orientation == Orientation.Horizontal => TryAdjustValue(step),
            UINavigationAction.MoveUp when Orientation == Orientation.Vertical => TryAdjustValue(-step),
            UINavigationAction.MoveDown when Orientation == Orientation.Vertical => TryAdjustValue(step),
            UINavigationAction.Decrement => TryAdjustValue(-step),
            UINavigationAction.Increment => TryAdjustValue(step),
            UINavigationAction.PageUp => TryAdjustValue(-largeStep),
            UINavigationAction.PageDown => TryAdjustValue(largeStep),
            UINavigationAction.Home => TrySetValue(Minimum),
            UINavigationAction.End => TrySetValue(Maximum),
            _ => false,
        };
    }

    public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness sharedSize)
    {
        sharedSize = new(0);
        return new(SliderWidth, SliderHeight, 0, 0);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        var bounds = ApplyAlignment(layoutBounds, HorizontalAlignment, VerticalAlignment, new Size(SliderWidth, SliderHeight));
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var content = MGColorPreview.GetContentBounds(bounds, BorderThickness);
        if (ShowCheckerboard || Channel == ColorSliderChannel.Alpha)
        {
            DrawCheckerboard(DA, content);
        }

        DrawGradient(DA, content);
        DrawThumb(DA, content);
        DrawBorder(DA, bounds);
    }

    internal static float GetPercentFromValue(float value, float minimum, float maximum)
    {
        if (minimum.Equals(maximum))
        {
            return 0f;
        }

        return Math.Clamp((value - minimum) / (maximum - minimum), 0f, 1f);
    }

    internal static float GetValueFromPercent(float percent, float minimum, float maximum)
        => minimum + Math.Clamp(percent, 0f, 1f) * (maximum - minimum);

    internal static float GetPercentFromPoint(Point point, Rectangle bounds, Orientation orientation)
    {
        if (orientation == Orientation.Horizontal)
        {
            return bounds.Width <= 0 ? 0f : Math.Clamp((point.X - bounds.Left) / (float)bounds.Width, 0f, 1f);
        }

        return bounds.Height <= 0 ? 0f : Math.Clamp((point.Y - bounds.Top) / (float)bounds.Height, 0f, 1f);
    }

    internal static ColorValue GetGradientColor(ColorSliderChannel channel, float percent, ColorValue baseColor)
    {
        var p = Math.Clamp(percent, 0f, 1f);
        return channel switch
        {
            ColorSliderChannel.Hue => ColorSpaceConverter.HsvToRgb(new HsvColor(p * 360f, 1f, 1f, baseColor.A), baseColor.ColorSpace),
            ColorSliderChannel.Alpha => baseColor.WithAlpha(p),
            ColorSliderChannel.Red => new ColorValue(p, baseColor.G, baseColor.B, baseColor.A, baseColor.ColorSpace),
            ColorSliderChannel.Green => new ColorValue(baseColor.R, p, baseColor.B, baseColor.A, baseColor.ColorSpace),
            ColorSliderChannel.Blue => new ColorValue(baseColor.R, baseColor.G, p, baseColor.A, baseColor.ColorSpace),
            ColorSliderChannel.Saturation => ColorSpaceConverter.HsvToRgb(new HsvColor(ColorSpaceConverter.RgbToHsv(baseColor).H, p, Math.Max(ColorSpaceConverter.RgbToHsv(baseColor).V, 1f), baseColor.A), baseColor.ColorSpace),
            ColorSliderChannel.Value => ColorSpaceConverter.HsvToRgb(new HsvColor(ColorSpaceConverter.RgbToHsv(baseColor).H, ColorSpaceConverter.RgbToHsv(baseColor).S, p, baseColor.A), baseColor.ColorSpace),
            ColorSliderChannel.Intensity => new ColorValue(baseColor.R * p, baseColor.G * p, baseColor.B * p, baseColor.A, baseColor.ColorSpace, baseColor.IsHdr),
            _ => baseColor,
        };
    }

    private void ApplyDefaultRangeForChannel(ColorSliderChannel channel)
    {
        if (channel == ColorSliderChannel.Hue)
        {
            _minimum = 0f;
            _maximum = 360f;
            _value = Math.Clamp(_value, _minimum, _maximum);
        }
        else
        {
            _minimum = 0f;
            _maximum = 1f;
            _value = Math.Clamp(_value, _minimum, _maximum);
        }

        NotifyPropertyChanged(nameof(Minimum));
        NotifyPropertyChanged(nameof(Maximum));
        NotifyPropertyChanged(nameof(Value));
    }

    private bool TryAdjustValue(float delta)
        => TrySetValue(Value + delta);

    private bool TrySetValue(float value)
    {
        var previous = Value;
        SetValue(value);
        return !previous.Equals(Value);
    }

    private void SetValueFromScreenPosition(Point screenPosition, bool preview)
    {
        var layoutPoint = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, screenPosition);
        var bounds = MGColorPreview.GetContentBounds(ApplyAlignment(LayoutBounds, HorizontalAlignment, VerticalAlignment, new Size(SliderWidth, SliderHeight)), BorderThickness);
        var percent = GetPercentFromPoint(layoutPoint, bounds, Orientation);
        var previous = Value;
        var actual = SetValue(GetValueFromPercent(percent, Minimum, Maximum));
        if (preview && !previous.Equals(actual))
        {
            ValueChanging?.Invoke(this, new EventArgs<float>(previous, actual));
        }
    }

    private void DrawGradient(ElementDrawArgs DA, Rectangle bounds)
    {
        if (Orientation == Orientation.Horizontal)
        {
            var cache = GetGradientCache(bounds.Width);
            for (var x = bounds.Left; x < bounds.Right; x++)
            {
                DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(x, bounds.Top, 1, bounds.Height), cache[x - bounds.Left] * DA.Opacity);
            }
        }
        else
        {
            var cache = GetGradientCache(bounds.Height);
            for (var y = bounds.Top; y < bounds.Bottom; y++)
            {
                DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.Left, y, bounds.Width, 1), cache[y - bounds.Top] * DA.Opacity);
            }
        }
    }

    private Color[] GetGradientCache(int length)
    {
        var actualLength = Math.Max(0, length);
        if (GradientCache == null || GradientCacheLength != actualLength || GradientCacheChannel != Channel || GradientCacheBaseColor != BaseColor)
        {
            GradientCacheLength = actualLength;
            GradientCacheChannel = Channel;
            GradientCacheBaseColor = BaseColor;
            GradientCache = new Color[actualLength];
            for (var index = 0; index < GradientCache.Length; index++)
            {
                var percent = GradientCache.Length <= 1 ? 0f : index / (float)(GradientCache.Length - 1);
                GradientCache[index] = GetGradientColor(Channel, percent, BaseColor).ToXnaColor();
            }
        }

        return GradientCache;
    }

    private void DrawThumb(ElementDrawArgs DA, Rectangle bounds)
    {
        var percent = GetPercentFromValue(Value, Minimum, Maximum);
        if (Orientation == Orientation.Horizontal)
        {
            var x = bounds.Left + (int)MathF.Round(bounds.Width * percent);
            Rectangle thumb = new(Math.Clamp(x - ThumbThickness / 2, bounds.Left, bounds.Right), bounds.Top, ThumbThickness, bounds.Height);
            DA.DT.FillRectangle(DA.Offset.ToVector2(), thumb, ThumbColor * DA.Opacity);
            DrawRectangleBorder(DA, thumb, ThumbBorderColor);
        }
        else
        {
            var y = bounds.Top + (int)MathF.Round(bounds.Height * percent);
            Rectangle thumb = new(bounds.Left, Math.Clamp(y - ThumbThickness / 2, bounds.Top, bounds.Bottom), bounds.Width, ThumbThickness);
            DA.DT.FillRectangle(DA.Offset.ToVector2(), thumb, ThumbColor * DA.Opacity);
            DrawRectangleBorder(DA, thumb, ThumbBorderColor);
        }
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
                DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(x, y, width, height), (light ? CheckerboardLightColor : CheckerboardDarkColor) * DA.Opacity);
            }
        }
    }

    private void DrawBorder(ElementDrawArgs DA, Rectangle bounds)
        => DrawRectangleBorder(DA, bounds, BorderColor);

    private void DrawRectangleBorder(ElementDrawArgs DA, Rectangle bounds, Color color)
    {
        var thickness = Math.Max(1, BorderThickness);
        var actual = color * DA.Opacity;
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), actual);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), actual);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), actual);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), actual);
    }
}