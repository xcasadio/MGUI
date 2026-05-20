using System.Reflection;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Graph;

public class GraphViewportTransformTests
{
    [Fact]
    public void Transform_RoundTripsWorldAndViewportCoordinates()
    {
        object transform = CreateTransform();
        Set(transform, "Zoom", 2.0f);
        Set(transform, "Pan", new Vector2(100, 50));

        Vector2 world = new(-25, 40);
        Vector2 viewport = Invoke<Vector2>(transform, "WorldToViewport", world);
        Vector2 roundTrip = Invoke<Vector2>(transform, "ViewportToWorld", viewport);

        Assert.Equal(new Vector2(50, 130), viewport);
        Assert.Equal(world.X, roundTrip.X, 4);
        Assert.Equal(world.Y, roundTrip.Y, 4);
    }

    [Fact]
    public void PanBy_OffsetsViewportWithoutChangingZoom()
    {
        object transform = CreateTransform();
        Set(transform, "Zoom", 1.5f);

        Invoke(transform, "PanBy", new Vector2(12, -8));

        Assert.Equal(new Vector2(12, -8), Get<Vector2>(transform, "Pan"));
        Assert.Equal(1.5f, Get<float>(transform, "Zoom"));
    }

    [Fact]
    public void ZoomAt_KeepsMouseWorldPointStable()
    {
        object transform = CreateTransform();
        Vector2 mouse = new(250, 120);
        Vector2 before = Invoke<Vector2>(transform, "ViewportToWorld", mouse);

        Invoke(transform, "ZoomAt", mouse, 2.0f);
        Vector2 after = Invoke<Vector2>(transform, "ViewportToWorld", mouse);

        Assert.Equal(2.0f, Get<float>(transform, "Zoom"));
        Assert.Equal(before.X, after.X, 4);
        Assert.Equal(before.Y, after.Y, 4);
    }

    [Fact]
    public void ZoomAt_ClampsToMinAndMaxZoom()
    {
        object transform = CreateTransform();
        Set(transform, "MinZoom", 0.5f);
        Set(transform, "MaxZoom", 2.0f);

        Invoke(transform, "ZoomAt", Vector2.Zero, 100.0f);
        Assert.Equal(2.0f, Get<float>(transform, "Zoom"));

        Invoke(transform, "ZoomAt", Vector2.Zero, 0.01f);
        Assert.Equal(0.5f, Get<float>(transform, "Zoom"));
    }

    [Fact]
    public void SnapPoint_UsesWorldGridIndependentFromZoom()
    {
        object transform = CreateTransform();
        Set(transform, "Zoom", 3.0f);
        Set(transform, "GridSize", 16.0f);

        Vector2 snapped = Invoke<Vector2>(transform, "SnapPoint", new Vector2(23, -9));

        Assert.Equal(new Vector2(16, -16), snapped);
    }

    [Fact]
    public void FrameBounds_FitsBoundsInsideViewportWithPadding()
    {
        object transform = CreateTransform();
        RectangleF worldBounds = new(100, 100, 200, 100);
        Rectangle viewport = new(0, 0, 1000, 500);

        Invoke(transform, "FrameBounds", worldBounds, viewport, 50.0f);

        Assert.Equal(4.0f, Get<float>(transform, "Zoom"), 4);
        Assert.Equal(new Vector2(-300, -350), Get<Vector2>(transform, "Pan"));
        Assert.Equal(new Vector2(500, 250), Invoke<Vector2>(transform, "WorldToViewport", new Vector2(200, 150)));
    }

    [Fact]
    public void FrameOrigin_CentersWorldOriginInViewport()
    {
        object transform = CreateTransform();
        Set(transform, "Zoom", 2.0f);
        Set(transform, "Pan", new Vector2(-100, -100));

        Invoke(transform, "FrameOrigin", new Rectangle(0, 0, 800, 600));

        Assert.Equal(1.0f, Get<float>(transform, "Zoom"));
        Assert.Equal(new Vector2(400, 300), Invoke<Vector2>(transform, "WorldToViewport", Vector2.Zero));
    }

    private static object CreateTransform() => Activator.CreateInstance(GraphType("GraphViewportTransform"))!;

    private static Type GraphType(string name)
        => Type.GetType($"MGUI.Core.UI.Graph.{name}, MGUI.Core", throwOnError: true)!;

    private static T Get<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName)
            ?? throw new MissingMemberException(target.GetType().FullName, propertyName);
        return (T)property.GetValue(target)!;
    }

    private static void Set(object target, string propertyName, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName)
            ?? throw new MissingMemberException(target.GetType().FullName, propertyName);
        property.SetValue(target, value);
    }

    private static T Invoke<T>(object target, string methodName, params object[] args)
        => (T)Invoke(target, methodName, args)!;

    private static object? Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethods().Single(x => x.Name == methodName && x.GetParameters().Length == args.Length);
        return method.Invoke(target, args);
    }
}