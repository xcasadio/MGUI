# Analyse Complète du Système de Docking — Tâches d'Audit IA

> Ce fichier définit les tâches qu'un agent IA doit exécuter pour auditer l'intégralité
> du code de la fonctionnalité docking. L'objectif est de vérifier que tout est
> architecturé intelligemment, proprement, sans workaround, et correctement testé.

---

## Inventaire des fichiers (27 fichiers source, ~9 600 lignes)

### Couche Model (DockLayout/)
| Fichier | Lignes | Rôle |
|---------|--------|------|
| `DockNode.cs` | 145 | Classe abstraite : Id, Parent, GetChildren, RemoveChild |
| `DockSplitNode.cs` | 283 | Nœud split : Orientation, SplitRatio, FirstChild/SecondChild |
| `DockTabGroupNode.cs` | 368 | Groupe d'onglets : Panels (ObservableCollection), ActivePanelId, IsDocumentArea |
| `DockPanelNode.cs` | 359 | Panel : Title, Icon, ContentFactory, AllowedZones, Family, IsPinned, auto-hide snapshot |
| `DockLayoutModel.cs` | 375 | Racine du modèle : RootNode, auto-hide stores (4 sides), LayoutChanged, tree validation |
| `DockOperation.cs` | 680 | Opérations pures : SplitDock, DockAsTab, ReorderTab, RemovePanel, SplitDockAtRoot |
| `DockLayoutSerializer.cs` | 444 | Sérialisation JSON : ToJson / FromJson round-trip, DTOs |
| `DockableDefinition.cs` | 126 | Définition d'un dockable : Id, Title, Factory, defaults |
| `DockableRegistry.cs` | 199 | Registre de définitions : NotifyShown/Hidden/Activated, SyncVisibility |
| `DockableType.cs` | 17 | Enum : Document, Tool |
| `AutoHideSide.cs` | 13 | Enum : Left, Top, Right, Bottom |

### Couche Contrôles (Controls/)
| Fichier | Lignes | Rôle |
|---------|--------|------|
| `MGDockHost.cs` | 2312 | Contrôle principal : visual tree build, drag & drop, Ctrl+Tab, auto-hide, floating, maximize |
| `MGDockTabGroup.cs` | 845 | Groupe d'onglets UI : tab headers, overflow/scroll, context menu, drop downs |
| `MGDockTabItem.cs` | 629 | En-tête d'onglet UI : titre, icône, bouton fermer, drag source |
| `MGDockSplitContainer.cs` | 457 | Container split UI : layout first/second child, ratio visuelle |
| `MGDockSplitterBar.cs` | 363 | Barre de split draggable : mouse events, ratio commit |
| `MGDockAutoHideStrip.cs` | 264 | Strip d'onglets auto-hide (une par côté) |
| `MGDockAutoHideDrawer.cs` | 344 | Tiroir auto-hide : slide-in panel, bouton pin/close |
| `MGDockDropIndicators.cs` | 565 | Indicateurs visuels de drop (losanges centraux + bords) |
| `MGDockPreviewOverlay.cs` | 191 | Overlay semi-transparent de preview du drop |
| `MGFloatingDockWindow.cs` | 214 | Fenêtre flottante détachée |
| `MGDockHostExtensions.cs` | 54 | Extensions utilitaires |

### Infrastructure drag & drop
| Fichier | Lignes | Rôle |
|---------|--------|------|
| `DockDragData.cs` | 70 | Données d'un drag en cours |
| `DockDropTarget.cs` | 84 | Cible de drop : zone, bounds, target group |
| `DockDropCalculator.cs` | 465 | Calcul des zones de drop (edge, proximity, center) |
| `DockZone.cs` | 25 | Enum : Left, Right, Top, Bottom, Center, None |

### Exemple
| Fichier | Lignes | Rôle |
|---------|--------|------|
| `EXAMPLE_SaveLoadLayout.cs` | 207 | Exemple d'utilisation save/load |

