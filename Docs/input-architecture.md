# Architecture des entrees (input)

## Objectif et portee

Decrire comment MGUI capture, route et consomme les evenements souris/clavier/gamepad : les trois couches d'input, le pipeline par tick, le contrat de consommation (`handled`), le comportement des fenetres superposees, le focus clavier, la saisie de texte native, et la couche semantique d'arbitrage UI-vs-gameplay.

Question centrale du routage : quand deux fenetres se superposent, la fenetre du dessus consomme-t-elle les evenements de sorte que la fenetre du dessous n'en recoive aucun ? Reponse courte : **oui pour les clics, la molette, les debuts de drag, et depuis la decision utilisateur du 2026-09-02 (tache 4, option b) pour l'etat visuel `Hovered`/`Pressed` — non pour les evenements de mouvement bruts (`Entered`/`MovedInside`/`Exited`, drag-drop) ni pour le clavier, qui est route par focus et non par z-order.** Detail dans la section "Fenetres superposees".

MGUI est un framework UI pour moteur de jeu, pas une replique de WPF : pas de routed events, pas de weak events generalises, pas d'`IDisposable` impose a l'arbre d'elements. Les mecanismes doivent rester deterministes et sans cout par frame.

## Vue d'ensemble : trois couches

Le routage est une propriete emergente de l'ordre de traversee de l'arbre + un flag `handled` cooperatif sur des event-args partages. Trois couches coexistent :

1. **Couche brute partagee** — `MGUI.Shared/Input` : `InputTracker` possede un `MouseTracker`, un `KeyboardTracker` et un `GamePadTracker`. A chaque tick, les trackers diffent l'etat des peripheriques (fourni par `IRawInputSource`) contre le tick precedent et materialisent **un seul objet event-args par evenement physique**, partage par tous les handlers du tick. Les `MouseHandler`/`KeyboardHandler` sont crees par element via `CreateHandler(owner, priority)`.
2. **Couche UI** — `MGUI.Core/UI` : chaque `MGElement` possede paresseusement son propre `MouseHandler`/`KeyboardHandler`, pompe par `ManualUpdate()` pendant son propre `Update`. L'ordre de traversee **est** l'ordre de routage. Le focus clavier est un pointeur unique au niveau du desktop (`MGDesktop.FocusedKeyboardHandler`).
3. **Couche semantique** — `MGUI.Shared/Input/Semantic` + `MGUI.Core/UI/InputRouting` : arbitrage UI-vs-gameplay par actions semantiques (`InputAction`, `InputRouter`, `MGUIInputContext`). Cette couche est **optionnelle et dormante par defaut** (`MGDesktop.UseRawNavigationInput = true`) ; seuls `MGUI.MiniGame` et l'outillage de replay l'utilisent.

A ne pas confondre : `UIViewInputRouter` (`MGUI.Core/UI/UIViewInputRouter.cs`) n'a rien a voir avec le `InputRouter` semantique — c'est un petit selecteur de vue cible pour les hotes multi-`UIView` (vue preferee > vue survolee > derniere vue).

## Pipeline par tick

Cable par `MGUI.MonoGame.Integration/Rendering/MainRenderer.cs` (c'est l'hote qui met a jour `InputTracker` avant `desktop.Update()` — voir `Docs/custom-render-backend-integration.md`) :

