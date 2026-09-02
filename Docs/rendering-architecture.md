# Architecture du rendu

## Objectif

Decrire l'etat actuel du sous-systeme de rendu de MGUI : separation des assemblies, contrats runtime et draw, hotes MonoGame, boucle d'update et pipeline de clipping composable.

## Portee

- Split des projets `MGUI.Rendering.Abstractions` / `MGUI.Shared` / `MGUI.MonoGame.Integration` / `MGUI.MonoGame.LegacyRenderer`.
- Contrats `IUIDesktopRuntime`, `IUIDrawTransaction`, `IUISurface`, `ITextEngine`, hotes `IRenderHost` et bootstrap backend.
- Pipeline de clipping : abstractions, resolution de strategie, backends scissor/stencil/mask, conventions de coordonnees, politique overlay.

Hors portee : le paint des shapes et brushes (`Docs/drawing-architecture.md`) et le layout du texte (`Docs/text-architecture.md`). Guides pratiques : `Docs/monogame-host-integration-guide.md` (application MonoGame) et `Docs/custom-render-backend-integration.md` (moteur proprietaire).

## Vue d'ensemble

Principe directeur : `MGUI.Core` porte la logique UI et consomme uniquement des contrats partages ; le renderer MonoGame concret est un backend de reference choisi explicitement par l'application, pas le contrat implicite de la pile.

Chaine de dependances :

```
MGUI.Core ──> MGUI.Shared ──> MGUI.Rendering.Abstractions
   │                ▲
   └──> MGUI.MonoGame.Integration (couche de support MonoGame partagee)
                    ▲
        MGUI.MonoGame.LegacyRenderer (backend concret, sources liees depuis Integration)
                    ▲
        Application MonoGame (reference MGUI.Core + MGUI.MonoGame.LegacyRenderer)
```

`MGUI.FontStashSharp` est un moteur texte optionnel construit au-dessus de cette separation.

## Separation des assemblies

### `MGUI.Rendering.Abstractions`

Contrats backend-neutral sans aucune reference MonoGame : types texte partages (`FontSpec`, `GlyphMetrics`, `CustomFontStyles`) et handle d'image opaque `IUIImageResource` (`MGUI.Rendering.Abstractions/Assets/IUIImageResource.cs`).

### `MGUI.Shared`

Contrats et helpers partages entre le coeur UI et les backends :

- runtime desktop-facing `IUIDesktopRuntime` (`MGUI.Shared/Rendering/IUIDesktopRuntime.cs`) ;
- surfaces de draw `IUIDrawContext` / `IUIRenderContext` / `IUIDrawTransaction` (`IUIDrawTransaction` = `IUIRenderContext` + `IDisposable`) ;
- contrats texte `ITextMeasurementEngine`, `ITextDrawEngine`, `ITextEngine` (composite des deux) dans `MGUI.Shared/Text/Engines/` ;
- surface logique `IUISurface` et buffer opaque `IUIRenderTarget` (qui etend `IUIImageResource`) ;
- abstractions de clipping dans `MGUI.Shared/Rendering/Clipping/` (voir plus bas) ;
- input partage : `IRawInputSource`, `InputTracker`, `KeyboardTracker` (avec le puits `IKeyboardTextInputSink.QueueTextInput`).

`MGUI.Shared` n'est pas le proprietaire de compilation des implementations MonoGame concretes.

### `MGUI.Core`

Controles, layout, theming, styles, runtime XAML. Consomme les contrats de runtime, de texte et de draw. Pour la mesure du texte, le coeur consomme `ITextMeasurementEngine` (la propriete `TextEngine` du runtime) ; le draw texte passe par `DrawTextViaEngine` sur le contexte de draw.

`MGUI.Core` reference encore `MGUI.MonoGame.Integration` (pont assume, voir Limites connues). `MGDesktop` n'expose ni `Renderer` ni `FontManager` ; son seul constructeur est `MGDesktop(IUIDesktopRuntime)` (`MGUI.Core/UI/MGDesktop.cs`).

