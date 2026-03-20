# Gameplay/UI Input Routing Architecture

## Goal

Define a concrete input architecture for MonoGame + MGUI where:

- gameplay input and UI input can coexist without leaking into each other
- modal and navigable UI can block gameplay predictably
- lightweight HUD interactions can coexist with movement and combat
- keyboard, mouse, and gamepad all use the same semantic routing model

This document is intentionally pragmatic. It focuses on a design that can be implemented incrementally on top of the current MGUI focus/input model.

## Core Rule

The UI owns interaction mechanics.
The game owns gameplay meaning.

In practice:

- MGUI decides hover, focus, navigation, hit-testing, submit, cancel, and pointer capture
- gameplay systems decide what `UseItem`, `EquipSlot`, `OpenInventory`, `Interact`, or `Attack` actually do
- an input router sits above both and decides who receives each semantic action in the current context

## Recommended Stack

Use 4 layers:

1. Raw device state
2. Semantic action mapping
3. Context-based routing
4. UI/gameplay consumers

### 1. Raw Device State

Collect device state once per frame:

- keyboard
- mouse
- gamepad

This layer should not know about gameplay or UI.

Example responsibilities:

- current key/button state
- transitions: pressed/released/held
- pointer position
- scroll delta
- active gamepad device

### 2. Semantic Action Mapping

Convert raw input into game actions and UI actions.

Examples:

- gameplay actions:
  - `Move`
  - `Look`
  - `Jump`
  - `Attack`
  - `OpenInventory`
  - `QuickSlot1`
- UI actions:
  - `NavigateUp`
  - `NavigateDown`
  - `NavigateLeft`
  - `NavigateRight`
  - `NavigateNext`
  - `NavigatePrevious`
  - `Submit`
  - `Cancel`
  - `OpenContext`
  - `Point`
  - `ClickPrimary`
  - `ClickSecondary`
  - `Scroll`

The key point is that gameplay code should not care whether `Submit` came from `Enter`, `A`, or left mouse.

## 3. Context-Based Routing

Introduce an `InputRouter` that evaluates a stack of input contexts every frame.

Suggested contexts:

- `GameplayContext`
- `HUDContext`
- `MenuContext`
- `ModalUIContext`
- `TextEntryContext`
- `CutsceneContext`

The router should ask contexts in priority order whether they want to consume a semantic action.

Recommended order:

1. modal UI
2. focused text entry UI
3. navigable UI
4. HUD interaction layer
5. gameplay

The first context that consumes an action wins.

## 4. UI and Gameplay Consumers

At the end of routing:

- MGUI receives UI actions when UI has priority
- gameplay systems receive gameplay actions only if no higher-priority UI context has captured them

This keeps the rules explicit and testable.

## Concrete Model For MGUI

### InputRouter

Recommended shape:

```csharp
public interface IInputContext
{
    int Priority { get; }
    bool IsActive();
    bool TryHandle(InputAction action, InputActionContext context);
}

public sealed class InputRouter
{
    private readonly List<IInputContext> _contexts = new();

    public void Register(IInputContext context) => _contexts.Add(context);

    public bool Route(InputAction action, InputActionContext context)
    {
        foreach (IInputContext inputContext in _contexts
            .Where(x => x.IsActive())
            .OrderByDescending(x => x.Priority))
        {
            if (inputContext.TryHandle(action, context))
            {
                return true;
            }
        }

        return false;
    }
}
```

### InputAction

Use a shared semantic type, not raw keys:

```csharp
public enum InputAction
{
    Move,
    Look,
    Attack,
    Interact,
    OpenInventory,
    OpenPauseMenu,
    QuickSlot1,
    QuickSlot2,
    NavigateUp,
    NavigateDown,
    NavigateLeft,
    NavigateRight,
    NavigateNext,
    NavigatePrevious,
    Submit,
    Cancel,
    OpenContext,
    Point,
    ClickPrimary,
    ClickSecondary,
    Scroll,
}
```

### InputActionContext

Carry frame-local metadata:

```csharp
public readonly record struct InputActionContext(
    Point MousePosition,
    Point MouseDelta,
    int ScrollDelta,
    bool IsRepeat,
    bool IsFromGamePad,
    bool IsFromKeyboard,
    bool IsFromMouse);
```

## Recommended Contexts

### ModalUIContext

Purpose:

- block gameplay while a modal dialog, popup, or overlay is active
- route navigation and submit/cancel directly to MGUI

Should be active when:

- an MGUI modal overlay is open
- a blocking menu is active

Should consume:

- all UI navigation actions
- pointer clicks inside modal UI
- usually also gameplay actions that should not leak through

### TextEntryContext

Purpose:

- give priority to focused text boxes and text-entry widgets

Should be active when:

- `MGDesktop.FocusedKeyboardHandler` is an `MGTextBox`

Should consume:

- text entry keys
- caret navigation keys that must stay local
- submit if the control wants it

Should not consume:

- unrelated actions that the focused control does not preserve

### MenuContext

Purpose:

- route keyboard/gamepad navigation to MGUI menus, context menus, tab bars, inventory panels, and navigable lists

Should be active when:

- the active input mode is `Navigation`
- there is a focused MGUI navigation target
- a focus scope exists for a menu/popup/tree/list/grid-like control

Should consume:

