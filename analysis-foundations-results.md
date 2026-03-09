# Résultats d'audit des fondations MGUI

> **Exécuté par :** agent IA (GitHub Copilot / Claude Sonnet 4.6)  
> **Méthode :** analyse statique du code source — lecture des fichiers + recherches ciblées  
> **Légende :** ✅ OK · ⚠️ Partiel / Remarque · ❌ Absent / Manquant

---

## Tâche 1 — Hiérarchie visuelle (Visual Tree)

**Fichiers analysés :** `MGContentHost.cs`, `MGElement.cs`

### Résultats

| Vérification | Verdict | Détail |
|---|---|---|
| `MGMultiContentHost._Children.CollectionChanged` → `SetParent` à l'ajout | ✅ | Ligne ~243 : `item.SetParent(this)` + `InvalidateVtcCache()` |
| `MGMultiContentHost._Children.CollectionChanged` → `SetParent(null)` à la suppression | ✅ | Ligne ~249 : `item.SetParent(null)` + `InvalidateVtcCache()` |
| `MGSingleContentHost.SetContentVirtual` → `SetParent` au set | ✅ | `_Content.SetParent(this)` dans le setter |
| `MGSingleContentHost.SetContentVirtual` → `SetParent(null)` au retrait | ✅ | `previous.SetParent(null)` avant remplacement |
| `AddComponent` → `SetParent` sur composant | ✅ | `element.ComponentParent = this` dans `MGElement.AddComponent` |
| Cas spécial `MGContentPresenter.SuppressContentAddedAndRemoved` | ⚠️ | Quand `true`, `SetParent` n'est **pas** appelé — intentionnel (commentaire en code), usage interne seulement |

### Conclusion
La gestion parent-enfant est **cohérente** dans l'ensemble de l'arbre. L'unique exception (`SuppressContentAddedAndRemoved`) est clairement documentée en code comme usage interne spécialisé. Aucune fuite de référence parent détectée.

---

## Tâche 2 — Système de layout (Measure / Arrange)

**Fichiers analysés :** `MGStackPanel.cs`, `MGDockPanel.cs`, `MGOverlayPanel.cs`, `MGGrid.cs`, `MGUniformGrid.cs`, `VirtualizingStackPanel.cs`, `MGContentHost.cs`

### Résultats

| Conteneur | `UpdateContentMeasurement` | `UpdateContentLayout` | Notes |
|---|---|---|---|
| `MGStackPanel` | ✅ ligne 165 | ✅ ligne 235 | Gère Vertical + Horizontal, Spacing, IsVisibilityCollapsed |
| `MGDockPanel` | ✅ ligne 100 | ✅ ligne 160 | Algorithme cumulatif par dock side |
| `MGOverlayPanel` | ✅ | ✅ | Gère ZIndex, offsets par enfant |
| `MGGrid` | ✅ ligne 1186 | ✅ ligne 1195 | `ComputeDimensions(IsMeasuring=true/false)`, gestion * / Auto / Pixel |
| `MGUniformGrid` | ✅ ligne 794 | ✅ ligne 796 | Grille uniforme |
| `VirtualizingStackPanel` | ✅ ligne 186 | ✅ ligne 193 | Virtualisation : seuls les éléments visibles sont mesurés/arrangés |
| `MGSingleContentHost` | ✅ ligne 279 | ✅ ligne 290 | Arrange unique enfant |

**`LayoutChanged` propagation :** ✅ Chaque `LayoutChanged()` remonte vers `Parent.LayoutChanged()` jusqu'à la racine. `DeferEventsManager` / `BeginInitializing()` permet de différer ces invalidations pendant la construction.

**MGGrid — cas particulier :** La méthode `ComputeDimensions` est appelée deux fois (mesure et arrange) avec un flag `IsMeasuring`. Les colonnes `*` (weighted) sont traitées comme `Auto` pendant la mesure, puis allouées proportionnellement pendant l'arrange. C'est le comportement attendu.

