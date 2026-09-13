using Microsoft.Xna.Framework;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Mouse;

namespace MGUI.Core.UI;

public enum CheckIndicatorStyle
{
    CheckMark,
    FilledSquare,
}

public class MGCheckStateIcon : MGElement
{
    private bool? _CheckState;
    public bool? CheckState
    {
        get => _CheckState;
        set
        {
            if (_CheckState != value)
            {
                _CheckState = value;
                NotifyPropertyChanged(nameof(CheckState));
            }
        }
    }

    private Color _MarkColor;
    public Color MarkColor
    {
        get => _MarkColor;
        set
        {
            if (_MarkColor != value)
            {
                _MarkColor = value;
                NotifyPropertyChanged(nameof(MarkColor));
            }
        }
    }

    private bool _IsShadowed;
    public bool IsShadowed
    {
        get => _IsShadowed;
        set
        {
            if (_IsShadowed != value)
            {
                _IsShadowed = value;
                NotifyPropertyChanged(nameof(IsShadowed));
            }
        }
    }

    private Color _ShadowColor;
    public Color ShadowColor
    {
        get => _ShadowColor;
        set
        {
            if (_ShadowColor != value)
            {
                _ShadowColor = value;
                NotifyPropertyChanged(nameof(ShadowColor));
            }
        }
    }

    private Point _ShadowOffset;
    public Point ShadowOffset
    {
        get => _ShadowOffset;
        set
        {
            if (_ShadowOffset != value)
            {
                _ShadowOffset = value;
                NotifyPropertyChanged(nameof(ShadowOffset));
            }
        }
    }

    private Color _CheckedFillColor;
    public Color CheckedFillColor
    {
        get => _CheckedFillColor;
        set
        {
            if (_CheckedFillColor != value)
            {
                _CheckedFillColor = value;
                NotifyPropertyChanged(nameof(CheckedFillColor));
            }
        }
    }

    private CheckIndicatorStyle _CheckedIndicatorStyle;
    public CheckIndicatorStyle CheckedIndicatorStyle
    {
        get => _CheckedIndicatorStyle;
        set
        {
            if (_CheckedIndicatorStyle != value)
            {
                _CheckedIndicatorStyle = value;
                NotifyPropertyChanged(nameof(CheckedIndicatorStyle));
            }
        }
    }

    private Color _IndeterminateFillColor;
    public Color IndeterminateFillColor
    {
        get => _IndeterminateFillColor;
        set
        {
            if (_IndeterminateFillColor != value)
            {
                _IndeterminateFillColor = value;
                NotifyPropertyChanged(nameof(IndeterminateFillColor));
            }
        }
    }

    public MGCheckStateIcon(MGWindow window)
        : base(window, MGElementType.Misc)
    {
        IsHitTestVisible = false;
        CheckedFillColor = Color.Transparent;
        CheckedIndicatorStyle = CheckIndicatorStyle.CheckMark;
        IndeterminateFillColor = Color.Transparent;
    }

    private static Rectangle GetCheckedSquareBounds(Rectangle bounds)
    {
        var padding = Math.Max(2, Math.Min(bounds.Width, bounds.Height) / 4);
        var targetBounds = bounds.GetCompressed(padding);
        return targetBounds.Width > 0 && targetBounds.Height > 0 ? targetBounds : bounds;
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        var origin = DA.Offset.ToVector2();

        if (!CheckState.HasValue)
        {
            var targetBounds = layoutBounds.GetScaledFromCenter(0.60f);
            if (targetBounds.Width % 2 != 0 || targetBounds.Height % 2 != 0)
            {
                targetBounds = new(targetBounds.Left, targetBounds.Top, targetBounds.Width / 2 * 2, targetBounds.Height / 2 * 2);
            }

            var fillColor = IndeterminateFillColor == Color.Transparent ? MarkColor : IndeterminateFillColor;
            if (IsShadowed)
            {
                DA.DT.FillRectangle(origin, targetBounds.GetTranslated(ShadowOffset), ShadowColor * DA.Opacity);
            }

            DA.DT.FillRectangle(origin, targetBounds, fillColor * DA.Opacity);
        }
        else if (CheckState.Value)
        {
            if (CheckedIndicatorStyle == CheckIndicatorStyle.FilledSquare)
            {
                var targetBounds = GetCheckedSquareBounds(layoutBounds);
                var fillColor = CheckedFillColor == Color.Transparent ? MarkColor : CheckedFillColor;

                if (IsShadowed)
                {
                    DA.DT.FillRectangle(origin, targetBounds.GetTranslated(ShadowOffset), ShadowColor * DA.Opacity);
                }

                DA.DT.FillRectangle(origin, targetBounds, fillColor * DA.Opacity);
            }
            else
            {
                if (CheckedFillColor != Color.Transparent)
                {
                    DA.DT.FillRectangle(origin, layoutBounds, CheckedFillColor * DA.Opacity);
                }

                if (IsShadowed)
                {
                    UISymbolDrawing.DrawCheckMark(DA.DT, origin, layoutBounds.GetTranslated(ShadowOffset), ShadowColor * DA.Opacity);
                }

                UISymbolDrawing.DrawCheckMark(DA.DT, origin, layoutBounds, MarkColor * DA.Opacity);
            }
        }
    }
}

