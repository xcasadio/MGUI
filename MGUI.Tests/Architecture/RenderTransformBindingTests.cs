using System;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.DataBinding;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;
using XamlElement = MGUI.Core.UI.XAML.Element;
using XamlBorder = MGUI.Core.UI.XAML.Border;
using XamlRenderTransform = MGUI.Core.UI.XAML.RenderTransform;

namespace MGUI.Tests.Architecture;

/// <summary>ADR-0020 ("Bindable render transform", G9 of the CasaEngine XAML-screens audit) coverage:
/// <see cref="MGUI.Core.UI.XAML.Element.RenderTransformTranslation"/>/<see cref="MGUI.Core.UI.XAML.Element.RenderTransformScale"/> renamed by
/// <c>BindingPathMappings</c> onto <c>RenderTransform.Translation</c>/<c>RenderTransform.Scale</c>, resolved through
/// the same nested target-path walk ADR-0016 gave <c>Background</c>/<c>CanvasLeft</c>, landing on the element's
/// stable <see cref="MGElement.RenderTransform"/> instance with an allocation-free <c>PushStrategy.TypedCopy</c>
/// push, no layout invalidation (render-only transform, ADR-0006).</summary>
[Collection(DataBindingRegistryCollection.Name)]
public class RenderTransformBindingTests
{
    private const float Tolerance = 1e-4f;

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window)
    {
        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 24, 24, 480, 260)
            {
                WindowStyle = WindowStyle.None,
                Padding = new Thickness(0),
            };
            Harness harness = new(runtime, desktop, window);
            harness.Frame();
            return harness;
        }

        public void Frame()
        {
            MouseState mouse = new(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
        }
    }

    //  (1) A live XAML {MGBinding} on RenderTransformTranslation/RenderTransformScale pushes into RenderTransform,
    //  on both an Image and a Canvas.

    [Fact]
    public void XamlBinding_On_RenderTransformTranslation_And_Scale_Pushes_Into_RenderTransform_On_An_Image()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
        xmlns:dataBinding=""clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core""
        Width=""200"" Height=""150"" Padding=""0"">
    <Image Name=""Img"" Width=""24"" Height=""24""
           RenderTransformTranslation=""{dataBinding:MGBinding Path=Translation}""
           RenderTransformScale=""{dataBinding:MGBinding Path=Scale}"" />
</Window>";

        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        TransformViewModel vm = new() { Translation = new Vector2(4f, 0f), Scale = new Vector2(2f, 2f) };
        window.WindowDataContext = vm;
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();
        harness.Desktop.Update();

        MGImage image = window.GetElementByName<MGImage>("Img");
        Assert.Equal(new Vector2(4f, 0f), image.RenderTransform.Translation);
        Assert.Equal(new Vector2(2f, 2f), image.RenderTransform.Scale);

        vm.Translation = new Vector2(10f, 5f);
        vm.Scale = new Vector2(0.5f, 0.5f);
        harness.Desktop.Update();
        harness.Desktop.Update();

        Assert.Equal(new Vector2(10f, 5f), image.RenderTransform.Translation);
        Assert.Equal(new Vector2(0.5f, 0.5f), image.RenderTransform.Scale);

        harness.Desktop.Windows.Remove(window);
    }

    [Fact]
    public void XamlBinding_On_RenderTransformTranslation_And_Scale_Pushes_Into_RenderTransform_On_A_Canvas()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
        xmlns:dataBinding=""clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core""
        Width=""200"" Height=""150"" Padding=""0"">
    <Canvas Name=""C"" Width=""160"" Height=""120""
            RenderTransformTranslation=""{dataBinding:MGBinding Path=Translation}""
            RenderTransformScale=""{dataBinding:MGBinding Path=Scale}"" />
