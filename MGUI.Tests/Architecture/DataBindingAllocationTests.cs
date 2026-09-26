using System;
using System.Collections;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.DataBinding;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Architecture;

/// <summary>ADR-0016 coverage: a binding push reads the source and writes the target through compiled, cached,
/// allocation-free accessors instead of <see cref="System.Reflection.PropertyInfo.GetValue(object)"/>/<see
/// cref="System.Reflection.PropertyInfo.SetValue(object, object)"/>, except when a converter, a string format or a
/// type conversion is involved.<para/>
/// The zero-allocation tests below drive <see cref="DataBinding"/>'s private <c>SourcePropertyValueChanged</c>
/// (i.e. the push itself, <see cref="PushSourceToTargetMethod"/>'s target) directly instead of raising
/// <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> on the view model. This codebase
/// builds with <c>UseWPF=true</c> (see <c>MGUI.Core.csproj</c>/<c>MGUI.Tests.csproj</c>), so a live OneWay update is
/// actually delivered through <see cref="System.Windows.Data.PropertyChangedEventManager"/>'s weak-event dispatch --
/// a real, ~192-byte-per-notification allocation confirmed by an isolated probe with no <see cref="DataBinding"/>
/// involved at all, present before and unrelated to this task. That cost belongs to the WPF weak-event plumbing this
/// binding subscribes through, not to the reflection/boxing push path ADR-0016 targets, so it would swamp the very
/// thing these tests exist to measure if left in the loop. The view model's "quiet" setters below mutate the
/// backing field without notifying, so the measured loop exercises only the push.</summary>
[Collection(DataBindingRegistryCollection.Name)]
public class DataBindingAllocationTests
{
    private const int WarmupIterations = 5000;
    private const int MeasuredIterations = 1000;