### `MGUI.MonoGame.Integration`

Couche de support MonoGame partagee entre le coeur UI, le texte optionnel et des backends possedes par le moteur. Compile notamment :

- `Rendering/RenderHost.cs` : `IRenderViewport`, `IObservableUpdate` (`PreviewUpdate` / `EndUpdate`), `IRenderHost` (= viewport + update + `IServiceProvider`) et `GameRenderHost<TObservableGame>` ;
- `Rendering/TextInputHost.cs` : interface opt-in `ITextInputHost` pour le cablage de la saisie texte native ;
- `Rendering/IMonoGameBackendContracts.cs` : `IMonoGameDesktopBackend` (= `IUIDesktopRuntime` + `Host` + `FontManager`) et `IMonoGameDrawContext` (= `IUIDrawTransaction` cote MonoGame) ;
- `Rendering/DrawContext.cs`, `Rendering/MonoGameRenderInterop.cs`, `Assets/MonoGameImageResource.cs`, `Text/FontManager.cs`.

Ce projet n'est pas le backend applicatif recommande : il exclut de sa propre compilation (`Compile Remove` dans le csproj) `MainRenderer`, `DrawTransaction`, `DelegateRenderHost`, `ClipManager`, `RenderTargetPool`, `View`, `BackBufferSurface`, `MonoGameRawInputSource`, `MonoGameBackendBootstrap` et les helpers GPU legacy.

### `MGUI.MonoGame.LegacyRenderer`

Le backend concret que les applications MonoGame referencent explicitement. Ce projet n'a aucune source propre : il compile par liens (`Compile Include` avec `Link`) les fichiers physiquement stockes sous `MGUI.MonoGame.Integration/` mais exclus de la compilation d'Integration :

- `MainRenderer` (implemente `IMonoGameDesktopBackend`), `DrawTransaction` (implemente `IMonoGameDrawContext`) ;
- `DelegateRenderHost`, `BackBufferSurface`, `View` (`UIView` cote backend), `RenderTargetPool` ;
- `Clipping/ClipManager` (backends scissor/stencil/mask) ;
- `MonoGameRawInputSource`, helpers (`ContentUtils`, `RenderUtils`, `TextureUtils`) ;
- point d'entree de composition `MonoGameBackendBootstrap` (namespace `MGUI.Backend.MonoGame`).

Note namespaces : pour raisons historiques, `MainRenderer`, `GameRenderHost<T>` et `DelegateRenderHost` vivent dans le namespace `MGUI.Shared.Rendering` bien que compiles dans Integration/LegacyRenderer ; le bootstrap et les contrats backend MonoGame vivent dans `MGUI.Backend.MonoGame`.

### `MGUI.FontStashSharp`

Moteur texte alternatif au-dessus des contrats partages et de la couche MonoGame. Aucune responsabilite de bootstrap.

### Dossier mort `MGUI.MonoGame/`

Le dossier `MGUI.MonoGame/` a la racine ne contient que `bin/` et `obj/` et n'est pas reference par `MGUI.sln`. C'est un vestige de l'ancien nom du projet, candidat a la suppression.

## Contrats runtime et boucle d'update

### `IUIDesktopRuntime`

Contrat consomme par `MGDesktop` :

```csharp
public interface IUIDesktopRuntime
{
    InputTracker Input { get; }
    string DefaultFontFamily { get; }
    IUISurface Surface { get; }
    IUIAssetProvider AssetProvider { get; }
    ITextMeasurementEngine TextEngine { get; set; }
    event EventHandler<EventArgs<ITextMeasurementEngine>> TextEngineChanged;
    event EventHandler<EventArgs> EndUpdate;
    UpdateBaseArgs UpdateArgs { get; }
    IUIDrawTransaction CreateDrawTransaction(DrawSettings Settings, bool DeferBegin);
    void RegisterView(IUIView View);
}
```

