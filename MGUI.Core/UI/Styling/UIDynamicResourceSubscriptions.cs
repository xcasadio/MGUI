using MGUI.Shared.Helpers;
using System.Runtime.CompilerServices;

namespace MGUI.Core.UI.Styling;

/// <summary>Tracks every <see cref="UIResourceReferenceConfig"/> a single <see cref="MGElement"/> (the "host") has dynamically bound,
/// and keeps them resolved against the host's CURRENT position in the resource-scope tree.<para/>
/// One instance is created per host element (stored in <see cref="MGElement.Metadata"/>) the first time it registers a dynamic
/// resource reference. It replaces the previous unbounded per-key <c>HashSet</c> + one-lambda-per-ancestor-scope design:<list type="bullet">
/// <item>Entries are deduplicated by (target identity, target path, resource name), so re-applying the same reference is a no-op.</item>
/// <item>Exactly ONE handler is subscribed at a time, to the host's nearest scope only (<see cref="MGResources.OnStaticResourceLookupChanged"/>);
/// ancestor-scope changes reach it through that event's own weak forwarding (<see cref="MGResources"/>), never through a strong handler on an
/// ancestor scope.</item>
/// <item>The container hooks <see cref="MGElement.OnParentChanged"/> once, so a re-parented host detaches from its previous scope and
/// re-attaches (re-resolving every entry) against its new position, instead of forever resolving against the scope captured at
/// registration time.</item>
/// </list></summary>
internal sealed class UIDynamicResourceSubscriptions
{
    private readonly record struct SubscriptionKey(int TargetIdentity, string TargetPath, string ResourceName);
    private readonly record struct SubscriptionEntry(object TargetObject, UIResourceReferenceConfig Config);

    private readonly MGElement HostElement;
    private readonly Dictionary<SubscriptionKey, SubscriptionEntry> Entries = new();
    private bool IsParentChangeHooked;

    private MGResources _AttachedScope;
    internal MGResources AttachedScope => _AttachedScope;

    public UIDynamicResourceSubscriptions(MGElement HostElement)
    {
        this.HostElement = HostElement ?? throw new ArgumentNullException(nameof(HostElement));
    }

    /// <summary>Registers a dynamic resource reference for this host. Returns false if an entry with the same
    /// (target, <see cref="UIResourceReferenceConfig.TargetPath"/>, <see cref="UIResourceReferenceConfig.ResourceName"/>) already exists
    /// (dedup) - in particular this makes re-entrant re-application (triggered by <see cref="HandleResourceLookupChanged"/> itself calling
    /// back into <see cref="UIResourceReferenceApplicator.Apply"/>) a no-op instead of growing the subscription list.</summary>
    public bool TryRegister(object TargetObject, UIResourceReferenceConfig Config, MGResources Resources)
    {
        SubscriptionKey Key = new(RuntimeHelpers.GetHashCode(TargetObject), Config.TargetPath, Config.ResourceName);
        if (!Entries.TryAdd(Key, new(TargetObject, Config)))
        {
            return false;
        }

        AttachTo(Resources);
        HookParentChangeOnce();
        return true;
    }

    private void HookParentChangeOnce()
    {
        if (!IsParentChangeHooked)
        {
            IsParentChangeHooked = true;
            HostElement.OnParentChanged += HandleParentChanged;
        }
    }

    private void HandleParentChanged(object sender, EventArgs<MGElement> e)
    {
        Detach();

        MGResources NewScope = ResolveHostResources();
        if (NewScope != null)
        {
            AttachTo(NewScope);
            ReapplyAll(NewScope);
        }
    }

    /// <summary>Resolves the scope the host should observe from its current position, or null when the host is outside the tree.<para/>
    /// A host with its own local scope (every <see cref="MGWindow"/>, or an element that called <see cref="MGElement.EnsureResourceScope"/>)
    /// observes that scope: its link to the ancestor chain is the weak one owned by <see cref="MGResources"/>. A host that still has a
    /// <see cref="MGElement.Parent"/> observes its inherited scope. A non-window host whose <see cref="MGElement.Parent"/> is null has been
    /// removed from its container and must stay detached: <see cref="MGElement.ParentWindow"/> is fixed at construction, so falling back to the
    /// window scope would keep a strong handler there for every discarded element for as long as the window lives. This also avoids the
    /// <see cref="MGElement.GetResources"/>/<see cref="MGDesktop"/> null-reference chain a fully orphaned element would hit.</summary>
    private MGResources ResolveHostResources()
    {
        if (HostElement.LocalResources != null)
        {
            return HostElement.LocalResources;
        }

        if (HostElement.Parent != null)
        {
            return HostElement.GetResources();
        }

        return null;
    }

    private void AttachTo(MGResources Scope)
    {
        if (Scope == null || ReferenceEquals(_AttachedScope, Scope))
        {
            return;
        }

        Detach();
        _AttachedScope = Scope;
        _AttachedScope.OnStaticResourceLookupChanged += HandleResourceLookupChanged;
    }

    /// <summary>Detaches from the currently attached scope, if any. Safe to call repeatedly.</summary>
    public void Detach()
    {
        if (_AttachedScope != null)
        {
            _AttachedScope.OnStaticResourceLookupChanged -= HandleResourceLookupChanged;
            _AttachedScope = null;
        }
    }

    /// <summary>Fully tears the container down: detaches from its scope, unhooks <see cref="MGElement.OnParentChanged"/>, and forgets every entry.</summary>
    public void Clear()
    {
        Detach();

        if (IsParentChangeHooked)
        {
            HostElement.OnParentChanged -= HandleParentChanged;
            IsParentChangeHooked = false;
        }

        Entries.Clear();
    }

    private void HandleResourceLookupChanged(object sender, string ResourceName)
    {
        if (_AttachedScope == null)
        {
            return;
        }

        foreach (SubscriptionEntry Entry in Entries.Values.ToArray())
        {
            if (string.Equals(Entry.Config.ResourceName, ResourceName, StringComparison.Ordinal))
            {
                _ = UIResourceReferenceApplicator.Apply(HostElement, Entry.TargetObject, Entry.Config, _AttachedScope);
            }
        }
    }

    private void ReapplyAll(MGResources Scope)
    {
        foreach (SubscriptionEntry Entry in Entries.Values.ToArray())
        {
            _ = UIResourceReferenceApplicator.Apply(HostElement, Entry.TargetObject, Entry.Config, Scope);
        }
    }
}