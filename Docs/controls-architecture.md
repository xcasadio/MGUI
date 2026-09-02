# Architecture des controles composites

## Objectif et portee

Ce document decrit l'etat ACTUEL des controles composites majeurs de MGUI.Core : `MGPropertyGrid`, le systeme de graphe a noeuds (`MGGraphView`), le ColorPicker avance, le systeme de docking, et les contrats notables de `MGNumericUpDown`, `MGWrapPanel` et `MGCanvas`.

Guides d'utilisation associes :
- graphe : `Docs/graph-view-v1-guide.md`
- color picker : `Docs/mgui_colorpicker_usage_guide.md`

## Vue d'ensemble

Principes communs a tous ces controles :

- **Lookless** : la structure visuelle vient de templates enregistres dans `MGUI.Core/UI/Styling/MGControlTemplateCatalog.cs` (parts nommees `PART_*`), le chrome (couleurs, paddings) vient de classes de settings sur `MGTheme` (`MGUI.Core/UI/MGTheme.cs`). Les controles ne dessinent pas leur chrome dans `DrawSelf`.
- **Separation modele / UI** : la logique pure (documents, descriptors, moteurs de layout, modeles numeriques) vit dans des classes sans dependance au rendu, testables sans MonoGame.
- **MGUI.Core reste generique** : pas de logique metier (compilation de graphe, materiaux, dialogues — cela appartient a l'application hote, ex. CasaEngine), pas d'acces filesystem (l'hote fournit les hooks), et pas de token MonoGame interdit par les garde-fous d'architecture (d'ou `GraphValueType.Texture` et non `Texture2D`).

## PropertyGrid

Controle : `MGUI.Core/UI/MGPropertyGrid.cs` (`MGPropertyGrid`, `MGSingleContentHost`). Couche non-UI : `MGUI.Core/UI/PropertyGrid/` (`MGPropertyGridDescriptor`, `MGPropertyGridEditorKind`, `MGPropertyGridDescriptorCache`, `MGPropertyGridCategoryModel`, `PropertyGridColorAdapter`).

### Descriptors et editeurs

- `MGPropertyGridDescriptor` : name / display name / category / property type / editor kind / getter / setter / read-only. Independant de l'UI. `CategoryAttribute` et `DisplayNameAttribute` sont honores ; categorie de repli `Misc`.
- Cache de descriptors par `Type` (`MGPropertyGridDescriptorCache`) : la reflexion n'a lieu qu'au changement de type ; jamais de `type.GetProperties()` par frame.
- `MGPropertyGridEditorKind` : `Bool`, `Int`, `Float`, `Double`, `String`, `Color`.
- Contrat interne des editeurs (classes imbriquees de `MGPropertyGrid`) : `Element`, `IsEditing`, `ApplyValue(value, force)`, event `ValueCommitted`, `SetReadOnly`, `ApplyTheme`, `Dispose`.
- Regles de commit : `bool` commit immediat au toggle ; `int`/`float`/`double`/`string` commit sur Enter ou perte de focus, parsing `InvariantCulture` ; une saisie numerique invalide n'appelle jamais le setter, ne jette pas, et est signalee via `MGThemePropertyGridSettings.InvalidEditorBorderBrush`. Le setter n'est invoque que si la valeur a reellement change.
- Editeur couleur : `MGPropertyGridEditorKind.Color` + `PropertyGridColorAdapter`, qui convertit vers/depuis `ColorValue` pour XNA `Color`, `Vector3`/`Vector4` et `System.Numerics.Vector3`/`Vector4`.

### Cycle de vie et refresh

- `SelectedObject` : reconstruit la vue (`RebuildView`) uniquement si le type change (ou si `ICustomTypeDescriptor` est implique, car les descriptors peuvent alors varier par instance) ; sinon simple relecture des valeurs.
- `RefreshVisibleValues()` est concu pour etre appele a chaque frame : il ne reconstruit jamais l'UI, ignore les categories repliees, les lignes hors du `ScrollViewer.ContentViewport`, et les editeurs dont `IsEditing == true` (protection de la saisie en cours) ; il compare avec `LastKnownValue` et ne touche l'editeur visuel qu'en cas de difference.
- L'etat replie/deplie est persiste par nom de categorie et survit aux changements d'objet de meme type.

### Template et theme

- Template `MGControlTemplateCatalog.PropertyGridTemplateName` (`"PropertyGrid.Default"`), parts `PART_OuterBorder` / `PART_ScrollViewer` / `PART_CategoriesPanel`.
- Chrome dans `MGThemePropertyGridSettings` (`MGTheme`), contrepartie declarative dans `MGUI.Core/UI/XAML/Themes.cs`, valeurs dans `BuiltInThemes.xaml`.
- `LabelColumnWidth` ajuste la colonne des libelles sur toutes les lignes existantes.

## Systeme de graphe a noeuds

Deux couches :

- Controles publics dans le namespace `MGUI.Core.UI` : `MGGraphView`, `MGGraphNode`, `MGGraphPort`, `MGGraphCommentBox` (`MGUI.Core/UI/MGGraphControls.cs`).
- Modele et services dans `MGUI.Core.UI.Graph` (`MGUI.Core/UI/Graph/`) : dossiers `Commands/`, `Interaction/`, `Model/`, `Presentation/`, `Rendering/`, `Serialization/`, `Validation/`.

### Modele document

`GraphDocument` (`Graph/Model/GraphDocument.cs`) est la source de verite : `Nodes`, `Edges`, `Comments`, metadata, event `GraphChanged`. Chaque element porte un `Guid` stable. `GraphNodeModel` (type de noeud, titre, position, taille optionnelle, ports, proprietes, `EditorMetadata`, collapse), `GraphPortModel` (direction, `GraphValueType`, `GraphPortCardinality`, required), `GraphEdgeModel`, `GraphCommentModel` (bounds monde, titre, texte, couleur). `GraphValueType` inclut `Texture` (pas `Texture2D`) et `Wildcard`.

### Synchronisation document -> vue

`GraphDocumentViewSynchronizer` (`Graph/Presentation/GraphDocumentViewSynchronizer.cs`, interne) maintient des dictionnaires `Guid -> controle` pour noeuds, ports et commentaires :

- cree/reutilise/supprime les controles enfants du `NodesCanvas` (un `MGCanvas`) ; position via `GraphViewportTransform.WorldToLayout` puis `MGCanvas.SetLeft/SetTop`.
- culling par viewport (`GraphCullingService`) : les controles hors viewport (avec `CullingPadding`) sont `Visibility.Collapsed` et reutilises ; diagnostics dans `MGGraphView.CullingDiagnostics`.
- ports poses dans la grille `PortsPanel` du noeud, colonnes input/output ; taille auto du noeud mesuree quand `model.Size` est null (taille monde memorisee via `GraphSelectionManager.SetAutoMeasuredWorldSize` pour la selection rectangle).
- bounds de commentaire normalises pour contenir le texte (`MGGraphCommentBox.MeasureRequiredHeight`), sauf pendant l'edition du commentaire.
- zoom applique aux controles (`ApplyZoomScale` : taille de police et paddings).

Toute mutation du document declenche `SynchronizeDocument()` (abonnement a `GraphChanged`). `OnThemeChanged` resynchronise avant la propagation du theme aux descendants.

### Rendu des edges

Les edges ne sont PAS des `MGElement` : `MGGraphSurfaceCanvas` (`Graph/Presentation/`) les dessine centralement en courbes de Bezier echantillonnees en polylignes, mises en cache dans `GraphEdgeGeometryCache` (cle : extremites, epaisseur, zoom, nombre de segments ; `RetainEdges` a chaque synchronisation). Un edge n'est dessine que si ses bounds approximatifs intersectent le viewport, sauf s'il est selectionne.

### Undo / redo et commandes

`GraphCommandStack` (`Graph/Commands/`) est local a la vue (`MGGraphView.Commands`). Les commandes mutent uniquement le modele ; l'UI se resynchronise. Commandes : `CreateNodeCommand`, `DeleteNodeCommand`, `MoveNodeCommand`, `MoveNodesCommand`, `ResizeNodeCommand`, `ConnectPortsCommand`, `DisconnectPortsCommand`, `CreateCommentCommand`, `DeleteCommentCommand`, `MoveCommentCommand`, `ResizeCommentCommand`, `EditCommentCommand`, `GraphBatchCommand` (groupement en une entree d'undo).

### Ports et connexions

- Ports types : direction + type de valeur + cardinalite + flag required.
- `GraphTypeCompatibilityService` (`Graph/Validation/`) valide direction source/cible, compatibilite de types, doublons, cardinalite single de la cible (connexion refusee, jamais remplacee automatiquement) et prevention de cycles optionnelle.
- `GraphConnectionController` (`Graph/Interaction/`) pilote le drag de connexion ; lacher sur une zone vide ouvre la palette (`GraphNodePalette.GetDefinitions(draggedPort)`) filtree par compatibilite et connecte automatiquement le premier port compatible du noeud cree.

### Edition de commentaire

Double-clic sur un `MGGraphCommentBox` -> `MGGraphView.TryBeginCommentEdit` : le `BodyTextBox` (un `MGTextBox` du template) devient editable et prend le focus. `Escape` annule, `Ctrl+Enter` commite, la perte de focus hors du commentaire commite. Le commit passe par `EditCommentCommand` (annulable) et re-normalise les bounds pour contenir le texte. Le texte affiche est `Text` si non vide, sinon `Title` (champ conserve pour la serialisation).

### Presse-papiers

`Ctrl+C` / `Ctrl+V` et menu contextuel Copy/Paste. Format : prefixe `"MGUI.GraphClipboard.v1"` suivi du JSON `GraphSerializer` du sous-graphe selectionne. Le paste regenere tous les `Guid`, place a la position du pointeur (snappee si `SnapToGrid`) et selectionne le contenu colle. Le presse-papiers OS passe par `StringClipboard` (`MGUI.Core/UI/Text/StringClipboard.cs`).

### Validation et serialisation

- `GraphDocumentValidator` valide edges et ports d'entree required ; il ne possede aucune presentation. L'hote pose `EditorMetadata` `HasError` / `HasWarning` (`"true"`/`"1"`/`"yes"`) sur les noeuds puis appelle `SynchronizeDocument()`.
- `GraphSerializer` (`Graph/Serialization/`) : JSON versionne via DTO (jamais de controles serialises) ; les types de noeuds inconnus sont preserves comme donnees ; `GraphMigrationService` gere les versions.

### Templates et theme

Templates dans `MGControlTemplateCatalog` : `GraphViewTemplateName`, `GraphNodeTemplateName`, `GraphPortTemplateName`, `GraphCommentBoxTemplateName`. Chrome dans `MGThemeGraphSettings` (`MGTheme.Graph`). Details des parts et tokens : `Docs/graph-view-v1-guide.md`.

## ColorPicker

Environ 40 fichiers dans `MGUI.Core/UI/Color/`. Utilisation : `Docs/mgui_colorpicker_usage_guide.md`.

- **`ColorValue`** : RGBA float + color space. Existe parce que XNA `Color` est byte-based et ne peut pas representer HDR ni Linear ; `ToXnaColor()` est toujours une conversion LDR explicite et clampee.
- **Controles** : `MGColorField`, `MGColorPicker`, `MGColorPickerPopup`, `MGColorPaletteView`, `MGColorPreview`, `MGColorSlider`, `MGColorSwatch` ; modeles purs testables : `MGColorPickerModel`, `MGColorFieldModel`, `MGColorTextInputModel`.
- **Commit modes** (`ColorEditCommitMode`) : `Live`, `OnMouseRelease`, `ExplicitOkCancel`. La selection dans une palette respecte le mode (commit immediat en Live/OnMouseRelease, preview en attente en ExplicitOkCancel).
- **Undo pilote par l'hote** : `IColorEditTransaction` via `ColorPickerOptions.EditTransaction` (`NoOpColorEditTransaction` par defaut) ; une transaction par drag, fermee au mouse release.
- **Eyedropper** : contrat hote `IColorPickService` (`bool BeginPick(...)`) ; MGUI.Core n'implemente pas la capture ecran.
- **Palettes** : `MGColorPalette` + `MGColorPaletteStore` (palette Recent dedupliquee, remontee en tete, plafonnee par `MaxRecentColors` ; `ProjectPalettes` + `AddProjectPalette` / `ExportPalette` / `TryImportProjectPalette` ; aucun acces filesystem dans MGUI.Core). `MGColorPaletteSerializer` : JSON avec regles de tolerance a l'import (nom de palette manquant -> `Palette`, swatch sans nom -> `Color n`, noms dupliques -> `Name (2)`, color space inconnu -> diagnostic + repli sRGB, swatches invalides ignores avec diagnostics).
- **Performance** : gradients caches par cle taille/canal/couleur de base (`MGColorSlider`) et taille/espace/plage (Hue + Kelvin du picker), regeneres au resize ou au changement de parametre, jamais par draw.

## Docking (vue d'ensemble)

`MGUI.Core/UI/Docking/` : modele dans `DockLayout/`, controles dans `Controls/`.

- **`DockLayoutModel`** : arbre de `DockNode` — `DockSplitNode` (orientation, `SplitRatio`, tailles minimales), `DockTabGroupNode`, `DockPanelNode` — plus un store auto-hide par `AutoHideSide` ; event `LayoutChanged` ; `ValidateTree()`, `FindNodeById`, `FindPanelById`.
- **`MGDockHost`** (`MGSingleContentHost`) : racine du systeme ; `LayoutModel` -> reconstruction de l'arbre visuel ; events `PanelAdded` / `PanelRemoved` / `ActivePanelChanged` / `DockLayoutChanged` ; pile maximize/restore ; docking par proximite (`ProximityDockingEnabled`, `ProximityBandWidth`) ; parts `PART_PreviewOverlay`, `PART_DropIndicators`, `PART_LeftAutoHideStrip` / `PART_RightAutoHideStrip` / `PART_TopAutoHideStrip` / `PART_BottomAutoHideStrip`, `PART_AutoHideDrawer`.
- **Drag & drop** : `DockDragData`, `DockDropCalculator`, `DockDropTarget`, `DockZone` ; retour visuel via `MGDockDropIndicators` et `MGDockPreviewOverlay`.
- **Controles** : `MGDockSplitContainer` + `MGDockSplitterBar` (`DockSplitSizing`), `MGDockTabGroup` + `MGDockTabItem`, `MGDockAutoHideStrip` + `MGDockAutoHideDrawer`, `MGFloatingDockWindow` (fenetre `MGWindow` flottante rattachee a son `OwnerHost`).
- **`DockableRegistry`** : definitions de panneaux par id (`DockableDefinition`, `DockableType`), events `OnShown` / `OnHidden` / `OnClosed` / `OnActivated` ; assigner `MGDockHost.DockableRegistry` synchronise automatiquement la visibilite.
- **Persistence** : `DockLayoutSerializer.ToJson` / `FromJson` (DTO versionne) ; extensions `MGDockHost.SaveLayoutToJson()` et `LoadLayoutFromJson(json, panelFactory)` ou `panelFactory` recree le contenu des panneaux par id.
- **Theme et templates** : `MGThemeDockingSettings` (`MGTheme.Docking`) ; templates `Dock.TabItem.Default`, `Dock.AutoHideDrawer.Default`, `Dock.AutoHideStrip.Default`, `Dock.Splitter.Default` dans `MGControlTemplateCatalog`.
- Composition en code uniquement (pas de wrapper XAML) ; demo : `MGUI.Samples/Features/DockingDemo.cs`.

## NumericUpDown, WrapPanel, Canvas

### MGNumericUpDown

`MGUI.Core/UI/MGNumericUpDown.cs` : sous-classe publique de `MGTextBox`, valeur `double`. Proprietes `Minimum` / `Maximum` / `Value` / `Increment` / `DecimalPlaces` / `FormatString` + event `ValueChanged`. La logique pure (coercion, validation de configuration, arrondi, parsing invariant, formatting, `TryIncrease` / `TryDecrease`, bornes) vit dans `MGUI.Core/UI/NumericUpDown/MGNumericUpDownModel.cs`. Template `NumericUpDown.Default`, parts `PART_SpinnerHost` / `PART_IncreaseButton` / `PART_DecreaseButton`. Clavier : Up/Down/PageUp/PageDown/Home/End/Enter. La perte de focus normalise le texte valide ou restaure la derniere valeur sur parse invalide ; les boutons spinner se desactivent selon `IsReadonly` et les bornes. XAML : `NumericUpDown` et alias `NUD`.

### MGWrapPanel

`MGUI.Core/UI/Containers/MGWrapPanel.cs` : `MGMultiContentHost` exposant `Orientation` et `Spacing` ; wrap deterministe dans l'ordre des enfants ; mesure et arrangement delegues au moteur pur `MGWrapPanelLayoutEngine` (teste sans rendu). Pas d'alignement/justification par ligne. Variante virtualisee : `MGUI.Core/UI/Containers/VirtualizingWrapPanel.cs`.

### MGCanvas

`MGUI.Core/UI/Containers/MGCanvas.cs` + `MGCanvasLayoutEngine.cs` : `MGMultiContentHost` avec API attachee statique `MGCanvas.SetLeft` / `SetTop` / `SetRight` / `SetBottom` (+ getters, `int?`). Precedence : Left gagne sur Right, Top gagne sur Bottom ; sans coordonnees, arrangement a l'origine. Coordonnees stockees dans `MGElement.Metadata` avec invalidation de layout automatique. XAML : attributs `CanvasLeft` / `CanvasTop` / `CanvasRight` / `CanvasBottom` sur les noeuds enfants. Pas de `ZIndex` public : ordre de dessin et de hit-test = ordre naturel des enfants. Conteneur neutre pour l'input. C'est la surface hote du graphe (`MGGraphView.NodesCanvas`).

## Limites connues

- PropertyGrid : editeurs limites a `Bool` / `Int` / `Float` / `Double` / `String` / `Color` ; pas d'enum, de collections, d'objets imbriques, d'undo/redo, de multi-selection ni de recherche/filtrage (voir le fichier de taches).
- Graphe : pas de cut/duplicate, recherche, minimap, blackboard, reroutes, sous-graphes, bookmarks, align/distribute, integration PropertyGrid, ni de couche compilation/debug (voir le fichier de taches).
- ColorPicker : pas de surfaces de preview avancees, d'outils d'authoring (gradient editor, harmonies), de simulations de daltonisme ni de formats `.mgpalette` / `.gpl` (voir le fichier de taches).
- Docking : composition en code uniquement, pas de wrapper XAML.
- WrapPanel : pas d'alignement par ligne. Canvas : pas de ZIndex.

## Reste a faire

Les travaux restants du theme sont suivis dans :
- `Docs/Tasks/graph-tasks.md`
- `Docs/Tasks/colorpicker-v3-tasks.md`
