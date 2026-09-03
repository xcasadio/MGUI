# Taches activation de fenetre et points d'extension input

## Objectif

La conception et les huit decisions utilisateur sont deja validees et vivent dans
`Docs/input-window-activation-design.md` (section 5 pour le decoupage, section 6
pour les decisions). Ce fichier ne fait que porter l'execution du lot valide : les
slices 1 a 4 du decoupage, dans l'ordre. La slice 5 (point d'extension clavier de
fenetre) est reportee par decision utilisateur et n'est pas incluse dans le lot a
executer maintenant ; elle figure ici a titre de reference, marquee reportee.

Ce document est destine a un agent IA implementeur.

## Consignes de travail pour l'agent IA

- Executer les taches strictement dans l'ordre.
- Faire exactement 1 commit par tache terminee.
- Mettre a jour le statut de la tache dans ce fichier avant chaque commit.
- Si une tache est bloquee, la marquer `⛔`, decrire le blocage juste en dessous, puis s'arreter.
- Ne pas faire de refactor hors perimetre.
- Ajouter ou adapter des tests a chaque tache impliquant un changement de comportement.
- Surfaces epinglees : les ensembles de membres de `IRenderHost`/`IUIDesktopRuntime` et des sous-chaines verbatim de `Game1.cs`/`MiniGame.cs` sont assertes par `RawInputSourceTests` et `HostRuntimeContractTests` — les lire avant d'editer ces fichiers.
- Consequence de la tache 2 (defaut `ActivatesOnClick = true`) : toute fenetre existante gagne le cablage clic -> premier plan, y compris dans les tests headless qui pressent des boutons souris sur des fenetres superposees. Une seule suite est concernee et doit etre revue deliberement (pas accidentellement) par la tache 2 : `MGUI.Tests/Input/OverlappingWindowsInputRoutingTests.cs`. `MGUI.Tests/Input/ContextMenuHoverOcclusionTests.cs` ne presse jamais de bouton souris (son helper `Frame`, `MGUI.Tests/Input/ContextMenuHoverOcclusionTests.cs:67-70`, n'utilise que `ButtonState.Released` pour les cinq boutons a chaque frame) : elle ne peut pas churner et ne doit pas etre modifiee par la tache 2.
- Fait de faisabilite pour la slice 5 (reportee, information seulement) : `IKeyboardHandlerHost.HasKeyboardFocus()` a une implementation d'interface par defaut qui retourne `true` (`MGUI.Shared/Input/Keyboard/KeyboardTracker.cs:17`), tandis que `MGElement` la surcharge explicitement (`MGUI.Core/UI/MGElement.cs:1621`, `GetDesktop().FocusedKeyboardHandler == this`) — c'est ce qui rend `MGWindow.WindowKeyboardHandler` inerte aujourd'hui et ce qui permet au futur handler preview de la slice 5 de fonctionner sans toucher cet invariant.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale

1. `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
2. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
3. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "Focus|Input" --logger "console;verbosity=minimal"` + filtre cible de la tache
4. Taches touchant la couche semantique (3, 4) : `dotnet build .\MGUI.MiniGame\MGUI.MiniGame.csproj --no-restore`
5. Critere transversal : aucun test nouvellement rouge par rapport a l'etat de depart.

## Taches

### ✅ 1. `ActiveWindow` observable (sans clic)

But:

introduire la notion de fenetre "active", aujourd'hui absente du framework, en lecture seule et derivee du focus courant — sans activation automatique, cette slice resout uniquement l'observabilite (option (B) de la section 3.a du document de conception).

Travail attendu:

- ajouter `MGDesktop.ActiveWindow` (lecture seule) derivee du focus courant (`FocusedKeyboardHandler?.SelfOrParentWindow`), en reutilisant le mecanisme deja calcule via `State.WindowFocusHistory` (`MGUI.Core/UI/MGDesktop.cs:965`) ;
- ajouter l'evenement `MGDesktop.ActiveWindowChanged` ;
- ne pas cabler d'activation au clic dans cette tache (c'est la tache 2) ;
- tests : focus un element d'une fenetre B alors que `ActiveWindow` valait A -> `ActiveWindow` passe a B et `ActiveWindowChanged` se declenche, sans appel a `BringToFront`.

Criteres d'acceptation:

- focus un element d'une fenetre B fait passer `ActiveWindow` de A a B sans `BringToFront` ;
- aucun test nouvellement rouge.

Commit recommande:

- `feat: add observable ActiveWindow derived from keyboard focus`

Resultat:

- `MGUI.Core/UI/MGDesktop.cs` : ajout de la propriete en lecture seule `ActiveWindow` (`=> FocusedKeyboardHandler?.SelfOrParentWindow`) et de l'evenement `ActiveWindowChanged` (`EventHandler<EventArgs<MGWindow>>`), tous deux juste apres le setter de `FocusedKeyboardHandler`. Le setter capture `PreviousActiveWindow` (`Previous?.SelfOrParentWindow`) avant d'appliquer le nouveau focus, puis compare a `ActiveWindow` (qui reutilise le meme calcul deja fait pour `State.WindowFocusHistory` a la ligne d'origine) : si la fenetre active a change, `NPC(nameof(ActiveWindow))` puis `ActiveWindowChanged` sont declenches avec `PreviousValue`/`NewValue`. Aucun appel a `BringToFront` n'est ajoute ; `Windows` n'est jamais reordonne par ce changement.
- Nouveau fichier de tests `MGUI.Tests/Focus/ActiveWindowObservableTests.cs` (3 tests, pattern `GraphTestRuntime` + reflexion sur le setter prive de `FocusedKeyboardHandler` calque sur `KeyboardFocusNotificationTests.cs`) : focus d'un bouton de la fenetre B alors que `ActiveWindow` valait A -> `ActiveWindow` devient B, `ActiveWindowChanged` se declenche exactement une fois avec `PreviousValue=A`/`NewValue=B`, et l'ordre de `desktop.Windows` est inchange (assertion directe, pas de mock de `BringToFront`) ; `ActiveWindow` vaut `null` sans `FocusedKeyboardHandler` ; deplacer le focus entre deux elements de la meme fenetre ne redeclenche pas `ActiveWindowChanged`.
- Validation : `dotnet build MGUI.Tests/MGUI.Tests.csproj --no-restore` OK ; `dotnet build MGUI.Samples/MGUI.Samples.csproj --no-restore` OK ; `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-build --filter "Focus|Input"` -> 420/420 verts ; suite complete `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-build` -> 1430/1430 verts (baseline 1427 + 3 nouveaux), aucun test rouge.

### ✅ 2. `MGWindow.ActivatesOnClick`, defaut `true`

But:

cabler l'activation de fenetre au clic hors du module docking, generalisee au niveau `MGWindow`, avec deplacement du focus clavier resolu par le mecanisme existant, garde par overlay modal actif, et exclusion explicite des sous-types popup de `MGWindow`.

Travail attendu:

- ajouter `MGWindow.ActivatesOnClick` (defaut `true`, decision utilisateur) cablant un handler equivalent a l'ancien cablage local du docking, qui appelle `Desktop.BringToFront(this)` / `ParentWindow.BringToFront(this)` puis deplace le focus clavier dans la fenetre cliquee selon la regle de resolution ci-dessous ; `ActiveWindow` (slice 1) reflete ce nouveau focus automatiquement puisqu'elle en est deja derivee (`FocusedKeyboardHandler?.SelfOrParentWindow`) — il n'y a pas de mise a jour manuelle d'`ActiveWindow` a ecrire, et il n'en existe aucun mecanisme, `ActiveWindow` etant en lecture seule (tache 1) ;
- regle de resolution du focus au clic (decision utilisateur) : appeler le helper par racine EXISTANT `MGDesktop.ResolveAutoFocusTarget(MGElement root, bool preferWindowDefault)` (`MGUI.Core/UI/MGDesktop.cs:1029`), qui effectue deja toute la resolution (`window.DefaultFocusElement`, puis `State.WindowFocusHistory[window]` (`Dictionary<MGWindow, MGElement>`, `MGUI.Core/UI/UIViewState.cs:12`), puis le premier element focusable, les deux premiers filtres par `IsNavigationTarget(...) && window.IsSelfOrAncestorOf(...)`) et delegue au statique 4 arguments `MGUI.Core/UI/MGDesktop.cs:244`. Ce helper est `private` aujourd hui : changer UNIQUEMENT sa visibilite en `internal` pour le rendre appelable depuis `MGWindow.cs`, sans toucher a sa logique. Ne PAS reecrire la resolution ni retraverser les elements focusables : le statique 4 arguments seul ne suffit pas, car son argument `firstFocusable` exige `MGDesktop.GetFocusableElements(MGElement root)`, `private` (`MGUI.Core/UI/MGDesktop.cs:427`), et la surcharge publique sans argument (`:443`) resout contre la racine de navigation, pas contre la fenetre cliquee ;
- valeur de `preferWindowDefault` : **`false`**. Une reactivation au clic doit rendre a l utilisateur l element ou il etait (`lastFocused` en premier), tandis que `true` est reserve aux entrees fraiches — ouverture de fenetre (`MGUI.Core/UI/Navigation/UIFocusNavigationService.cs:324`) et navigation (`:112`, `:142`, `:171`) — et `false` est deja le cas de reprise (`:352`) ;
- si le clic lui-meme focus un element (l'element clique est focusable), CE focus l'emporte : l'activation ne doit pas l'ecraser avec la cible resolue ci-dessus ;
- si la resolution ne produit aucune cible valide, le focus reste INCHANGE : l'activation n'efface jamais un focus existant ;
- l'activation doit rester sans effet derriere un overlay modal actif, en reutilisant la garde `HasModalWindow`/`OverlayHost.IsModal` existante, sans en ajouter une nouvelle ;
- `BringToFront` reste soumis au tri par `IsTopmost` — une fenetre non-topmost cliquee ne doit jamais passer devant une fenetre topmost ;
- migrer `MGFloatingDockWindow` vers ce mecanisme generique : supprimer le cablage local (`MGUI.Core/UI/Docking/Controls/MGFloatingDockWindow.cs:100`, `MouseHandler.LMBPressedInside += (_, _) => BringToFront();`) au profit d'`ActivatesOnClick`, afin que la garde modale soit appliquee de facon uniforme (decision utilisateur) ;
- exclure explicitement du cablage les quatre sous-types popup de `MGWindow` qui gagneraient sinon un reordonnancement de fenetres imbriquees a chaque clic interne (decision utilisateur) : fixer `ActivatesOnClick = false` pour chacun, precisement a l'endroit suivant —
  - `MGContextMenu` (`MGUI.Core/UI/MGContextMenu.cs:45`, sous-classe directe de `MGWindow`) : dans son constructeur ;
  - `MGToolTip` (`MGUI.Core/UI/MGToolTip.cs:18`, sous-classe directe de `MGWindow`) : dans son constructeur ;
  - le dropdown de `MGComboBox` (propriete `Dropdown` declaree `MGUI.Core/UI/MGComboBox.cs:522`, affectee depuis le template `MGUI.Core/UI/MGComboBox.cs:681` dans `AttachControlTemplateStructure`, ajoutee comme fenetre imbriquee `MGUI.Core/UI/MGComboBox.cs:626`) : immediatement apres l'affectation a `:681` — `Dropdown` n'est pas une sous-classe dediee mais une `MGWindow` issue du template, il n'y a donc pas de constructeur de type dropdown a modifier ;
  - la popup du color picker, `MGColorPickerPopup.PopupWindow` (propriete `MGUI.Core/UI/Color/MGColorPickerPopup.cs:13`, construite `new MGWindow(...)` a `:39-42` dans le constructeur de `MGColorPickerPopup`, ajoutee comme fenetre imbriquee `:87`) : dans ce constructeur de `MGColorPickerPopup`, immediatement apres la construction a `:39-42` — `MGColorPickerPopup` elle-meme n'est pas une `MGWindow`, seule sa `PopupWindow` l'est ;
- perimetre : `MGUI.Core/UI/MGWindow.cs`, `MGUI.Core/UI/MGDesktop.cs` (changement de visibilite du seul helper `ResolveAutoFocusTarget(MGElement, bool)`, aucune autre modification), `MGFloatingDockWindow.cs:100`, `MGContextMenu.cs`, `MGToolTip.cs`, `MGComboBox.cs`, `MGColorPickerPopup.cs`, plus les fichiers de tests que cette tache doit modifier ou creer : `MGUI.Tests/Input/OverlappingWindowsInputRoutingTests.cs` et le ou les nouveaux fichiers de tests portant le cas de chevauchement en defaut `true` et le cas docking. `MGUI.Tests/Input/ContextMenuHoverOcclusionTests.cs` est explicitement EXCLUE du perimetre ;
- revoir deliberement (voir avertissement de churn en "Consignes") `MGUI.Tests/Input/OverlappingWindowsInputRoutingTests.cs` : ajuster les tests dont le z-order attendu change du fait du nouveau cablage clic par defaut ; `MGUI.Tests/Input/ContextMenuHoverOcclusionTests.cs` ne presse jamais de bouton souris (`ButtonState.Released` uniquement dans son helper `Frame`, `:67-70`) et ne doit PAS etre modifiee ;
- ajouter au moins UN nouveau test qui garde le defaut `ActivatesOnClick = true` avec deux fenetres superposees et epingle les DEUX directions : une pression dans la zone de chevauchement occluse ne fait PAS remonter la fenetre du dessous au premier plan, et une pression sur sa zone exposee la fait remonter, les deux exprimees comme assertions sur l'ordre de `desktop.Windows` ;
- ajouter un test docking dedie qui epingle le comportement pre-existant de la fenetre flottante (clic ramene au premier plan) apres la migration.

Criteres d'acceptation:

- deux fenetres non-topmost, clic sur celle du dessous la ramene au premier plan ;
- aucun effet sous overlay modal ;
- clic sur une fenetre en arriere-plan B la ramene au premier plan ET deplace le focus dans B selon la resolution decrite ci-dessus, avec `ActiveWindow` devenant B et `ActiveWindowChanged` se declenchant ;
- clic sur une fenetre sans cible de focus valide la ramene au premier plan et laisse le focus inchange ;
- clic sur un element focusable focus CET element, pas la cible par defaut resolue par `ResolveAutoFocusTarget` ;
- sur une fenetre ayant A LA FOIS un `DefaultFocusElement` et une entree de `WindowFocusHistory` differente, le clic d activation focus l entree de `WindowFocusHistory` (consequence de `preferWindowDefault: false`), et un test l epingle ;
- un clic sur un item a l'interieur d'un dropdown `MGComboBox` ouvert, et un clic sur un item a l'interieur d'un `MGContextMenu` ouvert, ne reordonnent pas les fenetres imbriquees ;
- le test `ComboBoxDropdown_ReleasedOutsideClick_*` existant dans `MGUI.Tests/Input/OverlappingWindowsInputRoutingTests.cs` passe toujours sans modification ;
- `MGUI.Tests/Input/OverlappingWindowsInputRoutingTests.cs` est revue et ajustee deliberement ; `MGUI.Tests/Input/ContextMenuHoverOcclusionTests.cs` n'est pas modifiee ;
- au moins un nouveau test garde le defaut `ActivatesOnClick = true` avec deux fenetres superposees et epingle les deux directions (pression occluse ne remonte pas la fenetre du dessous, pression exposee la remonte), via assertions sur l'ordre de `desktop.Windows` ;
- un test docking dedie epingle le comportement pre-existant de la fenetre flottante (clic ramene au premier plan) apres la migration ;
- aucun test nouvellement rouge en dehors du churn attendu et traite ci-dessus.

Commit recommande:

- `feat: activate window on click via MGWindow.ActivatesOnClick`

Prerequis : tache 1.

Resultat:

- `MGUI.Core/UI/MGDesktop.cs` : changement de visibilite du seul helper cite, `ResolveAutoFocusTarget(MGElement, bool)`, `private` -> `internal`, aucune autre modification. Le texte de la tache citait `:1029` ; la ligne reelle est `:1047`, decalage du a l'ajout de `ActiveWindow`/`ActiveWindowChanged` par la tache 1 plus haut dans le fichier (le nom, la signature et le corps correspondent exactement a ce qui est decrit, verifie avant d'editer).
- `MGUI.Core/UI/MGWindow.cs` : ajout de la propriete `ActivatesOnClick` (defaut `true`, pattern champ prive + NPC identique a `AllowsClickThrough`, juste au-dessus) et du cablage dans le constructeur, sur `MouseHandler.LMBPressedInside` (juste apres le bloc `DragStartOutside` existant) : garde `!ActivatesOnClick || Desktop.IsBlockedByModalOrOverlay(this)` (reutilise tel quel, aucune nouvelle garde) ; `BringToFront` via `ParentWindow.BringToFront(this)` si fenetre imbriquee sinon `Desktop.BringToFront(this)` ; puis, uniquement si `Desktop.QueuedFocusedKeyboardHandler` est encore `null` (donc si le clic n'a pas deja mis en file d'attente le focus d'un descendant cliquable via `MGElement.IsFocusable`), resolution par `Desktop.ResolveAutoFocusTarget(this, false)` et `Focus(KeyboardFocusSource.Pointer)` sur la cible si non-nulle. La precedence "le clic gagne" repose sur l'ordre reel de traversee de `MGElement.Update()` (post-ordre : `UpdateContents` avant `_MouseHandler.ManualUpdate()` de l'element courant), verifiee par lecture de code puis par mutation (voir plus bas).
- `MGUI.Core/UI/Docking/Controls/MGFloatingDockWindow.cs:100` : suppression du cablage local `MouseHandler.LMBPressedInside += (_, _) => BringToFront();`. La methode `public void BringToFront()` de cette classe (helper local desormais sans appelant) est laissee telle quelle, hors perimetre de la tache.
- `MGContextMenu.cs`, `MGToolTip.cs`, `MGComboBox.cs`, `MGColorPickerPopup.cs` : `ActivatesOnClick = false` fixe precisement aux quatre emplacements listes dans la tache.
- Interaction avec l'occlusion croisee deja livree : aucune verification `IsOccludedAtMousePos` supplementaire ajoutee. `MGDesktop.Update()` traite les fenetres du dessus vers le dessous (`Windows.Reverse().OrderByDescending(IsTopmost)`), et les handlers catch-all deja existants du niveau fenetre (`MGWindow.cs`, bloc `PressedInside`/`ReleasedInside`/etc juste avant le nouveau cablage) marquent l'evenement partage comme gere (`e.SetHandledBy(this, false)`) avant que la fenetre du dessous ne soit mise a jour ; `MouseHandler.LMBPressedInside` de la fenetre occluee n'est donc jamais invoque (`HandledInputPolicy.Self` par defaut). Verifie par un test de mutation ad-hoc (sonde temporaire abonnee directement a `LMBPressedInside`) : 0 invocation sur la fenetre occluee, avec ou sans le nouveau cablage.
- Interaction avec la capture de drag active : `IActiveMouseDragCapture` ne concerne que `MGResizeGrip` et ne module que la persistance de `HoveredElement`/`PressedElement` pendant un drag en cours (donc apres la pression initiale) ; `LMBPressedInside` ne se declenche que sur le tick de pression initial, avant qu'un drag ne soit en cours, donc aucune interaction reelle avec le nouveau cablage.
- Garde modale : `Desktop.IsBlockedByModalOrOverlay(this)` (`MGDesktop.cs:250`, deja `internal`, non modifiee) reutilisee telle quelle. Test de mutation : dans les deux scenarios construits pour les tests (fenetre avec son propre `HasModalWindow`, et fenetre bloquee par un `OverlayHost.IsModal` global), retirer cet appel explicite ne fait PAS echouer les tests correspondants, car le mecanisme deja livre (`ComputedIsHitTestVisible`/`CanReceiveMouseInput`, deja reduit par `HasModalWindow` et par `IsHitTestVisible=false` cote `MGDesktop.Update`) empeche deja `_MouseHandler.ManualUpdate()` de tourner sur la fenetre bloquee (sonde ad-hoc : 0 invocation de `LMBPressedInside` dans les deux cas). L'appel explicite est donc actuellement redondant pour ces deux scenarios precis mais conserve tel que demande par la tache (defense en profondeur, aucune nouvelle garde inventee) ; signale ici comme observation, pas comme anomalie.
- Tests : nouveau fichier `MGUI.Tests/Input/WindowActivationOnClickTests.cs` (10 tests) couvrant chaque critere d'acceptation : clic sur fenetre en arriere-plan -> premier plan + resolution focus + `ActiveWindow`/`ActiveWindowChanged` ; clic sans cible de focus valide -> premier plan, focus inchange ; clic sur element focusable -> priorite au focus du clic sur la cible resolue ; `WindowFocusHistory` prioritaire sur `DefaultFocusElement` (`preferWindowDefault: false`) ; aucun effet sous modale (`PushModalWindow` et `OverlayHost`/`AddOverlay`) ; chevauchement bidirectionnel (pression occluse ne remonte pas, pression exposee remonte, assertions sur `desktop.Windows`) ; les quatre popups epingles a `ActivatesOnClick = false` ; clic sur item de dropdown `MGComboBox` ouvert ne reordonne pas `NestedWindows` (verifie juste apres la pression, avant que la selection ne ferme et retire le dropdown au relachement) ; clic dans un `MGContextMenu` ouvert (sur son separateur, point non focusable) ne perturbe ni `desktop.Windows` ni le focus existant. Nouveau fichier `MGUI.Tests/Input/FloatingDockWindowActivationTests.cs` (1 test) : epingle le comportement pre-existant de `MGFloatingDockWindow` (clic ramene au premier plan) apres la migration, via `MGDockHost`/`DockPanelNode` construits directement (sans passer par `DetachToFloating`). Les 10 tests neufs sur la logique de resolution/precedence/garde-modale ont ete verifies discriminants par mutation manuelle (retrait temporaire de la garde de precedence du clic, inversion de `preferWindowDefault`, retrait de l'exclusion `Dropdown.ActivatesOnClick`) : chacun echoue comme attendu, puis le code de production a ete restaure a l'identique (diff verifie propre).
- `MGUI.Tests/Input/OverlappingWindowsInputRoutingTests.cs` : revue deliberement (commentaire de classe ajoute documentant la revue) ; aucun de ses 7 tests n'a besoin d'ajustement, aucun n'assertant l'ordre de `desktop.Windows` (seule chose que le nouveau cablage peut changer) — ils assertent des compteurs de consommation, `HoveredElement`/`PressedElement`, le routage clavier par focus, et `IsDropdownOpen`. `MGUI.Tests/Input/ContextMenuHoverOcclusionTests.cs` non modifiee (conforme a l'exclusion de perimetre).
- Validation : `dotnet build MGUI.Tests/MGUI.Tests.csproj --no-restore` OK ; `dotnet build MGUI.Samples/MGUI.Samples.csproj --no-restore` OK ; `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-build --filter "Focus|Input"` -> 430/430 verts ; suite complete `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-build` -> 1440/1440 verts (baseline 1427 + 3 tache 1 + 10 tache 2), aucun test rouge.

### ⚪ 3. `ITextEntryHost` + migration des 6 sites `MGDesktop.cs`

But:

remplacer les tests `is MGTextBox` par une interface publique, pour qu'un controle d'edition tiers hors `MGUI.Core` beneficie des memes protections (gameplay non bloque pendant la saisie, touches de navigation internes volees, pas de nettoyage de focus a la readonly).

Travail attendu:

- definir dans `MGUI.Core/UI` (a cote de `MGTextBox`, decision utilisateur), un nouveau fichier interface public :

```csharp
public interface ITextEntryHost
{
    bool IsReadonly { get; }
    event EventHandler<bool> ReadonlyChanged;
    bool ShouldPreserveTextEntryKey(Keys key);
}
```

- `MGTextBox` implemente l'interface : `IsReadonly`/`ReadonlyChanged` sont deja publics (`MGTextBox.cs:935`/`:957`, implementation sans changement) ; le membre `internal bool ShouldPreserveTextEntryKey(Keys key)` existant (`MGTextBox.cs:69`) est CONSERVE tel quel, sans aucune modification, et une NOUVELLE implementation d'interface explicite est AJOUTEE en plus, qui delegue vers lui : `bool ITextEntryHost.ShouldPreserveTextEntryKey(Keys key) => ShouldPreserveTextEntryKey(key);`. C'est cette coexistence (membre `internal` conserve + implementation explicite ajoutee qui delegue vers lui) qui permet a la tache 3 de compiler seule, sans toucher `UIFocusNavigationService.cs:454` (qui continue d'appeler le membre `internal` directement et releve de la tache 4) ; la surface publique de `MGTextBox` ne s'agrandit pas, l'implementation explicite n'etant visible qu'a travers une reference `ITextEntryHost` ;
- migrer les 6 sites `MGDesktop.cs` vers `ITextEntryHost` (liste faisant autorite) : `:545` (`ResolveSemanticInputMode`), `:558` (`ShouldCaptureGameplayInput`), `:1366` (meme test, chemin `Update` brut), `:953`, `:960` (setter de `FocusedKeyboardHandler`, abonnement/desabonnement a `ReadonlyChanged`), `:985` (`TextBox_ReadonlyChanged`) ;
- tests : suite `Focus|Input` inchangee ; un double `ITextEntryHost` non-`MGTextBox` prouve le fonctionnement pour un type tiers (gameplay bloque pendant sa saisie, `ReadonlyChanged` nettoie bien le focus).

Criteres d'acceptation:

- suite `Focus|Input` inchangee ;
- un double `ITextEntryHost` non-`MGTextBox` prouve le fonctionnement pour un type tiers ;
- apres la tache 3 seule, la compilation reussit sans aucune modification hors de `MGTextBox.cs`, `MGDesktop.cs` et du nouveau fichier interface, et `rg "is MGTextBox" MGUI.Core/UI/MGDesktop.cs` ne retourne rien ;
- aucun test nouvellement rouge.

Commit recommande:

- `feat: introduce ITextEntryHost and migrate MGDesktop text-entry sites`

### ⚪ 4. Migration des 2 sites `UIFocusNavigationService.cs`

But:

completer la migration `ITextEntryHost` sur le service de navigation, dernier sous-systeme testant encore `is MGTextBox` ou appelant un membre `internal` de `MGTextBox`.

Travail attendu:

- migrer `UIFocusNavigationService.cs:216` (`TryDispatchNavigationAction`, flag `isTextEntryFocused`) et `:454` (`ShouldPreserveTextEntryKey`, qui appelle `MGTextBox.ShouldPreserveTextEntryKey`, `internal`) vers `ITextEntryHost` ;
- perimetre : `UIFocusNavigationService.cs` uniquement ;
- tests : suite `Focus|Input` inchangee ; garde de preservation testee avec un double `ITextEntryHost` tiers.

Criteres d'acceptation:

- suite `Focus|Input` inchangee ;
- garde de preservation testee avec un double tiers ;
- aucun test nouvellement rouge.

Commit recommande:

- `refactor: migrate UIFocusNavigationService to ITextEntryHost`

Prerequis : tache 3.

### ⚪ 5. [REPORTEE] Point d'extension clavier de fenetre + depreciation

Reportee par decision utilisateur (decision 6 de `Docs/input-window-activation-design.md`) : non incluse dans le lot a executer maintenant. La forme est deja decidee (decision 5 du meme document) : un handler "preview" par fenetre, pompe **avant** les enfants de la fenetre pendant `MGWindow.Update`, documente comme l'equivalent window-scoped du `HighPriorityKeyboardHandler` du desktop — pas un fallback pour les touches non consommees, ce qui serait du bubbling. Cette tache reste independante des taches 1 a 4 et pourra etre reprise dans une session dediee une fois le report leve.

But:

donner a `MGWindow.WindowKeyboardHandler` (inerte aujourd'hui, cf. `Docs/input-window-activation-design.md` section 1.c) un point d'extension clavier de fenetre reellement fonctionnel, sans toucher l'invariant de focus global.

Travail attendu (pour reference, non a executer avant leve du report):

- ajouter un nouveau membre "preview" pompe systematiquement pendant `MGWindow.Update`, voyant le clavier du tick que le focus soit sur la fenetre ou un descendant ;
- marquer `WindowKeyboardHandler` `[Obsolete]` (avertissement, pas erreur), conserve tel quel — source-compatible, il ne recevait deja rien ;
- un abonne au point d'extension doit ignorer les touches tant qu'un `ITextEntryHost` a le focus (regle qui rend le preview sans danger, pas de vol de touches d'edition) ;
- perimetre : `MGWindow.cs`.

Criteres d'acceptation:

- un handler abonne au nouveau membre recoit `Pressed` du tick que le focus soit sur la fenetre ou un descendant ;
- `WindowKeyboardHandler` compile (avec avertissement) et ne recoit toujours rien ;
- independant des taches 1 a 4.

Commit recommande:

- `feat: add window-scoped preview keyboard handler, deprecate WindowKeyboardHandler`
