# Host-resolved image sources and data binding pushes

## Scope

This is a short, practical companion to [ADR-0016](decisions/0016-host-resolved-image-sources-and-allocation-free-binding.md):
how `MGImage` names its source and how a host resolves that name, bindable canvas coordinates, and how a data
binding push reaches its target. It does not repeat the ADR's context or consequences.

Live sample: `MGUI.Samples/Features/BoundImages.xaml` / `.xaml.cs` (registered in `Compendium.xaml` as "Bound Images").

## `MGImage.SourceName` resolution order

An `MGImage` (`MGUI.Core/UI/MGImage.cs`) resolves its picture in this order, the first time `SourceName` or
`Source` is set and again whenever the resolved texture dictionary changes:

1. `Source` (an explicit `MGTextureData`), if set.
2. `MGResources.Textures`, walking the resource scope chain (window scope, then desktop scope) for a texture
   already registered by name, e.g. via `MGResources.AddTexture`.
3. If still unresolved and `SourceName` is not null: `MGResources.AssetProvider.TryCreateAnimatedImage`, at the
   *root* desktop scope for step 2's fallback, but at the image's own nearest resource scope for this step (see
   "Two known limits" below for the practical consequence).

Step 2's fallback, when no scope already has the name, asks the desktop-root `MGResources.AssetProvider.TryResolveImage`
once, caches the result at the root scope, and never asks again for a name it could not resolve. `SourceName`
subscribes to `MGResources.OnTextureAdded`/`OnTextureRemoved` once per image, not on every `SourceName` change.

```xml
<Image SourceName="sprite:hero" Width="64" Height="64" Stretch="Uniform" />
```

## Implementing a host provider

`IUIAssetProvider` (`MGUI.Shared/Assets/IUIAssetProvider.cs`) has two members with a "resolve/create nothing"
default implementation, so an existing implementer keeps compiling unchanged:

```csharp
public interface IUIAssetProvider
{
    IUIImageResource LoadImage(string assetName);
    bool TryLoadImage(string assetName, out IUIImageResource image);

    bool TryResolveImage(string name, out IUIImageResource image, out Rectangle? sourceRect) { ... }
    bool TryCreateAnimatedImage(string name, out IUIAnimatedImage animatedImage) { ... }
}
```

- **`TryResolveImage`**: turns a name unknown to `MGResources` into an image and an optional source rectangle
  within it (a sprite sheet region). Called once per unresolvable-or-resolvable name, at the desktop's root
  resource scope only (see `MGResources.TryGetTexture`/`TryResolveTextureFromProvider`).
- **`TryCreateAnimatedImage`**: creates a fresh, independent `IUIAnimatedImage` instance for a name the host
  recognizes as an animation, one instance per `MGImage`. Called at the image's own nearest resource scope, which
  need not be the root (a per-window scope with its own `AssetProvider` can serve animations without affecting
  `TryResolveImage` desktop-wide, since only the root scope consults it for static names).

A decorator that wraps the app's existing provider and only adds a few names, forwarding everything else
unchanged, is the usual shape (see `MGUI.Samples/Features/BoundImagesAssetProvider.cs`):

```csharp
internal sealed class MyAssetProvider : IUIAssetProvider
{
    private readonly IUIAssetProvider _inner;
    public MyAssetProvider(IUIAssetProvider inner) => _inner = inner;

    public IUIImageResource LoadImage(string assetName) => _inner.LoadImage(assetName);
    public bool TryLoadImage(string assetName, out IUIImageResource image) => _inner.TryLoadImage(assetName, out image);

    public bool TryResolveImage(string name, out IUIImageResource image, out Rectangle? sourceRect)
        => name == "sprite:hero" ? Resolve(out image, out sourceRect) : _inner.TryResolveImage(name, out image, out sourceRect);

    public bool TryCreateAnimatedImage(string name, out IUIAnimatedImage animatedImage)
        => _inner.TryCreateAnimatedImage(name, out animatedImage);
}
```

Since `MGDesktop`'s root `MGResources.AssetProvider` is fixed at construction from the runtime's own
`IUIDesktopRuntime.AssetProvider`, installing such a decorator desktop-wide means wrapping the runtime passed to
`new MGDesktop(...)`, forwarding every other member unchanged (see `BoundImagesRuntime` in the same sample file for
a full example, including why a partial wrapper would break other samples that pattern-match on the concrete
runtime type).

## Animated images

`IUIAnimatedImage` (`MGUI.Shared/Assets/IUIAnimatedImage.cs`) is a host-created, per-image handle:

