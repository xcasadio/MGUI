# Architecture de la gestion des evenements (input)

Date de l'audit : 2026-09-01 (branche `develop`). Les references `fichier:ligne` correspondent a cet etat du code et peuvent deriver.

## Objectif

Documenter de bout en bout la maniere dont MGUI capture, route et consomme les evenements souris/clavier/gamepad, evaluer cette architecture par rapport aux standards modernes (WPF/Avalonia : routed events tunneling/bubbling, capture, hit-testing z-order, focus scopes), et repondre precisement a la question centrale : **quand deux fenetres se superposent, la fenetre du dessus consomme-t-elle les evenements de sorte que la fenetre du dessous n'en recoive aucun ?**

Reponse courte : **oui pour les clics, la molette et les debuts de drag — non pour le survol (hover), l'etat presse visuel, et le clavier qui est route par focus et non par z-order.** Le detail est dans la section "Fenetres superposees".

## Vue d'ensemble : trois couches

MGUI n'a **pas** de systeme de routed events a la WPF. Le routage est une propriete emergente de l'ordre de traversee de l'arbre + un flag `handled` cooperatif sur des event-args partages. Trois couches coexistent :

1. **Couche brute partagee** — `MGUI.Shared/Input` : `InputTracker` possede un `MouseTracker`, un `KeyboardTracker` et un `GamePadTracker`. A chaque tick, les trackers diffent l'etat des peripheriques (fourni par `IRawInputSource`) contre le tick precedent et materialisent **un seul objet event-args par evenement physique**, partage par tous les handlers du tick. Les `MouseHandler`/`KeyboardHandler` sont crees par element via `CreateHandler(owner, priority)`.
2. **Couche UI** — `MGUI.Core/UI` : chaque `MGElement` possede paresseusement son propre `MouseHandler`/`KeyboardHandler` (`MGElement.cs:1409,1414`), pompe par `ManualUpdate()` pendant son propre `Update`. L'ordre de traversee **est** l'ordre de routage. Le focus clavier est un pointeur unique au niveau du desktop (`MGDesktop.FocusedKeyboardHandler`).
3. **Couche semantique** — `MGUI.Shared/Input/Semantic` + `MGUI.Core/UI/InputRouting` : arbitrage UI-vs-gameplay par actions semantiques (`InputAction`, `InputRouter`, `MGUIInputContext`). Voir `gameplay-ui-input-routing-v1.md`. Cette couche est **optionnelle et dormante par defaut** (`UseRawNavigationInput = true`) ; seul `MGUI.MiniGame` et l'outillage de replay l'utilisent.

A ne pas confondre : `UIViewInputRouter` (`MGUI.Core/UI/UIViewInputRouter.cs`) n'a rien a voir avec le `InputRouter` semantique — c'est un petit selecteur de vue cible pour les hotes multi-`UIView` (vue preferee > vue survolee > derniere vue).

## Pipeline par tick

Cable par `MGUI.MonoGame.Integration/Rendering/MainRenderer.cs:162-174` :

