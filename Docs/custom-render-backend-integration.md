# Custom Render Backend Integration Guide

## Objectif

Ce guide explique comment brancher un moteur hote qui possede lui-meme:

- le draw des shapes ;
- le draw du texte ;
- les buffers et surfaces offscreen.

Le point clef est simple: `MGDesktop` n'a plus besoin d'un `MainRenderer` MonoGame pour fonctionner. Il a besoin d'un runtime qui implemente les contrats partages.

## Ce que le moteur doit posseder

### 1. Runtime desktop-facing

Le point d'entree de `MGDesktop` est `IUIDesktopRuntime`.

Votre runtime doit fournir:

- `Input` pour l'etat souris/clavier/gamepad ;
- `DefaultFontFamily` pour les controles texte ;
- `Surface` pour la surface logique de draw ;
- `AssetProvider` pour charger des images opaques `IUIImageResource` ;
- `TextEngine` pour la resolution, la mesure et le draw du texte ;
- `UpdateArgs` pour l'etat brut de la frame courante ;
- `CreateDrawTransaction(...)` pour construire un `IUIDrawTransaction` ;
- `RegisterView(...)` pour enregistrer les `UIView` crees par `MGDesktop`.

### 2. Surface et buffers

Le moteur doit decider ou la UI est composee:

- backbuffer direct: `IUISurface.GetRenderTarget()` retourne `null` ;
- surface offscreen: `IUISurface.GetRenderTarget()` retourne un `IUIRenderTarget` opaque ;
- rendu multi-buffers: le backend gere ses propres `IUIRenderTarget` et expose les changements de cible via `SetRenderTargetTemporary(...)`.

`UIView.Draw(...)` s'appuie deja sur cette seam. Si la surface expose un render target, la UI sera dessinee dedans avant de rendre la main au moteur.

### 3. Shapes et images

Le moteur doit implementer `IUIDrawTransaction` / `IUIRenderContext` / `IUIDrawContext`.

Concretement, cela veut dire prendre en charge:

- `FillRectangle`, `StrokeRectangle`, `FillTriangle`, `FillCircle`, `StrokeLineSegment`, `FillPolygon`, etc. ;
- `DrawTextureTo(...)` et `DrawTextureAt(...)` sur `IUIImageResource` ;
- `SetDrawSettingsTemporary(...)`, `SetTransformTemporary(...)`, `PushClipTemporary(...)` et `SetRenderTargetTemporary(...)`.

Le coeur UI ne doit pas connaitre vos types GPU natifs. Il parle uniquement via ces contrats.

### 4. Texte

Le moteur doit fournir un `ITextEngine`.

En pratique:

- `ITextMeasurementEngine` couvre `ResolveFont`, `MeasureText`, `MeasureGlyph`, `GetLineHeight`, `GetSpaceWidth` ;
- `ITextDrawEngine.DrawText(...)` couvre le draw reel ;
- `ITextEngine` combine les deux surfaces.

Le texte n'est donc pas un detail interne du backend MonoGame. C'est une responsabilite explicite du moteur hote.

## Boucle d'integration minimale

Le schema minimal cote application est:

```csharp
MyRuntime runtime = new(...);
MGDesktop desktop = new(runtime);

void UpdateFrame(TimeSpan totalElapsed, TimeSpan frameElapsed, MouseState mouse, KeyboardState keyboard)
{
    runtime.AdvanceFrame(totalElapsed, frameElapsed, mouse, keyboard);
    desktop.Update();
}

void DrawFrame()
{
    using IUIDrawTransaction transaction = runtime.CreateDrawTransaction(DrawSettings.Default, false);
    desktop.View.Draw(transaction, 1.0f);
}
```

Le helper `AdvanceFrame(...)` est a vous de le definir. L'interface n'impose pas cette methode, mais votre runtime doit bien mettre a jour `UpdateArgs` et `InputTracker` avant `desktop.Update()`.

## Squelette minimal des types a implementer

