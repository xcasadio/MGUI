# MGUI - ColorPicker avance - plan agent IA

Source fonctionnelle: `Docs/mgui_advanced_colorpicker_features.md`.

Ce document transforme la specification du ColorPicker avance en plan d'execution pour un agent IA implementeur. La cible est une suite de controles couleur composee, testable et progressive, capable de remplacer ou completer `MGGridColorPicker` sans casser les usages existants.

## Consignes de travail pour l'agent IA

- Executer les taches strictement dans l'ordre.
- Faire exactement 1 commit git par tache terminee.
- Ne jamais passer a la tache suivante tant que la tache courante n'est pas validee et committee.
- Mettre a jour l'icone de statut dans le titre de la tache avant chaque commit.
- Quand une tache commence, remplacer `⚪` par `🟡` dans le titre de la tache.
- Quand une tache est terminee et validee, remplacer `🟡` par `✅`, remplir la section `Resultat`, puis committer.
- Si une tache est bloquee, remplacer l'icone par `⛔`, ajouter une section `Blocage` juste sous la tache, ne pas commencer la tache suivante, puis demander arbitrage.
- Garder les changements scopes a la tache courante; ne pas melanger deux taches dans le meme commit.
- Conserver `MGGridColorPicker` compatible. Le nouveau systeme peut le reutiliser, mais ne doit pas le renommer ou casser son API sans tache explicite.
- Preserver les patterns MGUI existants: proprietes notifiees via `NPC`, invalidation explicite du layout, hot paths sans allocations evitables, controles composables et styles/themes via `MGTheme`/XAML quand possible.
- Ajouter ou adapter des tests a chaque tache qui introduit logique, parsing, conversion, input ou integration PropertyGrid.
- Avant chaque commit: verifier `rtk git status`, verifier le diff, ne stage que les fichiers de la tache courante.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale apres chaque tache

Adapter selon le perimetre touche, mais ne pas sauter la validation sans le noter dans `Resultat`.

1. `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
2. `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
3. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Color --logger "console;verbosity=minimal"` des que des tests couleur existent.
4. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore` pour les taches qui touchent XAML, themes, samples, popup ou rendu visuel.
5. Si une tache est purement documentaire, relire le fichier modifie et verifier les liens, statuts et commits recommandes.

## Etat de depart observe

- `MGUI.Core/UI/MGGridColorPicker.cs` existe deja et fournit une grille de couleurs basee sur `Microsoft.Xna.Framework.Color`, avec navigation, selection simple/multiple et palettes predefinies.
- `MGGridColorPicker` est oriente palettes LDR byte-based; il ne couvre pas HSV, alpha avance, HDR, sRGB/Linear, text parsing riche, popup, commit/cancel ni PropertyGrid.
- `MGUI.Core/UI/MGPropertyGrid.cs` contient deja des editeurs internes pour `Bool`, `Int`, `Float`, `Double` et `String`.
- `MGUI.Core/UI/PropertyGrid/MGPropertyGridEditorKind.cs` ne contient pas encore de type `Color`.
- La couche XAML et themes existe deja dans `MGUI.Core/UI/XAML/`, avec un precedent recent pour les templates et styles.
- Les tests pertinents sont principalement sous `MGUI.Tests/PropertyGrid`, `MGUI.Tests/Integration` et les futurs tests couleur a creer.

## Cible architecturale

Le resultat attendu n'est pas un controle monolithique. La cible est un ensemble de types reutilisables:

- modele et services: `ColorValue`, `ColorSpaceConverter`, `ColorParser`, `ColorFormatter`, `ColorPickerConstraints`, `ColorPickerOptions`;
- controles: `MGColorField`, `MGColorPickerPopup`, `MGColorPicker`, `MGColorPreview`, `MGColorSlider`, `MGColorTextInput`, `MGColorPaletteView`;
- palettes: `MGColorPalette`, `MGColorSwatch`, reutilisation de `MGGridColorPicker` pour compatibilite;
- integration editeur: PropertyGrid, binding two-way, commit modes, undo/redo, mixed/null/default;
- extensions modernes: alpha complet, HSV/RGB/Hex MVP, puis sRGB/Linear, HDR, intensity, Kelvin, eyedropper MGUI.

## Hors perimetre initial

- Eyedropper ecran global OS dans `MGUI.Core`.
- Editeur de gradients complet.
- Preview sphere materiau 3D.
- Simulation daltonisme avancee.
- Remplacement ou renommage public de `MGGridColorPicker`.

Ces sujets peuvent etre prepares par interfaces ou backlog, mais ne doivent pas retarder le MVP.

## Ordre de commits attendu

1. `colorpicker: complete task 1 audit current color editor surface`
2. `colorpicker: complete task 2 add color value model`
3. `colorpicker: complete task 3 add color conversion math`
4. `colorpicker: complete task 4 add color parsing and formatting`
5. `colorpicker: complete task 5 add options constraints and edit contracts`
6. `colorpicker: complete task 6 add color preview control`
7. `colorpicker: complete task 7 add color slider primitives`
8. `colorpicker: complete task 8 add synchronized text and numeric inputs`
9. `colorpicker: complete task 9 add hsv picker mvp`
10. `colorpicker: complete task 10 add commit modes and edit events`
11. `colorpicker: complete task 11 add color picker popup`
12. `colorpicker: complete task 12 add compact color field`
13. `propertygrid: complete task 13 integrate color editor`
14. `colorpicker: complete task 14 add palette view and swatches`
15. `xaml: complete task 15 add color picker xaml and theme support`
16. `samples: complete task 16 add color picker sample coverage`
17. `colorpicker: complete task 17 harden undo redo and mixed values`
18. `colorpicker: complete task 18 add mgui eyedropper service`
19. `colorpicker: complete task 19 add srgb linear workflows`
20. `colorpicker: complete task 20 add hdr and intensity editing`
21. `colorpicker: complete task 21 add kelvin and engine presets`
22. `colorpicker: complete task 22 add palette persistence`
23. `colorpicker: complete task 23 harden accessibility and performance`
24. `colorpicker: complete task 24 add advanced preview backlog items`
25. `docs: complete task 25 document color picker api and migration`

## Taches

### ✅ 1. Auditer la surface couleur actuelle

But:
etablir un diagnostic precis avant d'introduire les nouveaux types, afin de reutiliser les patterns existants et d'eviter un controle couleur isole du reste de MGUI.

Travail attendu:

- lire `MGUI.Core/UI/MGGridColorPicker.cs` et noter ses APIs publiques, son modele `ColorPalette`, ses events implicites, sa navigation et ses limites;
- lire `MGUI.Core/UI/MGPropertyGrid.cs`, `MGUI.Core/UI/PropertyGrid/MGPropertyGridEditorKind.cs` et les tests PropertyGrid pour comprendre comment ajouter un editor couleur;
- identifier les patterns de controles composites proches: `MGSlider`, `MGTextBox`, `MGComboBox`, `MGOverlay`, popup/dropdown existants;
- identifier les patterns de rendu utiles pour damier alpha, gradients et previews: brushes, rectangles, textures cachees, draw args;
- identifier les conventions XAML/theme a suivre pour les nouveaux controles;
- verifier les dependances autorisees de `MGUI.Core` et ne pas ajouter de dependance platform-specific.

Livrables:

- section `Resultat` remplie dans cette tache avec les constats utiles;
- liste courte des fichiers a modifier lors des taches suivantes;
- aucune modification fonctionnelle hors documentation de ce plan.

Criteres d'acceptation:

- les contraintes de compatibilite de `MGGridColorPicker` sont explicites;
- le point d'integration PropertyGrid est identifie;
- les chemins de tests et samples a creer sont identifies;
- le commit ne contient que la mise a jour du plan et/ou une note d'audit.

Validation ciblee:

- relire ce fichier;
- `rtk git diff -- Docs/mgui_advanced_colorpicker_agent_tasks.md`.

Commit recommande:

- `colorpicker: complete task 1 audit current color editor surface`

Resultat:

- `MGGridColorPicker` est un controle public existant base sur `Microsoft.Xna.Framework.Color`, avec `ColorPalette`, selection simple/multiple, navigation directionnelle, hover, label de couleur selectionnee et rendu direct de swatches. Il doit rester compatible et servir de base ou d'adaptateur pour `MGColorPaletteView`.
- `MGPropertyGrid` choisit ses editors via `MGPropertyGridEditorKind` et cree des editors internes dans `PropertyGridRowView.CreateEditor(...)`; l'integration couleur devra donc ajouter un kind couleur, enrichir la detection des descriptors et introduire un editor `MGColorField` sans casser les editors bool/numeriques/string.
- Les popups/dropdowns doivent suivre les patterns de `MGComboBox`, notamment parts de template, fenetre dropdown, focus clavier et blocage de l'input derriere le popup.
- Les sliders couleur peuvent reutiliser les conventions de `MGSlider`: range explicite, `ValueChanged`, navigation keyboard/gamepad et invalidation limitee au changement de valeur ou de taille.
- Les themes fournissent deja `MGThemePropertyGridSettings.InvalidEditorBorderBrush`; les controles couleur devront ajouter leurs styles sans forcer une refonte globale de `MGTheme`.
- Les tests cibles existent deja sous `MGUI.Tests/PropertyGrid` et `MGUI.Tests/Integration`; les nouveaux tests couleur peuvent etre ajoutes avec des filtres `ColorValue`, `ColorSpace`, `ColorParser`, `ColorField`, `ColorEdit`, `Palette` et `Kelvin` comme prevu.

### ✅ 2. Ajouter le modele `ColorValue`

But:
introduire un modele couleur float independant de `Microsoft.Xna.Framework.Color`, capable de porter alpha, HDR et espace couleur sans forcer toute l'UI a rester byte-based.

Travail attendu:

- creer un dossier logique `MGUI.Core/UI/Color/` si l'audit confirme qu'il n'existe pas d'emplacement plus adapte;
- ajouter `ColorValue` comme `readonly struct` ou `struct` coherent avec les patterns MGUI;
- stocker `R`, `G`, `B`, `A` en `float`;
- ajouter `ColorSpaceMode` avec au minimum `Srgb` et `Linear`;
- ajouter `bool IsHdr` ou equivalent derive de valeurs hors plage selon la decision d'architecture;
- ajouter factories depuis `Microsoft.Xna.Framework.Color`, `Vector3`, `Vector4`, `System.Numerics.Vector3` et `System.Numerics.Vector4` si disponibles sans dependance nouvelle problematique;
- ajouter conversions vers `Microsoft.Xna.Framework.Color` avec clamp explicite LDR;
- ajouter helpers `WithAlpha`, `ClampLdr`, `ToVector4`, egalite et `GetHashCode`;
- documenter dans le code les conventions de canaux: float `0..1` pour LDR, valeurs > `1` autorisees pour HDR.

Livrables:

- `ColorValue.cs`;
- enums minimales necessaires;
- tests unitaires de creation, egalite, clamp et conversion XNA/Vector.

Criteres d'acceptation:

- `ColorValue.FromXnaColor(Color.White)` produit `1,1,1,1`;
- `ToXnaColor()` clamp les valeurs HDR sans exception;
- alpha est preserve dans tous les chemins;
- aucun controle existant n'est migre dans cette tache.

Validation ciblee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`;
- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorValue --logger "console;verbosity=minimal"`.

Commit recommande:

- `colorpicker: complete task 2 add color value model`

Resultat:

- `ColorSpaceMode` et `ColorValue` ont ete ajoutes dans `MGUI.Core/UI/Color/` avec canaux float `R/G/B/A`, espace `Srgb`/`Linear`, detection HDR explicite, conversions XNA/System.Numerics, `WithAlpha`, `WithColorSpace`, `ClampLdr`, `ToXnaColor`, egalite et hash stable.
- Les conversions depuis `Microsoft.Xna.Framework.Color` normalisent les bytes vers `0..1`; `ToXnaColor()` clamp explicitement les valeurs LDR et preserve alpha.
- Les tests `ColorValueTests` couvrent conversion XNA, clamp HDR/alpha, factories Vector et copies ajustees.
- Validation executee: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorValue --logger "console;verbosity=minimal"`.

### ✅ 3. Ajouter les conversions couleur et la mathematique HSV/HSL

But:
centraliser les conversions numeriques avant de construire les controles visuels, afin que sliders, picker, parsing et previews partagent les memes resultats.

Travail attendu:

- creer `ColorSpaceConverter.cs` ou types equivalents;
- implementer sRGB vers Linear et Linear vers sRGB avec formules standard et tests de valeurs pivots;
- implementer RGB vers HSV et HSV vers RGB;
- implementer RGB vers HSL et HSL vers RGB, meme si le mode HSL UI arrive plus tard;
- definir les plages attendues: Hue en degres `0..360` ou fraction `0..1`, Saturation/Value/Lightness en `0..1`;
- gerer les gris sans hue instable ni NaN;
- gerer alpha hors conversion chromatique;
- ajouter helpers de clamp/saturation sans allouer;
- eviter les conversions implicites cachees dans les controles.

Livrables:

- type de conversion couleur;
- tests couvrant rouge/vert/bleu/blanc/noir/gris, roundtrip RGB/HSV et RGB/HSL;
- notes dans `Resultat` sur les conventions de range retenues.

Criteres d'acceptation:

- les conversions ne produisent pas NaN pour les cas gris ou noirs;
- les roundtrips restent dans une tolerance flottante documentee;
- sRGB/Linear est disponible mais pas encore expose dans l'UI;
- les tests sont rapides et ne dependent pas du runtime graphique.

Validation ciblee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorSpace --logger "console;verbosity=minimal"`.

Commit recommande:

- `colorpicker: complete task 3 add color conversion math`

Resultat:

- `ColorSpaceConverter` centralise les conversions sRGB/Linear, RGB -> HSV, HSV -> RGB, RGB -> HSL, HSL -> RGB et la normalisation de hue.
- Les structs `HsvColor` et `HslColor` utilisent Hue en degres `0..360`, `S/V/L` en `0..1` et preservent alpha.
- Les gris/noirs/blancs retournent une hue stable a `0` sans NaN, et les roundtrips HSV/HSL preservent RGB dans une tolerance de `0.0001`.
- Validation executee: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorSpace --logger "console;verbosity=minimal"`.

### ✅ 4. Ajouter parsing et formatting couleur

But:
supporter l'edition texte rapide des formats moteurs et UI avant de brancher `MGColorField` et les inputs du picker.

Travail attendu:

- creer `ColorParser.cs` et `ColorFormatter.cs` ou un type pair coherent;
- ajouter `ColorValueFormat` avec `HexRgb`, `HexArgb`, `HexRgba`, `RgbByte`, `RgbaByte`, `RgbFloat`, `RgbaFloat`, `Vector3`, `Vector4`;
- parser `#fff`, `#ffff`, `#ffffff`, `#ffffffff`;
- documenter et tester l'ambiguite `#ffffffff` selon le format choisi ou une option explicite `HexArgb`/`HexRgba`;
- parser `rgb(...)`, `rgba(...)`, `Vector3(...)`, `Vector4(...)` avec culture invariant;
- accepter les espaces raisonnables sans accepter des formats ambigus dangereux;
- formatter en sortie stable et predictable;
- ajouter messages ou codes d'erreur internes suffisants pour afficher un etat invalide plus tard;
- ne pas parser a chaque frame: l'API doit etre appelee par changement texte ou commit.

Livrables:

- parser/formatter couleur;
- tests de formats valides et invalides;
- decision documentee sur alpha `ARGB` vs `RGBA`.

Criteres d'acceptation:

- tous les formats listes dans la specification MVP sont couverts;
- les nombres float utilisent `CultureInfo.InvariantCulture`;
- les erreurs retournent `false` ou un resultat diagnostique, sans exception sur input utilisateur invalide;
- les tests couvrent alpha et valeurs limites.