1. **`Host.PreviewUpdate`** : `InputTracker.Update(UpdateArgs)` — snapshot + diff. Les event-args du tick sont crees **neufs** (le flag `IsHandled` est donc implicitement reinitialise chaque tick).
2. **`MGDesktop.Update()`** (`MGDesktop.cs:1369-1481`) :
   - handlers haute priorite du desktop (`HighPriorityMouseHandler`/`HighPriorityKeyboardHandler`) — le seul vrai hook "preview" global ; c'est ici que la navigation clavier/gamepad brute est dispatchee ;
   - `ActiveContextMenu` puis `ActiveToolTip` (premier acces a l'input, avant toute fenetre) ;
   - fenetres **de l'avant vers l'arriere** : `OrderedWindows = Windows.Reverse().OrderByDescending(IsTopmost)` avec l'`OverlayWindow` en tete (`MGDesktop.cs:1427-1431`). L'ordre d'update est le miroir exact de l'ordre de draw (arriere vers avant, `MGDesktop.cs:1512-1515`) ;
   - les changements de focus sont appliques a des points definis du tick (`ApplyQueuedFocusChange`), avec assainissement (`SanitizeKeyboardFocusState`).
3. **`Host.EndUpdate`** : `Mouse.UpdateHandlers()`/`Keyboard.UpdateHandlers()` — n'invoque que les handlers "auto" crees avec une `UpdatePriority` non nulle, tries par priorite decroissante. **Tous les handlers de MGUI.Core sont crees avec priorite `null`** (manuels) : ce passage ne sert donc qu'aux abonnes applicatifs (couche jeu), qui voient l'input seulement apres l'UI, et seulement s'il n'est pas marque handled.

A l'interieur d'un element (`MGElement.Update`, `MGElement.cs:2375-2572`) : enfants d'abord (en ordre **inverse** de la liste, donc le frere visuellement au-dessus en premier), puis les handlers propres de l'element. Effet net : **livraison la plus-profonde d'abord, frere du dessus d'abord, fenetre de devant d'abord** — un "bubbling par ordre d'update", sans phase de tunneling.

## Le mecanisme de consommation (handled)

- `HandledByEventArgs<T>` (`MGUI.Shared/Input/InputTracker.cs:20-50`) porte `IsHandled` + `HandledBy` (l'**identite** du consommateur, pas un simple bool). `SetHandledBy` est premier-arrive-premier-servi (no-op silencieux si deja handled, sauf `OverwriteIfAlreadyHandled`).
- Un evenement handled **atteint quand meme** les handlers suivants ; c'est la boucle d'invocation qui consulte le flag et saute l'invocation. Trois politiques par handler modulent cela : `InvokeEvenIfHandled` (equivalent du `handledEventsToo` WPF), `AlwaysHandlesEvents`, et cote souris `InvokeIfHandledBySelf` (defaut `true` — re-livraison quand `HandledBy == Owner`, utilise par ex. par les boutons a repetition).
- **Incoherences du contrat selon la famille d'evenements** (verifiees) :
  - les evenements de mouvement (`MovedInside/Outside`, `Entered`, `Exited`) n'ont **aucun** support handled (`BaseMouseMovedEventArgs : EventArgs`) et sont invoques sans aucun test du flag (`MouseHandler.cs:391-415`) ;
  - `SetHandled` sur `Dragged`/`DragEnd` est un no-op documente ; la vraie porte est le `HandledBy` des args du `DragStart` ;
  - `Scrolled` ignore `InvokeIfHandledBySelf` que tous les evenements bouton honorent.
- **Capture douce** (pas d'API de capture explicite) :
  - drag : `Dragged`/`DragEnd` sont livres au proprietaire du `DragStart` meme hors de ses bornes et meme si `CanReceiveMouseInput` devient faux — chaque `DragStart` a toujours son `DragEnd` (`MouseHandler.cs:711-713, 782-803`) ;
  - clavier : un handler qui handle un `Pressed` adopte le `KeyboardInputStream` de la touche ; `Released`/`Repeat`/`Clicked` de cette pression lui restent routes meme si le focus bouge entre-temps (`KeyboardHandler.cs:54-63, 188-230`) ;
  - multi-clic : `MouseClickSequence` avec adoption par le premier handler contenant le clic + validation d'ancetre logique pour le double-clic ;
  - `IActiveMouseDragCapture` (grips de resize, splitters) : pendant la capture, les handlers des elements non concernes ne sont pas pompes et le hover de la fenetre est gele.

Le substitut au tunneling est manuel : un parent qui doit preempter ses enfants (ex. `MGScrollViewer` pendant un drag de scrollbar) va **directement estampiller handled les event-args partages du tracker** dans `OnBeginUpdateContents`, avant l'update des enfants (`MGScrollViewer.cs:661-677`). Ce pattern contourne l'abstraction et doit etre reinvente par conteneur.

## Fenetres superposees : la garantie et ses limites

Verdict de la verification adversariale : **PARTIAL** — la garantie est prouvee pour clics/molette/debut de drag, refutee dans son enonce absolu.

### Ce qui est garanti (mecanisme prouve)

Chaque `MGWindow` installe dans son constructeur des handlers "catch-all" sur son propre `MouseHandler` (`MGWindow.cs:1244-1272`) : `PressedInside`, `ReleasedInside`, `DragStart` et `Scrolled` appellent `e.SetHandledBy(this)` des lors que `!AllowsClickThrough || IsModalWindow` (`AllowsClickThrough` defaut `false` ; `WindowStyle.None` re-force `false`). Ce handler etant le **dernier** pompe dans le sous-arbre de la fenetre, il agit comme une **cloture** : tout press/release/scroll/drag-start dont la position est dans le rectangle de la fenetre — qu'un enfant ait reagi ou non — est marque handled avant que la boucle du desktop n'atteigne la fenetre suivante (plus basse), dont les handlers voient `IsHandled == true` et sautent l'invocation.

Comme l'ordre d'update est snapshotte par tick et que `BringToFront` ne prend effet qu'au tick suivant, **aucune frame ne livre un meme press aux deux fenetres**. Les fenetres modales consomment en plus `PressedOutside`/`ReleasedOutside`/`DragStartOutside` (blocage triple : consommation d'evenements + porte de hit-test `HasModalWindow` + porte de focus clavier).

### Les exceptions reelles (verifiees)

1. **Le hover traverse.** Les evenements de mouvement n'ont pas de flag handled et le hit-test est purement par-fenetre, sans test d'occlusion inter-fenetres. La fenetre du dessous continue de calculer son `HoveredElement`, ses elements recoivent `Entered`/`MovedInside`/`Exited` et allument l'etat visuel `Hovered` (les deux fenetres surlignent en meme temps). Le commentaire de `MGElement.IsHovered` (`MGElement.cs:1431-1435`) documente le caractere geometrique, mais le remede recommande (`VisualState.Hovered`) ne corrige que l'occlusion intra-fenetre. Seuls les **tooltips** sont proteges, via la boucle manuelle `IsWindowOccludedAtMousePos` (`MGDesktop.cs:1444-1469`). `DragEnter/DragOver/DragLeave` (drag-drop) reposant sur ces memes evenements, ils traversent aussi.
2. **L'etat presse fuit.** `MGWindow.OnBeginUpdate` fixe `PressedElement` des que le tracker signale un press recent, **sans consulter le flag handled** (`MGWindow.cs:1216-1226`) : un clic dans la zone de recouvrement met l'element des **deux** fenetres en etat visuel `Pressed` et declenche `PressedElementChanged` dans les deux.
3. **Un drag en cours suit son proprietaire.** Un drag demarre sur la fenetre du dessous continue de recevoir `Dragged`/`DragEnd` quand le curseur traverse la zone recouverte (semantique de capture standard — plutot souhaitable).
4. **Le clavier est route par focus, pas par z-order.** Si un element de la fenetre du dessous detient `FocusedKeyboardHandler`, il continue de recevoir **tout** le clavier meme entierement recouvert ; la fenetre du dessus ne "consomme" rien par superposition. L'assainissement du focus (`CanReceiveKeyboardInput`) n'a aucun terme d'occlusion.
5. **`AllowsClickThrough = true`** est un opt-in explicite qui desactive la cloture (les evenements non consommes tombent aux fenetres inferieures puis a la couche jeu).

Notons aussi : **cliquer sur une fenetre en arriere-plan ne la ramene pas au premier plan** et n'etablit aucune notion de "fenetre active" — il n'y a pas d'`ActiveWindow` ; seul le module docking cable `BringToFront` sur le clic (`MGFloatingDockWindow.cs`). Focus et z-order peuvent donc diverger silencieusement.

**Aucun test n'existe** pour l'input de deux fenetres desktop superposees (seuls le blocage modal et les nodes de graphe superposes intra-fenetre sont testes).

## Clavier : focus, navigation, texte

- **Focus transactionnel a proprietaire unique** : `MGElement.Focus(source)` ne fait que mettre en file une requete ; `ApplyQueuedFocusChange` la commite a des points definis du tick, apres verification d'eligibilite (`FocusInputPolicy`). Le focus est trace par source (`Pointer`/`Keyboard`/`GamePad`/`Programmatic`), ce qui pilote l'auto-scroll (sources non-pointeur uniquement) et les anneaux de focus affiches seulement hors mode pointeur — equivalent de `:focus-visible`, en avance sur beaucoup de frameworks de jeu.
- **Livraison exclusive** : `KeyboardHandler` ne livre `Pressed` que si `Owner.HasKeyboardFocus()`, c.-a-d. `GetDesktop().FocusedKeyboardHandler == this`. Pas de bubbling : une touche non traitee par l'element focus **n'est pas offerte a ses ancetres**. Les raccourcis de conteneur passent par le `HighPriorityKeyboardHandler` global ou par `TryHandleNavigationAction`.
- **Navigation semantique** : `UINavigationAction` (Tab/fleches/Submit/Cancel/...) est dispatchee element-focus-d'abord via le virtuel `TryHandleNavigationAction` (surcharge par ~23 controles), sinon deplacement de focus de repli. Clavier et gamepad alimentent le meme vocabulaire. Les touches "preservees" par un `MGTextBox` focus (fleches, Home/End, Space si editable, Enter/Tab selon `AcceptsReturn`/`AcceptsTab`) ne sont jamais reinterpretees en navigation — **sur le chemin brut uniquement** (voir faiblesse n. 2).
- **Scopes de focus** : pile (racine, cible de restauration) dans `UIFocusNavigationService`, poussee par `MGContextMenu` et le popup color-picker — piegeage du Tab + restauration a la fermeture, a la WPF.
- **Texte natif / IME** : un puits `IKeyboardTextInputSink.QueueTextInput` existe (fusion de caracteres natifs, synthese d'events pour caracteres sans touche physique) mais **rien ne cable `GameWindow.TextInput` dessus** ; la generation de caracteres retombe sur une table US-QWERTY codee en dur (voir faiblesse n. 6).

## Couche semantique (UI vs gameplay)

`InputRouter` parcourt une pile de `IInputContext` tries par priorite (stable) ; premier consommateur gagne ; chaque `InputRouteDecision` porte le nom du contexte gagnant et une raison lisible (excellent pour le debug). `MGUIInputContext` (priorite 100) **reserve inconditionnellement toutes les actions UI** (meme si aucun controle ne les consomme) et bloque les actions gameplay quand `MGDesktop.ShouldCaptureGameplayInput()` est vrai (overlay modal, menu contextuel ouvert, textbox editable focus). Pattern proche des action maps Unity / input mapping contexts Unreal — le bon modele pour un jeu, mais :

- la souris/le scroll/les axes ne passent **jamais** par le routeur (deux systemes d'arbitrage coexistent : "l'UI a-t-elle mange le clic" et "l'UI a-t-elle mange le bouton" se decident par des mecanismes differents) ;
- quatre tables de mapping paralleles doivent rester synchronisees a la main (`InputActionMapper`, cartes brutes de `MGDesktop`, doublons de `UIFocusNavigationService`, pont `InputAction`->`UINavigationAction`) ;
- aucun hote par defaut ne l'utilise — chaque integrateur doit ecrire sa boucle collect/map/route (voir `MiniGame.cs:980-1022`).

## Evaluation de modernite

### Points forts (au niveau ou au-dessus des standards)

- Separation detection/dispatch deterministe et testable (replay d'input deterministe via `UIToolingService.ReplayFrames`).
- `HandledBy` identitaire plus expressif que le `e.Handled` booleen de WPF (re-entree self, routage de drag, adoption de sequences).
- Capture implicite de drag robuste (DragEnd toujours apaire), streams clavier par pression, sequences multi-clic avec adoption de cible — des subtilites que beaucoup de frameworks ratent.
- Modele de focus moderne : transactionnel, source-track, `:focus-visible`-like, scopes avec restauration, memoire de focus par fenetre, parite gamepad complete.
- Clipping/scroll corrects par construction pour le hit-test (chaine d'intersections `ActualLayoutBounds` + retour de draw `RecentDrawWasClipped`).
- Modalite en defense-en-profondeur (consommation + hit-test + focus) et scopee par fenetre (piles modales par fenetre, composables).
- Fall-through propre vers la couche jeu (handlers auto a `EndUpdate` + `ShouldCaptureGameplayInput`).

### Ecarts vs WPF/Avalonia

- Pas de routed events : ni tunneling (`Preview*`), ni bubbling reel — l'ordre d'update fait office de routage ; la preemption parentale exige de muter l'etat global du tracker.
- Pas de hit-test global z-order-aware : l'occlusion est emulee par ordre d'update + flag cooperatif ; le hover et l'etat presse y echappent.
- Pas d'API de capture explicite (3 mecanismes ad hoc la remplacent).
- Clavier mono-cible strict (pas de chaine d'ancetres).
- Hit-test rectangle-only (coins arrondis, formes, `RenderScale` : la zone cliquable diverge des pixels dessines).
- Perte des transitions sous-frame (press+release dans une meme frame = aucun evenement) — inherent au polling, acceptable a 60 fps.
- Aucune reaction a la desactivation de la fenetre OS (`Game.IsActive`).

## Faiblesses confirmees par verification adversariale (audit 2026-09)

Chaque point ci-dessous a ete contre-verifie par un agent independant cherchant a le refuter ; les 6 sont **CONFIRMES**.

1. **[perf] Fast-path drag mort** — `MouseTracker.HasCurrentDragStart/Dragged/DragEndEvents` (`MouseTracker.cs:463-466`) testent `x.Value != null` sur des dictionnaires internes jamais nuls : les 3 flags sont toujours `true`, le bloc de dispatch drag de `MouseHandler.InvokeQueuedEvents` tourne chaque tick pour chaque handler abonne au drag et alloue une `List<DragStartCondition>` a chaque fois. Fix : `.Any(x => x.Value.Values.Any(v => v != null))` (pattern correct deja present pour les boutons, lignes 458-461). Benin fonctionnellement, pur cout.
2. **[correctness] Le chemin semantique contourne la preservation des touches de saisie** — `MGDesktop.TryHandleInputAction` ne passe pas par `ShouldPreserveTextEntryKey` : avec le routeur actif (`UseRawNavigationInput=false`), une fleche ou Tab pendant l'edition d'un textbox peut a la fois editer le caret **et** deplacer le focus — en violation de l'invariant documente dans `focus-input-behavior.md`. La configuration expediee (`InputActionMapper` + `MGUIInputContext` + MiniGame) est exactement la configuration vulnerable.
3. **[UX] Fuite de hover inter-fenetres** — voir section "Fenetres superposees". Etat `Hovered` + `Entered/MovedInside/Exited` + drag-drop atteignent les elements occlus d'une fenetre inferieure ; seuls les tooltips sont proteges.
4. **[leak] Les handlers ne sont jamais desenregistres des trackers** — `MouseHandler.Unsubscribe`/`KeyboardHandler.Unsubscribe` n'ont **zero** appelant dans le repo ; tout element ayant touche son handler (donc tout controle interactif, chaque `MGWindow` en cree 3) est retenu par le tracker a vie, enracinant les arbres d'elements des fenetres fermees. Pilote concret non borne : chaque flottement de `MGFloatingDockWindow` (docking) enregistre des handlers jamais liberes. Cout par tick : enumeration `Where` + allocations LINQ croissantes (pas de tri des handlers manuels, qui sont filtres avant). **CORRIGE (2026-09, tache 1 de `input-leaks-and-textinput-tasks.md`, decision D1)** : `MouseTracker.CreateHandler`/`KeyboardTracker.CreateHandler` n'ajoutent plus un handler manuel (`UpdatePriority == null`) a `_Handlers` — plus aucune racine tracker->element pour ce chemin ; voir `MGUI.Tests/Input/TrackerHandlerRegistrationTests.cs`.
5. **[leak] `MGTextBox`/`MGNumericUpDown` s'abonnent a `FocusedKeyboardHandlerChanged` sans jamais se desabonner** — lambdas anonymes dans les constructeurs (`MGTextBox.cs:1252-1258`, `MGNumericUpDown.cs:193`) : chaque textbox jamais cree reste enracine par le desktop et son handler tourne a chaque changement de focus, meme dans une fenetre fermee. Le pattern correct existe ailleurs (`MGGraphControls`, `MGPropertyGrid` se desabonnent) — incoherence, pas politique. **CORRIGE (2026-09, tache 2 de `input-leaks-and-textinput-tasks.md`, decision D2)** : `MGElement.OnKeyboardFocusChanged(bool)` est desormais invoque directement par le setter prive de `MGDesktop.FocusedKeyboardHandler` ; `MGTextBox`/`MGNumericUpDown` overrident ce virtuel au lieu de s'abonner a l'event public — plus d'abonnement de constructeur a desabonner. Fuite jumelle decouverte pendant la preparation du plan et corrigee dans la meme tranche (tache 3, decision D3) : `MGListBox` s'abonnait a `GetDesktop().Runtime.EndUpdate` (MGListBox.cs:1506-1520, avant correction) et enracinait la listbox pour la duree de vie du runtime ; remplace par un abonnement self-owned a son propre `OnBeginUpdate`. Voir `MGUI.Tests/Focus/KeyboardFocusNotificationTests.cs` et `MGUI.Tests/Controls/MGListBoxPressedItemTests.cs`.
6. **[i18n] Le puits de texte natif/IME n'est jamais cable** — `IKeyboardTextInputSink` est concu, teste unitairement, mais aucun code ne connecte `GameWindow.TextInput` (la couche `MGUI.MonoGame.Integration` cable deja `ClientSizeChanged` mais pas `TextInput`). Hors QWERTY-US (AZERTY, touches mortes, IME), la saisie retombe sur les tables codees en dur et produit des caracteres faux. Fix consommateur ~2 lignes (`game.Window.TextInput += (s, e) => tracker.QueueTextInput(e.Character, e.Key);`), mais non documente nulle part. **CORRIGE (2026-09, taches 4/5 de `input-leaks-and-textinput-tasks.md`, decision D4)** : nouvelle interface opt-in `ITextInputHost` implementee par `GameRenderHost<T>`, auto-cablee par `MonoGameBackendBootstrap.Create` ; cablage manuel equivalent documente et demontre pour `DelegateRenderHost` (MiniGame). Voir `MGUI.Tests/Input/GameRenderHostTextInputTests.cs`, `MGUI.Tests/Input/KeyboardTrackerTextInputTests.cs` et `Docs/monogame-host-integration-guide.md`.

**Fuite residuelle decouverte lors de la regression de bout en bout (tache 6, 2026-09, non couverte par D1-D4)** : fermer une fenetre (`MGWindow.TryCloseWindow` + retrait de `Desktop.Windows`) ne suffit pas a la rendre collectable. `MGWindow` cree en permanence, des son constructeur, un scope de ressources local (`EnsureResourceScope(UIResourceScope.Window)`) dont le `MGResources` s'abonne a `Desktop.Resources.OnDefaultThemeChanged` via `MGResources.SetParent`/`Parent_OnDefaultThemeChanged`, sans jamais s'en desabonner — chaine de retention verifiee experimentalement : `Desktop.Resources -> (delegate cible) window.LocalResources -> (event backing field) lambda capturant `this` -> window (et tout son sous-arbre)`. Initialement documentee plutot que corrigee, pour la meme raison que la fuite 4 (voir la note "Approche INTERDITE" ci-dessous) : ce repo re-affiche couramment la MEME instance de fenetre apres fermeture (`SampleBase.Show/Hide`, `BringToFront`), et desabonner ce lien au moment de la fermeture casserait silencieusement la propagation de theme a la reouverture, faute d'un point ou le rattacher. **CORRIGE (2026-09, post-plan)** : `MGResources.SetParent` s'abonne desormais au `OnDefaultThemeChanged` du parent via un forwarder faible (`WeakThemeChangedForwarder`, classe imbriquee de `MGResources`) qui ne reference le scope enfant que par `WeakReference` ; un forwarder dont l'enfant a ete collecte s'auto-desabonne au changement de theme suivant. Choix arbitre apres examen de l'alternative detacher-a-la-fermeture / rattacher-a-la-reouverture, impraticable pour trois raisons verifiees : `Desktop.Windows` est une simple `List<MGWindow>` sans hook d'ajout (`NotifyWindowOpened` n'est appele que pour les fenetres nested/modales, jamais pour le chemin `SampleBase.Show` -> `Desktop.Windows.Add`) ; un scope detache sans theme local fait jeter `MGResources.DefaultTheme` (MGResources.cs:418), donc `GetTheme()` exploserait sur toute fenetre cachee ; et `SetParent` ne re-propage pas un changement de theme survenu pendant le detachement (PreviousTheme null -> pas d'event, theme obsolete a la reouverture). Le lien faible preserve exactement "fermer n'est pas mourir" : tant que l'application tient l'instance, la propagation de theme fonctionne (fenetre fermee comprise, donc a la reouverture) ; quand l'application lache la fenetre, tout le domaine {resources locales, fenetre, sous-arbre} devient collectable. Ce n'est PAS un weak event generalise (la philosophie du plan reste respectee) : ce point d'abonnement est le seul a croiser deux domaines de duree de vie. Tests (`MGUI.Tests/Input/InputLifetimeRegressionTests.cs`) : `ClosedWindow_IsNotRootedByDesktopResourcesThemeSubscription` (ex-`WindowResourcesParentChain_IsAKnownResidualRoot_NotFixedByThisPlan`, assertion inversee), `ClosingWindow_AllowsTextBoxNumericUpDownAndListBox_ToBeCollected` (le helper de reflexion `DetachWindowLocalResourcesFromDesktop` est supprime : plus aucune neutralisation test-only), `ThemeChange_StillReachesWindow_WhileClosedAndAfterReopen` (propagation a travers GC, fenetre fermee puis re-affichee).

### Autres faiblesses notables (relevees par les lecteurs, non contre-verifiees individuellement)

- **Inversion de z-order dans les replis de navigation** : `GetWindowsFrontToBack`/`GetHoveredNavigationTarget`/`topWindowRoot` (`MGDesktop.cs:434-459` et leurs jumeaux vivants dans `UIFocusNavigationService.cs:369-389`) omettent le `.Reverse()` que la boucle d'update applique : quand rien n'est focus, Tab/auto-focus peut cibler la fenetre la plus en **arriere**. Signale independamment par deux lecteurs avec les memes details.
- **`WindowKeyboardHandler` est un point d'extension inerte** : documente comme "handled juste avant l'input de la fenetre", mais son proprietaire (la fenetre) n'a jamais le focus clavier, donc `Pressed` ne peut jamais etre livre.
- **Light-dismiss fragile hors du premier plan** : le dropdown `MGComboBox` se ferme sur `ReleasedOutside`, mais si le clic de fermeture atterrit sur une fenetre plus haute, la cloture de celle-ci consomme le release et le dropdown reste ouvert. Le menu contextuel n'y echappe que par son slot d'update privilegie.
- **`DerivedIsHitTestVisible` ne consulte que le parent immediat** (`MGElement.cs:1620`), contredisant sa doc (le pipeline interne, lui, chaine correctement).
- **`MGButton` ne marque pas ses clics handled par defaut** (SetHandledBy commente, `MGButton.cs:213,218`) : un clic sur un bouton atteint aussi ce qui le chevauche si l'abonne ne teste pas `e.IsHandled`.
- **Code mort / duplications** : bloc fast-path fully-clipped duplique verbatim dans `MGElement.Update` ; navigation dupliquee morte dans `MGDesktop` apres extraction vers `UIFocusNavigationService` (avec le meme bug d'inversion dans les deux copies) ; `AdjustedStartPosition` de `BaseMouseDragEndEventArgs` retourne la position de fin (copier-coller).
- **Machinerie de priorite inutilisee par le framework lui-meme** : deux regimes d'ordonnancement coexistent (priorite pour les handlers auto, traversee Core pour les manuels), tri LINQ double et redondant.
- **`KeyboardHandler` itere ~160 `Keys` par handler par tick** sans flag agrege d'early-out (le cote souris en a un).
- **Tests d'architecture fragiles** : `FocusArchitectureTests` lit les sources via des chemins absolus `d:\development\repo\MGUI\...` et assert des sous-chaines de code.

## Recommandations priorisees

Note 2026-09-01 : les points 1 a 3 sont couverts par le plan d'execution `Docs/Tasks/input-leaks-and-textinput-tasks.md`, qui **remplace** la piste initiale « Unsubscribe au teardown » (verifiee dangereuse : fermer une fenetre n'est pas la detruire — les Samples re-affichent la meme instance via `Desktop.Windows.Add/Remove`, et `IsValid` est definitif sans API de revalidation). Le plan retient : ne plus enregistrer les handlers manuels dans les trackers, notification directe `OnKeyboardFocusChanged` par le setter de focus, et seam `ITextInputHost` auto-cablee au bootstrap.

Mise a jour 2026-09 (tache 6 du plan, apres execution complete des taches 1 a 5) :

1. ~~**Fuites (4, 5)**~~ **FAIT** : voir decisions D1/D2/D3 ci-dessus — la troisieme fuite du meme type decouverte (`MGListBox` s'abonnait a `Runtime.EndUpdate` dans son constructeur, MGListBox.cs:1506-1520) est corrigee elle aussi. Une regression de bout en bout (`MGUI.Tests/Input/InputLifetimeRegressionTests.cs`) verrouille la collectabilite du sous-arbre d'une fenetre fermee pour ces trois mecanismes. Une fuite residuelle distincte (non D1-D4) a ete decouverte a cette occasion (chaine `Desktop.Resources` -> `MGWindow.LocalResources`, meme raison "fermer n'est pas mourir" que pour la fuite 4), d'abord documentee, puis corrigee post-plan via le forwarder faible cible decrit ci-dessus (`WeakThemeChangedForwarder` dans `MGResources.SetParent`).
2. ~~**Cabler `GameWindow.TextInput` -> `QueueTextInput` dans `MGUI.MonoGame.Integration`**~~ **FAIT** (6) : voir decision D4 ci-dessus et `Docs/monogame-host-integration-guide.md`.
3. **Corriger le fast-path drag** (1) — une ligne, gain par tick sur chaque handler abonne au drag.
4. **Corriger l'inversion `GetWindowsFrontToBack`** (les deux copies) et supprimer la copie morte de `MGDesktop`.
5. **Aligner le chemin semantique sur `ShouldPreserveTextEntryKey`** (2) avant de promouvoir le routeur au-dela de MiniGame.
6. **Decider du statut du hover inter-fenetres** (3) : soit l'assumer et le documenter comme limite, soit introduire un test d'occlusion desktop (le mecanisme `IsWindowOccludedAtMousePos` existe deja pour les tooltips et pourrait etre generalise au calcul de `HoveredElement` et a `PressedElement`).
7. **Ajouter des tests fenetres superposees** : press/release/scroll bloques, hover (comportement choisi), clavier par focus, `AllowsClickThrough`, light-dismiss d'un popup dans une fenetre non frontale.
8. Envisager a plus long terme : activation-fenetre-au-clic (ou au moins un `ActiveWindow` optionnel), interface `ITextEntryHost` a la place des tests `is MGTextBox`, point d'extension reel remplacant `WindowKeyboardHandler`.

## Documents lies

- `focus-input-behavior.md` — contrat des invariants focus/input (francais, toujours exact dans l'intention ; ne couvre pas les primitives de handlers).
- `focus-input-tasks.md` / `focus-input-risk-review.md` — archives du refactor focus (mars) ; l'ordre du tick qui y est decrit est reste correct.
- `gameplay-ui-input-routing-architecture.md` — proposition d'architecture (aspirationnelle : les 5 contextes esquisses n'ont jamais ete implementes tels quels).
- `gameplay-ui-input-routing-v1.md` — etat reellement livre de la couche semantique (le plus fiable des trois docs de routing).
- `graph-view-v1-guide.md` — gestes d'input du graph view.
- `custom-render-backend-integration.md` — point d'entree hote : c'est l'hote qui met a jour `InputTracker` avant `desktop.Update()`.