</Window>";

        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        TransformViewModel vm = new() { Translation = new Vector2(1f, 2f), Scale = new Vector2(3f, 3f) };
        window.WindowDataContext = vm;
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();
        harness.Desktop.Update();

        MGCanvas canvas = window.GetElementByName<MGCanvas>("C");
        Assert.Equal(new Vector2(1f, 2f), canvas.RenderTransform.Translation);
        Assert.Equal(new Vector2(3f, 3f), canvas.RenderTransform.Scale);

        harness.Desktop.Windows.Remove(window);
    }

    //  (2) The push strategy is TypedCopy (same reflection seam as DataBindingAllocationTests/ADR-0016), and it
    //  allocates nothing across 1000 pushes (AllocationWindow.Start(), matching commits 5e8ae82/7d830bb).

    private sealed class TransformViewModel : Shared.Helpers.ViewModelBase
    {
        private Vector2 _translation;
        public Vector2 Translation { get => _translation; set { _translation = value; NotifyPropertyChanged(); } }
        public void SetTranslationQuiet(Vector2 value) => _translation = value;

        private Vector2 _scale = Vector2.One;
        public Vector2 Scale { get => _scale; set { _scale = value; NotifyPropertyChanged(); } }
        public void SetScaleQuiet(Vector2 value) => _scale = value;
    }

    private static readonly MethodInfo PushSourceToTargetMethod =
        typeof(DataBinding).GetMethod("SourcePropertyValueChanged", BindingFlags.NonPublic | BindingFlags.Instance);

    private static Action GetPushInvoker(DataBinding binding)
        => (Action)PushSourceToTargetMethod.CreateDelegate(typeof(Action), binding);

    private static Action<object, object> GetTypedCopyDelegate(DataBinding binding)
        => (Action<object, object>)typeof(DataBinding).GetField("TypedCopyDelegate", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(binding);

    [Fact]
    public void Push_Translation_Uses_TypedCopy_Strategy()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);
        TransformViewModel vm = new();
        border.DataContextOverride = vm;

        DataBinding binding = DataBindingManager.AddBinding(
            new BindingConfig(XamlElement.MapBindingTargetPath(nameof(XamlElement.RenderTransformTranslation)), nameof(TransformViewModel.Translation)), border);

        Assert.NotNull(GetTypedCopyDelegate(binding));
    }

    [Fact]
    public void Push_Scale_Uses_TypedCopy_Strategy()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);
        TransformViewModel vm = new();
        border.DataContextOverride = vm;

        DataBinding binding = DataBindingManager.AddBinding(
            new BindingConfig(XamlElement.MapBindingTargetPath(nameof(XamlElement.RenderTransformScale)), nameof(TransformViewModel.Scale)), border);

        Assert.NotNull(GetTypedCopyDelegate(binding));
    }

    [Fact]
    public void Push_Translation_And_Scale_Allocate_Nothing_Across_1000_Pushes()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);
        TransformViewModel vm = new();
        border.DataContextOverride = vm;

        DataBinding translationBinding = DataBindingManager.AddBinding(
            new BindingConfig(XamlElement.MapBindingTargetPath(nameof(XamlElement.RenderTransformTranslation)), nameof(TransformViewModel.Translation)), border);
        DataBinding scaleBinding = DataBindingManager.AddBinding(
            new BindingConfig(XamlElement.MapBindingTargetPath(nameof(XamlElement.RenderTransformScale)), nameof(TransformViewModel.Scale)), border);
        Action pushTranslation = GetPushInvoker(translationBinding);
        Action pushScale = GetPushInvoker(scaleBinding);

        Vector2[] values = { new(1f, 2f), new(3f, 4f), Vector2.Zero, new(0.5f, 0.5f) };

        //  Warm-up (JIT tiering), matching DataBindingAllocationTests' MeasureAllocatedBytes.
        for (var i = 0; i < 5000; i++)
        {
            vm.SetTranslationQuiet(values[i % values.Length]);
            vm.SetScaleQuiet(values[(i + 1) % values.Length]);
            pushTranslation();
            pushScale();
        }

        long before = AllocationWindow.Start();
        for (var i = 0; i < 1000; i++)
        {
            vm.SetTranslationQuiet(values[i % values.Length]);
            vm.SetScaleQuiet(values[(i + 1) % values.Length]);
            pushTranslation();
            pushScale();
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.Equal(vm.Translation, border.RenderTransform.Translation);
        Assert.Equal(vm.Scale, border.RenderTransform.Scale);
    }

    //  (3) No layout invalidation from a push: RenderTransform is render-only (ADR-0006), so a push through the
    //  binding must not queue a layout refresh, exactly like a direct UIRenderTransform.Translation/.Scale set.

    [Fact]
    public void Push_Translation_And_Scale_Do_Not_Invalidate_Layout()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);
        TransformViewModel vm = new();
        border.DataContextOverride = vm;

        DataBinding translationBinding = DataBindingManager.AddBinding(
            new BindingConfig(XamlElement.MapBindingTargetPath(nameof(XamlElement.RenderTransformTranslation)), nameof(TransformViewModel.Translation)), border);
        DataBinding scaleBinding = DataBindingManager.AddBinding(
            new BindingConfig(XamlElement.MapBindingTargetPath(nameof(XamlElement.RenderTransformScale)), nameof(TransformViewModel.Scale)), border);
        Action pushTranslation = GetPushInvoker(translationBinding);
        Action pushScale = GetPushInvoker(scaleBinding);

        harness.Window.QueueLayoutRefresh = false;
        vm.SetTranslationQuiet(new Vector2(20f, 30f));
        vm.SetScaleQuiet(new Vector2(2f, 2f));
        pushTranslation();
        pushScale();

        Assert.False(harness.Window.QueueLayoutRefresh);
        Assert.Equal(new Vector2(20f, 30f), border.RenderTransform.Translation);
        Assert.Equal(new Vector2(2f, 2f), border.RenderTransform.Scale);
    }

    //  (4) Composition under a parent Canvas' integer RenderTransform.Scale (the case of Alundra's screens, whose
    //  root Canvas carries the whole window's scale): the child's pushed matrix is its own local matrix composed
    //  BEFORE the parent's, matching Draw_ComposesTheLocalMatrix_BeforeTheWindowScale's pattern.

    [Fact]
    public void Composition_UnderIntegerParentScale_MatchesMatrixComposition()
    {
        Scene scene = Scene.Build();
        scene.Canvas.RenderTransform.Scale = new Vector2(3f, 3f);
        scene.Child.RenderTransform.Translation = new Vector2(10f, 20f);
        scene.Child.RenderTransform.Scale = new Vector2(0.5f, 0.5f);
        scene.Frames(2, new Point(1, 1));

        GraphNoOpDrawTransaction transaction = scene.Draw();

        Assert.Equal(2, transaction.TransformPushes.Count);

        //  Expected matrices are built independently of UIRenderTransform.CreateMatrix (raw XNA composition around
        //  each element's own top-left corner, since Origin defaults to (0,0)), so a regression in CreateMatrix
        //  itself, not only in this feature's wiring, would still be caught here.
        Vector2 canvasPivot = new(scene.Canvas.LayoutBounds.Left, scene.Canvas.LayoutBounds.Top);
        Matrix canvasMatrix = Matrix.CreateTranslation(-canvasPivot.X, -canvasPivot.Y, 0f)
            * Matrix.CreateScale(3f, 3f, 1f)
            * Matrix.CreateTranslation(canvasPivot.X, canvasPivot.Y, 0f);

        Vector2 childPivot = new(scene.Child.LayoutBounds.Left, scene.Child.LayoutBounds.Top);
        Matrix childMatrix = Matrix.CreateTranslation(-childPivot.X, -childPivot.Y, 0f)
            * Matrix.CreateScale(0.5f, 0.5f, 1f)
            * Matrix.CreateTranslation(childPivot.X + 10f, childPivot.Y + 20f, 0f);

        AssertMatrix(canvasMatrix, transaction.TransformPushes[0]);
        AssertMatrix(childMatrix * canvasMatrix, transaction.TransformPushes[1]);
    }

    //  (5) A scale of 0 is allowed (no exception, no divide-by-zero downstream): the pushed local matrix collapses
    //  the element to a point at its own translated top-left corner.

    [Fact]
    public void Scale_Of_Zero_Is_Allowed()
    {
        Scene scene = Scene.Build();
        scene.Child.RenderTransform.Scale = Vector2.Zero;
        scene.Frames(2, new Point(1, 1));

        GraphNoOpDrawTransaction transaction = null;
        Exception thrown = Record.Exception(() => transaction = scene.Draw());
        Assert.Null(thrown);
        Assert.Equal(Vector2.Zero, scene.Child.RenderTransform.Scale);

        Matrix push = Assert.Single(transaction.TransformPushes);
        Vector2 topLeft = new(scene.Child.LayoutBounds.Left, scene.Child.LayoutBounds.Top);
        AssertPoint(topLeft, Vector2.Transform(topLeft, push));
        AssertPoint(topLeft, Vector2.Transform(topLeft + new Vector2(20f, 20f), push));
    }

    //  (6) The anchor is the top-left corner (default Origin): a translation-free scale leaves the top-left corner
    //  of the element's bounds fixed, matching CreateMatrix_ScalesAroundTheTopLeftCorner_ByDefault.

    [Fact]
    public void Anchor_Is_TheTopLeftCorner_ByDefault()
    {
        Scene scene = Scene.Build();
        scene.Child.RenderTransform.Scale = new Vector2(2f, 2f);
        scene.Frames(2, new Point(1, 1));

        GraphNoOpDrawTransaction transaction = scene.Draw();

        Matrix push = Assert.Single(transaction.TransformPushes);
        Vector2 topLeft = new(scene.Child.LayoutBounds.Left, scene.Child.LayoutBounds.Top);
        AssertPoint(topLeft, Vector2.Transform(topLeft, push));
        Assert.Equal(Vector2.Zero, scene.Child.RenderTransform.Origin);
    }

    //  (7) Literals of the new attributes, of the RenderTransform DTO, and of RenderScale give the same values as
    //  before (regression: adding the new attributes changed nothing about the existing ones).

    [Fact]
    public void Literal_RenderTransformTranslation_And_Scale_Apply_After_The_RenderTransform_Dto()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"" Padding=""0"">
    <Border Name=""B"" Width=""20"" Height=""10"" RenderTransformTranslation=""4,5"" RenderTransformScale=""2,3"">
        <Border.RenderTransform>
            <RenderTransform Translation=""1,1"" Rotation=""15"" />
        </Border.RenderTransform>
    </Border>
