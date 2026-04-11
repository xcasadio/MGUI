# MonoGame Host Integration Guide

## Objectif

Ce guide documente les 2 chemins d'integration MonoGame actuellement recommandes pour MGUI apres le decouplage du backend de rendu.

Le point important est le suivant:

- ajouter une reference directe au projet `MGUI.MonoGame` dans l'application MonoGame ;
- utiliser `MGUI.Backend.MonoGame.MonoGameBackendBootstrap` comme point d'entree du backend concret ;
- choisir ensuite la facon d'heberger ce backend via `GameRenderHost<TObservableGame>` ou `DelegateRenderHost` ;
- construire enfin `MGDesktop` depuis `IUIDesktopRuntime`.

MonoGame est maintenant le backend de reference du repo, pas le contrat implicite de toute la pile UI. Pour brancher un moteur proprietaire ou un backend non-MonoGame, voir aussi `Docs/custom-render-backend-integration.md`.

## Vue d'ensemble

MGUI se branche sur MonoGame a travers 4 seams complementaires:

- `IRenderHost` pour le `GraphicsDevice`, le viewport et les signaux d'update ;
- `IRawInputSource` pour les etats bruts souris/clavier ;
- `IUISurface` pour la surface logique de rendu UI ;
- `IUIDesktopRuntime` pour le sous-ensemble de runtime consomme par `MGDesktop`.

Dans le repo, `MainRenderer` implemente `IUIDesktopRuntime`, et `MGUI.Backend.MonoGame.MonoGameBackendBootstrap` est le point d'entree recommande pour le construire.

Si votre objectif est un backend custom, `IRenderHost` et `DelegateRenderHost` ne sont pas des prerequis generiques: ce sont des adapters MonoGame specifiques.

## Prerequis cote projet

Dans votre application MonoGame:

- referencer `MGUI.Core` et `MGUI.MonoGame` directement ;
- importer le namespace `MGUI.Backend.MonoGame` pour le bootstrap ;
- garder `MGUI.FontStashSharp` uniquement si vous utilisez ce moteur texte optionnel.

## Option 1: `GameRenderHost<TObservableGame>`

Utiliser cette option quand:

- votre classe `Game` expose deja `PreviewUpdate` et `EndUpdate` via `IObservableUpdate` ;
- vous voulez la voie la plus simple et la plus historique ;
- du code sample ou applicatif a encore besoin d'un acces direct au `Game` concret, par exemple via `Desktop.Renderer.Host as GameRenderHost<TGame>`.

Exemple minimal:

```csharp
using MGUI.Backend.MonoGame;

private MainRenderer _renderer;
private MGDesktop _desktop;

protected override void Initialize()
{
    MonoGameBackendSession<GameRenderHost<MyGame>> backend =
        MonoGameBackendBootstrap.Create(new GameRenderHost<MyGame>(this));

    _renderer = backend.Renderer;
    _desktop = new MGDesktop((IUIDesktopRuntime)_renderer);
    _desktop.LoadDefaultResources();

    base.Initialize();
}
```

Reference dans le repo:

- `MGUI.Samples/Game1.cs`

## Option 2: `DelegateRenderHost`

Utiliser cette option quand:

- vous ne voulez pas lier MGUI a une sous-classe `Game` particuliere ;
- vous preferez fournir explicitement le viewport et piloter les notifications d'update ;
- vous voulez integrer MGUI dans une boucle MonoGame deja structuree autrement.

Exemple minimal:

```csharp
using MGUI.Backend.MonoGame;

private DelegateRenderHost _mguiHost;
private MainRenderer _renderer;
private MGDesktop _desktop;

protected override void Initialize()
{
    _mguiHost = new DelegateRenderHost(
        GraphicsDevice,
        () => new Rectangle(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height),
        Services);

    MonoGameBackendSession<DelegateRenderHost> backend =
        MonoGameBackendBootstrap.Create(_mguiHost);

    _renderer = backend.Renderer;
    _desktop = new MGDesktop((IUIDesktopRuntime)_renderer);

    base.Initialize();
}

protected override void Update(GameTime gameTime)
{
    _mguiHost.NotifyPreviewUpdate(gameTime.TotalGameTime);

    _desktop.Update();

    base.Update(gameTime);

    _mguiHost.NotifyEndUpdate();
}
```

Reference dans le repo:

- `MGUI.MiniGame/MiniGame.cs`

## Choisir entre les 2

Choisir `GameRenderHost<TObservableGame>` si:

- votre `Game` est deja la source naturelle des signaux `PreviewUpdate` / `EndUpdate` ;
- vous voulez minimiser le wiring ;
- certains ecrans ou samples ont besoin de remonter jusqu'au `Game` concret.

Choisir `DelegateRenderHost` si:

- vous voulez un host plus leger et plus explicite ;
- vous preferez injecter le viewport via un delegate ;
- vous voulez garder la boucle d'update MonoGame comme source de verite et notifier MGUI explicitement.

## Migration depuis l'ancien wiring

Avant, le wiring recommande etait souvent:

```csharp
_renderer = new MainRenderer(new GameRenderHost<MyGame>(this), new MonoGameRawInputSource());
_desktop = new MGDesktop(_renderer);
```

Le wiring prefere maintenant est:

```csharp
MonoGameBackendSession<GameRenderHost<MyGame>> backend =
    MonoGameBackendBootstrap.Create(new GameRenderHost<MyGame>(this));

_renderer = backend.Renderer;
_desktop = new MGDesktop((IUIDesktopRuntime)_renderer);
```

L'ancien chemin reste supporte pour compatibilite descendante, mais il n'est plus le point d'entree recommande dans la documentation.

## Ce qui reste volontairement concret

Ce guide reste volontairement centre sur les types MonoGame suivants:

- `MainRenderer` ;
- `DrawTransaction` ;
- `GameRenderHost<TObservableGame>` ;
- `DelegateRenderHost` ;
- les primitives MonoGame comme `GraphicsDevice`, `SpriteBatch`, `Texture2D` ou `PrimitiveBatch`.

En revanche, les contrats backend-neutral consommes par `MGDesktop` sont maintenant stabilises et doivent etre preferes dans le code applicatif nominal:

- `IUIDesktopRuntime` ;
- `IUIDrawTransaction` / `IUIRenderContext` / `IUIDrawContext` ;
- `IUISurface`, `IUIRenderTarget` et `IUIImageResource` ;
- `ITextEngine`.

Si vous avez besoin du renderer concret MonoGame, traitez-le comme un backend de reference explicitement choisi par l'application, pas comme le contrat implicite du framework.

Pour la vue d'ensemble complete du split `Core / Contracts / MonoGame backend` et des dettes residuelles assumees, voir aussi `Docs/rendering-backend-architecture.md`.

## Hors perimetre

Ce guide reste specifique au backend `MGUI.MonoGame`.

Le chantier a maintenant une preuve runnable qu'un backend non-MonoGame est possible dans `MGUI.Tests/Integration/EngineOwnedRenderingProofTests.cs`, mais l'integration d'un moteur de production autre que MonoGame est documentee separerement dans `Docs/custom-render-backend-integration.md`.