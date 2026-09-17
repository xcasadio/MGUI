# ADR-0012: Keep emptied docking tab groups as placeholders and persist panel places in layout format 2.0

- **Status**: Proposed (plan approved by the author on 2026-09-17; to be marked Accepted when the chantier closes, task T6)
- **Date**: 2026-09-17
- **Source**: this chantier: the author's answers of 2026-09-17 and the approved plan `Docs/Tasks/docking-ghost-groups-tasks.md` (decisions D1-D9, proposals P1-P12); triggered by a fresh-context verification of the XAML editor's docking shell on branch `xaml-editor` (`c20c9d2`), recorded as a known limit in `Docs/decisions/0010-xaml-editor-v1.md` (X1b) and hinted at in `Docs/Tasks/docking-bugs-tasks.md:176`

## Context

Facts verified at `develop` `4b51ca6` (the docking code is identical on `xaml-editor`):

- When a tab group loses its last panel, `DockOperation.RemovePanel` removes the group and collapses its parent split, the sibling taking the split's place (`MGUI.Core/UI/Docking/DockLayout/DockOperation.cs:404-531`); only an empty root group stays.
- `MGDockHost.DetachToFloating` remembers, in memory only, the id of the group a panel floated out of (`MGUI.Core/UI/Docking/Controls/MGDockHost.cs:68`, `:1312-1349`). `RedockPanel` sends the panel back to that group if it still exists, otherwise appends it to the first visible group (`:1477-1506`). Floating then re-docking panes one after the other therefore collapses a layout where every pane has its own group into a single tab group.
- `UnpinPanel` / `RepinPanel` remember the group, zone and ratio on the panel (`DockPanelNode.cs:195-215`); when the group is gone the panel is re-created at a root edge (`MGDockHost.cs:1641-1707`), not next to its former neighbour. A closed panel's place is not remembered at all; `ShowDockable` appends it to the first group of the model (`:1247-1297`).
- `DockLayoutSerializer` writes version "1.0" and the root node only: no floating windows, no auto-hidden panels, no remembered places (`DockLayoutSerializer.cs:11`, `:117-137`); floating windows exist only in the host (`MGDockHost.cs:164-169`).
- A tab group visual follows its own group's `CollectionChanged` (`MGDockTabGroup.cs:714-717`); `DockLayoutModel.LayoutChanged` does not fire reliably when panels are added or removed (`DockLayoutModel.cs:29-39`, `:302-356`).
- `XamlEditorView.CreateDockHost` (branch `xaml-editor`) sets `LayoutModel` then calls `RegisterPanel` for each panel (`MGUI.Editor/XamlEditorView.cs:169-175`), and `RegisterPanel` throws on an already registered id (`MGDockHost.cs:1036-1039`).

## Decision

