# MGUI — ColorPicker avancé pour moteur moderne

## Objectif

Remplacer ou compléter le `MGGridColorPicker` existant par une suite de contrôles couleur beaucoup plus complète, adaptée à un éditeur de moteur moderne.

Le contrôle actuel semble surtout orienté **grille de couleurs / palettes prédéfinies**.  
La nouvelle cible doit être un véritable **Color Editor** utilisable dans :

- une PropertyGrid ;
- un éditeur de matériaux ;
- un éditeur de lumières ;
- un éditeur de thèmes UI ;
- un éditeur de scènes ;
- un éditeur de particules ou d’effets visuels ;
- des outils de debug/gizmos.

L’objectif n’est pas de créer un contrôle monolithique, mais une architecture composée de plusieurs contrôles spécialisés et réutilisables.

---

## Contrôles recommandés

### `MGColorField`

Champ compact destiné à être utilisé dans une PropertyGrid ou dans des formulaires d’édition.

Responsabilités :

- afficher une swatch couleur ;
- afficher l’alpha sur damier ;
- ouvrir un popup d’édition ;
- permettre une édition texte rapide ;
- supporter le binding two-way ;
- supporter les états mixed/null/default.

### `MGColorPicker`

Contrôle principal d’édition visuelle de couleur.

Responsabilités :

- édition HSV/HSL/RGB ;
- gestion alpha ;
- gestion HDR ;
- conversion sRGB/Linear ;
- preview couleur actuelle/précédente ;
- sliders et inputs numériques.

### `MGColorPickerPopup`

Version popup du picker, ouverte depuis un `MGColorField`.

Responsabilités :

- gérer l’ouverture/fermeture ;
- gérer le commit/cancel ;
- gérer le focus clavier ;
- gérer Escape/Enter ;
- éviter de casser la PropertyGrid.

### `MGColorPaletteView`

Évolution ou réutilisation de `MGGridColorPicker`.

Responsabilités :

- afficher des swatches ;
- gérer les palettes récentes/favorites/projet ;
- supporter le drag/drop ;
- supporter la sélection d’une couleur depuis une palette.

### `MGColorPreview`

Contrôle de preview indépendant.

Responsabilités :

- afficher couleur actuelle ;
- afficher couleur précédente ;
- afficher alpha checkerboard ;
- afficher éventuellement une preview HDR tonemappée.

### `MGColorSlider`

Slider spécialisé couleur.

Responsabilités :

- hue slider ;
- alpha slider ;
- RGB sliders ;
- HSV/HSL sliders ;
- intensity slider HDR.

---

# 1. Champ compact pour PropertyGrid

Le champ compact est prioritaire, car il permet d’intégrer rapidement le nouveau color picker dans les éditeurs existants.

## Fonctionnalités

- Petite swatch couleur.
- Swatch alpha séparée ou superposée sur damier.
- Ouverture du picker complet au clic.
- Édition inline optionnelle :
  - `#RRGGBB`
  - `#AARRGGBB`
  - `#RRGGBBAA`
  - `rgb(255, 128, 0)`
  - `rgba(255, 128, 0, 0.5)`
  - `Vector3(1, 0.5, 0)`
  - `Vector4(1, 0.5, 0, 1)`
- Bouton reset/revert.
- Bouton eyedropper optionnel.
- État read-only.
- État disabled.
- État nullable.
- État mixed value pour édition multi-objets.
- Binding two-way.
- Validation visuelle si le texte est invalide.

## Types supportés

Le contrôle doit pouvoir s’adapter à plusieurs types utilisés dans un moteur :

- `Microsoft.Xna.Framework.Color`
- `System.Drawing.Color` si besoin côté éditeur
- `Vector3`
- `Vector4`
- `System.Numerics.Vector3`
- `System.Numerics.Vector4`
- futur `ColorValue`
- futur `LinearColor`
- futur `HDRColor`

## Propriétés suggérées

```csharp
public ColorValue Value { get; set; }

public bool ShowAlpha { get; set; }
public bool ShowEyeDropper { get; set; }
public bool IsHdr { get; set; }
public bool AllowNull { get; set; }
public bool ShowTextInput { get; set; }

public ColorValue? DefaultValue { get; set; }

public ColorPickerMode PickerMode { get; set; }
public ColorValueFormat DisplayFormat { get; set; }
public ColorEditCommitMode CommitMode { get; set; }
```

