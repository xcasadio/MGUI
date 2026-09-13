using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Adorners;

public class MGResizeHandlesAdorner : MGAdorner
{
    private Color _outlineColor = new(0, 122, 204, 220);
    public Color OutlineColor
    {
        get => _outlineColor;
        set
        {
            if (_outlineColor != value)
            {
                _outlineColor = value;
                NPC(nameof(OutlineColor));
            }
        }
    }

    private int _outlineThickness = 1;
    public int OutlineThickness
    {
        get => _outlineThickness;
        set
        {
            int clamped = Math.Max(0, value);
            if (_outlineThickness != clamped)
            {
                _outlineThickness = clamped;
                NPC(nameof(OutlineThickness));
            }
        }
    }

    private Color _handleFillColor = Color.White;
    public Color HandleFillColor
    {
        get => _handleFillColor;
        set
        {
            if (_handleFillColor != value)
            {
                _handleFillColor = value;
                NPC(nameof(HandleFillColor));
            }
        }
    }

    private Color _handleBorderColor = new(0, 122, 204, 255);
    public Color HandleBorderColor
    {
        get => _handleBorderColor;
        set
        {
            if (_handleBorderColor != value)
            {
                _handleBorderColor = value;
                NPC(nameof(HandleBorderColor));
            }
        }
    }

    private int _handleBorderThickness = 1;
    public int HandleBorderThickness
    {
        get => _handleBorderThickness;
        set
        {
            int clamped = Math.Max(0, value);
            if (_handleBorderThickness != clamped)
            {
                _handleBorderThickness = clamped;
                NPC(nameof(HandleBorderThickness));
            }
        }
    }

    private int _handleSize = 8;
    public int HandleSize
    {
        get => _handleSize;
        set
        {
            int clamped = Math.Max(1, value);
            if (_handleSize != clamped)
            {
                _handleSize = clamped;
                NPC(nameof(HandleSize));
            }
        }
    }

    public MGResizeHandlesAdorner(MGWindow window)
        : base(window)
    {
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        if (!TryGetAdornedBounds(out Rectangle adornedBounds))
        {
            return;
        }

        if (OutlineThickness > 0)
        {
            MGBoundsAdorner.DrawBorder(DA, adornedBounds, OutlineColor * DA.Opacity, OutlineThickness);
        }

        Span<Rectangle> handleBounds = stackalloc Rectangle[MGAdornerGeometryHelper.ResizeHandleCount];
        MGAdornerGeometryHelper.FillResizeHandleBounds(adornedBounds, HandleSize, handleBounds);

        for (int i = 0; i < handleBounds.Length; i++)
        {
            Rectangle handle = handleBounds[i];
            if (HandleFillColor.A > 0)
            {
                DA.DT.FillRectangle(Vector2.Zero, new RectangleF(handle.X, handle.Y, handle.Width, handle.Height), HandleFillColor * DA.Opacity);
            }

            if (HandleBorderThickness > 0)
            {
                MGBoundsAdorner.DrawBorder(DA, handle, HandleBorderColor * DA.Opacity, HandleBorderThickness);
            }
        }
    }
}