1. **`Host.PreviewUpdate`** : `InputTracker.Update(UpdateArgs)` — snapshot + diff. Les event-args du tick sont crees **neufs** (le flag `IsHandled` est donc implicitement reinitialise chaque tick).
2. **`MGDesktop.Update()`** :
   - handlers haute priorite du desktop (`HighPriorityMouseHandler`/`HighPriorityKeyboardHandler`) — le seul vrai hook "preview" global ; c'est ici que la navigation clavier/gamepad brute est dispatchee ;
   - `ActiveContextMenu` puis `ActiveToolTip` (premier acces a l'input, avant toute fenetre) ;
   - fenetres **de l'avant vers l'arriere** : `OrderedWindows = Windows.Reverse().OrderByDescending(IsTopmost)` avec l'`OverlayWindow` en tete. L'ordre d'update est le miroir exact de l'ordre de draw (arriere vers avant) ;
   - les changements de focus sont appliques a des points definis du tick (`ApplyQueuedFocusChange`), avec assainissement (`SanitizeKeyboardFocusState`).
3. **`Host.EndUpdate`** : `Mouse.UpdateHandlers()`/`Keyboard.UpdateHandlers()` — n'invoque que les handlers "auto" crees avec une `UpdatePriority` non nulle, tries par priorite decroissante. **Tous les handlers de MGUI.Core sont crees avec priorite `null`** (manuels) : ce passage ne sert donc qu'aux abonnes applicatifs (couche jeu), qui voient l'input seulement apres l'UI, et seulement s'il n'est pas marque handled.

A l'interieur d'un element (`MGElement.Update`) : enfants d'abord (en ordre **inverse** de la liste, donc le frere visuellement au-dessus en premier), puis les handlers propres de l'element. Effet net : **livraison la plus-profonde d'abord, frere du dessus d'abord, fenetre de devant d'abord** — un "bubbling par ordre d'update", sans phase de tunneling.

## Consommation : handled et HandledBy

- `HandledByEventArgs<T>` (`MGUI.Shared/Input/InputTracker.cs`) porte `IsHandled` + `HandledBy` (l'**identite** du consommateur, pas un simple bool). `SetHandledBy` est premier-arrive-premier-servi (no-op silencieux si deja handled, sauf `OverwriteIfAlreadyHandled`).
- Un evenement handled **atteint quand meme** les handlers suivants ; c'est la boucle d'invocation qui consulte le flag et saute l'invocation. Trois politiques par handler modulent cela : `InvokeEvenIfHandled` (equivalent du `handledEventsToo` WPF), `AlwaysHandlesEvents`, et cote souris `InvokeIfHandledBySelf` (defaut `true` — re-livraison quand `HandledBy == Owner`, utilise par ex. par les boutons a repetition).
- **Variations du contrat selon la famille d'evenements** :
  - les evenements de mouvement (`MovedInside/Outside`, `Entered`, `Exited`) n'ont **aucun** support handled (`BaseMouseMovedEventArgs : EventArgs`) et sont invoques sans aucun test du flag ;
  - `SetHandled` sur `Dragged`/`DragEnd` est un no-op documente ; la vraie porte est le `HandledBy` des args du `DragStart` ;
  - `Scrolled` ignore `InvokeIfHandledBySelf` que tous les evenements bouton honorent.

### Capture douce (pas d'API de capture explicite)

- **Drag** : `Dragged`/`DragEnd` sont livres au proprietaire du `DragStart` meme hors de ses bornes et meme si `CanReceiveMouseInput` devient faux — chaque `DragStart` a toujours son `DragEnd` (`MouseHandler.cs`).
- **Clavier** : un handler qui handle un `Pressed` adopte le `KeyboardInputStream` de la touche ; `Released`/`Repeat`/`Clicked` de cette pression lui restent routes meme si le focus bouge entre-temps (`KeyboardHandler.cs`).
- **Multi-clic** : `MouseClickSequence` avec adoption par le premier handler contenant le clic + validation d'ancetre logique pour le double-clic.
- **`IActiveMouseDragCapture`** (grips de resize, splitters) : pendant la capture, les handlers des elements non concernes ne sont pas pompes et le hover de la fenetre est gele.

Le substitut au tunneling est manuel : un parent qui doit preempter ses enfants (ex. `MGScrollViewer` pendant un drag de scrollbar) va **directement estampiller handled les event-args partages du tracker** dans `OnBeginUpdateContents`, avant l'update des enfants. Ce pattern contourne l'abstraction et doit etre reinvente par conteneur.

## Fenetres superposees : la garantie et ses exceptions

### Ce qui est garanti

Chaque `MGWindow` installe dans son constructeur des handlers "catch-all" sur son propre `MouseHandler` : `PressedInside`, `ReleasedInside`, `DragStart` et `Scrolled` appellent `e.SetHandledBy(this)` des lors que `!AllowsClickThrough || IsModalWindow` (`AllowsClickThrough` defaut `false` ; `WindowStyle.None` re-force `false`). Ce handler etant le **dernier** pompe dans le sous-arbre de la fenetre, il agit comme une **cloture** : tout press/release/scroll/drag-start dont la position est dans le rectangle de la fenetre — qu'un enfant ait reagi ou non — est marque handled avant que la boucle du desktop n'atteigne la fenetre suivante (plus basse), dont les handlers voient `IsHandled == true` et sautent l'invocation.

Comme l'ordre d'update est snapshotte par tick et que `BringToFront` ne prend effet qu'au tick suivant, **aucune frame ne livre un meme press aux deux fenetres**. Les fenetres modales consomment en plus `PressedOutside`/`ReleasedOutside`/`DragStartOutside` (blocage triple : consommation d'evenements + porte de hit-test `HasModalWindow` + porte de focus clavier).

### Les exceptions

1. **Le hover brut traverse, l'etat visuel non.** Les evenements de mouvement (`Entered`/`MovedInside`/`Exited`, et `DragEnter/DragOver/DragLeave` du drag-drop qui reposent sur les memes evenements) n'ont pas de flag handled et le hit-test qui les alimente reste purement par-fenetre, sans test d'occlusion : la fenetre du dessous continue de les recevoir meme recouverte. En revanche, depuis la decision utilisateur du 2026-09-02 (tache 4, option b), `MGWindow.HoveredElement` (et donc l'etat visuel `Hovered`, qui derive de ce champ) n'est plus allume pour une fenetre visuellement occluse au point de la souris : `MGDesktop.Update` reutilise sa boucle d'occlusion existante (`IsWindowOccludedAtMousePos`, jusque-la reservee aux tooltips) pour peupler `MGWindow.IsOccludedAtMousePos` (nouvelle propriete interne) avant chaque `Window.Update`, et `MGWindow.OnBeginUpdate` force `HoveredElement = null` quand ce flag est vrai — sauf si la fenetre possede deja une capture de drag active (`HasActiveMouseDragCapture()`), auquel cas son hover reste gele comme avant (voir point 3). Cout : un bool assigne par fenetre par tick, aucune allocation ; nul en l'absence de recouvrement puisque le flag ne devient vrai que dans ce cas. La meme regle s'applique aux **fenetres imbriquees** (dropdown de `MGComboBox`, popups, fenetre modale) qui recouvrent le contenu de leur propre fenetre parente : `MGWindow.OnBeginUpdateContents` reutilise son predicat d'occlusion des tooltips (fenetre modale ou fenetre imbriquee non click-through survolee, etat du tick courant) pour forcer `HoveredElement = null` du parent — et `PressedElement = null` au tick d'un press — avant la mise a jour de ses propres enfants, donc sans lag ; la capture de drag active du parent est preservee de la meme facon.
2. **L'etat presse ne fuit plus.** `MGWindow.OnBeginUpdate` fixait `PressedElement` des qu'un press recent etait signale, **sans consulter le flag handled** — corrige par la meme tache 4 : un nouveau press (`MouseLeftButtonPressedRecently`) sur une fenetre occluse (`IsOccludedAtMousePos == true`) met desormais `PressedElement = null` au lieu du resultat du hit-test, donc seule la fenetre non occluse (celle du dessus) allume l'etat visuel `Pressed` et declenche `PressedElementChanged`. Un press deja en cours (drag) n'est pas concerne : `PressedElement` n'est reevalue qu'au tick du press ou du release, jamais entre les deux.
3. **Un drag en cours suit son proprietaire.** Un drag demarre sur la fenetre du dessous continue de recevoir `Dragged`/`DragEnd` quand le curseur traverse la zone recouverte (semantique de capture standard — plutot souhaitable).
4. **Le clavier est route par focus, pas par z-order.** Si un element de la fenetre du dessous detient `FocusedKeyboardHandler`, il continue de recevoir **tout** le clavier meme entierement recouvert ; la fenetre du dessus ne "consomme" rien par superposition. L'assainissement du focus (`CanReceiveKeyboardInput`) n'a aucun terme d'occlusion.
5. **`AllowsClickThrough = true`** est un opt-in explicite qui desactive la cloture (les evenements non consommes tombent aux fenetres inferieures puis a la couche jeu).

