# Taches input : corrections de routage et regressions

## Objectif

Corriger les limites actionnables du systeme d'input documentees dans `Docs/input-architecture.md` (section "Limites connues") et completer la couverture de tests du routage : fast-path drag mort, inversion de z-order dans les replis de navigation, preservation des touches de saisie sur le chemin semantique, statut du hover inter-fenetres, tests fenetres superposees, regressions end-to-end du routeur semantique, contexte HUD reutilisable, et pistes long terme (fenetre active, points d'extension reels).

Hors perimetre : les racines residuelles `MGResources` des fenetres fermees (chantier traite dans une session dediee).

Ce document est destine a un agent IA implementeur.

## Consignes de travail pour l'agent IA

- Executer les taches strictement dans l'ordre.
- Faire exactement 1 commit par tache terminee.
- Mettre a jour le statut de la tache dans ce fichier avant chaque commit.
- Si une tache est bloquee, la marquer `⛔`, decrire le blocage juste en dessous, puis s'arreter.
- Ne pas faire de refactor hors perimetre ; ne pas re-arbitrer les invariants de cycle de vie de `Docs/input-architecture.md` (handlers manuels non enregistres, "fermer n'est pas mourir", forwarder faible unique).
- Ajouter ou adapter des tests a chaque tache.
- Surfaces epinglees : les ensembles de membres de `IRenderHost`/`IUIDesktopRuntime` et des sous-chaines verbatim de `Game1.cs`/`MiniGame.cs` sont assertes par `RawInputSourceTests` et `HostRuntimeContractTests` — les lire avant d'editer ces fichiers.
- Ne rien supposer ni inventer : toute decision non couverte par le texte de la tache ou par les decisions utilisateur consignees dans ce fichier doit etre remontee — marquer la tache `⛔` avec la question precise, ne pas committer de code pour cette tache, et s'arreter.

## Decisions utilisateur (2026-09-02)

- Tache 4 : option (b) retenue — implementer l'occlusion inter-fenetres pour `HoveredElement` et `PressedElement` (pas seulement documenter).
- Tache 7 : type `HudInputContext` dans `MGUI.Core/UI/InputRouting`, priorite par defaut `50`.
- Tache 8 : reportee, non incluse dans l'execution courante (reste `⚪`).

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale

1. `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
2. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
3. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "Focus|Input" --logger "console;verbosity=minimal"` + filtre cible de la tache
4. Taches touchant la couche semantique (3, 6, 7) : `dotnet build .\MGUI.MiniGame\MGUI.MiniGame.csproj --no-restore`
5. Critere transversal : aucun test nouvellement rouge par rapport a l'etat de depart.

## Taches

### ✅ 1. Corriger le fast-path drag de MouseTracker

But:

supprimer un cout par tick inutile : les flags d'early-out du drag valent toujours `true`.

Travail attendu:

