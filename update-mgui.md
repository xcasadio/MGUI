# Mise à jour MGUI — Plan de tâches pour agent IA

## Contexte

Ajouter 3 fonctionnalités manquantes dans le framework MGUI pour supporter la création de contrôles complexes (Content Browser, éditeurs, etc.) :

1. **Framework Drag-and-Drop générique** — abstraction haut niveau au-dessus des événements `MouseHandler.DragStart/Dragged/DragEnd`
2. **Navigation clavier** dans `MGTreeView`, `MGListBox`, `MGListView` — navigation aux flèches, sélection, focus
3. **Tri des colonnes** dans `MGListView` — tri au clic sur les headers de colonnes

> **Règle : l'agent doit commiter après chaque tâche complétée.**

### État actuel du code

**Drag-and-Drop** :
- `MouseHandler` (dans `MGUI.Shared/Input/Mouse/`) fournit les événements bas niveau : `DragStart`, `Dragged`, `DragEnd`, `DragStartOutside`
- `BaseMouseDragStartEventArgs` contient `Position`, `Button`, `Condition` — **pas de payload/DragData**
- `BaseMouseDraggedEventArgs` contient `Position`, `PositionDelta`, `StartPosition`, `GetTotalDistanceMoved()`
- `BaseMouseDragEndEventArgs` contient `StartPosition`, `EndPosition`, `PositionDelta`
- Le système de docking a sa propre implémentation DnD (`DockDragData`, `DockDropTarget`, `DockDropCalculator`) qui fonctionne par **polling dans `UpdateSelf()`**, pas purement event-driven
- `MGListBox.PressedItem` / `ReleasedItem` sont documentés comme "useful for manually implementing drag-drop behavior"
- Aucune abstraction générique n'existe (pas de `DragEnter`/`DragOver`/`DragLeave`/`Drop` sur les éléments)

**Navigation clavier** :
- `KeyboardHandler` dans `MGUI.Shared/Input/Keyboard/` fournit les événements `Pressed`, `Released`, `Clicked`
- Les événements clavier ne sont délivrés que si `Owner.CanReceiveKeyboardInput() && Owner.HasKeyboardFocus()`
- `MGElement.CanHandleKeyboardInput` retourne `false` par défaut — seuls `MGTextBox` et `MGPasswordBox` le surchargent à `true`
- `MGDesktop.FocusedKeyboardHandler` — un seul élément focusé sur tout le desktop
- `MGDesktop.QueuedFocusedKeyboardHandler` — les éléments set cette propriété, le desktop l'applique en fin de tick
- `MGTreeView` a déjà `GetNextVisibleItem()` et `GetPreviousVisibleItem()` + `_VisibleItemsCache` — parfait pour la navigation Up/Down
- `MGListBox` a `SelectedItems`, `SelectItem()`, `ClearSelection()`, `SelectionMode` (None/Single/Contiguous/Multiple) et des index logiques par item
- **Aucun contrôle data (TreeView, ListBox, ListView) ne supporte le clavier actuellement**

**Tri des colonnes** :
- `MGListView<T>` stocke les colonnes dans `List<MGListViewColumn<T>> _Columns`
- `MGListViewColumn<T>` a un `Header` (MGElement) et un `CellTemplate` (Func<T, MGElement>)
- Le header est placé dans `HeaderGrid` (MGGrid) — **aucun événement de clic n'est câblé**
- Les items sont dans `ObservableCollection<MGListViewItem<T>> InternalRowItems` alimenté par `ItemsSource`
- Les lignes sont ajoutées au `DataGrid` (MGGrid) directement
- **Aucun support de tri, aucun indicateur visuel de direction**

---

## Tâches

### Tâche 1 — Enum `SortDirection` et infrastructure de base

**Objectif** : Ajouter l'enum de direction de tri et les types de base nécessaires pour les tâches suivantes.

**Fichiers à modifier** :
- [MGUI.Core/UI/Enums.cs](MGUI.Core/UI/Enums.cs)

**Actions** :
1. Ajouter l'enum `SortDirection` dans `Enums.cs` (namespace `MGUI.Core.UI`) :
   ```csharp
   /// <summary>Specifies the direction of a sort operation.</summary>
   public enum SortDirection
   {
       /// <summary>Sort in ascending order (A→Z, 0→9).</summary>
       Ascending,
       /// <summary>Sort in descending order (Z→A, 9→0).</summary>
       Descending
   }
   ```
2. Placer l'enum après `CoordinateSpace` et avant `WindowStyle` dans le fichier

**Tests** : Aucun test nécessaire (simple enum).

**Commit** : `feat(mgui): add SortDirection enum`

---

### Tâche 2 — Focus management sur `MGElement`

**Objectif** : Permettre aux contrôles de données (TreeView, ListBox, ListView) de recevoir le focus clavier sans être des contrôles de saisie de texte.

