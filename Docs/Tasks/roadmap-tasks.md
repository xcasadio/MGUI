# Taches roadmap transverse

## Objectif

Backlog de portefeuille pour les ameliorations transverses encore ouvertes, c'est-a-dire le travail qui traverse plusieurs sous-systemes et qui n'appartient a aucun backlog specialise. Quand un chantier possede un backlog dedie dans `Docs/Tasks/`, ce backlog reste la source de verite d'implementation ; ce fichier ne duplique pas leurs taches :

- input et focus : `Docs/Tasks/input-tasks.md`
- style, theme, templates : `Docs/Tasks/styling-theme-tasks.md`
- rendu et dessin : `Docs/Tasks/rendering-tasks.md`, `Docs/Tasks/drawing-tasks.md`
- editeur RichTextBox : `Docs/Tasks/richtextbox-autocomplete-tasks.md`
- graph view : `Docs/Tasks/graph-tasks.md`
- color picker v3 : `Docs/Tasks/colorpicker-v3-tasks.md`

Les validations s'appuient sur les scenarios stables de `Docs/scenario-validation-index.md` (convention `SCN-<zone>-<nnn>`).

## Consignes de travail pour l'agent IA

- Executer les taches dans l'ordre.
- Faire exactement 1 commit git par tache terminee.
- Mettre a jour le statut de la tache dans ce fichier avant chaque commit.
- Si une tache est bloquee, la marquer `⛔`, decrire le blocage juste sous la tache, puis s'arreter.
- Pas de refactor hors perimetre de la tache en cours.
- Ajouter ou adapter des tests dans la meme tache.
- Ne lancer qu'un chantier principal a la fois ; faire converger tests, samples et documentation dans le meme lot.
- Rattacher chaque validation a un identifiant `SCN-*` de `Docs/scenario-validation-index.md`.
- Garde-fous de perimetre permanents du framework (decisions actees, ne pas rouvrir sans decision explicite) :
  - pas de `DependencyObject`/`DependencyProperty` generalises ;
  - pas de moteur complet de triggers facon WPF (Trigger/DataTrigger/EventTrigger/storyboards) ;
  - pas de routed commands ni routed events generalises ;
  - pas de portage des familles desktop `DocumentViewer`, `Frame`, `Page`, `Ribbon`, `FlowDocument` ;
  - pas de retained rendering sophistique ni dirty regions generalises sans profilage MonoGame prealable ;
  - pas de document model, pagination ni flow document dans la surface texte.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale

1. `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
2. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
3. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "FullyQualifiedName~Theme|FullyQualifiedName~Style|FullyQualifiedName~Template|FullyQualifiedName~Dock"`

## Taches

### Priorite haute

### ⚪ 1. Migrer la deuxieme vague docking vers des templates structurels

But :
finir de sortir le chrome imperatif des controles de docking. La premiere vague est deja en place : `MGDockTabItem`, `MGDockSplitterBar`, `MGDockAutoHideStrip`, `MGDockAutoHideDrawer` et `MGDockDropIndicators` declarent un `DefaultControlTemplateName` et consomment les templates `Dock.*.Default` de `MGUI.Core/UI/Styling/MGControlTemplateCatalog.cs`, avec variantes sombres dans `MGUI.Core/UI/Templates/BuiltInControlTemplates.xaml`. Restent hybrides : `MGDockTabGroup` (enregistre ses parts `Accent`, `DropdownIcon`, `WindowStateIcon` mais construit son chrome sans template), `MGDockHost` (enregistre ses parts preview overlay, drop indicators, strips et drawer auto-hide, sans template par defaut) et `MGDockSplitContainer` (aucune part declaree).

Travail attendu :

- migrer d'abord `MGDockTabGroup` (`MGUI.Core/UI/Docking/Controls/MGDockTabGroup.cs`) : ajouter un template `Dock.TabGroup.Default` au catalogue, y deplacer les defaults visuels, poser `DefaultControlTemplateName`, conserver la logique de drag, selection et fermeture dans le controle ;
- statuer sur `MGDockHost` et `MGDockSplitContainer` : identifier la part de chrome reellement templatable et soit la migrer sur le meme modele, soit documenter dans `Docs/styling-theme-architecture.md` pourquoi le controle reste structure pure (container de layout sans chrome) ;
- ajouter les variantes `Dark.*` correspondantes dans `BuiltInControlTemplates.xaml` pour tout nouveau template ;
- ajouter des tests d'infrastructure verifiant l'enregistrement des nouveaux templates par defaut (voir `MGUI.Tests/Architecture/ControlTemplateInfrastructureTests.cs`).

Criteres d'acceptation :

- `MGDockTabGroup` declare son template structurel et ses defaults visuels ne vivent plus dans le constructeur ;
- une decision par controle restant (migre ou hors chrome) est actee dans la doc d'architecture ;
- drag, split, auto-hide, pin/close restent stables : `SCN-DOCK-001` (sample `MGUI.Samples/Features/DockingDemo.cs`) + tests `MGUI.Tests/Docking` verts.

Commit recommande : `dock: migrate second docking template wave`

### Priorite moyenne

### ⚪ 2. Cloturer la modelisation des composites a surfaces auxiliaires

But :
fermer la derniere zone ouverte de la convergence lookless. Le contrat de template couvre deja les parts hors sous-arborescence unique (`MGControlTemplateStructure.DetachedRoots` dans `MGUI.Core/UI/Styling/MGControlTemplate.cs`) et les controles a surfaces auxiliaires de reference consomment des templates structurels (`MGContextMenu`, `MGToolTip`, `MGComboBox`, `MGOverlay` posent tous `DefaultControlTemplateName`). Ce qui manque : un inventaire explicite des composites restants qui pilotent popups, overlays ou fenetres imbriquees hors du contrat, et la documentation des contraintes de cycle de vie de ces surfaces.

Travail attendu :

- inventorier les controles qui ouvrent encore des fenetres imbriquees, popups ou overlays sans passer par le contrat de template, et classer chaque cas : deja supportable par `DetachedRoots`, ou demandant une extension minimale du runtime ;
- documenter dans `Docs/styling-theme-architecture.md` les contraintes de parentage, d'ouverture et de fermeture des surfaces auxiliaires templatees, y compris le cas des controles derives d'un type deja template (`MGContextMenu` herite de `MGWindow` et doit tolerer la phase de template de base pendant sa construction) ;
- etendre la validation de template aux contraintes comportementales inter-parts la ou c'est peu couteux, sinon acter la limite dans la doc ;
- si une extension du runtime s'avere necessaire, produire la tache bornee correspondante dans `Docs/Tasks/styling-theme-tasks.md` plutot que de l'implementer ici.

Criteres d'acceptation :

- l'inventaire distingue clairement cas supportes et cas demandant une extension ;
- les contraintes de cycle de vie des surfaces auxiliaires sont documentees ;
- validation ciblee verte : `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "FullyQualifiedName~Theme|FullyQualifiedName~Style|FullyQualifiedName~Template"` (`SCN-THEME-001`).

Commit recommande : `templates: close auxiliary surface modeling`