Ce contrat n'expose volontairement aucun `GraphicsDevice`, `SpriteBatch`, `PrimitiveBatch` ni `IRenderHost`, pour ne pas deriver vers un pseudo-contrat backend generique.

### `IMonoGameDesktopBackend`

Quand une application MonoGame a besoin du host concret ou du `FontManager`, le chemin sanctionne est le downcast du runtime :

```csharp
if (Desktop.Runtime is IMonoGameDesktopBackend backend && backend.Host is GameRenderHost<Game1> host) { ... }
```

C'est le pattern utilise par les samples (`MGUI.Samples/Dialogs/SampleHUD.xaml.cs`, `MGUI.Samples/Features/PerformanceTest.xaml.cs`).

### Hotes MonoGame

- `GameRenderHost<TObservableGame>` (`TObservableGame : Game, IObservableUpdate`) : relaie `PreviewUpdate` / `EndUpdate` du `Game`, implemente `ITextInputHost` (cablage automatique de la saisie texte native par le bootstrap) ; `Dispose()` desabonne tout.
- `DelegateRenderHost` : `GraphicsDevice` explicite + delegate de viewport + `IServiceProvider` optionnel ; l'appelant pilote `NotifyPreviewUpdate(TimeSpan)` / `NotifyEndUpdate()` autour de `desktop.Update()`. N'implemente pas `ITextInputHost` (cablage saisie manuel obligatoire, voir le guide host).

### Bootstrap et boucle

`MGUI.Backend.MonoGame.MonoGameBackendBootstrap.Create(host, rawInputSource = null)` construit un `MainRenderer` sur le host, attache le puits de saisie texte si `host is ITextInputHost`, et retourne `MonoGameBackendSession<THost> { Host, Renderer }`.

Boucle par frame :

1. le host emet `PreviewUpdate` ; `MainRenderer` reconstruit `UpdateArgs` (temps total, delta, etats bruts souris/clavier via `IRawInputSource`) et appelle `Input.Update(...)` ;
2. l'application appelle `desktop.Update()` ;
3. le host emet `EndUpdate` ; `MainRenderer` releve l'evenement `IUIDesktopRuntime.EndUpdate` consomme par `MGDesktop` pour ses finalisations de frame ;
4. draw : `desktop.Draw()` cree sa propre transaction via `CreateDrawTransaction(...)`, ou l'application appelle `MGDesktop.Draw(IUIDrawTransaction, float)` / `UIView.Draw(IUIDrawTransaction, float)` avec une transaction qu'elle possede.

### Surfaces

`IUISurface.GetBounds()` donne la zone logique ; `GetRenderTarget()` retourne `null` pour un rendu backbuffer direct ou un `IUIRenderTarget` opaque pour une surface offscreen. `UIView.Draw` route le rendu a travers cette seam ; les changements de cible passent par `IUIRenderContext.SetRenderTargetTemporary(...)`.

## Pipeline de clipping composable

Le clipping est un contrat logique, pas un effet de bord scissor code en dur. Quatre couches :

1. les elements UI declarent une intention via `ClipDefinition` ;
2. `ClipStrategyResolver` (`MGUI.Shared/Rendering/Clipping/ClipStrategyResolver.cs`) mappe l'intention sur un backend effectif selon `ClipBackendCapabilities` ;
3. `ClipManager` (`MGUI.MonoGame.Integration/Rendering/Clipping/ClipManager.cs`, compile dans LegacyRenderer) pousse et depile l'etat GPU ;
4. `DrawTransaction` garde coherents `SpriteBatch`, `PrimitiveBatch`, render targets et transforms a travers les transitions.

### Modele

Namespace `MGUI.Shared.Rendering.Clipping` (`ClipAbstractions.cs`) :