### Tests existants (5 fichiers, 1 566 lignes, 91 tests docking)
| Fichier | Tests | Couverture |
|---------|-------|------------|
| `DockLayoutModelTests.cs` | 22 | Auto-hide store, GetAllTabGroups, JSON round-trip |
| `DockOperationTests.cs` | 22 | DockAsTab, ReorderTab, SplitDock, RemovePanel, SplitDockAtRoot |
| `DockTabGroupModelTests.cs` | 18 | AddPanel, SetActivePanel, IsEmpty, RemovePanel, ReorderPanel |
| `DockRulesModelTests.cs` | 16 | AllowedZones, Family, DockableType, IsDocumentArea |
| `DockAutoHideRepinTests.cs` | 13 | Auto-hide store ops, SimulateUnpin/Repin snapshots |

---

## Phase 1 — Architecture & Design Review

### 1.1 Séparation Model / View
- [ ] Vérifier que AUCUN code dans `DockLayout/` ne référence `Microsoft.Xna.Framework` (sauf `Orientation`) ni aucun contrôle UI (`MG*`).
- [ ] Vérifier que les contrôles dans `Controls/` ne modifient JAMAIS les nœuds du modèle directement sauf via `DockOperation` ou les setters publics documentés.
- [ ] Vérifier que `DockOperation` est la seule API de mutation structurelle (split, dock, remove).
- [ ] Analyser si `DockLayoutModel` devrait être un médiateur plutôt qu'un simple conteneur — reviewer le pattern `LayoutChanged` / `PropertyChanged` et les cascades d'événements.

### 1.2 Responsabilité de `MGDockHost` (2 312 lignes — GOD CLASS ?)
- [ ] Lister toutes les responsabilités distinctes de `MGDockHost` :
  - Visual tree build & rebuild
  - Drag & drop orchestration
  - Drop zone calculation
  - Keyboard shortcuts (Ctrl+Tab)
  - Auto-hide management (strips, drawer, pin/unpin)
  - Floating window management
  - Maximize/restore
  - Panel registry
  - Layout save/load
  - Node subscription management
  - Active panel tracking
- [ ] Proposer un refactoring en sous-composants (ex: `DockDragManager`, `DockAutoHideManager`, `DockKeyboardManager`) avec pour chacun l'interface publique minimale.
- [ ] Vérifier que chaque méthode publique de `MGDockHost` a une raison d'être publique (API surface review).

### 1.3 Gestion des événements & souscriptions
- [ ] Auditer la chaîne `PropertyChanged` → `LayoutChanged` → `RebuildVisualTree` :
  - `DockNode.PropertyChanged` → `DockLayoutModel.OnNodePropertyChanged` → `LayoutChanged`
  - `LayoutChanged` → `MGDockHost.OnLayoutModelChanged` → `SyncNodeSubscriptions` + `RebuildVisualTree`
  - `DockTabGroupNode.PropertyChanged` → `MGDockHost.OnTabGroupPropertyChanged` → `ActiveDockable` update
  - `DockTabGroupNode.PropertyChanged` → `MGDockHost.OnNodePropertyChanged` → guard for ActivePanelId
- [ ] Vérifier qu'il n'y a AUCUN risque de boucle infinie (PropertyChanged → LayoutChanged → RebuildVisualTree → PropertyChanged).
- [ ] Vérifier que les guards `ActivePanelId` dans `DockLayoutModel.OnNodePropertyChanged` ET `MGDockHost.OnNodePropertyChanged` sont cohérents et couvrent tous les cas.
- [ ] Vérifier que `SyncNodeSubscriptions` ne cause pas de double-subscription (subscribe déjà abonné) ou de missed-subscription (nouveau nœud pas abonné).
- [ ] Analyser si `RebuildVisualTree` est appelé trop souvent (chaque PropertyChanged non-ActivePanel déclenche un rebuild complet) et proposer un mécanisme de dirty-flag / batching.

### 1.4 Cohérence du panel registry
- [ ] Vérifier que `_panelRegistry` est TOUJOURS synchronisé avec le modèle :
  - Quand un panel est ajouté au modèle, est-il systématiquement `RegisterPanel`-é ?
  - Quand un panel est supprimé du modèle (RemovePanel, close), est-il systématiquement retiré de `_panelRegistry` ?
  - Quand un panel est auto-hidden, reste-t-il dans `_panelRegistry` ?
  - Quand un panel est floaté, reste-t-il dans `_panelRegistry` ?
- [ ] Vérifier que le `DockingDemo` qui ne call jamais `RegisterPanel` fonctionne correctement — déterminer si `_panelRegistry` est réellement utile ou s'il fait doublon avec `GetAllTabGroups().SelectMany(g => g.Panels)`.

