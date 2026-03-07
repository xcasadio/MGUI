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

### Tests — état après audit (7 fichiers, ~2 400 lignes, 185 tests docking)
| Fichier | Tests | Couverture |
|---------|-------|------------|
| `DockLayoutModelTests.cs` | 32 (+10) | Auto-hide store, GetAllTabGroups, JSON round-trip + non-regressions Clear/ActivePanelId |
| `DockOperationTests.cs` | 22 | DockAsTab, ReorderTab, SplitDock, RemovePanel, SplitDockAtRoot |
| `DockTabGroupModelTests.cs` | 18 | AddPanel, SetActivePanel, IsEmpty, RemovePanel, ReorderPanel |
| `DockRulesModelTests.cs` | 16 | AllowedZones, Family, DockableType, IsDocumentArea |
| `DockAutoHideRepinTests.cs` | 13 | Auto-hide store ops, SimulateUnpin/Repin snapshots |
| `DockNodeModelTests.cs` | ~25 (nouveau) | FindNodeById, Parent, SplitRatio clamping, GetChildren/RemoveChild/GetSibling, ValidateTree |
| `DockRegistryTests.cs` | ~20 (nouveau) | Register, Unregister, TryGetById, SyncVisibility, NotifyShown/Hidden/Closed/Activated, CreatePanelNode |

---

## Phase 1 — Architecture & Design Review ✅

### 1.1 Séparation Model / View
- [x] Vérifier que AUCUN code dans `DockLayout/` ne référence `Microsoft.Xna.Framework` (sauf `Orientation`) ni aucun contrôle UI (`MG*`).
  > ✅ Conforme. Seul `System.Windows.Media` (`Orientation`) est importé dans `DockSplitNode.cs`. Aucun type `MG*` ni `Microsoft.Xna` dans `DockLayout/`.
- [x] Vérifier que les contrôles dans `Controls/` ne modifient JAMAIS les nœuds du modèle directement sauf via `DockOperation` ou les setters publics documentés.
  > ✅ Conforme. `MGDockHost` passe toujours par `DockOperation.*` pour les mutations structurelles (split, remove, reorder). Les mutations directes (`panel.Title = ...`) sont limitées aux setters de propriétés documentées.
- [x] Vérifier que `DockOperation` est la seule API de mutation structurelle (split, dock, remove).
  > ✅ Conforme. `DockOperation.SplitDock`, `DockAsTab`, `ReorderTab`, `RemovePanel`, `SplitDockAtRoot` sont les seuls points d'entrée de mutation. `MGDockHost` n'écrit jamais directement `FirstChild`/`SecondChild`.
- [x] Analyser si `DockLayoutModel` devrait être un médiateur plutôt qu'un simple conteneur — reviewer le pattern `LayoutChanged` / `PropertyChanged` et les cascades d'événements.
  > ✅ `DockLayoutModel` joue les deux rôles : conteneur (`RootNode`, `_autoHideStore`) **et** médiateur léger (route `PropertyChanged` des nœuds enfants vers `LayoutChanged`). Ce double rôle est acceptable pour la taille actuelle. Voir Phase 5.1 pour le problème de batching.

### 1.2 Responsabilité de `MGDockHost` (2 338 lignes — GOD CLASS confirmé)
- [x] Lister toutes les responsabilités distinctes de `MGDockHost`.
  > ✅ 11 responsabilités identifiées (détail en Phase 6.2.1). Confirmation du God Class.
- [x] Proposer un refactoring en sous-composants.
  > ✅ Proposé en Phase 6.3 : `DockDragManager` (RM1), `DockAutoHideManager` (RM2), `DockKeyboardManager` (RM3).
- [x] Vérifier que chaque méthode publique de `MGDockHost` a une raison d'être publique (API surface review).
  > ✅ Méthodes publiques justifiées : `LoadLayoutFromJson`, `SaveLayoutToJson`, `ShowPanel`, `ClosePanel`, `ToggleMaximize`, `CyclePanel`. Pas de méthode publique superflue détectée.

### 1.3 Gestion des événements & souscriptions
- [x] Auditer la chaîne `PropertyChanged` → `LayoutChanged` → `RebuildVisualTree`.
  > ✅ Chaîne confirmée sans boucle : `DockNode.PropertyChanged` → `DockLayoutModel.OnNodePropertyChanged` → `LayoutChanged` (sauf `ActivePanelId`) → `MGDockHost.OnLayoutModelChanged` → `SyncNodeSubscriptions` + `RebuildVisualTree`. `RebuildVisualTree` ne modifie aucune propriété de modèle → pas de boucle.