### Conclusion
Le système Measure/Arrange est **complet et correctement implémenté** dans tous les conteneurs. La propagation d'invalidation est fonctionnelle.

---

## Tâche 3 — Gestion des entrées (Input Handling)

**Fichiers analysés :** `MGElement.cs`, `MGDesktop.cs`, `MGUI.Shared/Input/MouseHandler.cs`, `MGUI.Shared/Input/KeyboardHandler.cs`

### Résultats

| Vérification | Verdict | Détail |
|---|---|---|
| Bubbling via `HandledByEventArgs.SetHandledBy` | ✅ | Les handlers vérifient `args.IsHandled` avant d'exécuter ; un événement géré par un enfant ne remonte plus au parent |
| `_CanReceiveMouseInput` dirty-flag | ✅ | Flag `_inputStateDirty` posé quand `IsEnabled`, `IsHitTestVisible`, `Visibility` changent → recalculé dans `Update()` |
| `_CanReceiveKeyboardInput` dirty-flag | ✅ | Idem, conditionné par `CanHandleKeyboardInput && _CanReceiveKeyboardInput` |
| `IsHitTestVisible` fils hérité du parent | ✅ | `DerivedIsHitTestVisible = IsHitTestVisible && (Parent?.DerivedIsHitTestVisible ?? true)` |
| `GetTopmostHoveredElement` | ✅ | Parcours récursif post-order, retourne l'élément le plus profond sous le curseur |
| Priorité input : `HighPriorityMouseHandler` | ✅ | Déclenché avant tous les windows sur le Desktop |

### Conclusion
Le système d'input est **robuste**. Le dirty-flag évite des recalculs inutiles. Le bubbling via `HandledByEventArgs` est un pattern cohérent bien qu'absent en WPF (qui utilise les RoutedEvents). Pas d'input routing hiérarchique bubbling/tunneling au sens WPF.

---

## Tâche 4 — Gestion du focus clavier

**Fichiers analysés :** `MGDesktop.cs`, `MGElement.cs`, `MGListBox.cs`, `MGListView.cs`, `MGTextBox.cs`

### Résultats

| Vérification | Verdict | Détail |
|---|---|---|
| `Focus()` met `QueuedFocusedKeyboardHandler` | ✅ | `GetDesktop().QueuedFocusedKeyboardHandler = this` |
| `QueuedFocusedKeyboardHandler` appliqué en fin d'Update | ✅ | `MGDesktop.Update()` : `FocusedKeyboardHandler = QueuedFocusedKeyboardHandler` |
| Un seul élément focusé à la fois | ✅ | `FocusedKeyboardHandler` remplacé à chaque `Focus()` |
| Focus réinitialisé sur tout clic souris | ✅ | `HighPriorityMouseHandler.PressedInside/Outside → QueuedFocusedKeyboardHandler = null` |
| Auto-Focus sur LMBPressedInside pour éléments `IsFocusable` | ⚠️ | **Non automatique** dans la base — chaque contrôle l'implémente manuellement |
| `MGListView` → `Focus()` au clic | ✅ | Explicitement : `DataGrid.MouseHandler.LMBPressedInside += (s,e) => Focus()` |
| `MGTextBox` → Focus au clic | ✅ | `MouseHandler.LMBPressedInside` dans constructeur, appelle `Focus()` |
| `MGListBox` → Focus au clic | ⚠️ | `LMBPressedInside` handler présent mais focus implicite via sélection d'item |
| Tab/Shift+Tab navigation | ❌ | **Absent** — pas de `TabIndex`, pas de `IsTabStop` |
| Indicateur visuel de focus (focus ring) | ❌ | **Absent** — pas de `FocusVisualStyle` |

### Lacunes documentées (ne pas implémenter)
- Tab navigation cyclique entre éléments `IsFocusable` manquante
- Aucun indicateur visuel de l'élément focusé

