# Architecture drawing : formes, geometrie et paints

## Objectif / Portee

Ce document decrit l'etat actuel du pipeline de dessin des formes box-like de MGUI : modele de forme normalise, construction de geometrie reutilisable, contrats de paint (fills et borders), cycle de vie des paints stateful, et separation entre le paint de forme et le clip de contenu. Le backend de clip (scissor/stencil/mask) est traite dans `Docs/rendering-architecture.md` ; ce document ne couvre que la frontiere cote paint.

## Vue d'ensemble

Regle directrice : les controles choisissent une forme, le builder de geometrie calcule une topologie reutilisable, les paints colorent ou texturent cette topologie, et les backends de clip forment une couche separee appliquee ensuite.

- `MGCornerRadius` (`MGUI.Core/UI/MGCornerRadius.cs`) : contrat public de rayon de coin.
- `MGBoxShape` (`MGUI.Core/UI/MGBoxShape.cs`) : forme box normalisee (bounds, epaisseur de bordure, rayon de coin).
- `MGBoxGeometryBuilder` (`MGUI.Core/UI/Shapes/MGBoxGeometryBuilder.cs`) : tessellation et cache.
- `MGBoxGeometry` (`MGUI.Core/UI/Shapes/MGBoxGeometry.cs`) : payload de geometrie reutilisable.
- `DrawTransactionBoxShapeExtensions` (`MGUI.Core/UI/Shapes/DrawTransactionBoxShapeExtensions.cs`) : primitives de rendu bas niveau.
- Brushes fill/border (`MGUI.Core/UI/Brushes/`) : application des couleurs, textures, gradients, animations.

## Modele de forme

`MGBoxShape` est un `record struct` qui possede :

- `OuterBounds`, `BorderThickness`, `CornerRadius` (entrees brutes) ;
- `NormalizedBorderThickness`, `NormalizedCornerRadius` (clamp et validation) ;
- valeurs derivees `InnerBounds` et `InnerCornerRadius` ;
- `Normalize()` qui retourne la forme normalisee, plus `HasBorder` / `HasRoundedCorners` / `IsNormalized` ;
- `Contains(Vector2)` : hit test analytique dans l'espace pixel de `OuterBounds` (rectangle inclusif plus quadrants de coins bases sur `NormalizedCornerRadius`), sans dependance a la tessellation.

La normalisation et le clamp vivent a cette couche, de sorte que la geometrie et le cache en aval ne consomment que des entrees normalisees. Le modele de forme ne possede jamais : generation de vertex, winding de polygones, projection de texture, echantillonnage de gradient, batching.

## Builder de geometrie

`MGBoxGeometryBuilder.Build(shape, cornerSegmentCount, scale)` convertit une `MGBoxShape` normalisee en `MGBoxGeometry` :

- contour externe (`OuterContour`), contour interne (`InnerContour`), vertices partages (`Vertices`), indices de fill (`FillIndices`), indices d'anneau de bordure (`BorderRingIndices`), et flag `UsesRectangleFastPath` ;
- le cache est centralise dans le builder, cle uniquement par forme normalisee + parametres de tessellation (`cornerSegmentCount`, `scale`) — jamais par etat d'animation ou etat visuel (`ClearCache()` et `CachedGeometryCount` existent pour les tests) ;
- le branchement fast-path rectangle vs chemin arrondi est decide ici et dans les primitives, jamais dans les controles ou les brushes.

Le builder ne possede jamais : couleurs, textures, gradients, etat d'animation, overlays d'etat visuel. `MGBoxShapeRegionHelper.CreateSubShape` (interne) derive des sous-formes regionales d'une forme hote.

## Primitives de rendu

`DrawTransactionBoxShapeExtensions` est le pont bas niveau qui possede l'emission de triangle-lists :