**Fichiers à modifier** :
- [MGUI.Core/UI/MGElement.cs](MGUI.Core/UI/MGElement.cs)

**Actions** :
1. Localiser la propriété `CanHandleKeyboardInput` (ligne ~863) dont la valeur par défaut est `false`
2. Ajouter une propriété `IsFocusable` qui sera utilisée par les contrôles de données :
   ```csharp
   /// <summary>If true, this element can receive keyboard focus when clicked, 
   /// without necessarily being a text input control.<br/>
   /// Controls like <see cref="MGTreeView"/>, <see cref="MGListBox{TItemType}"/>, 
   /// and <see cref="MGListView{TItemType}"/> override this to enable keyboard navigation.</summary>
   public virtual bool IsFocusable { get; set; } = false;
   ```
3. Modifier la propriété `CanHandleKeyboardInput` (ou la logique dans `IKeyboardHandlerHost.CanReceiveKeyboardInput()`) pour aussi retourner `true` quand `IsFocusable` est `true` :
   ```csharp
   bool IKeyboardHandlerHost.CanReceiveKeyboardInput() 
       => (CanHandleKeyboardInput || IsFocusable) && _CanReceiveKeyboardInput;
   ```
4. Ajouter une méthode `Focus()` publique :
   ```csharp
   /// <summary>Requests keyboard focus for this element. 
   /// Focus will be applied at the end of the current update tick.</summary>
   public void Focus()
   {
       if (IsFocusable || CanHandleKeyboardInput)
           GetDesktop().QueuedFocusedKeyboardHandler = this;
   }
   ```
5. Sur le `MouseHandler.LMBPressedInside` de tout `MGElement` qui est `IsFocusable`, appeler `Focus()`. Ceci peut être fait en modifiant la logique existante dans la méthode `Update()` ou en ajoutant un handler dans le constructeur qui vérifie `IsFocusable`.

**Tests** :
- Vérifier qu'un élément avec `IsFocusable = true` peut recevoir le focus
- Vérifier qu'un élément avec `IsFocusable = false` (défaut) ne peut pas recevoir le focus
- Vérifier que `Focus()` met bien `QueuedFocusedKeyboardHandler`

**Commit** : `feat(mgui): add IsFocusable and Focus() to MGElement for keyboard navigation`

---

### Tâche 3 — Navigation clavier dans `MGTreeView`

**Objectif** : Permettre la navigation clavier complète dans le TreeView.

**Fichiers à modifier** :
- [MGUI.Core/UI/MGTreeView.cs](MGUI.Core/UI/MGTreeView.cs)
- [MGUI.Core/UI/MGTreeViewItem.cs](MGUI.Core/UI/MGTreeViewItem.cs)

**Actions** :
1. Dans `MGTreeView`, surcharger `IsFocusable` pour retourner `true` par défaut :
   ```csharp
   public override bool IsFocusable { get; set; } = true;
   ```
2. Dans le constructeur de `MGTreeView`, s'abonner aux événements clavier via `KeyboardHandler.Pressed` :
   ```csharp
   KeyboardHandler.Pressed += (sender, e) =>
   {
       if (e.IsHandled || SelectedItem == null)
           return;
       
       switch (e.Key)
       {
           case Keys.Up:
               var prev = GetPreviousVisibleItem(SelectedItem);
               if (prev != null) { SelectItem(prev); ScrollIntoView(prev); }
               e.SetHandledBy(this, true);
               break;
           case Keys.Down:
               var next = GetNextVisibleItem(SelectedItem);
               if (next != null) { SelectItem(next); ScrollIntoView(next); }
               e.SetHandledBy(this, true);
               break;
           case Keys.Right:
               if (!SelectedItem.IsExpanded)
                   SelectedItem.Expand();
               else
               {
                   var firstChild = SelectedItem.Items.FirstOrDefault();
                   if (firstChild != null) { SelectItem(firstChild); ScrollIntoView(firstChild); }
               }
               e.SetHandledBy(this, true);
               break;
           case Keys.Left:
               if (SelectedItem.IsExpanded)
                   SelectedItem.Collapse();
               else if (SelectedItem.ParentItem != null)
               {
                   SelectItem(SelectedItem.ParentItem);
                   ScrollIntoView(SelectedItem.ParentItem);
               }
               e.SetHandledBy(this, true);
               break;
           case Keys.Home:
               var first = _VisibleItemsCache?.FirstOrDefault();
               if (first != null) { SelectItem(first); ScrollIntoView(first); }
               e.SetHandledBy(this, true);
               break;
           case Keys.End:
               var last = _VisibleItemsCache?.LastOrDefault();
               if (last != null) { SelectItem(last); ScrollIntoView(last); }
               e.SetHandledBy(this, true);
               break;
           case Keys.Space:
           case Keys.Enter:
               SelectedItem.ToggleExpansion();
               e.SetHandledBy(this, true);
               break;
       }
   };
   ```