- `Advance(TimeSpan elapsed)`: called from `MGImage.UpdateSelf` every frame the image is not collapsed, with
  `UA.BA.FrameElapsed`.
- `Restart(TimeSpan startOffset)`: discards accumulated time and restarts at an offset.
- `CurrentImage` / `CurrentSourceRect` / `CurrentDrawOffset`: the current frame's image, an optional source
  rectangle within it, and a per-frame pixel offset added to the draw position (for frames authored with
  different pivots).

`MGImage` exposes two ordinary bindable properties over this handle:

- `AnimationStartOffset` (`TimeSpan`, default `TimeSpan.Zero`): restarts the animation at the new offset when set.
- `IsAnimationPlaying` (`bool`, default `true`): setting it false restarts and holds the first frame at
  `AnimationStartOffset`; setting it true resumes from there.

```xml
<Image SourceName="anim:idle" Width="64" Height="64"
       AnimationStartOffset="{dataBinding:MGBinding Path=AnimationStartOffset}"
       IsAnimationPlaying="{dataBinding:MGBinding Path=IsAnimationPlaying}" />
```

## Bindable canvas coordinates

`MGElement.CanvasLeft` / `CanvasTop` / `CanvasRight` / `CanvasBottom` (all `int?`) are typed fields on
`MGElement` itself (`MGUI.Core/UI/MGElement.cs`), read and written through `MGCanvas.GetLeft`/`SetLeft`/etc.
(`MGUI.Core/UI/Containers/MGCanvas.cs`), and only meaningful while the element's `Parent` is an `MGCanvas`. `Left`
wins over `Right`, `Top` wins over `Bottom`. Because the XAML `CanvasLeft`/`CanvasTop`/`CanvasRight`/`CanvasBottom`
attributes share their name with the runtime property, a binding written on the attribute reaches the property
directly, with no extra `BindingPathMappings` entry:

```xml
<Canvas Width="420" Height="110">
    <Image SourceName="cursor" Width="24" Height="24"
           CanvasLeft="{dataBinding:MGBinding Path=SpriteLeft}" CanvasTop="{dataBinding:MGBinding Path=SpriteTop}" />
</Canvas>
```

Setting either property live invalidates the parent canvas's layout, so the element moves on the next update.

## The `dataBinding:` prefix

`MGBinding` is declared in `MGUI.Core.UI.DataBinding`, not `MGUI.Core.UI.XAML`: referencing it from a XAML document
needs its own prefix mapped to that namespace, or the parser throws.

```xml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        xmlns:dataBinding="clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core">
    <TextBlock Text="{dataBinding:MGBinding Path=DisplayName}" />
</Window>
```

The unprefixed `{MGBinding ...}` form does not work.

## The three binding push paths

Each time a binding's source property is resolved (first resolution, a new source object, a new data context, or
a different declared type), the engine picks one of three paths to push the value into the target:

1. A pilot target of ADR-0005 (one of `MGElement`'s resolved-value-store properties) goes through a typed
   `UIPilotPropertyResolver.TrySetTagged` overload (`Thickness`, `int?`, `Color?`; other reference-typed pilots use
   the `object` overload, which does not box).
2. Any other target of the *same declared type* as its source, with no converter and no string format, gets a
   typed copy (`CanvasLeft`/`CanvasTop`/`CanvasRight`/`CanvasBottom`, `AnimationStartOffset`, `IsAnimationPlaying`
   above all take this path when the view model's property matches the target's type exactly).
3. A binding with a `Converter`, a `StringFormat`, or a type conversion (e.g. `string` to `double`) keeps the
   previous reflection-based path.

Paths 1 and 2 allocate nothing (no reflection, no boxing, no closure). Path 3 may still allocate: a converter takes
and returns `object`, and a format builds a `string`. Bind a value of the target's own type when a screen cares
about per-frame allocation.

## Two known limits

- **O3, weak-event dispatch allocation.** With `UseWPF`, a source's `PropertyChanged` is dispatched through
  `System.Windows.Data.PropertyChangedEventManager`, which allocates roughly 192 bytes per notification, before any
  binding push runs. This is independent of which of the three paths above the push itself takes.
- **A failed resolution is not refreshed across resource scopes.** `MGResources.OnTextureAdded` is not relayed
  between scopes: if an `MGImage`'s `SourceName` failed to resolve (through `TryGetTexture`'s own scope-chain
  walk, including the provider fallback) and the texture is only added later in a *different* scope than the one
  that first asked, that image is not notified and keeps showing nothing until something else touches its
  `SourceName`.