    private static readonly MethodInfo PushSourceToTargetMethod =
        typeof(DataBinding).GetMethod("SourcePropertyValueChanged", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>Returns a delegate bound to <paramref name="Binding"/>'s private push method, built once (outside
    /// any measured loop) via reflection.</summary>
    private static Action GetPushInvoker(DataBinding Binding)
        => (Action)PushSourceToTargetMethod.CreateDelegate(typeof(Action), Binding);

    /// <summary>Warms up the JIT well past tiering, then measures the bytes allocated on THIS thread by
    /// <paramref name="Change"/> over <see cref="MeasuredIterations"/> calls. Measures twice and keeps the smaller
    /// of the two: a flaky first measurement (tiering still in progress) would only ever show MORE allocations,
    /// never fewer, so keeping the smaller of two measurements cannot hide a real per-push allocation.</summary>
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

    private readonly record struct SamplePoint(int X, int Y);

    /// <summary>A view model whose properties cover every non-pilot CLR shape under test, plus the two pilot shapes
    /// (<see cref="Thickness"/> for <c>Margin</c>, <c>int?</c> for <c>MinHeight</c>). Derives from <see
    /// cref="ViewModelBase"/> so a notifying setter allocates nothing (cached <see
    /// cref="System.ComponentModel.PropertyChangedEventArgs"/> per property name) -- used by the non-allocation
    /// correctness tests below, which rely on a live OneWay update. Each property also has a "Quiet" setter that
    /// mutates the backing field without notifying, used only by the zero-allocation tests (see the class remarks).</summary>
    private sealed class AllocationViewModel : ViewModelBase
    {
        private string _Text;
        public string Text { get => _Text; set { _Text = value; NotifyPropertyChanged(); } }
        public void SetTextQuiet(string Value) => _Text = Value;

        private bool _Flag;
        public bool Flag { get => _Flag; set { _Flag = value; NotifyPropertyChanged(); } }
        public void SetFlagQuiet(bool Value) => _Flag = Value;

        private int _Count;
        public int Count { get => _Count; set { _Count = value; NotifyPropertyChanged(); } }
        public void SetCountQuiet(int Value) => _Count = Value;

        private float _Ratio;
        public float Ratio { get => _Ratio; set { _Ratio = value; NotifyPropertyChanged(); } }
        public void SetRatioQuiet(float Value) => _Ratio = Value;

        private Visibility _VisibilityValue;
        public Visibility VisibilityValue { get => _VisibilityValue; set { _VisibilityValue = value; NotifyPropertyChanged(); } }
        public void SetVisibilityValueQuiet(Visibility Value) => _VisibilityValue = Value;

        private SamplePoint _Point;
        public SamplePoint Point { get => _Point; set { _Point = value; NotifyPropertyChanged(); } }
        public void SetPointQuiet(SamplePoint Value) => _Point = Value;

        private Thickness _Margin;
        public Thickness Margin { get => _Margin; set { _Margin = value; NotifyPropertyChanged(); } }
        public void SetMarginQuiet(Thickness Value) => _Margin = Value;

        private int? _MinHeightValue;
        public int? MinHeightValue { get => _MinHeightValue; set { _MinHeightValue = value; NotifyPropertyChanged(); } }
        public void SetMinHeightValueQuiet(int? Value) => _MinHeightValue = Value;
    }

    /// <summary>A second, unrelated view model type used by the "data context replaced by a different type" test:
    /// its <see cref="Count"/> is a different CLR type (<see cref="long"/>) than <see cref="AllocationViewModel.Count"/>
    /// (<see cref="int"/>).</summary>
    private sealed class OtherViewModel : ViewModelBase
    {
        private long _Count;
        public long Count { get => _Count; set { _Count = value; NotifyPropertyChanged(); } }
    }

    /// <summary>A plain, non-MGElement POCO target for the non-pilot shapes. Its own "DataContext" property is read
    /// reflectively by <see cref="DataBinding"/>'s default <see cref="DataContextResolver.DataContext"/> resolver
    /// (the fallback branch in <see cref="DataBinding"/>'s constructor, used when the resolved source root doesn't
    /// implement <see cref="IObservableDataContext"/>) -- exactly the shape a plain view -&gt; POCO binding has,
    /// without needing an <see cref="MGElement"/> at all.</summary>
    private sealed class PlainTarget
    {
        public object DataContext { get; set; }
        public string Text { get; set; }
        public bool Flag { get; set; }
        public int Count { get; set; }
        public float Ratio { get; set; }
        public Visibility VisibilityValue { get; set; }
        public SamplePoint Point { get; set; }
    }

    private static PlainTarget BindPlainTarget(AllocationViewModel Vm, string PropertyName, out DataBinding Binding)
    {
        PlainTarget Target = new() { DataContext = Vm };
        Binding = DataBindingManager.AddBinding(new BindingConfig(PropertyName, PropertyName), Target);
        return Target;
    }

    //  (1) Zero allocation for every non-pilot target CLR shape under test: string, bool, int, float, an enum
    //  (Visibility), and a non-pilot struct.

    [Fact]
    public void Push_String_Allocates_Nothing()
    {
        AllocationViewModel vm = new();
        PlainTarget target = BindPlainTarget(vm, nameof(AllocationViewModel.Text), out DataBinding binding);
        Action push = GetPushInvoker(binding);
        string[] values = { "a", "bb", "ccc", "dddd" };
        var i = 0;
        long bytes = MeasureAllocatedBytes(() => { vm.SetTextQuiet(values[i++ % values.Length]); push(); });
        Assert.Equal(0, bytes);
        Assert.Equal(vm.Text, target.Text);
    }

    [Fact]
    public void Push_Bool_Allocates_Nothing()
    {
        AllocationViewModel vm = new();
        PlainTarget target = BindPlainTarget(vm, nameof(AllocationViewModel.Flag), out DataBinding binding);
        Action push = GetPushInvoker(binding);
        var i = 0;
        long bytes = MeasureAllocatedBytes(() => { vm.SetFlagQuiet((i++ & 1) == 0); push(); });
        Assert.Equal(0, bytes);
        Assert.Equal(vm.Flag, target.Flag);
    }

    [Fact]
    public void Push_Int_Allocates_Nothing()
    {
        AllocationViewModel vm = new();
        PlainTarget target = BindPlainTarget(vm, nameof(AllocationViewModel.Count), out DataBinding binding);
        Action push = GetPushInvoker(binding);
        var i = 0;
        long bytes = MeasureAllocatedBytes(() => { vm.SetCountQuiet(i++); push(); });
        Assert.Equal(0, bytes);
        Assert.Equal(vm.Count, target.Count);
    }

    [Fact]
    public void Push_Float_Allocates_Nothing()
    {
        AllocationViewModel vm = new();
        PlainTarget target = BindPlainTarget(vm, nameof(AllocationViewModel.Ratio), out DataBinding binding);
        Action push = GetPushInvoker(binding);
        var i = 0;
        long bytes = MeasureAllocatedBytes(() => { vm.SetRatioQuiet((i++ % 100) * 0.5f); push(); });
        Assert.Equal(0, bytes);
        Assert.Equal(vm.Ratio, target.Ratio);
    }

    [Fact]
    public void Push_Enum_Allocates_Nothing()
    {
        AllocationViewModel vm = new();
        PlainTarget target = BindPlainTarget(vm, nameof(AllocationViewModel.VisibilityValue), out DataBinding binding);
        Action push = GetPushInvoker(binding);
        Visibility[] values = { Visibility.Visible, Visibility.Hidden, Visibility.Collapsed };
        var i = 0;
        long bytes = MeasureAllocatedBytes(() => { vm.SetVisibilityValueQuiet(values[i++ % values.Length]); push(); });
        Assert.Equal(0, bytes);
        Assert.Equal(vm.VisibilityValue, target.VisibilityValue);
    }

    [Fact]
    public void Push_NonPilotStruct_Allocates_Nothing()
    {
        AllocationViewModel vm = new();
        PlainTarget target = BindPlainTarget(vm, nameof(AllocationViewModel.Point), out DataBinding binding);
        Action push = GetPushInvoker(binding);
        var i = 0;
        long bytes = MeasureAllocatedBytes(() => { vm.SetPointQuiet(new SamplePoint(i++, -i)); push(); });
        Assert.Equal(0, bytes);
        Assert.Equal(vm.Point, target.Point);
    }

    //  (1, continued) Zero allocation for the two pilot shapes: Margin<-Thickness and MinHeight<-int?.

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

    [Fact]
    public void Push_Pilot_Margin_Allocates_Nothing()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);
        AllocationViewModel vm = new();
        border.DataContextOverride = vm;

