# Index de scenarios de validation

## Objectif

Cette page sert de point d'entree unique pour les scenarios de validation prioritaires du repo. Chaque scenario recoit un identifiant stable, un sample de repro ou de demonstration, un invariant principal, et une validation ciblee associee.

Quand un bug est rapporte, il doit autant que possible etre rattache a un identifiant de cette matrice plutot qu'a une description libre.

## Convention d'identifiants

- format: `SCN-<zone>-<nnn>` ;
- les titres de fenetres des samples prioritaires reprennent ces IDs pour aligner la doc, le compendium et les artefacts de debug ;
- les validations ciblees restent bornees: build du sample si necessaire, puis filtre de test le plus etroit possible.

## Matrice

| ID | Sous-systeme | Invariant principal | Point d'entree sample | Validation ciblee |
| --- | --- | --- | --- | --- |
| `SCN-FOCUS-001` | focus + input | le focus clavier reste explicable pendant les transitions combo, menu contextuel, popup et overlay | `MGUI.Samples/Features/FocusInputReview.xaml` | `FullyQualifiedName~Focus|FullyQualifiedName~Input|FullyQualifiedName~Overlay` |
| `SCN-OVERLAY-001` | overlay + popup | un overlay ou une fenetre imbriquee n'isole pas l'input de facon silencieuse et reste observable via les diagnostics desktop | `MGUI.Samples/Features/FocusInputReview.xaml` + `F2` pour dump diagnostics | `FullyQualifiedName~Focus|FullyQualifiedName~Overlay|FullyQualifiedName~Tooling` |
| `SCN-OVERLAY-002` | adorner-lite + tooling overlays | les selection boxes, resize handles et guides restent ancres sur leur cible, non interactifs, et le preview docking continue de reutiliser la meme abstraction d'ornement visuel | `MGUI.Samples/Features/AdornerLite.xaml` | `FullyQualifiedName~Overlay|FullyQualifiedName~Dock|FullyQualifiedName~Focus` |
| `SCN-THEME-001` | theme + template + ressources | un theme switch et un changement de template restent reproductibles sans reparse complet et sans perdre la precedence visible | `MGUI.Samples/Features/StyleThemeRefactor.xaml` | `FullyQualifiedName~Theme|FullyQualifiedName~Style|FullyQualifiedName~Template` |
| `SCN-MARKUP-001` | markup + loader XAML | le loader strict remonte un diagnostic structure pour type inconnu, setter invalide, racine invalide et part manquante, sans casser le chemin legacy | `MGUI.Samples/Dialogs/XAMLDesignerWindow.xaml` | `FullyQualifiedName~XAML|FullyQualifiedName~Markup|FullyQualifiedName~Template` |
| `SCN-LAYOUT-001` | layout | `ActualLayoutBounds` des enfants de contenu restent bornes par le `Padding` et les infos de layout restent verifiables en direct | `MGUI.Samples/Features/ActualLayoutBoundsTest.xaml` | build sample + verification manuelle de l'ecran |
| `SCN-GRID-001` | datagrid-lite + list view + scroll | la grille outillage v1 reste lisible sur dataset moyen, le tri de colonnes est explicable, la selection de ligne reste stable et `EnsureRowVisible` deplace effectivement le viewport | `MGUI.Samples/Controls/DataGridLite.xaml` | `FullyQualifiedName~Grid|FullyQualifiedName~ListView|FullyQualifiedName~Scroll` |
| `SCN-SHAPE-001` | shapes + clipping | les primitives arrondies et leurs clips restent coherents visuellement et servent de base aux futures shapes retained | `MGUI.Samples/Features/RoundedShapes.xaml` | `FullyQualifiedName~Shape|FullyQualifiedName~Clip` |
| `SCN-DOCK-001` | docking | drag, split, save/load de layout et reprise des panneaux restent demonstrables sur un host de docking cible | `MGUI.Samples/Features/DockingDemo.cs` | build sample + validation docking ciblee |
| `SCN-TEXT-001` | text | la selection et l'echappement des backslashes dans `TextBox` restent stables et demonstrables | `MGUI.Samples/Features/TextBoxBackslashTest.xaml` | build sample + filtre texte cible |
| `SCN-TEXT-002` | text surface lite: chat + log + texte annote | les usages chat, log et debug reutilisent un petit chemin texte explicite: runs programmes pour l'annotation, feed append-only pour le log, formatting inline optionnel pour les messages de chat, sans ouvrir un RichTextBox complet | `MGUI.Samples/Features/TextSurfaceLite.xaml` | `FullyQualifiedName~Text|FullyQualifiedName~Chat|FullyQualifiedName~Focus` |
| `SCN-EDITOR-RTB-001` | rich textbox editor | l'editeur RichTextBox sample reste editable, colore lexicalement et capable d'accepter une completion C# demo | `MGUI.Samples/Features/EditorRichTextBox.xaml` | build sample + `FullyQualifiedName~RichTextBox|FullyQualifiedName~Completion|FullyQualifiedName~Syntax` |

## Priorite immediate

Les scenarios a utiliser en priorite pour les chantiers deja ouverts par la roadmap sont:

- `SCN-FOCUS-001` et `SCN-OVERLAY-001` pour le harness diagnostics et les regressions input/focus ;
- `SCN-OVERLAY-002` pour la couche `Adorner-lite` et les decorators de tooling/debug ;
- `SCN-THEME-001` pour la convergence lookless ;
- `SCN-MARKUP-001` pour le durcissement du loader XAML ;
- `SCN-GRID-001` pour le chantier `DataGrid-lite` oriente outils/debug ;
- `SCN-TEXT-002` pour la cloture des ameliorations textuelles ciblees avant tout `RichTextBox` complet ;
- `SCN-EDITOR-RTB-001` pour la tranche RichTextBox editeur, coloration syntaxique et autocompletion ;
- `SCN-DOCK-001` comme point d'entree visible des futures validations docking ;
- `SCN-SHAPE-001` comme point d'entree visible du lot shapes retained.

## Usage pratique

- reproduire le bug dans le sample de la ligne correspondante ;
- capturer si possible un artefact ou un dump de diagnostic avec le meme identifiant de scenario ;
- executer ensuite la validation ciblee associee au scenario ;
- reporter l'identifiant de scenario dans la doc de fix, le ticket ou le commit concernes.