- `ClipKind` : `None`, `Rectangle`, `RoundedRectangle`, `ArbitraryGeometry` ;
- `ClipDefinition` : requete logique emise par la couche UI ; factories `ClipDefinition.None(...)`, `.Rectangle(...)`, `.RoundedRectangle(...)`, `.ArbitraryGeometry(...)` ; options `IntersectWithCurrentClip`, `ClipStrategyPreference`, `AllowRectangleFallback`, `DebugName` ;
- `ClipShape` : bounds, corner radius optionnel, geometrie optionnelle ;
- `ClipResolveResult` : clip demande, clip effectif, strategie choisie, info de fallback, profondeur stencil eventuelle ;
- `ClipScope` : lifetime disposable qui restaure l'etat precedent.

Les controles ne choisissent jamais scissor, stencil ou mask : ils retournent des definitions logiques.

### API sur `IUIRenderContext`

- `PushClipTemporary(ClipDefinition)` : chemin general, retourne un `ClipScope` ;
- `PushRectangleClip(Rectangle?, bool intersect)` : chemin prefere pour les clips rectangulaires ;
- `ResolveClip(ClipDefinition)` : resolution sans push (diagnostic/inspection) ;
- `SetClipTargetTemporary(Rectangle?, bool intersect)` : shim de compatibilite rectangle-only, conserve pour la migration — le nouveau code doit preferer les deux premiers.

### Resolution de strategie

Mapping central de `ClipStrategyResolver` :

- `Rectangle` -> scissor ;
- `RoundedRectangle` -> stencil ;
- `ArbitraryGeometry` -> stencil ;
- `RoundedRectangle` / `ArbitraryGeometry` avec `AllowRectangleFallback` -> scissor (fallback grossier explicite) ;
- `RoundedRectangle` / `ArbitraryGeometry` quand le stencil n'est pas supporte -> mask.

### Backend scissor (fast path)

Les clips rectangulaires restent sur le scissor avec la semantique historique d'intersection des rectangles imbriques. C'est le cas commun et il reste bon marche.

### Backend stencil

Protocole increment-on-push / decrement-on-pop (`ClipManager.cs`) :

- premier push stencil sur une surface : `ClearStencil(0)`, puis ecriture profondeur 0 -> 1 ;
- clip enfant sous un parent de profondeur N : les pixels correspondants passent de N a N+1 ; le contenu du scope rend avec `DepthStencilType.StencilReadEqual` a la reference N+1 ;
- a la disposal du scope, la meme geometrie rend avec `DepthStencilType.StencilRestoreDecrement` a la reference N+1, ramenant les pixels a N.

Ce choix n'exige pas de valeurs compare/write separees, supporte l'imbrication avec une simple pile de profondeur, restaure la semantique parent a la disposal, et garde la possession du clip dans la couche rendering.

Profondeur maximale : 255 (buffer stencil 8 bits, `MaxStencilDepth` dans `ClipManager.cs`). Un depassement leve `InvalidOperationException` — prefere a un fallback silencieux car une imbrication excessive est un probleme d'architecture, pas un detail recuperable au draw.

Regles :

- ne jamais utiliser le stencil pour simplement peindre une forme arrondie ; uniquement pour contraindre les draws suivants par une region non rectangulaire ;
- les brushes ne sont pas stencil-aware ;
- `MGBorder` reutilise `MGBoxGeometry` pour decrire le clip de contenu arrondi (geometrie partagee avec le paint, responsabilites distinctes).

### Backend mask (fallback render target)

Pour les cas non rectangulaires quand le stencil n'est pas disponible : `ClipManager.PushMask` loue un render target temporaire poole par taille/format (`RentTemporaryRenderTarget` -> `RenderTargetLease`), alloue depuis les bounds transformes en espace render-target, dessine le contenu local dedans sous la transform active, puis composite le resultat. Cout et batching tres differents du stencil : ce chemin n'est volontairement pas le defaut.

### Conventions de coordonnees

Double convention simultanee :