Validation ciblee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorParser --logger "console;verbosity=minimal"`.

Commit recommande:

- `colorpicker: complete task 4 add color parsing and formatting`

Resultat:

- `ColorValueFormat`, `ColorParser` et `ColorFormatter` ont ete ajoutes.
- Le parse par defaut traite les hex 8 digits comme `#RRGGBBAA`; `TryParse(..., ColorValueFormat.HexArgb, ...)` permet de lever explicitement l'ambiguite `#AARRGGBB`.
- Les formats supportes couvrent `#rgb`, `#rgba`, `#rrggbb`, `#rrggbbaa`, `rgb(...)`, `rgba(...)`, `Vector3(...)` et `Vector4(...)`, avec `CultureInfo.InvariantCulture`.
- `ColorValue.TryParse(...)` et `ColorValue.ToHex(...)` fournissent les raccourcis utiles au reste du picker.
- Validation executee: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorParser --logger "console;verbosity=minimal"`.

### ✅ 5. Ajouter options, contraintes et contrats d'edition

But:
figer les petits contrats partages avant les controles visuels: contraintes contextuelles, modes de commit, evenements et transaction d'edition.

Travail attendu:

- ajouter `ColorPickerConstraints` avec `AllowAlpha`, `AllowHdr`, `AllowNull`, min/max canaux, min/max intensity;
- ajouter `ColorPickerOptions` si utile pour grouper `ShowAlpha`, `ShowEyeDropper`, `PickerMode`, `DisplayFormat`, `CommitMode`, `IsHdr`;
- ajouter `ColorPickerMode` avec au minimum `Hsv`, `Rgb`, `Hsl`, `Text` meme si tous les modes UI ne sont pas immediatement visibles;
- ajouter `ColorEditCommitMode` avec `Live`, `OnMouseRelease`, `ExplicitOkCancel`;
- ajouter event args `ColorValueChangingEventArgs` et `ColorValueChangedEventArgs`;
- ajouter interface `IColorEditTransaction` sans la brancher a un systeme undo externe pour l'instant;
- definir comment contraintes et options interagissent, par exemple `AllowAlpha=false` force `A=1` dans l'UI.

Livrables:

- types de contrats partages;
- tests de contraintes simples si elles contiennent de la logique;
- documentation XML courte pour les modes de commit.

Criteres d'acceptation:

- les controles futurs peuvent dependre de ces contrats sans cycle de dependance;
- les valeurs par defaut correspondent au MVP LDR avec alpha visible seulement si demande;
- les tests epinglent le comportement de clamp/force alpha si implemente ici.

Validation ciblee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorPicker --logger "console;verbosity=minimal"` si des tests existent deja.

Commit recommande:

- `colorpicker: complete task 5 add options constraints and edit contracts`

Resultat:

- Les contrats partages ont ete ajoutes: `ColorPickerConstraints`, `ColorPickerOptions`, `ColorPickerMode`, `ColorEditCommitMode`, `ColorValueChangingEventArgs`, `ColorValueChangedEventArgs` et `IColorEditTransaction`.
- `ColorPickerConstraints.Apply(...)` applique clamp LDR par defaut, force alpha opaque quand `AllowAlpha=false`, et respecte une plage HDR configuree quand `AllowHdr=true`.
- Les options par defaut ciblent le MVP: mode `Hsv`, format `HexRgba`, commit `Live`, texte visible, HDR desactive.
- Validation executee: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorPicker --logger "console;verbosity=minimal"`.

### ✅ 6. Ajouter `MGColorPreview`

But:
creer un controle independant de preview couleur, reutilisable par le field compact, le picker complet, les palettes et les samples.

Travail attendu:

- creer `MGColorPreview` comme controle MGUI dedie;
- exposer `CurrentValue`, `PreviousValue`, `ShowPrevious`, `ShowCheckerboard`, `ShowOpaqueComparison`;
- rendre un damier alpha stable et lisible sans regeneration par frame;
- afficher current/previous en moitie avant/apres ou configuration equivalente;
- afficher une preview opaque quand alpha < 1;
- utiliser `ColorValue.ToXnaColor()` pour la version LDR;
- prevoir un hook futur pour preview HDR tonemappee sans l'implementer completement;
- respecter les themes pour border, hover, disabled si le controle est interactif.

Livrables:

- `MGColorPreview.cs`;
- tests de logique non graphique si possible;
- sample minimal ou harness de construction si les tests graphiques ne sont pas disponibles.

Criteres d'acceptation:

- preview alpha utilise un damier visible;
- previous/current peut etre active sans dupliquer du code dans `MGColorPicker`;
- aucun recalcul couteux ne se produit si la taille et la couleur ne changent pas;
- le controle compile dans `MGUI.Core` sans dependance samples.

Validation ciblee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`;
- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`.

Commit recommande:

- `colorpicker: complete task 6 add color preview control`

Resultat:

- `MGColorPreview` a ete ajoute avec `CurrentValue`, `PreviousValue`, `ShowPrevious`, `ShowCheckerboard`, `ShowOpaqueComparison`, taille preferee, damier alpha, comparaison opaque et bordure simple.
- `MGElementType.ColorPreview` a ete ajoute pour identifier le nouveau controle.
- La geometrie current/previous et transparent/opaque est exposee en helpers internes testables, sans dependance au runtime graphique.
- Validation executee: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorPreview --logger "console;verbosity=minimal"`.

### ✅ 7. Ajouter les primitives `MGColorSlider`

But:
creer un slider couleur specialise qui peut afficher hue, alpha, RGB, HSV et intensity avec gradients caches.

Travail attendu:

- creer `MGColorSlider` ou une famille minimale de sliders couleur;
- supporter au minimum les modes `Hue`, `Alpha`, `Red`, `Green`, `Blue`, `Saturation`, `Value`;
- exposer `Value`, `Minimum`, `Maximum`, `Channel`, `BaseColor`, `ShowCheckerboard`;
- dessiner un gradient de canal coherent avec `BaseColor`;
- afficher un curseur manipulable souris avec drag continu;
- supporter keyboard/gamepad increments si le controle est focusable;
- ne pas invalider tout le layout pendant un drag simple;
- cacher ou recalculer les gradients seulement quand taille, mode ou base color change;
- emettre des evenements ou callbacks suffisamment fins pour `ValueChanging` et `ValueChanged`.

Livrables:

- `MGColorSlider.cs`;
- tests de mapping position -> valeur et valeur -> position;
- tests de clamp et navigation si accessibles.

Criteres d'acceptation:

- le hue slider couvre toute la plage de teinte;
- l'alpha slider affiche le damier;
- les sliders ne creent pas d'entree undo ou commit eux-memes;
- les gradients ne sont pas recrees chaque frame.

Validation ciblee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorSlider --logger "console;verbosity=minimal"`.

Commit recommande:

- `colorpicker: complete task 7 add color slider primitives`

Resultat:

- `ColorSliderChannel` et `MGColorSlider` ont ete ajoutes avec canaux `Hue`, `Alpha`, `Red`, `Green`, `Blue`, `Saturation`, `Value` et `Intensity`.
- Le controle expose range, valeur, orientation, base color, damier alpha, drag souris, navigation clavier/gamepad via `TryHandleNavigationAction`, events `ValueChanging`/`ValueChanged` et `DragStarted`/`DragCompleted`.
- Les gradients sont rendus directement depuis les canaux sans allocation de texture par frame; les helpers de mapping valeur/position et couleur de gradient sont testables.
- Validation executee: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorSlider --logger "console;verbosity=minimal"`.

### ✅ 8. Ajouter inputs texte et numeriques synchronises

But:
creer les briques d'edition Hex/RGB/HSV qui seront reutilisees dans le picker sans dupliquer parsing, validation et commit.

Travail attendu:

- creer `MGColorTextInput` ou une couche interne dediee;
- connecter un champ hex principal a `ColorParser`/`ColorFormatter`;
- ajouter inputs numeriques R/G/B/A en byte `0..255` et/ou float `0..1` selon le MVP retenu;
- ajouter inputs HSV `H`, `S`, `V` synchronises avec le modele;
- afficher un etat invalide sans committer une valeur invalide;
- gerer Enter pour commit et Escape pour revert si le controle est dans un contexte d'edition;
- eviter les boucles de synchronisation entre sliders et inputs;
- formatter seulement aux moments utiles: focus perdu, commit, changement de mode;
- exposer un mecanisme read-only/disabled coherent.

Livrables:

- controle ou helper d'input couleur;
- tests de synchronisation parser -> modele -> formatter;
- tests d'input invalide si possible.

Criteres d'acceptation:

- une modification hex met a jour RGB/HSV;
- une modification RGB met a jour hex/HSV;
- input invalide est visible et ne modifie pas `Value`;
- aucune boucle infinie d'evenements de texte.