### Conclusion
Le cycle de vie du focus est **fonctionnel** mais non automatisé au niveau de la classe de base. Chaque contrôle focusable doit explicitement appeler `Focus()` au clic. Les lacunes Tab/FocusVisual sont des limitations connues vs WPF.

---

## Tâche 5 — Data Binding et pattern MVVM

**Fichiers analysés :** `DataBinding.cs`, `DataBindingManager.cs`, `MGBinding.cs`, `XAMLBindableBase.cs`, `BindingConfig.cs`, `MGElement.cs`

### Résultats

| Vérification | Verdict | Détail |
|---|---|---|
| `DataContext` hérité depuis `WindowDataContext` | ✅ | `DataContext => DataContextOverride ?? SelfOrParentWindow.WindowDataContext` |
| `DataContextChanged` déclenché sur `DataContextOverride` | ✅ | `InvokeDataContextChanged()` dans le setter de `DataContextOverride` |
| `DataContextChanged` déclenché sur `WindowDataContext` | ✅ | `SelfOrParentWindow.WindowDataContextChanged` souscrit dans constructeur → `InvokeDataContextChanged()` |
| Propagation aux enfants | ✅ | `WindowDataContext` change → tous les éléments dont `DataContextOverride == null` reçoivent `DataContextChanged` |
| `DataBindingManager.RemoveBindings(object)` nettoie les listeners | ✅ | Supprime tous les bindings associés à l'objet cible |
| Modes OneWay / TwoWay | ✅ | Implémentés dans `DataBinding` avec `PropertyNameListener` |
| Conversion via `IValueConverter` | ✅ | `DataBinding.Converter` + `ConverterParameter` |
| `HasError` / `LastError` | ✅ | Diagnostic de binding disponible |
| `ICommand` / `RelayCommand` WPF-style | ⚠️ | Partiel : `CommandName` (string lookup dans `MGResources.Commands`) ou delegate `Command` |
| `MultiBinding` / `PriorityBinding` | ❌ | Absent |
| `RelativeSource` | ❌ | Absent |
| `DependencyProperty` | ❌ | Non applicable — architecture INPC classique |

### Conclusion
Le système de binding est **opérationnel et bien structuré** pour une architecture non-WPF. La propagation du `DataContext` depuis la fenêtre vers tous les éléments est correcte. Les limites vs WPF sont documentées et connues.

---

## Tâche 6 — Système de styles XAML

**Fichiers analysés :** `Style.cs`, `Element.cs` (`ProcessStyles`), `XAMLParser.cs`

### Résultats

| Vérification | Verdict | Détail |
|---|---|---|
| Styles implicites (par `TargetType`) appliqués | ✅ | `StylesByType` indexé, appliqué via `PropertyInfo.SetValue` + `TypeConverter` |
| Styles explicites (par `Name`) appliqués | ✅ | `StylesByName[name]` résolu, filtré par `TargetType == ElementType` |
| Styles globaux (Desktop-level) hérités | ✅ | `ProcessStyles(MGResources Resources)` pré-seed avec `Resources.ImplicitStyles` |
| Récursivité sur les enfants | ✅ | `foreach (Element Child in GetChildren()) Child.ProcessStyles(...)` |
| Non-écrasement des valeurs explicites | ⚠️ | Guard : `PropertyInfo.GetValue(this) == default` — **fragile** pour les types non-null par défaut (ex: bool, int, classes initialisées) |
| `AffectsComponents` | ✅ | Flag dans `Style` pour inclure ou non les composants internes dans le scope du style |
| `BasedOn` (héritage de style) | ❌ | Absent |
| `Trigger` / `DataTrigger` / `EventTrigger` | ❌ | Absent |
| `ControlTemplate` | ❌ | Absent |