```csharp
public sealed class MyRuntime : IUIDesktopRuntime
{
    public InputTracker Input { get; } = new();
    public string DefaultFontFamily { get; } = "MyDefaultFont";
    public IUISurface Surface { get; }
    public IUIAssetProvider AssetProvider { get; }
    public ITextMeasurementEngine TextEngine { get; set; }
    public UpdateBaseArgs UpdateArgs { get; private set; }

    public event EventHandler<EventArgs<ITextMeasurementEngine>> TextEngineChanged;

    public IUIDrawTransaction CreateDrawTransaction(DrawSettings settings, bool deferBegin)
        => new MyDrawTransaction(this, settings);

    public void RegisterView(IUIView view)
    {
        // optionnel: suivi des vues attachees
    }
}

public sealed class MySurface : IUISurface
{
    public Rectangle GetBounds() => ...;
    public IUIRenderTarget GetRenderTarget() => ...; // null pour backbuffer, sinon buffer opaque
}

public sealed class MyDrawTransaction : IUIDrawTransaction
{
    public DrawSettings CurrentSettings { get; private set; }
    public IUIDesktopRuntime Renderer { get; }
    public Rectangle? CurrentClipBounds { get; }

    public void FillRectangle(Vector2 origin, RectangleF destination, Color color) { ... }
    public void DrawTextViaEngine(ResolvedFont font, string text, Vector2 position, Color color, Vector2 origin, float scale, float rotation = 0f, float depth = 0f, UIDrawFlip flip = UIDrawFlip.None) { ... }
    public IDisposable SetRenderTargetTemporary(IUIRenderTarget target, Color? clearColor) { ... }
    public IDisposable SetDrawSettingsTemporary(DrawSettings settings) { ... }
    public IDisposable SetTransformTemporary(Matrix transform) { ... }
    public ClipScope PushClipTemporary(ClipDefinition definition) { ... }
    public void Dispose() { ... }
}

public sealed class MyTextEngine : ITextEngine
{
    public ResolvedFont ResolveFont(FontSpec spec) { ... }
    public Vector2 MeasureText(ResolvedFont font, string text) { ... }
    public GlyphMetrics MeasureGlyph(ResolvedFont font, char c) { ... }
    public float GetLineHeight(ResolvedFont font) { ... }
    public float GetSpaceWidth(ResolvedFont font) { ... }
    public void DrawText(IUIDrawContext drawContext, ResolvedFont font, string text, Vector2 position, Color color, Vector2 origin, float scale, float rotation = 0f, float depth = 0f, UIDrawFlip flip = UIDrawFlip.None) { ... }
    public void InvalidateCache() { ... }
}
```

## Ce que MonoGame reste responsable de faire

Le backend `MGUI.MonoGame` reste le backend de reference du repo.

Il fournit:

- `MainRenderer` ;
- `DrawTransaction` ;
- `GameRenderHost<TObservableGame>` et `DelegateRenderHost` ;
- les wrappers MonoGame pour images, render targets et texte SpriteFont.

Mais ces types ne sont plus le contrat implicite du coeur UI. Ils sont une implementation concrete parmi les backend possibles.

## Migration des consommateurs existants

Si votre code utilisait encore directement les types MonoGame du chemin nominal:

1. remplacez les parametres `MainRenderer` par `IUIDesktopRuntime` partout ou vous construisez `MGDesktop` ;
2. remplacez les parametres `DrawTransaction` par `IUIDrawTransaction`, `IUIRenderContext` ou `IUIDrawContext` selon le besoin reel ;
3. remplacez les images et buffers `Texture2D` / `RenderTarget2D` du chemin nominal par `IUIImageResource` / `IUIRenderTarget` ;
4. laissez les conversions vers des types MonoGame uniquement dans `MGUI.MonoGame` ou dans votre adaptateur backend concret ;
5. si vous restez sur MonoGame, preferez `MonoGameBackendBootstrap.Create(...)` plutot que d'etendre `MainRenderer` comme s'il etait le contrat principal.

## Preuve disponible dans le repo

Le repo contient deja une preuve runnable du montage attendu dans `MGUI.Tests/Integration/EngineOwnedRenderingProofTests.cs`.

Cette preuve montre:

- un runtime hote qui implemente les contrats partages ;
- un moteur texte backend-owned ;
- un render target opaque possede par la surface ;
- un vrai chemin `MGDesktop` / `UIView` qui dessine shapes, texte et buffer sans passer par `MainRenderer`.

Ce n'est pas un backend de production, mais c'est le modele minimal a reproduire pour une integration moteur proprietaire.

## Hors perimetre

Ce guide ne definit pas votre batching, votre synchronisation GPU, votre politique de cache, vos shaders ou votre presentation ecran.

Il fixe uniquement la frontiere d'integration attendue par MGUI pour qu'un moteur hote possede le rendu sans reintroduire des dependances MonoGame dans `MGUI.Core` ou `MGUI.Shared`.