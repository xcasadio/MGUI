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

## Phase 5 — Performance & Scalabilité

- [ ] Mesurer le coût de `RebuildVisualTree` avec 10, 50, 100 panels — est-il acceptable ?
- [ ] Vérifier que `GetAllTabGroups()` ne fait pas d'allocation excessive (ToList dans des hot paths).
- [ ] Analyser si le pattern `PropertyChanged → LayoutChanged → Rebuild` cause des rebuilds en cascade (N property changes = N rebuilds au lieu de 1 batché).
- [ ] Vérifier que `FindNodeById` n'est pas O(n²) dans les cas imbriqués.
- [ ] Vérifier que les indicateurs de drop ne recalculent pas les zones à chaque frame (seulement quand le mouse bouge).

---

## Phase 6 — Recommandations Architecturales

À produire après les phases 1-5 :

- [ ] Rédiger un bilan des forces et faiblesses de l'architecture actuelle.
- [ ] Proposer un plan de refactoring priorisé (quick wins vs. refactors majeurs).
- [ ] Identifier les abstractions manquantes (interfaces, patterns).
- [ ] Évaluer si le découplage model/view est suffisant pour permettre des tests unitaires de la couche host sans MonoGame.
- [ ] Proposer une stratégie de test (unit tests purs, integration tests headless, smoke tests visuels).
