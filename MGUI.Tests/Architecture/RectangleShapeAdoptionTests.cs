using MGUI.Core.UI;

namespace MGUI.Tests.Architecture;

public class RectangleShapeAdoptionTests
{
    [Fact]
    public void MGRectangle_ExposesCornerRadiusProperty()
    {
        var property = typeof(MGRectangle).GetProperty(nameof(MGRectangle.CornerRadius));

        Assert.NotNull(property);
        Assert.Equal(typeof(MGCornerRadius), property.PropertyType);
    }
}