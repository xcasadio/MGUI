# Taches roadmap transverse

## Objectif

Backlog de portefeuille pour les ameliorations transverses encore ouvertes, c'est-a-dire le travail qui traverse plusieurs sous-systemes et qui n'appartient a aucun backlog specialise. Quand un chantier possede un backlog dedie dans `Docs/Tasks/`, ce backlog reste la source de verite d'implementation ; ce fichier ne duplique pas leurs taches :

- input et focus : livres (fichiers de taches retires ; voir `Docs/input-architecture.md` et `Docs/input-window-activation-design.md`)
- style, theme, templates : `Docs/Tasks/styling-theme-tasks.md`
- rendu et dessin : livres (fichiers de taches retires ; voir `Docs/rendering-architecture.md` et `Docs/drawing-architecture.md`)
- editeur RichTextBox : `Docs/Tasks/richtextbox-autocomplete-tasks.md`
- graph view : `Docs/Tasks/graph-tasks.md`
- color picker v3 : `Docs/Tasks/colorpicker-v3-tasks.md`

Les validations s'appuient sur les scenarios stables de `Docs/scenario-validation-index.md` (convention `SCN-<zone>-<nnn>`).

## Historique du fichier

- 2 septembre 2026 : creation par consolidation (commit `6c6d9ae`).
- 7 septembre 2026 : les deux taches que ce fichier portait (seconde vague docking, surfaces auxiliaires) dupliquaient les taches du backlog styling/theme et decrivaient a tort les cinq controles docking feuilles comme une premiere vague structurelle deja livree (ils ne consomment que des applicateurs de defaults). Elles sont fusionnees dans `Docs/Tasks/styling-theme-tasks.md` (taches 9 et 12) ; ce fichier ne garde que les renvois ci-dessous.

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

Aucune tache transverse propre a ce fichier n'est ouverte. Les chantiers suivis ailleurs, par priorite :

### Priorite haute

- Migration structurelle des controles docking (vocabulaire des parts, feuilles, puis `MGDockTabGroup`, `MGDockHost`, `MGDockSplitContainer`) : `Docs/Tasks/styling-theme-tasks.md`, taches 3, 8 et 9. Validation `SCN-DOCK-001` (sample `MGUI.Samples/Features/DockingDemo.cs`) et tests `MGUI.Tests/Docking`.

### Priorite moyenne

- Cloture de la modelisation des composites a surfaces auxiliaires (`MGFloatingDockWindow`, `MGColorPickerPopup`, contrat "part requise de type `MGWindow`", cycle de vie des surfaces) : `Docs/Tasks/styling-theme-tasks.md`, tache 12. Validation `SCN-THEME-001`.