### Remarque sur la garde de non-écrasement
La condition `PropertyInfo.GetValue(this) == default` est fragile :
- Elle échoue silencieusement si un contrôle initialise une propriété non-null (ex: `Padding = new Thickness(4)` dans le constructeur)
- Le style pensera que c'est une valeur "par défaut" et l'écrasera
- Ce comportement peut produire des styles écrasant des valeurs explicitement définies dans certains cas

### Conclusion
Le système de styles est **fonctionnel pour le cas basique** (implicit + explicit par name). La limite principale est la détection fragile des valeurs explicitement assignées. Les fonctionnalités avancées WPF (Triggers, BasedOn, ControlTemplate) sont absentes.

---

## Tâche 7 — Visual State et pipeline de rendu

**Fichiers analysés :** `VisualState.cs`, `MGElement.cs` (sections Draw + Update)

### Résultats

| Vérification | Verdict | Détail |
|---|---|---|
| `PrimaryVisualState` calculé depuis `IsEnabled/Selected` | ✅ | Priorité : Disabled > Selected > Normal |
| `SecondaryVisualState` calculé depuis `IsLMBPressed/IsHovered` | ✅ | Priorité : Pressed > Hovered > None |
| `VisualState` recalculé chaque frame dans `UpdateSelf()` | ✅ | Via `IsEnabled`, `IsSelected`, `IsLMBPressed`, `IsHovered` |
| `IsHitTestVisible` masque `Hovered` | ✅ | `IsHovered` retourne false si `!IsHitTestVisible` |
| Pipeline de draw (ordre) | ✅ | DrawBeforeBackground → DrawBackground → DrawBeforeSelf → DrawSelf → DrawBeforeContents → DrawContents → DrawAfterContents → OverlayBrush → OnEndingDraw |
| Composants Draw avec `ComponentDrawPriority` | ✅ | Composants répartis sur `_componentsDrawBefore/AfterBackground/Self/Contents` |
| `SpoofIsPressed/HoveredWhileDrawingBackground` | ✅ | Override visuel local pendant la phase background |
| `ClipToBounds` via ScissorRectangle | ✅ | Activé si `ClipToBounds = true` |
| `Opacity` composée avec parent | ✅ | `DerivedOpacity = Opacity * (Parent?.DerivedOpacity ?? 1f)` |
| `Visibility.Collapsed` supprime la mesure | ✅ | `IsVisibilityCollapsed` retourne zéro size dans `MeasureSelf()` |

### Conclusion
Le pipeline de rendu est **bien structuré et complet**. L'ordre de draw est déterministe et cohérent avec WPF. Le système de VisualState couvre les états principaux (disabled/selected/hovered/pressed).

---

## Tâche 8 — Thèmes et ressources

**Fichiers analysés :** `MGTheme.cs`, `MGResources.cs`, `MGElementTemplate.cs`

### Résultats

| Vérification | Verdict | Détail |
|---|---|---|
| `GetBackgroundBrush` couvre tous les types | ✅ | Tous les `MGElementType` sont dans `_Backgrounds` (dict) — soit null brush, soit brush coloré |
| Types sans fond (null brush explicite) | ✅ | 35+ types : Border, CheckBox, Grid, DockPanel, StackPanel, TextBlock, etc. — cohérent avec leur rôle |
| Types avec fond thématique | ✅ | Window, Button, TextBox, ComboBox, ListBox, ScrollBar, TabControl, ProgressBar, etc. |
| Thèmes disponibles | ✅ | `BuiltInTheme` : Dark_Gray, Dark_Red, Dark_Green, Dark_Blue, Dark_Purple, Dark_Pink, Dark_Yellow, Light_Gray (8 thèmes) |
| `GetResources()` accessible depuis `MGElement` | ✅ | Via `ParentWindow.GetResources()` → `MGDesktop.Resources` |
| `MGResources.Textures` | ✅ | `Dictionary<string, MGTextureData>` |
| `MGResources.Commands` | ✅ | `Dictionary<string, Action<MGElement>>` pour `CommandName` binding |
| `MGResources.StaticResources` | ✅ | Pour binding `ResourceName` |
| `MGResources.ElementTemplates` | ✅ | Factory d'éléments `Dictionary<string, MGElementTemplate>` |
| `ThemeManagedGetter<T>` — copie défensive | ✅ | Retourne des copies pour éviter la mutation partagée des brushes |