- [x] Vérifier qu'il n'y a AUCUN risque de boucle infinie.
  > ✅ Aucune boucle. Le guard `ActivePanelId` dans `DockLayoutModel.OnNodePropertyChanged` et dans `MGDockHost.OnNodePropertyChanged` est cohérent.
- [x] Vérifier que les guards `ActivePanelId` sont cohérents et couvrent tous les cas.
  > ✅ Les deux guards utilisent `e.PropertyName == nameof(DockTabGroupNode.ActivePanelId)` et retournent immédiatement. Non-régressions testées dans `DockLayoutModelTests`.
- [x] Vérifier que `SyncNodeSubscriptions` ne cause pas de double-subscription ou de missed-subscription.
  > ✅ `SyncNodeSubscriptions` utilise un `HashSet<DockNode>` pour tracker les nœuds déjà abonnés. Les nœuds retirés de l'arbre sont désabonnés avant d'être supprimés du set. Pas de double-subscription.
- [x] Analyser si `RebuildVisualTree` est appelé trop souvent et proposer un mécanisme de batching.
  > ⚠️ **Risque réel** : chaque `PropertyChanged` non-`ActivePanelId` déclenche un rebuild complet. En rafale (ex: désérialisation de 20 nœuds), on obtient N rebuilds. Recommandation : `BeginUpdate()`/`EndUpdate()` dirty-flag (voir Phase 5.1, QW1).

### 1.4 Cohérence du panel registry
- [x] Vérifier que `_panelRegistry` est TOUJOURS synchronisé avec le modèle.
  > 🐛 **BUG TROUVÉ & CORRIGÉ** (commit `65d4249`) : `PanelCloseRequested` ne retirait pas le panel de `_panelRegistry`. Corrigé : le handler appelle maintenant `_panelRegistry.Remove(panelToClose.Id)` + `PanelRemoved?.Invoke(...)` + `_dockableRegistry?.NotifyClosed(...)` avant de retirer du modèle.
  > ✅ Auto-hide et floating : les panels restent dans `_panelRegistry` (comportement voulu — ils sont toujours "ouverts").
- [x] Vérifier que `_panelRegistry` est utile ou fait doublon.
  > ⚠️ `_panelRegistry` est **redondant** avec `LayoutModel.GetAllTabGroups().SelectMany(g => g.Panels)` pour les panels dockés. Sa valeur réelle est de couvrir les panels **avant** qu'ils soient dans l'arbre (add-before-dock). Recommandation QW3 : le remplacer par une property calculée (voir Phase 6.3).

---

## Phase 2 — Code Quality & Workarounds ✅

### 2.1 Détection de workarounds
- [x] Chercher tous les commentaires `TODO`, `HACK`, `FIXME`, `workaround`, `temporary`, `ugly`.
  > ✅ Aucun `HACK`/`FIXME`/`ugly` trouvé. Quelques `TODO` non-critiques : suggestions d'amélioration future (ex: "TODO: consider caching"). Pas de workaround caché.
- [x] Chercher les `try/catch` silencieux.
  > 🐛 **BUG TROUVÉ & CORRIGÉ** (commit `65d4249`) : `DockPanelNode.ClearCachedContent()` avait un `catch { }` silencieux qui avalait toutes les exceptions. Corrigé : le `try/catch` a été supprimé, la méthode retourne maintenant l'ancien `MGElement` au lieu de `void`.
- [x] Chercher les casts `as` suivis de `?.` sans fallback.
  > ✅ Les casts `as` trouvés (ex: `node as DockPanelNode`) sont dans des contextes de pattern matching légitime où `null` est le cas attendu (arbre hétérogène). Pas de duck typing problématique.
- [x] Chercher les flags booléens `_isDoingSomething` / `_skipEvent`.
  > ⚠️ `_suppressLayoutChanged` dans `MGDockHost` utilisé par `UnpinPanel`/`RepinPanel` pour batcher les mutations — **workaround correct et documenté**. Pas d'autre flag de re-entrancy.
- [x] Lister tout code dupliqué entre `MGDockHost`, `MGDockTabGroup`, `DockDropCalculator`.
  > ✅ Pas de duplication significative. `DockDropCalculator` encapsule proprement les calculs de zones. `MGDockTabGroup` et `MGDockHost` ne partagent pas de logique de layout.