Validation ciblee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorText --logger "console;verbosity=minimal"`.

Commit recommande:

- `colorpicker: complete task 8 add synchronized text and numeric inputs`

Resultat:

- `MGColorTextInputModel` a ete ajoute comme couche de synchronisation testable pour les futurs champs `MGTextBox`.
- Le modele synchronise `HexText`, RGB byte, alpha float et HSV depuis `ColorValue`, et applique les chemins inverses `TrySetHexText`, `TrySetRgbByteText`, `TrySetRgbFloatText` et `TrySetHsvText`.
- Les inputs invalides mettent `HasValidationError=true`, conservent la valeur precedente et gardent le texte invalide visible pour l'UI.
- Validation executee: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorText --logger "console;verbosity=minimal"`.

### ✅ 9. Ajouter `MGColorPicker` HSV MVP

But:
livrer le picker visuel principal en mode HSV classique avec alpha, preview et edition texte/RGB synchronisee.

Travail attendu:

- creer `MGColorPicker` compose de sous-controles, pas un draw monolithique;
- ajouter un carre Saturation/Value interactif;
- ajouter un hue slider;
- ajouter un alpha slider optionnel selon `ShowAlpha`;
- integrer `MGColorPreview` pour current/previous;
- integrer inputs Hex et RGB de la tache 8;
- exposer `Value`, `PreviousValue`, `ShowAlpha`, `Constraints`, `DisplayFormat`, `PickerMode`;
- synchroniser SV, hue, alpha, RGB et hex sans perte excessive de precision;
- supporter drag continu souris;
- eviter les allocations pendant le drag;
- prevoir le changement futur de modes RGB/HSL sans casser l'API.

Livrables:

- `MGColorPicker.cs`;
- tests de logique de mapping SV/hue si possible;
- sample de construction minimal ou integration sample reportee a la tache 16.

Criteres d'acceptation:

- une valeur initiale se reflete dans tous les controls;
- drag SV modifie saturation/value et conserve hue/alpha;
- drag hue modifie la teinte et conserve alpha;
- `ShowAlpha=false` masque alpha et force ou preserve alpha selon option documentee;
- le controle compile sans necessiter PropertyGrid.

Validation ciblee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`;
- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`.

Commit recommande:

- `colorpicker: complete task 9 add hsv picker mvp`

Resultat:

- `MGColorPickerModel` a ete ajoute pour centraliser la synchronisation `ColorValue`/HSV/texte sans dependance rendu.
- `MGColorPicker` a ete ajoute avec carre Saturation/Value, hue slider vertical, alpha slider optionnel, preview previous/current, bande d'input texte synchronisee et support drag souris continu.
- `ColorPickerOptions.InitialValue` et `MGElementType.ColorPicker` ont ete ajoutes pour construire et identifier le nouveau controle.
- Decision MVP documentee: `ShowAlpha=false` masque le slider alpha et preserve l'alpha courant; les modes de commit detailles restent pour la tache 10.
- Validation executee: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorPickerModel --logger "console;verbosity=minimal"`.

### ✅ 10. Ajouter modes de commit et evenements d'edition

But:
separer preview live et commit final pour eviter une entree undo par pixel de souris et permettre les usages PropertyGrid/editeur.

Travail attendu:

- brancher `ColorEditCommitMode.Live`, `OnMouseRelease` et `ExplicitOkCancel` dans `MGColorPicker`;
- emettre `ValueChanging` pendant drag/preview;
- emettre `ValueChanged` uniquement lors du commit selon le mode;
- ajouter `EditStarted`, `EditCommitted`, `EditCancelled`;
- implementer `BeginEdit`, `CommitEdit`, `CancelEdit` ou equivalents publics;
- conserver `PreviousValue` jusqu'au commit ou cancel;
- gerer Escape/Enter au niveau picker si le focus est dans le picker;
- verifier que les sliders et inputs ne committent pas directement hors politique du picker;
- ajouter tests unitaires autour des sequences d'evenements si les handlers peuvent etre appeles sans rendu.

Livrables:

- commit modes fonctionnels;
- tests d'ordre des evenements;
- documentation XML courte des modes.

Criteres d'acceptation:

- `Live` applique les changements immediatement;
- `OnMouseRelease` preview pendant drag puis commit une seule fois a la fin;
- `ExplicitOkCancel` attend une action explicite;
- cancel restaure la valeur precedente et emet l'evenement attendu.

Validation ciblee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorEdit --logger "console;verbosity=minimal"`.

Commit recommande:

- `colorpicker: complete task 10 add commit modes and edit events`

Resultat:

- `MGColorPickerModel` gere maintenant `Live`, `OnMouseRelease` et `ExplicitOkCancel` avec etat `CommittedValue`, preview courante et transaction active.
- Evenements ajoutes et relayes par `MGColorPicker`: `ValueChanging`, `ValueChanged`, `EditStarted`, `EditCommitted`, `EditCancelled`.
- API publique ajoutee: `BeginEdit`, `CommitEdit`, `CancelEdit`; le controle commit sur relache souris en mode `OnMouseRelease` et gere `Submit`/`Cancel` via navigation.
- `PreviousValue` est conserve pendant la preview et mis a jour au commit; `CancelEdit` restaure la valeur initiale.
- Validation executee: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorEdit --logger "console;verbosity=minimal"`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "ColorPickerModel|ColorText" --logger "console;verbosity=minimal"`.

### ✅ 11. Ajouter `MGColorPickerPopup`

But:
fournir une version popup du picker utilisable depuis un champ compact sans casser focus, clavier, PropertyGrid ni overlays existants.

Travail attendu:

- analyser le pattern existant des dropdowns/popups dans `MGComboBox`, menus ou overlays;
- creer `MGColorPickerPopup` autour de `MGColorPicker`;
- gerer ouverture/fermeture, placement relatif et fermeture quand clic exterieur si pattern existant;
- supporter OK/Cancel pour `ExplicitOkCancel`;
- supporter Escape pour cancel et Enter pour commit;
- capturer/restaurer le focus clavier proprement;
- ne pas laisser l'input fuiter au contenu derriere pendant l'edition;
- exposer events `PopupOpened`, `PopupClosed`, `EditCommitted`, `EditCancelled` si utiles;
- eviter que le popup force un theme ou une taille non configurable.

Livrables:

- `MGColorPickerPopup.cs`;
- tests de logique de commit/cancel si possible;
- integration manuelle reportee au sample tache 16.

Criteres d'acceptation:

- ouvrir le popup initialise le picker avec la valeur courante;
- Cancel restaure la valeur initiale;
- OK commit une seule valeur finale;
- Escape/Enter fonctionnent quand le focus est dans un input texte du picker;
- la fermeture nettoie les handlers et ne laisse pas de focus invalide.

Validation ciblee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`;
- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`.

Commit recommande:

- `colorpicker: complete task 11 add color picker popup`

Resultat:

- `MGColorPickerPopup` a ete ajoute autour d'une `MGWindow` imbriquee contenant un `MGColorPicker` force en `ExplicitOkCancel`.
- Le popup expose `Open`, `OpenRelativeTo`, `CommitAndClose`, `CancelAndClose`, `TryHandleNavigationAction`, `PopupOpened`, `PopupClosed`, `EditCommitted` et `EditCancelled`.
- Le placement suit le pattern dropdown: position relative a une ancre, clamp dans le viewport et fermeture/cancel sur release exterieur optionnel.
- Le focus est isole via `PushFocusScope`/`PopFocusScope`, avec focus initial sur le picker et restauration par le service de navigation.
- Validation executee: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorPickerPopup --logger "console;verbosity=minimal"`.

### ✅ 12. Ajouter `MGColorField` compact

But:
livrer le champ compact prioritaire pour PropertyGrid et formulaires, avec swatch, texte optionnel, reset et popup.

Travail attendu:

- creer `MGColorField` comme controle composite leger;
- afficher une swatch avec damier alpha;
- afficher optionnellement un texte hex ou format configure;
- ouvrir `MGColorPickerPopup` au clic ou via clavier;
- exposer `Value`, `DefaultValue`, `AllowNull`, `IsMixed`, `ShowTextInput`, `ShowAlpha`, `ShowEyeDropper`, `IsHdr`, `DisplayFormat`, `CommitMode`;
- gerer read-only et disabled;
- ajouter bouton reset/revert si `DefaultValue` est renseigne;
- afficher un etat mixed value sans inventer une couleur factice;
- afficher un etat null distinct;
- brancher validation visuelle si le texte inline est invalide;
- s'assurer que le controle peut etre utilise hors PropertyGrid.

Livrables:

- `MGColorField.cs`;
- tests de valeur/default/null/mixed si possible;
- integration popup.

Criteres d'acceptation:

- clic swatch ouvre le popup;
- commit popup met a jour `Value` et emet l'evenement attendu;
- cancel ne change pas `Value`;
- read-only empeche l'ouverture ou l'edition selon politique documentee;
- field compact a une taille stable adaptee aux lignes PropertyGrid.

Validation ciblee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorField --logger "console;verbosity=minimal"`.