- `Navigate*`
- `Submit`
- `Cancel`
- sometimes `OpenContext`

### HUDContext

Purpose:

- allow pointer-driven or dedicated quick-access HUD interactions without pausing gameplay

Examples:

- clicking a consumable slot
- hovering the minimap
- cycling a quickbar selection

Important:

- this context should usually be selective, not modal
- it should only consume actions that directly target the HUD widget under the pointer or current UI selection

### GameplayContext

Purpose:

- default fallback for world actions

Should be active when:

- gameplay is not globally locked

Should consume:

- movement
- look
- attack
- interact
- hotbar selection if that system belongs to gameplay rather than UI navigation

## Suggested Priority Values

Example numeric priorities:

- `ModalUIContext`: 500
- `TextEntryContext`: 400
- `MenuContext`: 300
- `HUDContext`: 200
- `GameplayContext`: 100

The actual numbers do not matter as long as the ordering is stable and obvious.

## How This Maps To RPG-Like Gameplay

### Case A: Full Inventory Screen

Expected behavior:

- player movement blocked
- gamepad d-pad and keyboard arrows navigate UI
- submit uses/selects item
- cancel closes screen

Recommended routing:

- `ModalUIContext` active
- `GameplayContext` still exists but never wins

### Case B: Clickable HUD While Moving

Expected behavior:

- WASD or left stick still moves the player
- mouse can click a HUD element
- clicking HUD does not trigger a gameplay click in the world

Recommended routing:

- `HUDContext` consumes `ClickPrimary` only when pointer is over an interactive HUD element
- `GameplayContext` still receives movement actions

### Case C: Gamepad Quickbar While Playing

Two valid models exist:

1. gameplay-owned selection
2. UI-owned navigation overlay

Prefer gameplay-owned selection when:

- slots are really part of combat/gameplay state
- the player is selecting equipment or abilities while still moving

Prefer UI-owned navigation when:

- the quickbar behaves like a navigable widget set
- you want standardized focus, highlight, and submit/cancel behavior

In most action RPGs, the quickbar selection itself is gameplay state, while the visible quickbar is a UI representation.

## Recommended Ownership Rules

Use this split consistently:

### UI owns

- hover state
- focus state
- tab order
- d-pad/arrow navigation between widgets
- visual highlight / pressed / selected state
- pointer capture
- menu opening/closing
- widget-local submit/cancel handling

### Game owns

- inventory contents
- equipped item
- cooldowns
- action validation
- character movement
- combat state
- whether an action is allowed right now

### Shared boundary

The UI raises intent.
The game applies state.
The UI reflects resulting state.

Example:

- HUD button click => `RequestUseConsumable(slotId)`
- gameplay system validates and performs it
- HUD updates from gameplay state afterward

## Integration With Current MGUI Architecture

MGUI already has the right low-level building blocks:

- focused keyboard handler
- navigation actions
- focus scopes
- overlay/modal blocking
- pointer vs navigation input mode

Recommended next step is not to move gameplay into MGUI.
Instead, put a thin router above MGUI and gameplay.

### Practical integration plan

1. Keep MGUI responsible for semantic UI navigation and pointer hit-testing.
2. Add a game-level `InputRouter` in the MonoGame update loop.
3. Convert raw device input to semantic `InputAction`s once per frame.
4. Route those actions through contexts in priority order.
5. Let `MenuContext` call into MGUI desktop/navigation services.
6. Let `GameplayContext` call player/world systems only if UI did not capture the action.

## Suggested Update Loop

```csharp
public void Update(GameTime gameTime)
{
    RawInputState raw = _inputCollector.Collect();
    IReadOnlyList<(InputAction Action, InputActionContext Context)> actions = _inputMapper.Map(raw);

    foreach ((InputAction action, InputActionContext context) in actions)
    {
        _inputRouter.Route(action, context);
    }

    _gameplay.Update(gameTime);
    _mguiDesktop.Update(gameTime);
}
```

If MGUI must be updated earlier in your frame for internal reasons, keep the routing order stable anyway:

- collect raw input once
- map once
- route once

Do not let gameplay and UI independently poll raw devices and make conflicting decisions.

## Anti-Patterns To Avoid

- gameplay and UI both reading raw keyboard/mouse state independently
- widgets directly mutating authoritative gameplay state without a boundary
- using raw keys in gameplay and UI everywhere instead of semantic actions
- treating all HUD actions as either fully modal or fully passive
- duplicating device-specific logic in each screen/control

## Recommended Minimum Implementation

If you want the smallest useful version first:

1. Add `InputAction` + `InputActionContext`
2. Add `InputRouter`
3. Add three contexts only:
   - `ModalUIContext`
   - `MenuContext`
   - `GameplayContext`
4. Route UI navigation actions to MGUI
5. Route gameplay actions only if UI did not consume them

That is enough to support:

- gameplay movement
- pause/inventory/menu navigation
- blocking modal dialogs
- gamepad keyboard parity

## Summary

For modern game UI:

- the UI should manage focus, highlight, navigation, and pointer interaction
- the game should manage world logic and authoritative state
- a router above both decides which semantic actions reach which side in the current context

That is the cleanest way to support gameplay, HUD interaction, modal menus, and gamepad navigation without ad-hoc exceptions spread across the codebase.