- `FillRoundedRectangle`, `StrokeRoundedRectangle`, `DrawBorderRing` sur `IUIDrawContext`, en variantes forme ou geometrie ;
- chaque primitive preserve le fast path : quand `MGBoxGeometry.UsesRectangleFastPath` est vrai, elle route vers `FillRectangle` / `StrokeRectangle` classiques ;
- chemin texture : `IUIDrawContext.DrawTexturedTriangleList(origin, image, vertices, uv, indices, couleur)` (`MGUI.Shared/Rendering/IUIDrawContext.cs`) dessine une triangle-list texturee avec des UV normalises ; `DrawTransaction` (`MGUI.MonoGame.Integration/Rendering/DrawTransaction.cs`) l'implemente via un `BasicEffect` partage par le renderer, avec les memes matrices et etats GPU que le contexte primitives (donc compatible avec le clip scissor/stencil et le `SamplerType` courant, Wrap pour tuiler). `FillTexturedRoundedRectangle` et `DrawTexturedBorderRing` emettent respectivement le mesh de fill et l'anneau de bordure d'une `MGBoxGeometry` ; les regles de mapping UV appartiennent aux paints, et les paints textures gardent leur propre chemin rectangle quand `UsesRectangleFastPath` est vrai (rendu identique a l'existant).

Nouvelle primitive partagee = ici ou dans la couche de rendu partagee ; ne jamais apprendre aux controles ou aux brushes a emettre de la topologie triangle directement.

## Contrats de paint

### Overloads

- `IFillBrush` (`MGUI.Core/UI/Brushes/Fill Brushes/IFillBrush.cs`) expose `Draw(DA, Element, Rectangle)` (legacy) et `Draw(DA, Element, MGBoxShape, MGBoxGeometry)` (prefere) ; l'implementation par defaut de l'overload shape-aware retombe sur `Shape.OuterBounds`.
- `IBorderBrush` (`MGUI.Core/UI/Brushes/Border Brushes/IBorderBrush.cs`) expose `Draw(DA, Element, Rectangle, Thickness)` (legacy) et `Draw(DA, Element, MGBoxShape, MGBoxGeometry)` (prefere) ; le fallback par defaut utilise `Shape.OuterBounds` + `Shape.NormalizedBorderThickness`.

Les overloads rectangle restent une API publique stable. L'overload rectangle est le fallback sanctionne uniquement quand la capacite manquante est specifiquement liee au rendu : clipping, projection UV, masquage non rectangulaire. Il existe exactement un pipeline canonique de geometrie arrondie ; aucune logique de compatibilite par brush.

### Interdictions pour les brushes

Un brush ne doit jamais devenir un builder de geometrie fantome. Il ne doit jamais redefinir :

- le contour externe ou interne d'un rectangle arrondi ;
- la topologie de l'anneau de bordure ;
- les regles de clamp des rayons de coin invalides ;
- la selection du fast path pour `CornerRadius == 0`.

Restent acceptables cote paint : mapping UV/destination de texture, regles d'interpolation de couleur, overlays d'etat visuel, composition de plusieurs paints sur la meme geometrie.

### Regle de placement

- La logique change le contour ou la topologie de la box -> couche forme ou geometrie.
- La logique change couleur, texture, interpolation, layering ou animation sur un contour existant -> couche paint.
- La logique optimise le dispatch rectangle-vs-arrondi -> chemin rendu/geometrie, pas les controles.

## Cycle de vie des paints stateful