</Window>";

        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGBorder border = window.GetElementByName<MGBorder>("B");
        //  RenderTransformTranslation is applied AFTER the DTO, so it overrides the DTO's Translation="1,1";
        //  Rotation, absent from the new attributes, keeps the DTO's value.
        Assert.Equal(new Vector2(4f, 5f), border.RenderTransform.Translation);
        Assert.Equal(new Vector2(2f, 3f), border.RenderTransform.Scale);
        Assert.Equal(15f, border.RenderTransform.Rotation);

        harness.Desktop.Windows.Remove(window);
    }

    [Fact]
    public void Literal_RenderTransform_Dto_Still_Applies_Unchanged()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"" Padding=""0"">
    <Border Name=""B"" Width=""20"" Height=""10"">
        <Border.RenderTransform>
            <RenderTransform Translation=""4,0"" Scale=""1.05"" Origin=""0.5,0.5"" Rotation=""10"" />
        </Border.RenderTransform>
    </Border>
</Window>";

        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGBorder border = window.GetElementByName<MGBorder>("B");
        Assert.Equal(new Vector2(4f, 0f), border.RenderTransform.Translation);
        Assert.Equal(new Vector2(1.05f, 1.05f), border.RenderTransform.Scale);
        Assert.Equal(new Vector2(0.5f, 0.5f), border.RenderTransform.Origin);
        Assert.Equal(10f, border.RenderTransform.Rotation);

        harness.Desktop.Windows.Remove(window);
    }

    [Fact]
    public void Literal_RenderScale_Attribute_Still_Applies_Unchanged()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"" Padding=""0"">
    <Border Name=""B"" Width=""20"" Height=""10"" RenderScale=""1.5"" />
