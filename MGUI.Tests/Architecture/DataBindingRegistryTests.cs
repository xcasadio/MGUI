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
/// (and the tree above it) reachable. Other test classes leave their bindings in this static registry, so these tests
/// look for their own bindings only, never at the registry's count.</summary>
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
}
