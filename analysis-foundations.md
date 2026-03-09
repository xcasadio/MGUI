# Analyse des fondations MGUI — Tâches pour agent IA

> Chaque tâche doit être commitée séparément après vérification.
> L'agent doit **uniquement vérifier, documenter et corriger** — ne pas inventer de fonctionnalité.

---

## 1. Vérifier la hiérarchie visuelle (Visual Tree)

**Fichiers :** `MGUI.Core/UI/MGElement.cs`, `MGUI.Core/UI/Containers/MGContentHost.cs`

**Ce qui existe :**
- `MGElement` est la classe de base abstraite de tous les éléments UI
- `Parent` / `SetParent()` — relation parent-enfant
- `ComponentParent` / `ManagedParent` — relations spéciales pour composants internes
- `GetChildren()` — retourne les enfants directs
- `TraverseVisualTree<T>()` — parcours récursif (Preorder/Postorder)
- `FindElement()`, `GetElements()`, `IsSelfOrAncestorOf()`, `TryFindParentOfType<T>()`
- `GetVisualTreeChildren()` avec cache (`_vtcCacheActiveOnly`, `InvalidateVtcCache()`)
- `MGContentHost` → `MGMultiContentHost` (ObservableCollection) / `MGSingleContentHost` (1 enfant)
- `MGContentPresenter`, `MGHeaderedContentPresenter`
- `OnContentAdded`, `OnContentRemoved`, `OnNestedContentAdded`, `OnNestedContentRemoved`

**Tâche :** Vérifier que `SetParent` est bien appelé dans tous les cas d'ajout/suppression d'enfants (dans `MGMultiContentHost._Children.CollectionChanged`, dans `MGSingleContentHost.SetContentVirtual`, dans `AddComponent`). Vérifier que le parent est bien mis à `null` lors de la suppression. Documenter le résultat dans un commentaire en haut de `MGContentHost.cs`.

**Commit :** `audit(visual-tree): verify parent-child consistency in content hosts`

---

## 2. Vérifier le système de layout (Measure / Arrange)

**Fichiers :** `MGUI.Core/UI/MGElement.cs` (sections `#region Layout`, `#region Measure`, `#region Arrange`)

**Ce qui existe :**
- `InvalidateLayout()` / `InvalidateLayoutTree()` — invalide les mesures en cache
- `LayoutChanged()` — propage l'invalidation vers le parent
- `UpdateMeasurement()` — mesure récursive (self + content), avec cache (`RecentMeasurementsFull`, `RecentMeasurementsSelfOnly`, `TryGetCachedMeasurement`)
- `MeasureSelf()` / `MeasureSelfOverride()` — mesure sans contenu
- `UpdateContentMeasurement()` — mesure du contenu (override dans les conteneurs)
- `UpdateLayout()` — arrangement (calcul de `AllocatedBounds`, `RenderBounds`, `LayoutBounds`, `StretchedContentBounds`, `AlignedContentBounds`)
- `UpdateContentLayout()` — arrangement du contenu (override dans les conteneurs)
- `Margin`, `Padding`, `MinWidth/Height`, `MaxWidth/Height`, `PreferredWidth/Height`
- `HorizontalAlignment`, `VerticalAlignment`, `HorizontalContentAlignment`, `VerticalContentAlignment`
- `IsLayoutValid`, `OnLayoutUpdated`, `OnLayoutBoundsChanged`
- `ElementMeasurement` avec cache de taille `MeasurementCacheSize = 15`
- `DeferEventsManager` / `BeginInitializing()` pour différer les invalidations de layout

**Tâche :** Vérifier que chaque conteneur (`MGStackPanel`, `MGDockPanel`, `MGGrid`, `MGOverlayPanel`) implémente bien `UpdateContentMeasurement` et `UpdateContentLayout`. Vérifier que `LayoutChanged` propage correctement vers le parent. Documenter les résultats.

**Commit :** `audit(layout): verify measure/arrange in all container types`