- les bounds de clip rectangulaires sont exprimes en espace render-target (le scissor recoit des bounds deja transformes) ;
- les vertex de geometrie de clip restent dans l'espace de draw LOCAL de l'element ; la transform active de la `DrawTransaction` les mappe vers l'espace render-target — les clips stencil suivent donc automatiquement `RenderScale` et les transforms parents ;
- les clips mask allouent leur render target temporaire depuis les bounds transformes, puis reutilisent la transform active pour y dessiner le contenu local.

`MGElement.Draw(...)` applique `RenderScale` AVANT de demander ses definitions de clip a l'element : les elements rectangle-only utilisent le fast path des `TargetBounds` transformes, les geometries arrondies decrivent leurs vertex en espace local, et aucun controle n'a besoin de savoir quel backend resoudra son clip.

### Integration `MGElement` et politique overlay

Hooks sur `MGElement` (`MGUI.Core/UI/MGElement.cs`) :

- `GetSelfClipDefinition(...)` (internal virtual) : clip applique autour du dessin decoratif/self ;
- `GetContentsClipDefinition(...)` (internal virtual) : clip applique uniquement autour du contenu heberge ;
- `CreateBorderBackedContentsClipDefinition(...)` (protected) : helper pour les clips de contenu arrondis adosses au border.

Trois categories de visuels :

- visuels self (background, border, chrome, overlay brush par defaut) : dessines sous le clip SELF ;
- visuels de contenu (enfants heberges) : dessines sous le clip CONTENTS, pousse uniquement autour des enfants et dispose avant `DrawOverlayBrush(...)` — les overlays hover/pressed issus des background brushes suivent donc le clip self, jamais le clip contents ;
- overlays globaux (diagnostics, decorations de docking) : ignorent le clipping local — `MGDockPreviewOverlay` et `MGDockDropIndicators` (`MGUI.Core/UI/Docking/Controls/`) overrident `GetSelfClipDefinition` / `GetContentsClipDefinition` pour s'exclure (et `MGDockDropIndicators` met `ClipToBounds = false`) afin de pouvoir rendre hors des bounds locaux.

Cette politique garde le chrome des elements stable et empeche les clips de contenu de couper les visuels de focus ou de docking.

Adopteurs de `GetContentsClipDefinition` : `MGBorder` (contenu arrondi quand `ClipToBounds` et shape interne arrondie), `MGScrollViewer` (viewport rectangulaire via le contrat partage), `MGTextBox`.

### Migration et extension

Migrer un controle existant :

1. garder la logique de draw inchangee ;
2. deplacer l'intention de clip dans `GetSelfClipDefinition(...)` ou `GetContentsClipDefinition(...)` ;
3. retourner `ClipDefinition.Rectangle(...)` / `.RoundedRectangle(...)` / `.ArbitraryGeometry(...)` ;
4. supprimer tout scope de clip local qui n'existait que pour emuler la meme intention dans `Draw(...)`.

Ajouter un nouveau type de clip :

1. etendre `ClipKind` et `ClipDefinition` ;
2. apprendre a `ClipStrategyResolver` a choisir le backend effectif ;
3. implementer le push/pop dans `ClipManager` ;
4. laisser les controles ignorants du backend.

### Diagnostics

Chaque transaction de draw expose des compteurs (`MGUI.Shared/Rendering/Clipping/ClipDiagnostics.cs`) : nombre de clips scissor/stencil/mask, profondeur stencil maximale atteinte, nombre de locations et de reutilisations de render targets temporaires. `DrawTransaction.GetClipDiagnosticsDebugText()` fournit la chaine de debug.

### Choix de strategie

- rectangle quand des bounds axis-aligned suffisent ;
- rounded rectangle quand le contenu visible doit respecter des coins arrondis ;
- arbitrary geometry uniquement quand ni l'un ni l'autre ne decrit la region ;
- fallback rectangle uniquement quand l'appelant accepte un clip plus grossier.

