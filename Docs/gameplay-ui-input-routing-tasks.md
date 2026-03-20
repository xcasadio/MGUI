# Gameplay/UI Input Routing Tasks

## Objectif

Traduire l'architecture definie dans [Docs/gameplay-ui-input-routing-architecture.md](d:\development\repo\MGUI\Docs\gameplay-ui-input-routing-architecture.md) en plan d'implementation incremental pour MonoGame + MGUI.

Le but est d'obtenir un systeme ou:

- le gameplay et l'UI ne lisent pas les inputs bruts chacun de leur cote sans arbitrage ;
- les actions de navigation UI, de texte, de HUD interactif et de gameplay passent par un routeur semantique unique ;
- l'UI peut capturer ce qu'elle doit capturer, sans bloquer abusivement le gameplay ;
- les menus modaux, l'inventaire, les overlays, le HUD et le gameplay pur ont des priorites explicites et testables.

Ce document est destine a un agent IA implementeur.

## Regles pour l'agent

- Executer les taches strictement dans l'ordre.
- Faire exactement 1 commit par tache terminee.
- Mettre a jour le statut de la tache dans ce fichier avant chaque commit.
- Utiliser l'icone de statut dans le titre de chaque tache.
- Si une tache est bloquee, marquer la tache avec `⛔`, decrire le blocage juste en dessous, puis s'arreter.
- Ne pas faire de refactor massif hors perimetre.
- Privilegier les types et contrats stables avant les integrations profondes.
- Garder MGUI responsable de la mecanique d'interaction UI, et laisser le jeu responsable du sens gameplay.
- Preserver la compatibilite avec l'input actuel tant qu'une tache ne demande pas explicitement de la remplacer.
- Ajouter des tests ou harnesses cibles a chaque tache quand c'est pertinent.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale apres chaque tache

Utiliser une validation ciblee et bornee.

1. `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
2. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
3. Si la tache touche la navigation ou l'input MGUI: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Focus --logger "console;verbosity=minimal"`
4. Si la tache ajoute des modeles purs: ajouter un filtre cible dedie aux nouveaux tests
5. Commit avec le message recommande par la tache

## Perimetre vise

- modeliser des actions semantiques d'input
- router ces actions selon des contextes explicites
- brancher MGUI comme consommateur UI semantique
- laisser le gameplay comme fallback ou contexte parallele selon le mode
- definir un chemin propre pour les HUD interactifs non modaux

## Hors perimetre initial

- rework complet des bindings clavier/souris/manette du jeu sample
- rebinding utilisateur complet
- persistence des mappings en fichier de config
- support reseau/multiplayer pour le routing d'input
- refactor de toute la boucle `Game.Update` si une facade suffit au debut

## V1 minimale retenue

Pour eviter le sur-engineering, cette implementation v1 se limite a 5 briques:

- types semantiques purs dans `MGUI.Shared`
- un routeur pur avec priorite et premier consommateur gagnant
- un contexte UI MGUI unique branche sur `MGDesktop`
- un contexte gameplay explicite en fallback
- une documentation d'integration qui montre comment composer les contextes

Ce qui reste volontairement hors v1:

- separation fine `ModalUIContext` / `MenuContext` / `HUDContext`
- remapping utilisateur complet
- unification immediate de tous les clicks souris MGUI via le routeur semantique
- remplacement massif de l'input brut deja stable dans toute la boucle runtime

## Ordre de commits attendu

1. `docs: complete task 1 define input routing vocabulary and contracts`
2. `feat: complete task 2 add semantic input action models`
3. `feat: complete task 3 add input router core`
4. `feat: complete task 4 integrate mgui semantic ui context`
5. `feat: complete task 5 add gameplay fallback context and docs`

## Taches

### ✅ 1. Definir le vocabulaire d'implementation et les contrats stables

But:

figer les types et responsabilites avant d'ajouter des couches runtime.

Travail attendu:

- retenir les types stables suivants:
  - `InputAction`
  - `InputActionSource`
  - `InputActionPhase`
  - `InputActionContext`
  - `InputActionEvent`
  - `IInputContext`
  - `InputRouter`
  - `InputCaptureResult`
  - `InputRouteDecision`
- heberger les types purs dans `MGUI.Shared.Input.Semantic`
- heberger les contextes branches sur `MGDesktop` dans `MGUI.Core.UI.InputRouting`
- garder le mapping brut -> semantique testable et independant du runtime UI
- laisser `MGDesktop` responsable du traitement UI effectif d'une action semantique
- laisser le gameplay responsable de la signification gameplay finale via un contexte fallback

Criteres d'acceptation:

- le contrat de chaque type est documente;
- la separation UI/gameplay/routeur est explicite;
- aucune ambiguite ne reste sur l'endroit ou les types doivent vivre.

Commit recommande:

- `docs: complete task 1 define input routing vocabulary and contracts`

### ✅ 2. Ajouter les modeles semantiques d'input

But:

poser les types de donnees necessaires sans encore brancher toute la boucle runtime.

Travail attendu:

- ajouter les types de base dans `MGUI.Shared`
- couvrir au minimum:
  - navigation UI
  - `Submit`, `Cancel`, `OpenContext`
  - quelques actions gameplay de fallback (`GameplayPrimary`, `GameplaySecondary`, `Pause`)
  - metadata source et phase
  - notion de repeat
- ajouter un mapper semantique pur pour clavier et gamepad
- ajouter des tests rapides de mapping

Criteres d'acceptation:

- les modeles compilent sans integration lourde;
- ils couvrent les besoins MGUI + gameplay identifies dans l'architecture;
- ils ne dependent pas d'un control concret.