---

## 3. Vérifier la gestion des entrées (Input Handling)

**Fichiers :** `MGUI.Shared/Input/`, `MGUI.Core/UI/MGElement.cs` (section `#region Input`)

**Ce qui existe :**
- `InputTracker` — contient `MouseTracker` et `KeyboardTracker`
- `MouseHandler` — événements : `PressedInside/Outside`, `ReleasedInside/Outside`, `ClickedInside/Outside`, `Entered`, `Exited`, `MovedInside/Outside`, `Scrolled`, `DragStart/Dragged/DragEnd`
  - Gestion LMB/RMB/MMB spécifique (`LMBPressedInside`, `RMBReleasedInside`, etc.)
  - `DragStartCondition` (MousePressed / MouseMovedAfterPress)
  - `AlwaysHandlesEvents`, `InvokeEvenIfHandled`, `InvokeIfHandledBySelf`
  - `HasSubscribedEvents` avec flags de monitoring cachés (`_isMonitoringScroll`, etc.)
- `KeyboardHandler` — événements : `Pressed`, `Released`, `Clicked`
  - Conditionné par `CanReceiveKeyboardInput()` et `HasKeyboardFocus()`
- `HandledByEventArgs<T>` — pattern de gestion "handled" pour les événements
- `IsHitTestVisible` / `DerivedIsHitTestVisible` — contrôle de la réception des inputs
- `_CanReceiveMouseInput` / `_CanReceiveKeyboardInput` — flags calculés avec dirty-flag optimisation
- `IsLMBPressed`, `IsHovered` — états dérivés sur MGElement
- Hovered element : `GetTopmostHoveredElement()` / `ComputeTopmostHoveredElement()` — recherche récursive

**Tâche :** Vérifier que le bubbling d'input (via `HandledByEventArgs.SetHandledBy`) fonctionne correctement — qu'un événement handled par un enfant ne soit plus traité par le parent. Vérifier que `_CanReceiveMouseInput`/`_CanReceiveKeyboardInput` sont correctement recalculés quand `IsEnabled`, `IsHitTestVisible`, ou `Visibility` changent (via `_inputStateDirty`). Documenter les résultats.

**Commit :** `audit(input): verify input bubbling and dirty-flag recomputation`

---

## 4. Vérifier la gestion du focus clavier

**Fichiers :** `MGUI.Core/UI/MGDesktop.cs` (section `#region Keyboard Focus`), `MGUI.Core/UI/MGElement.cs`

**Ce qui existe :**
- `MGDesktop.FocusedKeyboardHandler` — l'élément qui a le focus clavier (1 seul à la fois)
- `MGDesktop.QueuedFocusedKeyboardHandler` — mise en file d'attente du focus (appliqué en fin d'Update)
- `MGElement.IsFocusable` — propriété virtuelle (défaut `false`), overridée dans TreeView, ListBox, ListView
- `MGElement.CanHandleKeyboardInput` — propriété virtuelle, retourne `IsFocusable` par défaut, overridée dans les TextBox
- `MGElement.Focus()` — demande le focus (queue sur Desktop)
- `FocusedKeyboardHandlerChanged` — événement de notification
- Le focus est réinitialisé (`QueuedFocusedKeyboardHandler = null`) sur chaque press souris via `HighPriorityMouseHandler`

**Ce qui manque (constaté) :**
- Pas de navigation Tab/Shift+Tab entre éléments focusables (pas de `TabIndex`, pas de `IsTabStop`)
- Pas d'indicateur visuel de focus (pas de `FocusVisualStyle` ni de focus ring)
- `IsFocusable` non utilisé dans le constructeur de `MGElement` pour auto-capturer le focus au clic (pas de handler LMBPressedInside dans MGElement de base)

