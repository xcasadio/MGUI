# MGUI — Liste de tâches : Amélioration des Performances (Input & ListBox)

> **Instructions pour l'agent IA** : Traiter chaque tâche dans l'ordre. **Commiter après chaque tâche terminée** avec un message descriptif. Ne pas regrouper plusieurs tâches dans un seul commit. Les tâches sont ordonnées par dépendance — les fondations d'abord, puis les optimisations qui s'appuient dessus.

---

## Diagnostic du problème

Avec une `MGListBox` de 5800 éléments, l'application freeze car :

1. **Aucune virtualisation UI** : 5800 `MGListBoxItem` + 5800 `MGBorder` + 5800 `MGTextBlock` sont créés et ajoutés à un `MGStackPanel`. Chaque frame, les 5800 éléments sont traversés pour `Update()`, `ComputeTopmostHoveredElement()`, layout, et draw.

2. **Input testé sur TOUS les éléments** : `MGElement.UpdateContents()` appelle récursivement `Update()` sur chaque enfant, même ceux hors viewport. Le flag `RecentDrawWasClipped` empêche le dispatch des événements souris, mais le tree-walk complet (calcul de `VisualState`, transformations de coordonnées, propagation de `_CanReceiveMouseInput`) se fait pour tous les éléments.

3. **Recherche hover en O(n)** : `ComputeTopmostHoveredElement()` traverse récursivement tout l'arbre visuel sans early-out par bounds.

4. **Recherche linéaire d'item pressé** : `InternalItems?.FirstOrDefault(x => x.ContentPresenter.IsHovered)` scanne les 5800 items à chaque clic.

5. **LINQ dans les hot paths** : `HasSubscribedEvents` utilise `.Any()` avec des iterators `yield return`, causant des allocations chaque frame. `.Reverse().ToList()` dans `UpdateContents` crée des listes temporaires chaque frame.

6. **Layout complet** : Le `MGStackPanel` mesure et arrange les 5800 éléments à chaque passe de layout.

---

## Architecture cible

```
┌─────────────────────────────────────────────────────────┐
│ AVANT (actuel)                                          │
│                                                         │
│ MGListBox                                               │
│  └─ MGScrollViewer                                      │
│      └─ MGStackPanel (ItemsPanel)                       │
│          ├─ MGListBoxItem[0]   ← Update() chaque frame  │
│          ├─ MGListBoxItem[1]   ← Update() chaque frame  │
│          ├─ ...                                         │
│          └─ MGListBoxItem[5799] ← Update() chaque frame │
│                                                         │
│ Tous les 5800 éléments : créés, layoutés, updated,      │
│ testés pour input, traversés pour hover detection        │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ APRÈS (cible)                                           │
│                                                         │
│ MGListBox (VirtualizingMode = Auto | Always | Never)    │
│  └─ MGScrollViewer                                      │
│      └─ VirtualizingStackPanel                          │
│          ├─ Spacer (hauteur = items avant viewport)     │
│          ├─ MGListBoxItem[visible_0]  ← recyclé         │
│          ├─ MGListBoxItem[visible_1]  ← recyclé         │
│          ├─ ...                                         │
│          ├─ MGListBoxItem[visible_N]  ← recyclé         │
│          └─ Spacer (hauteur = items après viewport)     │
│                                                         │
│ Seuls ~20-30 éléments existent en mémoire.              │
│ Input géré par un handler consolidé unique.              │
│ Hover calculé par position mathématique, pas traversée. │
└─────────────────────────────────────────────────────────┘
```

---

## Phase 1 — Optimisations rapides (sans virtualisation)

### Tâche 1 — Cacher `HasSubscribedEvents` dans MouseHandler
- **Fichier** : `MGUI.Shared/Input/Mouse/MouseHandler.cs` (lignes ~194-200)
- **Problème** : `HasSubscribedEvents` recalcule via `.Any()` sur des enumerators `yield return` à chaque frame, causant des allocations GC.
- **Solution** :
  - Ajouter un champ `private bool _hasSubscribedEvents;`
  - Mettre à jour ce flag dans les accesseurs `add`/`remove` de chaque événement (`Scrolled`, `Moved*`, `Pressed*`, `Released*`, `Clicked*`, `DragStart`, `Dragged`, `DragEnd`)
  - `HasSubscribedEvents` devient une simple lecture de booléen
  - Faire pareil pour `IsMonitoringScroll`, `IsMonitoringMovement`, `IsMonitoringClicks`, `IsMonitoringDrag`