Par ailleurs : **cliquer sur une fenetre en arriere-plan ne la ramene pas au premier plan** et n'etablit aucune notion de "fenetre active" — il n'y a pas d'`ActiveWindow` ; seul le module docking cable `BringToFront` sur le clic (`MGFloatingDockWindow.cs`). Focus et z-order peuvent donc diverger silencieusement.

Les fenetres nested **non modales** n'etablissent aucun contrat d'occlusion clavier par elles-memes : le runtime ne protege que l'element effectivement focus. Un futur controle de type popup doit soit revendiquer le focus explicitement, soit etre modal s'il est cense bloquer l'interaction derriere lui. Surface de verification manuelle : `MGUI.Samples/Features/FocusInputReview.xaml(.cs)` (routage clavier TextBox/ComboBox/ListBox, restauration de focus des context menus, isolation des fenetres nested non modales, blocage des overlays modaux).

## Clavier : focus, navigation, texte

### Focus transactionnel a proprietaire unique

- `MGElement.Focus(source)` ne fait que mettre en file une requete (`QueueFocusedKeyboardHandler`) ; `ApplyQueuedFocusChange` la commite a des points definis du tick, apres verification d'eligibilite.
- **Eligibilite effective** (centralisee dans `MGDesktop` via `FocusInputPolicy`, `MGUI.Core/UI/FocusInputPolicy.cs`) : etre focusable ne suffit pas — il faut aussi `CanHandleKeyboardInput`, un hote capable de recevoir le clavier ce frame, l'absence de blocage par overlay modal, et un etat visible/interactif. Le desktop nettoie activement le focus courant **et** la file de focus quand une cible devient invalide en cours de tick ; un overlay modal actif force le nettoyage immediat d'un focus visant le contenu derriere lui.
- Le focus est trace par **source** (`Pointer`/`Keyboard`/`GamePad`/`Programmatic`), ce qui pilote l'auto-scroll (sources non-pointeur uniquement) et les anneaux de focus affiches seulement hors mode pointeur — equivalent de `:focus-visible`.
- **Notification** : `MGElement.OnKeyboardFocusChanged(bool gained)` (protected internal virtual) est invoque directement par le setter prive de `MGDesktop.FocusedKeyboardHandler` — strictement **apres** l'affectation d'etat et le swap `ReadonlyChanged`, immediatement **avant** l'event public `FocusedKeyboardHandlerChanged`. Cet ordre est porteur : `MGTextBox.UpdateFormattedText` lit `FocusedKeyboardHandler == this` pour choisir les couleurs de selection. Les controles overrident ce virtuel au lieu de s'abonner a l'event dans leur constructeur (`MGNumericUpDown` appelle base puis `CommitPendingText` a la perte). L'event public reste reserve aux observateurs inter-elements (`MGGraphControls`, `MGPropertyGrid` — paires abonnement/desabonnement).

