using MGUI.Core.UI;

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
}