**Tâche :** Vérifier que `Focus()` fonctionne correctement : qu'il met bien `QueuedFocusedKeyboardHandler`, que ce dernier est bien appliqué dans `MGDesktop.Update()`, et qu'un seul élément a le focus à la fois. Vérifier que les contrôles qui overrident `IsFocusable = true` appellent bien `Focus()` au clic. Documenter les lacunes trouvées (Tab navigation, focus visual) sans les implémenter.

**Commit :** `audit(focus): verify focus lifecycle and document missing features`

---

## 5. Vérifier le Data Binding et le pattern MVVM

**Fichiers :** `MGUI.Core/UI/Data Binding/`

**Ce qui existe :**
- `ViewModelBase : INotifyPropertyChanged` — classe de base avec `NPC()` / `AutoNPC()`
- `XAMLBindableBase : ViewModelBase` — supporte les bindings XAML, stocke `Bindings` temporairement
- `DataContext` / `DataContextOverride` — résolution de la source de données
  - `MGElement.DataContext` priorise `DataContextOverride`, fallback sur `MGWindow.WindowDataContext`
  - `IObservableDataContext` avec `DataContextChanged` event
- `DataBinding` — liaison active entre source et target, supporte `IValueConverter`
  - Modes : `OneTime`, `OneWay`, `OneWayToSource`, `TwoWay`
  - `HasError` / `LastError` pour diagnostic
- `DataBindingManager` — registry statique de tous les bindings
  - `AddBinding()`, `RemoveBinding()`, `RemoveBindings(object)`
- `MGBinding : MarkupExtension` — extension XAML pour déclarer les bindings
  - `Path`, `Mode`, `ElementName`, `ResourceName`, `Converter`, `ConverterParameter`, `FallbackValue`, `StringFormat`
  - `DataContextResolver` : `DataContext` ou `Self`
  - `SourceObjectResolver` : `FromSelf()`, `FromElementName()`, `FromResourceName()`
- `BindingConfig` — configuration immuable d'un binding (paths pointés, converter, etc.)
- `PropertyNameHandler` / `PropertyNameListener` — écoute des changements de propriété
- Processus de binding : XAML parsing → `Bindings` list → copie vers `MGElement.Metadata` → `ProcessBindings()` après parsing complet

**Ce qui est absent par rapport à WPF :**
- Pas de `DependencyProperty` — les propriétés utilisent des champs privés + `NPC()` (pattern INotifyPropertyChanged classique)
- Pas de `ICommand` — les boutons utilisent `CommandName` (string lookup dans `MGResources.Commands`) et/ou `Command` (delegate `Func<MGButton, BaseMouseClickedEventArgs, bool>`)
- Pas de `MultiBinding` ni `PriorityBinding`
- Pas de `RelativeSource` binding

**Tâche :** Vérifier que le cycle complet de data binding fonctionne : (1) le `DataContext` est bien propagé depuis `WindowDataContext` vers les enfants, (2) les changements de propriété source notifient correctement les targets, (3) `RemoveDataBindings()` nettoie proprement les listeners. Vérifier que `DataContextChanged` est bien invoqué quand `DataContextOverride` ou `WindowDataContext` changent. Documenter les résultats.

**Commit :** `audit(data-binding): verify binding lifecycle and DataContext propagation`

---

## 6. Vérifier le système de styles XAML

**Fichiers :** `MGUI.Core/UI/XAML/Style.cs`, `MGUI.Core/UI/XAML/XAMLParser.cs`, `MGUI.Core/UI/XAML/Element.cs`

**Ce qui existe :**
- `Style` — contient `TargetType` (MGElementType), `Name` (optionnel), `AffectsComponents`, et une liste de `Setter` (`Property` + `Value`)
- `Setter` — association propriété/valeur simple
- `XAMLParser` — parse le XAML, résout les namespaces, substitue les alias (`Button` → `MGUI:Button`)
- Les styles sont appliqués via `Element.ProcessStyles` dans le XAML middleman

**Ce qui manque par rapport à WPF :**
- Pas de `Trigger` / `DataTrigger` / `EventTrigger`
- Pas de `ControlTemplate` — les contrôles sont composés programmatiquement (pas de template XAML pour redéfinir la structure visuelle)
- Pas de `BasedOn` pour l'héritage de styles
- Pas de `TemplateBinding`

