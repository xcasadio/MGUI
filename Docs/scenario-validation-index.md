# Index de scenarios de validation

## Objectif

Cette page sert de point d'entree unique pour les scenarios de validation prioritaires du repo. Chaque scenario recoit un identifiant stable, un sample de repro ou de demonstration, un invariant principal, et une validation ciblee associee.

Quand un bug est rapporte, il doit autant que possible etre rattache a un identifiant de cette matrice plutot qu'a une description libre.

## Convention d'identifiants

- format : `SCN-<zone>-<nnn>` ;
- les titres de fenetres des samples prioritaires reprennent ces IDs pour aligner la doc, le compendium et les artefacts de debug ;
- les validations ciblees restent bornees : build du sample si necessaire, puis filtre de test le plus etroit possible.

## Matrice

| ID | Sous-systeme | Invariant principal | Point d'entree sample | Validation ciblee |
| --- | --- | --- | --- | --- |
| `SCN-FOCUS-001` | focus + input | le focus clavier reste explicable pendant les transitions combo, menu contextuel, popup et overlay | `MGUI.Samples/Features/FocusInputReview.xaml` | `FullyQualifiedName~Focus\|FullyQualifiedName~Input\|FullyQualifiedName~Overlay` |
| `SCN-OVERLAY-001` | overlay + popup | un overlay ou une fenetre imbriquee n'isole pas l'input de facon silencieuse et reste observable via les diagnostics desktop | `MGUI.Samples/Features/FocusInputReview.xaml` + `F2` pour dump diagnostics | `FullyQualifiedName~Focus\|FullyQualifiedName~Overlay\|FullyQualifiedName~Tooling` |
| `SCN-OVERLAY-002` | adorner-lite + tooling overlays | les selection boxes, resize handles et guides restent ancres sur leur cible, non interactifs, et le preview docking continue de reutiliser la meme abstraction d'ornement visuel | `MGUI.Samples/Features/AdornerLite.xaml` | `FullyQualifiedName~Overlay\|FullyQualifiedName~Dock\|FullyQualifiedName~Focus` |
| `SCN-THEME-001` | theme + template + ressources | un theme switch et un changement de template restent reproductibles sans reparse complet et sans perdre la precedence visible | `MGUI.Samples/Features/StyleThemeRefactor.xaml` | `FullyQualifiedName~Theme\|FullyQualifiedName~Style\|FullyQualifiedName~Template` |
| `SCN-MARKUP-001` | markup + loader XAML | le loader strict remonte un diagnostic structure pour type inconnu, setter invalide, racine invalide, part manquante et nom d'element declare deux fois, sans casser le chemin legacy ; un echec qui n'apparait qu'a l'attachement de l'arbre (un `Name` dans un template d'item) se decrit avec le meme code et la meme position via `XamlLoaderDiagnostic.FromException` | `MGUI.Samples/Dialogs/XAMLDesignerWindow.xaml` | `FullyQualifiedName~XAML\|FullyQualifiedName~Markup\|FullyQualifiedName~Template` |
| `SCN-LAYOUT-001` | layout | `ActualLayoutBounds` des enfants de contenu restent bornes par le `Padding` et les infos de layout restent verifiables en direct | `MGUI.Samples/Features/ActualLayoutBoundsTest.xaml` | build sample + verification manuelle de l'ecran |
| `SCN-GRID-001` | datagrid-lite + list view + scroll | la grille outillage v1 reste lisible sur dataset moyen, le tri de colonnes est explicable, la selection de ligne reste stable et `EnsureRowVisible` deplace effectivement le viewport | `MGUI.Samples/Controls/DataGridLite.xaml` | `FullyQualifiedName~Grid\|FullyQualifiedName~ListView\|FullyQualifiedName~Scroll` |
| `SCN-SHAPE-001` | shapes + clipping | les primitives arrondies et leurs clips restent coherents visuellement et servent de base aux shapes retained | `MGUI.Samples/Features/RoundedShapes.xaml` | `FullyQualifiedName~Shape\|FullyQualifiedName~Clip` |
| `SCN-DOCK-001` | docking | drag, split, save/load de layout et reprise des panneaux restent demonstrables sur un host de docking cible | `MGUI.Samples/Features/DockingDemo.cs` | build sample + validation docking ciblee |
| `SCN-TEXT-001` | text | la selection et l'echappement des backslashes dans `TextBox` restent stables et demonstrables | `MGUI.Samples/Features/TextBoxBackslashTest.xaml` | build sample + filtre texte cible |
| `SCN-TEXT-002` | text surface lite : chat + log + texte annote | les usages chat, log et debug reutilisent un petit chemin texte explicite : runs programmes pour l'annotation, feed append-only pour le log, formatting inline optionnel pour les messages de chat, sans ouvrir un RichTextBox complet | `MGUI.Samples/Features/TextSurfaceLite.xaml` | `FullyQualifiedName~Text\|FullyQualifiedName~Chat\|FullyQualifiedName~Focus` |
| `SCN-ANIM-001` | animation | une page par capacite du moteur, chacune a valider a la main : (1) transitions XAML (easing nomme, litteral de Bezier, `Delay`, reciblage en cours de run) ; (2) animations explicites et API fluente (`Animate`, `Then`, `Wait`, `AutoReverse`, `Repeat`, annulation) ; (3) transform de rendu (translation, echelle, rotation, origine) et `RenderScale` d'etat ; (4) horloge du desktop (pause, `TimeScale`, compteur d'animations actives) ; (5) composition (storyboard parallele, sequence avec delai sur deux elements) ; (6) keyframes et clip JSON multi-pistes charge depuis un texte embarque ; (7) etats visuels nommes (`Hover`, `Pressed`, `Checked`, `OverridesLocalValue` face a un etat plain, slot `Checked` code-seul) ; (8) styles et theme (`Style.Transitions`/`Style.VisualStates` partages, `RefreshStyles`, groupe `Animation` du theme) ; (9) brushes animables (brush gelee partagee dont un seul cote anime, brush inline mutee par code, cibles de fond/gradient/bordure) ; (10) surbrillance (`AutoStart` vrai et faux, `StopOnMouseOver`, `ResumeBorderHighlight`) ; (11) preview et seek sans enregistrement aupres du manager ; (12) serialisation d'un storyboard en JSON puis rechargement ; (13) `ProgressButton.Duration` et texte revele sur l'horloge du moteur ; (14) diagnostics (`ApplicablePaths`, mesure des allocations d'une frame complete pendant une transition de couleur) ; (15) animations attendables (`PlayAsync`, sequence `await` de trois etapes, annulation par jeton) ; (16) defilement fluide (`ScrollTo`, molette et clavier avec `ScrollAnimationDuration`, ecriture exterieure qui reprend la main) ; (17) cadres de planche de sprites (`FrameGrid`, `Background.Texture.Frame` en boucle, pause) ; (18) transitions de layout (insertion, retrait, deplacement dans une liste, taille animee, element deplace qui ne glisse pas) ; (19) entree et sortie d'un element (fondu, echelle, glissement, sortie non interactive) ; (20) fenetres et liste deroulante (entree a l'ouverture, sortie avant retrait, reouverture pendant la sortie) ; (21) animations de popup par le theme (bascule du groupe `Animation`, tooltip, menu contextuel, liste deroulante) | `MGUI.Samples/Features/AnimationDemo.xaml` | build sample + `FullyQualifiedName~Animation\|~AnimationDemo` |
| `SCN-EDITOR-RTB-001` | rich textbox editor | l'editeur RichTextBox sample reste editable, colore lexicalement en C# comme en XAML (bouton de bascule) et capable d'accepter une completion C# demo, annulable par Ctrl+Z | `MGUI.Samples/Features/EditorRichTextBox.xaml` | build sample + `FullyQualifiedName~RichTextBox\|FullyQualifiedName~Completion\|FullyQualifiedName~Syntax` |

## Rattachement aux docs par theme

Chaque scenario se rattache a une doc d'architecture et, quand du travail reste ouvert, a un fichier de taches :

- `SCN-FOCUS-001`, `SCN-OVERLAY-001` : [input-architecture.md](input-architecture.md), [input-window-activation-design.md](input-window-activation-design.md) ;
- `SCN-THEME-001`, `SCN-MARKUP-001` : [styling-theme-architecture.md](styling-theme-architecture.md), taches [Tasks/styling-theme-tasks.md](Tasks/styling-theme-tasks.md) ;
- `SCN-OVERLAY-002`, `SCN-GRID-001`, `SCN-DOCK-001` : [controls-architecture.md](controls-architecture.md) ; vagues docking restantes dans [Tasks/styling-theme-tasks.md](Tasks/styling-theme-tasks.md) (taches 3, 8 et 9) ;
- `SCN-LAYOUT-001` : [layout-architecture.md](layout-architecture.md) ;
- `SCN-SHAPE-001` : [drawing-architecture.md](drawing-architecture.md) ;
- `SCN-TEXT-001`, `SCN-TEXT-002` : [text-architecture.md](text-architecture.md) ;
- `SCN-EDITOR-RTB-001` : taches [Tasks/richtextbox-autocomplete-tasks.md](Tasks/richtextbox-autocomplete-tasks.md) ;
- `SCN-ANIM-001` : [animation-architecture.md](animation-architecture.md), [decisions/0006-animation-system.md](decisions/0006-animation-system.md), [decisions/0007-animation-v2-composition-states-keyframes.md](decisions/0007-animation-v2-composition-states-keyframes.md), [decisions/0008-animation-v3-editor-readiness-and-remaining-limits.md](decisions/0008-animation-v3-editor-readiness-and-remaining-limits.md), [decisions/0009-animation-v4-freezable-brushes.md](decisions/0009-animation-v4-freezable-brushes.md), [decisions/0011-animation-v5.md](decisions/0011-animation-v5.md).

## Usage pratique

- reproduire le bug dans le sample de la ligne correspondante ;
- capturer si possible un artefact ou un dump de diagnostic avec le meme identifiant de scenario (`F2` dans les samples ecrit l'artefact diagnostics courant, voir `MGUI.Samples/Game1.cs`) ;
- executer ensuite la validation ciblee associee au scenario ;
- reporter l'identifiant de scenario dans la doc de fix, le ticket ou le commit concernes.