public class MGRadioIndicatorIcon : MGElement
{
    private const int CircleDetailLevel = 32;

    private Color _BorderColor;
    public Color BorderColor
    {
        get => _BorderColor;
        set
        {
            if (_BorderColor != value)
            {
                _BorderColor = value;
                NotifyPropertyChanged(nameof(BorderColor));
            }
        }
    }

    private float _BorderThickness;
    public float BorderThickness
    {
        get => _BorderThickness;
        set
        {
            if (_BorderThickness != value)
            {
                _BorderThickness = value;
                NotifyPropertyChanged(nameof(BorderThickness));
            }
        }
    }

    private VisualStateColorBrush _Background;
    public VisualStateColorBrush Background
    {
        get => _Background;
        set
        {
            if (_Background != value)
            {
                _Background = value;
                NotifyPropertyChanged(nameof(Background));
            }
        }
    }

    private Color _CheckedColor;
    public Color CheckedColor
    {
        get => _CheckedColor;
        set
        {
            if (_CheckedColor != value)
            {
                _CheckedColor = value;
                NotifyPropertyChanged(nameof(CheckedColor));
            }
        }
    }

    private bool _IsChecked;
    public bool IsChecked
    {
        get => _IsChecked;
        set
        {
            if (_IsChecked != value)
            {
                _IsChecked = value;
                NotifyPropertyChanged(nameof(IsChecked));
            }
        }
    }

    public MGRadioIndicatorIcon(MGWindow window)
        : base(window, MGElementType.Misc)
    {
        IsHitTestVisible = false;
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        var fillColor = Background?.GetUnderlay(VisualState.Primary) ?? Color.Transparent;
        Color? overlayColor = null;

        if (!ParentWindow.HasModalWindow && Background != null)
        {
            var layoutSpacePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, InputTracker.Mouse.CurrentPosition);
            var isPressed = false;
            if (InputTracker.Mouse.RecentButtonPressedEvents[MouseButton.Left] != null)
            {
                var pressPositionScreenSpace = InputTracker.Mouse.RecentButtonPressedEvents[MouseButton.Left].Position;
                var pressPositionLayoutSpace = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, pressPositionScreenSpace);
                isPressed = layoutBounds.ContainsInclusive(pressPositionLayoutSpace);
            }

            var isHovered = layoutBounds.ContainsInclusive(layoutSpacePosition);
            overlayColor = Background.GetColorOverlay(isPressed ? SecondaryVisualState.Pressed : isHovered ? SecondaryVisualState.Hovered : SecondaryVisualState.None);
        }

        UISymbolDrawing.DrawRadioIndicator(DA.DT, DA.Offset.ToVector2(), layoutBounds, BorderColor * DA.Opacity,
            BorderThickness, fillColor * DA.Opacity, overlayColor * DA.Opacity, IsChecked, CheckedColor * DA.Opacity, CircleDetailLevel);
    }
}

public class MGRadioBulletIcon : MGElement
{
    private bool _IsChecked;
    public bool IsChecked
    {
        get => _IsChecked;
        set
        {
            if (_IsChecked != value)
            {
                _IsChecked = value;
                NotifyPropertyChanged(nameof(IsChecked));
            }
        }
    }

    private Color _RingColor;
    public Color RingColor
    {
        get => _RingColor;
        set
        {
            if (_RingColor != value)
            {
                _RingColor = value;
                NotifyPropertyChanged(nameof(RingColor));
            }
        }
    }

    private Color _FillColor;
    public Color FillColor
    {
        get => _FillColor;
        set
        {
            if (_FillColor != value)
            {
                _FillColor = value;
                NotifyPropertyChanged(nameof(FillColor));
            }
        }
    }

    public MGRadioBulletIcon(MGWindow window)
        : base(window, MGElementType.Misc)
    {
        IsHitTestVisible = false;
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        UISymbolDrawing.DrawRadioBullet(DA.DT, DA.Offset.ToVector2(), layoutBounds, RingColor * DA.Opacity, IsChecked, FillColor * DA.Opacity);
    }
}

public class MGTriangleArrowIcon : MGElement
{
    private Color _Color;
    public Color Color
    {
        get => _Color;
        set
        {
            if (_Color != value)
            {
                _Color = value;
                NotifyPropertyChanged(nameof(Color));
            }
        }
    }

    private UITriangleArrowDirection _Direction;
    public UITriangleArrowDirection Direction
    {
        get => _Direction;
        set
        {
            if (_Direction != value)
            {
                _Direction = value;
                NotifyPropertyChanged(nameof(Direction));
            }
        }
    }

    public MGTriangleArrowIcon(MGWindow window)
        : base(window, MGElementType.Misc)
    {
        IsHitTestVisible = false;
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        UISymbolDrawing.DrawFilledTriangleArrow(DA.DT, DA.Offset.ToVector2(), layoutBounds, Direction, Color * DA.Opacity);
    }
}

