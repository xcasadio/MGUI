# ADR-0004: Resolve hit-test occlusion from the window that displays an element

- **Status**: Accepted (decided by the author on 2026-09-07 while approving `Docs/Tasks/docking-bugs-tasks.md`, task 2)
- **Date**: 2026-09-07
- **Source**: this chantier: root-cause investigation of the "floated panel cannot be docked back" bug and the approved plan `Docs/Tasks/docking-bugs-tasks.md` (task 2); supersedes nothing, refines the occlusion rule recorded in `Docs/input-architecture.md` ("Fenetres superposees", decision of 2026-09-02, commit `a331639`)

## Context

Facts verified at HEAD `13f970b`:

- `MGElement.ParentWindow` is assigned once in the constructor (`MGUI.Core/UI/MGElement.cs:2047`) and is getter-only (`:607`); `SetParent` and `SetContent` only change the visual parent. An element built by an application with one window can therefore be displayed inside another window (re-parented into a nested window) while still reporting its construction window as `SelfOrParentWindow`.
- Since commit `a331639` (2026-09-02, z-order-aware hit-test), `MGElement.IsInside` (`MGElement.cs:1470-1481`) rejects a position covered by a window drawn over the element's window, asking `SelfOrParentWindow.IsUnscaledPositionOccluded` (`MGUI.Core/UI/MGWindow.cs:1204-1224`), which treats every nested window of that window as an occluder. The only exemption is the active context menu chain, carried as `Origin` (`MGWindow.cs:1236-1272`, `MGDesktop.cs:1488-1526`).
- `MGFloatingDockWindow` built its content with the dock host's window (`MGUI.Core/UI/Docking/Controls/MGFloatingDockWindow.cs:73`) and is registered as a nested window of that same window (`MGDockHost.cs:1301`, `:1325`). Every element displayed inside a floating window therefore self-occluded: no hover, no click, no context menu, no drag (`MouseHandler.cs:566`, `:827`). Task 1 of the plan fixes the docking side; application content of a panel (created by the application with the main window, cached in `DockPanelNode.GetOrCreateContent`, re-parented by its root at `MGDockTabGroup.cs:536`) still resolves its occlusion from the wrong window.
- `HoveredElement` and `PressedElement` are assigned by the window that displays the visual tree (`MGWindow.cs:1492`, `:1510`) and nulled when that window is occluded (`:1501`, `:1664`), but the visual state of an element read them from `SelfOrParentWindow` (`MGElement.cs:2409`, `:2500-2508`).
- `_Parent` is written in exactly one place, `MGElement.SetParent` (`MGElement.cs:649`); window content, components, content hosts, template parts and structures all go through it.

## Decision

- An element resolves hit-test occlusion from its displaying window: the first `MGWindow` found by walking its visual `Parent` chain (itself when it is a window), with `ParentWindow` as the fallback for a detached element. `IsInside` asks that window, not the construction window, whether the position is occluded. The active context menu exemption is unchanged.
- The visual-state path follows the same rule: `ComputeTopmostHoveredElement` and the `VisualState` computation read `HasModalWindow`, `HoveredElement` and `PressedElement` from the displaying window, which is the window that assigns them.
- The displaying window is cached per element and validated by a topology generation counter incremented by every `SetParent`; a re-parented subtree root therefore invalidates its whole subtree implicitly, and a mouse event costs one integer comparison plus a cache read outside topology changes.
- `SelfOrParentWindow` and `ParentWindow` keep their meaning for everything else (keyboard focus, activation, theme and resource resolution, drag capture ownership at `MGElement.cs:2627`, tooltips at `:2544`, `_CanReceiveMouseInput` at `:2566`).
- Rejected alternative: teaching `MGWindow.IsUnscaledPositionOccluded` to skip nested windows that are visual ancestors of the tested element. Same result, but the rule would live on the window side and require passing the element down the recursion; resolving the origin once, on the element, keeps the occlusion code untouched.

## Consequences

- Application content re-parented into a floating dock window (or any nested window) is interactive again, including hover and pressed feedback; the docking re-dock paths (drag from a floating window, context menu) work for application content too.
- Elements displayed in their construction window behave exactly as before: the displaying window equals the construction window, so the existing occlusion suites pin the unchanged behaviour.
- Known asymmetry: `VisualState` reads the displaying window's `HasModalWindow` while `_CanReceiveMouseInput` keeps the construction window's. The observable result stays correct: with a modal on the main window, `_CanReceiveMouseInput` is false, `ComputeTopmostHoveredElement` never selects the element and its secondary visual state stays `None` (`MGElement.cs:1739`).
- The generation counter is global to the process: a `SetParent` anywhere invalidates every cached displaying window, which only costs one chain walk per element on its next hit-test.
- A future explicit "displaying window changed" notification could replace the counter if per-element precision is ever needed.
