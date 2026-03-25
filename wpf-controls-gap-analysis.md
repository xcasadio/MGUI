# Analyse des Contrôles WPF Non Implémentés dans MGUI

## Introduction

Ce document identifie les contrôles WPF standard qui ne sont pas actuellement implémentés dans MGUI, un framework UI pour MonoGame qui réimplémente une partie de WPF pour le rendre exportable dans des environnements de jeu. L'analyse se concentre spécifiquement sur les contrôles standard de WPF et leur présence ou absence dans MGUI.

## Contrôles Actuellement Implémentés dans MGUI

### Conteneurs
- MGDockPanel (équivalent: DockPanel)
- MGGrid (équivalent: Grid)
- MGOverlayPanel (spécifique à MGUI)
- MGStackPanel (équivalent: StackPanel)
- MGUniformGrid (équivalent: UniformGrid)
- MGWrapPanel (équivalent: WrapPanel)
- MGCanvas (équivalent: Canvas)
- MGScrollViewer (équivalent: ScrollViewer)
- MGBorder (équivalent: Border)

### Contrôles de Contenu
- MGButton (équivalent: Button)
- MGCheckBox (équivalent: CheckBox)
- MGComboBox (équivalent: ComboBox)
- MGContextMenu (équivalent: ContextMenu)
- MGContextMenuItem (équivalent approchant: MenuItem / ContextMenu item)
- MGExpander (équivalent: Expander)
- MGGroupBox (équivalent: GroupBox)
- MGListBox (équivalent: ListBox)
- MGListView (équivalent: ListView)
- MGMenuBar (équivalent approchant: Menu)
- MGMenuBarItem (équivalent approchant: MenuItem)
- MGRadioButton (équivalent: RadioButton)
- MGTabControl (équivalent: TabControl)
- MGTabItem (équivalent: TabItem)
- MGToggleButton (équivalent: ToggleButton)
- MGTreeView (équivalent: TreeView)
- MGTreeViewItem (équivalent: TreeViewItem)
- MGToolTip (équivalent: ToolTip)
- MGWindow (équivalent: Window)

### Contrôles de Texte
- MGTextBlock (équivalent: TextBlock)
- MGTextBox (équivalent: TextBox)
- MGPasswordBox (équivalent: PasswordBox)

### Contrôles de Sélection et Entrée
- MGSlider (équivalent: Slider)
- MGProgressBar (équivalent: ProgressBar)
- MGNumericUpDown (équivalent usuel: NumericUpDown)

### Contrôles Visuels
- MGImage (équivalent: Image)
- MGRectangle (équivalent: Rectangle)
- MGSeparator (équivalent: Separator)

### Contrôles de Présentation
- MGGridSplitter (équivalent: GridSplitter)

### Contrôles Spécifiques à MGUI
- MGChatBox
- MGGridColorPicker
- MGProgressButton
- MGRatingControl
- MGSpoiler
- MGStopwatch
- MGTimer
- MGResizeGrip
- MGSpacer
- MGXAMLDesigner

## Équivalents WPF Déjà Présents Mais Sous Une Forme Différente

- **ContentControl**: MGUI expose surtout des briques de base comme `MGSingleContentHost` et `MGContentPresenter` plutôt qu'un contrôle public nommé `MGContentControl`.
- **Menu / MenuItem**: MGUI fournit `MGMenuBar` et `MGMenuBarItem` pour les menus principaux, avec `MGContextMenu` et `MGContextMenuItem` pour les sous-menus et menus contextuels.
- **TreeView / TreeViewItem**: déjà implémentés via `MGTreeView` et `MGTreeViewItem`.
- **GridSplitter**: déjà implémenté via `MGGridSplitter`.
- **ScrollContentPresenter / ScrollBar**: ces responsabilités semblent intégrées à `MGScrollViewer` plutôt qu'exposées comme contrôles autonomes.

## Contrôles WPF Standard Non Implémentés

### 1. Contrôles de Contenu Manquants

#### 1.1 Label
**Description**: Contrôle de texte avec support pour les mnémoniques (raccourcis clavier avec Alt+lettre)
**Différence avec TextBlock**: Support des access keys et association automatique avec d'autres contrôles

#### 1.2 ContentControl
**Description**: Contrôle de base pour afficher un seul élément de contenu
**Note**: MGUI couvre déjà ce besoin via `MGSingleContentHost` et `MGContentPresenter`, mais pas sous la forme d'un contrôle public nommé `ContentControl`