### Livraison et navigation

- **Livraison exclusive** : `KeyboardHandler` ne livre `Pressed` que si `Owner.HasKeyboardFocus()`. Pas de bubbling : une touche non traitee par l'element focus **n'est pas offerte a ses ancetres**. Les raccourcis de conteneur passent par le `HighPriorityKeyboardHandler` global ou par `TryHandleNavigationAction`.
- **Ordre de routage** : le desktop detecte les touches de navigation globales ; la navigation n'est routee que si l'evenement n'est pas deja handled ; l'eligibilite du focus est revalidee avant de deleguer ; l'element focus tente `TryHandleNavigationAction(...)` d'abord ; repli de deplacement de focus global sinon.
- **Navigation semantique** : `UINavigationAction` (Tab/fleches/Submit/Cancel/...) est dispatchee via le virtuel `TryHandleNavigationAction` (surcharge par 23 controles). Clavier et gamepad alimentent le meme vocabulaire.
- **Touches preservees** : les touches revendiquees par un `MGTextBox` focus (`ShouldPreserveTextEntryKey` : fleches, Home/End, Space si editable, Enter/Tab selon `AcceptsReturn`/`AcceptsTab`) ne sont jamais reinterpretees en navigation — **sur le chemin brut uniquement** (garde dans `UIFocusNavigationService.TryDispatchNavigationAction(BaseKeyPressedEventArgs)`) ; le chemin semantique la contourne (voir Limites connues).
- **Scopes de focus** : pile (racine, cible de restauration) dans `UIFocusNavigationService` (`MGUI.Core/UI/Navigation/UIFocusNavigationService.cs`), poussee par `MGContextMenu` et le popup color-picker — piegeage du Tab + restauration a la fermeture, seulement si la cible de restauration est encore valide.

### Regles pour les controles composites et nouveaux controles

`MGListBox`, `MGListView`, `MGTreeView`, `MGComboBox`, `MGMenuBar` :

- utiliser `TryHandleNavigationAction(...)` comme voie principale de navigation clavier ;
- reserver les handlers clavier bruts aux extras hors navigation partagee (ex. Ctrl+A du ListBox) ;
- ne jamais dupliquer une meme action dans les deux chemins ;
- ne jamais supposer qu'un controle invisible, masque ou bloque par overlay peut recevoir l'input.