3. Quand un `MGTreeViewItem` est cliqué (dans `OnHeaderPanelClick`), appeler `Focus()` sur le `MGTreeView` parent pour capturer le focus clavier
4. Si `SelectedItem` est `null` et qu'une touche de navigation est pressée, sélectionner le premier item visible
5. S'assurer que `ScrollIntoView()` est appelé après chaque changement de sélection par clavier

**Tests** :
- Créer un TreeView avec plusieurs items imbriqués
- Vérifier que Up/Down navigue correctement entre les items visibles
- Vérifier que Right développe un item réduit
- Vérifier que Right sur un item développé navigue au premier enfant
- Vérifier que Left réduit un item développé
- Vérifier que Left sur un item réduit navigue au parent
- Vérifier Home/End pour premier/dernier item
- Vérifier Enter/Space toggle l'expansion

**Commit** : `feat(mgui): add keyboard navigation to MGTreeView`

---

### Tâche 4 — Navigation clavier dans `MGListBox`

**Objectif** : Permettre la navigation clavier dans le ListBox.

**Fichiers à modifier** :
- [MGUI.Core/UI/MGListBox.cs](MGUI.Core/UI/MGListBox.cs)

**Actions** :
1. Surcharger `IsFocusable` pour retourner `true` par défaut
2. Ajouter une propriété `FocusedIndex` (int, -1 si aucun) pour tracker l'item ayant le focus visuel :
   ```csharp
   private int _FocusedIndex = -1;
   public int FocusedIndex
   {
       get => _FocusedIndex;
       set
       {
           int itemCount = /* InternalItems.Count ou logicalItemsList.Count */;
           int clamped = Math.Clamp(value, -1, itemCount - 1);
           if (_FocusedIndex != clamped)
           {
               _FocusedIndex = clamped;
               NPC(nameof(FocusedIndex));
           }
       }
   }
   ```
3. S'abonner aux événements clavier dans le constructeur :
   ```csharp
   KeyboardHandler.Pressed += (sender, e) =>
   {
       if (e.IsHandled) return;
       
       switch (e.Key)
       {
           case Keys.Up:
               NavigateItem(-1, e);
               break;
           case Keys.Down:
               NavigateItem(+1, e);
               break;
           case Keys.Home:
               NavigateToIndex(0, e);
               break;
           case Keys.End:
               NavigateToIndex(ItemCount - 1, e);
               break;
           case Keys.Space:
           case Keys.Enter:
               if (FocusedIndex >= 0)
                   SelectItemAtIndex(FocusedIndex);
               e.SetHandledBy(this, true);
               break;
           case Keys.A:
               if (InputTracker.Keyboard.IsControlDown && SelectionMode == ListBoxSelectionMode.Multiple)
               {
                   SelectAll();
                   e.SetHandledBy(this, true);
               }
               break;
       }
   };
   ```
4. Implémenter `NavigateItem(int delta, BaseKeyPressedEventArgs e)` :
   - Calculer le nouvel index : `FocusedIndex + delta`, clamper entre 0 et count-1
   - Mettre à jour `FocusedIndex`
   - Si Shift n'est pas pressé et `SelectionMode != Multiple` : `ClearSelection()` puis sélectionner l'item au nouvel index
   - Si Shift est pressé et `SelectionMode` est `Contiguous` ou `Multiple` : étendre la sélection
   - Scroll pour rendre l'item visible
   - Marquer l'événement comme handled
5. Ajouter un feedback visuel pour l'item focusé (bordure en pointillés ou highlight discret différent de la sélection). Modifier `ItemContainerStyle` ou le rendu de l'item wrapper pour afficher un indicateur de focus quand `LogicalIndex == FocusedIndex`
6. Quand un item est cliqué à la souris, mettre `FocusedIndex` à l'index de l'item cliqué et appeler `Focus()`
7. Ajouter `SelectAll()` comme méthode publique si elle n'existe pas

**Tests** :
- Vérifier Up/Down navigue entre les items
- Vérifier que la sélection suit le focus en mode Single
- Vérifier Shift+Down étend la sélection en mode Multiple/Contiguous
- Vérifier Home/End pour premier/dernier item
- Vérifier Ctrl+A sélectionne tout en mode Multiple
- Vérifier Enter/Space sélectionne l'item focusé

**Commit** : `feat(mgui): add keyboard navigation to MGListBox`

---

### Tâche 5 — Navigation clavier dans `MGListView`

**Objectif** : Permettre la navigation clavier dans le ListView (navigation par lignes).

**Fichiers à modifier** :
- [MGUI.Core/UI/MGListView.cs](MGUI.Core/UI/MGListView.cs)