---

## Phase 2 — Code Quality & Workarounds

### 2.1 Détection de workarounds
- [ ] Chercher tous les commentaires `TODO`, `HACK`, `FIXME`, `workaround`, `temporary`, `ugly` dans les 27 fichiers.
- [ ] Chercher les `try/catch` silencieux (catch vide, catch qui log mais ne traite pas).
- [ ] Chercher les casts `as` suivis de `?.` sans `else`/fallback — symptôme de duck typing au lieu de polymorphisme.
- [ ] Chercher les flags booléens `_isDoingSomething` / `_skipEvent` — symptôme de re-entrancy workaround.
- [ ] Lister tout code dupliqué entre `MGDockHost`, `MGDockTabGroup`, `DockDropCalculator`.

### 2.2 Gestion de la mémoire & fuites potentielles
- [ ] Vérifier que tous les `+= OnXxx` ont un `−= OnXxx` correspondant, en particulier :
  - `DockLayoutModel.LayoutChanged`
  - `DockTabGroupNode.PropertyChanged`
  - `DockNode.PropertyChanged`
  - `Panels.CollectionChanged`
  - `PanelCloseRequested`, `PanelFloatRequested`, `PanelPinToggleRequested`
  - `MaximizeRequested`, `RestoreRequested`
- [ ] Vérifier que les `MGDockTabGroup` créés dans `BuildTabGroup` sont correctement nettoyés lors de `RebuildVisualTree` (pas de fuite d'event handlers sur le modèle).
- [ ] Vérifier que `MGFloatingDockWindow` se désabonne correctement quand elle est redockée.
- [ ] Vérifier que `_autoHideDrawer` et `_autoHideStrips` ne gardent pas de références obsolètes.

### 2.3 Thread safety et re-entrancy
- [ ] `RebuildVisualTree` est-il safe quand appelé depuis un event handler (re-entrancy) ?
- [ ] `SyncNodeSubscriptions` peut-il être appelé pendant une itération de `PropertyChanged` qui est en cours de dispatch ?
- [ ] `CyclePanel` → `SetActivePanel` → éventuel `LayoutChanged` → `RebuildVisualTree` — est-ce safe pendant `UpdateSelf` ?

### 2.4 Qualité des API publiques
- [ ] Vérifier que toutes les méthodes publiques ont des XML doc.
- [ ] Vérifier que les paramètres `null` sont gérés (ArgumentNullException ou no-op documenté, pas de NullReferenceException).
- [ ] Vérifier la cohérence des conventions de nommage (PascalCase properties, _camelCase fields).

---

## Phase 3 — Revue Fichier par Fichier

### 3.1 `DockNode.cs` (145 lignes)
- [ ] Vérifier que `Parent` est toujours mis à jour correctement quand un nœud change de parent.
- [ ] Vérifier que `GetChildren()` / `RemoveChild()` sont cohérents entre `DockSplitNode` et `DockTabGroupNode`.

### 3.2 `DockSplitNode.cs` (283 lignes)
- [ ] Vérifier que `SplitRatio` est toujours clampé entre 0 et 1.
- [ ] Vérifier que `FirstChild` / `SecondChild` setter met à jour `child.Parent`.
- [ ] Vérifier que la suppression d'un enfant collapse le split (remontée de l'autre enfant).

### 3.3 `DockTabGroupNode.cs` (368 lignes)
- [ ] Vérifier que `OnPanelsCollectionChanged` gère correctement tous les `NotifyCollectionChangedAction` (Add, Remove, Replace, Reset, Move).
- [ ] Vérifier que `SetActivePanel` avec un ID inexistant ne crashe pas.
- [ ] Vérifier que `IsEmpty` est cohérent avec `Panels.Count == 0`.
- [ ] Vérifier la propriété `ScrollIndex` : est-elle clampée correctement après ajout/suppression de tabs ?

### 3.4 `DockPanelNode.cs` (359 lignes)
- [ ] Vérifier que les champs internes `AutoHideReturnGroup`, `AutoHideReturnZone`, `AutoHideReturnSplitRatio` sont toujours clean (pas de référence stale vers un groupe supprimé).
- [ ] Vérifier que `AllowedZones`, `Family`, `DockableType` sont utilisés correctement dans `CanDockTo`/`GetForbiddenZones`.
- [ ] Vérifier que `ContentFactory` est appelé une seule fois (lazy) et que le contenu est réutilisé.

