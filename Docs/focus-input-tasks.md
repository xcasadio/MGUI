# Focus and Input Tasks

## Objectif

Definir un plan d'execution pour un agent IA afin d'analyser puis corriger la gestion du focus et des inputs dans MGUI, avec un comportement proche des frameworks UI modernes:

- un controle qui n'a pas le focus ne doit pas reagir au clavier si l'input a deja ete gere ailleurs ;
- un controle masque, clippe, ou cache par un autre ne doit pas etre impacte par les events d'input ;
- les controles qui gerent le clavier comme `MGListBox`, `MGListView`, `MGComboBox`, `MGTreeView`, les menus et autres controles de navigation doivent avoir des regles coherentes ;
- la priorite, la consommation des events, l'occlusion visuelle et la visibilite effective doivent produire un resultat previsible.

Ce document est destine a un agent IA implementeur.

## Consignes de travail pour l'agent IA

- Executer les taches strictement dans l'ordre.
- Faire exactement 1 commit par tache terminee.
- Mettre a jour le statut de la tache dans ce fichier avant chaque commit.
- Utiliser l'icone de statut dans le titre de chaque tache.
- Si une tache est bloquee, marquer la tache avec `⛔`, decrire le blocage juste en dessous, puis s'arreter.
- Ne pas faire de refactor massif hors perimetre.
- Corriger la cause racine avant les cas particuliers quand c'est possible.
- Preserver les APIs publiques existantes sauf si une tache demande explicitement de les etendre.
- Ajouter ou adapter des tests a chaque tache quand c'est pertinent.
- Ne pas passer a la tache suivante tant que la tache courante n'est pas validee et committee.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale apres chaque tache

Utiliser une validation ciblee et bornee. Eviter les executions ouvertes non filtrees.

1. `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
2. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
3. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Focus --logger "console;verbosity=minimal"`
4. Si la tache touche des controles composites, ajouter un filtre de test cible dedie a la zone modifiee.
5. Commit avec le message recommande par la tache.

## Zones du code a auditer en priorite

- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Core/UI/MGOverlay.cs`
- `MGUI.Core/UI/Navigation/UIFocusNavigationService.cs`
- `MGUI.Core/UI/MGListBox.cs`
- `MGUI.Core/UI/MGListView.cs`
- `MGUI.Core/UI/MGComboBox.cs`
- `MGUI.Core/UI/MGTreeView.cs`
- `MGUI.Core/UI/MGTextBox.cs`
- `MGUI.Tests/Focus/FocusTests.cs`

## Hypotheses de travail a verifier pendant l'analyse

- `FocusedKeyboardHandler` est aujourd'hui l'arbitre principal du clavier, mais l'eligibilite reelle d'un controle depend aussi de `CanHandleKeyboardInput`, `_CanReceiveKeyboardInput`, `Visibility`, clipping, modalite et occlusion.
- Le pipeline actuel semble separer focus queue, update manuel des handlers, et navigation fallback. Il faut verifier l'ordre exact de ces etapes sur un tick complet.
- Les overlays modaux bloquent deja une partie des inputs, mais il faut confirmer si le focus courant est reellement invalide ou seulement la queue de focus.
- Certains controles composites gerent eux-memes une partie du clavier; il faut les remettre derriere un contrat commun au lieu d'empiler des exceptions.

## Ordre de commits attendu

1. `focus: complete task 1 audit input and focus pipeline`
2. `test: complete task 2 add focus and input regression matrix`
3. `focus: complete task 3 centralize effective input eligibility`
4. `focus: complete task 4 harden desktop focus lifecycle`
5. `input: complete task 5 enforce handled mouse event isolation`
6. `input: complete task 6 enforce keyboard routing and consumption`
7. `controls: complete task 7 align keyboard-driven composite controls`
8. `overlay: complete task 8 block hidden and occluded controls from input`
9. `test: complete task 9 add end-to-end regressions and sample coverage`
10. `docs: complete task 10 document focus and input behavior`

## Taches

### ✅ 1. Auditer le pipeline focus/input reel

But:
etablir un diagnostic precis avant de modifier les invariants du framework.

Travail attendu:

- cartographier l'ordre exact des phases sur un tick:
  - collecte des etats souris/clavier ;
  - handlers high priority ;
  - updates de fenetre et d'elements ;
  - `ManualUpdate` des handlers ;
  - application du focus queue ;
  - navigation fallback ;
- lister pour chaque type d'input les points de consommation existants:
  - mouse press/release/click/wheel ;
  - keyboard press/release/click/repeat ;
  - navigation actions ;
- verifier les invariants reels de `MGDesktop`, `MGElement`, `MGOverlay` et `UIFocusNavigationService`.
- produire dans ce document une courte section "Resultat" avec les constats confirmes avant commit.

Criteres d'acceptation:

- l'ordre de traitement des inputs et du focus est documente ;
- les ecarts entre comportement actuel et comportement cible sont listes ;
- les points de decision globaux et les cas locaux sont clairement distingues.

Commit recommande:

- `focus: complete task 1 audit input and focus pipeline`

Resultat:

- ordre confirme du tick desktop:
  - recalcul responsive et determination du mode d'input ;
  - `QueueAutoFocusIfNeeded()` puis `ApplyQueuedFocusChange()` ;
  - `HighPriorityMouseHandler.ManualUpdate()` ;
  - `HighPriorityKeyboardHandler.ManualUpdate()` ;
  - second `ApplyQueuedFocusChange()` ;
  - navigation gamepad puis troisieme `ApplyQueuedFocusChange()` ;
  - update des fenetres, puis update des handlers manuels de fenetre et d'elements ;
- le clavier n'a pas aujourd'hui d'equivalent global complet au blocage d'occlusion souris ;
- `FocusedKeyboardHandler` est valide sur `CanHandleKeyboardInput`, mais pas suffisamment revalide contre l'eligibilite effective au moment du dispatch ;
- les controles composites n'ont pas une politique homogene de `SetHandledBy(...)`, ce qui rend les fallbacks de navigation inconsistants ;
- les overlays modaux et popups topmost bloquent deja une partie du pointeur, mais le focus et le clavier peuvent encore viser un contenu devenu non legitime dans certains cas ;
- la meilleure strategie est de centraliser une notion unique d'eligibilite effective a l'input, puis de faire reposer dessus le focus, la navigation fallback, le clavier et les controles composites.

### ✅ 2. Ajouter une matrice de tests de regression focus/input

But:
verrouiller les comportements attendus avant les correctifs.

Travail attendu:

- etendre `MGUI.Tests/Focus/FocusTests.cs` et creer d'autres tests si necessaire ;
- couvrir au minimum les scenarios suivants:
  - un controle non focus n'est pas impacte par un event clavier deja gere par un autre controle ;
  - un controle derriere un overlay modal ne recoit pas l'input ;
  - un controle focus puis masque, clippe ou rendu ineligible perd ou ne conserve pas abusivement le focus ;
  - un controle cache par un autre element topmost ne reagit pas aux inputs pointeur ;
  - `MGListBox`, `MGListView`, `MGComboBox` et `MGTreeView` ne reagissent au clavier que dans les conditions legitimes ;
  - l'ouverture d'un dropdown, d'un menu ou d'un context menu ne laisse pas fuiter l'input au contenu derriere.
- privilegier des tests de logique pure et de petites harnesses au lieu de tests dependants du runtime MonoGame.

Criteres d'acceptation:

- les tests echouent avant les correctifs sur au moins une partie des regressions visees ;
- les tests decrivent clairement le contrat attendu ;
- la suite reste rapide et bornee.

Commit recommande:

- `test: complete task 2 add focus and input regression matrix`

Resultat:

- une matrice de regressions pure a ete ajoutee autour de la politique focus/input ;
- les tests couvrent l'eligibilite clavier, la retention/perte de focus, le gating navigation pour text entry, le gating input d'overlay/fenetre et l'isolation des handlers clavier composites ;
- validation executee avec succes sur `MGUI.Tests`, `MGUI.Samples` et le filtre `Focus`.

### ✅ 3. Centraliser la notion d'eligibilite effective a l'input

But:
avoir une seule source de verite pour savoir si un controle peut reellement recevoir souris et clavier sur le frame courant.

Travail attendu:

- auditer puis rationaliser le calcul de:
  - `_CanReceiveMouseInput` ;
  - `_CanReceiveKeyboardInput` ;
  - visibilite effective ;
  - clipping et element hors viewport ;
  - blocage par modalite ;
  - eventuelle occlusion par overlay ou popup topmost ;
- introduire si necessaire un helper ou un type de diagnostic qui evite de dupliquer la logique entre desktop, overlay et controles ;
- s'assurer que la logique de focus et la logique de dispatch utilisent la meme notion d'eligibilite.

Criteres d'acceptation:

- un controle ineligible ne peut ni prendre ni conserver un chemin d'input illegitime ;
- l'eligibilite ne depend plus d'exceptions eparses ;
- les tests de la tache 2 passent pour les cas cibles de cette etape.

Commit recommande:

- `focus: complete task 3 centralize effective input eligibility`

Resultat:

- `MGDesktop` expose maintenant une source unique de verite pour l'eligibilite clavier effective ;
- la navigation et l'autofocus ne ciblent plus des elements seulement "focusables" en theorie, mais des cibles effectivement eligibles ;
- le gating de traitement d'input des fenetres d'overlay passe par la meme politique pure ;
- la base est prete pour nettoyer activement le focus courant et la queue de focus aux taches suivantes.

### ✅ 4. Fiabiliser le cycle de vie du focus dans MGDesktop

But:
faire de `MGDesktop` l'arbitre robuste du focus, y compris quand un controle disparait, devient non interactif, ou est masque.

Travail attendu:

- durcir `QueueFocusedKeyboardHandler`, `ApplyQueuedFocusChange` et `FocusedKeyboardHandler` pour refuser ou nettoyer les cibles devenues invalides ;
- definir une politique explicite pour les cas suivants:
  - focus sur controle cache ou clippe ;
  - focus derriere overlay modal ;
  - fermeture de dropdown, menu, context menu, fenetre ou scope ;
  - restauration de focus apres fermeture d'un scope ;
  - fallback si le focus courant n'est plus navigable ;
- verifier l'integration avec `UIFocusNavigationService` et l'historique de focus par fenetre.

Criteres d'acceptation:

- `FocusedKeyboardHandler` ne pointe pas durablement sur un controle qui ne devrait plus recevoir le clavier ;
- les pertes de focus sont previsibles et testees ;
- les transitions overlay/menu/scope ne laissent pas d'etat zombie.

Commit recommande:

- `focus: complete task 4 harden desktop focus lifecycle`

Resultat:

- `MGDesktop` nettoie maintenant activement le focus courant et la queue de focus quand une cible devient non eligible ;
- `ApplyQueuedFocusChange()` refuse les cibles devenues invalides avant de muter `FocusedKeyboardHandler` ;
- la sanitation est executee a des points stables du tick pour eviter les etats zombies lors des transitions d'overlay, de modalite, de visibilite ou de fermeture.

### ✅ 5. Isoler strictement les events souris deja geres

But:
une interaction pointeur consommee par un controle topmost ne doit pas impacter les autres controles hors opt-in explicite.

Travail attendu:

- verifier comment `HandledBy`, priorites et `InvokeEvenIfHandled` sont utilises pour la souris ;
- corriger le dispatch pour garantir qu'un controle non focus ou sous-jacent n'est pas affecte par un event deja gere, sauf si son handler est explicitement configure pour cela ;
- confirmer le comportement des clicks, press, release, moved et wheel pour:
  - controles superposes ;
  - contenu + overlay ;
  - dropdowns et menus ;
  - controles parents/enfants.

Criteres d'acceptation:

- un event souris gere par un controle topmost ne fuit pas vers le contenu cache ;
- les cas d'opt-in restent possibles et explicites ;
- aucun correctif local n'est necessaire dans chaque controle pour obtenir ce comportement de base.

Commit recommande:

- `input: complete task 5 enforce handled mouse event isolation`

Resultat:

- des regressions dediees confirment qu'un event souris gere par un handler prioritaire ne fuit pas vers un handler de plus basse priorite par defaut ;
- l'opt-in `InvokeEvenIfHandled=true` reste disponible et teste ;
- aucun refactor supplementaire du dispatch souris n'a ete force, car le comportement de base etait deja correct et le risque principal du chantier reste cote clavier/focus.

### ⚪ 6. Enforcer le routage clavier et la consommation des events

But:
faire en sorte que le clavier soit route comme dans un framework UI moderne: focus d'abord, fallback seulement si autorise, aucune fuite vers les controles non legitimes.

Travail attendu:

- verifier le contrat entre `FocusedKeyboardHandler`, `HighPriorityKeyboardHandler`, navigation globale et `KeyboardHandler.ManualUpdate()` ;
- faire respecter les regles suivantes:
  - le controle focus recoit la priorite normale du clavier ;
  - un event clavier deja gere n'affecte pas les autres controles, sauf `InvokeEvenIfHandled` explicite ;
  - la navigation globale n'intervient que si aucun controle approprie n'a deja consomme l'action ;
  - un controle non focus ne traite pas d'input clavier applicatif par accident.
- distinguer clairement texte, navigation et raccourcis globaux.

Criteres d'acceptation:

- le clavier ne fuit plus vers des controles non focus ;
- les raccourcis globaux et la navigation fallback gardent un comportement volontaire ;
- la logique reste compatible avec les text boxes et les controles de navigation.

Commit recommande:

- `input: complete task 6 enforce keyboard routing and consumption`

### ⚪ 7. Aligner les controles composites qui gerent le clavier

But:
supprimer les divergences de comportement entre les controles comme listes, arbres, menus et combo boxes.

Travail attendu:

- auditer en priorite:
  - `MGListBox` ;
  - `MGListView` ;
  - `MGComboBox` ;
  - `MGTreeView` ;
  - `MGMenuBar` et composants associes ;
  - autres controles trouves pendant la tache 1 ;
- verifier pour chacun:
  - conditions d'acquisition du focus ;
  - conditions de traitement du clavier ;
  - comportement quand le popup ou dropdown est ouvert ;
  - comportement quand le controle est visible mais non topmost ;
  - cohherence entre pointeur, clavier et navigation.
- migrer si possible vers des helpers communs plutot que multiplier les if locaux.

Criteres d'acceptation:

- les controles composites appliquent les memes regles de base ;
- `MGComboBox` et les listes n'interferent plus avec l'input d'autres controles quand ils ne devraient pas ;
- les cas de popup/dropdown/menu sont couverts par tests.

Commit recommande:

- `controls: complete task 7 align keyboard-driven composite controls`

### ⚪ 8. Bloquer l'input des controles caches, occlus ou derriere overlay

But:
traiter explicitement les cas visuels ou un controle existe encore dans l'arbre mais ne doit plus recevoir d'input.

Travail attendu:

- finaliser la politique pour:
  - `Visibility.Hidden` et `Visibility.Collapsed` ;
  - clipping complet ou bounds vides ;
  - overlay modal ;
  - popup, dropdown ou context menu topmost ;
  - cas ou un controle est recouvert par un autre dans la meme fenetre ;
- corriger les points restants dans `MGOverlay`, le desktop ou les controles composites ;
- verifier si la perte de focus doit etre immediate ou differee selon le cas, et documenter cette regle.

Criteres d'acceptation:

- un controle visuellement non atteignable ne recoit plus d'input ;
- le comportement est stable quand les overlays ou popups s'ouvrent et se ferment ;
- aucun event residuel ne touche le contenu derriere un element topmost actif.

Commit recommande:

- `overlay: complete task 8 block hidden and occluded controls from input`

### ⚪ 9. Ajouter des regressions end-to-end et une couverture sample

But:
verifier les comportements corriges au-dela des tests unitaires de logique pure.

Travail attendu:

- completer les tests avec quelques scenarios d'integration bornee ;
- si necessaire, ajouter un petit ecran de sample ou scenario reproductible dans `MGUI.Samples` pour valider:
  - overlay modal + contenu derriere ;
  - combobox ouverte au-dessus d'une liste ;
  - navigation clavier entre plusieurs controles focusables ;
  - restauration de focus apres fermeture d'un popup.
- garder ces scenarios simples et maintenables.

Criteres d'acceptation:

- les regressions critiques sont verrouillees par des tests ou un sample minimal ;
- la validation manuelle est rapide et ciblee ;
- aucun comportement majeur du focus/input n'est laisse sans filet.

Commit recommande:

- `test: complete task 9 add end-to-end regressions and sample coverage`

### ⚪ 10. Documenter le comportement final et les invariants

But:
laisser une reference claire pour eviter le retour des regressions.

Travail attendu:

- documenter le contrat final de focus/input dans `Docs/` ;
- decrire:
  - qui peut prendre le focus et quand ;
  - qui peut traiter le clavier et quand ;
  - comment un event devient "handled" ;
  - comment overlays, menus, dropdowns et scopes affectent l'input ;
  - quelles APIs sont reservees aux raccourcis globaux ou a l'opt-in ;
- ajouter une courte section de recommandations pour les futurs controles personnalises.

Criteres d'acceptation:

- les invariants sont documentes et coherents avec les tests ;
- un nouveau controle peut se brancher sur MGUI sans reintroduire les memes bugs ;
- le document peut servir de reference pour les prochains agents.

Commit recommande:

- `docs: complete task 10 document focus and input behavior`