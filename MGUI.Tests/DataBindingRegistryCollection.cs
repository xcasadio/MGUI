namespace MGUI.Tests;

/// <summary>Every test class that reaches <see cref="MGUI.Core.UI.DataBinding.DataBindingManager"/> joins this collection, whose tests
/// xunit never runs in parallel with any other test. The manager keeps every binding in two static collections that are not
/// synchronized, because MGUI's contract is that the UI runs on one thread (ADR-0019); xunit, however, runs test classes in parallel,
/// and two classes touching the registry at once corrupt its dictionary ("Operations that change non-concurrent collections must have
/// exclusive access"), after which every later binding of the process fails.<para/>
/// A class reaches the registry directly (<c>AddBinding</c>, <c>RemoveBinding</c>, <c>RemoveBindings</c>, <c>Bindings</c>) or
/// indirectly: by loading XAML that declares a binding, or by removing an element's bindings (<c>MGElement.RemoveDataBindings</c>,
/// which a list box, combo box or list view calls when it replaces or recycles an item's content, and which the XAML preview and
/// designer call when they replace their content). A new test class that does any of these belongs here.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DataBindingRegistryCollection
{
    public const string Name = "DataBindingManager global registry";
}