**Tâche :** Vérifier que les styles sont correctement appliqués : que les `Setter` mettent bien la valeur sur la bonne propriété, que `AffectsComponents` filtre correctement les composants internes, et que `Name` permet bien de cibler des éléments spécifiques. Documenter les résultats.

**Commit :** `audit(styles): verify style application and setter resolution`

---

## 7. Vérifier le Visual State et le rendu

**Fichiers :** `MGUI.Core/UI/VisualState.cs`, `MGUI.Core/UI/MGElement.cs` (sections Draw)

**Ce qui existe :**
- `PrimaryVisualState` : `Disabled`, `Selected`, `Normal` (priorité dans cet ordre)
- `SecondaryVisualState` : `Pressed`, `Hovered`, `None` (priorité dans cet ordre)
- `VisualState` — record struct, calculé dans `Update()` à chaque frame
- `VisualStateSetting<T>` — permet de définir des valeurs différentes par état (DisabledValue, SelectedValue, NormalValue)
- `VisualStateFillBrush` — brush conditionnel par état visuel (Underlay + Overlay par état primaire/secondaire)
- `BackgroundBrush` — `VisualStateFillBrush` pour le fond
- `OverlayBrush` — dessiné par-dessus
- `DefaultTextForeground` / `DerivedDefaultTextForeground` — couleur texte héritée dans l'arbre
- `RenderScale` / `ConditionalScaleTransform` — scale conditionnel (Pressed/Hovered)
- `Opacity` — opacité (composée avec parent)
- `Visibility` : `Visible`, `Hidden`, `Collapsed`
- `ClipToBounds` — clipping via ScissorRectangle
- `RecentDrawWasClipped` — tracking si l'élément a été clippé
- Pipeline de draw : `DrawBeforeBackground` → `DrawBackground` → `DrawBeforeSelf` → `DrawSelf` → `DrawBeforeContents` → `DrawContents` → `DrawAfterContents` → `OverlayBrush` → `OnEndingDraw`
- `SpoofIsPressedWhileDrawingBackground` / `SpoofIsHoveredWhileDrawingBackground` — override visuel

**Tâche :** Vérifier que le `VisualState` est correctement calculé dans `Update()` en fonction de `IsEnabled`, `IsSelected`, `IsLMBPressed`, `IsHovered`, et `IsHitTestVisible`. Vérifier que les composants héritent bien des bonnes priorités de dessin (`ComponentDrawPriority`). Documenter les résultats.

**Commit :** `audit(visual-state): verify state computation and draw pipeline order`

---

## 8. Vérifier le système de thèmes et ressources

**Fichiers :** `MGUI.Core/UI/MGTheme.cs`, `MGUI.Core/UI/MGResources.cs`, `MGUI.Core/UI/MGElementTemplate.cs`

**Ce qui existe :**
- `MGTheme` — définit les brushes par défaut par `MGElementType` (via `GetBackgroundBrush`)
  - `ThemeFontSettings` — tailles de police configurables
  - `ThemeManagedGetter<T>` — retourne des copies des valeurs pour éviter la mutation partagée
- `MGResources` — registry centralisé :
  - `Textures` (dictionnaire string → `MGTextureData`)
  - `Commands` (dictionnaire string → `Action<MGElement>`)
  - `StaticResources` (pour le databinding `ResourceName`)
  - `ElementTemplates` (dictionnaire string → `MGElementTemplate`)
  - `DefaultTheme` — le thème actif
- `MGElementTemplate` — factory d'éléments avec support `IsShared`
- `ContentTemplate` — utilisé en XAML pour générer du contenu via template ou inline

**Tâche :** Vérifier que `MGTheme.GetBackgroundBrush(MGElementType)` retourne un brush pour chaque type d'élément défini dans l'enum `MGElementType`. Vérifier que les ressources (`Textures`, `Commands`, `StaticResources`) sont correctement accessible via `MGElement.GetResources()`. Documenter les résultats.

