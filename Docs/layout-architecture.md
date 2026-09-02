# Architecture layout et responsive

## Objectif / Portee

Decrire l'etat actuel du systeme de layout responsive de MGUI : configuration, resolution des metriques, opt-in par sous-arbre, application centralisee dans le pipeline de mesure/arrangement, scaling du texte, anchors, exposition XAML, et regles d'usage. Ce document couvre l'integration du responsive avec le moteur de layout central (`MGElement`), pas le detail des algorithmes de mesure de chaque container.

## Vue d'ensemble

Le responsive layout est une preoccupation de layout, pas un transform de rendu.

Principes en vigueur :

1. `MGWindow.Scale` reste un pur transform de rendu et ne doit jamais devenir le mecanisme responsive. Simuler l'adaptation en redimensionnant les fenetres via `MGWindow.Scale` couplerait layout, input, clipping et texte a un transform de rendu — c'est un non-but explicite.
2. Quatre notions restent strictement separees : layout logique, scale global de l'UI (`UIScaleFactor`), scale du texte (`TextScaleFactor`), transforms de rendu existants.
3. Le responsive est opt-in au niveau d'une racine de sous-arbre, puis herite par les descendants. Les arbres non opt-in sont strictement inchanges (compatibilite descendante).
4. Les containers de layout existants (`MGStackPanel`, `MGDockPanel`, `MGGrid`, etc.) restent le modele de composition principal ; les anchors ne couvrent que les overlays et elements flottants.
5. Les valeurs en pixels ecrites par l'auteur sont des valeurs en espace design : quand le sous-arbre est responsive, elles sont resolues via les metriques actives.

## Types du modele responsive

Tous dans `MGUI.Core/UI/Responsive/` :

| Type | Role |
| --- | --- |
| `UIDesignResolution` | Resolution de reference d'autorat (record struct immutable, valide > 0 ; `UIDesignResolution.HD` = 1920x1080) |
| `UIResponsiveSettings` | Configuration : design resolution, `UIScaleMode`, clamps `MinUIScaleFactor`/`MaxUIScaleFactor` (defauts 0.5/4.0), `TextScaleMultiplier` (defaut 1.0), clamps `MinTextScaleFactor`/`MaxTextScaleFactor` (defauts 0.85/3.0), `UseDpiScale` (defaut false). `UIResponsiveSettings.Default` = HD |
| `UIResolvedMetrics` | Metriques resolues : `WidthRatio`, `HeightRatio`, `ViewportScaleFactor`, `DpiScaleFactor`, `UIScaleFactor`, `TextScaleFactor`, `IsDpiApplied`, `IsIdentity` |
| `UIResponsiveScaleMode` | Strategie de scale ; seule valeur actuelle : `UniformFit` |
| `UIResponsiveResolver` | `Resolve(settings, viewportSize, dpiScale)` : calcul pur et deterministe des metriques |
| `UIResponsiveMath` | Helpers de scaling : `ScaleInt` (arrondi away-from-zero), `ScaleNullableInt`, `ScaleThickness`, `ScaleSize`, `ClampPositive` |
| `ResponsiveAnchor` | Enum d'ancrage : `None`, 9 positions (`TopLeft` ... `BottomRight`), `StretchHorizontal`, `StretchVertical`, `Stretch` |

Formule de resolution (`UIResponsiveResolver.Resolve`) :

- `widthRatio` / `heightRatio` = viewport / design resolution ;
- `viewportScale` = `min(widthRatio, heightRatio)` en mode `UniformFit` ;
- multiplicateur DPI applique seulement si `UseDpiScale` (borne a >= 0.1) ;
- `UIScaleFactor` = clamp(`viewportScale * dpi`, min/max UI) ;
- `TextScaleFactor` = clamp(`UIScaleFactor * TextScaleMultiplier`, min/max texte) — le texte peut donc diverger du scale UI ;
- viewport degenere (largeur ou hauteur <= 0) => metriques identite.

## Propriete et resolution : MGDesktop

`MGDesktop` (`MGUI.Core/UI/MGDesktop.cs`) possede la configuration globale et resout les metriques depuis le viewport actif :

