# MonoGame Host Integration Guide

## Objectif

Ce guide documente les 2 chemins d'integration MonoGame actuellement recommandes pour MGUI.

Le point important est le suivant:

- `MainRenderer` reste le runtime concret partage ;
- le choix porte sur la facon d'heberger ce runtime dans une boucle MonoGame ;
- ce chantier ne traite pas le support multi-moteur.

## Vue d'ensemble

MGUI se branche sur MonoGame a travers 4 seams complementaires:

- `IRenderHost` pour le `GraphicsDevice`, le viewport et les signaux d'update ;
- `IRawInputSource` pour les etats bruts souris/clavier ;
- `IUISurface` pour la surface logique de rendu UI ;
- `IUIDesktopRuntime` pour le sous-ensemble de runtime consomme par `MGDesktop`.

Dans le repo, `MainRenderer` implemente `IUIDesktopRuntime` et consomme un `IRenderHost`.

## Option 1: `GameRenderHost<TObservableGame>`

Utiliser cette option quand:

- votre classe `Game` expose deja `PreviewUpdate` et `EndUpdate` via `IObservableUpdate` ;
- vous voulez la voie la plus simple et la plus historique ;
- du code sample ou applicatif a encore besoin d'un acces direct au `Game` concret, par exemple via `Desktop.Renderer.Host as GameRenderHost<TGame>`.

Exemple minimal:

```csharp
private MainRenderer _renderer;
private MGDesktop _desktop;

protected override void Initialize()
{
    _renderer = new MainRenderer(new GameRenderHost<MyGame>(this), new MonoGameRawInputSource());
    _desktop = new MGDesktop(_renderer);
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
private DelegateRenderHost _mguiHost;
private MainRenderer _renderer;
private MGDesktop _desktop;

protected override void Initialize()
{
    _mguiHost = new DelegateRenderHost(
        GraphicsDevice,
        () => new Rectangle(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height),
        Services);

    _renderer = new MainRenderer(_mguiHost, new MonoGameRawInputSource());
    _desktop = new MGDesktop(_renderer);

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

## Ce qui reste volontairement concret

Ce chantier n'a pas tente de generaliser les couches suivantes:

- `MainRenderer` ;
- `DrawTransaction` ;
- `IUIRenderContext` ;
- les primitives MonoGame comme `GraphicsDevice`, `SpriteBatch`, `Texture2D` ou `PrimitiveBatch`.

`MGDesktop` depend maintenant d'un contrat `IUIDesktopRuntime`, mais la voie legacy `Renderer` reste exposee pour compatibilite. Certaines integrations plus anciennes peuvent donc encore acceder au `MainRenderer` concret quand elles en ont besoin.

## Hors perimetre

Ce guide ne decrit pas un backend de rendu generique par moteur.

Si un support Unity, Godot, Stride ou autre devait etre ajoute un jour, il faudrait ouvrir un chantier plus large que la simple flexibilite de host MonoGame:

- abstraction du runtime de draw ;
- separation des types MonoGame du core UI ;
- reconsideration de `DrawTransaction`, `IUIRenderContext` et des primitives de rendu.

Ce n'est pas l'objectif du refactor documente ici.