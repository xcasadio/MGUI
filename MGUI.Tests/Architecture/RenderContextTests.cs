using MGUI.Shared.Rendering;
using System.IO;

namespace MGUI.Tests.Architecture;

public class RenderContextTests
{
    [Fact]
    public void DrawTransaction_ImplementsIUIRenderContext()
    {
        Assert.Contains(typeof(IUIRenderContext), typeof(DrawTransaction).GetInterfaces());
    }

    [Fact]
    public void DrawTransaction_ImplementsIUIDrawContext()
    {
        Assert.Contains(typeof(IUIDrawContext), typeof(DrawTransaction).GetInterfaces());
    }

    [Fact]
    public void IUIDrawContext_ExposesHighLevelDrawCapabilitiesWithoutConcreteRendererAccess()
    {
        Type contractType = typeof(IUIDrawContext);
        string[] propertyNames = contractType.GetProperties().Select(x => x.Name).OrderBy(x => x).ToArray();
        string[] methodNames = contractType.GetMethods().Where(x => !x.IsSpecialName).Select(x => x.Name).Distinct().OrderBy(x => x).ToArray();

        Assert.Equal(new[] { "CurrentSettings" }, propertyNames);
        Assert.Contains("DrawTextureAt", methodNames);
        Assert.Contains("DrawTextureTo", methodNames);
        Assert.Contains("FillCircle", methodNames);
        Assert.Contains("FillPolygon", methodNames);
        Assert.Contains("FillRectangle", methodNames);
        Assert.Contains("FillTriangle", methodNames);
        Assert.Contains("StrokeCircle", methodNames);
        Assert.Contains("StrokeLineSegment", methodNames);
        Assert.Contains("StrokeRectangle", methodNames);
        Assert.DoesNotContain("Renderer", propertyNames);
        Assert.DoesNotContain("GD", propertyNames);
    }

    [Fact]
    public void DrawBaseArgs_ExposesContextProperty()
    {
        Assert.NotNull(typeof(DrawBaseArgs).GetProperty(nameof(DrawBaseArgs.Context)));
    }

    [Fact]
    public void ElementDrawArgs_ExposesContextProperty()
    {
        Assert.NotNull(typeof(MGUI.Core.UI.ElementDrawArgs).GetProperty(nameof(MGUI.Core.UI.ElementDrawArgs.Context)));
    }

    [Fact]
    public void FirstCoreBrushes_UseContextCapabilitiesInsteadOfDrawTransactionCasts()
    {
        string solidFillBrushSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Brushes\Fill Brushes\MGSolidFillBrush.cs");
        string textureFillBrushSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Brushes\Fill Brushes\MGTextureFillBrush.cs");
        string gradientBrushSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Brushes\Fill Brushes\MGProgressBarGradientBrush.cs");

        Assert.DoesNotContain("as MGUI.Shared.Rendering.DrawTransaction", solidFillBrushSource);
        Assert.DoesNotContain("as MGUI.Shared.Rendering.DrawTransaction", textureFillBrushSource);
        Assert.DoesNotContain("DA.DT.FillRectangle", gradientBrushSource);
        Assert.Contains("DA.Context.FillRectangle", solidFillBrushSource);
        Assert.Contains("DA.Context.DrawTextureTo", textureFillBrushSource);
        Assert.Contains("DA.Context.FillRoundedRectangle", gradientBrushSource);
    }

    [Fact]
    public void RoundedShapeExtensions_TargetIUIDrawContext()
    {
        string source = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Shapes\DrawTransactionBoxShapeExtensions.cs");

        Assert.Contains("this IUIDrawContext", source);
        Assert.DoesNotContain("this DrawTransaction", source);
    }
}