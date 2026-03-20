# Gameplay/UI Input Routing V1

## Objectif

La v1 fournit une chaine minimale et explicite:

- les inputs bruts sont convertis en `InputAction`
- un `InputRouter` arbitre les contextes actifs par priorite
- `MGUIInputContext` consomme les actions UI et bloque le gameplay quand l'UI est exclusive
- `GameplayInputContext` sert de fallback pour les actions gameplay

Cette v1 n'essaie pas de tout unifier d'un coup. L'input souris detaille de MGUI continue d'exister, mais la navigation UI et l'arbitrage UI/gameplay ont maintenant une surface semantique stable.

## Repartition des responsabilites

- `MGUI.Shared.Input.Semantic`
  - types semantiques purs
  - mapper clavier/gamepad
  - routeur et contrats de contexte
- `MGUI.Core.UI.InputRouting`
  - `MGUIInputContext`
  - `GameplayInputContext`
- `MGDesktop`
  - point d'entree `TryHandleInputAction(...)`
  - politique `ShouldCaptureGameplayInput()`

## Composition recommandee

```csharp
using MGUI.Core.UI.InputRouting;
using MGUI.Shared.Input.Semantic;

InputRouter router = new();
router.RegisterContext(new MGUIInputContext(desktop, priority: 100));
router.RegisterContext(new GameplayInputContext(
    name: "Gameplay",
    tryHandle: actionEvent => actionEvent.Action switch
    {
        InputAction.GameplayPrimary => HandlePrimaryAction(),
        InputAction.GameplaySecondary => HandleSecondaryAction(),
        InputAction.Pause => TogglePauseMenu(),
        _ => false,
    },
    priority: 0));
```

## Regle pratique

- si l'action est UI, `MGUIInputContext` la reserve pour l'UI, meme si aucun controle ne la consomme finalement
- si un overlay modal, un menu ou une text entry active impose l'exclusivite, `MGUIInputContext` bloque le gameplay
- sinon, l'action gameplay descend vers `GameplayInputContext`

## Point d'integration runtime minimal

Dans la boucle du jeu:

1. collecter l'input brut
2. convertir l'input voulu en `InputActionEvent`
3. appeler `router.Route(actionEvent)`
4. laisser le premier contexte consommateur gagner

Exemple minimal pour une action gameplay:

```csharp
InputActionEvent actionEvent = new(
    InputAction.GameplayPrimary,
    new InputActionContext(InputActionSource.Keyboard, InputActionPhase.Pressed, gameTime.TotalGameTime, Key: Keys.Space));

InputRouteDecision decision = router.Route(actionEvent);
```

## Extension future recommandee

Si la v1 tient bien en prod, la suite logique est:

1. extraire des contextes UI plus fins (`Modal`, `Menu`, `HUDInteractif`)
2. etendre progressivement les actions pointeur/manette avancee
3. brancher les samples de gameplay sur la meme chaine