**Actions** :
1. Surcharger `IsFocusable` pour retourner `true` par défaut
2. Ajouter `FocusedRowIndex` (int, -1 si aucun)
3. S'abonner aux événements clavier dans le constructeur :
   - `Keys.Up` : déplacer `FocusedRowIndex` de -1
   - `Keys.Down` : déplacer `FocusedRowIndex` de +1
   - `Keys.Home` : aller à la première ligne
   - `Keys.End` : aller à la dernière ligne
   - `Keys.PageUp` : remonter d'une page (calculer via la hauteur visible / hauteur de ligne)
   - `Keys.PageDown` : descendre d'une page
   - `Keys.Enter` / `Keys.Space` : sélectionner la ligne focusée
   - `Keys.A` + Ctrl : tout sélectionner
4. Mettre à jour la sélection du `DataGrid` quand `FocusedRowIndex` change
5. Scroll automatiquement pour garder la ligne focusée visible
6. Quand une ligne est cliquée à la souris, mettre à jour `FocusedRowIndex` et appeler `Focus()`
7. Ajouter un feedback visuel pour la ligne focusée (bordure ou highlight)

**Tests** :
- Vérifier Up/Down navigue entre les lignes
- Vérifier Home/End/PageUp/PageDown
- Vérifier que le scroll suit la navigation
- Vérifier la sélection au clavier

**Commit** : `feat(mgui): add keyboard navigation to MGListView`

---

### Tâche 6 — Tri des colonnes dans `MGListView` : infrastructure

**Objectif** : Ajouter le support du tri dans `MGListViewColumn` et `MGListView`.

**Fichiers à modifier** :
- [MGUI.Core/UI/MGListView.cs](MGUI.Core/UI/MGListView.cs)

**Actions** :
1. Ajouter dans `MGListViewColumn<TItemType>` :
   ```csharp
   /// <summary>Function that extracts the sort key from an item. If null, the column is not sortable.</summary>
   public Func<TItemType, IComparable> SortKeySelector { get; set; }
   
   /// <summary>Whether this column supports sorting by clicking the header.</summary>
   public bool IsSortable => SortKeySelector != null;
   
   /// <summary>Current sort direction of this column, or null if not being sorted.</summary>
   public SortDirection? CurrentSortDirection
   {
       get => _CurrentSortDirection;
       internal set
       {
           if (_CurrentSortDirection != value)
           {
               _CurrentSortDirection = value;
               NPC(nameof(CurrentSortDirection));
               UpdateSortIndicator();
           }
       }
   }
   private SortDirection? _CurrentSortDirection;
   ```
2. Ajouter un paramètre optionnel `Func<TItemType, IComparable> sortKeySelector = null` à la méthode `AddColumn()` de `MGListView`
3. Câbler l'événement clic sur le header de la colonne :
   ```csharp
   // Dans le setter de Header dans MGListViewColumn, ou dans AddColumn :
   if (Header != null)
   {
       Header.MouseHandler.LMBClickedInside += (sender, e) =>
       {
           if (IsSortable && !e.IsHandled)
           {
               ListView.SortByColumn(this);
               e.SetHandledBy(Header, true);
           }
       };
       // Changer le curseur au survol si sortable
   }
   ```
4. Ajouter un indicateur visuel de tri dans `UpdateSortIndicator()` :
   - Ajouter un `MGTextBlock` (▲ ou ▼) à côté du header
   - Le wrapper du header doit être un `MGDockPanel` avec le contenu original + l'indicateur docké à droite
   - Masquer l'indicateur quand `CurrentSortDirection` est null
5. Ajouter dans `MGListView<TItemType>` :
   ```csharp
   /// <summary>Sorts the items by the specified column. 
   /// Toggles between Ascending and Descending if the column is already sorted.</summary>
   public void SortByColumn(MGListViewColumn<TItemType> column)
   {
       if (!column.IsSortable) return;
       
       // Clear sort on all other columns
       foreach (var col in _Columns)
           if (col != column) col.CurrentSortDirection = null;
       
       // Toggle direction
       column.CurrentSortDirection = column.CurrentSortDirection == SortDirection.Ascending
           ? SortDirection.Descending
           : SortDirection.Ascending;
       
       ApplySort();
   }
   
   /// <summary>Raised when a column sort changes.</summary>
   public event EventHandler<ColumnSortChangedEventArgs<TItemType>> ColumnSortChanged;
   ```
6. Créer `ColumnSortChangedEventArgs<T>` :
   ```csharp
   public class ColumnSortChangedEventArgs<TItemType> : EventArgs
   {
       public MGListViewColumn<TItemType> Column { get; }
       public SortDirection Direction { get; }
       public ColumnSortChangedEventArgs(MGListViewColumn<TItemType> column, SortDirection direction)
       {
           Column = column;
           Direction = direction;
       }
   }
   ```