- **Impact** : Élimine des milliers d'allocations d'iterators par frame.
- **Commit** : `perf: cache HasSubscribedEvents in MouseHandler to avoid LINQ allocations`

### Tâche 2 — Cacher `HasSubscribedEvents` dans KeyboardHandler
- **Fichier** : `MGUI.Shared/Input/Keyboard/KeyboardHandler.cs`
- **Problème** : Même pattern que `MouseHandler` — vérifier et appliquer le même fix.
- **Commit** : `perf: cache HasSubscribedEvents in KeyboardHandler`

### Tâche 3 — Éliminer `.Reverse().ToList()` dans UpdateContents
- **Fichier** : `MGUI.Core/UI/MGElement.cs` (lignes ~1580-1584)
- **Problème** : `GetVisualTreeChildren(...).Reverse().ToList()` crée une liste temporaire à chaque frame pour chaque élément avec des enfants. Avec 5800 items, cela génère des milliers d'allocations.
- **Solution** :
  - Si `GetVisualTreeChildren` retourne une `IReadOnlyList`, itérer à l'envers avec un index `for (int i = count - 1; i >= 0; i--)`
  - Si c'est un `IEnumerable`, mettre en cache la liste des enfants et l'invalider uniquement quand la collection change
  - Alternative : stocker les enfants dans un `List<MGElement>` réutilisé (pooling)
- **Commit** : `perf: eliminate Reverse().ToList() allocations in UpdateContents`

### Tâche 4 — Éliminer les allocations LINQ dans ComputeTopmostHoveredElement
- **Fichier** : `MGUI.Core/UI/MGElement.cs` (lignes ~1455-1473)
- **Problème** : `Components.Where(x => x.DrawBeforeBackground).Select(x => x.BaseElement)` crée des iterators allocants à chaque appel, et ceci pour chaque élément de l'arbre.
- **Solution** :
  - Pré-calculer et cacher les listes de composants par catégorie lors de l'ajout/suppression de composants : `_componentsDrawBeforeBackground`, `_componentsDrawBeforeSelf`, `_componentsDrawBeforeContents`, `_componentsDrawAfterContents`
  - Utiliser ces listes cachées dans `ComputeTopmostHoveredElement` et `Update`
- **Impact** : Élimine N × nombre_de_catégories allocations d'iterators par frame.
- **Commit** : `perf: pre-compute component category lists to avoid LINQ in hot paths`

### Tâche 5 — Early-out par bounds dans ComputeTopmostHoveredElement
- **Fichier** : `MGUI.Core/UI/MGElement.cs` (lignes ~1443-1473)
- **Problème** : La recherche du hover traverse TOUT l'arbre visuel même si la souris est clairement en dehors des bounds d'un conteneur.
- **Solution** :
  ```csharp
  private void ComputeTopmostHoveredElement(..., Vector2 mousePos, ref MGElement Result)
  {
      if (Visibility != Visibility.Visible) return;
      if (RecentDrawWasClipped) return;  // AJOUT: skip les éléments clippés
      
      // AJOUT: early-out si la souris est hors des bounds de cet élément
      if (!ActualLayoutBounds.ContainsInclusive(mousePos))
          return;
      
      // ... reste de la logique
  }
  ```
  - Attention : les composants peuvent déborder de leur parent, il faut maintenir une marge ou ne pas faire l'early-out pour eux.
- **Impact** : Réduit la traversée de O(n) à O(log n) pour les hiérarchies bien organisées.
- **Commit** : `perf: add bounds-based early-out in ComputeTopmostHoveredElement`

### Tâche 6 — Skip Update pour les éléments hors viewport dans les ScrollViewers
- **Fichier** : `MGUI.Core/UI/MGScrollViewer.cs` (~ligne 525)
- **Problème** : `UpdateContents` appelle `base.UpdateContents(UA)` qui traverse TOUS les enfants, même ceux hors viewport.
- **Solution** :
  - Override `UpdateContents` dans `MGScrollViewer`
  - Calculer le viewport visible basé sur l'offset de scroll et la taille du viewport
  - Pour chaque enfant, vérifier si ses bounds intersectent le viewport
  - Appeler `child.Update(UA)` uniquement pour les enfants visibles ou partiellement visibles
  - Les enfants hors viewport reçoivent un update minimal (juste `_CanReceiveMouseInput = false`)
  - **Attention** : il faut quand même mettre à jour les propriétés minimales des enfants clippés pour que `RecentDrawWasClipped` fonctionne. Considérer un mode `Update` léger vs complet.