### 3.5 `DockLayoutModel.cs` (375 lignes)
- [ ] Vérifier que `SubscribeToNodeTree` / `UnsubscribeFromNodeTree` sont toujours symmétriques.
- [ ] Vérifier que les auto-hide stores (`_autoHidePanels`) ne contiennent jamais un panel qui est aussi dans l'arbre layout.
- [ ] Vérifier que `Clear()` nettoie correctement tout (root, auto-hide, subscriptions).
- [ ] Vérifier que `ValidateTree()` détecte réellement tous les cas d'incohérence.

### 3.6 `DockOperation.cs` (680 lignes)
- [ ] Vérifier que chaque opération laisse l'arbre dans un état valide (pas de split avec un seul enfant, pas de groupe vide non-root).
- [ ] Vérifier que `CleanupEmptyTabGroup` collapse correctement les splits imbriqués.
- [ ] Vérifier que `SplitDock` / `SplitDockAtRoot` gèrent correctement le cas `DockZone.Center` (délègue à `DockAsTab`).
- [ ] Vérifier les edge cases : split sur un panel qui est l'unique enfant de la racine, split récursif, etc.

### 3.7 `DockLayoutSerializer.cs` (444 lignes)
- [ ] Vérifier le round-trip pour tous les types de nœuds (split, tab group, panel).
- [ ] Vérifier que les propriétés de règles (`AllowedZones`, `Family`, `DockableType`) sont préservées.
- [ ] Vérifier que les auto-hide panels sont sérialisés/désérialisés avec leur side.
- [ ] Vérifier la rétrocompatibilité : que se passe-t-il si le JSON contient des champs inconnus ou manquants ?
- [ ] Vérifier que `CleanupInvalidNodes` (post-désérialisation) ne supprime pas silencieusement des données valides.

