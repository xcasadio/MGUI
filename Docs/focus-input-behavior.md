# Focus and Input Behavior

## Objectif

Ce document decrit le contrat runtime de MGUI pour le focus et le routage des inputs apres le chantier de stabilisation focus/input.

Il doit servir de reference pour:

- maintenir un comportement coherent entre les controles ;
- eviter les regressions quand de nouveaux controles sont ajoutes ;
- guider les futures corrections autour du focus, des overlays, des popups et de la navigation.

## Principes generaux

- Le focus clavier est arbitre par `MGDesktop.FocusedKeyboardHandler`.
- Le fait qu'un controle soit focusable ne suffit pas: il doit aussi etre effectivement eligible a l'input sur le frame courant.
- Un event souris ou clavier deja gere ne doit pas fuiter vers d'autres controles, sauf opt-in explicite.
- Les controles composites doivent reposer d'abord sur `TryHandleNavigationAction(...)` pour la navigation clavier partagee.

## Eligibilite effective a l'input

Un controle peut etre cible par le focus ou la navigation clavier seulement si toutes les conditions suivantes sont vraies:

- `CanHandleKeyboardInput == true` ;
- le host peut effectivement recevoir le clavier sur ce frame ;
- le controle n'est pas bloque par un overlay modal ou par une modalite superieure ;
- le controle est visible et interactif du point de vue derive du tree runtime.

En pratique, cette notion est centralisee dans `MGDesktop` et s'appuie sur `FocusInputPolicy`.

## Cycle de vie du focus

- `QueueFocusedKeyboardHandler(...)` exprime une intention de focus.
- `ApplyQueuedFocusChange()` applique le focus seulement si la cible est encore eligible.
- Le desktop nettoie activement:
  - le focus courant ;
  - la queue de focus ;
  quand une cible devient invalide pendant le tick.
- Un overlay modal peut forcer le nettoyage immediat du focus courant si celui-ci vise encore le contenu derriere l'overlay.

## Routage clavier

### Ordre logique

- Le desktop detecte les touches de navigation globales.
- La navigation n'est routee que si l'event n'est pas deja gere.
- Le desktop revalide l'eligibilite effective du focus avant de deleguer l'action de navigation.
- Le controle focus tente de gerer l'action via `TryHandleNavigationAction(...)`.
- Si le controle focus ne la gere pas, le desktop peut appliquer un fallback de navigation global.

### Text entry

- Un `MGTextBox` peut preserver certaines touches pour l'edition de texte.
- Quand une touche est preservee pour la text entry, elle ne doit pas etre reinterpretee comme action de navigation.

## Routage souris

- Les handlers souris respectent deja la consommation prioritaire des events.
- Un handler de plus basse priorite ne recoit pas un event souris deja gere, sauf opt-in explicite comme `InvokeEvenIfHandled`.
- Les fenetres et overlays modaux restent responsables de bloquer la chute d'input vers le contenu derriere.

## Overlays, menus, dropdowns et scopes

- Un overlay modal actif bloque le contenu derriere pour le pointeur et pour le clavier.
- Un focus scope doit restaurer un focus cible seulement si cette cible est encore valide.
- Les menus, context menus et autres popups doivent isoler leur navigation via le contrat commun au lieu de reimplementer un routage brut parallele.
- Les dropdowns et popups ne doivent pas laisser fuiter l'input au contenu sous-jacent quand ils sont actifs et topmost.

## Controles composites

Les controles composites comme:

- `MGListBox`
- `MGListView`
- `MGTreeView`
- `MGComboBox`
- `MGMenuBar`

doivent suivre les regles suivantes:

- utiliser `TryHandleNavigationAction(...)` comme voie principale de navigation clavier ;
- reserver les handlers clavier bruts aux extras qui ne relevent pas de la navigation partagee ;
- ne pas dupliquer la meme action a la fois dans un handler brut et dans `TryHandleNavigationAction(...)` ;
- ne jamais supposer qu'un controle invisible, masque ou bloque par overlay peut encore recevoir l'input.

## Recommandations pour les futurs controles

- Si le controle est navigable au clavier, implementer `TryHandleNavigationAction(...)` avant d'ajouter un handler `KeyboardHandler.Pressed`.
- Si un handler brut est necessaire, il doit se limiter aux raccourcis specifiques au controle.
- Ne pas appeler `SetHandledBy(...)` par reflexe: ne le faire que si le controle revendique vraiment l'input.
- Verifier comment le controle se comporte quand:
  - il perd le focus ;
  - il devient invisible ;
  - un overlay modal s'ouvre ;
  - un popup topmost apparait au-dessus de lui.
- Ajouter des regressions de logique pure quand c'est possible, avant de compter sur un sample manuel.