- **Impact** : Réduit les updates de 5800 à ~20-30 éléments visibles.
- **Commit** : `perf: skip full Update for off-viewport children in MGScrollViewer`

---

## Phase 2 — Optimisation de l'input pour MGListBox

### Tâche 7 — Handler d'input consolidé pour MGListBox
- **Fichier** : `MGUI.Core/UI/MGListBox.cs` (lignes ~754-770)
- **Problème** : Chaque `MGListBoxItem` a potentiellement son propre `MouseHandler`. La recherche de l'item pressé est un scan linéaire O(n) : `InternalItems?.FirstOrDefault(x => x.ContentPresenter.IsHovered)`.
- **Solution** :
  - Au lieu de scanner tous les items, calculer l'index de l'item sous la souris **mathématiquement** :
    ```csharp
    private MGListBoxItem<TItemType> GetItemAtMousePosition(Vector2 mousePos)
    {
        if (InternalItems == null || InternalItems.Count == 0) return null;
        
        // Convertir la position souris en coordonnées locales au ScrollViewer
        var localPos = mousePos - ContentViewportOrigin + ScrollOffset;
        
        // Si tous les items ont la même hauteur (cas commun) :
        int index = (int)(localPos.Y / ItemHeight);
        if (index >= 0 && index < InternalItems.Count)
            return InternalItems[index];
        
        return null;
    }
    ```
  - Remplacer les `FirstOrDefault` par cette méthode O(1)
  - Pour les items de hauteur variable, utiliser une recherche binaire sur les positions cumulées
- **Commit** : `perf: replace linear item search with mathematical position lookup in MGListBox`

### Tâche 8 — Désactiver les MouseHandlers individuels des items de ListBox
- **Fichier** : `MGUI.Core/UI/MGListBox.cs`
- **Problème** : Même avec la tâche 7, les `MouseHandler` individuels de chaque `MGListBoxItem.ContentPresenter` sont toujours créés et invoqués.
- **Solution** :
  - Ajouter une propriété `IsHitTestVisible = false` sur les `ContentPresenter` des items
  - Gérer tout l'input (hover, sélection, click) au niveau du `MGListBox` lui-même via son handler consolidé
  - Les items individuels n'ont plus besoin de gérer l'input
  - Mettre à jour le `VisualState` des items (hover, selected, pressed) depuis le `MGListBox` parent en fonction de la position calculée
- **Impact** : Élimine 5800 `ManualUpdate()` d'handlers souris par frame.
- **Commit** : `perf: disable individual input handlers on ListBox items, use consolidated handler`

---

## Phase 3 — Virtualisation UI

### Tâche 9 — Créer la classe `VirtualizingStackPanel`
- **Fichier** : Nouveau fichier `MGUI.Core/UI/Containers/VirtualizingStackPanel.cs`
- **Description** : Panel spécialisé qui ne crée et maintient que les éléments visibles dans le viewport.
- **Architecture** :
  ```csharp
  public class VirtualizingStackPanel : MGElement
  {
      // Pool d'éléments réutilisables
      private readonly Queue<MGElement> _recyclePool = new();
      
      // Mapping index → élément réalisé
      private readonly Dictionary<int, MGElement> _realizedItems = new();
      
      // Données du viewport
      public int FirstVisibleIndex { get; private set; }
      public int LastVisibleIndex { get; private set; }
      public int TotalItemCount { get; set; }
      public double ItemHeight { get; set; } // Hauteur uniforme (v1)
      
      // Callbacks
      public Func<int, MGElement> ItemGenerator { get; set; }
      public Action<int, MGElement> ItemRecycler { get; set; }
      
      // Méthodes clés
      private void RealizeItems(int firstIndex, int lastIndex) { ... }
      private void RecycleItem(int index) { ... }
      private MGElement GetOrCreateItem(int index) { ... }
      
      protected override Size MeasureOverride(Size availableSize)
      {
          // Hauteur totale = TotalItemCount * ItemHeight
          // Largeur = max des items réalisés
          return new Size(maxWidth, TotalItemCount * ItemHeight);
      }
      
      protected override void UpdateContents(ElementUpdateArgs UA)
      {
          // Calculer les indices visibles à partir du scroll offset
          int newFirst = (int)(scrollOffset / ItemHeight);
          int newLast = Math.Min(newFirst + visibleCount + 1, TotalItemCount - 1);
          
          // Recycler les items hors viewport
          // Réaliser les nouveaux items dans le viewport
          // Update uniquement les items réalisés
      }
  }
  ```
