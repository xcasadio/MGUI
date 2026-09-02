# Taches restantes — ColorPicker V3

## Objectif

Etendre le ColorPicker avance de MGUI (`MGUI.Core/UI/Color/`, environ 40 fichiers) avec les fonctionnalites V3 volontairement laissees hors du perimetre V2 : surfaces de preview, outils d'authoring, simulations d'accessibilite et formats de persistence supplementaires. L'existant a respecter : `ColorValue` (float RGBA + color space, HDR/Linear), commit modes `Live`/`OnMouseRelease`/`ExplicitOkCancel`, undo hote via `IColorEditTransaction`, palettes JSON via `MGColorPaletteSerializer`, aucun acces filesystem dans MGUI.Core (l'hote fournit les hooks). Reference : `Docs/mgui_colorpicker_usage_guide.md` et la section ColorPicker de `Docs/controls-architecture.md`.

## Consignes de travail pour l'agent IA

- Executer les taches dans l'ordre.
- 1 commit par tache.
- Mettre a jour le statut de chaque tache dans ce fichier au fur et a mesure.
- Si une tache est bloquee, la marquer ⛔ avec une description du blocage.
- Pas de refactor hors perimetre.
- Ajouter des tests pour chaque tache (logique pure testable sans rendu, comme `MGColorPickerModel`).

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale

```bash
dotnet build MGUI.Core/MGUI.Core.csproj
dotnet test MGUI.Tests/MGUI.Tests.csproj --filter "FullyQualifiedName~Color"
```

## Taches

### ⚪ 1. Surfaces de preview

**But** : previsualiser la couleur en contexte, au-dela de l'actuel `MGColorPreview` (current/previous, checkerboard, tone-mapped).

**Travail attendu** :
- Sphere de preview "material" avec roughness/metalness configurables et eclairage studio neutre (rendu procedural cote MGUI, sans assets externes).
- Preview gizmo/debug : couleurs d'axes, couleurs de grille, transparence testee sur viewports sombre et clair.
- Hook de preview image custom fourni par l'editeur hote — MGUI.Core ne charge aucun fichier ; l'hote fournit la texture/le callback.
- Bande de preview sky/fog : blend horizon/zenith et simulation de depth ramp.

**Criteres d'acceptation** :
- Chaque surface reagit en mode `Live` sans allocation par frame (respecter le pattern de caches par taille/parametres des gradients existants dans `MGColorSlider`).
- Le hook image custom est utilisable sans reference MonoGame interdite dans MGUI.Core.

**Commit recommande** : `feat(colorpicker): add contextual preview surfaces`

### ⚪ 2. Outils d'authoring

**But** : outils de creation de couleurs derivees.

**Travail attendu** :
- Editeur de gradient : stops draggables, stops d'alpha, controles de midpoint, import/export (JSON, coherent avec `MGColorPaletteSerializer`).
- Color ramps (terrain, heatmaps, data viz, overlays de debug) generees depuis un gradient.
- Suggestions d'harmonie : complementaire, analogue, triadique, split-complementary (calculs purs sur `HsvColor`/`HslColor` existants).
- Generation de palette depuis une couleur selectionnee avec variantes de theme UI respectant le contraste (reutiliser `ColorContrastHelper`).

**Criteres d'acceptation** :
- Logique de gradient/harmonie/generation en classes pures testees sans rendu.
- Round-trip import/export du gradient.

**Commit recommande** : `feat(colorpicker): add gradient editor and harmony tools`

### ⚪ 3. Simulations d'accessibilite

**But** : verifier les choix de couleurs pour les utilisateurs daltoniens. Seul `ColorContrastHelper` (contraste WCAG valeur unique) existe aujourd'hui.

**Travail attendu** :
- Modes de preview daltonisme : protanopia, deuteranopia, tritanopia, achromatopsia (matrices de simulation appliquees a la couleur courante et aux palettes affichees).
- Overlays de contraste pour une palette complete, pas seulement la valeur active du picker.
- Avertissements quand des etats d'une palette debug/gizmo ne se distinguent que par la teinte.

**Criteres d'acceptation** :
- Les transformations de simulation sont des fonctions pures testees sur des valeurs de reference connues.
- L'overlay palette signale les paires sous le seuil de contraste choisi.

**Commit recommande** : `feat(colorpicker): add color blindness simulations and palette contrast overlays`

### ⚪ 4. Formats de persistence

**But** : interoperabilite au-dela du JSON actuel (`MGColorPaletteSerializer`).

**Travail attendu** :
- Format `.mgpalette` si le MVP JSON a besoin de metadata typees, de vignettes de preview ou de groupement editeur (a evaluer d'abord ; ne pas creer le format sans besoin concret).
- Import/export `.gpl` (GIMP palette) pour l'interop avec les outils d'art existants.
- Helpers de migration depuis des listes de swatches legacy une fois que de vraies palettes projet existent.
- Toujours via flux/chaines : aucun acces filesystem dans MGUI.Core (meme regle que `MGColorPaletteStore`).

**Criteres d'acceptation** :
- Round-trip `.gpl` sur des fichiers de reference ; les entrees invalides produisent des diagnostics sans exception (memes regles de tolerance que l'import JSON).

**Commit recommande** : `feat(colorpicker): add gpl import export and palette migration helpers`
