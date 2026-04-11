# Rendering Backend Architecture

## Objectif

Ce document decrit l'etat final du chantier de decouplage du rendu.

Le point clef est le suivant:

- `MGUI.Core` porte la logique UI, pas le backend de draw concret ;
- `MGUI.MonoGame` porte l'implementation MonoGame concrete et le point d'entree de composition backend ;
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
- contrats texte `ITextMeasurementEngine`, `IMonoGameTextRenderer` et `ITextEngine` ;
- helpers transverses, XAML, theming partage et logique commune.

`MGUI.Shared` n'est plus le proprietaire de compilation des implementations MonoGame concretes.

### `MGUI.Core`

Responsabilites:

- controles, layout, theming, styles, XAML runtime et logique UI ;
- consommation des contrats de runtime, de texte et de draw ;
- ressources UI haut niveau comme `MGTextureData`, `MGResources`, brushes et shapes.

Le coeur UI doit preferer les contrats partages et eviter de dependere du backend concret, sauf ponts legacy explicitement assumes.

### `MGUI.MonoGame`

Responsabilites:

- `MainRenderer`, `DrawTransaction`, hosts MonoGame, surfaces et pools GPU ;
- chargeurs d'assets et wrappers d'images MonoGame ;
- implementation texte SpriteFont concrete ;
- point d'entree de composition backend `MGUI.Backend.MonoGame.MonoGameBackendBootstrap`.

Ce projet est la dependance concrete que les apps MonoGame doivent referencer explicitement.

### `MGUI.FontStashSharp`

Responsabilites:

- moteur texte alternatif au-dessus des contrats partages et du backend MonoGame ;
- aucune responsabilite de bootstrap applicatif.

## Flux d'integration recommande

1. L'application MonoGame reference `MGUI.Core` et `MGUI.MonoGame` directement.
2. Elle choisit un host MonoGame concret:
   - `GameRenderHost<TObservableGame>` si le `Game` publie deja `PreviewUpdate` et `EndUpdate` ;
   - `DelegateRenderHost` si la boucle d'update reste pilotee manuellement.
3. Elle cree le renderer concret via `MGUI.Backend.MonoGame.MonoGameBackendBootstrap.Create(...)`.
4. Elle construit `MGDesktop` depuis `IUIDesktopRuntime`, pas depuis un constructeur concret par defaut.
5. Elle charge ses ressources et utilise ensuite `MGDesktop.Update()` / `MGDesktop.Draw()` comme avant.

## Seams utiles pour un futur backend alternatif

Les seams stabilisees par ce chantier sont les suivantes:

- `IUIDesktopRuntime` pour le runtime consomme par `MGDesktop` ;
- `IUIDrawContext` et `IUIDrawTransaction` pour les capacites de draw exposees au coeur UI ;
- `IUIImageResource` pour les images UI ;
- `ITextMeasurementEngine` pour la mesure/layout du texte ;
- `IRenderHost`, `IRawInputSource` et `IUISurface` pour l'integration avec la boucle applicative et la surface logique.

Un backend non-MonoGame devrait se brancher derriere ces contrats, pas reouvrir `MGUI.Core`.

## Dette residuelle acceptee

Le chantier laisse volontairement quelques ponts de compatibilite bornes:

- `MGDesktop` garde `Renderer` et le constructeur `MGDesktop(MainRenderer)` pour les acces legacy ;
- `MGResources` et `MGTextureData` gardent des overloads `DrawTransaction` pour compatibilite descendante ;
- `MGImage`, `MGTextureData` et `MGTexturedBorderBrush` gardent des surfaces `Texture2D` limitees a des constructeurs ou proprietes legacy ;
- les types de valeur MonoGame (`Color`, `Rectangle`, `Point`, `Vector2`, `Matrix`) restent toleres dans le coeur UI ;
- `MGUI.Core` reference encore `MGUI.MonoGame` tant que ces ponts publics existent.

Ces exceptions sont epinglees par les tests d'architecture et ne doivent pas s'etendre silencieusement.

## Migration pour les consommateurs

Avant:

- le wiring typique faisait directement `new MainRenderer(...)` puis `new MGDesktop(renderer)` ;
- les projets de demo pouvaient s'appuyer sur la reference transitive du backend via `MGUI.Core`.

Maintenant:

- ajouter une reference explicite a `MGUI.MonoGame` dans l'application ;
- utiliser le namespace `MGUI.Backend.MonoGame` pour le bootstrap ;
- preferer `MonoGameBackendBootstrap.Create(...)` pour creer le couple `Host + MainRenderer` ;
- construire `MGDesktop` via `new MGDesktop((IUIDesktopRuntime)renderer)` dans le code consommateur ;
- reserver `Desktop.Renderer` aux usages legacy qui ont besoin du renderer concret.

## Hors perimetre

Ce document ne promet pas un backend multi-moteur pret a l'emploi.

Le chantier livre une frontiere exploitable pour un futur backend alternatif, mais pas un second backend concret. Toute integration Unity, Godot, Stride ou autre resterait un chantier distinct.