- `MGUI.Shared/Input/Mouse/MouseTracker.cs:470-472` : `HasCurrentDragStartEvents`/`HasCurrentDraggedEvents`/`HasCurrentDragEndEvents` sont calcules par `.Any(x => x.Value != null)` sur des `Dictionary<DragStartCondition, Dictionary<MouseButton, ...>>` — `x.Value` est le dictionnaire interne, jamais nul, donc les 3 flags sont toujours vrais et le bloc de dispatch drag de `MouseHandler.InvokeQueuedEvents` tourne chaque tick pour chaque handler abonne au drag (avec allocation d'une `List<DragStartCondition>`) ;
- remplacer le predicat par `.Any(x => x.Value.Values.Any(v => v != null))` (pattern deja correct pour les evenements bouton quelques lignes plus haut dans le meme fichier) ;
- ajouter un test unitaire verifiant que les 3 flags sont faux sur un tick sans activite drag, et vrais pendant un drag reel.

Criteres d'acceptation:

- les flags reflechissent l'etat reel des evenements drag du tick ;
- comportement drag existant inchange (`dotnet test --filter "Focus|Input"` vert) ;
- aucun test nouvellement rouge.

Commit recommande:

- `input: fix dead drag fast-path flags in mouse tracker`

Resultat:

- `MGUI.Shared/Input/Mouse/MouseTracker.cs:470-472` : remplacement de `.Any(x => x.Value != null)` par `.Any(x => x.Value.Values.Any(v => v != null))` pour `HasCurrentDragStartEvents`/`HasCurrentDraggedEvents`/`HasCurrentDragEndEvents` (meme pattern que les flags bouton).
- Nouveau fichier de tests `MGUI.Tests/Input/MouseTrackerDragFastPathTests.cs` (5 tests) : flags a faux hors activite drag et sur simple mouvement sans bouton presse, `HasCurrentDragStartEvents` vrai au tick de press (condition `MousePressed`), `HasCurrentDraggedEvents` vrai pendant un drag en cours, `HasCurrentDragEndEvents` vrai au relachement puis retour a faux au tick suivant.
- Validation : `dotnet build MGUI.Tests/MGUI.Tests.csproj --no-restore` OK ; `dotnet build MGUI.Samples/MGUI.Samples.csproj --no-restore` OK ; `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-build --filter "Focus|Input"` -> 366/366 verts ; suite complete `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-build` -> 1210/1210 verts (baseline 1205 + 5 nouveaux tests), aucun test rouge.

### ⚪ 2. Corriger l'inversion GetWindowsFrontToBack et supprimer la copie morte

But:

faire pointer les replis de navigation vers la fenetre reellement au premier plan, et supprimer la duplication morte.

Travail attendu:

- la boucle d'update ordonne les fenetres de l'avant vers l'arriere via `Windows.Reverse().OrderByDescending(IsTopmost)` ; les replis de navigation omettent le `.Reverse()` : quand rien n'est focus, Tab/auto-focus peut cibler la fenetre la plus en **arriere** ;
- copie vivante a corriger : `MGUI.Core/UI/Navigation/UIFocusNavigationService.cs:369` (`Desktop.Windows.OrderByDescending(x => x.IsTopmost)`) et `:386` (idem pour `topWindowRoot`) — aligner sur l'ordre de la boucle d'update ;
- copie morte a supprimer : `MGDesktop.GetWindowsFrontToBack`/`GetHoveredNavigationTarget`/`GetNavigationRoot`/`GetNearestNavigationTarget` (`MGUI.Core/UI/MGDesktop.cs:421-459`) — methodes privees sans aucun appelant depuis l'extraction vers `UIFocusNavigationService` (verifie : `GetNavigationRoot` n'a aucune reference hors sa definition) ;
- ajouter un test : deux fenetres non-topmost, aucune focus — le repli de navigation (racine de navigation / cible hover) doit resoudre la fenetre du **dessus** (la derniere de `Desktop.Windows`, l'update la traitant en premier apres `Reverse()`).

Criteres d'acceptation:

- ordre de navigation identique a l'ordre visuel de la boucle d'update, y compris avec des fenetres `IsTopmost` ;
- plus aucune copie de la logique de navigation dans `MGDesktop` ;
- aucun test nouvellement rouge.

Commit recommande:

- `focus: fix front-to-back window ordering in navigation fallbacks`

### ⚪ 3. Aligner le chemin semantique sur ShouldPreserveTextEntryKey

But:

empecher le routeur semantique de voler les touches d'edition d'un textbox focus, comme le fait deja le chemin brut.

Travail attendu:

- `MGDesktop.TryHandleInputAction` (`MGUI.Core/UI/MGDesktop.cs:594`) mappe l'action semantique puis appelle directement `NavigationService.TryDispatchNavigationAction(navigationAction, source)` ; la garde `ShouldPreserveTextEntryKey` n'existe que dans la surcharge brute `TryDispatchNavigationAction(BaseKeyPressedEventArgs)` (`MGUI.Core/UI/Navigation/UIFocusNavigationService.cs:215`). Avec `UseRawNavigationInput = false` (configuration expediee : `InputActionMapper` + `MGUIInputContext` + MiniGame), une fleche ou Tab pendant l'edition peut a la fois editer le caret et deplacer le focus ;
- `InputActionEvent.Context` porte deja la touche d'origine (`InputActionContext.Key`) : ajouter la garde sur le chemin semantique — si `FocusedKeyboardHandler` est un `MGTextBox` dont `ShouldPreserveTextEntryKey(key)` est vrai pour la touche portee par le contexte, ne pas dispatcher la navigation (retourner la valeur qui laisse `MGUIInputContext` reserver l'action UI, afin qu'elle ne fuie pas non plus vers le gameplay) ;
- factoriser la garde avec le chemin brut plutot que la dupliquer (les deux chemins convergent dans `UIFocusNavigationService`) ;
- tests : textbox editable focus + action semantique `NavigateLeft`/`NavigateNext` portant `Keys.Left`/`Keys.Tab` -> pas de deplacement de focus ; meme action sans textbox focus -> navigation normale ; verifier aussi le cas `AcceptsTab == false` (Tab doit naviguer).

Criteres d'acceptation:

- l'invariant "les touches preservees ne sont jamais reinterpretees en navigation" tient sur les deux chemins ;
- pas de fuite de ces actions vers le gameplay via le routeur ;
- aucun test nouvellement rouge (`--filter "Focus|Input"` + build MiniGame).

Commit recommande:

- `input: honor text entry key preservation on semantic navigation path`

### ⚪ 4. Decider et implementer le statut du hover inter-fenetres

But:

trancher si le hover (et l'etat presse) d'une fenetre occluse est un comportement assume ou un defaut a corriger.

Travail attendu:

- etat actuel : les evenements de mouvement n'ont aucun flag handled, le hit-test est par-fenetre, la fenetre du dessous calcule son `HoveredElement` et allume `Hovered` ; `MGWindow.OnBeginUpdate` fixe `PressedElement` sans consulter le flag handled ; seuls les tooltips sont proteges via la boucle `IsWindowOccludedAtMousePos` de `MGDesktop.Update` ;
- decision prise (utilisateur, 2026-09-02) : option (b) — generaliser le test d'occlusion desktop (mecanisme `IsWindowOccludedAtMousePos` existant) au calcul de `HoveredElement` et a `PressedElement` ; l'option (a) n'est plus a considerer ;
- attention au drag en cours (un drag demarre sur la fenetre du dessous doit continuer a suivre son proprietaire — semantique de capture a preserver) et au cout par tick (le test d'occlusion doit rester borne au cas multi-fenetres qui se chevauchent) ;
- consigner la decision dans `Docs/input-architecture.md` (sections "Fenetres superposees" et "Limites connues").

Criteres d'acceptation:

- decision explicite documentee ;
- si implementation : les elements occlus d'une fenetre inferieure n'allument plus `Hovered`/`Pressed`, tests a l'appui, drag-capture intact ;
- aucun test nouvellement rouge.

Commit recommande:

- `input: resolve cross-window hover occlusion status` (ou `docs: ...` si option (a))

### ⚪ 5. Ajouter des tests fenetres superposees

But:

verrouiller par tests la garantie centrale du routage (cloture de fenetre) et ses exceptions — aucun test n'existe aujourd'hui pour deux fenetres desktop superposees.

Travail attendu:

- construire un harness avec deux `MGWindow` qui se chevauchent sur un desktop de test (patron de runtime : `MGUI.Tests/Focus/KeyboardFocusNotificationTests.cs`) ;
- scenarios a couvrir :
  - press/release/scroll dans la zone de recouvrement : consommes par la fenetre du dessus, jamais livres a celle du dessous ;
  - debut de drag dans la zone de recouvrement : meme garantie ;
  - hover : pinner le comportement choisi en tache 4 ;
  - clavier : un element focus de la fenetre du dessous recoit le clavier meme recouvert (routage par focus, pas par z-order) ;
  - `AllowsClickThrough = true` : les evenements non consommes tombent a la fenetre inferieure ;
  - light-dismiss d'un popup en fenetre non frontale : documenter par un test le comportement actuel du dropdown `MGComboBox` (`ReleasedOutside` consomme par la cloture d'une fenetre plus haute -> dropdown reste ouvert), en le marquant comme comportement connu si non corrige ;
- pas de correction de comportement dans cette tache (sauf trivialite d'une ligne revelee par un test) : c'est une tache de couverture.

Criteres d'acceptation:

- chaque scenario ci-dessus a un test dedie et vert ;
- les tests documentent les exceptions (hover, clavier) au lieu de les masquer ;
- aucun test nouvellement rouge.

Commit recommande:

- `test: add overlapping desktop windows input coverage`

### ⚪ 6. Ajouter des regressions end-to-end sur le routage semantique

But:

verrouiller les scenarios mixtes UI/gameplay — `MGUI.Tests/Architecture/InputRoutingIntegrationTests.cs` ne contient que 2 tests (fallback gameplay, capture UI) et `InputRouterTests.cs` ne couvre que l'arbitrage pur.

Travail attendu:

- ajouter des tests sur les scenarios suivants (harnesses de logique et doubles simples, pas de runtime complet) :
  - overlay modal actif => actions gameplay bloquees (`ShouldCaptureGameplayInput`) ;
  - menu contextuel ouvert => idem ;
  - menu navigable non modal => `Navigate*` va a l'UI, le gameplay ne recoit pas l'action ;
  - text entry focalise => touches preservees par l'UI, jamais routees en navigation ni au gameplay (s'appuie sur la tache 3) ;
  - contexte HUD actif sous pointeur => clic consomme par le HUD, pas par le gameplay ; HUD non vise => l'action descend au gameplay (utiliser un contexte selectif de test calque sur `MGUI.MiniGame/MiniGameHudInputContext.cs`) ;
- verifier que chaque `InputRouteDecision` porte le bon contexte gagnant et une raison exploitable.

Criteres d'acceptation:

- les priorites de contexte et les cas de fuite UI -> gameplay et gameplay -> UI sont couverts ;
- la suite reste rapide et bornee ;
- aucun test nouvellement rouge.

Commit recommande:

- `test: add end-to-end semantic input routing regressions`

### ⚪ 7. Promouvoir un contexte HUD selectif reutilisable

But:

offrir dans le framework le contexte HUD selectif non modal qui n'existe aujourd'hui que localement dans MiniGame.

Travail attendu:

- reference existante : `MGUI.MiniGame/MiniGameHudInputContext.cs`, enregistre dans `MGUI.MiniGame/MiniGame.cs` (`_inputRouter.RegisterContext(new MiniGameHudInputContext(TryHandleHudAction, 50, IsHudContextActive))`) entre le contexte UI (100) et le gameplay (0) — activation selective conforme au principe "un HUD consomme uniquement les actions visant le widget HUD vise, jamais le mouvement" ;
- ajouter un contexte generique dans `MGUI.Core/UI/InputRouting` nomme `HudInputContext` (decision utilisateur) : delegate `tryHandle`, priorite par defaut `50` (decision utilisateur), predicat d'activation selectif obligatoire ; forme calquee sur `GameplayInputContext` ;
- migrer MiniGame vers ce type (en respectant les sous-chaines verbatim de `MiniGame.cs` assertees par `HostRuntimeContractTests` — les lire avant d'editer) ;
- tests purs de la politique selective : action visant le HUD consommee ; action de mouvement jamais consommee meme HUD actif ; HUD inactif => fall-through complet.

Criteres d'acceptation:

- un integrateur peut composer UI > HUD > gameplay sans ecrire son propre type de contexte ;
- MiniGame utilise le type framework et builde ;
- politique selective pinnee par tests ; aucun test nouvellement rouge.

Commit recommande:

- `feat: add reusable selective hud input context`

### ⚪ 8. Long terme : activation de fenetre et points d'extension reels

Reportee par decision utilisateur (2026-09-02) : non incluse dans l'execution courante des taches 1 a 7.

But:

resorber trois limites de conception documentees dans `Docs/input-architecture.md` ; tache de conception a decouper avant implementation.

Travail attendu:

- proposer (document de conception court, puis decoupage en taches) :
  - activation-fenetre-au-clic ou au minimum un concept `ActiveWindow` optionnel — aujourd'hui cliquer une fenetre en arriere-plan ne la ramene pas au premier plan (seul le docking cable `BringToFront` sur clic, `MGFloatingDockWindow.cs`), et focus et z-order divergent silencieusement ;
  - une interface de type `ITextEntryHost` remplacant les tests `is MGTextBox` (`MGDesktop.ShouldCaptureGameplayInput`, garde des touches preservees) pour que les controles d'edition tiers beneficient des memes protections ;
  - un vrai point d'extension clavier de fenetre remplacant `MGWindow.WindowKeyboardHandler`, inerte aujourd'hui (son proprietaire n'a jamais le focus clavier, `Pressed` ne peut pas lui etre livre) — ou sa suppression ;
- ne pas implementer sans validation prealable du decoupage.

Criteres d'acceptation:

- proposition ecrite avec impacts API et compatibilite ;
- decoupage en taches executables valide avant tout code.

Commit recommande:

- `docs: propose window activation and input extension points design`