</Window>";

        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGBorder border = window.GetElementByName<MGBorder>("B");
        Assert.NotNull(border.RenderScale);

        harness.Desktop.Windows.Remove(window);
    }

    //  (8) An invalid literal on the new attributes fails the same way an invalid RenderTransform DTO literal does.

    [Fact]
    public void Invalid_Literal_On_RenderTransformTranslation_Throws_LikeTheDto()
    {
        XamlElement element = new XamlBorder();
        Exception fromNewAttribute = Record.Exception(() => element.RenderTransformTranslation = "not-a-vector");
        Assert.IsType<InvalidOperationException>(fromNewAttribute);

        XamlRenderTransform dto = new();
        Exception fromDto = Record.Exception(() => dto.Translation = "not-a-vector");
        Assert.IsType<InvalidOperationException>(fromDto);
    }

    [Fact]
    public void Invalid_Literal_On_RenderTransformScale_Throws_LikeTheDto()
    {
        XamlElement element = new XamlBorder();
        Exception fromNewAttribute = Record.Exception(() => element.RenderTransformScale = "nope");
        Assert.IsType<InvalidOperationException>(fromNewAttribute);

        XamlRenderTransform dto = new();
        Exception fromDto = Record.Exception(() => dto.Scale = "nope");
        Assert.IsType<InvalidOperationException>(fromDto);
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

    /// <summary>A 400x300 window holding a Canvas that positions one child Border, matching RenderTransformTests' Scene.</summary>
    private sealed class Scene
    {
        public GraphTestRuntime Runtime { get; private init; }
        public MGDesktop Desktop { get; private init; }
        public MGWindow Window { get; private init; }
        public MGCanvas Canvas { get; private init; }
        public MGBorder Child { get; private init; }
        private int _frame;

        public static Scene Build()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
            MGCanvas canvas = new(window) { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            MGBorder child = new(window) { PreferredWidth = 40, PreferredHeight = 40 };
            canvas.TryAddChild(child, 20, 20, null, null);
            window.SetContent(canvas);
            desktop.Windows.Add(window);

            return new Scene { Runtime = runtime, Desktop = desktop, Window = window, Canvas = canvas, Child = child };
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
