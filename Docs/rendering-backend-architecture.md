# Rendering Backend Architecture

## Objectif

Ce document decrit l'etat final du chantier de decouplage du rendu.

Le point clef est le suivant:

- `MGUI.Core` porte la logique UI, pas le backend de draw concret ;
- `MGUI.MonoGame.LegacyRenderer` porte l'implementation MonoGame concrete et le point d'entree de composition backend ;
- `MGUI.MonoGame.Integration` porte la couche de support MonoGame partagee entre le coeur UI, le texte optionnel et les backends proprietaires ;
- `MGUI.Rendering.Abstractions` et `MGUI.Shared` contiennent les contrats consommes de part et d'autre de cette frontiere ;
- `MGUI.FontStashSharp` reste une integration texte optionnelle construite au-dessus de cette separation.

## Separation finale des projets

### `MGUI.Rendering.Abstractions`

Responsabilites:

- contrats backend-neutral extraits du coeur du rendu ;
- types de texte partages comme `FontSpec`, `GlyphMetrics` et `CustomFontStyles` ;
- handle d'image opaque `IUIImageResource`.

Ce projet ne doit pas referencer MonoGame.

### `MGUI.Shared`

Responsabilites:

- contrats et helpers partages entre le coeur UI et le backend ;
- runtime desktop-facing `IUIDesktopRuntime` ;
- surfaces de draw `IUIDrawContext`, `IUIRenderContext` et `IUIDrawTransaction` ;
- contrats texte `ITextMeasurementEngine`, `ITextDrawEngine` et `ITextEngine` ;
- helpers transverses, XAML, theming partage et logique commune.

`MGUI.Shared` n'est plus le proprietaire de compilation des implementations MonoGame concretes.

### `MGUI.Core`

Responsabilites:

- controles, layout, theming, styles, XAML runtime et logique UI ;
- consommation des contrats de runtime, de texte et de draw ;
- ressources UI haut niveau comme `MGTextureData`, `MGResources`, brushes et shapes.

Le coeur UI doit preferer les contrats partages et eviter de dependere du backend concret, sauf ponts legacy explicitement assumes.

### `MGUI.MonoGame.Integration`

Responsabilites:

- contrats, hosts partages et types de support MonoGame encore consommes par `MGUI.Core`, `MGUI.FontStashSharp` et des backends possedes par le moteur ;
- infrastructure texte/image partagee qui ne doit pas forcer le pipeline renderer historique ;
- aucun `MainRenderer`, `DrawTransaction` ou helper GPU legacy compile sur le chemin nominal.

Ce projet n'est pas le backend applicatif recommande pour un consommateur MonoGame standard.

### `MGUI.MonoGame.LegacyRenderer`

Responsabilites:

- `MainRenderer`, `DrawTransaction`, hosts MonoGame, surfaces et pools GPU ;
- chargeurs d'assets et wrappers d'images MonoGame ;
- implementation texte SpriteFont concrete ;
- point d'entree de composition backend `MGUI.Backend.MonoGame.MonoGameBackendBootstrap`.

Ce projet est la dependance concrete que les apps MonoGame doivent referencer explicitement quand elles utilisent le renderer upstream fourni par MGUI.

### `MGUI.FontStashSharp`

Responsabilites:

- moteur texte alternatif au-dessus des contrats partages et du backend MonoGame ;
- aucune responsabilite de bootstrap applicatif.

## Flux d'integration recommande

1. L'application MonoGame reference `MGUI.Core` et `MGUI.MonoGame.LegacyRenderer` directement.
2. Elle choisit un host MonoGame concret:
   - `GameRenderHost<TObservableGame>` si le `Game` publie deja `PreviewUpdate` et `EndUpdate` ;
   - `DelegateRenderHost` si la boucle d'update reste pilotee manuellement.
3. Elle cree le renderer concret via `MGUI.Backend.MonoGame.MonoGameBackendBootstrap.Create(...)`.
4. Elle construit `MGDesktop` depuis `IUIDesktopRuntime`, pas depuis un constructeur concret par defaut.
5. Elle charge ses ressources et utilise ensuite `MGDesktop.Update()` / `MGDesktop.Draw()` comme avant.

## Flux d'integration d'un backend custom

Un moteur proprietaire ne doit pas partir de `MainRenderer` ni d'un host MonoGame puis essayer de les contourner.

Le flux recommande est le suivant:

