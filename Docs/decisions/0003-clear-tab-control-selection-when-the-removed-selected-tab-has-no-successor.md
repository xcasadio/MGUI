# ADR-0003: Clear the MGTabControl selection when the removed selected tab has no successor

- **Status**: Accepted (approved by the author in the discussion of 2026-09-07, before implementation)
- **Date**: 2026-09-07
- **Source**: this chantier: discussion of 2026-09-07 following the `MGTabControl.RemoveTab` parent-reset fix (commit `07dc570`), author's answer "Oui, corrige aussi le cas dernier onglet retire"

## Context

Facts verified at HEAD `07dc570`:

- `MGTabControl.RemoveTab` (`MGUI.Core/UI/MGTabControl.cs`, lines 464-485) removes the tab from `_Tabs`, resets its parent, raises `OnContentRemoved`, discards its header wrapper and then, when the removed tab was `SelectedTab`, calls `TrySelectTabAtIndex(Math.Max(0, TabIndex - 1))` and ignores the result.
- `TrySelectTabAtIndex` returns false when the index is out of range (no tab left), and `TrySelectTab` returns false when a `SelectedTabChanging` handler cancels. In both cases `_SelectedTab` and `_Content` kept pointing at the removed tab: `GetVisualTreeChildren` still returned it, `UpdateContents` still updated it, `MGTabItem.IsTabSelected` still returned true, and the next `AddTab` did not select the new tab because `SelectedTab` was not null.
- `SelectedTab` is already null before the first `AddTab`, and `AddTab` selects the new tab only when `SelectedTab == null`. `TryDeselectTab` never clears the selection: it selects a neighbour and refuses when the tab is alone. `SelectedTabChanging` is documented as raised just before `SelectedTab` changes, with the new tab being selected as argument, and allows cancellation; the doc of `TrySelectTab` directs deselection to `TryDeselectTab` rather than a null tab argument.
- Consumers outside the control at that HEAD: `MGXAMLDesigner` reads `SelectedTabIndex` and subscribes to `SelectedTabChanged` (it never removes tabs); the XAML `TabItem` sets `IsTabSelected`; the TabControl sample removes a tab through `RemoveTab`. None assumes a non-null `SelectedTab` after a removal.

## Decision

- A removed tab never remains `SelectedTab`. When the selected tab is removed and no other tab takes over the selection (last tab removed, or the switch to the neighbour cancelled by `SelectedTabChanging`), `RemoveTab` clears the selection: `SelectedTab` becomes null, `SelectedTabIndex` becomes -1, the displayed `Content` becomes null, and `SelectedTabChanged` is raised once with (removed tab, null).
- This forced deselection does not raise `SelectedTabChanging`: the removed tab is already gone, so there is nothing left to cancel, and the event keeps its documented contract: its argument is always the tab being selected.
- The neighbour rule (select the tab to the left of the removed one) is unchanged.

## Consequences

- `SelectedTab` can be null while `Tabs` is not empty, after a cancelled replacement selection, as it already could be when the first `AddTab` was cancelled. Handlers of `SelectedTabChanged` must accept a null new value.
- After removing the last tab, the control no longer updates, draws or reports the removed tab through `GetVisualTreeChildren`, and the next `AddTab` selects the new tab again.
- The header element of a removed tab still lives inside its discarded header wrapper button; unchanged and out of scope.
- Pinned by `MGUI.Tests/Controls/MGTabControlRemoveTabTests.cs`.