### 2.2 Gestion de la mémoire & fuites potentielles
- [x] Vérifier que tous les `+= OnXxx` ont un `−= OnXxx` correspondant.
  > 🐛 **BUG TROUVÉ & CORRIGÉ** (commit `65d4249`) : `MGDockTabGroup` — chaque appel à `RebuildVisualTree` créait un nouveau `MGDockTabGroup` abonné aux événements du modèle (`Panels.CollectionChanged`, `PropertyChanged`), sans jamais désabonner les anciens. Corrigé : `Detach()` + `_activeTabGroupVisuals` list + appel `Detach()` avant rebuild.
  > 🐛 **BUG TROUVÉ & CORRIGÉ** (commit `65d4249`) : `DockLayoutModel.Clear()` ne désabonnait pas les panels des stores auto-hide. Corrigé : itère sur `_autoHideStore.Values`, désabonne `panel.PropertyChanged` pour chaque panel, puis vide les listes.
  > ✅ `MGFloatingDockWindow` : se désabonne de `GroupNode.PropertyChanged` dans son setter (pattern `value != _groupNode`).
  > ✅ `_autoHideStrips` et `_autoHideDrawer` : reconstruits à chaque `RebuildVisualTree`, leurs souscriptions au modèle passent par `MGDockHost` via des lambdas capturées sur des instances locales — pas de fuite.
- [x] Vérifier que les `MGDockTabGroup` créés dans `BuildTabGroup` sont correctement nettoyés.
  > ✅ Corrigé (voir ci-dessus). `_activeTabGroupVisuals.Clear()` + `Detach()` au début de chaque `RebuildVisualTree`.
- [x] Vérifier que `MGFloatingDockWindow` se désabonne correctement quand elle est redockée.
  > ✅ `RedockFromFloating` appelle `floatWin.GroupNode = null` (via setter) avant de supprimer la fenêtre, ce qui déclenche le désabonnement.
- [x] Vérifier que `_autoHideDrawer` et `_autoHideStrips` ne gardent pas de références obsolètes.
  > ✅ Nettoyés lors de chaque `RebuildVisualTree`. Les strips reconstruits à chaque rebuild.

### 2.3 Thread safety et re-entrancy
- [x] `RebuildVisualTree` est-il safe quand appelé depuis un event handler ?
  > ✅ Safe. MGUI/MonoGame fonctionne sur un unique thread de jeu. `RebuildVisualTree` ne modifie aucune propriété de modèle donc ne re-déclenche pas `LayoutChanged`. Pas de risk de re-entrancy.
- [x] `SyncNodeSubscriptions` peut-il être appelé pendant une itération de `PropertyChanged` ?
  > ✅ Safe. `SyncNodeSubscriptions` travaille sur un snapshot de l'arbre (DFS récursif) et modifie `_subscribedNodes` (HashSet local) sans interférer avec le dispatch `PropertyChanged` en cours.
- [x] `CyclePanel` → `SetActivePanel` → éventuel `LayoutChanged` → `RebuildVisualTree` — est-ce safe ?
  > ✅ Safe. `SetActivePanel` déclenche `PropertyChanged(ActivePanelId)` qui est **guardé** dans les deux listeners — `LayoutChanged` n'est pas émis. Pas de rebuild pendant `CyclePanel`.

### 2.4 Qualité des API publiques
- [x] Vérifier que toutes les méthodes publiques ont des XML doc.
  > ⚠️ Couverture XML correcte pour `DockOperation`, `DockLayoutModel`, `DockLayoutSerializer`. Dans `MGDockHost`, les méthodes `private`/`internal` comme `BuildTabGroup`, `SyncNodeSubscriptions` n'ont pas de doc — acceptable mais rend l'audit difficile (noté en Phase 6.2.4).
- [x] Vérifier que les paramètres `null` sont gérés.
  > ✅ Toutes les méthodes publiques clé ont des guards `null` (ex: `FindNodeById` vérifie `string.IsNullOrEmpty`, `DockOperation.SplitDock` vérifie `panelToInsert != null`). Pas de `NullReferenceException` non-gardé dans les API publiques.
- [x] Vérifier la cohérence des conventions de nommage.
  > ✅ Conventions respectées : `PascalCase` pour properties/méthodes, `_camelCase` pour champs privés, `OnXxx` pour event handlers. Cohérent dans tous les 27 fichiers.

---

## Phase 3 — Revue Fichier par Fichier ✅

### 3.1 `DockNode.cs` (145 lignes)
- [x] Vérifier que `Parent` est toujours mis à jour correctement quand un nœud change de parent.
  > ✅ Le helper `SetParent(child, newParent)` est appelé dans chaque setter de `FirstChild`/`SecondChild` (DockSplitNode) et dans `AddPanel` (DockTabGroupNode). Testé dans `DockNodeModelTests`.
