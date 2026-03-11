using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;

namespace MGUI.Tests.Architecture;

public class BorderShapeAdoptionTests
{
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