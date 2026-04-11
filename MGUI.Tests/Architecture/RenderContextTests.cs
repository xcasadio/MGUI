using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;
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
        Type[] parameterTypes = contractType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(x => !x.IsSpecialName)
            .SelectMany(x => x.GetParameters())
            .Select(x => x.ParameterType)
            .ToArray();

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
        Assert.DoesNotContain(typeof(Texture2D), parameterTypes);
        Assert.DoesNotContain(typeof(SpriteEffects), parameterTypes);
    }

    [Fact]
    public void IUIRenderContext_UsesOpaqueRenderTargetAndClipBounds()
    {
        Type contractType = typeof(IUIRenderContext);
        PropertyInfo[] properties = contractType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        string[] propertyNames = properties.Select(x => x.Name).OrderBy(x => x).ToArray();

        Assert.Contains(properties, property => property.Name == nameof(IUIRenderContext.Renderer) && property.PropertyType == typeof(IUIDesktopRuntime));
        Assert.Contains(properties, property => property.Name == nameof(IUIRenderContext.CurrentClipBounds) && property.PropertyType == typeof(Rectangle?));
        Assert.NotNull(contractType.GetMethod(nameof(IUIRenderContext.SetRenderTargetTemporary), new[] { typeof(IUIRenderTarget), typeof(Color?) }));
        Assert.DoesNotContain("GraphicsDevice", propertyNames);
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