- La normalisation de forme et la generation de geometrie sont deterministes et sans etat : memes entrees, meme resultat.
- Seule la couche paint peut dependre du temps ecoule, de l'etat visuel hover/pressed/focused, ou de valeurs de controle comme une progression.
- Un paint stateful ne redefinit jamais la topologie des contours, l'anneau de bordure, ni les regles de normalisation.
- Les hooks d'update restent cote paint : `IBorderBrush.Update(UpdateBaseArgs)` et `IFillBrush.Update(UpdateBaseArgs)` existent avec une implementation par defaut vide ; `MGHighlightBorderBrush.Update` fait tourner son animation et transfere l'appel a son `Underlay`.
- Les paints composites transferent les appels de cycle de vie a leurs enfants : `MGBorderedFillBrush` (fill et border), `MGCompositedFillBrush`, `MGPaddedFillBrush`, `VisualStateFillBrush` (etats dedupliques par reference, car les constructeurs a un seul brush partagent l'instance entre etats), `MGUniformBorderBrush`, `MGDockedBorderBrush` (cotes dedupliques par reference), `MGBandedBorderBrush`, `MGCompositedBorderBrush`, `MGHighlightBorderBrush`.
- Point d'entree unique : `MGElement.Update` tick, une fois par frame, `GetVisualStateFillBrushes()` (par defaut `BackgroundBrush`), `GetFillBrushes()` (par defaut `OverlayBrush`) et `GetBorderBrushes()`. Un controle qui dessine lui-meme d'autres emplacements `IFillBrush` / `VisualStateFillBrush` surcharge ces hooks (voir Limites connues pour la liste et les exclusions).

## Etat des paints avances

### Consomment pleinement la geometrie arrondie

- Fills solides et bordures uniformes solides (rendu direct depuis la geometrie fournie).
- Paints composites, fills avec padding, fills avec bordure, bordures en bandes, bordures dockees (composition sur le meme contrat de geometrie).
- Gradients arrondis : `MGGradientFillBrush` colore les vertices du mesh de fill fourni (`Geometry.Vertices` + `Geometry.FillIndices`) au lieu de reconstruire la geometrie.
- Fills textures : `MGTextureFillBrush` projette ses UV sur `Geometry.Vertices` (chemin `DrawTexturedTriangleList`). Regles conservees dans le paint : `Fill` et `UniformToFill` projettent depuis la destination etiree ; `Uniform` et `None` projettent de la meme facon sous un clip rectangulaire exprime en espace ecran (la partie non couverte de la forme reste vide, comme sur le chemin rectangle) ; `Tile` sur la texture entiere tuile par sampler Wrap avec des UV en unites de tuile. Cas restant : `Tile` sur un sous-rectangle d'atlas (un sampler Wrap repeterait tout l'atlas) garde le chemin rectangle, commente dans le code.
- Bordures texturees : `MGTexturedBorderBrush` conserve exactement la disposition du chemin rectangle (texture de coin sur les blocs de coin de la taille de l'epaisseur, textures de bord sur les rectangles de bord entre ces blocs) et laisse l'anneau arrondi couper cette disposition : chaque quad de `BorderRingIndices` est decoupe (Sutherland-Hodgman) contre chaque rectangle de la disposition, les morceaux sont triangules en eventail et emis par region (au plus 8 appels), de sorte que les coutures suivent les lignes de la disposition et non les quads. La partie de l'anneau plus profonde que l'epaisseur dans le carre d'un coin (quand le rayon depasse l'epaisseur) n'appartient a aucun rectangle de la disposition : elle est decoupee a part et peinte avec la texture de coin en coordonnees clampees, donc l'anneau n'a jamais de trou et les morceaux couvrent exactement son aire. Rotations et reflexions de `TextureTransforms` sont appliquees autour du centre de chaque rectangle avec la meme taille ajustee que le chemin rectangle (UV toujours dans [0,1], y compris pour un angle libre). Cas restant : quand une epaisseur atteint le rayon du coin, l'arc interne s'effondre et `MGBoxGeometryBuilder.BuildBorderRingIndices` ne produit pas d'anneau (`HasBorderRingMesh` faux) ; le paint garde alors le chemin rectangle, commente dans le code.

### Encore lies au rectangle (par conception, localise dans le paint)

- `MGNineSliceFillBrush` : cible des destinations rectangulaires tant que la decomposition en patchs arrondis n'existe pas.
- `MGHighlightFillBrush` et les modes `Progress` / `Scan` de `MGHighlightBorderBrush` : logique d'exclusion orientee rectangle. Les modes `Pulse` et `Flash` passent par le chemin shape-aware (`MGHighlightBorderBrush.Draw(..., MGBoxShape, MGBoxGeometry)`).

