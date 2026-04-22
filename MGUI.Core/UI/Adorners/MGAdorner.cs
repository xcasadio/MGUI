using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Rendering.Clipping;

namespace MGUI.Core.UI.Adorners;

public abstract class MGAdorner : MGElement
{
    private MGElement _targetElement;
    public MGElement TargetElement
    {
        get => _targetElement;
        set
        {
            if (_targetElement != value)
            {
                _targetElement = value;
                NPC(nameof(TargetElement));
            }
        }
    }

    private Rectangle? _targetBoundsOverride;
    public Rectangle? TargetBoundsOverride
    {
        get => _targetBoundsOverride;
        set
        {
            if (_targetBoundsOverride != value)
            {
                _targetBoundsOverride = value;
                NPC(nameof(TargetBoundsOverride));
            }
        }
    }

    private Thickness _targetMargin;
    public Thickness TargetMargin
    {
        get => _targetMargin;
        set
        {
            if (!_targetMargin.Equals(value))
            {
                _targetMargin = value;
                NPC(nameof(TargetMargin));
            }
        }
    }

    private bool _clipToTargetBounds;
    public bool ClipToTargetBounds
    {
        get => _clipToTargetBounds;
        set
        {
            if (_clipToTargetBounds != value)
            {
                _clipToTargetBounds = value;
                NPC(nameof(ClipToTargetBounds));
            }
        }
    }

    protected MGAdorner(MGWindow window)
        : base(window, MGElementType.Custom)
    {
        using (BeginInitializing())
        {
            IsHitTestVisible = false;
            ClipToBounds = false;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
        }
    }

    public bool TryGetAdornedBounds(out Rectangle bounds)
    {
        if (TargetBoundsOverride.HasValue)
        {
            Rectangle candidate = MGAdornerGeometryHelper.ResolveTargetBounds(TargetBoundsOverride.Value, TargetMargin);
            if (candidate.Width > 0 && candidate.Height > 0)
            {
                bounds = candidate;
                return true;
            }
        }

        MGElement target = TargetElement;
        if (target != null && target.Visibility == Visibility.Visible)
        {
            Rectangle candidate = MGAdornerGeometryHelper.ResolveTargetBounds(target.ActualLayoutBounds, TargetMargin);
            if (candidate.Width > 0 && candidate.Height > 0)
            {
                bounds = candidate;
                return true;
            }
        }

        bounds = Rectangle.Empty;
        return false;
    }

    internal override ClipDefinition GetSelfClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
    {
        if (ClipToTargetBounds && TryGetAdornedBounds(out Rectangle adornedBounds))
        {
            Rectangle clipBounds = TransformClipBounds(DA, adornedBounds);
            return CreateRectangleClipDefinition(clipBounds, $"{GetType().Name}.Self");
        }

        return base.GetSelfClipDefinition(DA, layoutBounds, targetBounds);
    }
}