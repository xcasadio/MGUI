using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MGUI.Shared.Helpers;
using MonoGame.Extended;

namespace MGUI.Tests.Animation;

/// <summary>Slice S2 of Docs/Tasks/animation-tasks.md: <see cref="UIRenderTransform"/> matrices, their composition at draw time,
/// the inverse hit-test and the zero-cost path of elements without transform.</summary>
public class RenderTransformTests
{
    private const float Tolerance = 1e-4f;

    private static readonly Rectangle Bounds = new(10, 20, 100, 50);

    [Fact]
    public void CreateMatrix_TranslatesInLayoutPixels()
    {
        Matrix matrix = UIRenderTransform.CreateMatrix(Bounds, new Vector2(5f, 7f), Vector2.One, 0f, Vector2.Zero);

        AssertPoint(new Vector2(15f, 27f), Vector2.Transform(new Vector2(10f, 20f), matrix));
        AssertPoint(new Vector2(115f, 77f), Vector2.Transform(new Vector2(110f, 70f), matrix));
    }

    [Fact]
    public void CreateMatrix_ScalesAroundTheTopLeftCorner_ByDefault()
    {
        Matrix matrix = UIRenderTransform.CreateMatrix(Bounds, Vector2.Zero, new Vector2(2f, 2f), 0f, Vector2.Zero);

        AssertPoint(new Vector2(10f, 20f), Vector2.Transform(new Vector2(10f, 20f), matrix));
        AssertPoint(new Vector2(210f, 120f), Vector2.Transform(new Vector2(110f, 70f), matrix));
    }

    [Fact]
    public void CreateMatrix_ScalesAroundTheCentre_WithHalfOrigin()
    {
        Matrix matrix = UIRenderTransform.CreateMatrix(Bounds, Vector2.Zero, new Vector2(2f, 2f), 0f, new Vector2(0.5f, 0.5f));

        AssertPoint(new Vector2(60f, 45f), Vector2.Transform(new Vector2(60f, 45f), matrix));
        AssertPoint(new Vector2(-40f, -5f), Vector2.Transform(new Vector2(10f, 20f), matrix));
    }

    [Fact]
    public void CreateMatrix_RotatesClockwise_InDegrees()
    {
        Matrix matrix = UIRenderTransform.CreateMatrix(Bounds, Vector2.Zero, Vector2.One, 90f, new Vector2(0.5f, 0.5f));

        // A quarter turn clockwise (y down) around the centre (60, 45): the top-right corner lands at the bottom-right side.
        AssertPoint(new Vector2(85f, 95f), Vector2.Transform(new Vector2(110f, 20f), matrix));
        AssertPoint(new Vector2(85f, -5f), Vector2.Transform(new Vector2(10f, 20f), matrix));
        AssertPoint(new Vector2(60f, 45f), Vector2.Transform(new Vector2(60f, 45f), matrix));
    }

    [Fact]
    public void CreateMatrix_AppliesScaleThenRotationThenTranslation()
    {
        Matrix matrix = UIRenderTransform.CreateMatrix(Bounds, new Vector2(100f, 0f), new Vector2(2f, 2f), 90f, Vector2.Zero);

        // (110, 20) - pivot (10, 20) = (100, 0) -> scaled (200, 0) -> rotated (0, 200) -> + pivot + translation = (110, 220).
        AssertPoint(new Vector2(110f, 220f), Vector2.Transform(new Vector2(110f, 20f), matrix));
    }

    [Fact]
    public void CreateCenteredScale_KeepsTheCentre()
    {
        Matrix matrix = UIRenderTransform.CreateCenteredScale(new Rectangle(0, 0, 100, 50), 2f);

        AssertPoint(new Vector2(50f, 25f), Vector2.Transform(new Vector2(50f, 25f), matrix));
        AssertPoint(new Vector2(-50f, -25f), Vector2.Transform(Vector2.Zero, matrix));
    }

    [Fact]
    public void IsIdentity_TracksTheComponents_AndIgnoresTheOrigin()
    {
        UIRenderTransform transform = new();
        Assert.True(transform.IsIdentity);

        transform.Origin = new Vector2(0.5f, 0.5f);
        Assert.True(transform.IsIdentity);

        transform.Translation = new Vector2(1f, 0f);
        Assert.False(transform.IsIdentity);
        transform.Translation = Vector2.Zero;
        Assert.True(transform.IsIdentity);

        transform.Scale = new Vector2(1.05f, 1.05f);
        Assert.False(transform.IsIdentity);
        transform.Rotation = 10f;
        transform.Reset();
        Assert.True(transform.IsIdentity);
        Assert.Equal(Vector2.Zero, transform.Origin);
    }