**Tests** :
- Vérifier qu'un clic sur un header sortable déclenche le tri
- Vérifier que la direction alterne (Ascending → Descending → Ascending)
- Vérifier que cliquer sur une colonne différente reset la direction de l'ancienne
- Vérifier que les colonnes non-sortables (SortKeySelector = null) ne réagissent pas au clic

**Commit** : `feat(mgui): add column sort infrastructure to MGListView`

---

### Tâche 7 — Tri des colonnes dans `MGListView` : exécution du tri

**Objectif** : Implémenter le tri effectif des lignes dans le `DataGrid`.

**Fichiers à modifier** :
- [MGUI.Core/UI/MGListView.cs](MGUI.Core/UI/MGListView.cs)

**Actions** :
1. Implémenter `ApplySort()` dans `MGListView<TItemType>` :
   ```csharp
   private void ApplySort()
   {
       var sortedColumn = _Columns.FirstOrDefault(c => c.CurrentSortDirection.HasValue);
       if (sortedColumn == null)
       {
           // Restaurer l'ordre original
           RestoreOriginalOrder();
           return;
       }
       
       var direction = sortedColumn.CurrentSortDirection.Value;
       var keySelector = sortedColumn.SortKeySelector;
       
       // Trier les items
       var sortedItems = direction == SortDirection.Ascending
           ? InternalRowItems.OrderBy(item => keySelector(item.Data))
           : InternalRowItems.OrderByDescending(item => keySelector(item.Data));
       
       // Réorganiser les lignes du DataGrid
       ReorderRows(sortedItems.ToList());
       
       ColumnSortChanged?.Invoke(this, 
           new ColumnSortChangedEventArgs<TItemType>(sortedColumn, direction));
   }
   ```
2. Implémenter `ReorderRows(List<MGListViewItem<TItemType>> sortedItems)` :
   - Supprimer toutes les lignes du `DataGrid` (temporairement avec `AllowChangingContentTemporarily()`)
   - Recréer les `RowDefinition` et remettre les cellules dans le nouvel ordre
   - Ou, plus simplement, vider et reconstruire le DataGrid en réutilisant les `MGListViewItem` existants
