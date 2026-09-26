# ADR-0019: The data binding registry stays single-threaded, and the tests that reach it never run in parallel

- **Status**: Accepted
- **Date**: 2026-09-26
- **Source**: this chantier: discussion with the author on 2026-09-26, branch `chantier/binding-registry-test-collection`
  ("production code is not changed to fix a test problem"). Engine counterpart already in place: CasaEngine commit
  `f2bc60bb`, `CasaEngine.Tests/MguiDataBindingCollection.cs`.

## Context

- `DataBindingManager` (`MGUI.Core/UI/DataBinding/DataBindingManager.cs:6-9`) keeps every binding in two static,
  unsynchronized collections, a `List<DataBinding>` and a `Dictionary<object, List<DataBinding>>`. `AddBinding`,
  `RemoveBinding` and `RemoveBindings` mutate them; `Bindings` exposes the list itself.
- MGUI's contract is that the UI runs on one thread. The animation engine stays on the update thread, and a
  cancellation token only posts a request that the update drains (ADR-0011, decision 2). The editor has no thread
  and no system timer (`Docs/editor-architecture.md`). Bindings are created while XAML is loaded
  (`MGUI.Core/UI/XAML/Element.cs`, `ProcessBindings`). They are removed through `MGElement.RemoveDataBindings`,
  which list boxes, combo boxes and list views call when they replace or recycle an item's content, and which the
  XAML preview (`MGUI.Editor/Preview/XamlPreviewHost.cs`) and `MGXAMLDesigner` call when they replace their content.
- All of these run on the UI thread, with one known exception. `MGXAMLDesigner`'s auto-refresh handles
  `FileSystemWatcher.Changed` (`MGUI.Core/UI/MGXAMLDesigner.cs:47-54`), and that watcher has no
  `SynchronizingObject`, so .NET raises the event on a thread-pool thread. `RefreshParsedContent` then loads the
  file on that thread, bindings included (`:282`), and removes the bindings of the previous content (`:319`). The
  production code starts only two other threads: the STA threads of `MGXAMLDesigner.StartSTATask` (`:254-265`, a
  file dialog) and `StringClipboard` (`MGUI.Core/UI/Text/StringClipboard.cs:21`). Their callers block on them, and
  neither touches the registry.
- The process-wide caches keyed by type are thread-safe: `TypedAccessorCache`, the converter caches of `DataBinding`,
  `UIEasing` and `UIAnimationTargets`. The registry, which holds per-object UI state, is not.
- xunit 2.9.2 runs the test classes of `MGUI.Tests` in parallel, in one process (no `xunit.runner.json`, no
  assembly-level `CollectionBehavior`). A probe that added bindings from 4 threads threw between 14,000 and 22,000
  `InvalidOperationException`s ("Operations that change non-concurrent collections must have exclusive access") in each
  of 3 runs. Once the dictionary is corrupted, every later `AddBinding` of the process fails. In `CasaEngine.Tests`, the
  same race failed about one run in thirty before its binding tests were serialized.
- A temporary, uncommitted probe recorded every call into the registry during two full runs of `MGUI.Tests` (3,090
  tests, none skipped). Both runs found the same 19 test classes. Eight reach `AddBinding`, directly or by loading
  bound XAML. The others reach the registry only through `RemoveBindings` (item recycling, preview and designer
  content replacement) or `Bindings`. No call came from outside a test class.
- The xunit documentation guarantees that "Any test which is opted out of parallelism will be guaranteed not to run
  in parallel against any other test" (https://xunit.net/docs/running-tests-in-parallel).

## Decision

- `DataBindingManager` keeps its single-threaded contract: bindings are created, removed and read on the UI thread.
  No lock and no concurrent collection is added. A test problem is not fixed in production code.
- `Bindings` stays the live internal list, to be read on the UI thread only.
- Every `MGUI.Tests` class that reaches the registry, directly or indirectly, joins `DataBindingRegistryCollection`
  (`MGUI.Tests/DataBindingRegistryCollection.cs`, `DisableParallelization = true`).

## Consequences

- The runtime is unchanged and bears no cost. The registry's thread contract is written down here for the first time.
- A new test class that reaches the registry must join the collection. This covers classes that load XAML declaring a
  binding, that replace list box, combo box or list view item content, that drive the XAML preview or designer, or
  that read `Bindings`. Nothing enforces it: a class that forgets reintroduces the race, silently until it collides.
  The collection's doc comment lists what counts as reaching the registry.
- Validation on 2026-09-26: a temporary class that added and removed bindings for 10 seconds, outside the collection.
  With the collection, 3 full runs out of 3 passed. With `DisableParallelization` set back to `false`, 3 runs out of 3
  failed: `Bindings` was modified while `DataBindingRegistryTests` enumerated it, and a binding was lost from the
  dictionary (`RemoveBindings` returned 0 instead of 1).
- The 19 classes no longer run in parallel with other tests. The suite went from 12 to 13 seconds before the change to
  14 seconds after, as reported by `dotnet test`.
- A class belongs to one collection only. A class that must also be isolated for another reason can stay in another
  `DisableParallelization` collection: it is then not run in parallel with any test either. This settles
  `DataBindingAllocationTests`, which the unmerged branch `chantier/host-image-allocation-test-flake` (`10cf82c`)
  moves into `TypedAccessorCacheCollection`: when the two branches meet, either attribute is correct.
- Deferred to a separate task: `MGXAMLDesigner`'s auto-refresh breaks the contract. It builds the new content,
  registry included, on a thread-pool thread (see Context). Bringing that refresh back to the update thread is a
  production fix of its own, outside this decision.
- Deferred to a separate task: `AddBinding` adds the binding to `_Bindings` (`DataBindingManager.cs:19`) before it
  refuses a duplicate target path (`:25-29`), so a refused binding stays registered, never disposed and still
  subscribed, and `RemoveBindings` cannot find it.
