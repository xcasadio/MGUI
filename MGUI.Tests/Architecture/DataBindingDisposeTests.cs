using System;
using System.ComponentModel;
using MGUI.Core.UI;
using MGUI.Core.UI.DataBinding;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Xunit;

namespace MGUI.Tests.Architecture;

/// <summary>A binding removed from <see cref="DataBindingManager"/> is disposed, and a disposed binding no longer follows its target's
/// data context: a later <c>DataContextChanged</c> on the target must neither push the new data context's value into the target nor
/// start listening to the new source.</summary>
[Collection(DataBindingRegistryCollection.Name)]
public class DataBindingDisposeTests
{
    /// <summary>Raises <see cref="PropertyChanged"/> on every write. Bindings subscribe through a weak event manager, which puts a
    /// single handler on the source whatever the number of listeners, so the tests observe the target instead of counting handlers.</summary>
    private sealed class ObservableTitleViewModel : INotifyPropertyChanged
    {
        private string _Title;
        public string Title
        {
            get => _Title;
            set
            {
                _Title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }

        public ObservableTitleViewModel(string title) => _Title = title;

        public event PropertyChangedEventHandler PropertyChanged;
    }

    private static MGWindow NewWindow()
    {
        MGDesktop desktop = new(new GraphTestRuntime(new Rectangle(0, 0, 640, 480)));
        return new MGWindow(desktop, 0, 0, 320, 200);
    }

    [Fact]
    public void RemoveBindings_ThenDataContextChange_DoesNotReachTheTarget()
        => AssertRemovedBindingIgnoresNewDataContext((text, binding) => Assert.Equal(1, DataBindingManager.RemoveBindings(text)));

    [Fact]
    public void RemoveBinding_ThenDataContextChange_DoesNotReachTheTarget()
        => AssertRemovedBindingIgnoresNewDataContext((text, binding) => Assert.True(DataBindingManager.RemoveBinding(binding)));

    /// <summary>The event copies its handlers before invoking them, so a binding removed by an earlier handler of the same
    /// <c>DataContextChanged</c> is still called once, after its <see cref="DataBinding.Dispose"/>.</summary>
    [Fact]
    public void RemoveBindings_FromAnEarlierDataContextChangedHandler_DoesNotReachTheTarget()
    {
        MGWindow window = NewWindow();
        MGTextBlock text = new(window, string.Empty) { DataContextOverride = new ObservableTitleViewModel("First title") };
        int removedCount = -1;
        text.DataContextChanged += (sender, e) => removedCount = DataBindingManager.RemoveBindings(text);
        DataBinding binding = DataBindingManager.AddBinding(new BindingConfig(nameof(MGTextBlock.Text), nameof(ObservableTitleViewModel.Title)), text);
        Assert.Equal("First title", text.Text);

        ObservableTitleViewModel second = new("Second title");
        text.DataContextOverride = second;

        Assert.Equal(1, removedCount);
        Assert.True(binding.IsDisposed);
        Assert.Equal("First title", text.Text);
        Assert.NotSame(second, binding.SourceObject);
    }

    private static void AssertRemovedBindingIgnoresNewDataContext(Action<MGTextBlock, DataBinding> removeBinding)
    {
        MGWindow window = NewWindow();
        ObservableTitleViewModel first = new("First title");
        MGTextBlock text = new(window, string.Empty) { DataContextOverride = first };
        DataBinding binding = DataBindingManager.AddBinding(new BindingConfig(nameof(MGTextBlock.Text), nameof(ObservableTitleViewModel.Title)), text);
        Assert.Equal("First title", text.Text);

        removeBinding(text, binding);
        Assert.True(binding.IsDisposed);

        ObservableTitleViewModel second = new("Second title");
        text.DataContextOverride = second;
        Assert.Equal("First title", text.Text);

        second.Title = "Changed second title";
        Assert.Equal("First title", text.Text);
        Assert.NotSame(second, binding.SourceObject);
    }
}