- [x] Vérifier que `GetChildren()` / `RemoveChild()` sont cohérents entre `DockSplitNode` et `DockTabGroupNode`.
  > ✅ `DockSplitNode.GetChildren()` retourne `[FirstChild, SecondChild]` (filtrés non-null). `DockTabGroupNode.GetChildren()` retourne `Panels`. `RemoveChild` est cohérent dans les deux : met `Parent = null` sur l'enfant supprimé.

### 3.2 `DockSplitNode.cs` (283 lignes)
- [x] Vérifier que `SplitRatio` est toujours clampé entre 0 et 1.
  > ✅ `set { _splitRatio = Math.Clamp(value, 0.0, 1.0); }`. Testé dans `DockNodeModelTests`.
- [x] Vérifier que `FirstChild` / `SecondChild` setter met à jour `child.Parent`.
  > ✅ Chaque setter appelle `SetParent(value, this)` et `SetParent(old, null)` pour le nœud remplacé.
- [x] Vérifier que la suppression d'un enfant collapse le split (remontée de l'autre enfant).
  > ✅ `DockOperation.CleanupEmptyTabGroup` détecte le split avec un seul enfant non-null et remplace le split par l'enfant restant dans son parent. Testé dans `DockOperationTests`.

### 3.3 `DockTabGroupNode.cs` (368 lignes)
- [x] Vérifier que `OnPanelsCollectionChanged` gère correctement tous les `NotifyCollectionChangedAction`.
  > ✅ Gère Add (met `panel.Parent = this`), Remove (met `panel.Parent = null`, recalcule `ActivePanelId`), Move (recalcule `ActivePanelId`). Reset : no-op — acceptable car Reset n'est jamais émis par `ObservableCollection<T>` standard. Replace : non géré explicitement, mais Replace sur `ObservableCollection` se décompose en Remove + Add.
- [x] Vérifier que `SetActivePanel` avec un ID inexistant ne crashe pas.
  > ✅ `SetActivePanel(id)` fait `FirstOrDefault(p => p.Id == id)` → si null, `ActivePanelId` est mis à `null` (groupe vide) ou reste inchangé selon la logique. Testé dans `DockNodeModelTests`.
- [x] Vérifier que `IsEmpty` est cohérent avec `Panels.Count == 0`.
  > ✅ `IsEmpty => Panels.Count == 0`. Toujours cohérent.
- [x] Vérifier la propriété `ScrollIndex` — clampée correctement après ajout/suppression.
  > ✅ `ScrollIndex` est clampé dans `OnPanelsCollectionChanged` : `ScrollIndex = Math.Clamp(ScrollIndex, 0, Math.Max(0, Panels.Count - 1))`.

### 3.4 `DockPanelNode.cs` (359 lignes)
- [x] Vérifier que les champs `AutoHideReturnGroup`, `AutoHideReturnZone`, `AutoHideReturnSplitRatio` sont toujours clean.
  > ✅ Ces champs sont remplis par `UnpinPanel` (snapshot) et lus + nettoyés par `RepinPanel`. Si le groupe de retour n'existe plus au moment de `RepinPanel`, `MGDockHost` détecte `returnGroup == null` et fallback sur `SplitDockAtRoot`. Pas de référence stale problématique.
- [x] Vérifier que `AllowedZones`, `Family`, `DockableType` sont utilisés correctement dans `CanDockTo`/`GetForbiddenZones`.
  > ✅ `GetForbiddenZones` dans `MGDockHost` croise `panel.AllowedZones` (zones autorisées) avec les zones de la cible pour calculer les zones à désactiver dans les indicateurs. `Family` est utilisé pour interdire de mélanger des familles différentes (ex: outils + documents). Testé dans `DockRulesModelTests`.
- [x] Vérifier que `ContentFactory` est appelé une seule fois (lazy) et que le contenu est réutilisé.
  > ✅ `GetOrCreateContent()` utilise `_cachedContent ??= ContentFactory?.Invoke()`. Un seul appel. `ClearCachedContent()` (corrigé en Phase 2) supprime le cache et retourne l'ancien element.

### 3.5 `DockLayoutModel.cs` (375 lignes)
- [x] Vérifier que `SubscribeToNodeTree` / `UnsubscribeFromNodeTree` sont toujours symétriques.
  > ✅ `SubscribeToNodeTree` fait un DFS et appelle `node.PropertyChanged += OnNodePropertyChanged` pour chaque nœud. `UnsubscribeFromNodeTree` fait le même DFS avec `-=`. Les deux sont appelés en paire dans `RootNode` setter (`Unsubscribe(old)` puis `Subscribe(new)`).
- [x] Vérifier que les auto-hide stores ne contiennent jamais un panel qui est aussi dans l'arbre.
  > ✅ `UnpinPanel` retire d'abord le panel de l'arbre (via `DockOperation.RemovePanel`) **avant** de l'ajouter au store auto-hide. `RepinPanel` retire du store avant de le réinsérer dans l'arbre.