- Placeholder groups (AvalonDock style): an emptied tab group stays in the tree, hidden, as long as a remembered place references it. When no place references it any more, it is removed and splits collapse as today.
- Structural visibility, no stored flag: an empty non-root group is hidden; a split with a hidden or null child shows only its other child, without a splitter, and keeps its `SplitRatio`; a split with two hidden children is hidden. An empty root group is still shown. Minimum sizes, splitter-bar drops, `ShowDockable`, maximize and `GetDocumentArea` ignore hidden parts; `GetAllTabGroups()` still lists every group.
- Remembered places live in `DockLayoutModel`, in one internal table keyed by panel id (group id, tab index), shared by floating, auto-hidden and closed panels. They replace the host's `_floatedFromGroupId` and the internal `AutoHideReturn*` members of `DockPanelNode`. A place is remembered on close only when the host's `DockableRegistry` knows the id.
- Floating windows live in the model (`DockLayoutModel` floating store of `DockFloatingGroup`: tab group plus bounds); the host creates and closes `MGFloatingDockWindow` instances from it and writes window bounds back.
- On return (Dock, Pin, `ShowDockable`), a panel goes back to its remembered group at its remembered tab index, clamped, and becomes the active tab. Fallbacks when the place leads nowhere (the application replaced the tree) keep today's behaviour.
- Every host operation that removes, returns, hides, re-pins, closes or reopens a panel rebuilds the visual tree itself instead of relying on `LayoutChanged`.
- Layout format 2.0, without backward compatibility: `rootNode` (placeholder groups included), `floatingGroups`, `autoHide`, `placements`. Any other version, malformed JSON, a duplicated panel id or a place whose panel is docked fails the load. `DockLayoutSerializer.TryFromJson` and `MGDockHostExtensions.TryLoadLayoutFromJson(host, json, panelFactory, out diagnostics)` report the failure without throwing, leave the current layout untouched and write the message to `Debug`; `FromJson` and `LoadLayoutFromJson` keep throwing. The host application (for example the CasaEngine editor) logs the diagnostics and keeps its default layout.
- Replacing the host's `LayoutModel` syncs floating windows, auto-hide strips and the drawer but leaves the panel registry alone, so "set `LayoutModel`, then `RegisterPanel`" keeps working; only a layout load rebuilds the registry from the loaded model.
- `ShowDockable(id)` resolves in this order: registered (docked or auto-hidden) panel as today; floating panel, activated in its window without creating a node; remembered place; first visible group.
- No public API is removed or renamed; additions are the model's floating store, `DockFloatingGroup`, `DockLayoutSerializer.TryFromJson` and `MGDockHostExtensions.TryLoadLayoutFromJson`. New `DockOperation` operations are internal.
- Rejected alternatives: remembering the former neighbour and rebuilding the split around it, re-using the original node ids (exact only when panels return in reverse order); anchoring on the common ancestor of the neighbour's former panels (more logic, still approximate for interleaved orders); keeping the remembered place in memory only (floating windows and auto-hidden panels were not saved, so a persisted place would have had nothing to restore).

## Consequences

- A panel floated, auto-hidden or closed returns to the same group node, between the same splits with the same ids and ratios, whatever the return order; with one panel per group the saved layout is identical after the return.
- Within a group holding several tabs, tab order is identical only when one panel of the group is away or when absent panels return in the reverse order of their departure; otherwise it follows the index rule, and the active tab is the last returned panel. An exact order for every return order would require remembering each group's full order (deferred).
- Existing `docking_layout.json` files and layouts saved by host applications in format 1.0 no longer load; the failure is reported, not thrown, through the `Try` APIs.
- Callers of `GetAllTabGroups()` may now receive empty placeholder groups.
- Placeholders of closed dockables stay in the tree while their place is remembered (at most one per registry definition).
- The `xaml-editor` branch must re-run its docking tests after merging `develop`, and its ADR-0010 X1b note on the collapsing layout becomes outdated.

## Decisions taken during delivery

- T1, 2026-09-17: `DockLayoutModel.SubscribeToNodeTree` is idempotent (it removes the handler before adding it). A panel removed from the tree by `DockOperation.RemovePanel` was never unsubscribed, so adding it to a floating group subscribed it a second time and every later structural change on it raised `LayoutChanged` twice, one more time per float cycle. Unsubscribing panels when they leave the tree was left out: it would change the `LayoutChanged` notifications raised by the existing operations, which this slice must not alter.
- T3, 2026-09-17: `UnpinPanel` and `RepinPanel` are no-ops when the panel is already in the target state (`IsPinned` false and true respectively). The placement operations require the panel to be docked, respectively detached, and would otherwise throw; the public API keeps the tolerant behaviour it had.
- T2, 2026-09-17: `MGDockHost.CloseFloatingWindow`, called directly on a window whose floating group is still in the model, now closes the panels it still holds (as the window's own close button already did) and reports them through `PanelRemoved` and the dockable registry. Otherwise the group would stay in the model's floating store and the window would be recreated at the next commit. Called on a window whose group has already left the model (the host's own paths) it only stops tracking the window, as before.
- T1, 2026-09-17: `AddFloatingGroup` and `RemoveFloatingGroup` raise `LayoutChanged` in the middle of the placeholder operations; host callers suspend their subscription around those calls and rebuild once afterwards, as `UnpinPanel` / `RepinPanel` already do.