- **Points d'attention** :
  - Supporter le mode hauteur uniforme (v1) puis hauteur variable (v2)
  - Gérer le scroll smooth (pas seulement par item entier)
  - Maintenir la compatibilité avec le système de layout existant
- **Commit** : `feat: create VirtualizingStackPanel with item recycling`

### Tâche 10 — Intégrer le VirtualizingStackPanel dans MGListBox
- **Fichier** : `MGUI.Core/UI/MGListBox.cs`
- **Description** : Remplacer le `MGStackPanel` (ItemsPanel) par le `VirtualizingStackPanel` quand la virtualisation est activée.
- **Solution** :
  ```csharp
  public enum VirtualizationMode { Never, Auto, Always }
  
  // Dans MGListBox :
  public VirtualizationMode VirtualizationMode { get; set; } = VirtualizationMode.Auto;
  
  // Auto = virtualise si ItemsCount > seuil (ex: 100)
  // Le VirtualizingStackPanel remplace ItemsPanel
  // Les MGListBoxItem ne sont créés que pour les items visibles
  // La sélection reste basée sur les indices de données, pas les éléments UI
  ```
- **Points d'attention** :
  - `SelectedItems` doit fonctionner avec des indices logiques, pas des références UI
  - La sélection contiguë (Shift+Click) doit calculer les ranges sur les données
  - `AlternatingRowBackgrounds` doit se baser sur l'index logique
  - Le `ScrollViewer` doit rapporter la hauteur totale correcte (TotalItemCount × ItemHeight)
- **Commit** : `feat: integrate VirtualizingStackPanel into MGListBox`

### Tâche 11 — Gérer la sélection avec virtualisation
- **Fichier** : `MGUI.Core/UI/MGListBox.cs`
- **Description** : Adapter le système de sélection pour fonctionner avec les indices logiques au lieu des instances `MGListBoxItem`.
- **Solution** :
  - Stocker la sélection comme `HashSet<int>` (indices) au lieu de `List<MGListBoxItem>`
  - Quand un item est réalisé (rendu visible), appliquer le style "selected" s'il est dans le `HashSet`
  - Quand un item est recyclé, pas besoin de modifier la sélection
  - Exposer `SelectedIndices` et `SelectedDataItems` comme propriétés publiques
  - Maintenir la compatibilité avec l'API existante (`SelectedItems`) via des wrappers
- **Commit** : `feat: index-based selection system for virtualized MGListBox`

### Tâche 12 — Recycling pool et templates
- **Fichier** : `MGUI.Core/UI/MGListBox.cs`, `MGUI.Core/UI/Containers/VirtualizingStackPanel.cs`
- **Description** : Implémenter le mécanisme de recyclage des éléments UI.
- **Solution** :
  ```csharp
  // Quand un item sort du viewport :
  void RecycleItem(MGListBoxItem item)
  {
      item.ContentPresenter.Visibility = Visibility.Collapsed;
      _recyclePool.Enqueue(item);
      _realizedItems.Remove(item.LogicalIndex);
  }
  
  // Quand un item entre dans le viewport :
  MGListBoxItem RealizeItem(int logicalIndex, TItemType data)
  {
      MGListBoxItem item;
      if (_recyclePool.TryDequeue(out var recycled))
      {
          item = recycled;
          item.UpdateData(data);  // Rebind avec les nouvelles données
          item.ContentPresenter.Visibility = Visibility.Visible;
      }
      else
      {
          item = new MGListBoxItem(this, data);  // Créer seulement si pool vide
      }
      item.LogicalIndex = logicalIndex;
      _realizedItems[logicalIndex] = item;
      return item;
  }
  ```
- **Points d'attention** :
  - Le `ItemTemplate` doit pouvoir rebinder un élément existant avec de nouvelles données
  - Les `DataBindings` doivent être mis à jour proprement lors du recyclage
  - Buffer ~5 items supplémentaires au-dessus et en dessous du viewport pour le scroll smooth
- **Commit** : `feat: implement item recycling pool for virtualized ListBox`

---

## Phase 4 — Optimisations avancées

