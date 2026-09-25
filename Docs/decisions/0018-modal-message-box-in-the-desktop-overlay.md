# ADR-0018: A modal message box hosted in the desktop overlay, and the floating-window cause of a panel close

- **Status**: Accepted
- **Date**: 2026-09-25
- **Source**: CasaEngine chantier `ai-agent/tasks/mgui-message-boxes-tasks.md` (decisions D3, D5, D7), branch
  `chantier/mgui-message-boxes`, decided with the author on 2026-09-25. CasaEngine counterpart: CasaEngine ADR-0039
  (`docs/decisions/0039-editor-message-boxes-are-mgui.md` in the parent repository).

## Context

- MGUI has no reusable message box. CasaEngine's editor wants one to replace its native WinForms boxes.
- `MGWindow.PushModalWindow` blocks only the owner window (`MGDesktop.IsBlockedByModalOrOverlay`, the
  `SelfOrParentWindow.HasModalWindow` check); other root windows and nested windows such as floating dock windows
  stay interactive.
- `MGDesktop.OverlayHost` (`MGOverlayHost`) draws overlays over the whole desktop. While a modal overlay is active,
  `IsBlockedByModalOrOverlay` blocks every element outside it, and `MGDesktop.ShouldCaptureGameplayInput()` is true,
  so a host that honours it stops its own shortcuts and pointer handling.
- `MGDockHost.PanelClosing` tells a subscriber which panel is closing, not why. When a whole floating window is
  closed, `MGDockHost.OnFloatingWindowClosing` stops at the first refusal and cancels the window close, so a
  subscriber that refuses in order to ask a question asynchronously cannot resume the window close afterwards.

## Decision

- Add `MGMessageBox`, a generic control that shows a title, a message and one to three labelled buttons, and
  reports the index of the chosen button once through a callback. Enter chooses the default button, Escape the
  cancel button.
- The message box is hosted in the desktop's modal overlay (`MGDesktop.OverlayHost`), not pushed as a modal window
  of a window, so it blocks the whole desktop, floating windows included. Showing it when the overlay host is not
  modal is an error.
- Opening a new message box from the callback of the one that just closed is supported: the new one becomes the
  active overlay and the input that closed the previous one never reaches it.
- MGUI keeps no queue of message boxes: a host that may ask several questions queues them itself.
- `MGDockHost.PanelClosing` keeps its type; its arguments become `DockPanelClosingEventArgs`, a subclass of
  `CancelEventArgs<DockPanelNode>` whose `ClosingFloatingWindow` names the floating window being closed as a whole,
  and is null for every other close path.
- Add `MGDockHost.ClosePanel(DockPanelNode)`: closes a panel by code wherever the host holds it (docked, auto-hidden,
  or in a floating window the host tracks) exactly as the user's close does once `PanelClosing` lets it through, without
  raising `PanelClosing`. Taken during the same chantier (plan task T1.2), when a test showed that
  `MGDockHost.DetachToFloating` removes the panel from the host's registry, so `RemovePanel` cannot close a floating
  panel and an asynchronous subscriber had no public way to close it after its answer.

## Consequences

- A host gets a themed question that blocks all of its desktop without extra code, and can answer it
  asynchronously.
- One overlay host serves every overlay of a desktop: a message box shown while another overlay is open becomes the
  active one; hosts that use overlays for something else must account for it.
- The `PanelClosing` change is additive: existing subscribers keep compiling and behaving the same; a subscriber that
  cares about whole floating-window closes casts the arguments.
- `ClosePanel` is additive too; `RemovePanel` keeps its behaviour (it still does not see floating panels). A floating
  window created by application code and never tracked by the host is not searched by `ClosePanel`.
- No icons in this version; they can be added later without changing the callback contract.
