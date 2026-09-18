# ADR-0013: One rule for keyboard focus navigation, and text entry controls as navigation targets

- **Status**: Proposed
- **Date**: 2026-09-18
- **Source**: this chantier: `Docs/Tasks/focus-navigation-text-entry-tasks.md`

## Context

MGUI carried two notions of "this element can take keyboard focus", and they disagreed for text entry controls.

`MGElement.IsFocusable` was added by commit `4633f13` (8 March 2026) as an opt-in for non-text controls, documented as
"can receive keyboard focus **when clicked**". Before that commit, `MGElement.CanHandleKeyboardInput` was `=> false` by
default and `MGTextBox` overrode it to `true`; that override was the single notion of keyboard activity. The new flag
fed the old one (`CanHandleKeyboardInput => IsFocusable`), and `MGTextBox` was never updated, because its override
already satisfied every rule that existed at the time.

`MGDesktop.IsNavigationTarget`, introduced later with the focus navigation service, then reinterpreted `IsFocusable` as
"is a Tab stop" while also calling `CanElementReceiveKeyboardInput`, which already tests `CanHandleKeyboardInput`. The
`IsFocusable` term was therefore **redundant for every control that does not override `CanHandleKeyboardInput`**, and
exactly two types in the repository do: `MGTextBox` (`=> true`) and `MGMenuBar` (`=> IsFocusable || IsMenuActive`).

The observable defect: a text box was never a valid navigation target, so `MGDesktop.ResolveAutoFocusTarget` could
neither restore it as a window's last-focused element nor pick it as the first focusable one. Because
`MGWindow.ActivatesOnClick` (default `true`) resolves a new auto-focus target on any click inside the window that
queued no focus of its own, any such click moved focus **away** from a text box, and focus could never return by that
path. It was found while fixing the XAML editor, where a click on the non-interactive preview pane silently defocused
the text pane; that editor worked around it with `MGTextCaret.ShowWhenUnfocused` (commit `744af73`, ADR-0010).

Two places in the repository already worked around the framework by hand, setting `IsFocusable = true` on text boxes:
`MGUI.Samples/Features/FocusInputReview.xaml.cs` and `MGUI.Tests/Tooling/StableDiagnosticIdTests.cs`. `IsFocusable` is
not exposed in XAML, so a XAML-only consumer had no opt-in at all.

## Decision

1. **One rule.** `MGDesktop.IsNavigationTarget` no longer tests `MGElement.IsFocusable`. It keys off
   `MGElement.CanHandleKeyboardInput` (through `CanElementReceiveKeyboardInput`) plus the enabled / hit-test-visible /
   visible conditions. Since `CanHandleKeyboardInput` defaults to `IsFocusable`, opting a control in with
   `IsFocusable = true` still makes it a navigation target; the converse no longer holds, so a control that overrides
   `CanHandleKeyboardInput` is not excluded from navigation any more.
2. **`MGTextBox` sets `IsFocusable = true`.** The public flag now states what was already true of the control, and
   `MGPasswordBox`, `MGRichTextBox` and `MGNumericUpDown` inherit it. `CanHandleKeyboardInput => true` is kept, so a
   consumer cannot silently disable text input by clearing the flag.
3. **`MGTextBox.AcceptsTab` defaults to `false`** (it was `true`), matching WPF. Without this, Tab could reach a text
   box and never leave it, because `ShouldPreserveTextEntryKey` reserves Tab whenever `AcceptsTab` is set.
4. **Text entry hosts are deprioritised as the "first focusable" fallback.** `UIFocusNavigationService` resolves that
   fallback to the first navigation target that is not an `ITextEntryHost`, and only failing that to the first target
   at all. Focusing an editable text entry host makes `MGDesktop.ShouldCaptureGameplayInput()` true, so a mere click on
   a window's empty body must not land in a text box while another control could take it. This governs the fallback and
   nothing else: `MGWindow.DefaultFocusElement` and the window's focus history restore a text entry host normally, and
   the Tab order is not filtered.
5. **Ctrl+Tab and Ctrl+Shift+Tab will leave a control that reserves Tab** (WPF convention), so controls that
   legitimately set `AcceptsTab = true` - `MGRichTextBox`, the `MGGraphControls` comment box, the editable markup box of
   `MGXAMLDesigner` - do not stay keyboard traps. **Decided but NOT yet implemented**: this is task 2 of
   `Docs/Tasks/focus-navigation-text-entry-tasks.md`, and no Ctrl+Tab handling exists in the code that decisions 1-4 and
   6 ship. Until it lands, those controls are reachable by Tab with no keyboard way out.
6. **Auto-focus resolution has one implementation.** `MGDesktop.ResolveAutoFocusTarget(MGElement, bool)` delegates to
   `UIFocusNavigationService`; the duplicate copy of it and of `GetFocusableElements` in `MGDesktop` are deleted, so the
   rule cannot drift between them.

## Consequences

- Every text entry control - `MGTextBox`, `MGPasswordBox`, `MGRichTextBox`, `MGNumericUpDown` - is reachable by Tab and
  by directional navigation, can be restored as a window's last-focused element, and can be `DefaultFocusElement`.
  The reported defect disappears: a click elsewhere in the window resolves back to the text box through the focus
  history instead of moving focus off it.
- The internal editors of composite controls become tab stops: one per row in `MGPropertyGrid`, `MGChatBox.InputTextBox`,
  both text boxes of `MGXAMLDesigner` (one of which is read-only), and the two of the `MGGraphControls` comment box.
  `IsNavigationTarget` does not filter `IsReadonly`, so a read-only text box is a tab stop too - as in WPF, where this
  is what lets a user select and copy its text from the keyboard. Accepted deliberately, with no exception carved out
  of rule 1.
- `MGMenuBar` is a navigation target while its menu is open even if a consumer set `IsFocusable = false`, because its
  `CanHandleKeyboardInput` override says it is keyboard-active. Accepted.
- A text box no longer inserts four spaces on Tab unless `AcceptsTab="True"` is set explicitly. `MGRichTextBox` and
  `MGGraphControls` already set it; `MGXAMLDesigner.FromStringTextBoxComponent` did not and was relying on the old
  default to indent hand-typed markup, so this decision sets it explicitly there - a consumer that an inventory of
  `AcceptsTab` occurrences structurally cannot find, since it never mentioned the property.
  `MGPasswordBox` forces it off, `MGNumericUpDown` and `MGPropertyGrid` set it off.
  `MGXAMLDesigner.FromFileTextBoxComponent` needs no opt-in: it is readonly, and a readonly host never reserves Tab.
- Decision 5, once implemented, will only cover the raw input path. `InputActionContext` carries no modifier, so the semantic path
  (`MGDesktop.TryHandleInputAction`, used by `MGUI.MiniGame` and the replay tooling) cannot tell Ctrl+Tab from Tab.
  Extending that public record is a separate chantier. The raw path is the default
  (`MGDesktop.UseRawNavigationInput = true`).
- Gamepad navigation was never trapped and is unaffected: the text-entry key preservation guard only applies when a
  `Keys?` is supplied, which is null on the gamepad path.
- The manual `IsFocusable = true` workarounds in the sample and in the tooling tests are removed. The XAML editor's
  `MGTextCaret.ShowWhenUnfocused` workaround is left in place: it is still wanted while the tree pane can hold focus,
  and it belongs to the editor chantier.
- `IsFocusable` remains unexposed in XAML. This decision makes that irrelevant for text boxes, but a third-party
  navigable control still has no declarative opt-in.