Nouveau controle navigable : implementer `TryHandleNavigationAction(...)` avant tout handler `Pressed` brut ; n'appeler `SetHandledBy(...)` que si le controle revendique vraiment l'input ; verifier le comportement a la perte de focus, a l'invisibilite, a l'ouverture d'un overlay modal et sous un popup topmost ; preferer des regressions de logique pure a un sample manuel.

### Saisie de texte native (TextInput/IME)

- Le puits est `IKeyboardTextInputSink.QueueTextInput(char, Keys)` (implemente par `KeyboardTracker`). Sans alimentation native, la generation de caracteres retombe sur une table US-QWERTY codee en dur (AZERTY, touches mortes et IME produisent alors des caracteres faux).
- Les caracteres de controle rapportes par la saisie native (`\t` pour Tab, `\r`/`\n` pour Entree) sont normalises par `KeyboardTracker` vers les valeurs canoniques du framework (`KeyboardTracker.DefaultTabValue` = 4 espaces, `DefaultEnterValue` = `\n`) : le modele texte de `MGTextBox` insere `MGTextRun.TabSpacesCount` caracteres pour un Tab et deplace le caret d'autant, un `\t` brut desynchroniserait le caret du texte. Seuls les caracteres imprimables conservent leur valeur native. Le chemin Backspace/Delete de `MGTextBox` borne en outre l'index du caret a la longueur du texte (`NormalizeEditableCaretIndex`), comme le chemin d'insertion.
- `ITextInputHost` (`MGUI.MonoGame.Integration/Rendering/TextInputHost.cs`, namespace `MGUI.Shared.Rendering`) est le contrat opt-in : implemente par `GameRenderHost<T>` et **auto-cable par `MonoGameBackendBootstrap.Create`** — zero code utilisateur sur ce chemin. `DelegateRenderHost` et les hotes custom doivent cabler `Window.TextInput += (s, e) => keyboard.QueueTextInput(e.Character, e.Key);` manuellement — voir `Docs/monogame-host-integration-guide.md` et `Docs/custom-render-backend-integration.md`.
- Les caracteres natifs recus pendant un key-repeat sont deliberement jetes : le repeat visuel reutilise le `PrintableValue` de l'evenement de press initial (`KeyboardHandler.GetCurrentKeyRepeatEvent`).
- Diagnostic : la variable d'environnement `CASA_MGUI_INPUT_PROBE` active `KeyboardInputProbe` (`MGUI.Shared/Input/Keyboard/KeyboardInputProbe.cs`), qui trace `source=native-text` vs `source=key-map`.
- Tests de reference : `MGUI.Tests/Input/KeyboardTrackerTextInputTests.cs`, `MGUI.Tests/Input/GameRenderHostTextInputTests.cs`.

## Couche semantique : arbitrage UI vs gameplay

### Contrats livres

- Types purs dans `MGUI.Shared/Input/Semantic` : `InputAction`, `InputActionContext`, `InputActionEvent`, `InputActionMapper`, `IInputContext`, `InputRouter`, `InputCaptureResult`, `InputRouteDecision`. Contextes branches sur le desktop dans `MGUI.Core/UI/InputRouting` : `MGUIInputContext`, `GameplayInputContext`.
- `IInputContext` : `{ string Name; int Priority; bool IsActive { get; } ; bool TryHandle(InputActionEvent, out InputCaptureResult) }`.
- `InputRouter.RegisterContext(ctx)` enregistre ; `Route(InputActionEvent)` parcourt les contextes actifs par priorite decroissante (ordre stable a priorite egale), premier consommateur gagnant, et retourne un `InputRouteDecision` portant le nom du contexte gagnant et une raison lisible (precieux pour le debug).
- `MGUIInputContext(MGDesktop desktop, int priority = 100)` : **reserve inconditionnellement toutes les actions UI** (meme si aucun controle ne les consomme finalement) et bloque les actions gameplay quand `MGDesktop.ShouldCaptureGameplayInput()` est vrai — overlay modal actif, menu contextuel ouvert, ou `MGTextBox` editable focus.
- `GameplayInputContext(string name, Func<InputActionEvent, bool> tryHandle, int priority = 0, Func<bool> isActive = null)` : fallback gameplay.
- Point d'entree UI : `MGDesktop.TryHandleInputAction(InputActionEvent)` mappe l'action semantique en `UINavigationAction`, la dispatche via le service de navigation et applique la file de focus.

