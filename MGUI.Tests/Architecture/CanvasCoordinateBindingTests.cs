using System;
using System.ComponentModel;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.DataBinding;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Architecture;

/// <summary>ADR-0016 decision D7 ("Bindable canvas coordinates") coverage: since T1.2, the four coordinates a parent
/// <see cref="MGCanvas"/> positions a child by live in typed fields on <see cref="MGElement"/> itself
/// (<see cref="MGElement.CanvasLeft"/>/<see cref="MGElement.CanvasTop"/>/<see cref="MGElement.CanvasRight"/>/
/// <see cref="MGElement.CanvasBottom"/>) instead of <see cref="MGElement.Metadata"/>, so an <c>{MGBinding}</c>
/// written on the <c>CanvasLeft</c>/<c>CanvasTop</c> XAML attribute reaches those properties directly (no
/// <c>BindingPathMappings</c> entry needed, since the XAML DTO and runtime property share the same name) and moves
/// the child live, through the same compiled, allocation-free typed accessors T1.1 gave every other OneWay push.</summary>
[Collection(DataBindingRegistryCollection.Name)]
public class CanvasCoordinateBindingTests
{
    private sealed class PositionViewModel : INotifyPropertyChanged
    {
        private int? _x;
        public int? X
        {
            get => _x;
            set { if (_x != value) { _x = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(X))); } }
        }
        public void SetXQuiet(int? value) => _x = value;

        private int? _y;
        public int? Y
        {
            get => _y;
            set { if (_y != value) { _y = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Y))); } }
        }
        public void SetYQuiet(int? value) => _y = value;

        public event PropertyChangedEventHandler PropertyChanged;
    }

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

    //  (1) A live XAML {MGBinding} on CanvasLeft/CanvasTop moves the child when the view model changes.

    [Fact]
    public void XamlBinding_On_CanvasLeft_And_CanvasTop_Moves_Child_Live()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
        xmlns:dataBinding=""clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core""
        Width=""200"" Height=""150"" Padding=""0"">
    <Canvas Name=""C"" Width=""160"" Height=""120"">
        <Rectangle Name=""R"" Width=""20"" Height=""10"" CanvasLeft=""{dataBinding:MGBinding Path=X}"" CanvasTop=""{dataBinding:MGBinding Path=Y}"" />
    </Canvas>
</Window>";

        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        PositionViewModel vm = new() { X = 10, Y = 20 };
        window.WindowDataContext = vm; // set AFTER loading, matching MGUI's own samples (Window.WindowDataContext = this, set AFTER LoadRootWindow)
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();
        harness.Desktop.Update(); // MGUI's layout engine settles over two passes (matches the rest of this test suite)

        MGRectangle rect = window.GetElementByName<MGRectangle>("R");
        Assert.Equal(10, rect.CanvasLeft);
        Assert.Equal(20, rect.CanvasTop);
        Rectangle before = rect.ActualLayoutBounds;

        vm.X = 50;
        vm.Y = 70;
        harness.Desktop.Update();
        harness.Desktop.Update();

        Assert.Equal(50, rect.CanvasLeft);
        Assert.Equal(70, rect.CanvasTop);
        Rectangle after = rect.ActualLayoutBounds;
        Assert.Equal(before.Left + 40, after.Left);
        Assert.Equal(before.Top + 50, after.Top);

        harness.Desktop.Windows.Remove(window);
    }

    //  (2) A literal CanvasLeft="12" still positions the child, and MGCanvas.GetLeft/SetLeft round-trip through the
    //  same typed property MGElement.CanvasLeft holds.

    [Fact]
    public void Literal_CanvasLeft_Still_Positions_Child_And_Roundtrips_With_MGCanvas_GetLeft()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"" Padding=""0"">
    <Canvas Name=""C"" Width=""160"" Height=""120"">
        <Rectangle Name=""R"" Width=""20"" Height=""10"" CanvasLeft=""12"" CanvasTop=""16"" />
    </Canvas>
</Window>";

        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();
        harness.Desktop.Update();

        MGRectangle rect = window.GetElementByName<MGRectangle>("R");
        MGCanvas canvas = window.GetElementByName<MGCanvas>("C");

        Assert.Equal(12, rect.CanvasLeft);
        Assert.Equal(16, rect.CanvasTop);
        Assert.Equal(12, MGCanvas.GetLeft(rect));
        Assert.Equal(16, MGCanvas.GetTop(rect));

        Rectangle canvasBounds = canvas.ActualLayoutBounds;
        Rectangle rectBounds = rect.ActualLayoutBounds;
        Assert.Equal(canvasBounds.Left + 12, rectBounds.Left);
        Assert.Equal(canvasBounds.Top + 16, rectBounds.Top);

        MGCanvas.SetLeft(rect, 40);
        Assert.Equal(40, rect.CanvasLeft);
        Assert.Equal(40, MGCanvas.GetLeft(rect));

        rect.CanvasTop = 55;
        Assert.Equal(55, MGCanvas.GetTop(rect));

        harness.Desktop.Windows.Remove(window);
    }

    //  (3) Zero-allocation coverage, matching T1.1's DataBindingAllocationTests.cs methodology.

    private const int WarmupIterations = 5000;
    private const int MeasuredIterations = 1000;

    private static readonly MethodInfo PushSourceToTargetMethod =
        typeof(DataBinding).GetMethod("SourcePropertyValueChanged", BindingFlags.NonPublic | BindingFlags.Instance);

    private static Action GetPushInvoker(DataBinding Binding)
        => (Action)PushSourceToTargetMethod.CreateDelegate(typeof(Action), Binding);

    private static long MeasureAllocatedBytes(Action Change)
    {
        for (var i = 0; i < WarmupIterations; i++)
        {
            Change();
        }

        long First = MeasureOnce(Change);
        long Second = MeasureOnce(Change);
        return Math.Min(First, Second);
    }

    private static long MeasureOnce(Action Change)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long Before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < MeasuredIterations; i++)
        {
            Change();
        }
        return GC.GetAllocatedBytesForCurrentThread() - Before;
    }

    [Fact]
    public void Push_Int_Source_Into_CanvasLeft_Allocates_Nothing()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);
        PositionViewModel vm = new();
        border.DataContextOverride = vm;

        DataBinding binding = DataBindingManager.AddBinding(new BindingConfig(nameof(MGElement.CanvasLeft), nameof(PositionViewModel.X)), border);
        Action push = GetPushInvoker(binding);

        int?[] values = { 10, 20, null, 30 };
        var i = 0;
        long bytes = MeasureAllocatedBytes(() => { vm.SetXQuiet(values[i++ % values.Length]); push(); });
        Assert.Equal(0, bytes);
        Assert.Equal(vm.X, border.CanvasLeft);
    }

    [Fact]
    public void Setting_CanvasLeft_Directly_Allocates_Nothing()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);

        int?[] values = { 1, 2, 3, null, 4 };
        var i = 0;
        long bytes = MeasureAllocatedBytes(() => { border.CanvasLeft = values[i++ % values.Length]; });
        Assert.Equal(0, bytes);
    }
}