---

# 2. Picker visuel principal

Le picker complet doit proposer plusieurs modes d’édition.

## Mode HSV classique

Fonctionnalités :

- carré Saturation/Value ;
- barre Hue verticale ou horizontale ;
- barre Alpha avec damier ;
- curseurs manipulables à la souris ;
- drag continu ;
- preview couleur actuelle ;
- preview couleur précédente ;
- reset/revert ;
- validation en temps réel.

C’est le mode à implémenter en premier.

## Mode HSL

Fonctionnalités :

- édition Hue/Saturation/Lightness ;
- sliders numériques ;
- conversion HSL/RGB ;
- synchronisation avec les autres modes.

Ce mode peut venir après le MVP.

## Mode RGB

Fonctionnalités :

- sliders R/G/B ;
- sliders en byte `0-255` ;
- sliders en float `0.0-1.0` ;
- champs numériques synchronisés ;
- possibilité de lock alpha.

## Mode Color Wheel

Fonctionnalités :

- roue de teinte ;
- triangle ou carré saturation/value ;
- mode plus confortable pour designers ;
- optionnel en V2.

## Mode Hex/Text

Fonctionnalités :

- champ texte principal ;
- parsing tolérant ;
- copier/coller rapide ;
- validation visuelle ;
- normalisation du format en sortie.

Formats à supporter :

```text
#fff
#ffff
#ffffff
#ffffffff
#RRGGBB
#AARRGGBB
#RRGGBBAA
rgb(...)
rgba(...)
Vector3(...)
Vector4(...)
```

---

# 3. Gestion de l’alpha

La gestion de l’alpha est indispensable pour un moteur moderne et pour une UI.

## Fonctionnalités

- Option `ShowAlpha`.
- Alpha slider sur damier.
- Preview avec damier.
- Preview opaque.
- Preview moitié opaque / moitié damier.
- Support alpha en float `0.0-1.0`.
- Support alpha en byte `0-255`.
- Option pour forcer alpha à 1.
- Option pour conserver l’alpha lors d’un eyedropper.
- Option pour masquer totalement l’alpha pour certains usages.

## Formats alpha

Le contrôle doit pouvoir afficher/exporter :

```text
#AARRGGBB
#RRGGBBAA
rgba(r, g, b, a)
Vector4(r, g, b, a)
```

## Cas particuliers

- Matériau opaque : alpha masqué ou forcé à 1.
- UI semi-transparente : alpha visible.
- Couleur de lumière : alpha masqué.
- Couleur de debug : alpha optionnel.
- Particules : alpha très important.

---

# 4. HDR

La prise en charge HDR est ce qui différencie un color picker moderne d’un simple picker UI.

## Objectifs

Permettre d’éditer des couleurs dont les composantes peuvent dépasser `1.0`.

Exemples :

```text
(1.0, 0.8, 0.4)
(2.5, 1.2, 0.4)
(8.0, 4.0, 1.0)
```

## Fonctionnalités

- Mode LDR clampé entre `0` et `1`.
- Mode HDR avec valeurs supérieures à `1`.
- Slider `Intensity`.
- Slider `Exposure`.
- Affichage de la couleur brute.
- Affichage de la couleur tonemappée.
- Warning visuel si la couleur dépasse le LDR.
- Option `HdrMaxIntensity`.
- Option `HdrPreviewToneMapping`.
- Bouton pour normaliser une couleur HDR en base color + intensity.
- Presets HDR utiles.

## Modèles possibles

### RGB direct

La couleur stockée contient directement des composantes HDR.

```text
R = 4.0
G = 2.0
B = 1.0
```

### Couleur + intensité

La couleur est stockée comme :

```text
FinalColor = BaseColor * Intensity
```

C’est souvent plus pratique côté éditeur.

### Exposure / EV

La couleur est éditée avec une intensité exprimée en exposition.

Utile pour les workflows avancés, mais peut être V2/V3.

## Propriétés suggérées

```csharp
public bool IsHdr { get; set; }

public float Intensity { get; set; }
public float MinIntensity { get; set; } = 0f;
public float MaxIntensity { get; set; } = 16f;

public bool ShowIntensity { get; set; }
public bool UseExposureSlider { get; set; }
public bool ShowToneMappedPreview { get; set; }
```