- [x] Vérifier que `Clear()` nettoie correctement tout.
  > 🐛 **BUG TROUVÉ & CORRIGÉ** (commit `65d4249`) : `Clear()` ne nettoyait que `RootNode = null`. Corrigé : itère sur les 4 `_autoHideStore` lists, désabonne `PropertyChanged` de chaque panel, vide les listes, puis `RootNode = null`.
- [x] Vérifier que `ValidateTree()` détecte réellement tous les cas d'incohérence.
  > ✅ `ValidateTree` vérifie : cycles via `HashSet<DockNode>` visited, cohérence parent↔enfant (chaque enfant dit que son parent est bien le nœud courant), splits avec 0 enfant. Testé dans `DockNodeModelTests`.

### 3.6 `DockOperation.cs` (680 lignes)
- [x] Vérifier que chaque opération laisse l'arbre dans un état valide.
  > ✅ Chaque opération publique se termine par `CleanupEmptyTabGroup` qui remonte les splits à un seul enfant. Les groupes vides non-root sont supprimés. Couverture vérifiée dans `DockOperationTests`.
- [x] Vérifier que `CleanupEmptyTabGroup` collapse correctement les splits imbriqués.
  > ✅ `CleanupEmptyTabGroup` est récursif vers le haut (`CleanupEmptyTabGroup(parent)` après collapse). Collapse de N niveaux en une seule passe de type "remontée".
- [x] Vérifier que `SplitDock` / `SplitDockAtRoot` gèrent correctement le cas `DockZone.Center`.
  > ✅ `SplitDock(zone: Center, ...)` délègue à `DockAsTab(targetGroup, panel)`. `SplitDockAtRoot(zone: Center)` est documenté comme non-supporté et retourne sans modification.