    [Fact]
    public void Setters_RaisePropertyChanged_OncePerChange()
    {
        UIRenderTransform transform = new();
        List<string> raised = new();
        transform.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        transform.Rotation = 45f;
        transform.Rotation = 45f;
        transform.Scale = new Vector2(2f, 2f);

        Assert.Equal(new[] { nameof(UIRenderTransform.Rotation), nameof(UIRenderTransform.Scale) }, raised);
    }

    [Fact]
    public void Draw_PushesNoTransform_ForElementsWithoutTransform()
    {
        Scene scene = Scene.Build();
        scene.Frames(2, new Point(1, 1));

        GraphNoOpDrawTransaction transaction = scene.Draw();

        Assert.Empty(transaction.TransformPushes);
        Assert.Equal(0, scene.Desktop.ActiveRenderTransformCount);
        Assert.Null(RenderTransformField(scene.Top));
        Assert.Null(RenderTransformField(scene.Bottom));
        Assert.Null(RenderTransformField(scene.Panel));
    }

    [Fact]
    public void Draw_PushesTheLocalMatrixOnce_ComposedBeforeTheCurrentTransform()
    {
        Scene scene = Scene.Build();
        scene.Frames(2, new Point(1, 1));
        scene.Top.RenderTransform.Translation = new Vector2(40f, 0f);

        GraphNoOpDrawTransaction transaction = scene.Draw();

        Matrix expected = UIRenderTransform.CreateMatrix(scene.Top.LayoutBounds, new Vector2(40f, 0f), Vector2.One, 0f, Vector2.Zero) * Matrix.Identity;
        Matrix push = Assert.Single(transaction.TransformPushes);
        AssertMatrix(expected, push);
        Assert.Equal(1, scene.Desktop.ActiveRenderTransformCount);
    }

    [Fact]
    public void Draw_RenderScale_KeepsScalingFromTheCentre_WhenHovered()
    {
        Scene scene = Scene.Build();
        scene.Top.RenderScale = new ConditionalScaleTransform(0.96f, 1.05f);
        scene.Frames(2, new Point(1, 1));
        scene.Frames(2, scene.Top.LayoutBounds.Center);
        Assert.True(scene.Top.VisualState.IsHovered);

        GraphNoOpDrawTransaction transaction = scene.Draw();

        Matrix push = Assert.Single(transaction.TransformPushes);
        AssertMatrix(UIRenderTransform.CreateCenteredScale(scene.Top.LayoutBounds, 1.05f), push);
    }

    [Fact]
    public void Draw_RenderScale_PushesNothing_WhenTheStateScaleIsOne()
    {
        Scene scene = Scene.Build();
        scene.Top.RenderScale = new ConditionalScaleTransform(1f, 1f);
        scene.Frames(2, new Point(1, 1));
        scene.Frames(2, scene.Top.LayoutBounds.Center);
        Assert.True(scene.Top.VisualState.IsHovered);

        GraphNoOpDrawTransaction transaction = scene.Draw();

        Assert.Empty(transaction.TransformPushes);
    }

    [Fact]
    public void Draw_ComposesTheStateScale_ThenTheRenderTransform()
    {
        Scene scene = Scene.Build();
        scene.Top.RenderScale = new ConditionalScaleTransform(0.96f, 1.05f);
        scene.Top.RenderTransform.Translation = new Vector2(0f, 10f);
        scene.Frames(2, new Point(1, 1));
        scene.Frames(2, scene.Top.LayoutBounds.Center);

        GraphNoOpDrawTransaction transaction = scene.Draw();

        Matrix expected = UIRenderTransform.CreateCenteredScale(scene.Top.LayoutBounds, 1.05f) * scene.Top.RenderTransform.ToMatrix(scene.Top.LayoutBounds);
        Matrix push = Assert.Single(transaction.TransformPushes);
        AssertMatrix(expected, push);
    }

