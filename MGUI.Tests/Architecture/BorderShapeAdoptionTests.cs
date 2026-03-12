using MGUI.Core.UI;
using MGUI.Core.UI.Clipping;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using Microsoft.Xna.Framework;
using System.Reflection;

namespace MGUI.Tests.Architecture;

public class BorderShapeAdoptionTests
{
    [Fact]
    public void MGElement_DefinesSeparateSelfAndContentsClipHooks()
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

        var selfClipMethod = typeof(MGElement).GetMethod("GetSelfClipRequest", flags);
        var contentsClipMethod = typeof(MGElement).GetMethod("GetContentsClipRequest", flags);

        Assert.NotNull(selfClipMethod);
        Assert.NotNull(contentsClipMethod);
        Assert.Equal(typeof(ElementClipRequest?), selfClipMethod!.ReturnType);
        Assert.Equal(typeof(ElementClipRequest?), contentsClipMethod!.ReturnType);
        Assert.Equal(new[] { typeof(Rectangle) }, selfClipMethod.GetParameters().Select(x => x.ParameterType));
        Assert.Equal(new[] { typeof(Rectangle) }, contentsClipMethod.GetParameters().Select(x => x.ParameterType));
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