#### 1.3 Frame
**Description**: Contrôle de navigation qui peut afficher des pages et gérer l'historique de navigation
**Cas d'usage**: Applications avec navigation entre différentes vues

#### 1.4 ScrollContentPresenter
**Description**: Affiche le contenu d'un ScrollViewer
**Note**: Peut être intégré dans MGScrollViewer

### 2. Contrôles de Sélection Manquants

#### 2.1 Calendar
**Description**: Contrôle d'affichage de calendrier mensuel
**Cas d'usage**: Sélection de dates, planification

#### 2.2 DatePicker
**Description**: Contrôle de sélection de date avec popup de calendrier
**Cas d'usage**: Formulaires nécessitant une entrée de date

#### 2.3 DataGrid
**Description**: Grille de données avec tri, filtrage, édition de cellules
**Cas d'usage**: Affichage et édition de données tabulaires complexes

### 3. Contrôles de Menu Manquants

#### 3.1 ToolBar
**Description**: Barre d'outils avec boutons et autres contrôles
**Cas d'usage**: Actions rapides, commandes fréquentes

#### 3.2 StatusBar
**Description**: Barre d'état en bas de fenêtre
**Cas d'usage**: Affichage d'informations de statut, progression

### 4. Contrôles de Texte Avancés Manquants

#### 4.1 RichTextBox
**Description**: Éditeur de texte riche avec formatage (gras, italique, couleurs, etc.)
**Cas d'usage**: Éditeurs de texte, chat avec formatage

#### 4.2 FlowDocumentReader
**Description**: Lecteur de documents avec pagination, zoom
**Cas d'usage**: Affichage de documents complexes

#### 4.3 FlowDocumentScrollViewer
**Description**: Affichage de documents avec défilement
**Cas d'usage**: Documents longs avec formatage

### 5. Contrôles de Média Manquants

#### 5.1 MediaElement
**Description**: Lecture de vidéo et audio
**Cas d'usage**: Cutscenes, musique de fond, effets sonores
**Note**: Peut être moins pertinent pour MonoGame qui a ses propres systèmes audio/vidéo

#### 5.2 SoundPlayerAction
**Description**: Lecture de sons simples
**Note**: Probablement géré par MonoGame directement

### 6. Contrôles de Forme Manquants

#### 6.1 Ellipse
**Description**: Forme elliptique ou circulaire
**Cas d'usage**: Indicateurs, avatars circulaires

#### 6.2 Line
**Description**: Ligne droite
**Cas d'usage**: Séparateurs personnalisés, diagrammes

#### 6.3 Path
**Description**: Forme vectorielle complexe définie par géométrie
**Cas d'usage**: Icônes vectorielles, formes personnalisées

#### 6.4 Polygon
**Description**: Forme polygonale
**Cas d'usage**: Formes géométriques personnalisées

#### 6.5 Polyline
**Description**: Série de lignes connectées
**Cas d'usage**: Graphiques, diagrammes

### 7. Contrôles de Conteneur Avancés Manquants

#### 7.1 ViewBox
**Description**: Mise à l'échelle de contenu pour remplir l'espace disponible
**Cas d'usage**: Redimensionnement automatique d'icônes, logos

#### 7.2 BulletDecorator
**Description**: Alignement d'une puce avec du contenu
**Cas d'usage**: Listes à puces personnalisées

#### 7.3 InkCanvas
**Description**: Surface de dessin à l'encre
**Cas d'usage**: Dessin à main levée, annotations

### 8. Contrôles de Saisie Spécialisés Manquants

#### 8.1 MaskedTextBox
**Description**: TextBox avec masque de saisie (téléphone, date, etc.)
**Note**: Pas dans WPF de base mais dans WPF Toolkit

### 9. Contrôles de Document Manquants

#### 9.1 DocumentViewer
**Description**: Visualiseur de documents avec pagination
**Cas d'usage**: Affichage de documents XPS, PDF

#### 9.2 Popup
**Description**: Fenêtre popup légère
**Cas d'usage**: Tooltips personnalisés, menus contextuels
**Note**: MGToolTip et MGContextMenu peuvent couvrir certains cas

### 10. Contrôles de Validation et Décoration Manquants

#### 10.1 Adorner
**Description**: Couche de décoration au-dessus d'éléments
**Cas d'usage**: Indicateurs de validation, poignées de redimensionnement