---

# 5. sRGB / Linear / Gamma

Un moteur moderne doit distinguer l’espace couleur affiché et l’espace couleur stocké.

## Fonctionnalités

- Mode d’affichage `sRGB`.
- Mode d’affichage `Linear`.
- Conversion sRGB vers Linear.
- Conversion Linear vers sRGB.
- Indication claire dans l’UI de l’espace utilisé.
- Option `StoreAsLinear`.
- Option `DisplayAsSrgb`.
- Preview dans les deux espaces.
- Bouton “Copy Linear”.
- Bouton “Copy sRGB”.
- Warning si l’utilisateur édite une couleur dans un espace différent de celui du stockage.

## Enum suggéré

```csharp
public enum ColorSpaceMode
{
    Srgb,
    Linear
}
```

## Recommandation

Pour un moteur, le modèle interne devrait idéalement stocker les couleurs en float linéaire.

L’UI peut afficher les valeurs en sRGB, car c’est ce que les utilisateurs attendent visuellement.

---

# 6. Eyedropper

L’eyedropper est très utile dans un éditeur de moteur, mais il doit être découplé de MGUI.Core.

## Niveau 1 : eyedropper MGUI

À implémenter en premier.

Fonctionnalités :

- pick d’un pixel dans un contrôle MGUI ;
- pick dans un `MGImage` ;
- pick dans un viewport connu ;
- pick dans un render target fourni par le moteur ;
- pas de dépendance à l’OS ;
- fonctionne dans l’éditeur.

## Niveau 2 : eyedropper écran global

À implémenter plus tard.

Fonctionnalités :

- pick d’un pixel n’importe où sur l’écran ;
- nécessite une implémentation platform-specific ;
- probablement hors de `MGUI.Core` ;
- peut être fourni par `CasaEngine.Editor`.

## Service recommandé

```csharp
public interface IColorPickService
{
    bool IsSupported { get; }

    void BeginPick(ColorPickRequest request);
    void CancelPick();

    event EventHandler<ColorPickedEventArgs> ColorPicked;
    event EventHandler ColorPickCancelled;
}
```

## Options utiles

```csharp
public sealed class ColorPickRequest
{
    public bool PreserveAlpha { get; set; }
    public bool PickFromScreen { get; set; }
    public bool PickFromMGUIOnly { get; set; }
    public ColorSpaceMode OutputColorSpace { get; set; }
}
```

---

# 7. Palettes et swatches

Le contrôle existant `MGGridColorPicker` peut être réutilisé comme base ou sous-contrôle.

## Fonctionnalités

- Couleurs récentes.
- Couleurs favorites.
- Couleurs projet.
- Couleurs thème UI.
- Couleurs matériau.
- Couleurs scène/lumière.
- Couleurs debug/gizmos.
- Palettes prédéfinies.
- Palettes utilisateur.
- Import/export palette.
- Drag/drop d’une couleur vers une palette.
- Réorganisation des swatches.
- Nommage des couleurs.
- Recherche par nom.
- Recherche par valeur hex.
- Groupes de palettes.
- Palette globale partagée dans l’éditeur.

## Palettes prédéfinies utiles

- Grayscale.
- Web colors.
- Material Design colors.
- UI theme colors.
- Debug colors.
- Light temperature presets.
- Common material colors.
- Console palettes si elles existent déjà dans MGUI.

## Modèle suggéré

```csharp
public sealed class MGColorPalette
{
    public string Name { get; set; }
    public ObservableCollection<MGColorSwatch> Colors { get; }
}

public sealed class MGColorSwatch
{
    public string Name { get; set; }
    public ColorValue Value { get; set; }
}
```

## Persistance

Prévoir un format simple :

```json
{
  "name": "Project Colors",
  "colors": [
    {
      "name": "Primary",
      "value": "#ff3366cc",
      "space": "sRGB"
    }
  ]
}
```

---

# 8. Presets orientés moteur

Le color picker doit proposer des presets adaptés aux usages d’un moteur.

## Catégories

- Material base color.
- Material emissive color.
- Light color.
- Fog color.
- Ambient color.
- Sky color.
- UI theme color.
- Debug color.
- Physics gizmo color.
- Collision shape color.
- Navigation mesh color.
- Selection outline color.
- Particle color.