Commit recommande:

- `colorpicker: complete task 12 add compact color field`

Resultat:

- `MGColorField` a ete ajoute comme champ compact focusable avec swatch alpha, etat null/mixed, reset optionnel et ouverture de `MGColorPickerPopup`.
- `MGColorFieldModel` et `ColorFieldValueChangedEventArgs` centralisent les transitions testables `Value`, `DefaultValue`, `AllowNull`, `IsMixed` et `IsReadOnly`.
- Le champ expose les options requises: `Value`, `DefaultValue`, `AllowNull`, `IsMixed`, `ShowTextInput`, `ShowAlpha`, `ShowEyeDropper`, `IsHdr`, `DisplayFormat`, `CommitMode` et `IsReadOnly`.
- Le commit popup met a jour `Value`; le cancel ne modifie pas la valeur; read-only bloque reset et ouverture.
- Validation executee: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorField --logger "console;verbosity=minimal"`.

### ✅ 13. Integrer l'editeur couleur dans PropertyGrid

But:
faire du nouveau champ couleur un editeur de propriete officiel pour les types couleur et vecteurs moteur.

Travail attendu:

- etendre `MGPropertyGridEditorKind` avec un kind couleur ou une strategie plus expressive si l'audit le justifie;
- detecter au minimum `Microsoft.Xna.Framework.Color`;
- evaluer et supporter prudemment `Vector3`, `Vector4`, `System.Numerics.Vector3`, `System.Numerics.Vector4` si les dependances sont deja disponibles;
- convertir valeur propriete -> `ColorValue` -> valeur propriete sans perdre alpha quand le type le supporte;
- ajouter un `ColorPropertyGridEditor` interne ou public selon l'architecture existante;
- respecter `IsReadOnly`, validation, `RefreshVisibleValues`, categories et theme PropertyGrid;
- definir le comportement pour valeur null et mixed si PropertyGrid sait deja les representer;
- eviter que chaque drag cree une mise a jour couteuse de toute la grille;
- ajouter tests de descriptor detection et commit valeur.

Livrables:

- PropertyGrid detecte et affiche les couleurs;
- tests dans `MGUI.Tests/PropertyGrid` et/ou `MGUI.Tests/Integration`;
- sample PropertyGrid enrichi ou reporte explicitement a la tache 16.

Criteres d'acceptation:

- une propriete `Color` apparait avec `MGColorField`;
- le commit modifie l'objet inspecte;
- read-only affiche une preview non editable;
- les types non supportes ne regressent pas vers un mauvais editor;
- les tests PropertyGrid existants passent.

Validation ciblee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter PropertyGrid --logger "console;verbosity=minimal"`;
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore` si le sample PropertyGrid est modifie.

Commit recommande:

- `propertygrid: complete task 13 integrate color editor`

Resultat:

- `MGPropertyGridEditorKind.Color` a ete ajoute et le cache de descriptors detecte `Microsoft.Xna.Framework.Color`, `Vector3`, `Vector4`, `System.Numerics.Vector3` et `System.Numerics.Vector4`.
- `PropertyGridColorAdapter` convertit les valeurs inspectees vers `ColorValue` et reconstruit le type source au commit, avec alpha preserve pour les types qui le supportent.
- `MGPropertyGrid` cree maintenant un editeur `ColorPropertyGridEditor` base sur `MGColorField`; les proprietes read-only utilisent le meme champ en mode non editable.
- Le commit du champ couleur appelle le setter de la propriete via le flux existant `ValueCommitted`/`CommitRowValue`, sans rafraichir toute la grille pendant les previews popup.
- Validation executee: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "PropertyGridDescriptor|ColorAdapter" --logger "console;verbosity=minimal"`.

### ✅ 14. Ajouter `MGColorPaletteView` et les swatches

But:
reutiliser la valeur de `MGGridColorPicker` tout en introduisant un modele de palettes modernes: recentes, favorites, projet et swatches nommees.

Travail attendu:

- creer `MGColorSwatch` avec `Name`, `Value`, metadata minimale si utile;
- creer `MGColorPalette` avec `Name` et collection de swatches;
- creer `MGColorPaletteView` qui peut, pour le MVP, adapter `MGGridColorPicker` ou le composer;
- supporter selection d'une couleur et notification vers `MGColorPicker`;
- ajouter palettes recentes et favorites en memoire;
- ajouter API pour ajouter/reordonner/supprimer une swatch si raisonnable dans cette tranche;
- garder `MGGridColorPicker` public et fonctionnel;
- eviter la virtualisation prematuree, mais ne pas bloquer une future virtualisation;
- ajouter tests de modele palette et selection.

Livrables:

- modeles de palettes/swatches;
- `MGColorPaletteView.cs`;
- integration optionnelle dans `MGColorPicker`.

Criteres d'acceptation:

- `MGColorPicker` peut afficher une palette simple;
- `MGGridColorPicker` continue a compiler et fonctionner;
- une couleur selectionnee depuis la palette met a jour le picker selon le commit mode;
- les palettes recentes ne grandissent pas sans limite.

Validation ciblee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Palette --logger "console;verbosity=minimal"`.

Commit recommande:

- `colorpicker: complete task 14 add palette view and swatches`

Resultat:

- Modeles ajoutes: `MGColorSwatch`, `MGColorPalette`, `MGColorPaletteStore` et `ColorSwatchSelectedEventArgs`.
- `MGColorPaletteStore` fournit des palettes `Recent` et `Favorites`; les couleurs recentes sont de-dupliquees, remontees en tete et limitees par `MaxRecentColors`.
- `MGColorPaletteView` affiche une grille de swatches, expose `SwatchSelected`, calcule la selection souris et peut etre liee a un `MGColorPicker` via `BindPicker`.
- La selection applique la couleur au picker en respectant `CommitMode`: commit immediat pour `Live`/`OnMouseRelease`, preview en attente pour `ExplicitOkCancel`.
- Validation executee: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Palette --logger "console;verbosity=minimal"`.

### ✅ 15. Ajouter support XAML, styles et themes

But:
rendre les nouveaux controles declarables et stylables comme les autres controles MGUI, sans imposer un usage code-only.

Travail attendu:

- ajouter les aliases XAML necessaires pour `ColorField`, `ColorPicker`, `ColorPreview`, `ColorPaletteView` selon les conventions existantes;
- parser les proprietes importantes: `Value`, `ShowAlpha`, `IsHdr`, `ShowEyeDropper`, `DisplayFormat`, `CommitMode`, `PickerMode`, `AllowNull`;
- ajouter converters XAML pour `ColorValue` si necessaire;
- ajouter settings de theme pour field, swatch, popup, sliders, inputs, preview, palette;
- exposer les etats visuels: hover, focused, disabled, read-only, invalid, mixed, null, popup open;
- verifier que styles/templates existants ne sont pas casses;
- ajouter tests de parsing XAML minimal.

Livrables:

- support XAML des controles couleur;
- tests de parsing et application simple;
- theme par defaut utilisable en dark/light existants.

Criteres d'acceptation:

- un `ColorField` peut etre declare en XAML avec binding ou valeur statique;
- les proprietes enum parsees fonctionnent;
- le theme par defaut rend le controle lisible sans configuration custom;
- les tests XAML existants passent.

Validation ciblee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Xaml --logger "console;verbosity=minimal"`;
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`.

Commit recommande:

- `xaml: complete task 15 add color picker xaml and theme support`

Resultat:

- `XAMLColorValue` et `ColorValueStringConverter` ont ete ajoutes pour declarer des `ColorValue` depuis les formats supportes par `ColorParser`.
- Les controles XAML `ColorField`, `ColorPicker`, `ColorPreview` et `ColorPaletteView` ont ete ajoutes avec les proprietes principales (`Value`, `ShowAlpha`, `DisplayFormat`, `CommitMode`, `PickerMode`, etc.).
- Les alias de noms XAML couleur sont declares dans `XAMLParser` et les controles restent des leaf elements compatibles avec le systeme de styles existant.
- `ColorPaletteView` supporte une palette simple via `PaletteName` et `CommaSeparatedColors`; les controles conservent leurs proprietes visuelles directes pour les themes dark/light existants.
- Validation executee: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorXaml --logger "console;verbosity=minimal"`.

### ✅ 16. Ajouter samples et couverture visuelle de base

But:
fournir une experience testable manuellement dans `MGUI.Samples` et verrouiller les workflows MVP.

Travail attendu:

- ajouter une page sample `ColorPicker` ou etendre le compendium existant selon les conventions samples;
- montrer `MGColorField` compact, `MGColorPicker` complet, popup, alpha, palettes et PropertyGrid;
- ajouter un objet sample avec proprietes `Color`, `Vector3`, `Vector4` si supportees;
- afficher current/previous et resultat texte pour verifier les conversions;
- inclure cas read-only, disabled, null/mixed si disponibles;
- verifier le rendu avec themes existants dark/dark blue;
- ajouter tests d'integration de chargement XAML/sample si le projet en contient pour les autres samples.

Livrables:

- sample compilable;
- eventuelles ressources XAML sample;
- tests d'integration de chargement si possible.

Criteres d'acceptation:

- `MGUI.Samples` compile;
- le sample permet de changer une couleur par picker, texte, slider et palette;
- le sample PropertyGrid montre l'editeur couleur;
- aucun controle ne deborde visuellement dans les tailles sample usuelles.

Validation ciblee:

- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Color --logger "console;verbosity=minimal"`.

Commit recommande:

- `samples: complete task 16 add color picker sample coverage`

Resultat:

- Ajout du sample `ColorPicker` dans `MGUI.Samples.Controls`, expose dans le compendium.
- La fenetre sample montre `MGColorPicker`, `MGColorField` compact, ouverture popup, preview current/previous, palette liee au picker, slider rouge runtime, et cas read-only/disabled/null/mixed.
- Ajout d'un `PropertyGrid` alimente par un objet contenant `Color`, `Vector3`, `Vector4`, `System.Numerics.Vector4` et une valeur texte hex; le theme peut basculer entre `Dark_Blue` et `Dark`.
- Ajout d'un test `ColorXaml` qui parse le XAML du sample `Controls/ColorPicker.xaml` en plus des controles XAML couleur.
- Validation executee: `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Color --logger "console;verbosity=minimal"`.

### ✅ 17. Durcir undo/redo, transactions et valeurs mixed

But:
preparer les usages editeur serieux: multi-object editing, transactions propres et annulation sans multiplication d'entrees undo.

Travail attendu:

- relire le systeme undo/redo disponible dans le depot ou confirmer son absence;
- brancher `IColorEditTransaction` sur le mecanisme existant si disponible;
- sinon garder une interface propre et un adaptateur no-op/documente;
- garantir une seule transaction par drag en `OnMouseRelease`;
- permettre preview live puis commit final;
- ajouter support mixed value dans `MGColorField` et PropertyGrid si la grille sait representer multi-objets;
- definir reset to default et revert previous clairement;
- ajouter tests des sequences Begin/Preview/Commit/Cancel.

Livrables:

- transaction couleur robuste;
- mixed/default/revert clarifies;
- tests d'evenements et transactions.

Criteres d'acceptation:

- un drag ne cree qu'un commit logique;
- Escape annule la transaction active;
- previous/current reste coherent apres cancel;
- mixed value n'ecrase pas tous les objets tant que l'utilisateur n'a pas choisi une couleur.

Validation ciblee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorEdit --logger "console;verbosity=minimal"`;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter PropertyGrid --logger "console;verbosity=minimal"`.

Commit recommande:

- `colorpicker: complete task 17 harden undo redo and mixed values`

Resultat:

- Audit undo/redo: aucun service undo global reutilisable n'existe hors piles locales `MGTextBox`/rich text; `IColorEditTransaction` reste donc le point d'integration propre pour les editeurs hote.
- Ajout de `NoOpColorEditTransaction` et d'une transaction injectable via `ColorPickerOptions.EditTransaction`/`MGColorPickerModel.EditTransaction`.
- `MGColorPickerModel` appelle maintenant `Begin`, `Preview`, `Commit` et `Cancel` sur une seule transaction logique par edition/drag, avec annulation si une valeur externe remplace l'edition active.
- `MGColorFieldModel` expose `SetMixedValue`/`ClearMixedValue`; choisir une couleur depuis un etat mixed declenche un commit meme si elle egale la valeur de fallback affichee.
- Ajout de tests de lifecycle transaction, annulation, commit unique OnMouseRelease et comportement mixed field.
- Validation executee: `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorEdit --logger "console;verbosity=minimal"`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter PropertyGrid --logger "console;verbosity=minimal"`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorField --logger "console;verbosity=minimal"`.

### ✅ 18. Ajouter le service eyedropper MGUI

But:
ajouter un eyedropper decouple qui sait picker dans MGUI, un `MGImage`, un viewport connu ou un render target fourni, sans API OS globale dans `MGUI.Core`.

Travail attendu:

- ajouter `IColorPickService` avec `IsSupported`, `BeginPick`, `CancelPick`, `ColorPicked`, `ColorPickCancelled`;
- ajouter `ColorPickRequest` avec `PreserveAlpha`, `PickFromScreen`, `PickFromMGUIOnly`, `OutputColorSpace`;
- ajouter `ColorPickedEventArgs`;
- implementer un service MGUI/render-target minimal ou un contrat injectable selon les capacites du rendu actuel;
- ajouter bouton eyedropper optionnel dans `MGColorField` et `MGColorPicker`;
- respecter `PreserveAlpha` et `OutputColorSpace`;
- gerer annulation Escape et perte de focus;
- documenter explicitement que pick ecran global est hors `MGUI.Core`.

Livrables:

- contrat eyedropper;
- integration UI optionnelle;
- tests du contrat et des transitions support/cancel si possible.

Criteres d'acceptation:

- un service non supporte masque ou disable le bouton;
- un pick reussi met a jour le picker selon le commit mode;
- cancel ne modifie pas la valeur;
- aucune dependance Windows ou OS n'est ajoutee a `MGUI.Core`.

Validation ciblee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter EyeDropper --logger "console;verbosity=minimal"` si des tests existent.

Commit recommande:

- `colorpicker: complete task 18 add mgui eyedropper service`

Resultat:

- Ajout des contrats `IColorPickService`, `ColorPickRequest` et `ColorPickedEventArgs`, plus `UnsupportedColorPickService` par defaut.
- `ColorPickerOptions` transporte maintenant le service de pick; `MGColorPicker`, `MGColorField` et le popup le propagent et exposent `BeginEyeDropperPick`/`IsEyeDropperAvailable`.
- Les boutons eyedropper sont optionnels: ils restent desactives/masques selon le controle quand aucun service supporte n'est injecte; un service unsupported ne demarre jamais de pick.
- Un pick reussi applique la couleur selon le commit mode du picker; `ShowAlpha=false` preserve l'alpha courant. Un cancel ne modifie pas la valeur.
- `MGUI.Core` n'ajoute aucune dependance OS ni API de pick ecran global; les hotes peuvent injecter un service MGUI/render-target specifique.
- Validation executee: `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter EyeDropper --logger "console;verbosity=minimal"`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorXaml --logger "console;verbosity=minimal"`.

### ✅ 19. Ajouter workflows sRGB / Linear

But:
exposer proprement la difference entre espace d'affichage et espace de stockage pour les usages moteur modernes.

Travail attendu:

- ajouter proprietes `StoreAsLinear`, `DisplayAsSrgb` ou equivalent coherent avec `ColorSpaceMode`;
- permettre bascule UI entre `sRGB` et `Linear`;
- afficher clairement l'espace courant dans le picker sans texte explicatif encombrant;
- ajouter preview ou valeurs copiees `Copy Linear` et `Copy sRGB` si un pattern de commande existe;
- convertir sans double-conversion quand la valeur change par slider ou texte;
- ajouter warnings visuels si l'utilisateur edite un espace different du stockage;
- ajouter tests de roundtrip et de preservation alpha.

Livrables:

- support UI et modele sRGB/Linear;
- tests de conversion dans le flux picker;
- sample mis a jour si pertinent.