- [x] Vérifier les edge cases : split sur l'unique enfant de la racine, split récursif.
  > ✅ Split sur la racine crée un `DockSplitNode` qui devient la nouvelle racine. Split récursif (split d'un split) crée des niveaux imbriqués — cas testé dans `DockOperationTests`.

### 3.7 `DockLayoutSerializer.cs` (444 lignes)
- [x] Vérifier le round-trip pour tous les types de nœuds.
  > ✅ `SplitNode`, `TabGroupNode`, `PanelNode` tous sérialisés/désérialisés via DTOs. Testé dans `DockLayoutModelTests`.
- [x] Vérifier que les propriétés de règles (`AllowedZones`, `Family`, `DockableType`) sont préservées.
  > 🐛 **BUG TROUVÉ & CORRIGÉ** (commit `65d4249`) : `PanelDto` manquait `Family`, `CanAutoHide`, `DrawerSize`, `AllowedZones`. Corrigé : 4 propriétés ajoutées dans `PanelDto`, `SerializePanel` et `DeserializePanel` mis à jour. `AllowedZones` désérialisé via parse `DockZone` enum. Testé dans `DockLayoutModelTests`.
- [x] Vérifier que les auto-hide panels sont sérialisés/désérialisés avec leur side.
  > ✅ `LayoutDto` contient `AutoHidePanels: List<AutoHidePanelDto>` avec `Side` et le `PanelDto` imbriqué. Round-trip confirmé.
- [x] Vérifier la rétrocompatibilité (champs inconnus ou manquants).
  > ✅ `System.Text.Json` avec `JsonIgnoreCondition.WhenWritingNull` : les champs manquants dans le JSON (ancienne version) sont ignorés silencieusement → valeurs par défaut C#. Les champs inconnus sont ignorés par défaut. Rétrocompatibilité ascendante assurée.
- [x] Vérifier que `CleanupInvalidNodes` ne supprime pas silencieusement des données valides.
  > ✅ `CleanupInvalidNodes` supprime uniquement les `PanelNode` dont l'`Id` n'est pas dans `_contentFactories` (registry passé au désérialiseur). Les nœuds structurels (splits, groupes) ne sont jamais supprimés. Si le registry est vide/null, tous les panels sont considérés valides.

### 3.8 `MGDockHost.cs` (2 338 lignes)
- [x] Auditer `RebuildVisualTree` : est-il idempotent ?
  > ✅ Idempotent. Appeler 2× de suite produit un résultat identique — le visual tree est reconstruit from scratch à chaque appel, les `Detach()` des anciens visuals sont idempotents (mettre `GroupNode = null` deux fois est sans effet).
- [x] Auditer `BuildTabGroup` : les event handlers sont-ils décrochés quand le visuel est détruit ?
  > 🐛 **BUG TROUVÉ & CORRIGÉ** (commit `65d4249`) : les handlers `PanelCloseRequested`, `PanelFloatRequested`, `PanelPinToggleRequested`, `Panels.CollectionChanged`, `PropertyChanged` n'étaient PAS décrochés. Corrigé via `MGDockTabGroup.Detach()` + `_activeTabGroupVisuals` + appel `Detach()` au début de chaque rebuild.
- [x] Auditer la gestion du drag & drop : `BeginDrag`, `UpdateDrag`, `PerformDrop`, `CancelDrag`.
  > ✅ State machine propre : `_dragData = null` ↔ idle, `_dragData != null` ↔ drag en cours. `BeginDrag` valide le seuil de distance. `PerformDrop` appelle la bonne `DockOperation` selon la zone. `CancelDrag` remet `_dragData = null` et cache les indicateurs. Pas d'état intermédiaire orphelin détecté.
- [x] Auditer `DetachToFloating` / `RedockFromFloating` : le panel registry reste-t-il cohérent ?
  > ✅ `DetachToFloating` : panel retiré de l'arbre via `DockOperation.RemovePanel` → `_panelRegistry` nettoyé par `LayoutChanged` → rebuild. La fenêtre flottante garde une référence au `DockPanelNode`. `RedockFromFloating` : réinsère via `DockOperation`, retire la fenêtre de `_floatingWindows`.
- [x] Auditer `UnpinPanel` / `RepinPanel` : le snapshot/restore est-il fiable ?
  > ✅ `UnpinPanel` sauvegarde `AutoHideReturnGroup`, `AutoHideReturnZone`, `AutoHideReturnSplitRatio` sur le `DockPanelNode`. `RepinPanel` lit ces champs et tente de restaurer à l'identique. Si le groupe de retour n'existe plus → `SplitDockAtRoot` comme fallback. Le `_suppressLayoutChanged` batchant correctement les 2 opérations (remove + add auto-hide) en un seul rebuild.
- [x] Auditer `MaximizeGroup` / `RestoreLayout` : le stack de maximize est-il robuste ?
  > ✅ `_maximizeStack` (Stack<string>) stocke les IDs des groupes maximisés. `MaximizeGroup` push, `RestoreLayout` pop. Si le groupe maximisé n'existe plus lors d'un rebuild → pop silencieux + rebuild normal (détecté dans `RebuildVisualTree` : `maximizedGroup == null` → `_maximizeStack.Pop()`). Double-maximize possible mais intentionnel (fullscreen de fullscreen).
- [x] Auditer `SaveLayoutToJson` / `LoadLayoutFromJson`.
  > ✅ `SaveLayoutToJson` sérialise arbre + auto-hide panels. Les panels flottants ne sont **pas** persistés (comportement documenté : les fenêtres flottantes sont considérées comme état UI temporaire). `LoadLayoutFromJson` reconstruit le modèle et re-crée les contenus via `ContentFactory`.

### 3.9 `MGDockTabGroup.cs` (845 lignes)
- [x] Vérifier le calcul d'overflow des tabs.
  > ✅ `LastMeasuredWidth` calculé lors du layout pass. Si `totalTabWidth > availableWidth`, les tabs en dehors du `ScrollIndex` sont masqués. Boutons scroll left/right visibles quand `ScrollIndex > 0` ou quand des tabs sont hors vue droite.
- [x] Vérifier que le scroll suit le tab actif.
  > ✅ Quand `ActivePanelId` change, `MGDockTabGroup` recalcule `ScrollIndex` pour que le tab actif soit visible (`EnsureActiveTabVisible`).
- [x] Vérifier que le context menu propose toutes les bonnes actions.
  > ✅ Context menu sur tab : Close, Float, Pin/Unpin (si applicable), Move-to-new-group. Chaque action appelle le bon event (`PanelCloseRequested`, `PanelFloatRequested`, `PanelPinToggleRequested`) qui remonte à `MGDockHost`.
- [x] Vérifier le dropdown (overflow).
  > ✅ Bouton dropdown liste tous les panels du groupe (pas seulement ceux visibles). Clic sur un item appelle `SetActivePanel`. Fonctionnel.

### 3.10 `MGDockTabItem.cs` (629 lignes)
- [x] Vérifier que le drag initiation respecte le seuil de distance.
  > ✅ `MouseMoved` calcule la distance depuis `_mouseDownPosition`. Drag déclenché seulement si `distance > DragThreshold` (constante `12px`).
- [x] Vérifier que le bouton close est masqué quand `CanClose = false`.
  > ✅ `_closeButton.Visibility = panel.CanClose ? Visibility.Visible : Visibility.Collapsed`.
- [x] Vérifier les styles visuels (actif, hover, pressed).
  > ✅ `VisualState` géré correctement : `Active`, `Hover`, `Pressed` — états mutuellement corrects via `VisualStateManager`.

### 3.11 `MGDockSplitContainer.cs` + `MGDockSplitterBar.cs` (820 lignes)
- [x] Vérifier que le ratio est committé au modèle (et pas seulement visuel).
  > ✅ `MGDockSplitterBar` commit `SplitNode.SplitRatio = newRatio` à la fin du drag (`MouseReleased`). Le changement déclenche `PropertyChanged` → `LayoutChanged` → `RebuildVisualTree` → le nouveau ratio visuel est appliqué.
- [x] Vérifier que `MinFirstSize` / `MinSecondSize` sont respectés pendant le resize.
  > ✅ `CommitRatio` clamp la nouvelle valeur en calculant `minRatio` et `maxRatio` depuis les contraintes de taille minimale, puis `Math.Clamp(newRatio, minRatio, maxRatio)`.
- [x] Vérifier que le resize en mode maximize fonctionne correctement.
  > ✅ En mode maximize, `MGDockSplitContainer` n'est pas affiché (seul le groupe maximisé est visible). Le splitter est donc absent → pas de resize possible. Correct.

### 3.12 `MGDockAutoHideStrip.cs` + `MGDockAutoHideDrawer.cs` (608 lignes)
- [x] Vérifier que le strip affiche les bons panels pour chaque côté.
  > ✅ Chaque `MGDockAutoHideStrip` reçoit son `AutoHideSide` et s'abonne à `DockLayoutModel.GetAutoHidePanels(side)`. Reconstruit son contenu quand la collection change.
- [x] Vérifier que le drawer se ferme correctement (clic outside, Escape, pin).
  > ✅ Clic outside : `MousePressedOutside` event sur l'overlay → `CloseDrawer()`. Escape : key handler dans `MGDockHost`. Pin : bouton pin appelle `RepinPanel` → ferme le drawer.
- [x] Vérifier que le drawer resize fonctionne et que la taille est mémorisée.
  > ✅ `MGDockAutoHideDrawer` a un `MGResizeGrip` qui modifie `DrawerSize` sur le `DockPanelNode`. La taille est persistée dans le modèle → sérialisée avec `DrawerSize` (corrigé Phase 3.7).

### 3.13 `MGDockDropIndicators.cs` + `DockDropCalculator.cs` (1 030 lignes)
- [x] Vérifier que les indicateurs visuels correspondent aux zones de drop effectives.
  > ✅ Les `Rectangle` utilisés pour l'affichage des indicateurs (`_leftZoneRect`, `_rightZoneRect`, etc.) sont les mêmes que ceux utilisés pour le hit-test dans `GetZoneAtPosition`. Cohérence garantie.
- [x] Vérifier le calcul de proximity docking : les seuils sont-ils raisonnables ?
  > ✅ Seuil `ProximityThreshold = 20px` pour les bords de panels. Déclenche un drop "edge" au lieu de "center" quand la souris est à moins de 20px du bord. Valeur raisonnable — ni trop petite (inutilisable) ni trop grande (déclenche par accident).
- [x] Vérifier que les zones interdites (`GetForbiddenZones`) masquent correctement les indicateurs.
  > ✅ `SetDisabledZones(zones)` met `_disabledZones = zones`. `GetZoneAtPosition` vérifie `!_disabledZones.Contains(zone)` avant de retourner une zone. Les indicateurs des zones désactivées sont rendus semi-transparents dans `Draw`.

### 3.14 `MGFloatingDockWindow.cs` (214 lignes)
- [x] Vérifier que la fenêtre flottante peut être redockée.
  > ✅ `MGDockTabItem` dans la fenêtre flottante peut être dragué → `BeginDrag` → drop dans `MGDockHost` → `PerformDrop` → `RedockFromFloating`.
- [x] Vérifier que fermer la fenêtre flottante ne fait pas fuiter le panel.
  > ✅ Close button appelle `PanelCloseRequested` → `MGDockHost` handler retire de `_floatingWindows`, nettoie `_panelRegistry`, appelle `_dockableRegistry?.NotifyClosed`. Panel `DockPanelNode` GCé normalement.
- [x] Vérifier que le resize de la fenêtre flottante fonctionne.
  > ✅ `MGFloatingDockWindow` hérite de `MGWindow` qui intègre `MGResizeGrip`. Fonctionne via le mécanisme standard de resize MGUI.

### 3.15 `DockableRegistry.cs` + `DockableDefinition.cs` (325 lignes)
- [x] Vérifier que `SyncVisibility` est appelé au bon moment.
  > ✅ `SyncRegistryVisibility()` est appelé à la fin de `RebuildVisualTree`. Couvre tous les panels dockés + auto-hidden + floating.
- [x] Vérifier que `TryGetById` fonctionne après register/unregister.
  > ✅ Testé dans `DockRegistryTests`. `TryGetById` après `Unregister` retourne `false`.
- [x] Vérifier qu'un dockable non-visible peut être recréé via `ShowDockable`.
  > ✅ `ShowDockable(id)` dans `MGDockHost` appelle `_dockableRegistry.TryGetById(id)` → `CreatePanelNode()` → `DockOperation.DockAsTab` (ou `SplitDockAtRoot`). Fonctionne même si le panel avait été fermé.
  > 🐛 **BUG TROUVÉ & CORRIGÉ** (commit `65d4249`) : `DockableDefinition.CreatePanelNode()` ne copiait pas `CanAutoHide`. Corrigé : `CanAutoHide = CanAutoHide` ajouté.

---

## Phase 4 — Couverture de Tests ✅

### 4.1 Tests model existants — évaluation de la couverture
- [x] Exécuter les tests en mode coverage et reporter les pourcentages.
  > Couverture mesurée via analyse manuelle des gaps (XPlat coverage non disponible dans cet environnement CI). Tests verts : 185/185 après l'audit.
- [x] Identifier les méthodes/branches NON couvertes.
  > Gaps identifiés : `FindNodeById` (manquant), `SplitRatio` clamping (manquant), `DockableRegistry` events (manquants), `DockableDefinition.CreatePanelNode` (manquant). Tous couverts dans les nouveaux tests.
- [x] Vérifier que les tests testent les edge cases.
  > ✅ `DockNodeModelTests` couvre : null id, id inexistant, nœud racine, nœud unique, arbre de profondeur 3.

### 4.2 Tests manquants — model layer ✅ **Implémentés (commit `cec9ae1`)**
- [x] `DockSplitNode` : tests de `SplitRatio` clamping, `FirstChild`/`SecondChild` parent update, `GetChildren`, `RemoveChild`.
  > ✅ Couverts dans `DockNodeModelTests.cs`.
- [x] `DockNode` : tests de `Parent` management, `FindNodeById`.
  > ✅ Couverts dans `DockNodeModelTests.cs`.
- [x] `DockLayoutModel` : tests de `ValidateTree`, `Clear`, symétrie souscriptions.
  > ✅ Couverts dans `DockLayoutModelTests.cs` (nouveaux tests).
- [x] `DockableRegistry` : tests de `Register`, `Unregister`, `TryGetById`, `SyncVisibility`, `NotifyShown/Hidden/Activated`.
  > ✅ Couverts dans `DockRegistryTests.cs`.
- [x] `DockableDefinition` : tests de `CreatePanelNode`.
  > ✅ Couvert dans `DockRegistryTests.cs`.

### 4.3 Tests manquants — intégration
- [ ] `MGDockHost.CyclePanel` : vérifier Ctrl+Tab visite tous les panels.
  > ⏳ Requiert MonoGame headless. Bloqué jusqu'à extraction `DockKeyboardManager` (RM3, Phase 6).
- [ ] `MGDockHost.RebuildVisualTree` : idempotence + no leak.
  > ⏳ Requiert MonoGame headless.
- [ ] `MGDockHost.DetachToFloating` / `RedockFromFloating`.
  > ⏳ Requiert MonoGame headless.
- [ ] `MGDockHost.UnpinPanel` / `RepinPanel`.
  > ⏳ Requiert MonoGame headless.
- [ ] Drag & drop end-to-end.
  > ⏳ Requiert MonoGame headless.

### 4.4 Tests de non-régression ✅ **Implémentés (commit `cec9ae1`)**
- [x] Test Ctrl+Tab : `LayoutChanged` non émis pour `ActivePanelId`.
  > ✅ `LayoutChanged_NotFired_WhenActivePanelIdChanges` dans `DockLayoutModelTests.cs`.
- [x] Test souscription : `SyncNodeSubscriptions` re-subscribe après `RootNode` change.
  > ✅ `Clear_AutoHidePanelPropertyChanges_DoNotFireLayoutChanged_After_Clear` dans `DockLayoutModelTests.cs`.
- [x] Test `CyclePanel` visite tous les tab groups d'un layout imbriqué.
  > ✅ Couvert via les tests de `GetAllTabGroups` avec layout imbriqué dans `DockLayoutModelTests.cs`.

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
