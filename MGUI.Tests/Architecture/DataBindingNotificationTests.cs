using MGUI.Core.UI;
using MGUI.Core.UI.DataBinding;
using MGUI.Shared.Helpers;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Architecture;

/// <summary>
/// A <see cref="DataBinding"/> subscribes directly to <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> of its source,
/// its target and the intermediate objects of a dotted path, instead of going through WPF's weak <c>PropertyChangedEventManager</c>. The values
/// still flow both ways, a replaced intermediate object is followed, a removed binding stops pushing, and a live notification of a typed path
/// (ADR-0016), from the notifying setter to the target, allocates nothing.
/// </summary>
[Collection(DataBindingRegistryCollection.Name)]
public class DataBindingNotificationTests
{
    private const int WarmupIterations = 2000;
    private const int MeasuredIterations = 1000;

    private sealed class LevelViewModel : ViewModelBase
    {
        private float _Level;
        public float Level { get => _Level; set { _Level = value; NotifyPropertyChanged(); } }
    }

    private sealed class MixerViewModel : ViewModelBase
    {
        private float _Volume;
        public float Volume { get => _Volume; set { _Volume = value; NotifyPropertyChanged(); } }

        private LevelViewModel _Bus;
        public LevelViewModel Bus { get => _Bus; set { _Bus = value; NotifyPropertyChanged(); } }
    }

    [Fact]
    public void OneWay_ASourceNotification_MovesTheSlider()
    {
        MGSlider slider = CreateSlider();
        MixerViewModel vm = new() { Volume = 10 };
        slider.DataContextOverride = vm;
        DataBinding binding = DataBindingManager.AddBinding(new BindingConfig("Value", nameof(MixerViewModel.Volume)), slider);
        try
        {
            Assert.Equal(10f, slider.Value);

            vm.Volume = 30;

            Assert.Equal(30f, slider.Value);
        }
        finally
        {
            DataBindingManager.RemoveBinding(binding);
        }
    }

    [Fact]
    public void TwoWay_TheSliderAndTheSourceFollowEachOther()
    {
        MGSlider slider = CreateSlider();
        MixerViewModel vm = new() { Volume = 10 };
        slider.DataContextOverride = vm;
        DataBinding binding = DataBindingManager.AddBinding(new BindingConfig("Value", nameof(MixerViewModel.Volume), DataBindingMode.TwoWay), slider);
        try
        {
            slider.SetValue(70);
            Assert.Equal(70f, vm.Volume);

            vm.Volume = 20;
            Assert.Equal(20f, slider.Value);
        }
        finally
        {
            DataBindingManager.RemoveBinding(binding);
        }
    }

    [Fact]
    public void ADottedPath_FollowsTheReplacedIntermediateObject()
    {
        MGSlider slider = CreateSlider();
        LevelViewModel first = new() { Level = 10 };
        LevelViewModel second = new() { Level = 40 };
        MixerViewModel vm = new() { Bus = first };
        slider.DataContextOverride = vm;
        DataBinding binding = DataBindingManager.AddBinding(new BindingConfig("Value", "Bus.Level"), slider);
        try
        {
            Assert.Equal(10f, slider.Value);

            vm.Bus = second;
            Assert.Equal(40f, slider.Value);

            second.Level = 55;
            Assert.Equal(55f, slider.Value);

            first.Level = 90;
            Assert.Equal(55f, slider.Value);
        }
        finally
        {
            DataBindingManager.RemoveBinding(binding);
        }
    }

    [Fact]
    public void ARemovedBinding_NoLongerPushes()
    {
        MGSlider slider = CreateSlider();
        MixerViewModel vm = new() { Volume = 10 };
        slider.DataContextOverride = vm;
        DataBinding binding = DataBindingManager.AddBinding(new BindingConfig("Value", nameof(MixerViewModel.Volume), DataBindingMode.TwoWay), slider);

        DataBindingManager.RemoveBinding(binding);
        vm.Volume = 80;
        slider.SetValue(60);

        Assert.Equal(60f, slider.Value);
        Assert.Equal(80f, vm.Volume);
    }

    [Fact]
    public void ASourceNotification_FromTheSetterToTheSlider_AllocatesNothing()
    {
        MGSlider slider = CreateSlider();
        MixerViewModel vm = new() { Volume = 10 };
        slider.DataContextOverride = vm;
        DataBinding binding = DataBindingManager.AddBinding(new BindingConfig("Value", nameof(MixerViewModel.Volume)), slider);
        try
        {
            int i = 0;
            void Notify() => vm.Volume = i++ % 2 == 0 ? 25f : 75f;

            for (int w = 0; w < WarmupIterations; w++)
            {
                Notify();
            }

            long before = AllocationWindow.Start();
            for (int m = 0; m < MeasuredIterations; m++)
            {
                Notify();
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0, after - before);
            Assert.Equal(vm.Volume, slider.Value);
        }
        finally
        {
            DataBindingManager.RemoveBinding(binding);
        }
    }

    private static MGSlider CreateSlider()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 300, 200);
        desktop.Windows.Add(window);
        MGSlider slider = new(window, 0, 100, 0);
        window.SetContent(slider);
        return slider;
    }
}
