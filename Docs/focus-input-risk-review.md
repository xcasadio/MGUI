# Focus/Input Residual Risk Review

This review covers the remaining areas that were intentionally re-checked after the main focus/input refactor.

## Findings

1. ComboBox dropdowns are aligned with the new model.
   The dropdown is still implemented as a nested window, but keyboard routing remains anchored on the owning ComboBox. That matches the centralized eligibility policy and avoids duplicate raw navigation paths.

2. ContextMenu focus scopes already behave correctly.
   Context menus push a focus scope, move focus to a valid visible item, and restore the previous scope on close. No additional runtime fix was required.

3. Non-modal nested windows still need discipline from future controls.
   The current runtime is safe for the active focused element, but arbitrary non-modal nested windows do not establish a broader keyboard-occlusion contract by themselves. Future popup-like controls should either claim focus explicitly or be modal when they are intended to block interaction behind them.

## Outcome

To make these scenarios easy to verify manually, a dedicated sample was added in the samples app. It exercises:

- TextBox, ComboBox and ListBox keyboard routing
- ContextMenu open/close and focus restoration
- Non-modal nested child window focus isolation
- Modal overlay input blocking