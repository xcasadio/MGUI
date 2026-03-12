using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Shared.Rendering.Clipping;
using Microsoft.Xna.Framework;
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
            typeof(MGStackPanel),
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
}