### Invariants de design

- `VisualShape != ContentClipShape` par design : un chrome arrondi n'implique jamais automatiquement un clip de contenu arrondi. Restent volontairement rectangulaires (ou sans clip) : viewports de scroll, previews et drop indicators de docking, grips/handles de resize, adorners externes (ombre, glow), separateurs de tabs et de docking, overlays couvrant un rectangle logique. Ce sont des exceptions documentees, pas de la dette de migration.
- Le hit testing est decouple du modele de clip : il reste rectangle-based via `ActualLayoutBounds` meme pour les controles au chrome arrondi (aucun type `HitTestShape` n'existe). C'est un non-but deliberate, pas un oubli.
- Le clip de viewport rectangulaire de `MGScrollViewer` est architecturalement correct : un clip de contenu arrondi sur un host arrondi est un besoin distinct (optionnel, futur) du clipping de viewport.
- Architecture de paint des shapes et architecture de clip restent separees : le code shape decide comment dessiner le visuel, le code clip decide comment contraindre les draws suivants ; la geometrie arrondie partagee (`MGBoxGeometry`) peut etre reutilisee par les deux.

## Brancher un backend engine-owned

Un moteur proprietaire ne part pas de `MainRenderer` ni d'un host MonoGame : il implemente `IUIDesktopRuntime`, `IUISurface` (+ `IUIRenderTarget` opaque si offscreen), `IUIDrawTransaction` et `ITextEngine`, puis construit `MGDesktop` directement avec ce runtime. `IRenderHost` et `IRawInputSource` sont des adapters MonoGame, pas des prerequis.

Preuve executable dans le repo : `MGUI.Tests/Integration/EngineOwnedRenderingProofTests.cs` — un runtime non-MonoGame qui dessine shapes, texte et buffer offscreen a travers un vrai chemin `MGDesktop` / `UIView`, entierement sur les contrats partages.

Guide pas-a-pas : `Docs/custom-render-backend-integration.md`.

## Garde-fous : tests d'architecture

La frontiere est epinglee par des suites de tests dans `MGUI.Tests/Architecture/` qui ne doivent pas etre affaiblies : `RenderingBoundaryArchitectureTests`, `Phase4RenderingArchitectureTests`, `BackendProjectSplitTests`, `HostRuntimeContractTests`, `RenderContextTests`, `SurfaceAbstractionTests`, `AssetProviderTests`, `UIViewTests`, `ClipPipelineStrategyTests`, plus `MGUI.Tests/Integration/EngineOwnedRenderingProofTests`. Reintroduire des tokens `GraphicsDevice`, `SpriteBatch`, `PrimitiveBatch`, `Texture2D`, `RenderTarget2D`, `ContentManager` ou `MainRenderer` dans `MGUI.Shared` / `MGUI.Core` les fait echouer.

## Limites connues

- Les types de valeur MonoGame (`Color`, `Rectangle`, `Point`, `Vector2`, `Matrix`) restent volontairement toleres dans `MGUI.Core` et `MGUI.Shared` ; leur abstraction est explicitement hors perimetre.
- `MGUI.Core` reference encore `MGUI.MonoGame.Integration` (pont assume, epingle par les tests d'architecture).
- Aucun backend non-MonoGame de production n'est livre : la preuve de test retire l'ambiguite architecturale mais une integration Unity/Godot/Stride resterait un chantier distinct.
- Le shim `SetClipTargetTemporary` a encore des call sites legacy (`MGDesktop`, `MGContextMenu`, `MGRatingControl`) ; voir les taches.
- Profondeur stencil plafonnee a 255 ; le backend mask a un cout memoire/batching eleve et ne doit pas devenir un chemin chaud.
- Le dossier racine `MGUI.MonoGame/` est mort (bin/obj uniquement, hors solution).

## Reste a faire

Voir `Docs/Tasks/rendering-tasks.md`.