### Tâche 13 — Layout incrémental pour le StackPanel virtualisé
- **Fichier** : `MGUI.Core/UI/Containers/VirtualizingStackPanel.cs`
- **Problème** : Le layout complet de 5800 items n'est plus nécessaire, mais il faut calculer les positions correctes.
- **Solution** :
  - Mode hauteur uniforme : position = index × hauteur. Aucun layout nécessaire pour les items hors viewport.
  - Mode hauteur variable (futur) : maintenir un tableau de hauteurs mesurées. Utiliser une hauteur estimée pour les items jamais réalisés. Recalculer uniquement quand un item est réalisé et que sa hauteur diffère de l'estimation.
  - Le `ScrollViewer` utilise la hauteur totale calculée (somme estimée) pour le scrollbar.
- **Commit** : `perf: implement incremental layout for VirtualizingStackPanel`

### Tâche 14 — Dirty flags et invalidation ciblée
- **Fichier** : `MGUI.Core/UI/MGElement.cs`
- **Problème** : Actuellement, layout et visual state sont recalculés chaque frame pour tous les éléments, même si rien n'a changé.
- **Solution** :
  - Ajouter un système de dirty flags : `_isLayoutDirty`, `_isVisualStateDirty`, `_isInputDirty`
  - Ne recalculer le layout que si les bounds ou le contenu ont changé
  - Ne recalculer le VisualState que si l'état hover/pressed/focus a changé
  - Propager l'invalidation vers le haut (parent → ancêtres) quand un enfant est dirty
  - **Attention** : changement architectural significatif, nécessite des tests approfondis
- **Commit** : `perf: add dirty flag system for targeted invalidation in MGElement`

### Tâche 15 — Optimiser le draw avec culling dans MGScrollViewer
- **Fichier** : `MGUI.Core/UI/MGScrollViewer.cs`
- **Problème** : Même si le scissor rectangle clippe visuellement, le draw call est quand même émis pour les 5800 éléments. Le GPU fait le clippage, mais les draw calls CPU sont gaspillés.
- **Solution** :
  - Avant d'appeler `base.DrawContents`, filtrer les enfants dont les bounds intersectent le viewport
  - Utiliser les bounds pré-calculés des enfants pour un culling rapide
  - Avec la virtualisation, ce point sera résolu automatiquement (seuls les items réalisés sont dessinés)
- **Commit** : `perf: add CPU-side frustum culling in MGScrollViewer draw`

### Tâche 16 — Optimiser GetVisualTreeChildren
- **Fichier** : `MGUI.Core/UI/MGElement.cs`
- **Problème** : `GetVisualTreeChildren` est une méthode `yield return` appelée dans des hot paths. Chaque appel crée un iterator.
- **Solution** :
  - Cacher la liste des visual tree children et l'invalider uniquement quand des enfants sont ajoutés/supprimés
  - Retourner `IReadOnlyList<MGElement>` au lieu de `IEnumerable<MGElement>`
  - Les boucles `foreach` sur `IReadOnlyList` sont optimisées par le compilateur (pas d'allocation d'enumerator pour `List<T>`)
- **Commit** : `perf: cache GetVisualTreeChildren results to avoid iterator allocations`

### Tâche 17 — Profiling et validation
- **Description** : Créer un scénario de test de performance reproductible.
- **Action** :
  - Créer un sample `PerformanceTest.xaml` / `PerformanceTest.xaml.cs` dans `MGUI.Samples/Features/`
  - Instancier une `MGListBox` avec 5000, 10000, et 50000 éléments
  - Mesurer et afficher le FPS, le temps de frame (Update + Draw), la mémoire utilisée
  - Comparer les métriques avant/après virtualisation
  - Afficher un overlay de debug avec :
    - Nombre d'éléments total vs réalisés
    - Temps passé dans `Update`, `ComputeTopmostHoveredElement`, `Draw`
    - Nombre de `MouseHandler.ManualUpdate()` invoqués par frame
- **Commit** : `test: add performance benchmark sample for large ListBox`

---

## Résumé de l'impact attendu

| Métrique | Avant | Après (estimé) |
|----------|-------|-----------------|
| Éléments UI en mémoire | ~17400 (5800 × 3) | ~90 (30 × 3) |
| `Update()` calls par frame | ~17400 | ~90 |
| `ComputeTopmostHoveredElement` traversée | ~17400 nœuds | ~90 nœuds |
| `MouseHandler.ManualUpdate()` par frame | ~5800 | ~1 (consolidé) |
| Allocations LINQ par frame | milliers | 0 |
| Recherche item pressé | O(n) scan | O(1) calcul |
| Layout pass | 5800 éléments | ~30 éléments |
| Draw calls | 5800+ | ~30 |