## Presets de lumières

Ajouter un mode température Kelvin peut être très utile.

Exemples :

- candle ;
- tungsten ;
- warm white ;
- neutral white ;
- daylight ;
- overcast ;
- blue sky.

## Fonctionnalités Kelvin

- Slider Kelvin.
- Range configurable, par exemple `1000K-12000K`.
- Conversion Kelvin vers RGB.
- Option pour appliquer la couleur en sRGB ou Linear.
- Affichage de la température sélectionnée.
- Presets rapides.

---

# 9. Preview avancée

## Fonctionnalités MVP

- Preview couleur courante.
- Preview couleur précédente.
- Preview sur damier alpha.
- Preview opaque.
- Bouton revert.

## Fonctionnalités V1

- Preview sur fond clair.
- Preview sur fond foncé.
- Preview moitié avant / moitié après.
- Affichage du hex courant.
- Affichage RGB/HSV rapide.

## Fonctionnalités V2

- Preview HDR tonemappée.
- Preview couleur appliquée à un rectangle UI.
- Preview couleur appliquée à une sphère matériau.
- Preview couleur appliquée à un gizmo.
- Affichage de la luminance relative.
- Warning contraste texte.

## Fonctionnalités V3

- Preview daltonisme :
  - protanopia ;
  - deuteranopia ;
  - tritanopia.
- Preview sur image personnalisée.
- Preview en contexte éditeur.

---

# 10. Accessibilité et navigation

## Fonctionnalités

- Navigation clavier.
- Navigation gamepad.
- Focus visible.
- Tooltips.
- Support Escape/Enter.
- Support Tab/Shift+Tab.
- Incréments clavier :
  - flèches : petit pas ;
  - Shift + flèche : grand pas ;
  - Ctrl + flèche : précision fine.
- Verrouillage de canaux.
- Mode compact pour gamepad.
- Contraste suffisant dans les thèmes dark/light.
- Indicateurs visuels non uniquement basés sur la couleur.

---

# 11. Validation et contraintes

Le contrôle doit pouvoir être configuré selon le contexte d’usage.

## Contraintes possibles

- Autoriser ou interdire alpha.
- Forcer alpha à 1.
- Autoriser ou interdire HDR.
- Définir min/max par canal.
- Définir min/max intensity.
- Autoriser ou interdire null.
- Autoriser ou interdire texte libre.
- Restreindre à une palette.
- Restreindre aux couleurs opaques.
- Restreindre aux couleurs LDR.

## Classe suggérée

```csharp
public sealed class ColorPickerConstraints
{
    public bool AllowAlpha { get; set; } = true;
    public bool AllowHdr { get; set; } = false;
    public bool AllowNull { get; set; } = false;

    public float MinChannelValue { get; set; } = 0f;
    public float MaxChannelValue { get; set; } = 1f;

    public float MinIntensity { get; set; } = 0f;
    public float MaxIntensity { get; set; } = 16f;
}
```

---

# 12. Intégration PropertyGrid

Le color picker doit être pensé dès le départ comme un éditeur de propriété.

## Fonctionnalités

- `MGColorPropertyEditor`.
- Binding two-way.
- Commit immédiat ou différé.
- Support undo/redo.
- Support `BeginEdit`.
- Support `CommitEdit`.
- Support `CancelEdit`.
- Support édition multi-objets.
- Support mixed value.
- Support reset to default.
- Support read-only.
- Support validation.

## Modes de commit

```csharp
public enum ColorEditCommitMode
{
    Live,
    OnMouseRelease,
    ExplicitOkCancel
}
```

## Comportements

### `Live`

La valeur est appliquée à chaque changement.

Utile pour :

- UI theme preview ;
- changement de couleur simple ;
- debug tools.

### `OnMouseRelease`

La valeur est prévisualisée pendant le drag, puis commit à la fin.

Utile pour :

- matériaux ;
- lumières ;
- propriétés coûteuses ;
- undo/redo propre.

### `ExplicitOkCancel`

La valeur est appliquée uniquement avec OK.

Utile pour :

- dialogues modaux ;
- édition destructive ;
- propriétés très coûteuses.

---

# 13. Undo / redo

## Objectifs