**Commit :** `audit(theme-resources): verify theme coverage and resource accessibility`

---

## 9. Vérifier le ToolTip et le ContextMenu

**Fichiers :** `MGUI.Core/UI/MGDesktop.cs`, `MGUI.Core/UI/MGElement.cs`, `MGUI.Core/UI/MGToolTip.cs`, `MGUI.Core/UI/MGContextMenu.cs`

**Ce qui existe :**
- **ToolTip :** `MGElement.ToolTip`, `MGDesktop.ActiveToolTip`, `QueuedToolTip`, `ToolTipShowDelay`
  - Logique de hover time dans `Update()` — affichage après délai configurable
  - Events : `ToolTipOpened`, `ToolTipClosed`, `ToolTipOpening` (cancellable)
  - Mutual exclusion : un seul ToolTip actif à la fois
  - Fenêtres occultées ne peuvent pas overrider le ToolTip actif
- **ContextMenu :** `MGElement.ContextMenu`, `MGDesktop.ActiveContextMenu`
  - `TryOpenContextMenu()` / `TryCloseActiveContextMenu()` — avec events cancellables
  - `ContextMenuRequested` event — permet la construction dynamique du menu au clic droit
  - `ContextMenuOpening/Opened/Closing/Closed` events
  - Auto-positionnement via `FitMenuToViewport`
  - Menus imbriqués supportés (`TryCloseActiveContextMenu` ferme les nested)
  - `SyncContextMenuRmbHandler()` — auto-souscription au RMB release

**Tâche :** Vérifier le cycle de vie complet du ToolTip (hover → delay → show → leave → hide) et du ContextMenu (right-click → build → open → select → close). Vérifier que le `ContextMenuRequested` event permet bien de construire un menu dynamiquement sans assigner `ContextMenu`. Documenter les résultats.

**Commit :** `audit(tooltip-contextmenu): verify lifecycle and dynamic menu creation`

---

## 10. Vérifier le Drag and Drop

**Fichiers :** `MGUI.Core/UI/DragDrop/`, `MGUI.Core/UI/MGElement.cs` (section Drag and Drop), `MGUI.Core/UI/MGDesktop.cs`

**Ce qui existe :**
- `DragDropManager` sur `MGDesktop` — gère les opérations D&D globalement
  - `DoDragDrop()`, `NotifyDragOver()`, `NotifyDragLeave()`, `NotifyDrop()`, `CancelDrag()`
  - `IsDragging`, `CurrentDropTarget`
- `MGElement.AllowDrop` — active la réception de drop sur un élément
  - Souscrit automatiquement à `MouseHandler.MovedInside` et `Exited`
- Events sur `MGElement` : `DragEnter`, `DragOver`, `DragLeave`, `Drop`
- `DragDropData` — données transportées
- `DragDropEventArgs` : `DragEnterEventArgs`, `DragOverEventArgs`, `DragLeaveEventArgs`, `DropEventArgs`
- `DragDropEffect` — enum d'effets
- Integration Desktop : `HighPriorityMouseHandler.ReleasedInside/Outside` → finalize/cancel drag

**Tâche :** Vérifier que le cycle D&D complet fonctionne : `DoDragDrop()` → `DragEnter` → `DragOver` → `Drop` (ou `CancelDrag`). Vérifier que `DragLeave` est bien invoqué quand la souris quitte un drop target. Vérifier que seuls les éléments avec `AllowDrop = true` reçoivent les events. Documenter les résultats.

**Commit :** `audit(drag-drop): verify full drag-drop lifecycle`

---

## 11. Vérifier le Window management

**Fichiers :** `MGUI.Core/UI/MGWindow.cs`, `MGUI.Core/UI/MGDesktop.cs`

