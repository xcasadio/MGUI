using MGUI.Core.UI.Brushes.BorderBrushes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Shared.Helpers;

namespace MGUI.Core.UI.Containers;

public class MGCanvas : MGMultiContentHost
{
    private static readonly Size UnlimitedMeasureSize = new(int.MaxValue / 2, int.MaxValue / 2);
    private readonly List<CanvasChildMeasurement> _ChildMeasurements = new();
    private readonly List<Rectangle> _ArrangedChildBounds = new();

    #region Border
    public MGComponent<MGBorder> BorderComponent { get; }
    private MGBorder BorderElement { get; }
    public override MGBorder GetBorder() => BorderElement;

    public IBorderBrush BorderBrush
    {
        get => BorderElement.BorderBrush;
        set => BorderElement.BorderBrush = value;
    }

    public Thickness BorderThickness
    {
        get => BorderElement.BorderThickness;
        set => BorderElement.BorderThickness = value;
    }

    public MGCornerRadius CornerRadius
    {
        get => BorderElement.CornerRadius;
        set => BorderElement.CornerRadius = value;
    }
    #endregion Border

    public static int? GetLeft(MGElement element) => element?.CanvasLeft;
    public static int? GetTop(MGElement element) => element?.CanvasTop;
    public static int? GetRight(MGElement element) => element?.CanvasRight;
    public static int? GetBottom(MGElement element) => element?.CanvasBottom;

    public static void SetLeft(MGElement element, int? value) => RequireElement(element).CanvasLeft = value;
    public static void SetTop(MGElement element, int? value) => RequireElement(element).CanvasTop = value;
    public static void SetRight(MGElement element, int? value) => RequireElement(element).CanvasRight = value;
    public static void SetBottom(MGElement element, int? value) => RequireElement(element).CanvasBottom = value;

    private static MGElement RequireElement(MGElement element)
        => element ?? throw new ArgumentNullException(nameof(element));

    public bool TryAddChild(MGElement item, int? left = null, int? top = null, int? right = null, int? bottom = null)
    {
        if (!CanChangeContent)
        {
            return false;
        }

        _Children.Add(item);
        SetLeft(item, left);
        SetTop(item, top);
        SetRight(item, right);
        SetBottom(item, bottom);
        return true;
    }

    public bool TryRemoveChild(MGElement item)
    {
        if (!CanChangeContent)
        {
            return false;
        }

        return _Children.Remove(item);
    }

    public bool TryRemoveAll()
    {
        if (!CanChangeContent)
        {
            return false;
        }

        _Children.ClearOneByOne();
        return true;
    }

    public MGCanvas(MGWindow window)
        : base(window, MGElementType.Canvas)
    {
        using (BeginInitializing())
        {
            BorderElement = new(window, 0, null as IFillBrush);
            BorderComponent = MGComponentBase.Create(BorderElement);
            AddComponent(BorderComponent);
            BorderElement.OnBorderBrushChanged += (_, _) => NotifyPropertyChanged(nameof(BorderBrush));
            BorderElement.OnBorderThicknessChanged += (_, _) => NotifyPropertyChanged(nameof(BorderThickness));
            BorderElement.OnCornerRadiusChanged += (_, _) => NotifyPropertyChanged(nameof(CornerRadius));
        }
    }

    protected override void UpdateContentLayout(Rectangle bounds)
    {
        if (!HasContent)
        {
            return;
        }

        var childMeasurements = MeasureChildren();
        MGCanvasLayoutEngine.ArrangeInto(childMeasurements, bounds, _ArrangedChildBounds);
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].UpdateLayout(_ArrangedChildBounds[i]);
        }
    }

    protected override Thickness UpdateContentMeasurement(Size availableSize)
    {
        if (!HasContent)
        {
            return UpdateContentMeasurementBaseImplementation(availableSize);
        }

        var desiredSize = MGCanvasLayoutEngine.Measure(MeasureChildren());
        return new Thickness(desiredSize.Width, desiredSize.Height, 0, 0);
    }

    private List<CanvasChildMeasurement> MeasureChildren()
    {
        _ChildMeasurements.Clear();
        foreach (var child in Children)
        {
            child.UpdateMeasurement(UnlimitedMeasureSize, out _, out var fullSize, out _, out _);
            _ChildMeasurements.Add(new CanvasChildMeasurement(fullSize.Width, fullSize.Height, GetLeft(child), GetTop(child), GetRight(child), GetBottom(child), child.IsVisibilityCollapsed));
        }

        return _ChildMeasurements;
    }
}