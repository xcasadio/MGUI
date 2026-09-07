# ADR-0001: Tie DynamicResource subscriptions to tree membership with a single weak scope link

- **Status**: Accepted (2026-09-07, after the author's review of the task 1 implementation, commit `00cec29`)
- **Date**: 2026-09-07
- **Source**: this chantier: discussion of 2026-09-07 (grouped questions before executing `Docs/Tasks/styling-theme-tasks.md`), recorded in `Docs/Tasks/styling-theme-tasks.md`, task 1, paragraph "Decision (7 septembre 2026)"

## Context

Facts verified at HEAD `abe99f5`:

- `UIResourceReferenceApplicator.RegisterDynamicSubscription` (`MGUI.Core/UI/Styling/UIResourceReferenceApplicator.cs`, lines 84-89) subscribed three lambdas on every ancestor scope of the resolving `MGResources`, with no unsubscription path. Its deduplication (a `HashSet<string>` in `MGElement.Metadata`) ignored the scope and was never cleared, and the refresh closure re-applied against the scope captured at registration, so a reparented element kept resolving against its old chain.
- `MGElement` has no dispose or destroy hook. `SetParent` raises `OnParentChanged` (`MGUI.Core/UI/MGElement.cs:657`), which is the only signal of an element entering or leaving the tree.
- Every `MGWindow` owns a `Window` scope whose parent is `MGDesktop.Resources` (`MGUI.Core/UI/MGWindow.cs:1101` and `:1388`). Closing a window removes it from `Desktop.Windows` (`MGWindow.cs:804`) without changing the parent of its elements, and a closed window may be re-shown as the same instance.
- `MGResources.WeakThemeChangedForwarder` (`MGUI.Core/UI/MGResources.cs:127-156`) forwards `OnDefaultThemeChanged` from a parent scope to a child scope through a weak reference, precisely because a strong subscription from the desktop scope would root every closed window and unsubscribing on close would break propagation to re-shown windows. It is documented as a deliberately narrow weak event ("pas de weak events generalises").
- The static resource events `OnStaticResourceAdded`/`Changed`/`Removed` were self-only (not forwarded from parents); `TryGetStaticResource` walks up the parent chain. The applicator was their only subscriber in the repository.
- Alternatives considered and rejected: a weak forwarder from every ancestor scope to every element (handler count on the desktop scope still grows with the number of elements, and dead handlers linger until the next notification); strong subscriptions on all ancestor scopes with unsubscription on parent change (closed windows stay rooted by the desktop scope because closing does not reparent).

## Decision

- Dynamic resource subscriptions follow tree membership. Each host element owns one subscription container (stored in `MGElement.Metadata` under the existing `DynamicResourceSubscriptions` key) whose entries are deduplicated by (target object, target path, resource name). The container attaches a single handler to the host's nearest resource scope only. On `OnParentChanged` it detaches from the previous scope, attaches to the new nearest scope and re-resolves every entry against the current chain.
- Changes in ancestor scopes reach the nearest scope through a new internal `MGResources` event, `OnStaticResourceLookupChanged(name)`, raised once per `AddStaticResource`/`SetStaticResource`/`RemoveStaticResource` and forwarded from parent to child by the same weak forwarder as the theme event, wired and unwired in `MGResources.SetParent`. No element ever subscribes to an ancestor scope.
- The three existing static resource events keep their self-only semantics, `UIResourceReferenceApplicator.Apply` keeps its signature, and the `HostElement == null` path (unit tests only) stays a fire-and-forget subscription on the given scope.
- The weak link remains the single scope-to-scope point of the framework; no generalised weak-event mechanism is introduced.

## Consequences

- Closed windows are no longer rooted by the desktop scope through dynamic references, and the number of handlers on long-lived scopes no longer grows with the number of elements. Reparenting an element re-resolves its dynamic values from its new position in the tree.
- Any future code that needs a "resource lookup may have changed" notification must subscribe to its nearest scope and rely on the parent-to-child forwarding, never walk the ancestor chain.
- Known limit: creating a local scope on an ancestor after construction (`EnsureResourceScope`) is not followed until the element changes parent. Documented in `Docs/Tasks/styling-theme-tasks.md`, task 1.
- An explicit teardown entry point (`Detach`/`Clear`) exists on the container but has no caller yet; tree membership is the primary lifecycle.
- `OnParentChanged` fires during XAML tree construction, so each dynamic reference is re-applied once when its host gets parented. `DynamicResource` has no shipped usage in the repository today, so this cost is theoretical.
