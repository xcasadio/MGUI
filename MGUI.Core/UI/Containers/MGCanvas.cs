using MGUI.Core.UI.Brushes.BorderBrushes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Shared.Helpers;

namespace MGUI.Core.UI.Containers;

public class MGCanvas : MGMultiContentHost
{
    private const string LeftMetadataKey = "Canvas.Left";
    private const string TopMetadataKey = "Canvas.Top";
    private const string RightMetadataKey = "Canvas.Right";
    private const string BottomMetadataKey = "Canvas.Bottom";
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

    public static int? GetLeft(MGElement element) => GetCanvasCoordinate(element, LeftMetadataKey);
    public static int? GetTop(MGElement element) => GetCanvasCoordinate(element, TopMetadataKey);
    public static int? GetRight(MGElement element) => GetCanvasCoordinate(element, RightMetadataKey);
    public static int? GetBottom(MGElement element) => GetCanvasCoordinate(element, BottomMetadataKey);

    public static void SetLeft(MGElement element, int? value) => SetCanvasCoordinate(element, LeftMetadataKey, value);
    public static void SetTop(MGElement element, int? value) => SetCanvasCoordinate(element, TopMetadataKey, value);
    public static void SetRight(MGElement element, int? value) => SetCanvasCoordinate(element, RightMetadataKey, value);
    public static void SetBottom(MGElement element, int? value) => SetCanvasCoordinate(element, BottomMetadataKey, value);

    private static int? GetCanvasCoordinate(MGElement element, string metadataKey)
    {
        if (element == null)
        {
            return null;
        }

        if (element.Metadata.TryGetValue(metadataKey, out object value) && value is int actualValue)
        {
            return actualValue;
        }

        return null;
    }

    private static void SetCanvasCoordinate(MGElement element, string metadataKey, int? value)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        if (value.HasValue)
        {
            element.Metadata[metadataKey] = value.Value;
        }
        else
        {
            element.Metadata.Remove(metadataKey);
        }

        if (element.Parent is MGCanvas canvas)
        {
            canvas.LayoutChanged(canvas, true);
        }
    }

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
            BorderElement.OnBorderBrushChanged += (_, _) => NPC(nameof(BorderBrush));
            BorderElement.OnBorderThicknessChanged += (_, _) => NPC(nameof(BorderThickness));
            BorderElement.OnCornerRadiusChanged += (_, _) => NPC(nameof(CornerRadius));
        }
    }

    protected override void UpdateContentLayout(Rectangle bounds)
    {
        if (!HasContent)
        {
            return;
        }

        List<CanvasChildMeasurement> childMeasurements = MeasureChildren();
        MGCanvasLayoutEngine.ArrangeInto(childMeasurements, bounds, _ArrangedChildBounds);
        for (int i = 0; i < Children.Count; i++)
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

        Size desiredSize = MGCanvasLayoutEngine.Measure(MeasureChildren());
        return new Thickness(desiredSize.Width, desiredSize.Height, 0, 0);
    }

    private List<CanvasChildMeasurement> MeasureChildren()
    {
        _ChildMeasurements.Clear();
        foreach (MGElement child in Children)
        {
            child.UpdateMeasurement(UnlimitedMeasureSize, out _, out Thickness fullSize, out _, out _);
            _ChildMeasurements.Add(new CanvasChildMeasurement(fullSize.Width, fullSize.Height, GetLeft(child), GetTop(child), GetRight(child), GetBottom(child), child.IsVisibilityCollapsed));
        }

        return _ChildMeasurements;
    }
}