Éviter de créer une entrée undo à chaque mouvement de souris.

## Fonctionnalités

- Début d’action au premier drag.
- Mise à jour live pendant le drag.
- Commit unique à la fin du drag.
- Cancel avec Escape.
- Revert avec bouton.
- Comparaison previous/current.
- Intégration avec un système de commandes éditeur.
- Support multi-objets.

## Interface possible

```csharp
public interface IColorEditTransaction
{
    void Begin(ColorValue initialValue);
    void Preview(ColorValue value);
    void Commit(ColorValue finalValue);
    void Cancel();
}
```

---

# 14. Performance

MGUI peut être rafraîchi chaque frame. Le color picker doit éviter les allocations et recalculs inutiles.

## Règles

- Ne pas régénérer les textures chaque frame.
- Ne pas parser le texte chaque frame.
- Ne pas invalider le layout pendant un drag simple.
- Ne pas créer une entrée undo par pixel de souris.
- Ne pas allouer pendant les interactions continues.
- Ne pas recalculer les palettes si elles n’ont pas changé.
- Virtualiser les palettes volumineuses.
- Utiliser du cache pour les gradients.
- Rebuild uniquement si taille/mode change.
- Prévoir un mode `UpdateOnRelease`.

## Gradients

Les gradients peuvent être rendus de plusieurs manières :

1. textures générées et mises en cache ;
2. rendu shader ;
3. primitives custom ;
4. rendu CPU uniquement lors d’un changement de taille.

Pour un MVP, une texture cache par zone est suffisante.

Pour une version moderne, un shader peut être préférable.

---

# 15. XAML / styles / thèmes

Le contrôle doit être déclarable et stylable via le système XAML-like de MGUI.

## Exemple LDR

```xml
<ColorField Value="{MGBinding Path=Material.BaseColor, Mode=TwoWay}"
            ShowAlpha="True"
            IsHdr="False"
            ShowEyeDropper="True"
            DisplayFormat="HexArgb" />
```

## Exemple HDR

```xml
<ColorField Value="{MGBinding Path=Material.EmissiveColor, Mode=TwoWay}"
            IsHdr="True"
            ShowAlpha="False"
            ShowIntensity="True"
            MaxIntensity="16" />
```

## Exemple lumière

```xml
<ColorField Value="{MGBinding Path=Light.Color, Mode=TwoWay}"
            IsHdr="True"
            ShowAlpha="False"
            ShowTemperature="True"
            ShowIntensity="True" />
```

## Éléments stylables

- champ compact ;
- swatch ;
- damier alpha ;
- popup ;
- sliders ;
- inputs numériques ;
- boutons ;
- curseurs du picker ;
- previews ;
- palettes ;
- tooltips ;
- état hover ;
- état focused ;
- état disabled ;
- état mixed value.

---

# 16. Architecture proposée

## Dossier recommandé

```text
MGUI.Core/
  UI/
    Color/
      ColorValue.cs
      ColorSpaceConverter.cs
      ColorParser.cs
      ColorFormatter.cs
      ColorPickerOptions.cs
      ColorPickerConstraints.cs

      MGColorField.cs
      MGColorPicker.cs
      MGColorPickerPopup.cs
      MGColorPreview.cs
      MGColorSlider.cs
      MGColorTextInput.cs
      MGColorPaletteView.cs
      MGColorSwatch.cs

      IColorPickService.cs
      ColorPickRequest.cs
      ColorPickedEventArgs.cs
```

## Réutilisation du contrôle existant

`MGGridColorPicker` peut être conservé dans un premier temps.

Deux options :

### Option A : le conserver tel quel

Il reste disponible pour compatibilité.

Le nouveau `MGColorPaletteView` peut l’utiliser ou le remplacer progressivement.

### Option B : le renommer/migrer

`MGGridColorPicker` devient `MGColorPaletteView`.

À faire seulement si la compatibilité API n’est pas critique.

---

# 17. Modèle couleur recommandé

## `ColorValue`

Créer un type indépendant de `Microsoft.Xna.Framework.Color`.

Raisons :

- `Color` XNA est byte-based ;
- il ne supporte pas HDR ;
- il ne distingue pas clairement sRGB/Linear ;
- il ne permet pas une édition moteur propre.

## Exemple