Ces limitations sont volontairement localisees dans les implementations de paint, commentees a l'endroit du repli, suivies dans `Docs/Tasks/drawing-tasks.md` (Tache 4), et ne fuient jamais dans `MGBorder`, `MGRectangle` ni le builder de geometrie.

### Chemins d'extension prepares

- Nine-slice arrondi : decomposer les neuf patchs sur la geometrie arrondie via `DrawTexturedTriangleList`.
- Highlight : parametrer `Progress` / `Scan` le long du contour arrondi et construire les masques d'exclusion sur le mesh.
- Traits multi-bandes : deriver des `MGBoxShape` imbriquees depuis `InnerBounds` et `InnerCornerRadius`.
- Paints composes : mixer passes fill et border sans reconstruire la geometrie en restant dans les overloads shape-aware.

Regle : un nouveau paint avance consomme d'abord `MGBoxGeometry` et ne retombe sur un comportement rectangulaire que la ou la fonctionnalite manquante est le clipping, le mapping UV ou les maths d'exclusion non rectangulaires.

## Separation paint de forme vs clip de contenu

Le pipeline de paint arrondi et le pipeline de clip composable sont volontairement separes :

- les fonds et bordures arrondis n'exigent jamais de stencil ; le stencil n'est requis que quand un controle demande un clip non rectangulaire sur son contenu ;
- le code de paint choisit comment dessiner les visuels ; le code de clip choisit comment contraindre le dessin ulterieur ; `ClipStrategyResolver` (`MGUI.Shared/Rendering/Clipping/ClipStrategyResolver.cs`) decide scissor vs stencil vs mask selon `ClipBackendCapabilities` — le code de paint ne choisit jamais le backend de clip.

Concretement dans `MGUI.Core/UI/MGBorder.cs` :

- `DrawBackground` et `DrawSelf` rendent via `MGBoxShape` + `MGBoxGeometry` (brushes shape-aware) ;
- `GetContentsClipDefinition` reutilise cette meme geometrie uniquement pour decrire un clip de contenu arrondi (`MGElement.CreateBorderBackedContentsClipDefinition`) ;
- `MGElement.Draw` applique d'abord le self clip, puis le content clip via `PushClipTemporary`, et libere le content clip avant les overlays.

`MGBoxGeometry` est la couture qui permet au paint arrondi et au clip de contenu arrondi de partager une seule description de forme normalisee sans coupler le paint a la politique de clip GPU.

## Comment un controle dessine une forme themee

- `MGBorder` est le point de reutilisation : de nombreux controles (`MGButton`, `MGComboBox`, `MGTextBox`, `MGWindow`, `MGProgressBar`, etc.) exposent leurs proprietes de bordure en les transferant a un `MGBorder` interne. `MGBorder.DrawSelf` construit `new MGBoxShape(LayoutBounds, BorderThickness, CornerRadius).Normalize()`, appelle `MGBoxGeometryBuilder.Build`, puis passe forme et geometrie aux brushes de bordure et aux overlays.
- `MGRectangle.DrawSelf` (`MGUI.Core/UI/MGRectangle.cs`) montre le motif minimal : construire la forme et la geometrie une fois, puis dessiner le fill (`Fill?.Draw(DA, this, shape, geometry)`) et le trait (`MGUniformBorderBrush` sur la meme geometrie).

### Recettes

- Nouveau paint : decider fill ou border ; implementer l'overload shape-aware ; consommer `MGBoxGeometry` au lieu de reconstruire les contours ; garder les limitations UV/masquage locales au paint.
- Nouvelle primitive : dans `DrawTransactionBoxShapeExtensions` ou la couche de rendu partagee, seulement si plusieurs paints en beneficient.
- Nouvelle famille de forme : modele de forme dedie (equivalent de `MGBoxShape`), payload de geometrie, builder avec normalisation et cache, primitives draw-transaction ; etendre les brushes seulement si la nouvelle forme se peint via une abstraction commune. Garder l'ordre en couches : semantique de forme d'abord, backend de rendu ensuite, adoption par les controles en dernier.

## Limites connues

