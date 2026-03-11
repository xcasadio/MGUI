using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Shapes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

public class RoundedShapeScenarioTests
{
    [Fact]
    public void SimpleRectangleScenario_UsesRectangleFastPath()
    {
        MGBoxShape shape = new(new Rectangle(0, 0, 80, 40), new Thickness(1), MGCornerRadius.Zero);

        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, 8);

        Assert.True(geometry.UsesRectangleFastPath);
        Assert.Equal(4, geometry.OuterContour.Count);
    }

    [Fact]
    public void UniformRoundedScenario_BuildsRoundedContour()
    {
        MGBoxShape shape = new(new Rectangle(0, 0, 80, 40), new Thickness(2), new MGCornerRadius(10));

        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, 8);

        Assert.False(geometry.UsesRectangleFastPath);
        Assert.True(geometry.OuterContour.Count > 4);
    }

    [Fact]
    public void AsymmetricRoundedScenario_PreservesDistinctCornerRadii()
    {
        MGBoxShape shape = new(new Rectangle(0, 0, 90, 50), new Thickness(3, 5, 7, 9), new MGCornerRadius(4, 12, 16, 6));

        MGBoxShape normalized = shape.Normalize();

        Assert.Equal(4, normalized.NormalizedCornerRadius.TopLeft);
        Assert.Equal(12, normalized.NormalizedCornerRadius.TopRight);
        Assert.Equal(16, normalized.NormalizedCornerRadius.BottomRight);
        Assert.Equal(6, normalized.NormalizedCornerRadius.BottomLeft);
    }

    [Fact]
    public void SmallBoundsAndThickBorderScenario_ClampWithoutThrowing()
    {
        MGBoxShape shape = new(new Rectangle(0, 0, 12, 12), new Thickness(10), new MGCornerRadius(12));

        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, 8);

        Assert.NotEmpty(geometry.OuterContour);
        Assert.True(geometry.Shape.NormalizedBorderThickness.Left <= geometry.Shape.OuterBounds.Width);
        Assert.True(geometry.Shape.NormalizedBorderThickness.Top <= geometry.Shape.OuterBounds.Height);
    }

    [Fact]
    public void BorderBrushScenarios_ExposeShapeAwareDrawOverloads()
    {
        Assert.NotNull(typeof(MGUniformBorderBrush).GetMethod("Draw", new[] { typeof(ElementDrawArgs), typeof(MGElement), typeof(MGBoxShape), typeof(MGBoxGeometry) }));
        Assert.NotNull(typeof(MGBandedBorderBrush).GetMethod("Draw", new[] { typeof(ElementDrawArgs), typeof(MGElement), typeof(MGBoxShape), typeof(MGBoxGeometry) }));
        Assert.NotNull(typeof(MGDockedBorderBrush).GetMethod("Draw", new[] { typeof(ElementDrawArgs), typeof(MGElement), typeof(MGBoxShape), typeof(MGBoxGeometry) }));
    }
}