public class MGGripDotsIcon : MGElement
{
    private bool _IsVertical;
    public bool IsVertical
    {
        get => _IsVertical;
        set
        {
            if (_IsVertical != value)
            {
                _IsVertical = value;
                NotifyPropertyChanged(nameof(IsVertical));
            }
        }
    }

    private Color _DotColor;
    public Color DotColor
    {
        get => _DotColor;
        set
        {
            if (_DotColor != value)
            {
                _DotColor = value;
                NotifyPropertyChanged(nameof(DotColor));
            }
        }
    }

    public int DotSize { get; set; } = 2;
    public int DotSpacing { get; set; } = 4;
    public int DotCount { get; set; } = 5;

    public MGGripDotsIcon(MGWindow window)
        : base(window, MGElementType.Misc)
    {
        IsHitTestVisible = false;
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        UISymbolDrawing.DrawGripDots(DA.DT, DA.Offset.ToVector2(), layoutBounds, IsVertical, DotSize, DotSpacing, DotCount, DotColor * DA.Opacity);
    }
}

public class MGCloseIcon : MGElement
{
    private Color _Color;
    public Color Color
    {
        get => _Color;
        set
        {
            if (_Color != value)
            {
                _Color = value;
                NotifyPropertyChanged(nameof(Color));
            }
        }
    }

    public string TextureName { get; set; } = "DockClose";

    public MGCloseIcon(MGWindow window)
        : base(window, MGElementType.Misc)
    {
        IsHitTestVisible = false;
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        if (!string.IsNullOrEmpty(TextureName) && GetResources().TryDrawTexture(DA.DT, TextureName, layoutBounds, DA.Opacity, Color))
        {
            return;
        }

        UISymbolDrawing.DrawCloseIcon(DA.DT, DA.Offset.ToVector2(), layoutBounds, Color * DA.Opacity);
    }
}

public class MGDockPinIcon : MGElement
{
    private Color _Color;
    public Color Color
    {
        get => _Color;
        set
        {
            if (_Color != value)
            {
                _Color = value;
                NotifyPropertyChanged(nameof(Color));
            }
        }
    }

    private bool _IsPinned;
    public bool IsPinned
    {
        get => _IsPinned;
        set
        {
            if (_IsPinned != value)
            {
                _IsPinned = value;
                NotifyPropertyChanged(nameof(IsPinned));
            }
        }
    }

    public string PinnedTextureName { get; set; } = "DockPin";
    public string AutoHideTextureName { get; set; } = "DockPinOff";

    public MGDockPinIcon(MGWindow window)
        : base(window, MGElementType.Misc)
    {
        IsHitTestVisible = false;
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        var textureName = IsPinned ? PinnedTextureName : AutoHideTextureName;
        if (!string.IsNullOrEmpty(textureName) && GetResources().TryDrawTexture(DA.DT, textureName, layoutBounds, DA.Opacity, Color))
        {
            return;
        }

        UISymbolDrawing.DrawDockPinIcon(DA.DT, DA.Offset.ToVector2(), layoutBounds, Color * DA.Opacity);
    }
}

public class MGEllipsisIcon : MGElement
{
    private Color _Color;
    public Color Color
    {
        get => _Color;
        set
        {
            if (_Color != value)
            {
                _Color = value;
                NotifyPropertyChanged(nameof(Color));
            }
        }
    }

    public MGEllipsisIcon(MGWindow window)
        : base(window, MGElementType.Misc)
    {
        IsHitTestVisible = false;
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        UISymbolDrawing.DrawEllipsisIcon(DA.DT, DA.Offset.ToVector2(), layoutBounds, Color * DA.Opacity);
    }
}

public class MGWindowStateIcon : MGElement
{
    private Color _Color;
    public Color Color
    {
        get => _Color;
        set
        {
            if (_Color != value)
            {
                _Color = value;
                NotifyPropertyChanged(nameof(Color));
            }
        }
    }

    private bool _IsRestoredState;
    public bool IsRestoredState
    {
        get => _IsRestoredState;
        set
        {
            if (_IsRestoredState != value)
            {
                _IsRestoredState = value;
                NotifyPropertyChanged(nameof(IsRestoredState));
            }
        }
    }

    public string MaximizedTextureName { get; set; } = "DockMinimize";
    public string NormalTextureName { get; set; } = "DockMaximize";

    public MGWindowStateIcon(MGWindow window)
        : base(window, MGElementType.Misc)
    {
        IsHitTestVisible = false;
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        var textureName = IsRestoredState ? MaximizedTextureName : NormalTextureName;
        if (!string.IsNullOrEmpty(textureName) && GetResources().TryDrawTexture(DA.DT, textureName, layoutBounds, DA.Opacity, Color))
        {
            return;
        }

        UISymbolDrawing.DrawWindowStateIcon(DA.DT, DA.Offset.ToVector2(), layoutBounds, IsRestoredState, Color * DA.Opacity);
    }
}