- `MGDesktop.ResponsiveSettings` (`UIResponsiveSettings`, defaut `UIResponsiveSettings.Default`) ;
- `MGDesktop.EffectiveDpiScale` (`float`, fourni par l'application, defaut 1.0) ;
- `MGDesktop.ResponsiveMetrics` (`UIResolvedMetrics`) + evenement `ResponsiveMetricsChanged`.

`RecalculateResponsiveMetrics` recalcule via `UIResponsiveResolver.Resolve(ResponsiveSettings, ValidScreenBounds.Size, EffectiveDpiScale)` :

- au changement de `ResponsiveSettings` ou de `EffectiveDpiScale` ;
- a chaque `MGDesktop.Update` (couvre les changements de viewport).

Quand les metriques changent effectivement, l'invalidation est globale : `RefreshTextLayouts` (rafraichit le moteur de texte) puis `InvalidateResponsiveLayouts` qui appelle `InvalidateLayoutTree` sur chaque `MGWindow` et sur l'`OverlayWindow`. Les metriques sont partagees par toutes les fenetres — il n'y a pas de variante par fenetre.

## Opt-in et application centrale : MGElement

Opt-in (`MGUI.Core/UI/MGElement.cs`, region `Responsive`) :

- `MGElement.UseResponsiveLayout` est `bool?` ; `IsResponsiveLayoutEnabled` = `UseResponsiveLayout ?? Parent?.IsResponsiveLayoutEnabled ?? false` (heritage par la chaine parent, defaut false) ;
- `MGResponsiveRoot` (`MGUI.Core/UI/Containers/MGResponsiveRoot.cs`) est la racine d'opt-in officielle : sous-classe de `MGOverlayPanel` qui pose `UseResponsiveLayout = true`, alignements stretch et `ClipToBounds = true`.

Application du `UIScaleFactor` : elle est centralisee dans `MGElement`, pas dupliquee dans chaque container. Le pipeline de mesure/layout consomme des valeurs resolues :

- `ResolvedMargin` / `ResolvedPadding` : `UIResponsiveMath.ScaleThickness` avec `ResponsiveSpacingScaleFactor`, gate par `ScaleSpacingWithResponsive` (defaut true) ;
- `ResolvedMinWidth/Height`, `ResolvedMaxWidth/Height`, `ResolvedPreferredWidth/Height` : `ResponsiveLayoutScaleFactor`, gate par `ScaleDimensionsWithResponsive` (defaut true) ;
- `ResolveExternalSpacing(Thickness)` : scaling des espacements externes fournis par un container (utilise par `MGOverlayPanel` pour les offsets d'enfants).

Les facteurs valent 1.0 hors sous-arbre responsive : `ResponsiveLayoutScaleFactor` et `ResponsiveSpacingScaleFactor` retournent `ResponsiveMetrics.UIScaleFactor` uniquement si `IsResponsiveLayoutEnabled` et le gate correspondant sont vrais.

## Scaling du texte : MGTextBlock

Le scale du texte est independant du scale UI (`MGUI.Core/UI/MGTextBlock.cs`) :

- `MGTextBlock.UseResponsiveTextScale` est `bool?` ; par defaut il suit la participation responsive du sous-arbre (`UseResponsiveTextScale ?? IsResponsiveLayoutEnabled`) ;
- quand actif, `TextScaleFactor` scale la taille de police effective (`EffectiveFontSize`, minimum 1) et le `LinePadding` effectif, donc la mesure du texte suit.

## Anchors : MGOverlayPanel

Les anchors servent au positionnement type overlay/HUD, jamais a remplacer les containers de layout.

- `MGElement.ResponsiveAnchor` (`ResponsiveAnchor`, defaut `None`) est consomme par `MGOverlayPanel` (`MGUI.Core/UI/Containers/MGOverlayPanel.cs`), donc aussi par `MGResponsiveRoot` ;
- enfant avec `None` : layout sur les bounds disponibles complets ;
- enfant ancre : mesure via `UpdateMeasurement`, puis bounds calcules par `MGOverlayPanel.CreateAnchoredBounds(availableBounds, desiredSize, anchor)` (taille bornee aux bounds disponibles ; variantes stretch remplissent l'axe correspondant) ; les offsets du container passent par `ResolveExternalSpacing`, donc sont scales en responsive.

## Exposition XAML

- `MGUI.Core/UI/XAML/Element.cs` : `UseResponsiveLayout`, `ScaleSpacingWithResponsive`, `ScaleDimensionsWithResponsive`, `ResponsiveAnchor` sur tout element ;
- `MGUI.Core/UI/XAML/Containers.cs` : type `ResponsiveRoot` (alias `RR` dans `XAMLParser`) ;
- `MGUI.Core/UI/XAML/Controls.cs` : `UseResponsiveTextScale` sur `TextBlock`.

Exemple declaratif complet : `MGUI.Samples/Features/ResponsiveLayout.xaml` (`ResponsiveLayoutSample` dans `MGUI.Samples`) — racine responsive, layouts, anchors et text scale separe.

## Regles d'usage

Adoption d'un ecran :

1. laisser intacts les ecrans qui n'ont pas besoin de responsive ;
2. envelopper le sous-arbre adaptatif dans `MGResponsiveRoot` (ou poser `UseResponsiveLayout = true` sur sa racine) ;
3. configurer `MGDesktop.ResponsiveSettings` avec une design resolution deliberee : 1920x1080 est un bon defaut, 1600x900 pour une baseline plus dense. La design resolution est l'espace de reference d'autorat, pas une taille de rendu cible — ne pas choisir une valeur artificiellement petite pour forcer l'upscale ;
4. composer la structure principale avec les containers standards (`MGStackPanel`, `MGDockPanel`, `MGGrid`) ; reserver `ResponsiveAnchor` aux badges de coin, compteurs HUD, panneaux flottants, overlays centres, widgets colles aux bords ;
5. ecrire les valeurs en unites design-space et garder des clamps min/max raisonnables pour que l'UI reste utilisable sur tres petits et tres grands ecrans ;
6. activer `UseResponsiveTextScale` sur les titres, corps de texte et labels HUD ; le laisser desactive quand une taille exacte doit etre preservee ;
7. rollout DPI : livrer avec DPI desactive, valider sur plusieurs tailles de viewport, puis activer `ResponsiveSettings.UseDpiScale` une fois que l'application dispose d'une source fiable pour `EffectiveDpiScale`.

## Guidance pour nouveaux panels

Un nouveau container participe automatiquement au responsive pour tout ce qui transite par le pipeline central : margins, padding, tailles preferees et min/max de ses enfants sont deja resolus par les proprietes `Resolved*` de `MGElement`. Regles a respecter :

- ne jamais lire `MGWindow.Scale` ni les metriques de rendu pour adapter le layout ;
- pour un espacement possede par le panel lui-meme (equivalent de `MGStackPanel.Spacing`), le scaling n'est pas automatique : utiliser `ResponsiveSpacingScaleFactor` avec `UIResponsiveMath.ScaleInt`/`ScaleThickness` si la semantique design-space est voulue (voir Limites connues) ;
- pour des offsets externes appliques aux enfants, passer par `MGElement.ResolveExternalSpacing` comme le fait `MGOverlayPanel` ;
- eviter toute arithmetique en pixels bruts hors du pipeline de mesure/arrangement : elle ne sera pas resolue par les metriques.

Tests du resolveur et des anchors : `MGUI.Tests/Architecture/ResponsiveMetricsResolverTests.cs`.

## Limites connues

- Les metriques sont resolues depuis le viewport du desktop (`ValidScreenBounds`), pas par fenetre.
- Les espacements possedes par les containers (`MGStackPanel.Spacing`, `MGGrid.RowSpacing`/`ColumnSpacing`, `MGUniformGrid.RowSpacing`/`ColumnSpacing`, `MGWrapPanel.Spacing`) ne sont pas scales par les metriques responsive ; seul le scaling central de `MGElement` s'applique.
- Les controles qui font de l'arithmetique en pixels bruts hors du pipeline central necessitent une adoption incrementale.
- Le DPI est opt-in et desactive par defaut.
- Une seule strategie de scale existe (`UniformFit`).

## Reste a faire

Voir `Docs/Tasks/layout-tasks.md`.
