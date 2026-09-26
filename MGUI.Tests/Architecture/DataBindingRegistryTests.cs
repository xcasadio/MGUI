using System;
using System.ComponentModel;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.DataBinding;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Xunit;

namespace MGUI.Tests.Architecture;

/// <summary>A binding removed from <see cref="DataBindingManager"/> leaves its registry entirely: disposed, and gone
/// from <see cref="DataBindingManager.Bindings"/> too, since a binding still listed there keeps its target element
/// (and the tree above it) reachable. A binding the manager refuses never enters it. Other test classes leave their
/// bindings in this static registry, so these tests look for their own bindings only, never at the registry's count.</summary>
[Collection(DataBindingRegistryCollection.Name)]
public class DataBindingRegistryTests
{
    private sealed class LabelViewModel : INotifyPropertyChanged
    {
        public string Title => "Title";
        public string Subtitle => "Subtitle";

        public event PropertyChangedEventHandler PropertyChanged
        {
            add { }
            remove { }
        }
    }

    /// <summary>Raises <see cref="PropertyChanged"/> on every write, so a test can tell whether a binding it cannot
    /// reach still listens to its source: bindings subscribe through a weak event manager, which puts a single
    /// handler on the source whatever the number of listeners, so counting handlers would not tell.</summary>
    private sealed class ObservableLabelViewModel : INotifyPropertyChanged
    {
        private string _Title = "Title";
        public string Title
        {
            get => _Title;
            set
            {
                _Title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }

        private string _Subtitle = "Subtitle";
        public string Subtitle
        {
            get => _Subtitle;
            set
            {
                _Subtitle = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Subtitle)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    private static MGWindow NewWindow()
    {
        MGDesktop desktop = new(new GraphTestRuntime(new Rectangle(0, 0, 640, 480)));
        return new MGWindow(desktop, 0, 0, 320, 200);
    }

    [Fact]
    public void RemoveBindings_TakesTheTargetsBindingsOutOfTheRegistry()
    {
        MGWindow window = NewWindow();
        MGTextBlock text = new(window, string.Empty) { DataContextOverride = new LabelViewModel() };
        DataBinding first = DataBindingManager.AddBinding(new BindingConfig(nameof(MGTextBlock.Text), nameof(LabelViewModel.Title)), text);
        DataBinding second = DataBindingManager.AddBinding(new BindingConfig(nameof(MGElement.Tag), nameof(LabelViewModel.Subtitle)), text);

        Assert.Equal(2, DataBindingManager.RemoveBindings(text));

        Assert.True(first.IsDisposed);
        Assert.True(second.IsDisposed);
        Assert.DoesNotContain(first, DataBindingManager.Bindings);
        Assert.DoesNotContain(second, DataBindingManager.Bindings);
        Assert.Equal(0, DataBindingManager.RemoveBindings(text));
    }

    [Fact]
    public void RemoveDataBindings_OnAWindow_TakesEveryBindingOfItsTreeOutOfTheRegistry()
    {
        MGWindow window = NewWindow();
        MGTextBlock text = new(window, string.Empty) { DataContextOverride = new LabelViewModel() };
        window.SetContent(text);
        DataBinding binding = DataBindingManager.AddBinding(new BindingConfig(nameof(MGTextBlock.Text), nameof(LabelViewModel.Title)), text);

        Assert.Equal(1, window.RemoveDataBindings(IncludeChildren: true));

        Assert.True(binding.IsDisposed);
        Assert.DoesNotContain(DataBindingManager.Bindings, b => ReferenceEquals(b.TargetObject, text));
    }

    [Fact]
    public void AddBinding_RefusingADuplicateTargetPath_LeavesNoTraceOfTheRefusedBinding()
    {
        MGWindow window = NewWindow();
        ObservableLabelViewModel viewModel = new();
        MGTextBlock text = new(window, string.Empty) { DataContextOverride = viewModel };
        DataBinding first = DataBindingManager.AddBinding(new BindingConfig(nameof(MGTextBlock.Text), nameof(ObservableLabelViewModel.Title)), text);

        Assert.Throws<InvalidOperationException>(() =>
            DataBindingManager.AddBinding(new BindingConfig(nameof(MGTextBlock.Text), nameof(ObservableLabelViewModel.Subtitle)), text));

        //  The refused binding did not write its source value over the existing binding's, and does not listen to its source.
        Assert.Equal("Title", text.Text);
        viewModel.Subtitle = "Changed subtitle";
        Assert.Equal("Title", text.Text);
        viewModel.Title = "Changed title";
        Assert.Equal("Changed title", text.Text);

        Assert.Same(first, Assert.Single(DataBindingManager.Bindings, b => ReferenceEquals(b.TargetObject, text)));
        Assert.Equal(1, DataBindingManager.RemoveBindings(text));
        Assert.True(first.IsDisposed);
        Assert.DoesNotContain(DataBindingManager.Bindings, b => ReferenceEquals(b.TargetObject, text));
    }
}