```csharp
public struct ColorValue
{
    public float R;
    public float G;
    public float B;
    public float A;

    public ColorSpaceMode ColorSpace;
    public bool IsHdr;
}
```

## Extensions utiles

```csharp
public Microsoft.Xna.Framework.Color ToXnaColor();
public Vector4 ToVector4();
public string ToHex(ColorValueFormat format);

public static ColorValue FromXnaColor(Color color);
public static ColorValue FromVector4(Vector4 value);
public static bool TryParse(string text, out ColorValue value);
```

## Formats

```csharp
public enum ColorValueFormat
{
    HexRgb,
    HexArgb,
    HexRgba,
    RgbByte,
    RgbaByte,
    RgbFloat,
    RgbaFloat,
    Vector3,
    Vector4
}
```

---

# 18. Événements

## Événements utiles

```csharp
public event EventHandler<ColorValueChangedEventArgs> ValueChanged;
public event EventHandler<ColorValueChangingEventArgs> ValueChanging;

public event EventHandler EditStarted;
public event EventHandler EditCommitted;
public event EventHandler EditCancelled;

public event EventHandler EyeDropperRequested;
```

## Différence entre Changing et Changed

### `ValueChanging`

Déclenché pendant un drag.

Utile pour preview live.

### `ValueChanged`

Déclenché quand la valeur est commit.

Utile pour undo/redo et sauvegarde.

---

# 19. États UI

Le contrôle doit gérer clairement les états suivants :

- normal ;
- hover ;
- focused ;
- active drag ;
- disabled ;
- read-only ;
- invalid ;
- mixed value ;
- null value ;
- HDR overflow ;
- eyedropper active ;
- popup open.

---

# 20. Fonctionnalités avancées futures

## Gradient editor

Permettre d’éditer une rampe de couleurs.

Utile pour :

- particules ;
- courbes d’effets ;
- ciel ;
- post-processing ;
- heatmaps ;
- debug visualization.

## Color harmony tools

Fonctionnalités :

- complémentaire ;
- analogue ;
- triadique ;
- split-complementary ;
- monochromatique.

Ce n’est pas prioritaire pour un moteur, mais utile pour les outils UI/design.

## Contraste UI

Fonctionnalités :

- calcul de contraste ;
- warning texte illisible ;
- suggestion couleur texte claire/foncée ;
- utile pour thème MGUI.

## Import/export palette

Formats possibles :

- JSON ;
- `.gpl` GIMP palette ;
- format custom `.mgpalette`.

---

# 21. Priorités d’implémentation

## MVP

- Créer `ColorValue`.
- Ajouter conversion XNA `Color` <-> `ColorValue`.
- Créer `MGColorField`.
- Créer popup simple.
- Créer `MGColorPicker` avec :
  - carré SV ;
  - hue slider ;
  - alpha slider ;
  - preview previous/current ;
  - input hex ;
  - input RGB.
- Ajouter `ShowAlpha`.
- Ajouter binding two-way.
- Ajouter `CommitMode`.
- Réutiliser `MGGridColorPicker` comme palette simple.
- Éviter les allocations pendant le drag.

## V1

- Ajouter modes RGB/HSV/HSL complets.
- Ajouter palettes récentes/favorites/projet.
- Ajouter undo/redo propre.
- Ajouter support PropertyGrid.
- Ajouter multi-object mixed value.
- Ajouter XAML complet.
- Ajouter styles/thèmes.
- Ajouter eyedropper MGUI/render target.

## V2

- Ajouter HDR.
- Ajouter intensity/exposure.
- Ajouter sRGB/Linear.
- Ajouter Kelvin temperature.
- Ajouter presets moteurs :
  - material ;
  - light ;
  - fog ;
  - sky ;
  - UI ;
  - debug.
- Ajouter preview HDR tonemappée.
- Ajouter service eyedropper platform-specific.

## V3

- Ajouter gradient editor.
- Ajouter color ramps.
- Ajouter material sphere preview.
- Ajouter color harmony tools.
- Ajouter color blindness preview.
- Ajouter contraste/accessibilité.
- Ajouter import/export palettes.

---

# 22. Fonctionnalités indispensables

Pour un contrôle réellement utile dans un moteur moderne, les fonctionnalités indispensables sont :

