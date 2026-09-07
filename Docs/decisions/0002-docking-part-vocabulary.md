# ADR-0002: Align the docking part vocabulary on the framework's shared roles

- **Status**: Accepted (decided by the author on 2026-09-07 during the styling task 3 discussion)
- **Date**: 2026-09-07
- **Source**: this chantier: grouped questions of 2026-09-07 before executing task 3 of `Docs/Tasks/styling-theme-tasks.md`; the frozen vocabulary is recorded in `Docs/styling-theme-architecture.md`, section "Docking: vocabulaire des parts visuelles"

## Context

Facts verified at HEAD `5ce7a4d`:

- The docking controls (`MGUI.Core/UI/Docking/Controls/*.cs`) register `PART_*` template parts, but their names diverged from the target vocabulary written in the earlier plan (`PART_HeaderText` and `PART_Grip` for the auto-hide drawer, `PART_TabHeader`/`PART_TabTitle`/`PART_TabCloseButton` for tabs, `PART_Overlay` plus five `PART_*DropZone` for the drop indicators). None of those target names exists anywhere in the framework.
- The shared roles that do exist are declared by non-docking controls: `MGWindow` (`PART_Border`, `PART_TitleBar`, `PART_TitleBarText`, `PART_CloseButton`, `PART_ResizeGrip`), `MGOverlay` (`PART_Border`, `PART_CloseButton`), `MGTextBox` (`PART_ResizeGrip`) and `MGTabControl` (`PART_HeadersPanel`). `PART_Grip` is the drag handle of `MGDockSplitterBar`, a different role from a resize grip.
- `MGDockAutoHideDrawer` already used `PART_Border`, `PART_CloseButton` and `PART_ResizeGrip`; only its header container (`PART_Header`) and title text (`PART_TitleLabel`) diverged from the window roles.
- `MGDockTabItem` registered `PART_Surface`, `PART_Accent`, `PART_CloseIcon`, `PART_PinIcon` but left its title `MGTextBlock` and its two `MGBorder` buttons unregistered. `MGDockTabGroup` left its tab headers `MGStackPanel` unregistered. `MGDockDropIndicators` registered nothing although it owns nine zone elements (five target zones and four host-edge zones) and has no separate surface element.
- `PART_Surface` and `PART_Accent` are already shared by the tab item, the tab group, the splitter bar and the preview overlay.
- No test, sample or XAML asset references the docking part names; the `Dark.Dock*` templates are bare `BasedOn` entries; the catalogue resolves three drawer parts by constant.

## Decision

- Shared roles win over per-control aliases: a docking part takes the name already used by the framework for the same role, and the old plan's docking-specific names are dropped.
- `MGDockAutoHideDrawer`: `PART_Header` becomes `PART_TitleBar` and `PART_TitleLabel` becomes `PART_TitleBarText` (the `MGWindow` roles); `PART_ResizeGrip` stays, `PART_Grip` is not adopted. The old constant identifiers remain as obsolete aliases carrying the new values.
- `MGDockTabItem` keeps `PART_Surface`, `PART_Accent`, `PART_CloseIcon`, `PART_PinIcon` and additionally registers `PART_TitleText`, `PART_CloseButton` and `PART_PinButton`, the same roles as the drawer and the window.
- `MGDockDropIndicators` registers its nine zones as `PART_LeftDropZone`, `PART_RightDropZone`, `PART_TopDropZone`, `PART_BottomDropZone`, `PART_CenterDropZone`, `PART_HostLeftDropZone`, `PART_HostRightDropZone`, `PART_HostTopDropZone`, `PART_HostBottomDropZone`; no `PART_Overlay`, the control itself is the overlay.
- `MGDockTabGroup` keeps its three parts and additionally registers `PART_HeadersPanel`, the `MGTabControl` role.
- `MGDockSplitterBar`, `MGDockPreviewOverlay`, `MGDockAutoHideStrip` and `MGDockHost` are unchanged. Behaviour, layout orchestration and docking semantic states stay on the owning controls; this decision is naming and registration only.

## Consequences

- Editor tooling and templates can address the same role with the same name across windows, overlays, text boxes, tab controls and docking surfaces.
- The structural docking migration (tasks 8 and 9 of the styling backlog) starts from a frozen part set: required parts and structure creators must use these names.
- Renaming a docking part later is a deliberate change: the vocabulary is pinned by tests.
- The obsolete drawer aliases keep external callers of `GetRequiredPart` working; they may be removed in a later major change.
- Known limit: the drop-zone elements are `internal`; registering them as parts exposes them through `TemplateParts` as `MGElement` only.