    [Fact]
    public void HitTest_FollowsTheTranslation()
    {
        Scene scene = Scene.Build();
        scene.Frames(2, new Point(1, 1));
        Point original = scene.Top.LayoutBounds.Center;
        scene.Frames(2, original);
        Assert.Same(scene.Top, scene.Window.HoveredElement);

        scene.Top.RenderTransform.Translation = new Vector2(150f, 0f);
        scene.Frames(2, original);
        Assert.False(scene.Top.IsHovered);
        Assert.NotSame(scene.Top, scene.Window.HoveredElement);

        Point translated = original + new Point(150, 0);
        scene.Frames(2, translated);
        Assert.True(scene.Top.IsHovered);
        Assert.Same(scene.Top, scene.Window.HoveredElement);
    }

    [Fact]
    public void HitTest_FollowsTheParentRotation()
    {
        Scene scene = Scene.Build();
        scene.Frames(2, new Point(1, 1));
        Point topCentre = scene.Top.LayoutBounds.Center;
        Point bottomCentre = scene.Bottom.LayoutBounds.Center;
        Assert.Equal(scene.Top.LayoutBounds.Height, scene.Bottom.LayoutBounds.Height);

        // A half turn of the panel around its centre swaps the two equal buttons.
        scene.Panel.RenderTransform.Origin = new Vector2(0.5f, 0.5f);
        scene.Panel.RenderTransform.Rotation = 180f;
        scene.Frames(2, topCentre);
        Assert.Same(scene.Bottom, scene.Window.HoveredElement);
        Assert.True(scene.Bottom.IsHovered);
        Assert.False(scene.Top.IsHovered);

        scene.Frames(2, bottomCentre);
        Assert.Same(scene.Top, scene.Window.HoveredElement);
    }

    [Fact]
    public void HitTest_FollowsTheStateScale()
    {
        Scene scene = Scene.Build();
        scene.Top.RenderScale = new ConditionalScaleTransform(1f, 2f);
        scene.Frames(2, new Point(1, 1));
        Rectangle bounds = scene.Top.LayoutBounds;
        Point justOutside = new(bounds.Right + bounds.Width / 4, bounds.Center.Y);

        // Not hovered: the state scale is one, the point lies outside the layout bounds.
        scene.Frames(2, justOutside);
        Assert.False(scene.Top.IsHovered);

        // Hovered at the centre, the element doubles around its centre and now covers the same point.
        scene.Frames(2, bounds.Center);
        Assert.True(scene.Top.VisualState.IsHovered);
        scene.Frames(2, justOutside);
        Assert.True(scene.Top.IsHovered);
    }

    [Fact]
    public void ActiveRenderTransformCount_TracksTransformsAndRenderScale()
    {
        Scene scene = Scene.Build();
        Assert.Equal(0, scene.Desktop.ActiveRenderTransformCount);

        scene.Top.RenderTransform.Origin = new Vector2(0.5f, 0.5f);
        Assert.Equal(0, scene.Desktop.ActiveRenderTransformCount);

        scene.Top.RenderTransform.Scale = new Vector2(1.5f, 1.5f);
        Assert.Equal(1, scene.Desktop.ActiveRenderTransformCount);
        Assert.True(scene.Top.HasActiveRenderTransform);

        scene.Bottom.RenderScale = new ConditionalScaleTransform(0.96f, 1.05f);
        Assert.Equal(2, scene.Desktop.ActiveRenderTransformCount);

        scene.Top.RenderTransform.Reset();
        Assert.Equal(1, scene.Desktop.ActiveRenderTransformCount);
        Assert.False(scene.Top.HasActiveRenderTransform);

        scene.Bottom.RenderScale = null;
        Assert.Equal(0, scene.Desktop.ActiveRenderTransformCount);
    }

    [Fact]
    public void Window_IgnoresItsRenderTransform()
    {
        Scene scene = Scene.Build();
        scene.Frames(2, new Point(1, 1));

        scene.Window.RenderTransform.Translation = new Vector2(10f, 10f);

        Assert.False(scene.Window.HasActiveRenderTransform);
        Assert.Equal(0, scene.Desktop.ActiveRenderTransformCount);
        Assert.Empty(scene.Draw().TransformPushes);
    }

    [Fact]
    public void TransformedBounds_StayValid_UnderRotation()
    {
        Matrix quarterTurn = UIRenderTransform.CreateMatrix(Bounds, Vector2.Zero, Vector2.One, 90f, new Vector2(0.5f, 0.5f));

        RectangleF bounds = Bounds.CreateTransformedBoundsF(quarterTurn);

        Assert.Equal(35f, bounds.Left, Tolerance);
        Assert.Equal(-5f, bounds.Top, Tolerance);
        Assert.Equal(50f, bounds.Width, Tolerance);
        Assert.Equal(100f, bounds.Height, Tolerance);

        RectangleF halfTurn = Bounds.CreateTransformedBoundsF(UIRenderTransform.CreateMatrix(Bounds, Vector2.Zero, Vector2.One, 180f, new Vector2(0.5f, 0.5f)));
        Assert.Equal(Bounds.X, halfTurn.Left, Tolerance);
        Assert.Equal(Bounds.Width, halfTurn.Width, Tolerance);
    }