1. `MGColorField` compact pour PropertyGrid.
2. Alpha avec damier.
3. RGB/HSV/Hex synchronisés.
4. Preview previous/current.
5. Palettes récentes/favorites/projet.
6. Eyedropper au moins dans le viewport MGUI.
7. Undo/redo propre.
8. Mode live/deferred commit.
9. HDR avec intensity.
10. sRGB/Linear.
11. Support des types moteur (`Color`, `Vector3`, `Vector4`, futur `ColorValue`).
12. XAML/styling/thèmes.
13. Performance compatible refresh chaque frame.

---

# 23. Points d’attention

## Ne pas coupler à MonoGame `Color`

`Microsoft.Xna.Framework.Color` est utile pour l’affichage, mais ne suffit pas comme modèle interne.

Il faut un type float capable de représenter :

- alpha ;
- HDR ;
- linear ;
- sRGB ;
- valeurs hors plage LDR.

## Ne pas mélanger UI et services platform-specific

L’eyedropper écran global doit passer par une interface.

`MGUI.Core` ne doit pas dépendre directement d’API Windows.

## Ne pas créer un contrôle trop monolithique

Le color picker doit être composé de sous-contrôles :

- field ;
- popup ;
- picker ;
- sliders ;
- preview ;
- palette ;
- parser/formatter ;
- services.

## Prévoir l’éditeur dès le départ

Les systèmes suivants doivent être anticipés :

- PropertyGrid ;
- undo/redo ;
- multi-object editing ;
- validation ;
- binding ;
- thèmes ;
- refresh temps réel.

---

# 24. Résumé final

Le contrôle actuel `MGGridColorPicker` peut rester comme composant de palettes.

Le nouveau système devrait être pensé comme un ensemble :

```text
MGColorField
MGColorPickerPopup
MGColorPicker
MGColorPaletteView
MGColorPreview
ColorValue
ColorSpaceConverter
IColorPickService
```

Le MVP doit couvrir l’édition LDR/alpha classique.

La V2 doit ajouter les fonctionnalités vraiment moteur moderne :

- HDR ;
- intensity ;
- sRGB/Linear ;
- Kelvin ;
- presets lumière/matériau ;
- eyedropper viewport ;
- undo/redo propre.

Cette architecture permettra d’utiliser le même contrôle dans :

- la PropertyGrid ;
- l’éditeur de matériaux ;
- l’éditeur de lumières ;
- l’éditeur de thèmes MGUI ;
- l’éditeur de scènes ;
- les outils debug/gizmos.

---

# 25. État d'implémentation final

La cible a été livrée comme une suite de contrôles composée plutôt qu'un contrôle monolithique.

Implémenté dans le MVP/V2 :

- `ColorValue` float RGBA avec espace `Srgb`/`Linear` et metadata HDR ;
- parsing/formatting texte pour hex, rgb/rgba et vecteurs ;
- conversion HSV/HSL/RGB, sRGB/Linear, Kelvin et contraste WCAG ;
- `MGColorPreview`, `MGColorSlider`, `MGColorPicker`, `MGColorPickerPopup`, `MGColorField`, `MGColorPaletteView` ;
- integration PropertyGrid pour `ColorValue`, XNA `Color`, `Vector3`/`Vector4` et equivalents `System.Numerics` ;
- XAML pour field, picker, preview et palette ;
- modes de commit `Live`, `OnMouseRelease`, `ExplicitOkCancel` avec contrat `IColorEditTransaction` injectable ;
- eyedropper via contrat `IColorPickService` fourni par l'hote ;
- sRGB/Linear distincts entre stockage et affichage ;
- HDR/intensity, exposure slider et preview tonemappee explicite ;
- temperature Kelvin, presets moteur opt-in, JSON palettes et previews contraste clair/fonce ;
- navigation clavier/gamepad de base et caches de gradients.

Conserve hors scope volontairement :

- remplacement public de `MGGridColorPicker` ; il reste disponible et compatible ;
- color wheel complet, HSL UI complete, gradient editor, ramp editor, harmony tools, simulations color blindness ;
- previews lourdes material/sphere/gizmo/image custom ;
- formats `.gpl` et `.mgpalette`.

Documentation utilisateur : `Docs/mgui_colorpicker_usage_guide.md`.

Backlog V3 borne : `Docs/mgui_colorpicker_v3_backlog.md`.