### Conclusion
La couverture thématique est **complète** — tous les types ont une entrée dans le dictionnaire. Les ressources sont centralisées et accessibles depuis n'importe quel élément. La copie défensive des brushes évite les effets de bord.

---

## Tâche 9 — ToolTip et ContextMenu

**Fichiers analysés :** `MGDesktop.cs`, `MGElement.cs`, `MGToolTip.cs`, `MGContextMenu.cs`

### Résultats

**ToolTip :**

| Vérification | Verdict | Détail |
|---|---|---|
| Un seul ToolTip actif à la fois | ✅ | `_ActiveToolTip` géré exclusivement dans `MGDesktop` |
| Délai configurable avant affichage | ✅ | `ToolTipShowDelay` (défaut 0.3s), `QueuedToolTip` en attente |
| Events cycle de vie | ✅ | `ToolTipOpening` (cancellable), `ToolTipOpened`, `ToolTipClosed` |
| Fermeture sur `ToolTipChanged` du host | ✅ | `Host.ToolTipChanged += Host_ToolTipChanged` |
| Mutual exclusion même host | ⚠️ | Si même host, `Cancellable = false` → le ToolTipOpening n'est **pas** déclenché pour le remplacement |

**ContextMenu :**

| Vérification | Verdict | Détail |
|---|---|---|
| `TryOpenContextMenu` avec events cancellables | ✅ | `ContextMenuOpening` cancellable ; `InvokeContextMenuOpening()` sur le menu |
| `TryCloseActiveContextMenu` récursif | ✅ | Ferme d'abord les nested menus avant le menu parent |
| Events cycle de vie | ✅ | `ContextMenuOpening/Opened/Closing/Closed` |
| Menu dynamique via `ContextMenuRequested` | ✅ | Event permettant la construction du menu au RMB release sans pré-assigner `ContextMenu` |
| Auto-positionnement | ✅ | `FitMenuToViewport` ajuste la position pour rester dans `ValidScreenBounds` |
| Menus imbriqués | ✅ | `MGContextMenu.TryCloseActiveContextMenu` gère la hiérarchie |
| `SyncContextMenuRmbHandler()` | ✅ | Auto-souscription au RMB release quand `ContextMenu != null` |

### Conclusion
Les cycles de vie ToolTip et ContextMenu sont **correctement implémentés**. L'exclusion mutuelle est assurée. Le menu dynamique via `ContextMenuRequested` est fonctionnel.

---

## Tâche 10 — Drag and Drop

**Fichiers analysés :** `DragDropManager.cs`, `DragDropEventArgs.cs`, `MGElement.cs`

### Résultats

| Vérification | Verdict | Détail |
|---|---|---|
| `DoDragDrop()` → `DragStarted` event | ✅ | Annule le drag précédent si actif, set `DragSource` + `ActiveDrag` + fire event |
| `NotifyDragEnter` → `DragEnter` event sur target | ✅ | `CurrentDropTarget = target; target.RaiseDragEnter(...)` |
| `NotifyDragEnter` → `DragLeave` sur l'ancien target | ✅ | Transition automatique : `old.RaiseDragLeave(...)` avant `new.RaiseDragEnter(...)` |
| `NotifyDragOver` → `DragOver` event | ✅ | Appelle `NotifyDragEnter` si target a changé (lazy enter) |
| `NotifyDragLeave` → `DragLeave` event | ✅ | Nettoie `CurrentDropTarget = null` |
| `NotifyDrop` → `Drop` event seulement si `AllowDrop = true` | ✅ | `target != null && target.AllowDrop` vérifié avant `RaiseDrop` |
| `CancelDrag()` → cleanup propre | ✅ | `RaiseDragLeave` sur `CurrentDropTarget`, `EndDrag()`, `DragEnded` event |
| `AllowDrop = true` → auto-souscription `MovedInside`/`Exited` | ✅ | `RefreshDragDropSubscriptions()` via `AllowDrop` setter |
| Éléments sans `AllowDrop` ne reçoivent pas d'events | ✅ | `NotifyDrop` vérifie `AllowDrop` — les handlers `MovedInside`/`Exited` ne sont pas branchés |