- Cycle de vie des fill brushes : sont tickes une fois par frame les paints atteints par `MGElement.Update`, c'est-a-dire `BackgroundBrush` (tous etats, dedupliques par reference), `OverlayBrush`, les surcharges de `GetFillBrushes()` / `GetVisualStateFillBrushes()` (`MGSlider`, `MGRectangle`, `MGShapeElementBase`, `MGScrollViewer`, `MGProgressButton`, `MGProgressBar`, `MGGridColorPicker`, `MGOverlayHost`, `MGUniformGrid`, `MGGrid`, `MGGridSplitter`) et les border brushes de `GetBorderBrushes()`. Ne sont volontairement pas surcharges : les proprietes qui redirigent vers le `BackgroundBrush` d'un enfant, deja ticke par cet enfant (`MGSpoiler.UnspoiledBackgroundBrush`, `MGTabControl.HeaderAreaBackground`, `MGExpander.ExpanderButtonBackgroundBrush`, `MGToggleButton.CheckedBackgroundBrush`) ; les brushes modeles assignes par reference au `BackgroundBrush` d'autres elements, qui sont tickes une fois par element qui les recoit et par frame (`MGDockAutoHideStrip.ButtonBackgroundBrush` : une fois par bouton ; `MGTreeView.SelectionBackgroundBrush` : deux fois pour l'item selectionne, HeaderPanel et HeaderContainer ; `MGListBox.AlternatingRowBackgrounds` : une fois par ligne) ; les brushes d'etat dont seule la valeur courante est recopiee, donc tickes uniquement tant qu'ils sont l'etat courant (`MGDockSplitterBar` et `MGDockTabItem` Normal/Hover/Pressed-Active) ; et les brushes de `MGGraphView` dont seule la couleur est extraite. Regle generale : un paint stateful est ticke une fois par frame et par emplacement qui le reference, dans le meme element ou dans des elements differents ; un paint partage par reference entre N elements avance donc N fois plus vite, comme c'est deja le cas pour un border brush partage. Suivi : `Docs/Tasks/drawing-tasks.md` Tache 4.
- Paints encore lies au rectangle (voir la matrice ci-dessus), chacun avec un repli commente dans le code et un item en Tache 4 : `MGNineSliceFillBrush` (pas de decomposition des patchs sur la geometrie arrondie), `MGHighlightFillBrush` (exclusions par soustraction de rectangles), modes `Progress` / `Scan` de `MGHighlightBorderBrush` (parcours du perimetre rectangulaire), `MGTextureFillBrush` en mode `Tile` sur un sous-rectangle d'atlas, et `MGTexturedBorderBrush` quand l'anneau de bordure est vide parce qu'une epaisseur atteint le rayon du coin (`MGBoxGeometryBuilder.BuildBorderRingIndices` exige des contours de meme taille).
- Hit testing : `MGBoxShape.Contains(Vector2)` fournit un test analytique (rectangle inclusif plus quadrants de coins, base sur `NormalizedCornerRadius`, sans dependance a la tessellation). Seul `MGBorder` l'utilise, et uniquement quand `IsShapeAwareHitTestEnabled` est vrai (false par defaut, miroir XAML `Border.IsShapeAwareHitTestEnabled`) ; le reste du framework, y compris `MGRectangle` et les controles qui embarquent un `MGBorder`, reste teste sur ses bounds rectangulaires.
- `MGRatingControl` (`MGUI.Core/UI/MGRatingControl.cs`, rendu des items) dessine polygones et cercles directement via `DrawTransaction` (`StrokeAndFillPolygon`, `FillCircle`, `StrokeCircle`) et reste hors du pipeline box-shape — futur consommateur d'un pipeline de formes generalise.
- Les overloads rectangle legacy `IFillBrush.Draw(DA, Element, Rectangle)` et `IBorderBrush.Draw(DA, Element, Rectangle, Thickness)` subsistent en API publique aux cotes des overloads shape-aware.

## Reste a faire

Le travail restant du theme drawing est specifie dans `Docs/Tasks/drawing-tasks.md`.