### Integration runtime

Dans la boucle du jeu : collecter l'input brut **une fois**, mapper **une fois** en `InputActionEvent`, router **une fois**.

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

InputActionEvent actionEvent = new(
    InputAction.GameplayPrimary,
    new InputActionContext(InputActionSource.Keyboard, InputActionPhase.Pressed, gameTime.TotalGameTime, Key: Keys.Space));
InputRouteDecision decision = router.Route(actionEvent);
```

Quand un routeur externe alimente `TryHandleInputAction`, mettre `MGDesktop.UseRawNavigationInput = false` pour couper le double dispatch de la navigation brute. Integration de reference : `MGUI.MiniGame/MiniGame.cs` (contexte UI 100, contexte HUD selectif local 50, gameplay 0).

### Principes de partage UI/gameplay

- **L'UI possede la mecanique d'interaction** : hover, focus, ordre de tabulation, navigation d-pad/fleches, hit-testing, capture pointeur, ouverture/fermeture de menus, submit/cancel local au widget.
- **Le jeu possede le sens gameplay et l'etat autoritaire** : contenu d'inventaire, equipement, cooldowns, validation d'actions, mouvement, combat.
- **Frontiere** : l'UI leve une intention (`RequestUseConsumable(slotId)`), le jeu applique l'etat, l'UI reflete l'etat resultant.
- **Un contexte HUD doit etre selectif, pas modal** : ne consommer que les actions visant le widget HUD sous le pointeur ou la selection courante ; le mouvement joueur n'est jamais capture par accident. Pour une quickbar, la selection est generalement de l'etat gameplay ; son affichage est de l'UI.
- **Anti-patterns** : UI et gameplay lisant chacun l'etat brut des peripheriques ; widgets mutant directement l'etat gameplay autoritaire ; touches brutes au lieu d'actions semantiques dans les deux couches.

La separation fine en contextes framework `ModalUI`/`Menu`/`TextEntry`/`HUD` (priorites echelonnees) est un prolongement volontairement differe : la v1 livree se limite a `MGUIInputContext` + `GameplayInputContext`. Ordre d'extension prevu : contextes UI plus fins, puis actions pointeur/manette etendues, puis branchement des samples gameplay sur la meme chaine.

### Limites structurelles de la couche semantique

- La souris, le scroll et les axes ne passent **jamais** par le routeur : deux systemes d'arbitrage coexistent ("l'UI a-t-elle mange le clic" et "l'UI a-t-elle mange le bouton" se decident par des mecanismes differents).
- Quatre tables de mapping paralleles doivent rester synchronisees a la main : `InputActionMapper`, cartes brutes de `MGDesktop`, doublons de `UIFocusNavigationService`, pont `InputAction` -> `UINavigationAction`.
- Aucun hote par defaut ne l'utilise — chaque integrateur ecrit sa boucle collect/map/route.

## Invariants de cycle de vie (fuites)

Ces decisions sont arbitrees et verrouillees par des tests ; ne pas les re-explorer.

- **Handlers manuels non enregistres** : `MouseTracker.CreateHandler`/`KeyboardTracker.CreateHandler` avec `UpdatePriority == null` (tout ce que cree MGUI.Core) n'ajoutent **pas** le handler a `_Handlers` — seul `UpdateHandlers()` lit ces listes et il ne sert qu'aux handlers auto. Aucune racine tracker -> element sur ce chemin. `Unsubscribe()` reste un kill switch sur (no-op de `List.Remove` + `IsValid = false`).
- **Interdit : `Unsubscribe()` depuis un chemin de teardown.** "Fermer n'est pas mourir" : la MEME instance de `MGWindow` est couramment re-affichee apres fermeture (`SampleBase.Show/Hide` ; `BringToFront` = Remove+Add sur `Desktop.Windows`, une simple `List<MGWindow>` sans hook d'ajout), et `IsValid` est definitivement faux sans API de revalidation.
- **Pas d'abonnement de constructeur a un evenement de duree de vie desktop/runtime** (`FocusedKeyboardHandlerChanged`, `Runtime.EndUpdate`, ...). La notification de focus passe par le virtuel `OnKeyboardFocusChanged` (voir plus haut) ; les resets de fin de tick passent par un hook du cycle de vie de l'element lui-meme (ex. `MGListBox` sur son propre `OnBeginUpdate` — la sequence par frame etant Update -> Draw -> EndUpdate, un reset en debut d'update suivant est equivalent pour le Draw).
- **Un seul weak event sanctionne** : `MGResources.SetParent` s'abonne au `OnDefaultThemeChanged` du parent via `WeakThemeChangedForwarder` (classe imbriquee de `MGUI.Core/UI/MGResources.cs`), qui ne reference le scope enfant que par `WeakReference` et s'auto-desabonne quand l'enfant est collecte. C'est le seul point d'abonnement croisant deux domaines de duree de vie ; la propagation de theme fonctionne fenetre fermee comprise (donc a la reouverture), et le domaine complet devient collectable quand l'application lache l'instance. Ce n'est pas un feu vert pour des weak events generalises.
- **Surfaces epinglees par tests d'architecture** : les ensembles de membres de `IRenderHost` et `IUIDesktopRuntime` ainsi que des sous-chaines verbatim de `Game1.cs`/`MiniGame.cs` sont assertes par `RawInputSourceTests` et `HostRuntimeContractTests` — les lire avant d'editer ces fichiers.
- Regressions : `MGUI.Tests/Input/TrackerHandlerRegistrationTests.cs`, `MGUI.Tests/Input/InputLifetimeRegressionTests.cs`, `MGUI.Tests/Focus/KeyboardFocusNotificationTests.cs`, `MGUI.Tests/Controls/MGListBoxPressedItemTests.cs`.

## Limites connues

1. **Fuite de hover inter-fenetres (partiellement corrigee, tache 4, option b, 2026-09-02)** : `HoveredElement`/`PressedElement` (donc l'etat visuel `Hovered`/`Pressed`) d'une fenetre occluse au point de la souris n'atteignent plus les elements de cette fenetre — `MGDesktop.Update` propage son `IsWindowOccludedAtMousePos` (jusque-la reserve aux tooltips) a `MGWindow.IsOccludedAtMousePos`, et `MGWindow.OnBeginUpdate` neutralise `HoveredElement`/`PressedElement` en consequence, sauf pendant une capture de drag active (qui continue de suivre son proprietaire). Ce qui **reste** ouvert (hors perimetre de la decision (b), qui ne portait que sur `HoveredElement`/`PressedElement`) : les evenements de mouvement bruts `Entered/MovedInside/Exited` et le drag-drop (`DragEnter/DragOver/DragLeave`) n'ont toujours aucun flag handled et atteignent quand meme les elements occlus d'une fenetre inferieure.
2. **`MGWindow.WindowKeyboardHandler` est un point d'extension inerte** : son proprietaire (la fenetre) n'a jamais le focus clavier, donc `Pressed` ne peut jamais lui etre livre.
3. **Light-dismiss fragile hors du premier plan** : le dropdown `MGComboBox` se ferme sur `ReleasedOutside`, mais si le clic de fermeture atterrit sur une fenetre plus haute, la cloture de celle-ci consomme le release et le dropdown reste ouvert. Le menu contextuel n'y echappe que par son slot d'update privilegie.
4. **Collectabilite des fenetres fermees** : la chaine de retention via l'abonnement theme de `MGResources.SetParent` est neutralisee par le forwarder faible ; un travail complementaire sur les racines residuelles `MGResources` des fenetres fermees est en cours dans une session dediee — ne pas re-specifier ce chantier ici.
5. **Aucune reaction a la desactivation de la fenetre OS** (`Game.IsActive` ignore).
6. Divers : hit-test rectangle-only (coins arrondis, `RenderScale` — la zone cliquable diverge des pixels dessines) ; transitions sous-frame perdues (press+release dans une meme frame = aucun evenement, inherent au polling) ; `DerivedIsHitTestVisible` ne consulte que le parent immediat (le pipeline interne, lui, chaine correctement) ; `MGButton` ne marque pas ses clics handled par defaut ; `KeyboardHandler` itere ~160 `Keys` par handler par tick sans early-out agrege ; machinerie de priorite des trackers inutilisee par le framework lui-meme ; `FocusArchitectureTests` lit les sources via des chemins absolus.

## Reste a faire

Le travail restant sur ce theme est specifie dans `Docs/Tasks/input-tasks.md`.