### 3.8 `MGDockHost.cs` (2 312 lignes) — FICHIER CRITIQUE
- [ ] Auditer `RebuildVisualTree` : est-il idempotent ? Que se passe-t-il si appelé 2x de suite ?
- [ ] Auditer `BuildTabGroup` : les event handlers (PanelCloseRequested, PanelFloatRequested, etc.) sont-ils décrochés quand le visuel est détruit ?
- [ ] Auditer la gestion du drag & drop : `BeginDrag`, `UpdateDrag`, `PerformDrop`, `CancelDrag` — vérifier la state machine complète.
- [ ] Auditer `DetachToFloating` / `RedockFromFloating` : le panel registry reste-t-il cohérent ?
- [ ] Auditer `UnpinPanel` / `RepinPanel` : le snapshot/restore est-il fiable quand le layout a changé entre unpin et repin ?
- [ ] Auditer `MaximizeGroup` / `RestoreLayout` : le stack de maximize est-il robuste (double-maximize, maximize d'un groupe supprimé) ?
- [ ] Auditer `SaveLayoutToJson` / `LoadLayoutFromJson` : les panels fermés/auto-hidden/floating sont-ils correctement gérés ?

### 3.9 `MGDockTabGroup.cs` (845 lignes)
- [ ] Vérifier le calcul d'overflow des tabs : `LastMeasuredWidth`, `ScrollIndex`, boutons de scroll.
- [ ] Vérifier que le scroll suit le tab actif (active tab toujours visible).
- [ ] Vérifier que le context menu (clic droit sur un tab) propose toutes les bonnes actions et les exécute correctement.
- [ ] Vérifier le dropdown (liste de tous les tabs quand overflow) : fonctionne-t-il correctement ?

### 3.10 `MGDockTabItem.cs` (629 lignes)
- [ ] Vérifier que le drag initiation respecte le seuil de distance.
- [ ] Vérifier que le bouton close est masqué quand `CanClose = false`.
- [ ] Vérifier les styles visuels (actif, hover, pressed).

### 3.11 `MGDockSplitContainer.cs` + `MGDockSplitterBar.cs` (820 lignes)
- [ ] Vérifier que le ratio est committé au modèle (et pas seulement visuel).
- [ ] Vérifier que `MinFirstSize` / `MinSecondSize` sont respectés pendant le resize.
- [ ] Vérifier que le resize en mode maximize fonctionne correctement (ou est désactivé).

### 3.12 `MGDockAutoHideStrip.cs` + `MGDockAutoHideDrawer.cs` (608 lignes)
- [ ] Vérifier que le strip affiche les bons panels pour chaque côté.
- [ ] Vérifier que le drawer se ferme correctement (clic outside, Escape, pin).
- [ ] Vérifier que le drawer resize fonctionne et que la taille est mémorisée.

### 3.13 `MGDockDropIndicators.cs` + `DockDropCalculator.cs` (1 030 lignes)
- [ ] Vérifier que les indicateurs visuels correspondent aux zones de drop effectives.
- [ ] Vérifier le calcul de proximity docking (tâche 13) : les seuils sont-ils raisonnables ?
- [ ] Vérifier que les zones interdites (`GetForbiddenZones`) masquent correctement les indicateurs.

### 3.14 `MGFloatingDockWindow.cs` (214 lignes)
- [ ] Vérifier que la fenêtre flottante peut être redockée (re-drop dans le host).
- [ ] Vérifier que fermer la fenêtre flottante ne fait pas fuiter le panel.
- [ ] Vérifier que le resize de la fenêtre flottante fonctionne.

### 3.15 `DockableRegistry.cs` + `DockableDefinition.cs` (325 lignes)
- [ ] Vérifier que `SyncVisibility` est appelé au bon moment (après chaque rebuild).
- [ ] Vérifier que `TryGetById` fonctionne après register/unregister.
- [ ] Vérifier qu'un dockable non-visible peut être recréé via `ShowDockable`.

---

## Phase 4 — Couverture de Tests

### 4.1 Tests model existants — évaluer la couverture
- [ ] Exécuter les tests en mode coverage (`dotnet test --collect:"XPlat Code Coverage"`) et reporter les pourcentages pour chaque fichier du dossier `DockLayout/`.
- [ ] Identifier les méthodes/branches NON couvertes.
- [ ] Vérifier que les tests existants testent les edge cases (null, vide, un seul élément, max éléments).

### 4.2 Tests manquants — model layer
- [ ] `DockSplitNode` : tests de `SplitRatio` clamping, `FirstChild`/`SecondChild` parent update, `GetChildren`, `RemoveChild`.
- [ ] `DockNode` : tests de `Parent` management, `FindNodeById`.
- [ ] `DockLayoutModel` : tests de `ValidateTree`, `Clear`, `SubscribeToNodeTree` / `UnsubscribeFromNodeTree` symmetry.
- [ ] `DockableRegistry` : tests de `Register`, `Unregister`, `TryGetById`, `SyncVisibility`, `NotifyShown/Hidden/Activated`.
- [ ] `DockableDefinition` : tests de `CreatePanelNode`.

### 4.3 Tests manquants — intégration (nécessitent MonoGame mock ou headless)
- [ ] `MGDockHost.CyclePanel` : vérifier que Ctrl+Tab visite tous les panels (docked + auto-hidden + floating) dans le bon ordre.
- [ ] `MGDockHost.RebuildVisualTree` : vérifier l'idempotence, vérifier que les event handlers ne fuient pas.
- [ ] `MGDockHost.DetachToFloating` / `RedockFromFloating` : vérifier la cohérence du registry.
- [ ] `MGDockHost.UnpinPanel` / `RepinPanel` : vérifier snapshot/restore dans tous les cas.
- [ ] Drag & drop end-to-end : drag un tab → drop dans une zone → vérifier l'arbre modèle.

### 4.4 Tests de non-régression
- [ ] Écrire un test pour le bug Ctrl+Tab corrigé : `DockLayoutModel.OnNodePropertyChanged` ne doit PAS fire `LayoutChanged` pour `ActivePanelId`.
- [ ] Écrire un test pour le bug de souscription : `SyncNodeSubscriptions` doit correctement re-subscribe après un `RootNode` change.
- [ ] Écrire un test qui vérifie que `CyclePanel` visite les panels de TOUS les tab groups dans un layout imbriqué (leftGroup + centerGroup + bottomGroup).

---

## Phase 5 — Performance & Scalabilité ✅

### 5.1 `RebuildVisualTree` — coût et fréquence

**Coût par rebuild :** O(n) en nombre de nœuds dans l'arbre — un parcours récursif de `BuildVisualTree`
crée un contrôle UI par nœud. Pour 10 panels c'est trivial; pour 100 panels (scénario extrême)
le coût est linéaire côté modèle, mais l'impact principal est le layout pass de MonoGame qui
suit immédiatement.

**Fréquence — risque réel :** `LayoutChanged` est déclenché par tout `PropertyChanged` d'un nœud
(SAUF `ActivePanelId` — guard présent et vérifié). Cela signifie qu'un rename de panel, un
changement de `IsPinned`, un changement de `SplitRatio` — chaque propriété déclenche un rebuild
complet. Si l'on change 5 propriétés en rafale (ex: lors d'une désérialisation), on obtient
5 rebuilds au lieu d'1.

