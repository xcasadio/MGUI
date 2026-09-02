# Taches layout et responsive

## Objectif

Completer le systeme de layout responsive (voir `Docs/layout-architecture.md`) sur ses deux manques verifies : le scaling des espacements possedes par les containers, et la couverture de tests de regression du layout sous scale responsive.

## Consignes de travail pour l'agent IA

- Executer les taches dans l'ordre.
- Faire exactement 1 commit par tache.
- Mettre a jour le statut de chaque tache dans ce fichier (⚪ -> 🟡 -> ✅).
- En cas de blocage : marquer ⛔ et decrire le blocage sous la tache.
- Pas de refactor hors perimetre : ne pas toucher au resolveur (`UIResponsiveResolver`), au modele (`UIResponsiveSettings`, `UIResolvedMetrics`) ni a `MGWindow.Scale`.
- Ajouter des tests pour tout comportement nouveau ou modifie.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale

1. `dotnet build .\MGUI.Tests\MGUI.Tests.csproj`
2. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj`
3. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj`

## Taches

### 1. ⚪ Scaler les espacements possedes par les containers en sous-arbre responsive

But: aujourd'hui le scaling responsive est centralise dans `MGElement` (margins, padding, tailles preferees/min/max via `ResolvedMargin`, `ResolvedPadding`, `Resolved*Width/Height`), mais les espacements possedes par les containers restent en pixels bruts. Dans un sous-arbre responsive, `MGStackPanel.Spacing = 8` ne suit donc pas le `UIScaleFactor` alors que les margins voisines le suivent, ce qui desequilibre les ecrans quand le viewport diverge de la design resolution.

Travail attendu:

- Introduire dans le pipeline de mesure/arrangement de chaque container concerne une valeur resolue (pattern des `Resolved*` de `MGUI.Core/UI/MGElement.cs`), calculee avec `ResponsiveSpacingScaleFactor` et `UIResponsiveMath.ScaleInt`, gated par `ScaleSpacingWithResponsive` :
  - `MGUI.Core/UI/Containers/MGStackPanel.cs` : `Spacing` ;
  - `MGUI.Core/UI/Containers/Grids/MGGrid.cs` : `RowSpacing`, `ColumnSpacing` (attention a la contrainte `GridLineMargin < RowSpacing/ColumnSpacing` documentee dans le fichier) ;
  - `MGUI.Core/UI/Containers/Grids/MGUniformGrid.cs` : `RowSpacing`, `ColumnSpacing` ;
  - `MGUI.Core/UI/Containers/MGWrapPanel.cs` et `MGUI.Core/UI/Containers/VirtualizingWrapPanel.cs` : `Spacing`.
- Ne pas changer la valeur stockee ni la surface publique : les proprietes restent des valeurs design-space ; seule la consommation dans mesure/arrangement passe par la valeur resolue.
- Comportement strictement inchange hors sous-arbre responsive (facteur 1.0).

Criteres d'acceptation:

- Dans un sous-arbre responsive avec `UIScaleFactor != 1.0`, les espacements de `MGStackPanel`, `MGGrid`, `MGUniformGrid` et `MGWrapPanel` sont scales de maniere coherente avec les margins/paddings.
- `ScaleSpacingWithResponsive = false` sur le container restaure les pixels bruts.
- Les arbres non responsive produisent des bounds identiques a avant (verifie par test).
- Tests unitaires ajoutes couvrant au moins `MGStackPanel.Spacing` et `MGGrid.RowSpacing`/`ColumnSpacing` sous scale.

Commit recommande:

- `feat: scale container-owned spacing under responsive layout`

### 2. ⚪ Completer la couverture de tests de regression layout sous scale responsive

But: la seule suite responsive existante est `MGUI.Tests/Architecture/ResponsiveMetricsResolverTests.cs` (formules du resolveur, clamps UI/texte independants, toggle DPI, bounds des anchors via `MGOverlayPanel.CreateAnchoredBounds`). Rien ne couvre le comportement des containers sous scale global, la mesure du texte sous `TextScaleFactor`, ni les layouts complets en ratios extremes — les regressions de bounds passeraient inapercues.

Travail attendu:

- Ajouter des tests de layout (calculs et bounds, pas de captures visuelles) couvrant :
  - un container standard (`MGStackPanel`, `MGGrid`) mesure/arrange dans un sous-arbre responsive avec `UIScaleFactor` < 1 et > 1 : verifier margins/padding/tailles resolues et positions des enfants ;
  - la mesure de `MGTextBlock` avec `UseResponsiveTextScale` actif : `EffectiveFontSize` et la taille mesuree suivent `TextScaleFactor` independamment du `UIScaleFactor` ;
  - des viewports a ratios extremes (ultra-wide type 3440x1440, portrait type 1080x1920, tres petite fenetre) appliques a un petit arbre `MGResponsiveRoot` + layouts + un enfant ancre : bounds stables et dans le viewport ;
  - la non-regression des arbres non opt-in : memes bounds avec et sans metriques non-identite sur le desktop.
- S'appuyer sur l'infrastructure de tests headless existante : `GraphTestRuntime` (`MGUI.Tests/Graph`) + `new MGDesktop(runtime)` + `desktop.Update()`, pattern illustre par `MGUI.Tests/Architecture/ScrollViewerLayoutTests.cs` ; piloter les metriques via `desktop.ResponsiveSettings` et la taille du runtime.

Criteres d'acceptation:

- Les principaux calculs responsive (containers, texte, anchors en situation, ratios extremes) sont couverts par des tests reproductibles et independants du rendu.
- Une regression de bounds ou de mesure sous scale responsive fait echouer `dotnet test .\MGUI.Tests\MGUI.Tests.csproj`.

Commit recommande:

- `test: add responsive layout regression coverage`