#### 10.2 ValidationRule
**Description**: Règles de validation de données
**Note**: Peut être partiellement implémenté dans le système de binding

### 11. Contrôles de Présentation Manquants

#### 11.1 Thumb
**Description**: Contrôle de base pour le glisser-déposer
**Cas d'usage**: Curseurs personnalisés, poignées de redimensionnement

#### 11.2 RepeatButton
**Description**: Bouton qui déclenche des événements répétés quand maintenu
**Cas d'usage**: Boutons de défilement, incrémentation continue

### 12. Contrôles de Barre de Défilement Manquants

#### 12.1 ScrollBar
**Description**: Barre de défilement autonome
**Note**: Probablement intégré dans MGScrollViewer

### 13. Contrôles de Ribbon Manquants

#### 13.1 Ribbon
**Description**: Interface de type Office avec onglets et groupes
**Cas d'usage**: Applications de type Office, éditeurs complexes

## Résumé des Priorités

### Haute Priorité (Contrôles Fréquemment Utilisés)
1. **DataGrid** - Affichage de données tabulaires
2. **Label** - Support des mnémoniques
3. **ToolBar/StatusBar** - Shell d'application classique encore absent
4. **Ellipse/Path** - Formes vectorielles de base
5. **RepeatButton** - Utile pour le défilement et les interactions maintenues
6. **ViewBox** - Mise à l'échelle automatique encore absente

### Priorité Moyenne (Utiles mais Alternatives Possibles)
1. **Calendar/DatePicker** - Sélection de dates
2. **RichTextBox** - Texte formaté (MGTextBlock supporte déjà du formatage basique)
3. **ViewBox** - Mise à l'échelle automatique
4. **Popup** - Fenêtres popup (MGToolTip/MGContextMenu couvrent certains cas)
5. **Thumb** - Glisser-déposer
6. **MaskedTextBox** - Saisie spécialisée fréquente mais non standard WPF

### Basse Priorité (Spécialisés ou Moins Pertinents pour Jeux)
1. **Frame** - Navigation entre pages
2. **MediaElement** - MonoGame a ses propres systèmes
3. **FlowDocument*** - Documents complexes
4. **DocumentViewer** - Visualisation de documents
5. **InkCanvas** - Dessin à l'encre
6. **Ribbon** - Interface Office-like
7. **Adorner** - Décoration avancée

## Contrôles Spécifiques à MGUI (Non WPF)

MGUI inclut plusieurs contrôles qui n'existent pas dans WPF standard, adaptés aux besoins des jeux:
- **MGChatBox** - Chat de jeu
- **MGGridColorPicker** - Sélecteur de couleurs en grille
- **MGProgressButton** - Bouton avec barre de progression (cooldowns)
- **MGRatingControl** - Système de notation (étoiles)
- **MGSpoiler** - Contenu masquable
- **MGStopwatch/MGTimer** - Chronométrage
- **MGXAMLDesigner** - Designer XAML runtime

## Conclusion

MGUI implémente une base solide de contrôles WPF essentiels (environ 30+ contrôles). Les principaux manques concernent:
- **Layouts avancés restants** (ViewBox)
- **Shell d'application complémentaire** (ToolBar, StatusBar)
- **Données tabulaires** (DataGrid)
- **Formes vectorielles** (Ellipse, Path, Polygon)
- **Texte riche avancé** (RichTextBox, FlowDocument)

En revanche, la situation est meilleure que dans la version précédente de cette analyse sur plusieurs points importants: **TreeView**, **Menu/MenuItem**, **GridSplitter**, **WrapPanel**, **Canvas** et **NumericUpDown** disposent désormais d'équivalents fonctionnels dans le dépôt (`MGTreeView`, `MGMenuBar` / `MGMenuBarItem`, `MGGridSplitter`, `MGWrapPanel`, `MGCanvas`, `MGNumericUpDown`).

Pour un framework UI de jeu, MGUI couvre bien les besoins essentiels et ajoute des contrôles spécifiques aux jeux. Les contrôles manquants sont soit moins critiques pour les jeux, soit peuvent être implémentés avec les contrôles existants.

## Plan d'implémentation lié

Le plan d'exécution détaillé pour **WrapPanel**, **Canvas** et **NumericUpDown** a été exécuté et documenté dans `Docs/wrap-canvas-numericupdown-tasks.md`, avec statuts, commits, tests et samples.