**Recommandation :** Implémenter un dirty-flag avec `BeginUpdate()`/`EndUpdate()` (compteur, pas
booléen) sur `DockLayoutModel`. Pendant `BeginUpdate`, mettre en file au plus 1 rebuild pending.
`EndUpdate` déclenche le rebuild une seule fois. Cela bénéficierait au load de layouts complets.

### 5.2 `GetAllTabGroups()` — allocations

`GetAllTabGroups()` retourne un `IEnumerable<DockTabGroupNode>` via `yield return` — pas
d'allocation List sauf quand l'appelant matérialise. Analyse des call sites :

| Call site | Impact |
|-----------|--------|
| `MGDockHost.L1165` — `.ToList()` (dans `CyclePanel`) | Allocation list, appelé une fois par Ctrl+Tab → négligeable |
| `MGDockHost.L1074` — `.Contains()` (dans `ActivatePanel`) | Traverse le générateur jusqu'au match — O(n) acceptable |
| `MGDockHost.L1030` — `.SelectMany().AddRange()` (dans `CyclePanel`) | Une matérialisation par Ctrl+Tab — acceptable |
| `DockLayoutModel.ToString()` — `.Count()` × 2 | Double traverse dans `ToString()` — seulement appelé par debugger |
| `DockLayoutModel.ValidateTree.L383` — `.Count()` | Appelé ponctuellement, pas dans hot path |
| `RebuildVisualTree.L1684` — `.FirstOrDefault()` en mode maximize | Appelé à chaque rebuild en mode maximize — O(n) acceptable |

**Conclusion :** Pas de problème d'allocation critique. Aucun `GetAllTabGroups()` n'est appelé
dans un hot path per-frame.

### 5.3 `FindNodeById` — complexité

Implémentation : DFS récursif dans `DockNode.FindNodeById`. Retours anticipés (early exit dès
trouvé). Complexité : O(n) dans le pire cas (nœud absent ou feuille droite), O(1) amortissable
si le nœud est proche de la racine.

**Pas d'O(n²) :** Chaque appel fait au plus n comparaisons pour un arbre de n nœuds. Il n'y a
pas de boucle qui rappelle `FindNodeById` à chaque itération.

**Recommandation faible :** Pour des layouts très larges (100+ groupes), un `Dictionary<string,
DockNode>` maintenu à jour dans `DockLayoutModel` réduirait `FindNodeById` à O(1). Actuellement
non nécessaire pour les usages typiques (< 20 groupes).

### 5.4 Drop indicators — recalcul per-frame

`UpdateDragPreview` est appelé à chaque `MouseMoved` pendant un drag. Optimisations présentes :

1. **Distance threshold** (seuil 5px) : si la souris n'a pas bougé de plus de 5 pixels depuis
   le dernier calcul, la fonction retourne immédiatement. Cela évite les recalculs inutiles à
   60 fps quand la souris est "quasi-statique".

2. **Lazy indicator positioning** : `_dropIndicators.Show()` recalcule les positions des
   indicateurs (`CalculateIndicatorPositions`) seulement lors d'un changement de `_targetBounds`,
   pas à chaque appel de `UpdateActiveZone`.

3. **`DockDropCalculator.CalculateHostEdgeZones`** : recalculé à chaque passage dans la branche
   host-edge après le seuil de 5px. Ce calcul retourne un petit tableau de 4 `DockDropTarget`
   (Left/Right/Top/Bottom) — coût O(1), allocation d'un tableau fixe. Acceptable mais pourrait
   être mis en cache sur `_lastPreviewCalculation`.