### Conclusion
Le cycle Drag & Drop est **complet et correctement implémenté**. Les transitions entre targets sont proprement gérées (DragLeave automatique). L'API est simple et bien encapsulée dans `DragDropManager`.

---

## Tâche 11 — Window management

**Fichiers analysés :** `MGWindow.cs`, `MGDesktop.cs`

### Résultats

| Vérification | Verdict | Détail |
|---|---|---|
| `IsTopmost` windows rendues au-dessus | ✅ | Filtrées en dernier dans l'ordre de rendu |
| `BringToFront` / `BringToBack` | ✅ | Réordonnent `MGDesktop.Windows` ; `IsTopmost` contraint la position |
| `ModalWindow` bloque les inputs | ✅ | `ParentWindow.HasModalWindow` vérifié dans les handlers input (ex: `MGGrid.SelectionMouseHandler.LMBPressedInside`) |
| Nettoyage modal à la fermeture | ✅ | `ParentWindow.ModalWindow = null` dans `IsVisible = false` du modal |
| `NestedWindows` | ✅ | Liste de fenêtres filles rendues dans la fenêtre parente |
| `AllowsClickThrough` | ✅ | Utilisé dans `FindFirstOpaqueParent` pour les hit-tests |
| `WindowDataContext` comme DataContext racine | ✅ | Tous les éléments de la fenêtre héritent de `WindowDataContext` |
| `Scale` via matrice de transformation | ✅ | `UnscaledScreenSpaceToScaledScreenSpace` appliqué à tout le rendu de la fenêtre |
| `HoveredElement` / `PressedElement` | ✅ | Trackés dans `MGWindow.Update()` |
| `OverlayWindow` — toujours rendu en dernier | ✅ | `MGDesktop.OverlayHost` rendu après tous les windows |

### Remarque sur le modal
Le blocage modal est **par fenêtre** (pas global) : `ParentWindow.HasModalWindow`. Cela signifie que plusieurs niveaux de modales sont théoriquement possibles. Les éléments individuels doivent vérifier `ParentWindow.HasModalWindow` — c'est fait dans les contrôles actifs (Grid, etc.) mais n'est **pas enforced automatiquement** dans le système de base pour tous les éléments.

### Conclusion
La gestion des fenêtres est **solide**. Le système modal fonctionne correctement pour le cas d'usage typique. L'overlay system assure que certains éléments (context menus, tooltips) sont toujours au premier plan.

---

## Tâche 12 — Coordonnées et transformations

**Fichiers analysés :** `MGElement.cs` (sections Bounds, CoordinateSpace)

### Résultats