Commit recommande:

- `feat: complete task 2 add semantic input action models`

### ⚪ 3. Ajouter le routeur d'input minimal

But:

introduire le coeur d'arbitrage commun sans multiplier les types de contexte.

Travail attendu:

- implementer `IInputContext`, `InputRouter`, `InputCaptureResult` et `InputRouteDecision`
- supporter:
  - activation/inactivation d'un contexte
  - priorite numerique
  - ordre stable entre contextes de meme priorite
  - premier consommateur gagnant
- ajouter des tests purs sur l'arbitrage

Criteres d'acceptation:

- le routeur arbitre correctement plusieurs contextes;
- l'ordre de priorite est deterministe;
- la suite de tests reste rapide.

Commit recommande:

- `feat: complete task 3 add input router core`

### ⚪ 4. Integrer MGUI comme contexte UI semantique

But:

faire de `MGDesktop` un consommateur d'actions UI semantiques sans casser l'input brut existant.

Travail attendu:

- ajouter un point d'entree cote `MGDesktop`, par exemple `TryHandleInputAction(...)`
- ajouter un contexte UI unique branche sur l'etat du desktop
- faire consommer au moins:
  - `Navigate*`
  - `Submit`
  - `Cancel`
  - `OpenContext`
  - `Home`, `End`, `PageUp`, `PageDown`, `Increment`, `Decrement`
- centraliser la decision de blocage gameplay pour:
  - overlays modaux
  - menus/context menus ouverts
  - text entry focalise
- ajouter des tests cibles sur l'API et le contrat de capture

Criteres d'acceptation:

- MGUI peut etre pilote par actions semantiques sans connaitre la touche brute;
- les actions UI ne fuient pas vers le gameplay;
- le blocage gameplay repose sur une politique desktop explicite.

Commit recommande:

- `feat: complete task 4 integrate mgui semantic ui context`

### ⚪ 5. Ajouter le contexte gameplay fallback et la doc d'integration

But:

rendre explicite la place du gameplay dans la chaine sans forcer un framework de jeu particulier.

Travail attendu:

- ajouter un contexte gameplay fallback simple, base sur delegate ou callback
- documenter la composition recommandee:
  - contexte UI MGUI prioritaire
  - contexte gameplay en fallback
- documenter un exemple d'integration runtime minimal
- ajouter un test pur montrant qu'un contexte gameplay ne recoit pas une action captee par l'UI

Criteres d'acceptation:

- le gameplay ne lit plus en concurrence implicite quand le routeur est utilise;
- la composition UI puis gameplay est documentee;
- un utilisateur du framework voit clairement ou brancher sa logique gameplay.

Commit recommande:

- `feat: complete task 5 add gameplay fallback context and docs`
- lui faire consommer les actions gameplay uniquement si les contextes UI prioritaires n'ont rien capture
- documenter la frontiere entre:
  - selection gameplay via HUD
  - navigation UI veritable
- prevoir une interface simple pour brancher le jeu reel ou les samples.

Criteres d'acceptation:

- le gameplay devient un fallback clair;
- un menu modal ou navigable bloque correctement les actions monde;
- l'architecture ne depend plus d'un ordre implicite de polling brut.

Commit recommande:

- `feat: complete task 7 add gameplay fallback context`

### ⚪ 8. Ajouter un contexte HUD selectif non modal

But:

supporter les HUD interactifs sans transformer toute l'UI en mode modal.

Travail attendu:

- ajouter un `HUDContext` ou equivalent
- definir une politique selective:
  - clicks consommes si un widget HUD interactif est vise
  - mouvement joueur toujours autorise si non capture
  - d-pad/stick selon le modele choisi pour les quickbars
- ajouter au moins un exemple concret dans les samples ou une harness simple.

Criteres d'acceptation:

- le HUD peut coexister avec le gameplay;
- seuls les inputs lies au widget HUD sont captures;
- la politique de priorite reste lisible.

Commit recommande:

- `feat: complete task 8 add selective hud interaction context`

### ⚪ 9. Ajouter des regressions end-to-end sur le routing

But:

verrouiller les scenarios mixtes UI/gameplay.

Travail attendu:

- ajouter des tests sur les scenarios suivants:
  - menu modal ouvert => gameplay bloque
  - menu navigable non modal => `Navigate*` va a l'UI, gameplay non declenche
  - HUD interactif sous souris => click consomme par HUD, pas par gameplay
  - HUD non vise => gameplay recoit l'action
  - text entry focalise => touches preservees par l'UI avant gameplay
- preferer des harnesses de logique et des doubles simples plutot qu'un runtime complet.

Criteres d'acceptation:

- les priorites de contexte sont testees;
- les cas de fuite UI -> gameplay et gameplay -> UI sont couverts;
- les tests restent bornes.

Commit recommande:

- `test: complete task 9 add end-to-end routing regressions`

### ⚪ 10. Documenter l'integration runtime et les patterns d'usage

But:

laisser une voie claire pour adopter le routeur dans un vrai jeu MonoGame + MGUI.

Travail attendu:

- documenter:
  - ou collecter les inputs bruts
  - ou mapper vers des actions semantiques
  - a quel moment router vers l'UI ou le gameplay
  - comment choisir entre HUD gameplay-owned et UI-owned
- ajouter un exemple minimal de boucle `Update`
- si pertinent, relier ce document au sample ou a un futur sample dedie.

Criteres d'acceptation:

- un integrateur peut brancher le systeme sans deviner l'ordre des etapes;
- les compromis sont documentes;
- la separation UI/gameplay reste claire.

Commit recommande:

- `docs: complete task 10 document runtime integration and sample usage`