    [Fact]
    public void Draw_RotatedElement_IsStillDrawn_AndHoverable()
    {
        Scene scene = Scene.Build();
        scene.Frames(2, new Point(1, 1));
        scene.Panel.RenderTransform.Origin = new Vector2(0.5f, 0.5f);
        scene.Panel.RenderTransform.Rotation = 180f;

        GraphNoOpDrawTransaction transaction = scene.Draw();

        Assert.Single(transaction.TransformPushes);
        Assert.False(scene.Panel.RecentDrawWasClipped);
        Assert.False(scene.Top.RecentDrawWasClipped);
        Assert.False(scene.Bottom.RecentDrawWasClipped);

        scene.Frames(2, scene.Top.LayoutBounds.Center);
        Assert.Same(scene.Bottom, scene.Window.HoveredElement);
    }

    [Fact]
    public void Draw_ComposesTheLocalMatrix_BeforeTheWindowScale()
    {
        Scene scene = Scene.Build();
        scene.Window.Scale = 2f;
        scene.Frames(2, new Point(1, 1));
        scene.Top.RenderTransform.Translation = new Vector2(40f, 0f);

        GraphNoOpDrawTransaction transaction = scene.Draw();

        Assert.Equal(2, transaction.TransformPushes.Count);
        Matrix windowScale = scene.Window.UnscaledScreenSpaceToScaledScreenSpace;
        AssertMatrix(windowScale, transaction.TransformPushes[0]);
        Matrix local = UIRenderTransform.CreateMatrix(scene.Top.LayoutBounds, new Vector2(40f, 0f), Vector2.One, 0f, Vector2.Zero);
        AssertMatrix(local * windowScale, transaction.TransformPushes[1]);
    }

    private static void AssertPoint(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
    }

    private static void AssertMatrix(Matrix expected, Matrix actual)
    {
        Assert.Equal(expected.M11, actual.M11, Tolerance);
        Assert.Equal(expected.M12, actual.M12, Tolerance);
        Assert.Equal(expected.M21, actual.M21, Tolerance);
        Assert.Equal(expected.M22, actual.M22, Tolerance);
        Assert.Equal(expected.M41, actual.M41, Tolerance);
        Assert.Equal(expected.M42, actual.M42, Tolerance);
    }

    private static UIRenderTransform RenderTransformField(MGElement element)
        => (UIRenderTransform)typeof(MGElement).GetField("_RenderTransform", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(element);

    /// <summary>A 400x300 window holding a content-sized vertical panel of two equal buttons.</summary>
    private sealed class Scene
    {
        public GraphTestRuntime Runtime { get; private init; }
        public MGDesktop Desktop { get; private init; }
        public MGWindow Window { get; private init; }
        public MGStackPanel Panel { get; private init; }
        public MGButton Top { get; private init; }
        public MGButton Bottom { get; private init; }
        private int _frame;

        public static Scene Build()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
            MGButton top = new(window) { PreferredWidth = 120, PreferredHeight = 40 };
            MGButton bottom = new(window) { PreferredWidth = 120, PreferredHeight = 40 };
            MGStackPanel panel = new(window, Orientation.Vertical)
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 0,
            };
            panel.TryAddChild(top);
            panel.TryAddChild(bottom);
            window.SetContent(panel);
            desktop.Windows.Add(window);

            return new Scene { Runtime = runtime, Desktop = desktop, Window = window, Panel = panel, Top = top, Bottom = bottom };
        }

        public void Frames(int count, Point mouse)
        {
            for (int i = 0; i < count; i++)
            {
                _frame++;
                MouseState state = new(mouse.X, mouse.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * _frame), TimeSpan.FromMilliseconds(16), state, new KeyboardState()));
                Desktop.Update();
            }
        }

        public GraphNoOpDrawTransaction Draw()
        {
            GraphNoOpDrawTransaction transaction = new(Runtime, DrawSettings.Default);
            Desktop.Draw(transaction);
            return transaction;
        }
    }
}
