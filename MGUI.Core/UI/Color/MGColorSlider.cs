using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Mouse;
using System.Diagnostics;

namespace MGUI.Core.UI;

public class MGColorSlider : MGElement
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ColorSliderChannel _Channel;
    public ColorSliderChannel Channel
    {
        get => _Channel;
        set
        {
            if (_Channel != value)
            {
                _Channel = value;
                ApplyDefaultRangeForChannel(value);
                NPC(nameof(Channel));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ColorValue _BaseColor;
    public ColorValue BaseColor
    {
        get => _BaseColor;
        set
        {
            if (_BaseColor != value)
            {
                _BaseColor = value;
                NPC(nameof(BaseColor));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private float _Minimum;
    public float Minimum
    {
        get => _Minimum;
        set => SetRange(value, Maximum);
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private float _Maximum;
    public float Maximum
    {
        get => _Maximum;
        set => SetRange(Minimum, value);
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private float _Value;
    public float Value
    {
        get => _Value;
        set => SetValue(value);
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Orientation _Orientation;
    public Orientation Orientation
    {
        get => _Orientation;
        set
        {
            if (_Orientation != value)
            {
                _Orientation = value;
                LayoutChanged(this, true);
                NPC(nameof(Orientation));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _SliderWidth;
    public int SliderWidth
    {
        get => _SliderWidth;
        set
        {
            int actual = Math.Max(0, value);
            if (_SliderWidth != actual)
            {
                _SliderWidth = actual;
                LayoutChanged(this, true);
                NPC(nameof(SliderWidth));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _SliderHeight;
    public int SliderHeight
    {
        get => _SliderHeight;
        set
        {
            int actual = Math.Max(0, value);
            if (_SliderHeight != actual)
            {
                _SliderHeight = actual;
                LayoutChanged(this, true);
                NPC(nameof(SliderHeight));
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

        bool minimumChanged = _Minimum != minimum;
        bool maximumChanged = _Maximum != maximum;
        if (!minimumChanged && !maximumChanged)
        {
            return;
        }

        _Minimum = minimum;
        _Maximum = maximum;
        _ = SetValue(Value);
        if (minimumChanged)
        {
            NPC(nameof(Minimum));
        }

        if (maximumChanged)
        {
            NPC(nameof(Maximum));
        }
    }

    public float SetValue(float desiredValue)
    {
        float actual = Math.Clamp(desiredValue, Minimum, Maximum);
        if (!_Value.Equals(actual))
        {
            float previous = _Value;
            _Value = actual;
            NPC(nameof(Value));
            ValueChanged?.Invoke(this, new EventArgs<float>(previous, _Value));
        }

        return actual;
    }

    public override bool TryHandleNavigationAction(UINavigationAction action)
    {
        float step = Math.Max((Maximum - Minimum) / 100f, 0.01f);
        float largeStep = step * 10f;
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

    public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness SharedSize)
    {
        SharedSize = new(0);
        return new(SliderWidth, SliderHeight, 0, 0);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
    {
        Rectangle bounds = ApplyAlignment(LayoutBounds, HorizontalAlignment, VerticalAlignment, new Size(SliderWidth, SliderHeight));
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        Rectangle content = MGColorPreview.GetContentBounds(bounds, BorderThickness);
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
        float p = Math.Clamp(percent, 0f, 1f);
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
            _Minimum = 0f;
            _Maximum = 360f;
            _Value = Math.Clamp(_Value, _Minimum, _Maximum);
        }
        else
        {
            _Minimum = 0f;
            _Maximum = 1f;
            _Value = Math.Clamp(_Value, _Minimum, _Maximum);
        }

        NPC(nameof(Minimum));
        NPC(nameof(Maximum));
        NPC(nameof(Value));
    }

    private bool TryAdjustValue(float delta)
        => TrySetValue(Value + delta);

    private bool TrySetValue(float value)
    {
        float previous = Value;
        SetValue(value);
        return !previous.Equals(Value);
    }

    private void SetValueFromScreenPosition(Point screenPosition, bool preview)
    {
        Point layoutPoint = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, screenPosition);
        Rectangle bounds = MGColorPreview.GetContentBounds(ApplyAlignment(LayoutBounds, HorizontalAlignment, VerticalAlignment, new Size(SliderWidth, SliderHeight)), BorderThickness);
        float percent = GetPercentFromPoint(layoutPoint, bounds, Orientation);
        float previous = Value;
        float actual = SetValue(GetValueFromPercent(percent, Minimum, Maximum));
        if (preview && !previous.Equals(actual))
        {
            ValueChanging?.Invoke(this, new EventArgs<float>(previous, actual));
        }
    }

    private void DrawGradient(ElementDrawArgs DA, Rectangle bounds)
    {
        if (Orientation == Orientation.Horizontal)
        {
            Color[] cache = GetGradientCache(bounds.Width);
            for (int x = bounds.Left; x < bounds.Right; x++)
            {
                DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(x, bounds.Top, 1, bounds.Height), cache[x - bounds.Left] * DA.Opacity);
            }
        }
        else
        {
            Color[] cache = GetGradientCache(bounds.Height);
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.Left, y, bounds.Width, 1), cache[y - bounds.Top] * DA.Opacity);
            }
        }
    }

    private Color[] GetGradientCache(int length)
    {
        int actualLength = Math.Max(0, length);
        if (GradientCache == null || GradientCacheLength != actualLength || GradientCacheChannel != Channel || GradientCacheBaseColor != BaseColor)
        {
            GradientCacheLength = actualLength;
            GradientCacheChannel = Channel;
            GradientCacheBaseColor = BaseColor;
            GradientCache = new Color[actualLength];
            for (int index = 0; index < GradientCache.Length; index++)
            {
                float percent = GradientCache.Length <= 1 ? 0f : index / (float)(GradientCache.Length - 1);
                GradientCache[index] = GetGradientColor(Channel, percent, BaseColor).ToXnaColor();
            }
        }

        return GradientCache;
    }

    private void DrawThumb(ElementDrawArgs DA, Rectangle bounds)
    {
        float percent = GetPercentFromValue(Value, Minimum, Maximum);
        if (Orientation == Orientation.Horizontal)
        {
            int x = bounds.Left + (int)MathF.Round(bounds.Width * percent);
            Rectangle thumb = new(Math.Clamp(x - ThumbThickness / 2, bounds.Left, bounds.Right), bounds.Top, ThumbThickness, bounds.Height);
            DA.DT.FillRectangle(DA.Offset.ToVector2(), thumb, ThumbColor * DA.Opacity);
            DrawRectangleBorder(DA, thumb, ThumbBorderColor);
        }
        else
        {
            int y = bounds.Top + (int)MathF.Round(bounds.Height * percent);
            Rectangle thumb = new(bounds.Left, Math.Clamp(y - ThumbThickness / 2, bounds.Top, bounds.Bottom), bounds.Width, ThumbThickness);
            DA.DT.FillRectangle(DA.Offset.ToVector2(), thumb, ThumbColor * DA.Opacity);
            DrawRectangleBorder(DA, thumb, ThumbBorderColor);
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

    private void DrawBorder(ElementDrawArgs DA, Rectangle bounds)
        => DrawRectangleBorder(DA, bounds, BorderColor);

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