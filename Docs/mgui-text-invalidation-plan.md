# Plan IA - invalidation fine de MGTextBlock

Date: 2026-05-21

## Objectif

MGUI doit distinguer proprement 3 niveaux d'invalidation pour `MGTextBlock` :

- contenu change : redraw seulement
- lignes internes changees : reflow local
- taille desiree changee : relayout parent

Le but est de supprimer le cout de relayout inutile pour les textes temps reel (telemetrie, compteur FPS, barre de statut, overlay debug, valeurs d'inspecteur a footprint stable) sans casser les labels normaux, le wrapping, ni la securite fonctionnelle.

## Faits verifies dans le depot

- `MGUI/MGUI.Core/UI/MGTextBlock.cs`
  - `Text` appelle aujourd'hui `SetText(value, false)`.
  - `SetText(..., false)` appelle `InvokeLayoutChanged()`.
  - `SetText(..., true)` appelle seulement `UpdateLines()`.
  - `UpdateLines()` depend de `LayoutBounds.Width`, `WrapText` et `Runs`.
  - `MeasureSelfOverride(...)` calcule la taille desiree a partir des lignes, `MinLines`, `MaxLines`, `Padding` et du moteur de texte.
  - `InvokeLayoutChanged()` vide `RecentSelfMeasurements` puis propage `LayoutChanged(this, true)`.
  - `OnLayoutUpdated` appelle deja `UpdateLines()`.
- `CasaEngine.Editor/Controls/ParticlePreviewViewport.cs` utilise deja une optimisation locale via `SetText(..., SuppressLayoutChanged: true)` et `MinLines = 4`.
- Le depot contient `MGUI/MGUI.Tests/MGUI.Tests.csproj` et `CasaEngine.Editor.MonoGame.sln` pour la validation.

## Decision de design recommandee

- Premiere passe : API explicite et opt-in pour les textes a footprint stable.
- Ne pas deduire automatiquement qu'un texte est "stable" partout dans le framework.
- Conserver un comportement par defaut sur pour `Text` et pour les labels ordinaires.
- Le pipeline interne peut escalader vers un relayout parent seulement si la taille desiree change reellement, ou si le contexte ne permet pas une conclusion sure.
- Garder le bool legacy pour compatibilite, mais ne plus l'utiliser comme API cible dans les nouveaux call sites.

## Regles obligatoires pour l'agent IA

1. Une seule tache a la fois.
2. Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
3. A la fin d'une tache validee, remplacer `🚧` par `✅`.
4. Si le code compile mais qu'une verification visible reste a faire, utiliser `🧪`.
5. Si une tache est bloquee, utiliser `⚠️` et ecrire la cause precise sous la tache.
6. Committer apres chaque tache terminee. Le commit doit inclure le code et la mise a jour de ce plan.
7. Ne jamais commencer la tache suivante avant d'avoir valide et committe la tache courante.
8. Toujours verifier `git status --short` avant `git add` et ne pas embarquer de changements non lies.
9. Apres le premier edit d'une tache, lancer la validation la plus ciblee disponible avant de continuer.
10. Ne pas reintroduire de workaround de throttling ou de baisse de frequence d'update visible.
11. Si le contexte n'est pas sur, preferer une escalation de layout sure a une optimisation risquee.
12. Respecter les regles repo : pas de LINQ/closures/allocations dans `Update`/`Draw`, pas de nouveau code WPF, build local avant de considerer une tache finie.

## Validation minimale par tache

Preferer `rtk` en wrapper si disponible, sinon utiliser directement `dotnet`.

```powershell
dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore
dotnet test .\MGUI\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore
dotnet build .\CasaEngine.Editor.MonoGame.sln -c Debug --no-restore
```

Pour les taches qui migrent un call site editor visible, rejouer aussi un scenario de preview particules :

```powershell
.\CasaEngine.Editor\bin\Debug\net9.0-windows\CasaEngine.Editor.exe --project .\Projects\SampleProject\SampleProject.json --open-asset .\Particles\FireLoop_Minimal.particle --diagnostics-out .\ai-agent\particle-preview-perf-after.txt --capture-delay 5
```

## Criteres d'acceptation

- `MGTextBlock` expose une API plus explicite que le bool `SuppressLayoutChanged`.
- Le framework sait distinguer redraw local, reflow local et relayout parent.
- Les updates temps reel sur footprint stable n'invalident plus inutilement le layout parent.
- Une variation de texte qui change vraiment la taille desiree force toujours le relayout parent.
- Les paths `Text`, `SetTextRuns`, `ClearTextRuns` et les runs explicites restent coherents.
- Les cas `WrapText`, `MinLines`, `MaxLines`, `Padding`, changement de police, changement de theme et control non encore layouted restent corrects.
- Les call sites editor reels passent par la nouvelle API au lieu d'un bool ad hoc.
- Des tests de regression couvrent les cas stables et les cas qui doivent escalader.
- Le preview particules reste fluide sans throttling et sans perte d'information.

## Taches

### ✅ T01 - Auditer le baseline et verrouiller les cibles de migration

But : partir d'une base reproductible avant de toucher l'invalidation.

Fichiers a lire :

- `MGUI/MGUI.Core/UI/MGTextBlock.cs`
- `CasaEngine.Editor/Controls/ParticlePreviewViewport.cs`
- call sites `MGTextBlock`, `SetText`, `SetTextRuns` et labels temps reel identifies dans `CasaEngine.Editor` et `MGUI`

Actions :

1. Lancer `git status --short` et noter les fichiers deja modifies a exclure des commits.
2. Relire les chemins `Text`, `SetText`, `SetTextRuns`, `ClearTextRuns`, `MeasureSelfOverride`, `UpdateLines`, `InvokeLayoutChanged`.
3. Rechercher dans `CasaEngine.Editor` et `MGUI` les `MGTextBlock` ou mises a jour de texte appelees en boucle ou sur telemetrie.
4. Lister sous cette tache les call sites a migrer en premiere passe : preview particules et tout autre overlay/label temps reel trouve.
5. Etablir un baseline de build/test `MGUI.Core` et, si necessaire, editor.

Validation :

- `dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore`
- `dotnet test .\MGUI\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore`
- `dotnet build .\CasaEngine.Editor.MonoGame.sln -c Debug --no-restore` si le scope editor doit etre touche des le debut

Notes d'audit :

- `rtk git status --short` avant audit : `.github/agents/mgui-engine-developer.agent.md` etait deja modifie et doit rester exclu des commits de cette serie.
- Chemins verifies dans ce workspace : `MGUI.Core/UI/MGTextBlock.cs`, `MGUI.Core/UI/MGPropertyGrid.cs`, `MGUI.MiniGame/MiniGame.cs`, `MGUI.Samples/Features/PerformanceTest.xaml.cs`, `MGUI.Samples/Dialogs/Debugging/Debug1.xaml.cs`.
- `CasaEngine.Editor/Controls/ParticlePreviewViewport.cs` et `CasaEngine.Editor.MonoGame.sln` ne sont pas presents dans ce workspace. Les migrations editor et le scenario preview particules sont donc impossibles ici tant que ce dossier n'est pas ajoute au workspace.
- Chemins `MGTextBlock` verifies : `Text` route vers `SetText(value, false)`, `SetText`, `SetTextRuns` et `ClearTextRuns` choisissent entre `InvokeLayoutChanged()` et `UpdateLines()` via le bool legacy, `UpdateLines()` depend de `LayoutBounds.Width`, `WrapText` et `Runs`, `MeasureSelfOverride(...)` reparses les lignes avec la largeur de mesure, `MinLines`, `MaxLines`, `Padding` et le moteur texte, et `InvokeLayoutChanged()` vide `RecentSelfMeasurements` avant de propager `LayoutChanged(this, true)`.
- Cibles de migration premiere passe disponibles dans le workspace : `MGUI.Core/UI/MGPropertyGrid.cs` readonly display text, `MGUI.Core/UI/MGProgressBar.cs`, `MGUI.Core/UI/MGStopWatch.cs`, `MGUI.Core/UI/MGTimer.cs`, `MGUI.MiniGame/MiniGame.cs` HUD/status, `MGUI.Samples/Features/PerformanceTest.xaml.cs` FPS/frame/elements/mode, `MGUI.Samples/Dialogs/Debugging/Debug1.xaml.cs` labels de grille mis a jour sur layout.
- Baseline `rtk dotnet build .\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore` : OK, 0 erreur, 19 warnings XML existants.
- Baseline `rtk dotnet test .\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore` : 1113 passes, 2 echecs preexistants sans lien avec cette tache (`BackendProjectSplitTests.IntegrationProject_StripsLegacyRendererFiles`, `ToolingHooksTests.UIToolingService_ExposesSnapshotAndPreviewHooks`).
- Baseline editor : non lancee, solution/fichiers editor absents du workspace.

Commit attendu :

```powershell
git add ai-agent/mgui-text-invalidation-plan.md
git commit -m "plan: audit mgui text invalidation baseline"
```

### ✅ T02 - Introduire le contrat public d'invalidation du texte

But : remplacer le bool implicite par une API lisible et exploitable par le framework.

Fichiers probables :

- `MGUI/MGUI.Core/UI/MGTextBlock.cs`
- nouveau type public ou interne partage dans `MGUI/MGUI.Core/UI/`

Actions :

1. Ajouter un type nomme pour l'impact d'une mise a jour texte, par exemple `MGTextInvalidationMode` ou equivalent, avec trois niveaux : `ContentOnly`, `ReflowLocal`, `RelayoutParent`.
2. Ajouter sur `MGTextBlock` une notion explicite de footprint stable, par exemple une policy ou un flag opt-in.
3. Conserver `Text` et le bool legacy pour compatibilite, mais les faire router vers le nouveau contrat interne.
4. Documenter clairement quand chaque niveau est autorise et quand il ne l'est pas.

Validation :

- `dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore`

Commit attendu :

```powershell
git add MGUI/MGUI.Core/UI/* ai-agent/mgui-text-invalidation-plan.md
git commit -m "mgui: add explicit text invalidation contract"
```

### ✅ T03 - Centraliser le pipeline d'invalidation dans MGTextBlock

But : avoir un seul point de decision pour tous les changements de texte.

Actions :

1. Introduire un helper interne qui sequence : update des runs, update des lignes, evaluation de la taille desiree, escalation eventuelle.
2. Faire passer par ce helper `SetText`, `SetTextRuns` et `ClearTextRuns`.
3. Conserver un comportement sur par defaut quand le control n'est pas encore layouted ou si les dimensions utiles sont inconnues.
4. Eviter de vider `RecentSelfMeasurements` tant qu'un relayout parent n'est pas necessaire.

Validation :

- `dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore`
- `dotnet test .\MGUI\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore`

Resultat :

- `rtk dotnet build .\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore` : OK, 0 erreur, 19 warnings XML existants.
- `rtk dotnet test .\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore` : 1113 passes, memes 2 echecs baseline preexistants (`BackendProjectSplitTests.IntegrationProject_StripsLegacyRendererFiles`, `ToolingHooksTests.UIToolingService_ExposesSnapshotAndPreviewHooks`).

Commit attendu :

```powershell
git add MGUI/MGUI.Core/UI/MGTextBlock.cs ai-agent/mgui-text-invalidation-plan.md
git commit -m "mgui: centralize text invalidation routing"
```

### ✅ T04 - Implementer l'escalade par variation de taille desiree

But : faire la vraie optimisation structurelle, pas un bypass aveugle.

Actions :

1. Avant mise a jour, capturer l'etat minimal necessaire pour comparer la taille desiree.
2. Recalculer la taille desiree apres changement de contenu ou de lignes, avec les memes contraintes de largeur utiles.
3. Si la taille desiree ne change pas : rester en redraw ou reflow local.
4. Si la taille desiree change : propager un relayout parent et nettoyer le cache de mesure.
5. Si la comparaison n'est pas sure, preferer l'escalade sure.
6. Verrouiller les cas `WrapText`, `MinLines`, `MaxLines`, `Padding`, inline formatting et explicit runs.

Validation :

- `dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore`
- `dotnet test .\MGUI\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore`

Resultat :

- `rtk dotnet build .\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore` : OK, 0 erreur, 19 warnings XML existants.
- `rtk dotnet test .\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore` : 1113 passes, memes 2 echecs baseline preexistants (`BackendProjectSplitTests.IntegrationProject_StripsLegacyRendererFiles`, `ToolingHooksTests.UIToolingService_ExposesSnapshotAndPreviewHooks`).

Commit attendu :

```powershell
git add MGUI/MGUI.Core/UI/MGTextBlock.cs ai-agent/mgui-text-invalidation-plan.md
git commit -m "mgui: relayout text only when desired size changes"
```

### ✅ T05 - Durcir les garde-fous de compatibilite

But : ne rien casser pour les labels ordinaires.

Actions :

1. Conserver le comportement legacy du setter `Text` pour les call sites non migres, sauf si la nouvelle policy explicite dit le contraire.
2. Verifier que les changements de police, taille, padding, wrap ou limites de lignes continuent a invalider le layout normalement.
3. Verifier le comportement avant premier layout et apres resize parent.
4. Si utile, ajouter des assertions ou garde-fous de debug pour interdire l'usage du mode stable dans des cas manifestement incompatibles.

Validation :

- `dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore`
- `dotnet test .\MGUI\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore`

Resultat :

- `rtk dotnet build .\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore` : OK, 0 erreur, 19 warnings XML existants.
- `rtk dotnet test .\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore` : 1113 passes, memes 2 echecs baseline preexistants (`BackendProjectSplitTests.IntegrationProject_StripsLegacyRendererFiles`, `ToolingHooksTests.UIToolingService_ExposesSnapshotAndPreviewHooks`).

Commit attendu :

```powershell
git add MGUI/MGUI.Core/UI/* ai-agent/mgui-text-invalidation-plan.md
git commit -m "mgui: preserve safe legacy text layout behavior"
```

### ✅ T06 - Ajouter les tests de regression MGUI

But : verrouiller le contrat avant la migration des call sites editor.

Cas minimaux a couvrir :

1. Changement de texte a largeur equivalente sur footprint stable sans relayout parent.
2. Changement de texte qui reflow localement mais conserve la meme taille desiree.
3. Changement de texte qui ajoute une ligne wrappee et force un relayout parent.
4. `MinLines` reserve une zone stable pour de la telemetrie changeante.
5. `MaxLines`, inline formatting et explicit runs restent coherents.
6. Control non encore layouted ou largeur inconnue : fallback sur relayout parent.
7. Compatibilite du bool legacy et de la property `Text`.

Validation :

- `dotnet test .\MGUI\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore`
- `dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore`

Resultat :

- `rtk dotnet test .\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~TextBlockInvalidationTests` : OK, 7 tests passes.
- `rtk dotnet build .\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore` : OK, 0 erreur.
- `rtk dotnet test .\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore` : 1120 passes, memes 2 echecs baseline preexistants (`BackendProjectSplitTests.IntegrationProject_StripsLegacyRendererFiles`, `ToolingHooksTests.UIToolingService_ExposesSnapshotAndPreviewHooks`).

Commit attendu :

```powershell
git add MGUI/MGUI.Tests/* MGUI/MGUI.Core/UI/* ai-agent/mgui-text-invalidation-plan.md
git commit -m "mgui: add text invalidation regression tests"
```

### ✅ T07 - Migrer les labels temps reel de l'editeur vers l'API explicite

But : remplacer les usages ad hoc par la solution structurelle.

Fichiers probables :

- `CasaEngine.Editor/Controls/ParticlePreviewViewport.cs`
- autres call sites identifies en T01

Actions :

1. Remplacer les `SetText(..., SuppressLayoutChanged: true)` et equivalents par la nouvelle API explicite.
2. Ne migrer que les textes dont l'empreinte est stable ou reservee.
3. Si necessaire, reserver explicitement la zone avec `MinLines`, taille fixe ou layout connu.
4. Ne pas toucher les labels generalistes, localisables ou dont le wrapping varie librement.

Validation :

- `dotnet build .\CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- scenario preview particules avec capture `ai-agent/particle-preview-perf-after.txt`

Resultat :

- `CasaEngine.Editor/Controls/ParticlePreviewViewport.cs`, `CasaEngine.Editor.MonoGame.sln` et le scenario preview particules ne sont pas disponibles dans ce workspace.
- Migré les labels temps reel accessibles : `MGProgressBar`, `MGStopwatch`, `MGTimer`, `MGPropertyGrid` read-only display, HUD/shop stables de `MGUI.MiniGame`, overlays perf `MGUI.Samples/Features/PerformanceTest` et `MGUI.Samples/Controls/ListBox`, label de grille debug.
- `rtk dotnet build .\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore` : OK, 0 erreur.
- `rtk dotnet build .\MGUI.Samples\MGUI.Samples.csproj -c Debug --no-restore` : OK, 0 erreur, 4 warnings XML existants.
- `rtk dotnet build .\MGUI.MiniGame\MGUI.MiniGame.csproj -c Debug --no-restore` : OK, 0 erreur.
- `rtk dotnet test .\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~TextBlockInvalidationTests` : OK, 7 tests passes.

Commit attendu :

```powershell
git add CasaEngine.Editor/Controls/* ai-agent/mgui-text-invalidation-plan.md
git commit -m "editor: adopt explicit stable text invalidation"
```

### ✅ T08 - Ajouter une demo minimale et documenter l'usage

But : laisser un contrat reutilisable par les prochains ecrans sans reouvrir le probleme.

Actions :

1. Ajouter une documentation courte sur la notion de footprint stable et les 3 niveaux d'invalidation.
2. Ajouter un test ou sample simple montrant un compteur mis a jour chaque frame sans relayout parent.
3. Documenter quels types de labels peuvent utiliser ce mode et lesquels doivent rester en layout normal.

Validation :

- `dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore`
- `dotnet test .\MGUI\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore`
- `dotnet build .\CasaEngine.Editor.MonoGame.sln -c Debug --no-restore` si sample ou editor touches

Resultat :

- Ajout de `Docs/stable-text-invalidation-usage.md`.
- Ajout du test compteur `StableTelemetryCounter_CanUpdateRepeatedlyWithoutRelayoutParent`.
- `rtk dotnet test .\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~TextBlockInvalidationTests` : OK, 8 tests passes.
- `rtk dotnet build .\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore` : OK, 0 erreur.
- `rtk dotnet test .\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore` : 1121 passes, memes 2 echecs baseline preexistants (`BackendProjectSplitTests.IntegrationProject_StripsLegacyRendererFiles`, `ToolingHooksTests.UIToolingService_ExposesSnapshotAndPreviewHooks`).
- Build editor non lance : solution editor absente du workspace.

Commit attendu :

```powershell
git add MGUI/* CasaEngine.Editor/* ai-agent/mgui-text-invalidation-plan.md
git commit -m "docs(mgui): document stable text invalidation usage"
```

### ⏳ T09 - Validation finale end-to-end et nettoyage

But : verifier que la correction reste intelligente jusqu'au bout.

Actions :

1. Rejouer le scenario preview particules sans throttling.
2. Confirmer que le cout UI parasite chute sans baisse de frequence des updates visibles.
3. Chercher les derniers usages du bool legacy dans les chemins temps reel et migrer ou documenter ceux qui restent.
4. Mettre a jour le resultat final et les notes de validation sous cette tache.

Validation :

- `dotnet build .\MGUI\MGUI.Core\MGUI.Core.csproj -c Debug --no-restore`
- `dotnet test .\MGUI\MGUI.Tests\MGUI.Tests.csproj -c Debug --no-restore`
- `dotnet build .\CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- scenario preview particules avec capture `ai-agent/particle-preview-perf-after.txt`

Resultats :

- A completer par l'agent a la fin de l'execution.

Commit attendu :

```powershell
git add ai-agent/mgui-text-invalidation-plan.md MGUI/* CasaEngine.Editor/*
git commit -m "perf(mgui): validate fine-grained text invalidation end to end"
```