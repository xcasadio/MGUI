using System.ComponentModel;

namespace MGUI.Core.UI.Animation.Targets;

/// <summary>Watches one named property of an <see cref="INotifyPropertyChanged"/> source (an element, a render transform, a container).</summary>
internal sealed class UIPropertyChangedSubscription : IDisposable
{
    private INotifyPropertyChanged _Source;
    private readonly string _PropertyName;
    private readonly Action _Changed;

    public UIPropertyChangedSubscription(INotifyPropertyChanged source, string propertyName, Action changed)
    {
        _Source = source ?? throw new ArgumentNullException(nameof(source));
        _PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        _Changed = changed ?? throw new ArgumentNullException(nameof(changed));
        _Source.PropertyChanged += HandlePropertyChanged;
    }

    private void HandlePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == _PropertyName)
        {
            _Changed();
        }
    }

    public void Dispose()
    {
        if (_Source != null)
        {
            _Source.PropertyChanged -= HandlePropertyChanged;
            _Source = null;
        }
    }
}

/// <summary>Watches one sub-field of a container held by an element (<c>BackgroundBrush.NormalValue</c>, <c>Foreground.NormalValue</c>, ...):
/// follows the container when the element replaces it (the element notifies the container property) and the sub-field inside the container.</summary>
internal sealed class UIContainerSlotSubscription : IDisposable
{
    private readonly MGElement _Element;
    private readonly string _ContainerPropertyName;
    private readonly Func<MGElement, INotifyPropertyChanged> _GetContainer;
    private readonly string _SlotPropertyName;
    private readonly Action _Changed;
    private INotifyPropertyChanged _Container;
    private bool _Disposed;

    public UIContainerSlotSubscription(MGElement element, string containerPropertyName, Func<MGElement, INotifyPropertyChanged> getContainer, string slotPropertyName, Action changed)
    {
        _Element = element ?? throw new ArgumentNullException(nameof(element));
        _ContainerPropertyName = containerPropertyName ?? throw new ArgumentNullException(nameof(containerPropertyName));
        _GetContainer = getContainer ?? throw new ArgumentNullException(nameof(getContainer));
        _SlotPropertyName = slotPropertyName ?? throw new ArgumentNullException(nameof(slotPropertyName));
        _Changed = changed ?? throw new ArgumentNullException(nameof(changed));
        _Element.PropertyChanged += HandleElementPropertyChanged;
        AttachContainer();
    }

    private void AttachContainer()
    {
        INotifyPropertyChanged container = _GetContainer(_Element);
        if (ReferenceEquals(container, _Container))
        {
            return;
        }

        if (_Container != null)
        {
            _Container.PropertyChanged -= HandleContainerPropertyChanged;
        }

        _Container = container;
        if (_Container != null)
        {
            _Container.PropertyChanged += HandleContainerPropertyChanged;
        }
    }

    private void HandleElementPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (_Disposed || e.PropertyName != _ContainerPropertyName)
        {
            return;
        }

        AttachContainer();
        _Changed();
    }

    private void HandleContainerPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (!_Disposed && e.PropertyName == _SlotPropertyName)
        {
            _Changed();
        }
    }

    public void Dispose()
    {
        if (_Disposed)
        {
            return;
        }

        _Disposed = true;
        _Element.PropertyChanged -= HandleElementPropertyChanged;
        if (_Container != null)
        {
            _Container.PropertyChanged -= HandleContainerPropertyChanged;
            _Container = null;
        }
    }
}