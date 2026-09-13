using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Adorners;

public enum MGGuideAxis
{
    Horizontal,
    Vertical,
}

public enum MGGuideAlignment
{
    Start,
    Center,
    End,
}

public class MGGuideAdorner : MGAdorner
{
    private MGGuideAxis _axis = MGGuideAxis.Vertical;
    public MGGuideAxis Axis
    {
        get => _axis;
        set
        {
            if (_axis != value)
            {
                _axis = value;
                NPC(nameof(Axis));
            }
        }
    }

    private MGGuideAlignment _alignment = MGGuideAlignment.Center;
    public MGGuideAlignment Alignment
    {
        get => _alignment;
        set
        {
            if (_alignment != value)
            {
                _alignment = value;
                NPC(nameof(Alignment));
            }
        }
    }

    private int? _positionOverride;
    public int? PositionOverride
    {
        get => _positionOverride;
        set
        {
            if (_positionOverride != value)
            {
                _positionOverride = value;
                NPC(nameof(PositionOverride));
            }
        }
    }

    private Color _guideColor = new(255, 192, 0, 210);
    public Color GuideColor
    {
        get => _guideColor;
        set
        {
            if (_guideColor != value)
            {
                _guideColor = value;
                NPC(nameof(GuideColor));
            }
        }
    }

    private int _guideThickness = 1;
    public int GuideThickness
    {
        get => _guideThickness;
        set
        {
            int clamped = Math.Max(1, value);
            if (_guideThickness != clamped)
            {
                _guideThickness = clamped;
                NPC(nameof(GuideThickness));
            }
        }
    }

    public MGGuideAdorner(MGWindow window)
        : base(window)
    {
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        int coordinate;
        if (PositionOverride.HasValue)
        {
            coordinate = PositionOverride.Value;
        }
        else if (TryGetAdornedBounds(out Rectangle adornedBounds))
        {
            coordinate = MGAdornerGeometryHelper.ResolveGuideCoordinate(adornedBounds, Axis, Alignment);
        }
        else
        {
            return;
        }

        int thickness = Math.Max(1, GuideThickness);
        Color color = GuideColor * DA.Opacity;
        if (color.A <= 0)
        {
            return;
        }

        int offset = thickness / 2;
        Rectangle guideBounds = Axis switch
        {
            MGGuideAxis.Vertical => new Rectangle(coordinate - offset, layoutBounds.Top, thickness, Math.Max(0, layoutBounds.Height)),
            MGGuideAxis.Horizontal => new Rectangle(layoutBounds.Left, coordinate - offset, Math.Max(0, layoutBounds.Width), thickness),
            _ => Rectangle.Empty,
        };

        if (guideBounds.Width <= 0 || guideBounds.Height <= 0)
        {
            return;
        }

        DA.DT.FillRectangle(Vector2.Zero, new RectangleF(guideBounds.X, guideBounds.Y, guideBounds.Width, guideBounds.Height), color);
    }
}