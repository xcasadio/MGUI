using MGUI.Core.UI;
using MGUI.Core.UI.Adorners;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using MonoGame.Extended;

namespace MGUI.Samples.Features;

public class AdornerLiteSamples : SampleBase
{
    private readonly MGOverlayPanel _surface;
    private readonly MGTextBlock _selectionStatusText;
    private readonly MGAdornerLayer _adornerLayer;
    private readonly MGBoundsAdorner _selectionBounds;
    private readonly MGResizeHandlesAdorner _resizeHandles;
    private readonly MGGuideAdorner _verticalGuide;
    private readonly MGGuideAdorner _horizontalGuide;
    private bool _guidesVisible = true;

    public AdornerLiteSamples(ContentManager content, MGDesktop desktop)
        : base(content, desktop, nameof(Features), "AdornerLite.xaml")
    {
        ApplyScenarioId("SCN-OVERLAY-002");

        _surface = Window.GetElementByName<MGOverlayPanel>("AdornerSurface");
        _selectionStatusText = Window.GetElementByName<MGTextBlock>("SelectionStatusText");

        _adornerLayer = new(Window);
        _selectionBounds = new(Window)
        {
            BorderColor = new Color(0, 122, 204, 255),
            BorderThickness = 2,
            FillColor = new Color(0, 122, 204, 45),
            TargetMargin = new Thickness(4)
        };
        _resizeHandles = new(Window)
        {
            OutlineColor = new Color(0, 122, 204, 255),
            OutlineThickness = 1,
            HandleFillColor = Color.White,
            HandleBorderColor = new Color(0, 122, 204, 255),
            HandleBorderThickness = 1,
            HandleSize = 10,
            TargetMargin = new Thickness(4)
        };
        _verticalGuide = new(Window)
        {
            Axis = MGGuideAxis.Vertical,
            Alignment = MGGuideAlignment.Center,
            GuideColor = new Color(255, 196, 0, 210),
            GuideThickness = 2
        };
        _horizontalGuide = new(Window)
        {
            Axis = MGGuideAxis.Horizontal,
            Alignment = MGGuideAlignment.Center,
            GuideColor = new Color(255, 196, 0, 210),
            GuideThickness = 2
        };

        _adornerLayer.TryAddAdorner(_verticalGuide, 10);
        _adornerLayer.TryAddAdorner(_horizontalGuide, 11);
        _adornerLayer.TryAddAdorner(_selectionBounds, 20);
        _adornerLayer.TryAddAdorner(_resizeHandles, 21);
        _surface.TryAddChild(_adornerLayer, default, 500);

        Window.GetElementByName<MGButton>("SelectNavigatorButton").AddCommandHandler((_, __) => SelectTarget("Navigator", Window.GetElementByName<MGGroupBox>("NavigatorCard")));
        Window.GetElementByName<MGButton>("SelectMetricsButton").AddCommandHandler((_, __) => SelectTarget("Metrics", Window.GetElementByName<MGGroupBox>("MetricsCard")));
        Window.GetElementByName<MGButton>("SelectLogsButton").AddCommandHandler((_, __) => SelectTarget("Logs", Window.GetElementByName<MGGroupBox>("LogsCard")));
        Window.GetElementByName<MGButton>("ToggleGuidesButton").AddCommandHandler((_, __) => ToggleGuides());

        SelectTarget("Navigator", Window.GetElementByName<MGGroupBox>("NavigatorCard"));
    }

    private void SelectTarget(string label, MGElement target)
    {
        _selectionBounds.TargetElement = target;
        _resizeHandles.TargetElement = target;
        _verticalGuide.TargetElement = target;
        _horizontalGuide.TargetElement = target;
        _selectionStatusText.Text = $"Selected target: {label}";
        ApplyGuideVisibility();
    }

    private void ToggleGuides()
    {
        _guidesVisible = !_guidesVisible;
        ApplyGuideVisibility();
        string guideState = _guidesVisible ? "on" : "off";
        _selectionStatusText.Text = $"Selected target: {DescribeCurrentTarget()} | Guides: {guideState}";
    }

    private void ApplyGuideVisibility()
    {
        Visibility visibility = _guidesVisible ? Visibility.Visible : Visibility.Collapsed;
        _verticalGuide.Visibility = visibility;
        _horizontalGuide.Visibility = visibility;
    }

    private string DescribeCurrentTarget()
        => _selectionBounds.TargetElement?.Name switch
        {
            "NavigatorCard" => "Navigator",
            "MetricsCard" => "Metrics",
            "LogsCard" => "Logs",
            _ => "none"
        };
}