**Ce qui existe :**
- `MGDesktop.Windows` — liste ordonnée de fenêtres (dernière = topmost pour le rendu)
- `BringToFront()` / `BringToBack()` — réordonnancement
- `MGWindow.IsTopmost` — fenêtre toujours au-dessus
- `ModalWindow` / `HasModalWindow` / `IsModalWindow` — support modal
  - Le modal bloque les inputs sur la fenêtre parente
- `NestedWindows` — fenêtres enfants
- `WindowStyle` — style de fenêtre
- `AllowsClickThrough` — permet de cliquer à travers la fenêtre
- `SizeToContent` — dimensionnement automatique
- `WindowDataContext` — DataContext racine pour le binding
- `OverlayHost` / `OverlayWindow` — overlay système au-dessus de tout
- Position : `Left`, `Top`, `TopLeft`, `OnWindowPositionChanged`
- Taille : `WindowWidth`, `WindowHeight` avec clamp Min/Max
- `Scale` — mise à l'échelle de la fenêtre avec matrices de transformation
- `HoveredElement` / `PressedElement` — tracking des éléments interactifs

**Tâche :** Vérifier que l'ordre de rendu respecte bien la logique `IsTopmost` > position dans la liste. Vérifier que les fenêtres modales bloquent effectivement les inputs sur leur parent. Vérifier que `AllowsClickThrough` fonctionne correctement avec la logique `FindFirstOpaqueParent`. Documenter les résultats.

**Commit :** `audit(window): verify window ordering, modal, and click-through`

---

## 12. Vérifier les coordonnées et transformations

**Fichiers :** `MGUI.Core/UI/MGElement.cs` (section Bounds)

**Ce qui existe :**
- Espaces de coordonnées : `CoordinateSpace.Layout`, `CoordinateSpace.UnscaledScreen`, `CoordinateSpace.Screen`
- `GetTransform()` — matrice de transformation entre espaces
- `ConvertCoordinateSpace()` — conversion Rectangle, Vector2, Point
- `Origin` — offset pour ScrollViewer
- Bounds multiples :
  - `AllocatedBounds` — espace alloué par le parent
  - `RenderBounds` — après alignement
  - `LayoutBounds` — après marge
  - `StretchedContentBounds` — avant alignement du contenu
  - `AlignedContentBounds` — après alignement du contenu
  - `ActualLayoutBounds` — bounds réelles en screen space, intersectées avec le parent (clipping)
- `TranslateAllBounds()` — translation de tous les bounds lors du déplacement de fenêtre

**Tâche :** Vérifier que `ActualLayoutBounds` est correctement calculé comme l'intersection de l'élément avec les bounds visibles du parent. Vérifier que `Origin` est correctement appliqué dans le cas du `ScrollViewer`. Documenter les résultats.

**Commit :** `audit(coordinates): verify coordinate spaces and ActualLayoutBounds clipping`

---

## Résumé des lacunes architecturales identifiées (vs WPF)

Ces points sont documentés à titre informatif — **ne pas implémenter** pour l'instant :

| Fonctionnalité WPF | État dans MGUI | Notes |
|---|---|---|
| DependencyProperty | Absent | Utilise INotifyPropertyChanged classique |
| Routed Events (Bubbling/Tunneling) | Absent | Events normaux C#, handled via `SetHandledBy` |
| Tab/Shift+Tab Navigation | Absent | Pas de `TabIndex`, pas de `IsTabStop` |
| Focus Visual (focus ring) | Absent | Pas d'indicateur visuel de focus |
| ICommand / RelayCommand | Partiel | `CommandName` (string) + delegate `Command`, pas d'ICommand |
| ControlTemplate | Absent | Composition programmatique uniquement |
| Triggers (Property/Data/Event) | Absent | — |
| MultiBinding / PriorityBinding | Absent | — |
| RelativeSource / TemplateBinding | Absent | — |
| Style BasedOn (héritage) | Absent | — |
| Attached Properties | Absent | Grid.Row/Column via attributs XAML mais pas un vrai système |