| Vérification | Verdict | Détail |
|---|---|---|
| 3 espaces de coordonnées définis | ✅ | `Layout`, `UnscaledScreen`, `Screen` |
| `Layout` → `UnscaledScreen` | ✅ | `Matrix.CreateTranslation(-Origin.X, -Origin.Y, 0)` |
| `UnscaledScreen` → `Screen` | ✅ | `SelfOrParentWindow.UnscaledScreenSpaceToScaledScreenSpace` |
| `Screen` → `Layout` | ✅ | Inverse des matrices ci-dessus |
| `ConvertCoordinateSpace` (Rectangle, Point, Vector2) | ✅ | Surcharges disponibles pour les 3 types |
| `Origin` = offset de scroll | ✅ | `MGScrollViewer` modifie `Origin` pour décaler les bounds des enfants |
| `ActualLayoutBounds` = bounds intersectées avec le parent | ✅ | Calculé dans `Update()` : `Rectangle.Intersect(element.LayoutBounds, parent.ActualLayoutBounds)` + translation `Offset` |
| `TranslateAllBounds()` lors du déplacement de fenêtre | ✅ | `OnWindowPositionChanged` → `TranslateAllBounds(delta)` sur tous les éléments |
| Cohérence hit-test avec `ActualLayoutBounds` | ✅ | `IsInside()` utilise `ActualLayoutBounds` en `UnscaledScreen` space |
| Nested `ScrollViewer` — formule correcte | ✅ | Commentaire détaillé dans `MGGrid.UpdateSelection` : `Origin - SV.Origin` = scroll-offset propre |

### Conclusion
Le système de coordonnées est **bien conçu et cohérent**. Les 3 espaces couvrent les besoins (layout logique, screen non-scalé, screen scalé). La formule de `ActualLayoutBounds` garantit un clipping correct à chaque niveau de l'arbre. Le cas ScrollViewer imbriqué est explicitement commenté et validé.

---

## Synthèse globale

### Bilan des vérifications

| # | Tâche | Verdict global |
|---|---|---|
| 1 | Visual Tree — parent/enfant | ✅ Correct |
| 2 | Layout — Measure/Arrange | ✅ Correct |
| 3 | Input — bubbling + dirty flags | ✅ Correct |
| 4 | Focus clavier | ⚠️ Fonctionnel, non automatisé au niveau base |
| 5 | Data Binding / MVVM | ✅ Correct (dans le périmètre non-WPF) |
| 6 | Styles XAML | ⚠️ Fonctionnel, garde fragile sur valeurs par défaut |
| 7 | Visual State + pipeline rendu | ✅ Correct |
| 8 | Thèmes + ressources | ✅ Correct |
| 9 | ToolTip + ContextMenu | ✅ Correct |
| 10 | Drag & Drop | ✅ Correct |
| 11 | Window management | ✅ Correct (modal non-enforced automatiquement) |
| 12 | Coordonnées + transformations | ✅ Correct |

### Points nécessitant attention (sans blocage)

1. **Task 4** — `Focus()` doit être appelé manuellement par chaque contrôle au clic. Risque d'oubli dans les futurs contrôles.
2. **Task 6** — La condition `PropertyInfo.GetValue(this) == default` pour éviter l'écrasement des valeurs explicites est fragile. Peut produire des comportements inattendus avec des propriétés initialisées à des valeurs non-default.
3. **Task 11** — Le blocage modal n'est pas automatique pour tous les éléments. Les nouveaux contrôles doivent vérifier `ParentWindow.HasModalWindow` manuellement.

### Lacunes architecturales confirmées (vs WPF)

| Fonctionnalité WPF | État MGUI |
|---|---|
| `DependencyProperty` | ❌ Absent — INPC classique |
| Routed Events (Bubbling/Tunneling) | ❌ Absent — `HandledByEventArgs` en remplacement |
| Tab/Shift+Tab navigation | ❌ Absent |
| Focus visual (focus ring) | ❌ Absent |
| `ICommand` / `RelayCommand` | ⚠️ Partiel — `CommandName` string + delegate |
| `ControlTemplate` | ❌ Absent |
| `Trigger` / `DataTrigger` | ❌ Absent |
| `MultiBinding` / `RelativeSource` | ❌ Absent |
| Style `BasedOn` | ❌ Absent |

Ces lacunes sont **documentées et connues** — elles représentent des choix d'implémentation adaptés au contexte MonoGame (pas de XAML live, pas de reflexion DependencyProperty, performances prioritaires).
