using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Shapes;
using XamlCanvas = MGUI.Core.UI.XAML.Canvas;
using XamlOverlay = MGUI.Core.UI.XAML.Overlay;
using XamlNumericUpDown = MGUI.Core.UI.XAML.NumericUpDown;
using XamlTabControl = MGUI.Core.UI.XAML.TabControl;
using XamlTextBox = MGUI.Core.UI.XAML.TextBox;
using XamlTreeView = MGUI.Core.UI.XAML.TreeView;
using XamlWrapPanel = MGUI.Core.UI.XAML.WrapPanel;
using MGUI.Shared.Rendering.Clipping;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System.Reflection;

namespace MGUI.Tests.Architecture;

public class BorderShapeAdoptionTests
{
    [Fact]
    public void MGElement_DefinesSeparateSelfAndContentsClipHooks()
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

        var selfClipMethod = typeof(MGElement).GetMethod("GetSelfClipDefinition", flags);
        var contentsClipMethod = typeof(MGElement).GetMethod("GetContentsClipDefinition", flags);

        Assert.NotNull(selfClipMethod);
        Assert.NotNull(contentsClipMethod);
        Assert.Equal(typeof(ClipDefinition), selfClipMethod!.ReturnType);
        Assert.Equal(typeof(ClipDefinition), contentsClipMethod!.ReturnType);
        Assert.Equal(new[] { typeof(ElementDrawArgs), typeof(Rectangle), typeof(Rectangle) }, selfClipMethod.GetParameters().Select(x => x.ParameterType));
        Assert.Equal(new[] { typeof(ElementDrawArgs), typeof(Rectangle), typeof(Rectangle) }, contentsClipMethod.GetParameters().Select(x => x.ParameterType));
    }

    [Fact]
    public void MGBorder_OverridesContentClipDefinition()
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var method = typeof(MGBorder).GetMethod("GetContentsClipDefinition", flags);

        Assert.NotNull(method);
        Assert.Equal(typeof(MGBorder), method!.DeclaringType);
        Assert.Equal(typeof(ClipDefinition), method.ReturnType);
    }

    [Fact]
    public void MGScrollViewer_OverridesViewportContentClipDefinition()
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var method = typeof(MGScrollViewer).GetMethod("GetContentsClipDefinition", flags);

        Assert.NotNull(method);
        Assert.Equal(typeof(MGScrollViewer), method!.DeclaringType);
        Assert.Equal(typeof(ClipDefinition), method.ReturnType);
    }

    [Fact]
    public void MGTextBox_OverridesContentClipDefinition()
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var method = typeof(MGTextBox).GetMethod("GetContentsClipDefinition", flags);

        Assert.NotNull(method);
        Assert.Equal(typeof(MGTextBox), method!.DeclaringType);
        Assert.Equal(typeof(ClipDefinition), method.ReturnType);
    }

    [Fact]
    public void MGTextBox_OverridesDrawContentsForCaretRendering()
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var method = typeof(MGTextBox).GetMethod("DrawContents", flags);

        Assert.NotNull(method);
        Assert.Equal(typeof(MGTextBox), method!.DeclaringType);
    }

    [Fact]
    public void DockingOverlays_ExplicitlyOptOutOfElementClipScopes()
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

        var previewMethod = typeof(MGDockPreviewOverlay).GetMethod("GetSelfClipDefinition", flags);
        var indicatorsMethod = typeof(MGDockDropIndicators).GetMethod("GetSelfClipDefinition", flags);

        Assert.NotNull(previewMethod);
        Assert.NotNull(indicatorsMethod);
        Assert.Equal(typeof(MGDockPreviewOverlay), previewMethod!.DeclaringType);
        Assert.Equal(typeof(MGDockDropIndicators), indicatorsMethod!.DeclaringType);
    }

    [Fact]
    public void ClipGeometryHelper_TranslatesVerticesByDrawOffset()
    {
        MGBoxShape shape = new(new Rectangle(10, 20, 100, 40), new Thickness(0), new MGCornerRadius(12));
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape.Normalize());

        ClipGeometry translated = geometry.ToClipGeometry(new Point(-7, 13));

        Assert.Equal(geometry.Vertices.Count, translated.Vertices.Count);
        Assert.Equal(geometry.Vertices[0] + new Vector2(-7, 13), translated.Vertices[0]);
        Assert.Same(geometry.FillIndices, translated.Indices);
    }

    [Fact]
    public void InteriorFillGeometry_ReusesBorderInnerContour()
    {
        MGBoxShape shape = new(new Rectangle(10, 20, 140, 44), new Thickness(2), new MGCornerRadius(12));
        MGBoxGeometry borderGeometry = MGBoxGeometryBuilder.Build(shape.Normalize());

        MGBoxGeometry fillGeometry = MGBoxGeometryBuilder.BuildInteriorFillGeometry(borderGeometry);

        Assert.True(borderGeometry.HasInnerContour);
        Assert.Equal(borderGeometry.InnerContour.Count, fillGeometry.OuterContour.Count);
        Assert.Equal(borderGeometry.InnerContour[0], fillGeometry.OuterContour[0]);
        Assert.Empty(fillGeometry.InnerContour);
    }

    [Fact]
    public void InteriorFillGeometry_CanOverlapUnderBorderForPaint()
    {
        MGBoxShape shape = new(new Rectangle(10, 20, 140, 44), new Thickness(2), new MGCornerRadius(12));
        MGBoxGeometry borderGeometry = MGBoxGeometryBuilder.Build(shape.Normalize());

        MGBoxGeometry overlappedGeometry = MGBoxGeometryBuilder.BuildInteriorFillGeometry(borderGeometry, 1.0f);

        Assert.True(borderGeometry.HasInnerContour);
        Assert.NotEqual(borderGeometry.InnerContour[0], overlappedGeometry.OuterContour[0]);
    }

    [Fact]
    public void MGElement_ExposesBackgroundBorderOverlayToggle()
    {
        var property = typeof(MGElement).GetProperty(nameof(MGElement.DrawBackgroundBorderOverlayEnabled));

        Assert.NotNull(property);
        Assert.Equal(typeof(bool), property!.PropertyType);
    }

    [Fact]
    public void MGBorder_ExposesCornerRadiusProperty()
    {
        var property = typeof(MGBorder).GetProperty(nameof(MGBorder.CornerRadius));

        Assert.NotNull(property);
        Assert.Equal(typeof(MGCornerRadius), property.PropertyType);
    }

    [Fact]
    public void BorderBackedControls_ExposeCornerRadiusProperty()
    {
        Type[] borderBackedTypes =
        {
            typeof(MGButton),
            typeof(MGChatBox),
            typeof(MGComboBox<string>),
            typeof(MGProgressBar),
            typeof(MGProgressButton),
            typeof(MGTextBox),
            typeof(MGOverlay),
            typeof(MGGroupBox),
            typeof(MGGridColorPicker),
            typeof(MGStopwatch),
            typeof(MGTabControl),
            typeof(MGTimer),
            typeof(MGToggleButton),
            typeof(MGWindow),
            typeof(MGCanvas),
            typeof(MGStackPanel),
            typeof(MGWrapPanel),
            typeof(MGNumericUpDown),
            typeof(VirtualizingStackPanel),
            typeof(MGGridSplitter),
            typeof(MGMenuBar),
            typeof(MGMenuBarItem)
        };

        foreach (Type type in borderBackedTypes)
        {
            var property = type.GetProperty(nameof(MGBorder.CornerRadius));
            Assert.NotNull(property);
            Assert.Equal(typeof(MGCornerRadius), property!.PropertyType);
        }
    }

    [Fact]
    public void XamlInternalBorders_DoNotInheritParentImplicitStyles()
    {
        Assert.False(new XamlCanvas().Border.InheritsParentStyles);
        Assert.False(new XamlOverlay().Border.InheritsParentStyles);
        Assert.False(new XamlTabControl().Border.InheritsParentStyles);
        Assert.False(new XamlTextBox().Border.InheritsParentStyles);
        Assert.False(new XamlTreeView().Border.InheritsParentStyles);
        Assert.False(new XamlWrapPanel().Border.InheritsParentStyles);
        Assert.False(new XamlNumericUpDown().Border.InheritsParentStyles);
    }
}