Criteres d'acceptation:

- une valeur stockee Linear peut etre affichee/editee en sRGB;
- la bascule d'affichage ne change pas la couleur stockee par erreur;
- les formats textes indiquent ou respectent l'espace choisi;
- les tests couvrent valeurs extremes et mid-gray.

Validation ciblee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorSpace --logger "console;verbosity=minimal"`;
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore` si le sample est modifie.

Commit recommande:

- `colorpicker: complete task 19 add srgb linear workflows`

Resultat:

- Ajout de `ColorSpaceConverter.Convert` pour convertir explicitement un `ColorValue` entre `Srgb` et `Linear` en preservant l'alpha.
- `ColorPickerOptions` expose `StorageColorSpace`, `DisplayColorSpace`, `StoreAsLinear` et `DisplayAsSrgb`.
- `MGColorPickerModel` separe maintenant valeur stockee et valeur affichee: `DisplayValue`, `PreviewDisplayValue`, `GetValueForColorSpace`, `GetDisplayText`, `IsDisplayDifferentFromStorage`.
- Les editions HSV/textuelles du picker travaillent dans l'espace d'affichage puis reconvertissent une seule fois vers l'espace de stockage.
- `MGColorPicker` expose les proprietes runtime et ajoute un warning visuel discret sur la zone texte quand l'espace d'affichage differe de l'espace stocke.
- Les wrappers XAML `ColorPicker` acceptent `StorageColorSpace` et `DisplayColorSpace`.
- Tests ajoutes pour roundtrip sRGB/Linear, extremes/mid-gray, preservation alpha, bascule d'affichage sans mutation de stockage, et edition display vers stockage Linear.
- Validation executee: `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorSpace --logger "console;verbosity=minimal"`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter ColorXaml --logger "console;verbosity=minimal"`.

### ✅ 20. Ajouter HDR, intensity et preview tonemappee

But:
etendre le picker au cas moteur moderne ou les composantes peuvent depasser `1.0`.

Travail attendu:

- activer `IsHdr` dans `ColorValue`, options, field et picker;
- ajouter sliders `Intensity` et optionnellement `Exposure` selon la complexite acceptable;
- ajouter `MinIntensity`, `MaxIntensity`, `ShowIntensity`, `UseExposureSlider`, `ShowToneMappedPreview`;
- definir le modele retenu: RGB direct HDR, couleur LDR + intensity, ou les deux via helper;
- afficher couleur brute et preview tonemappee;
- signaler visuellement les valeurs hors LDR;
- permettre normalisation base color + intensity;
- eviter de clamp par erreur les valeurs HDR dans les controles internes;
- garder `ToXnaColor()` comme conversion LDR explicite seulement.

Livrables:

- UI HDR fonctionnelle;
- tests de valeurs > 1, intensity et clamp explicite;
- sample emissive/light mis a jour.

Criteres d'acceptation:

- valeur `(4, 2, 1, 1)` peut etre editee sans etre perdue;
- le preview LDR ne remplace pas la valeur stockee HDR;
- intensity respecte min/max;
- les controles LDR existants gardent leur comportement.

Validation ciblee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Hdr --logger "console;verbosity=minimal"`;
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`.

Commit recommande:

- `colorpicker: complete task 20 add hdr and intensity editing`

Resultat:

- Ajout de `ColorHdrHelper` pour `GetIntensity`, normalisation base color + intensity, reconstruction HDR et tone mapping Reinhard explicite.
- `ColorPickerOptions` expose maintenant `MinIntensity` et `MaxIntensity` en plus des options HDR existantes.
- `MGColorPickerModel` supporte `SetIntensity`, `Intensity`, `BaseColor` et `GetToneMappedPreview` sans remplacer la valeur HDR stockee.
- `MGColorPicker` active `IsHdr`, `ShowIntensity`, `UseExposureSlider`, `ShowToneMappedPreview`, `MinIntensity` et `MaxIntensity`; un slider intensity optionnel edite les valeurs HDR en respectant les bornes.
- Le preview peut afficher une version tonemappee explicite; `ToXnaColor()` reste une conversion LDR clampée et ne modifie jamais la valeur stockee.
- Les wrappers XAML `ColorPicker` acceptent les options HDR/intensity.
- Tests ajoutes pour valeurs > 1, clamp LDR existant, normalisation ratio/intensity, bornes d'intensity et preview tonemappee non destructive.
- Validation executee: `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Hdr --logger "console;verbosity=minimal"`, `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`.

### ✅ 21. Ajouter temperature Kelvin et presets moteur

But:
ajouter des presets utiles aux editeurs de materiaux, lumieres, fog, sky, UI et debug.

Travail attendu:

- ajouter modele de presets couleur moteur avec categorie, nom, valeur et espace couleur;
- ajouter presets material base color, emissive, light, fog, sky, UI theme, debug/gizmo;
- ajouter conversion Kelvin -> RGB avec range configurable, par exemple `1000K..12000K`;
- ajouter slider Kelvin optionnel dans le picker quand `ShowTemperature` est active;
- ajouter presets candle, tungsten, warm white, neutral white, daylight, overcast, blue sky;
- definir si Kelvin applique en sRGB ou Linear et tester le chemin;
- integrer les presets dans `MGColorPaletteView` ou une zone presets dediee;
- eviter d'imposer les presets moteur aux usages UI simples.

Livrables:

- presets moteur et Kelvin;
- tests Kelvin valeurs de reference approximatives;
- sample lumiere/emissive.

Criteres d'acceptation:

- selectionner un preset applique la couleur au picker;
- Kelvin reste dans la range configuree;
- les presets peuvent etre masques/desactives;
- les tests acceptent une tolerance documentee pour Kelvin.

Validation ciblee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Kelvin --logger "console;verbosity=minimal"`;
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`.

Commit recommande:

- `colorpicker: complete task 21 add kelvin and engine presets`

Resultat:

- Ajout de `ColorTemperatureConverter` avec conversion Kelvin -> RGB sRGB, range configurable `1000K..12000K` par defaut, clamp robuste et sortie optionnelle Linear via `ColorSpaceConverter`.
- Ajout de `MGColorPresetCategory`, `MGColorPreset` et `MGColorEnginePresets` pour presets material, emissive HDR, light, fog, sky, UI theme, debug/gizmo et temperature.
- Presets temperature ajoutes: candle, tungsten, warm white, neutral white, daylight, overcast et blue sky.
- `ColorPickerOptions`, `MGColorPickerModel`, `MGColorPicker`, `MGColorPickerPopup` et les wrappers XAML exposent `ShowTemperature`, `MinKelvin` et `MaxKelvin`.
- `MGColorPicker` affiche un slider Kelvin optionnel et applique la temperature dans l'espace d'affichage avant conversion vers l'espace de stockage.
- `MGColorPaletteView.SetEnginePresets(...)` permet de charger les presets moteur a la demande, avec filtrage par categorie, sans les imposer aux usages UI simples.
- Sample `ColorPicker` enrichi avec un panneau light/emissive: picker HDR/intensity/temperature et palette engine presets liee au picker.
- Tests ajoutes pour valeurs Kelvin approximatives avec tolerance explicite, clamp de range, chemin Linear, application via model et metadata/categories de presets.
- Validation executee: `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Kelvin --logger "console;verbosity=minimal"`, `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`.

### ✅ 22. Ajouter persistance de palettes

But:
permettre aux palettes projet/utilisateur d'etre importees, exportees et restaurees sans coupler le controle a un editeur specifique.

Travail attendu:

- definir un format JSON simple pour `MGColorPalette` et `MGColorSwatch`;
- inclure `name`, `value`, `space`, et eventuellement `isHdr`/metadata future-compatible;
- ajouter serializer/deserializer sans dependance lourde non existante;
- valider les donnees chargees: noms manquants, valeurs invalides, doublons raisonnables;
- exposer API import/export sans acceder directement au file system si `MGUI.Core` doit rester decouple;
- ajouter hooks pour palettes recentes/favorites/projet;
- reporter `.gpl` et `.mgpalette` si le JSON MVP suffit, mais laisser le backlog documente.

Livrables:

- persistence JSON palettes;
- tests de roundtrip et erreurs;
- documentation courte du format dans ce fichier ou une doc dediee.

Criteres d'acceptation:

- une palette exportee puis importee conserve noms, couleurs et espace;
- input invalide ne crashe pas le loader;
- `MGUI.Core` ne depend pas d'un chemin projet concret;
- le format reste compatible avec futures valeurs HDR.

Validation ciblee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Palette --logger "console;verbosity=minimal"`.

