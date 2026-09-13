using MGUI.Core.UI;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System.Collections.Generic;
using MGUI.Core.UI.Brushes.BorderBrushes;

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

    /// <summary>Docs/drawing-architecture.md, Limites connues: a border thickness reaching the corner radius used to collapse the inner arc to a single point and leave the
    /// border ring mesh empty (mismatched outer/inner contour counts), so <see cref="DrawTransactionBoxShapeExtensions.DrawBorderRing(IUIDrawContext, Vector2, MGBoxGeometry, Color)"/>
    /// drew nothing. The inner contour now repeats the collapsed point to match the outer count, so the solid ring draws normally.</summary>
    [Fact]
    public void ThicknessReachingCornerRadiusScenario_DrawBorderRingEmitsTriangles()
    {
        MGBoxShape shape = new(new Rectangle(0, 0, 80, 40), new Thickness(4), new MGCornerRadius(4));
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, 8);
        Assert.True(geometry.HasBorderRingMesh);

        GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
        GraphNoOpDrawTransaction transaction = new(runtime, DrawSettings.Default);

        transaction.DrawBorderRing(Vector2.Zero, geometry, Color.White);

        Assert.Equal(geometry.BorderRingIndices.Count / 3, transaction.FillTriangleCalls.Count);
        Assert.NotEmpty(transaction.FillTriangleCalls);
    }

    [Fact]
    public void BorderBrushScenarios_ExposeShapeAwareDrawOverloads()
    {
        Assert.NotNull(typeof(MGUniformBorderBrush).GetMethod("Draw", new[] { typeof(ElementDrawArgs), typeof(MGElement), typeof(MGBoxShape), typeof(MGBoxGeometry) }));
        Assert.NotNull(typeof(MGBandedBorderBrush).GetMethod("Draw", new[] { typeof(ElementDrawArgs), typeof(MGElement), typeof(MGBoxShape), typeof(MGBoxGeometry) }));
        Assert.NotNull(typeof(MGDockedBorderBrush).GetMethod("Draw", new[] { typeof(ElementDrawArgs), typeof(MGElement), typeof(MGBoxShape), typeof(MGBoxGeometry) }));
    }

    [Fact]
    public void BandedBorderBrush_ShapeAwareDraw_KeepsPerBandThicknessBasedOnOriginalBorder()
    {
        List<MGBoxShape> capturedShapes = new();
        RecordingBorderBrush[] recordingBands = Enumerable.Range(0, 6)
            .Select(_ => new RecordingBorderBrush(capturedShapes))
            .ToArray();

        MGBandedBorderBrush brush = new(recordingBands.Select(x => new MGBorderBand(x, 1.0)).ToArray());
        MGBoxShape shape = new(new Rectangle(0, 0, 80, 40), new Thickness(6), MGCornerRadius.Zero);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, 8);

        brush.Draw(default, null!, shape, geometry);

        Assert.Equal(6, capturedShapes.Count);
        Assert.All(capturedShapes, x => Assert.Equal(new Thickness(1), x.NormalizedBorderThickness));
    }

    [Fact]
    public void BoxShapeRegionHelper_PreservesOnlyCornersTouchingHostEdges()
    {
        Rectangle hostBounds = new(0, 0, 100, 40);
        MGCornerRadius hostCornerRadius = new(8, 10, 12, 14);

        MGBoxShape topBand = MGBoxShapeRegionHelper.CreateSubShape(hostBounds, hostCornerRadius, new Rectangle(0, 0, 100, 12));
        MGBoxShape leftSegment = MGBoxShapeRegionHelper.CreateSubShape(hostBounds, hostCornerRadius, new Rectangle(0, 10, 40, 20));

        Assert.Equal(new MGCornerRadius(8, 10, 0, 0), topBand.CornerRadius);
        Assert.Equal(new MGCornerRadius(0, 0, 0, 0), leftSegment.CornerRadius);
    }

    private sealed class RecordingBorderBrush : IBorderBrush
    {
        private readonly List<MGBoxShape> _CapturedShapes;

        public RecordingBorderBrush(List<MGBoxShape> capturedShapes)
        {
            _CapturedShapes = capturedShapes;
        }

        public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds, Thickness BT)
        {
        }

        public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
        {
            _CapturedShapes.Add(Shape.Normalize());
        }

        public IBorderBrush Copy() => new RecordingBorderBrush(_CapturedShapes);
    }
}