1. implementer un runtime applicatif qui satisfait `IUIDesktopRuntime` ;
2. exposer une surface logique via `IUISurface`, avec `GetRenderTarget() == null` pour le backbuffer ou un `IUIRenderTarget` opaque pour une surface offscreen ;
3. implementer un `IUIDrawTransaction` / `IUIRenderContext` capable de dessiner les shapes, les images et de changer de buffer via `SetRenderTargetTemporary(...)` ;
4. implementer un `ITextEngine` pour la resolution, la mesure et le draw du texte ;
5. construire `MGDesktop` directement avec ce runtime puis laisser `UIView` router le draw vers la surface choisie par le moteur ;
6. reserver tout type concret MonoGame au seul backend `MGUI.MonoGame.LegacyRenderer`.

Le guide pas-a-pas est documente dans `Docs/custom-render-backend-integration.md`.

## Seams utiles pour un futur backend alternatif

Les seams stabilisees par ce chantier sont les suivantes:

- `IUIDesktopRuntime` pour le runtime consomme par `MGDesktop` ;
- `IUIDrawContext` et `IUIDrawTransaction` pour les capacites de draw exposees au coeur UI ;
- `IUIRenderContext` pour les changements temporaires de settings, clips, transforms et render targets ;
- `IUISurface` et `IUIRenderTarget` pour la possession des buffers et surfaces offscreen ;
- `IUIImageResource` pour les images UI ;
- `ITextEngine` pour la mesure/layout et le draw du texte ;
- `IRenderHost` et `IRawInputSource` uniquement pour le backend MonoGame, pas comme prerequis d'un backend custom.

Un backend non-MonoGame devrait se brancher derriere ces contrats, pas reouvrir `MGUI.Core`.

## Preuve dans le repo

Le repo contient maintenant une preuve executable qu'un moteur hote peut posseder le rendu sans passer par le runtime MonoGame historique:

- `MGUI.Tests/Integration/EngineOwnedRenderingProofTests.cs` ajoute un runtime de preuve qui implemente `IUIDesktopRuntime`, `IUIDrawTransaction`, `ITextEngine`, `IUISurface`, `IUIRenderTarget` et `IUIAssetProvider` ;
- le test direct valide qu'un backend hote peut dessiner des shapes, du texte et changer de buffer offscreen en restant entierement sur les contrats partages ;
- le test d'integration `MGDesktop` / `UIView` valide qu'une vraie `MGWindow` avec `MGBorder` et `MGTextBlock` passe bien par ce backend de preuve pour les shapes, le texte et la surface offscreen.

Cette preuve ne remplace pas un deuxieme backend de production, mais elle retire l'ambiguite architecturale: la separation est maintenant testee, pas seulement annoncee.

## Dette residuelle acceptee

Le chantier laisse volontairement quelques ponts de compatibilite bornes:

- `MGResources` et `MGTextureData` gardent des overloads `DrawTransaction` pour compatibilite descendante ;
- `MGImage`, `MGTextureData` et `MGTexturedBorderBrush` gardent des surfaces `Texture2D` limitees a des constructeurs ou proprietes legacy ;
- les types de valeur MonoGame (`Color`, `Rectangle`, `Point`, `Vector2`, `Matrix`) restent toleres dans le coeur UI ;
- `MGUI.Core` reference encore `MGUI.MonoGame.Integration` tant que ces ponts publics existent.

Ces exceptions sont epinglees par les tests d'architecture et ne doivent pas s'etendre silencieusement.

## Migration pour les consommateurs

Avant:

- le wiring typique faisait directement `new MainRenderer(...)` puis `new MGDesktop((IUIDesktopRuntime)renderer)` ou dependait implicitement du backend via `MGUI.Core` ;
- les projets de demo pouvaient s'appuyer sur la reference transitive du backend via `MGUI.Core`.

Maintenant:

- ajouter une reference explicite a `MGUI.MonoGame.LegacyRenderer` dans l'application ;
- utiliser le namespace `MGUI.Backend.MonoGame` pour le bootstrap ;
- preferer `MonoGameBackendBootstrap.Create(...)` pour creer le couple `Host + MainRenderer` ;
- construire `MGDesktop` via `new MGDesktop((IUIDesktopRuntime)renderer)` dans le code consommateur ;
- remplacer les usages nominaux de `DrawTransaction` par `IUIDrawTransaction` / `IUIRenderContext` / `IUIDrawContext` ;
- remplacer les surfaces et buffers MonoGame du chemin nominal par `IUISurface`, `IUIRenderTarget` et `IUIImageResource` ;
- traiter `MainRenderer` comme un backend de reference, pas comme le contrat implicite de la pile UI.

## Hors perimetre

Ce document ne promet pas un backend multi-moteur de production pret a l'emploi.

Le chantier livre une frontiere exploitable pour un backend alternatif et une preuve runnable dans les tests, mais pas encore une integration Unity, Godot, Stride ou autre packagée comme backend officiel. Toute integration de production dans un autre moteur resterait un chantier distinct.