Commit recommande:

- `colorpicker: complete task 22 add palette persistence`

Resultat:

- Ajout de `MGColorPaletteSerializer` avec format JSON versionne: `name`, `swatches`, valeur RGBA float, `space`, `isHdr` et `metadata` string/string future-compatible.
- Ajout de `MGColorPaletteSerializationResult` pour retourner `Success`, `Palette` et diagnostics sans jeter d'exception sur les inputs invalides.
- Import JSON tolerant: nom de palette manquant remplace par `Palette`, swatch sans nom remplace par `Color n`, doublons renommes `Name (2)`, espaces inconnus diagnostiques et rabattus en sRGB, swatches invalides ignorees.
- Export/import restent decouples du file system: API string JSON uniquement dans `MGUI.Core`.
- `MGColorPaletteStore` expose maintenant `ProjectPalettes`, `AddProjectPalette`, `ExportPalette` et `TryImportProjectPalette` pour hooks recent/favorites/projet sans dependance editeur.
- Le MVP JSON couvre les besoins actuels; `.gpl` et `.mgpalette` restent volontairement hors scope pour un backlog ulterieur.
- Tests ajoutes pour roundtrip noms/couleurs/space/HDR/metadata, JSON invalide sans crash, swatches invalides/doublons, et import projet via store sans file system.
- Validation executee: `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Palette --logger "console;verbosity=minimal"`.

### ✅ 23. Durcir accessibilite, navigation et performance

But:
stabiliser le controle pour un moteur rafraichi chaque frame, avec navigation clavier/gamepad et couts previsibles.

Travail attendu:

- verifier focus visible sur field, sliders, inputs, swatches et boutons;
- supporter Tab/Shift+Tab, Escape, Enter et navigation directionnelle;
- ajouter increments clavier: fleches petit pas, Shift grand pas, Ctrl precision fine si coherent avec les inputs MGUI;
- garantir que les controles disabled/read-only/mixed/null ont des indicateurs non uniquement bases sur la couleur;
- profiler ou auditer les allocations pendant drag;
- cacher gradients, damier et textures generees;
- eviter invalidations layout pendant interaction simple;
- limiter les updates PropertyGrid pendant drag selon commit mode;
- ajouter tests de navigation pure quand possible;
- remplir une note `Resultat` avec les risques perf restants.

Livrables:

- correctifs accessibilite/navigation;
- correctifs caches/performance;
- tests ou notes d'audit.

Criteres d'acceptation:

- picker utilisable sans souris pour les actions essentielles;
- pas d'allocation evidente dans les handlers de drag couleur;
- resize ou changement de mode regenere les caches, pas chaque draw;
- tous les etats UI importants sont visibles en dark/light.

Validation ciblee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`;
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`;
- tests filtres couleur/navigation disponibles.

Commit recommande:

- `colorpicker: complete task 23 harden accessibility and performance`

Resultat:

- `MGColorPicker` supporte maintenant une navigation clavier/gamepad interne: `MoveNext`/`MovePrevious` parcourt les zones visibles, fleches/increment/decrement modifient la zone active, `PageUp`/`PageDown` font un grand pas, `Home`/`End` vont aux bornes, `Enter` commit et `Escape` annule en mode explicite.
- Helpers internes testables ajoutes pour appliquer les actions de navigation aux cibles saturation/value, hue, alpha, intensity et temperature Kelvin.
- `MGColorPickerModel` conserve le dernier Kelvin applique afin que les increments clavier temperature soient predictibles.
- `MGColorPaletteView` ajoute `FocusedSwatchIndex`, une bordure de focus distincte, navigation directionnelle/Home/End/Page et selection par `Submit`.
- `MGColorField` affiche maintenant des glyphes non bases uniquement sur la couleur pour les etats null et mixed.
- `MGColorPicker` met en cache les gradients Hue et Kelvin par taille/espace/range, et `MGColorSlider` met en cache ses gradients par taille/channel/base color; ces caches sont regeneres au resize ou changement de parametres, pas a chaque draw.
- Audit allocation: les handlers de drag couleur restent des calculs sur structs/valeurs; les nouvelles allocations de cache sont hors chemin drag et liees aux changements de dimensions ou de parametres.
- Risques perf restants: le damier est encore dessine en tuiles rectangulaires faute d'abstraction texture/cache partagee dans cette couche; `SaturationValue` reste calcule a l'ecran car il depend de deux axes et du hue courant.
- Tests ajoutes pour navigation pure picker/palette.
- Validation executee: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`, `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`, `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "Color|Navigation" --logger "console;verbosity=minimal"`, `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`.

### ⚪ 24. Ajouter les previews avancees et backlog V3 borne

But:
traiter les demandes avancees sans transformer le MVP en chantier infini: contraste UI, preview contextuelle et backlog gradient/harmony/color blindness.

Travail attendu:

- ajouter si raisonnable une preview sur fond clair/fonce et moitie avant/apres;
- ajouter affichage rapide hex/RGB/HSV dans le picker si cela ne surcharge pas l'UI;
- ajouter calcul luminance relative et warning contraste texte pour themes UI;
- preparer interfaces ou notes pour preview materiau, sphere, gizmo et image custom;
- creer backlog explicite pour gradient editor, ramps, color harmony et color blindness preview;
- ne pas implementer les gros outils V3 sauf si deja trivialement supports par les briques existantes.

Livrables:

- previews avancees legeres;
- backlog V3 documente;
- tests de contraste/luminance si implementes.

Criteres d'acceptation:

- les previews ajoutees restent optionnelles;
- le picker compact ne devient pas encombre;
- la luminance/contraste est testee si exposee;
- les gros sujets V3 sont documentes et non melanges a l'implementation MVP/V2.

Validation ciblee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Contrast --logger "console;verbosity=minimal"` si contraste implemente;
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`.

Commit recommande:

- `colorpicker: complete task 24 add advanced preview backlog items`

Resultat:

- A remplir par l'agent.

### ⚪ 25. Documenter l'API et finaliser la migration

But:
clore la roadmap par une documentation utilisable et une verification de non-regression.

Travail attendu:

- mettre a jour `Docs/mgui_advanced_colorpicker_features.md` si la cible finale diverge de la specification initiale;
- ajouter un guide d'utilisation court pour `MGColorField`, `MGColorPicker`, PropertyGrid, XAML et palettes;
- documenter les formats supportes et l'ambiguite ARGB/RGBA;
- documenter les modes de commit et implications undo/redo;
- documenter sRGB/Linear, HDR/intensity et limites connues;
- documenter comment fournir un `IColorPickService` custom;
- ajouter notes de migration depuis `MGGridColorPicker` vers `MGColorPaletteView` sans casser l'ancien controle;
- executer une validation large et noter les resultats;
- verifier que toutes les taches terminees ont `✅` et une section `Resultat`.

Livrables:

- documentation finale;
- plan de migration;
- validation finale notee.

Criteres d'acceptation:

- un utilisateur peut declarer un champ couleur en XAML en suivant la doc;
- un utilisateur peut brancher le controle dans PropertyGrid;
- les limites hors perimetre sont explicites;
- la suite build/test ciblee est verte ou les exceptions sont documentees.

Validation ciblee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`;
- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --logger "console;verbosity=minimal"` si le temps d'execution est acceptable;
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`.

Commit recommande:

- `docs: complete task 25 document color picker api and migration`

Resultat:

- A remplir par l'agent.

## Definition of done globale

- Toutes les taches implementees sont marquees `✅`.
- Chaque tache terminee a exactement un commit associe.
- Les taches bloquees, s'il y en a, sont marquees `⛔` avec un blocage explicite.
- `MGGridColorPicker` reste disponible et compatible.
- `MGColorField` fonctionne hors PropertyGrid et dans PropertyGrid.
- Le MVP couvre LDR, alpha, HSV, RGB, Hex, previous/current, popup et commit/cancel.
- La V2 couvre HDR/intensity, sRGB/Linear, Kelvin/presets et eyedropper MGUI si toutes les taches correspondantes sont terminees.
- Les tests couleur, PropertyGrid, XAML et samples pertinents passent ou les limites sont documentees.