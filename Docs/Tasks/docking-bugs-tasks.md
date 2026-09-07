# Taches correctifs docking (bugs du 7 septembre 2026)

## Objectif

Corriger les trois bugs releves par l'auteur dans la demo docking (`MGUI.Samples/Features/DockingDemo.cs`, scenario `SCN-DOCK-001`) apres la tache 3 du backlog styling, a la racine et sans contournement :

1. les boutons epingler/fermer des onglets n'apparaissent pas sur les groupes non cliques ;
2. un panneau devenu flottant ne peut plus etre redocke ;
3. le chrome docking est gris/noir alors que le menu contextuel des onglets et la liste deroulante des onglets sont bleus.

## Contexte : causes racines (investigation en lecture seule du 7 septembre 2026, contre-verifiee)

- Bug 1, preexistant (commits `5c7bb65` mars 2026 et `e0d65c7` avril 2026), sans lien avec le focus ni le survol. `MGDockTabItem` positionne ses icones d'apres les bounds des boutons freres (`GetCloseIconBounds` lit `_closeButton.LayoutBounds`, `MGDockTabItem.cs:373` ; `GetPinIconBounds` lit `_pinButton.LayoutBounds`, `:389`), mais `MGElement.UpdateLayout` arrange les composants (`MGElement.cs:3151-3155`) AVANT `UpdateContentLayout` (`:3211`), seul endroit qui positionne ces boutons (`MGDockTabItem.cs:637-654`). Les icones sont donc placees a la position du passage precedent, puis clippees (`ClipToBounds` par defaut, `MGElement.cs:2077`). Un clic sur un onglet force un nouveau passage (`MGDockTabGroup.cs:538`), d'ou l'impression que seuls les groupes "avec le focus" ont leurs boutons. Le meme defaut latent existe dans `MGDockTabGroup` (icone deroulante `:324`, icone d'etat de fenetre `:235`). En outre les visuels dependant du survol ne sont evalues que pendant le layout (`MGDockTabItem.cs:484`, `:495`, `:656`), jamais au dessin, contrairement au pattern de `MGSlider.DrawSelf` (`MGSlider.cs:1041`).
- Bug 2, regression du hit-test z-order du 2 septembre 2026 (commit `a331639`). `MGFloatingDockWindow` construit son contenu avec la fenetre du host (`new MGDockTabGroup(ownerHost.ParentWindow)`, `MGFloatingDockWindow.cs:73`) et non avec elle-meme ; les onglets heritent de ce `ParentWindow` (`MGDockTabGroup.cs:410`), fixe une fois pour toutes (`MGElement.cs:607`, `:2047`). La fenetre flottante est enregistree comme fenetre imbriquee de cette meme fenetre principale (`MGDockHost.cs:1301`, `:1325`). Depuis `a331639`, `MGElement.IsInside` demande a `SelfOrParentWindow.IsUnscaledPositionOccluded(...)` (`MGElement.cs:1480`) si une fenetre imbriquee couvre le point (`MGWindow.cs:1211-1224`), sans exemption pour la fenetre qui affiche l'element : tout element d'une fenetre flottante s'auto-occlut. `RMBReleasedInside` (`MouseHandler.cs:566`) et `DragStart` (`MouseHandler.cs:827`, position d'appui d'origine, `MouseTracker.cs:461`) ne se declenchent plus : ni menu contextuel, ni drag, ni changement d'onglet, ni fermeture, ni contenu applicatif. Seul le chrome de la fenetre (son propre `MouseHandler`) reste vivant. Le contenu applicatif des panneaux est cree par l'application avec la fenetre principale (`DockingDemo.cs:180-199`, un `MGStackPanel` a plusieurs niveaux), mis en cache (`MGUI.Core/UI/Docking/DockLayout/DockPanelNode.cs:272-279`) et seulement re-parente visuellement par sa racine (`MGDockTabGroup.cs:536`) : corriger le seul point (a) ne le ranime pas. L'entree "Dock" du menu contextuel prevue par `MGUI.Core/UI/Docking/TODO-DockingManager.md` (item 10.4) n'a jamais existe.
- Bug 3, preexistant, donnees de theme. Un seul theme est en jeu : la demo ne fixe aucun theme, `MGDesktop` cree `Dark_Blue` par defaut, et le menu contextuel comme la liste deroulante (un `MGContextMenu`) resolvent le meme theme par la chaine de scopes (`MGElement.GetTheme()`, `MGElement.cs:191` ; `MGWindow.Theme` est l'override local, nul ici). Le bloc `Docking` de `Dark_Blue` (`MGUI.Core/UI/Themes/BuiltInThemes.xaml:434-479`) est une palette gris Visual Studio (TabNormalBackground rgb 45,45,48 ; TabActive rgb 37,37,38 ; AutoHideStrip rgb 30,30,32 ; SplitterNormal rgb 64,64,64) alors que le ContextMenu du meme theme est bleu marine (rgb 11,28,72). Les cinq controles docking templates prennent bien leurs couleurs du theme (premiere application de template, `MGControlTemplate.cs:107-110`). Restent codes en dur, hors palette : le survol des boutons compacts du tab group (`new Color(70,70,74)`, `MGDockTabGroup.cs:367`), ses icones (`new Color(200,200,200)`, `:259`, `:264`, `:320`, `:342`), et `MGDockPreviewOverlay` (rgb 0,122,204, `:75-76`) ; `MGDockTabGroup` n'a aucun template. Le fond noir dominant de la zone est le fond de fenetre du theme (rgb 0,10,18, `BuiltInThemes.xaml:179`), `MGDockHost` ne peignant rien : conforme au theme. `Dark` est `BasedOn="Dark_Blue"` mais declare tous les champs de son bloc Docking (`ThemeDefinitionBuilder.ApplyDocking` fusionne champ par champ) : une re-teinte de `Dark_Blue` ne fuit pas dans `Dark` ; `Light_Gray` a son propre bloc complet.

## Decisions de l'auteur (7 septembre 2026)

- Bug 1 : nouveau comportement type IDE, les boutons epingler/fermer sont masques sur les onglets inactifs et reveles au survol ou sur l'onglet actif ; correction des positions d'icones et evaluation du survol au dessin incluses. Hypothese de travail (a confirmer a la revue) : l'espace des boutons reste reserve, pas de reflow des onglets au survol.
- Bug 2 : correctif racine en deux temps, (a) contenu de la fenetre flottante construit avec elle-meme, puis (b) occlusion du hit-test calculee depuis la fenetre qui AFFICHE l'element (premiere `MGWindow` de la chaine visuelle `Parent`) et non depuis sa fenetre de construction ; entree "Dock" ajoutee au menu contextuel des onglets flottants.
- Bug 3 : le chrome docking suit la famille du theme ; re-teinte du bloc Docking de `Dark_Blue` vers la famille bleu marine (donnees seulement). Les couleurs encore codees en dur passent par les taches 8 et 9 de `Docs/Tasks/styling-theme-tasks.md` (templates structurels docking), dont l'acceptation doit desormais inclure ce point.

## Consignes de travail pour l'agent IA

- Executer les taches dans l'ordre.
- Faire exactement 1 commit par tache terminee ; mettre a jour le statut de la tache dans ce fichier dans le meme commit. Rollback d'une tache = revert de son commit.
- Si une tache est bloquee, la marquer ⛔, decrire le blocage juste sous la tache, puis s'arreter.
- Pas de refactor hors perimetre ; ne pas toucher a `MGTabControl`/`MGTabItem` ni au moteur de drag hors des points cites.
- Tester le comportement observable des controles reels (evenements publics, `IsHovered`, `TemplateParts`, texte), pas des champs intermediaires ; prouver chaque garde par mutation (revert temporaire => test rouge). Les tests de survol passent par de vraies trames souris via `GraphTestRuntime` (pattern `MGUI.Tests/Input/NestedWindowHoverOcclusionTests.cs`, `FloatingDockWindowActivationTests.cs`) : `IsHovered` traverse `IsInside` (bornes ecran valides, occlusion), deplacer la position du tracker ne suffit pas.
- Toute decision d'architecture prise pendant l'execution est enregistree en ADR dans `Docs/decisions/` (skill `adr`).
- Validation manuelle de l'auteur dans `MGUI.Samples` (demo docking) apres chaque tache ; ne jamais lancer `MGUI.Samples` depuis un agent.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale

1. `dotnet build .\MGUI.Core\MGUI.Core.csproj`
2. `dotnet build .\MGUI.Tests\MGUI.Tests.csproj`
3. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj` (build seulement)
4. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "FullyQualifiedName~Dock|FullyQualifiedName~Input|FullyQualifiedName~Modal"`
5. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "FullyQualifiedName~Architecture"`
6. Scenarios : `SCN-DOCK-001` (drag, split, flottant, retour au dock), `SCN-OVERLAY-001` (fenetres imbriquees et input), `SCN-FOCUS-001` (transitions menu contextuel).

## Taches

### ✅ 1. Construire le contenu de la fenetre flottante avec la fenetre flottante

**Statut** : livre le 7 septembre 2026. `MGFloatingDockWindow.cs:73` construit le tab group avec `this` (ordre `OwnerDockHost`/`OwnerFloatingWindow` avant `GroupNode` preserve ; le constructeur de base garde `ownerHost.ParentWindow` comme fenetre proprietaire). Aucun autre site ne construisait du contenu flottant avec la fenetre du host. Tests : `MGUI.Tests/Docking/FloatingWindowContentInputTests.cs` (layout a deux panneaux, flottement par `DetachToFloating`, trames souris reelles) : `SelfOrParentWindow` de l'onglet flottant = fenetre flottante ; survol `IsHovered` ; clic droit ouvre `Desktop.ActiveContextMenu` ; appui + deplacement atteint `MGDockHost.CurrentDrag` avec `SourceFloatingWindow`. Mutation check : retour a `ownerHost.ParentWindow` => les quatre tests rouges, vert apres reversion. Le contenu applicatif des panneaux flottants reste sans interaction jusqu'a la tache 2.

But :
rendre la fenetre flottante conforme a la convention du framework (le contenu d'une fenetre lui appartient : `MGContextMenu` construit ses items avec `this`, le dropdown de `MGComboBox` appartient a la fenetre dropdown), pour que ses onglets retrouvent survol, clic, menu contextuel et drag.

Travail attendu :

- `MGUI.Core/UI/Docking/Controls/MGFloatingDockWindow.cs:73` : `new MGDockTabGroup(this)` au lieu de `ownerHost.ParentWindow`, en preservant l'ordre d'affectation `OwnerDockHost` / `OwnerFloatingWindow` avant `GroupNode` (commentaire lignes ~70-78) ; verifier les autres usages de `ownerHost.ParentWindow` dans le fichier et dans `MGDockHost.CreateFloatingWindow` (`:1316`) / `DetachToFloating` (`:1301`) ;
- tests (`MGUI.Tests/Docking`, trames souris reelles) : apres flottement d'un panneau, l'onglet de la fenetre flottante a `SelfOrParentWindow == fenetre flottante` ; le survol de cet onglet donne `IsHovered == true` ; un clic droit ouvre un menu contextuel (`Desktop` a un menu contextuel ouvert) ; un debut de drag atteint `MGDockHost` (`CurrentDrag` non nul avec `SourceFloatingWindow` renseigne).

Criteres d'acceptation :

- les onglets d'une fenetre flottante reagissent au survol, au clic, au clic droit et au drag ;
- le contenu applicatif d'un panneau flottant reste, a ce stade, sans interaction (documente, traite par la tache 2) ;
- suites Dock, Input, Modal et Architecture vertes ; `FloatingDockWindowActivationTests` inchange et vert.

Commit recommande : `fix(docking): build floating window content with the floating window`

### ✅ 2. Calculer l'occlusion du hit-test depuis la fenetre qui affiche l'element

**Statut** : livre le 7 septembre 2026 (ADR-0004). `MGElement.DisplayingWindow` (interne) : l'element s'il est une fenetre, sinon la premiere `MGWindow` de la chaine `Parent`, repli `ParentWindow` ; cache par element valide par un compteur de generation statique incremente par `SetParent` (`Interlocked.Increment`, lecture `Volatile.Read`). `IsInside`, `ComputeTopmostHoveredElement` et le calcul de `VisualState` lisent la fenetre d'affichage ; `MGWindow`, `MGDesktop`, `MGUI.Shared` et les controles docking inchanges ; non-objectifs conserves sur `SelfOrParentWindow`. Tests : `MGUI.Tests/Input/DisplayingWindowHitTestTests.cs` (contenu profond amorce puis re-parente dans une fenetre imbriquee : survol, appui, `PressedElement` de la fenetre imbriquee, `VisualState` Hovered puis Pressed ; non-regression `a331639` ; element detache) et `MGUI.Tests/Docking/FloatingWindowRedockTests.cs` (contenu applicatif d'un panneau flottant survole et clique avec retour visuel ; drag de l'onglet flottant vers le host : cible `Center`, redock, fenetre flottante fermee, `DisplayingWindow` du contenu revenu a la fenetre principale). Mutations nommees : invalidation de la seule instance re-parentee => assertion du bouton profond rouge, racine verte ; retour a `SelfOrParentWindow` dans `VisualState` => rouge. Suite complete 1489/1489. Docs : `Docs/input-architecture.md` (exceptions, point 1) et ADR-0004.

But :
qu'un element affiche dans une fenetre imbriquee ne soit plus occlus par cette fenetre elle-meme, quelle que soit sa fenetre de construction ; le contenu applicatif re-parente dans une fenetre flottante redevient interactif.

Etat actuel :

- `MGElement.IsInside` (`MGUI.Core/UI/MGElement.cs:1470-1481`) termine par `!(SelfOrParentWindow?.IsUnscaledPositionOccluded(UnscaledPosition) ?? false)` ; `MGWindow.IsUnscaledPositionOccluded` (`MGWindow.cs:1211-1224`) considere toute fenetre imbriquee couvrant le point, `AllowsClickThrough` par defaut faux (`:893`) ; seule exemption : la chaine du menu contextuel actif via `Origin` (`MGWindow.cs:1234-1268`) ;
- `ParentWindow` est fixe a la construction (`MGElement.cs:607`, `:2047`) ; `SetParent`/`SetContent` ne changent que le parent visuel ;
- `IsInside` s'execute par element et par evenement souris : le calcul de la fenetre d'affichage doit etre mis en cache et invalide sur `OnParentChanged`.

Travail attendu :

- introduire sur `MGElement` la notion de fenetre d'affichage : la premiere `MGWindow` rencontree en remontant la chaine `Parent` (l'element lui-meme s'il est une fenetre), avec repli sur `ParentWindow` pour un element detache ; valeur mise en cache par element et validee par un compteur de generation de topologie, incremente par tout `SetParent` (`MGElement.cs:644-655`) et porte par le desktop ou en statique, relu paresseusement : un re-parentage de RACINE (cas reel : `_activeContentContainer.SetParent(this)`, `MGDockTabGroup.cs:536`, un seul appel sur la racine du contenu) invalide implicitement tout son sous-arbre sans parcours recursif, et le cout par evenement souris reste une comparaison d'entiers hors changement de topologie ;
- `IsInside` (`MGElement.cs:1480`) interroge l'occlusion depuis la fenetre d'affichage ; conserver l'exemption du menu contextuel actif ;
- migrer aussi vers la fenetre d'affichage le chemin d'etat visuel, car c'est la fenetre qui affiche l'arbre qui renseigne `HoveredElement`/`PressedElement` (`MGWindow.cs:1492`, `:1510`) et les force a null quand elle est occluse (`:1501`, `:1664`) : `ComputeTopmostHoveredElement` (`MGElement.cs:2409`, `!SelfOrParentWindow.HasModalWindow && IsHovered`) et le calcul de `VisualState` (`MGElement.cs:2500-2508`, `IsSelfOrAncestorOf(SelfOrParentWindow.PressedElement)` / `HoveredElement`) ; sans cela un element re-parente recevrait le clic mais sans retour visuel de survol ni d'appui ;
- ne pas changer la semantique de `SelfOrParentWindow` ni de `ParentWindow` pour le reste (focus clavier, activation, theme). Non-objectifs explicites, a lister dans le rapport, les autres consommateurs du chemin souris releves le 7 septembre 2026 : ceux qui lisent `HasModalWindow` ou `Scale` (identiques entre fenetre principale et flottante) ou `HoveredElement` hors docking : `MGElement.cs:2544` (tooltip), `:2566` (`_CanReceiveMouseInput`), `MGDesktop.cs:264`, `MGResizeGrip.cs:211`, `MGGraphControls.cs:1733`, `:1828`, `:2403` ; et `MGElement.cs:2627` (`GetActiveMouseDragCaptureOwner`), qui lit la chaine `PressedElement` de la fenetre de construction (`MGWindow.cs:1282-1289`) : sans danger, car un appui dans la fenetre flottante met `PressedElement` de la fenetre principale a null (`MGWindow.cs:1510`, `:1664`), donc aucune suppression de `MouseHandler` ne frappe le contenu re-parente ; le seul cas suppressif (drag en cours possede par la fenetre principale) est le comportement actuel. Asymetrie a consigner dans l'ADR-0004 : `VisualState` lira `HasModalWindow` de la fenetre d'affichage (`MGElement.cs:2502`) alors que `_CanReceiveMouseInput` (`:2566`) garde celui de la fenetre de construction ; resultat observable correct (avec un modal sur la fenetre principale, `_CanReceiveMouseInput == false` empeche la selection par `ComputeTopmostHoveredElement`, donc `SecondaryVisualState` reste `None`, `MGElement.cs:1739`) ;
- alternative a ecarter explicitement dans l'ADR : etendre `IsUnscaledPositionOccluded(position, origine)` pour ignorer les fenetres imbriquees ancetres visuels de l'origine (meme resultat, mais logique dispersee cote fenetre) ;
- ADR-0004 (`Docs/decisions/`) : origine de l'occlusion = fenetre d'affichage ; mise a jour de la section hit-test / fenetres imbriquees de `Docs/input-architecture.md` et de `Docs/input-window-activation-design.md` si elle decrit l'origine ;
- tests (trames souris reelles) : (i) en trois etapes : (1) un conteneur (`MGStackPanel`) cree avec la fenetre principale, contenant un `MGButton` enfant profond, est d'abord affiche et hit-teste dans la fenetre principale (au moins une trame souris `GraphTestRuntime` sur le bouton profond, `IsHovered == true`), ce qui amorce le cache de fenetre d'affichage comme dans le cas reel (panneau affiche et survole avant flottement) ; (2) apres re-parentage de la racine dans une fenetre imbriquee, le bouton profond ET le conteneur racine recoivent survol et clic ; (3) mutation nommee : remplacer l'invalidation par generation par une invalidation de la seule instance re-parentee (equivalent d'une invalidation dans `OnParentChanged`), ce qui doit rendre rouge l'assertion sur le bouton PROFOND et laisser verte l'assertion sur la racine ; mutation retiree, les deux passent ; (ii) le meme contenu, affiche dans la fenetre principale et couvert par une autre fenetre imbriquee, n'est pas survole (non-regression de `a331639`) ; (iii) `VisualState.SecondaryVisualState` du bouton re-parente passe a `Hovered` puis `Pressed` ; mutation : revenir a `SelfOrParentWindow` dans le calcul de `VisualState` => rouge ; (iv) suites `NestedWindowHoverOcclusionTests`, `CrossWindowHoverPressedOcclusionTests` (regression directe de la decision d'occlusion du 2 septembre), `ContextMenuHoverOcclusionTests`, `Modal/ContextMenuClickThroughTests`, `OverlappingWindowsInputRoutingTests`, `WindowActivationOnClickTests`, `FloatingDockWindowActivationTests` inchangees et vertes ; (v) docking bout en bout : le contenu applicatif d'un panneau flottant (conteneur + bouton crees avec la fenetre principale, comme `DockingDemo.cs:180-199`) recoit clic et retour visuel ; un drag d'onglet depuis la fenetre flottante vers le host affiche les indicateurs et `ExecuteDrop` redocke le panneau (fenetre flottante fermee quand vide).

Criteres d'acceptation :

- tout element affiche dans une fenetre flottante est interactif, y compris le contenu applicatif profond, avec retour visuel de survol et d'appui ;
- aucun changement de comportement pour les elements affiches dans leur fenetre de construction (suites d'occlusion existantes vertes) ;
- ADR-0004 enregistree, docs input a jour.

Contrainte de conception (non testee) : hors changement de topologie, `IsInside` ne fait qu'une comparaison de generation et une lecture de cache, jamais une remontee de chaine par evenement souris.

Risque : eleve (hit-test framework-wide) ; revue de plan et verification independantes obligatoires. Rollback : revert du commit de la tache (ADR-0004 et docs compris).

Commit recommande : `fix(input): resolve hit-test occlusion from the displaying window`

### ⚪ 3. Onglets docking : positions d'icones, survol au dessin, boutons reveles au survol ou si actif

But :
corriger le decalage d'un passage des icones, faire suivre le survol au dessin, et adopter le comportement type IDE decide par l'auteur.

Travail attendu :

- `MGDockTabItem.cs` : `GetCloseIconBounds` / `GetPinIconBounds` calcules depuis les `LayoutBounds` de l'onglet lui-meme avec l'arithmetique deja utilisee par `UpdateContentLayout` (`:624-654` : `Bounds.Right - largeur des boutons`, `CloseButtonSize`/`PinButtonSize`), sans lire les bounds des boutons freres ; meme correction dans `MGDockTabGroup.cs` pour l'icone deroulante (`:324`, lit `_dropdownBtn?.LayoutBounds`) et l'icone d'etat de fenetre (`GetWindowStateIconBounds`, `:235`, lit `_maximizeBtn.LayoutBounds`) ;
- survol evalue au dessin (ou a chaque tick) et non seulement dans le passage de layout : fond et accent de l'onglet (`:480-498`) suivent la souris sans clic prealable ; s'inspirer de `MGSlider.DrawSelf` (`MGSlider.cs:1041`) ; ne pas invalider le layout au survol ;
- revelation : icones fermer/epingler (et leurs boutons) visibles si `(IsActive || IsHovered)` et `CanClose`/`CanAutoHide` respectivement, masquees sinon ; l'espace reste reserve (hypothese de travail, pas de reflow au survol) ; les boutons masques ne recoivent pas de clic ;
- tests (`MGUI.Tests/Docking`) : (i) dans un meme passage de layout, les bounds des icones sont ceux des boutons (mutation : revenir aux bounds freres => rouge) ; (ii) onglet inactif non survole : icones masquees ; onglet actif : visibles ; onglet inactif survole (trames souris reelles) : visibles ; (iii) largeur d'onglet identique masque/revele ; (iv) `MGDockTabGroup` : bounds de l'icone deroulante et de l'icone d'etat corrects des le premier passage.

Criteres d'acceptation :

- sur un groupe jamais clique, survoler un onglet revele ses boutons a la bonne position ; l'onglet actif les montre toujours ;
- fond et accent de survol suivent la souris sans clic ;
- aucune regression des tests docking existants ; `DockPartVocabularyTests` vert (les parts restent enregistrees, seule leur visibilite change).

Commit recommande : `fix(docking): reveal tab buttons on hover and fix icon bounds`

### ⚪ 4. Entree "Dock" dans le menu contextuel d'un onglet flottant

But :
offrir un retour au dock sans drag, prevu par `TODO-DockingManager.md` (item 10.4) et jamais implemente.

Travail attendu :

- `MGDockTabItem.BuildContextMenu` (`:415-460`) : entree "Dock" quand `OwnerFloatingWindow != null`, evenement `DockRequested` remonte par `MGDockTabGroup` (pattern `PanelFloatRequested`, `MGDockTabGroup.cs:433`, `MGDockHost.cs:2009`) ;
- le modele ne memorise aucune position dockee precedente (verifie le 7 septembre 2026 : aucun `LastDock*`/`PreviousDock*` dans `MGUI.Core/UI/Docking`). `MGDockHost` memorise, au moment du flottement (`DetachToFloating` `:1301`, `CreateFloatingWindow` `:1316`), l'identifiant du groupe source du panneau, en memoire seulement (aucun champ ajoute au layout sauvegarde, aucun changement de format) ; "Dock" redocke dans ce groupe s'il existe encore, sinon au centre du premier groupe visible, en reutilisant la branche `SourceFloatingWindow` de `ExecuteDrop` (`MGDockHost.cs:920`) sans la dupliquer ; fermer la fenetre flottante devenue vide ;
- tests : le menu d'un onglet docke n'a pas "Dock" ; celui d'un onglet flottant l'a ; l'invoquer remet le panneau dans le layout du host et ferme la fenetre flottante vide ; le layout sauvegarde/recharge reste coherent (`SCN-DOCK-001`).

Criteres d'acceptation :

- un panneau flottant se redocke par le menu, au bon endroit ;
- aucune duplication de la logique de drop.

Commit recommande : `feat(docking): add a Dock entry to the floating tab context menu`

### ⚪ 5. Theme Dark_Blue : palette docking dans la famille bleu marine

But :
faire suivre au chrome docking la famille de couleurs de son theme, comme le menu contextuel et la liste deroulante.

Travail attendu :

- `MGUI.Core/UI/Themes/BuiltInThemes.xaml`, bloc `Docking` de `Dark_Blue` (`:434-479`) : fonds (tabs, drawer, strip, splitter) re-teintes vers la famille bleu marine du theme (ContextMenu rgb 11,28,72 ; Button/ComboBox rgb 0,108,214), accents existants (`TabActiveAccentColor`, `SplitterHoverBrush`, indicateurs de drop) conserves ; `Dark` et `Light_Gray` intacts ;
- ne pas toucher aux couleurs codees en dur de `MGDockTabGroup` et `MGDockPreviewOverlay` : ajouter a l'acceptation des taches 8 et 9 de `Docs/Tasks/styling-theme-tasks.md` que ces valeurs passent par le theme ;
- tests : (i) apres `ApplyDockTabItemTemplate`, `TabItem.NormalBrush` vaut `theme.Docking.TabNormalBackground` (le theme gagne sur le constructeur) ; (ii) un `MGContextMenu` cree comme dans `BuildContextMenu` resout `GetTheme()` sur la meme instance que le host (asserter `GetTheme()`, pas `Window.Theme`) ; (iii) les valeurs du bloc Docking de `Dark` sont inchangees apres l'edition de `Dark_Blue` (pas de fuite par `BasedOn`).

Criteres d'acceptation :

- dans la demo docking, tabs, drawer, strip et splitters lisent dans la meme famille que le menu contextuel et la liste deroulante ;
- `Dark` et `Light_Gray` visuellement inchanges ;
- `SCN-THEME-001` vert.

Commit recommande : `fix(theme): tint the Dark_Blue docking palette to the theme family`

## Points ouverts

- Tache 3 : l'espace des boutons reveles reste-t-il reserve (pas de reflow au survol) ? Hypothese retenue : oui.
- Tache 4 : le groupe source memorise au flottement n'est pas persiste ; apres un save/load de layout avec une fenetre flottante ouverte, "Dock" retombe au centre du premier groupe visible. Acceptable pour l'auteur ? Hypothese retenue : oui.