        DataBinding binding = DataBindingManager.AddBinding(new BindingConfig("Margin", nameof(AllocationViewModel.Margin)), border);
        Action push = GetPushInvoker(binding);

        Thickness[] values = { new(1), new(2, 3), new(4, 5, 6, 7), new(0) };
        var i = 0;
        long bytes = MeasureAllocatedBytes(() => { vm.SetMarginQuiet(values[i++ % values.Length]); push(); });
        Assert.Equal(0, bytes);
        Assert.Equal(vm.Margin, border.Margin);
    }

    [Fact]
    public void Push_Pilot_MinHeight_Allocates_Nothing()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);
        AllocationViewModel vm = new();
        border.DataContextOverride = vm;

        DataBinding binding = DataBindingManager.AddBinding(new BindingConfig("MinHeight", nameof(AllocationViewModel.MinHeightValue)), border);
        Action push = GetPushInvoker(binding);

        int?[] values = { 10, 20, null, 30 };
        var i = 0;
        long bytes = MeasureAllocatedBytes(() => { vm.SetMinHeightValueQuiet(values[i++ % values.Length]); push(); });
        Assert.Equal(0, bytes);
        Assert.Equal(vm.MinHeightValue, border.MinHeight);
    }

    //  (2) Same zero-allocation check with the data context assigned AFTER the binding is built, on a non-pilot
    //  target and on Margin, matching MGUI's own samples (Window.WindowDataContext = this, set AFTER LoadRootWindow).

    [Fact]
    public void Push_NonPilot_Allocates_Nothing_When_DataContext_Assigned_After_Binding_Built()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);
        // No DataContextOverride yet: SourceObject/SourceProperty resolve to null when the binding is built.
        DataBinding binding = DataBindingManager.AddBinding(new BindingConfig("Opacity", nameof(AllocationViewModel.Ratio)), border);

        AllocationViewModel vm = new();
        border.DataContextOverride = vm; // assigned AFTER the binding was built

        Action push = GetPushInvoker(binding);
        var i = 0;
        long bytes = MeasureAllocatedBytes(() => { vm.SetRatioQuiet((i++ % 100) * 0.25f); push(); });
        Assert.Equal(0, bytes);
        Assert.Equal(vm.Ratio, border.Opacity);
    }

    [Fact]
    public void Push_Pilot_Margin_Allocates_Nothing_When_DataContext_Assigned_After_Binding_Built()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);
        DataBinding binding = DataBindingManager.AddBinding(new BindingConfig("Margin", nameof(AllocationViewModel.Margin)), border);

        AllocationViewModel vm = new();
        border.DataContextOverride = vm; // assigned AFTER the binding was built

        Action push = GetPushInvoker(binding);
        Thickness[] values = { new(1), new(2, 3), new(4, 5, 6, 7), new(0) };
        var i = 0;
        long bytes = MeasureAllocatedBytes(() => { vm.SetMarginQuiet(values[i++ % values.Length]); push(); });
        Assert.Equal(0, bytes);
        Assert.Equal(vm.Margin, border.Margin);
    }

    //  (3) A same-type pilot binding (Margin<-Thickness) still records a LocalBinding contribution after the change.
    //  Uses the normal notifying setter: this test isn't allocation-sensitive, so a live OneWay update (through the
    //  WPF weak-event plumbing) is the more faithful way to exercise it end to end.

    [Fact]
    public void Push_Pilot_Margin_Still_Records_LocalBinding_Contribution()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);
        AllocationViewModel vm = new() { Margin = new Thickness(3) };
        border.DataContextOverride = vm;

        DataBindingManager.AddBinding(new BindingConfig("Margin", nameof(AllocationViewModel.Margin)), border);

        Assert.Equal(new Thickness(3), border.Margin);
        Assert.True(border.TryGetResolvedPilotValue(UIPilotProperty.Margin, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner));
        Assert.Equal(UIValueSourceKind.LocalBinding, winner.Source.Kind);

        vm.Margin = new Thickness(9);
        Assert.Equal(new Thickness(9), border.Margin);
        Assert.True(border.TryGetResolvedContribution<Thickness>(UIPilotProperty.Margin, UIValueSlot.Whole, UIValueSourceKind.LocalBinding, out UIResolvedValue<Thickness> contribution));
        Assert.Equal(new Thickness(9), contribution.Value);
    }

    //  (4) The data context replaced by a view model of a DIFFERENT type whose bound property has a different type:
    //  the target receives the correct value and nothing throws.

    [Fact]
    public void DataContext_Replaced_By_DifferentType_With_DifferentPropertyType_Updates_Target_Without_Throwing()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);
        AllocationViewModel firstVm = new() { Count = 7 };
        border.DataContextOverride = firstVm;

        DataBindingManager.AddBinding(new BindingConfig("MinHeight", nameof(AllocationViewModel.Count)), border);
        Assert.Equal(7, border.MinHeight);

        OtherViewModel secondVm = new() { Count = 42L };
        Exception Thrown = Record.Exception(() => border.DataContextOverride = secondVm);
        Assert.Null(Thrown);
        Assert.Equal(42, border.MinHeight);

        secondVm.Count = 99L;
        Assert.Equal(99, border.MinHeight);
    }

    //  (5) The type-keyed cache does not grow when binding new instances of an already-seen type.

    [Fact]
    public void TypedAccessorCache_Does_Not_Grow_For_Repeated_Instances_Of_An_Already_Seen_Type()
    {
        static int CacheCount(string FieldName)
        {
            FieldInfo Field = typeof(TypedAccessorCache).GetField(FieldName, BindingFlags.NonPublic | BindingFlags.Static);
            var Dictionary = (ICollection)Field.GetValue(null);
            return Dictionary.Count;
        }

        //  Seed the cache once.
        AllocationViewModel SeedVm = new();
        PlainTarget SeedTarget = BindPlainTarget(SeedVm, nameof(AllocationViewModel.Count), out _);
        SeedVm.Count = 1;
        Assert.Equal(1, SeedTarget.Count);

        int GetterCountBefore = CacheCount("GetterCache");
        int SetterCountBefore = CacheCount("SetterCache");
        int PropertyCountBefore = CacheCount("PropertyCache");
        int CopyCountBefore = CacheCount("CopyCache");

        for (var i = 0; i < 50; i++)
        {
            AllocationViewModel Vm = new();
            PlainTarget Target = BindPlainTarget(Vm, nameof(AllocationViewModel.Count), out _);
            Vm.Count = i;
            Assert.Equal(i, Target.Count);
        }

        Assert.Equal(GetterCountBefore, CacheCount("GetterCache"));
        Assert.Equal(SetterCountBefore, CacheCount("SetterCache"));
        Assert.Equal(PropertyCountBefore, CacheCount("PropertyCache"));
        Assert.Equal(CopyCountBefore, CacheCount("CopyCache"));
    }

    //  (6) A binding with a converter or a string format gives the same result as before.

    private sealed class DoublingConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => (int)value * 2;
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => (int)value / 2;
    }

    [Fact]
    public void Push_With_Converter_Gives_Same_Result_As_Before()
    {
        AllocationViewModel vm = new() { Count = 5 };
        PlainTarget target = new() { DataContext = vm };
        DataBindingManager.AddBinding(new BindingConfig(nameof(PlainTarget.Count), nameof(AllocationViewModel.Count))
        {
            Converter = new DoublingConverter()
        }, target);

        Assert.Equal(10, target.Count);
        vm.Count = 8;
        Assert.Equal(16, target.Count);
    }

    [Fact]
    public void Push_With_StringFormat_Gives_Same_Result_As_Before()
    {
        AllocationViewModel vm = new() { Count = 5 };
        PlainTarget target = new() { DataContext = vm };
        DataBindingManager.AddBinding(new BindingConfig(nameof(PlainTarget.Text), nameof(AllocationViewModel.Count))
        {
            StringFormat = "Count={0}"
        }, target);

        Assert.Equal("Count=5", target.Text);
        vm.Count = 8;
        Assert.Equal("Count=8", target.Text);
    }
}