3. Maintenir une copie de l'ordre original (`_OriginalOrder`) pour pouvoir le restaurer si le tri est annulé
4. Quand `ItemsSource` change (ajout/suppression d'items), réappliquer le tri actif :
   - S'abonner au `CollectionChanged` de `ItemsSource`
   - Sur Add/Remove/Reset → rappeler `ApplySort()` si un tri est actif
5. Ajouter une méthode publique `ClearSort()` pour supprimer le tri :
   ```csharp
   public void ClearSort()
   {
       foreach (var col in _Columns)
           col.CurrentSortDirection = null;
       RestoreOriginalOrder();
   }
   ```

**Tests** :
- Créer un ListView avec des données non triées
- Trier par une colonne ascendante → vérifier l'ordre des lignes
- Trier par la même colonne → vérifier l'inversion (descendant)
- Ajouter un item pendant un tri actif → vérifier qu'il est inséré à la bonne position
- Appeler `ClearSort()` → vérifier le retour à l'ordre original
- Trier par des types différents (string, int, DateTime)

**Commit** : `feat(mgui): implement column sort execution in MGListView`

---

### Tâche 8 — Framework Drag-and-Drop : types de base

**Objectif** : Créer les types et interfaces de base pour le système de drag-and-drop générique.

**Fichiers à créer** :
- `MGUI.Core/UI/DragDrop/DragDropData.cs`
- `MGUI.Core/UI/DragDrop/DragDropEventArgs.cs`
- `MGUI.Core/UI/DragDrop/IDropTarget.cs`
- `MGUI.Core/UI/DragDrop/DragDropEffect.cs`

**Actions** :
1. Créer `DragDropEffect` enum :
   ```csharp
   namespace MGUI.Core.UI.DragDrop
   {
       /// <summary>Specifies the effects of a drag-and-drop operation.</summary>
       [Flags]
       public enum DragDropEffect
       {
           /// <summary>The drop target does not accept the data.</summary>
           None = 0,
           /// <summary>The data is moved from the drag source to the drop target.</summary>
           Move = 1,
           /// <summary>The data is copied to the drop target.</summary>
           Copy = 2,
           /// <summary>Move or Copy, determined by modifier keys (Ctrl = Copy).</summary>
           MoveOrCopy = Move | Copy
       }
   }
   ```
2. Créer `DragDropData` :
   ```csharp
   namespace MGUI.Core.UI.DragDrop
   {
       /// <summary>Encapsulates the data being dragged during a drag-and-drop operation.</summary>
       public class DragDropData
       {
           /// <summary>The element that initiated the drag.</summary>
           public MGElement Source { get; }
           /// <summary>The data payload being dragged. Can be any object.</summary>
           public object Data { get; }
           /// <summary>A string identifying the type/format of the data (e.g., "ContentItem", "TreeNode").</summary>
           public string Format { get; }
           /// <summary>Allowed effects for this drag operation.</summary>
           public DragDropEffect AllowedEffects { get; }
           /// <summary>The visual element to display as the drag ghost. If null, a default ghost is created.</summary>
           public MGElement DragGhost { get; set; }
           /// <summary>Offset of the ghost relative to the cursor.</summary>
           public Point GhostOffset { get; set; }
           
           public DragDropData(MGElement source, object data, string format, 
               DragDropEffect allowedEffects = DragDropEffect.MoveOrCopy) { ... }
       }
   }
   ```
3. Créer `DragDropEventArgs` (plusieurs classes) :
   ```csharp
   namespace MGUI.Core.UI.DragDrop
   {
       public class DragEventArgs : EventArgs
       {
           public DragDropData Data { get; }
           public Point Position { get; }
           public DragDropEffect Effect { get; set; }
           /// <summary>Set to true to indicate that this drop target accepts the data.</summary>
           public bool Handled { get; set; }
           
           public DragEventArgs(DragDropData data, Point position) { ... }
       }
       
       public class DragOverEventArgs : DragEventArgs
       {
           /// <summary>Set to true to show a visual indicator that this is a valid drop target.</summary>
           public bool ShowDropIndicator { get; set; }
           public DragOverEventArgs(DragDropData data, Point position) : base(data, position) { }
       }
       
       public class DropEventArgs : DragEventArgs
       {
           public DragDropEffect FinalEffect { get; set; }
           public DropEventArgs(DragDropData data, Point position) : base(data, position) { }
       }
   }
   ```
4. Créer `IDropTarget` interface :
   ```csharp
   namespace MGUI.Core.UI.DragDrop
   {
       /// <summary>Interface for elements that can accept dropped data.</summary>
       public interface IDropTarget
       {
           /// <summary>Called when a drag operation enters this element's bounds.</summary>
           void OnDragEnter(DragEventArgs e);
           /// <summary>Called continuously while dragging over this element.</summary>
           void OnDragOver(DragOverEventArgs e);
           /// <summary>Called when a drag operation leaves this element's bounds.</summary>
           void OnDragLeave(DragEventArgs e);
           /// <summary>Called when the data is dropped on this element.</summary>
           void OnDrop(DropEventArgs e);
       }
   }
   ```

**Tests** :
- Vérifier que `DragDropData` est correctement construit
- Vérifier les flags de `DragDropEffect`

**Commit** : `feat(mgui): add drag-and-drop base types`

---

### Tâche 9 — Framework Drag-and-Drop : `DragDropManager`

**Objectif** : Implémenter le gestionnaire central de drag-and-drop sur `MGDesktop`.

**Fichiers à créer** :
- `MGUI.Core/UI/DragDrop/DragDropManager.cs`

**Fichiers à modifier** :
- [MGUI.Core/UI/MGDesktop.cs](MGUI.Core/UI/MGDesktop.cs)

**Actions** :
1. Créer `DragDropManager` :
   ```csharp
   namespace MGUI.Core.UI.DragDrop
   {
       /// <summary>Manages the lifecycle of drag-and-drop operations across the desktop.</summary>
       public class DragDropManager
       {
           public MGDesktop Desktop { get; }
           
           /// <summary>Whether a drag operation is currently in progress.</summary>
           public bool IsDragging => CurrentDrag != null;
           
           /// <summary>The current drag data, or null if not dragging.</summary>
           public DragDropData CurrentDrag { get; private set; }
           
           /// <summary>The element currently under the cursor during a drag.</summary>
           public MGElement CurrentDropTarget { get; private set; }
           
           /// <summary>The last accepted drop effect.</summary>
           public DragDropEffect CurrentEffect { get; private set; }
           
           /// <summary>Starts a drag-and-drop operation.</summary>
           public void BeginDrag(DragDropData data, Point startPosition) { ... }
           
           /// <summary>Called each frame during a drag to update state.</summary>
           internal void Update(Point mousePosition, bool isLMBPressed, bool isEscPressed) { ... }
           
           /// <summary>Renders the drag ghost overlay.</summary>
           internal void Draw(SpriteBatch spriteBatch) { ... }
           
           /// <summary>Cancels the current drag operation.</summary>
           public void CancelDrag() { ... }
           
           /// <summary>Raised when a drag operation starts.</summary>
           public event EventHandler<DragEventArgs> DragStarted;
           /// <summary>Raised when a drag operation completes (drop or cancel).</summary>
           public event EventHandler<DropEventArgs> DragCompleted;
       }
   }
   ```
2. Implémenter `Update()` en suivant le pattern du docking system (polling) :
   - Si `!isLMBPressed` → exécuter le drop sur `CurrentDropTarget` (si valid) puis `EndDrag()`
   - Si `isEscPressed` → `CancelDrag()`
   - Hit-test `mousePosition` contre tous les éléments visibles pour trouver le premier `IDropTarget`
   - Si le target change : appeler `OnDragLeave` sur l'ancien, `OnDragEnter` sur le nouveau
   - Appeler `OnDragOver` sur le target courant
   - Mettre à jour la position du ghost
3. Implémenter `Draw()` :
   - Si `CurrentDrag?.DragGhost` exists → dessiner le ghost à la position de la souris + `GhostOffset`
   - Sinon, dessiner un rectangle semi-transparent par défaut avec le texte du format
4. Ajouter le hit-testing pour trouver les `IDropTarget` :
   - Parcourir l'arbre visuel (windows du desktop, puis enfants récursivement)
   - Trouver l'élément le plus profond sous la souris qui implémente `IDropTarget`
   - Utiliser `IsInside()` pour le test de bounds
5. Dans `MGDesktop` :
   - Ajouter `public DragDropManager DragDropManager { get; }` — instancié dans le constructeur
   - Appeler `DragDropManager.Update()` dans `UpdateSelf()` après la gestion de l'input souris
   - Appeler `DragDropManager.Draw()` dans `DrawSelf()` après les windows mais avant les tooltips/menus

**Tests** :
- Vérifier `BeginDrag` → `IsDragging == true`
- Vérifier `CancelDrag` → `IsDragging == false`
- Vérifier que `DragStarted` et `DragCompleted` sont levés
- Vérifier le lifecycle DragEnter/DragOver/DragLeave sur un mock IDropTarget

**Commit** : `feat(mgui): add DragDropManager to MGDesktop`

---

### Tâche 10 — Framework Drag-and-Drop : intégration sur `MGElement`

**Objectif** : Fournir une API simple sur `MGElement` pour initier et recevoir des drags.

**Fichiers à modifier** :
- [MGUI.Core/UI/MGElement.cs](MGUI.Core/UI/MGElement.cs)

**Actions** :
1. Ajouter des événements de drag-and-drop sur `MGElement` :
   ```csharp
   /// <summary>Raised when a drag operation enters this element's bounds. 
   /// Only fires if <see cref="AllowDrop"/> is true.</summary>
   public event EventHandler<DragEventArgs> DragEnter;
   
   /// <summary>Raised continuously while dragging over this element.
   /// Only fires if <see cref="AllowDrop"/> is true.</summary>
   public event EventHandler<DragOverEventArgs> DragOver;
   
   /// <summary>Raised when a drag operation leaves this element's bounds.
   /// Only fires if <see cref="AllowDrop"/> is true.</summary>
   public event EventHandler<DragEventArgs> DragLeave;
   
   /// <summary>Raised when data is dropped on this element.
   /// Only fires if <see cref="AllowDrop"/> is true.</summary>
   public event EventHandler<DropEventArgs> Drop;
   ```
2. Ajouter `AllowDrop` property :
   ```csharp
   /// <summary>If true, this element can receive drag-and-drop data.<br/>
   /// When true, the element will receive <see cref="DragEnter"/>, <see cref="DragOver"/>,
   /// <see cref="DragLeave"/>, and <see cref="Drop"/> events.</summary>
   public bool AllowDrop { get; set; } = false;
   ```
3. Faire implémenter `IDropTarget` implicitement par `MGElement` quand `AllowDrop == true` (le `DragDropManager` vérifie `AllowDrop` au lieu de l'interface) :
   ```csharp
   internal void RaiseDragEnter(DragEventArgs e) => DragEnter?.Invoke(this, e);
   internal void RaiseDragOver(DragOverEventArgs e) => DragOver?.Invoke(this, e);
   internal void RaiseDragLeave(DragEventArgs e) => DragLeave?.Invoke(this, e);
   internal void RaiseDrop(DropEventArgs e) => Drop?.Invoke(this, e);
   ```
4. Modifier le hit-testing dans `DragDropManager` pour chercher les éléments avec `AllowDrop == true` au lieu de `IDropTarget`
5. Ajouter une méthode helper pour initier facilement un drag :
   ```csharp
   /// <summary>Initiates a drag-and-drop operation from this element.</summary>
   public void DoDragDrop(object data, string format, 
       DragDropEffect allowedEffects = DragDropEffect.MoveOrCopy, 
       MGElement dragGhost = null)
   {
       var dragData = new DragDropData(this, data, format, allowedEffects)
       {
           DragGhost = dragGhost
       };
       GetDesktop().DragDropManager.BeginDrag(dragData, InputTracker.Mouse.CurrentPosition);
   }
   ```
6. Conserver `IDropTarget` comme interface optionnelle pour les contrôles MGUI internes qui veulent un handling plus poussé

**Tests** :
- Créer un élément source avec `DoDragDrop()` 
- Créer un élément cible avec `AllowDrop = true`
- Vérifier que les événements DragEnter/DragOver/DragLeave/Drop sont levés dans le bon ordre
- Vérifier que les éléments sans `AllowDrop` ne reçoivent pas d'événements

**Commit** : `feat(mgui): add drag-and-drop events and API to MGElement`

---

### Tâche 11 — Tests d'intégration et documentation

**Objectif** : Écrire des tests d'intégration complets et documenter les 3 nouvelles fonctionnalités.

**Fichiers à créer/modifier** :
- `MGUI.Tests/KeyboardNavigationTests.cs`
- `MGUI.Tests/ColumnSortTests.cs`
- `MGUI.Tests/DragDropTests.cs`

**Actions** :
1. **Tests navigation clavier** :
   - TreeView : navigation complète Up/Down/Left/Right/Home/End/Enter sur une arborescence à 3 niveaux
   - ListBox : navigation Up/Down/Home/End, multi-sélection avec Shift, Ctrl+A
   - ListView : navigation lignes Up/Down/PageUp/PageDown
2. **Tests tri colonnes** :
   - Tri ascendant/descendant par string
   - Tri ascendant/descendant par nombre
   - Tri avec valeurs null
   - Changement de colonne de tri
   - Ajout d'item pendant tri actif
   - ClearSort()
3. **Tests drag-and-drop** :
   - Lifecycle complet : BeginDrag → DragEnter → DragOver → Drop
   - Cancel avec Echap : BeginDrag → CancelDrag → DragLeave
   - DragDropEffect (Move vs Copy)
   - Éléments sans AllowDrop ignorés
   - Drag ghost positionné correctement
4. Ajouter des commentaires XML de documentation sur toutes les nouvelles API publiques
5. S'assurer que tous les tests existants passent encore (pas de régression)

**Commit** : `test(mgui): add integration tests for keyboard nav, column sort, and drag-drop`

---

## Résumé de l'architecture des modifications

```
MGUI.Core/UI/
├── Enums.cs                          # + SortDirection enum
├── MGElement.cs                      # + IsFocusable, Focus(), AllowDrop, DragEnter/Over/Leave/Drop, DoDragDrop()
├── MGDesktop.cs                      # + DragDropManager property, Update/Draw calls
├── MGTreeView.cs                     # + keyboard navigation (Up/Down/Left/Right/Home/End/Enter)
├── MGListBox.cs                      # + keyboard navigation (Up/Down/Home/End/Enter, Ctrl+A, Shift+select)
├── MGListView.cs                     # + keyboard navigation + column sorting (SortByColumn, ClearSort)
└── DragDrop/                         # NOUVEAU DOSSIER
    ├── DragDropData.cs               # Payload du drag
    ├── DragDropEffect.cs             # Enum None/Move/Copy
    ├── DragDropEventArgs.cs          # DragEventArgs, DragOverEventArgs, DropEventArgs
    ├── DragDropManager.cs            # Gestionnaire central (lifecycle, hit-test, ghost)
    └── IDropTarget.cs                # Interface optionnelle pour drop targets avancés

MGUI.Tests/
├── KeyboardNavigationTests.cs        # Tests navigation clavier
├── ColumnSortTests.cs                # Tests tri colonnes
└── DragDropTests.cs                  # Tests drag-and-drop
```

## Ordre des dépendances

```
Tâche 1 (SortDirection enum)
  └→ Tâche 2 (Focus management sur MGElement)
       ├→ Tâche 3 (Keyboard nav TreeView)
       ├→ Tâche 4 (Keyboard nav ListBox)
       └→ Tâche 5 (Keyboard nav ListView)
            └→ Tâche 6 (Column sort infrastructure)
                 └→ Tâche 7 (Column sort execution)
  └→ Tâche 8 (DnD base types)
       └→ Tâche 9 (DragDropManager)
            └→ Tâche 10 (DnD events on MGElement)
                 └→ Tâche 11 (Tests d'intégration)
```

## Impact sur le Content Browser

Une fois ces tâches complétées dans MGUI, le Content Browser (`content-browser.md`) pourra directement utiliser :
- **Navigation clavier** : le TreeView de dossiers et les ListBox/ListView de fichiers répondront nativement aux flèches
- **Tri des colonnes** : la vue détaillée triera les fichiers au clic sur les headers (nom, taille, date, type) en passant un `SortKeySelector`
- **Drag-and-drop** : le déplacement de fichiers utilisera `DoDragDrop()` et `AllowDrop` + les événements `DragEnter`/`Drop` au lieu de câbler manuellement les événements souris bas niveau