**Recommandation :** Mettre en cache `CalculateHostEdgeZones(LayoutBounds)` dans un champ
`_cachedHostEdgeTargets`, invalidé seulement quand `LayoutBounds` change. Gain marginal en
allocations.

### 5.5 Bilan Performance

| Problème | Sévérité | Recommandation |
|----------|----------|----------------|
| N rebuilds pour N property changes en rafale | Moyenne | `BeginUpdate`/`EndUpdate` dirty-flag |
| `CalculateHostEdgeZones` alloue à chaque preview | Faible | Cache `_cachedHostEdgeTargets` |
| `FindNodeById` O(n) | Faible | Dictionary optionnel pour 100+ nœuds |
| `GetAllTabGroups()` allocations | Inexistant | Rien à faire |

---

## Phase 6 — Recommandations Architecturales ✅

### 6.1 Bilan des forces

| Force | Détail |
|-------|--------|
| **Séparation model/view nette** | `DockLayout/` ne référence aucun type MonoGame/UI (hors `Orientation`). `DockOperation` est la seule API de mutation structurelle. |
| **Model testable sans MonoGame** | 185 tests unitaires purs sur le model layer (DockOperation, DockTabGroupNode, DockLayoutModel, DockableRegistry, DockNodeModel, etc.) sans dépendance XNA. |
| **Event-driven proprement** | `PropertyChanged → LayoutChanged → RebuildVisualTree` est une chaîne unidirectionnelle sans boucles. Les guards `ActivePanelId` sont en place des deux côtés. |
| **Sérialization round-trip** | Après les fixes Phase 1, `DockLayoutSerializer` préserve toutes les propriétés métier (`Family`, `CanAutoHide`, `DrawerSize`, `AllowedZones`). |
| **Drag & drop optimisé** | Seuil 5px dans `UpdateDragPreview`, `Show/Hide` lazy sur les indicateurs. |
| **Subscription lifecycle fixé** | `Detach()` + `_activeTabGroupVisuals` + `Clear()` auto-hide évitent les fuites d'event handlers lors des rebuilds. |

### 6.2 Faiblesses et dette technique

#### 6.2.1 God Class `MGDockHost` (2 338 lignes, 11+ responsabilités)

`MGDockHost` a les responsabilités suivantes, toutes entremêlées :

1. Construction et rebuild du visual tree
2. Orchestration du drag & drop (BeginDrag, UpdateDrag, PerformDrop, CancelDrag)
3. Calcul des zones de drop (appels à `DockDropCalculator`)
4. Raccourcis clavier (Ctrl+Tab, Ctrl+F4)
5. Gestion auto-hide (strips, drawer, pin/unpin)
6. Gestion des fenêtres flottantes
7. Maximize/restore
8. Registry des panels (`_panelRegistry`)
9. Chargement/sauvegarde de layout
10. Gestion des souscriptions nœuds (`SyncNodeSubscriptions`)
11. Tracking du panel actif (`ActiveDockable`)

**Impact :** Impossible de tester les comportements de drag & drop, auto-hide, ou maximize sans
instancier un `MGDockHost` complet (qui requiert un `MGDesktop` et MonoGame).

#### 6.2.2 Absence de dirty-batching sur `LayoutChanged`

Tout changement de propriété sur n'importe quel nœud déclenche un rebuild complet du visual tree.
Ceci n'est pas batché. Lors d'un `LoadLayoutFromJson` avec 20 nœuds, chaque nœud modifié lors
de la désérialisation peut déclencher un rebuild.

**Mitigation actuelle :** `UnpinPanel`/`RepinPanel` utilisent un pattern de désabonnement
temporaire (`_suppressLayoutChanged`) pour batcher les mutations — c'est un workaround correct
mais non généralisé.

#### 6.2.3 `_panelRegistry` vs. `GetAllTabGroups().SelectMany`

`_panelRegistry` est un `Dictionary<string, DockPanelNode>` maintenu manuellement, mais son
contenu est redondant avec `LayoutModel.GetAllTabGroups().SelectMany(g => g.Panels)`. Des
incohérences sont possibles si un panel est ajouté au modèle sans passer par `MGDockHost`.

#### 6.2.4 XML doc partielle

Les méthodes publiques de `DockOperation.cs` ont une bonne couverture XML. En revanche,
plusieurs méthodes `internal` et `private` dans `MGDockHost.cs` (ex: `BuildTabGroup`,
`BuildSplitContainer`, `SyncNodeSubscriptions`) n'ont pas de doc — acceptable pour du privé
mais rend l'audit difficile.

### 6.3 Plan de refactoring priorisé

#### Quick wins (< 1 jour chacun)

| # | Action | Bénéfice |
|---|--------|----------|
| QW1 | Ajouter `BeginUpdate()`/`EndUpdate()` sur `DockLayoutModel` avec dirty-flag | Évite N rebuilds lors de chargement JSON |
| QW2 | Mettre en cache `CalculateHostEdgeZones` sur changement de `LayoutBounds` | Réduit allocations pendant drag |
| QW3 | Remplacer `_panelRegistry` par une property calculée `GetDockedPanelIds()` | Élimine la source d'incohérence |

#### Refactors moyens (1–3 jours chacun)

| # | Action | Bénéfice |
|---|--------|----------|
| RM1 | Extraire `DockDragManager` de `MGDockHost` (BeginDrag/UpdateDrag/PerformDrop/Cancel + _dragData + _dropIndicators) | Testable séparément, réduit MGDockHost de ~500 lignes |
| RM2 | Extraire `DockAutoHideManager` (strip, drawer, pin/unpin, snapshots) | Réduit MGDockHost de ~400 lignes |
| RM3 | Extraire `DockKeyboardManager` (Ctrl+Tab cycle, Ctrl+F4) | Testable avec mock IActivatablePanel |

#### Refactors majeurs (> 1 semaine)

| # | Action | Bénéfice |
|---|--------|----------|
| RJ1 | Introduire `IDockHostMediator` interface pour permettre des tests headless de la logique host | Tous les comportements testables sans MonoGame |
| RJ2 | Passer `DockPanelNode.ContentFactory` à `Func<object>` avec résolution via service locator | Découple complètement model layer de MGElement |

### 6.4 Abstractions manquantes

| Abstraction | Justification |
|-------------|---------------|
| `IDockLayoutObserver` | Permettrait à des composants externes de s'abonner aux changements de layout sans coupler à `DockLayoutModel` concret |
| `IDockPanelLifecycle` | Interface `OnShown()`, `OnHidden()`, `OnActivated()` implémentée par les contenus de panel — actuellement tout passe par `DockableRegistry` events |
| `IDockCommandBus` | Pattern Command pour les opérations `BeginDrag/PerformDrop` — permettrait undo/redo |

### 6.5 Stratégie de test recommandée

| Couche | Approche | Outils |
|--------|----------|--------|
| **Model layer** (`DockLayout/`) | Tests unitaires purs xUnit — déjà en place (185 tests) | xUnit, aucune dépendance MonoGame |
| **Host behavior** (drag, auto-hide, maximize) | Tests d'intégration headless après extraction des managers (RM1-RM3) | xUnit + mock `ILayoutBoundsProvider` |
| **Visual rendering** | Smoke tests / screenshot comparison | MonoGame headless mode ou captures manuelles |
| **Non-régression UI** | Tests manuels déclenché par PR + checklist visuelle | Compendium sample |

### 6.6 Conclusion

L'architecture du système de docking est **solide dans sa couche model** : séparation nette,
testabilité élevée, event-driven proprement. Les problèmes réels sont **concentrés dans
`MGDockHost`** qui est un God Class classique — conséquence inévitable de développements
itératifs sur un contrôle de haute complexité.

Les 6 bugs corrigés en Phase 1-2 (fuites d'event handlers, registry stale, sérialiseur incomplet,
try/catch silencieux) étaient des défauts réels mais de sévérité moyenne — aucun ne causait de
crash, mais ils auraient conduit à des comportements incohérents en usage intensif (nombreux
rebuilds, layouts perdus au save/load, registry phantoms).

**Priorité recommandée :**
1. `BeginUpdate/EndUpdate` (QW1) — impact immédiat sur la performance de chargement
2. Extraction `DockDragManager` (RM1) — plus grande réduction du God Class
3. Extraction `DockAutoHideManager` (RM2) — deuxième plus grande réduction
