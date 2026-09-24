# ADR-0016: Host-resolved and animated image sources, bindable canvas coordinates, and allocation-free binding pushes

- **Status**: Accepted
- **Date**: 2026-09-24
- **Source**: this chantier: the CasaEngine program `ai-agent/tasks/bound-screens-tasks.md` (engine repository),
  decided with the author on 2026-09-22 to 2026-09-24 while making a game's XAML screens editable in the
  CasaEngine editor. Engine counterpart: CasaEngine ADR-0038.

## Context

- A XAML `Image` can name its source with `SourceName`, resolved against `MGResources.Textures`
  (`MGUI.Core/UI/MGImage.cs:43-109`, `MGUI.Core/UI/MGResources.cs:203-218`). That dictionary is only filled
  from C# (`AddTexture`, `TryLoadImage`); `TryGetTexture` never asks the host. A game engine whose images
  are catalogued assets (a sprite: a sheet texture plus a source rectangle) cannot let the markup name them.
- `IUIAssetProvider` exposes `LoadImage` and `TryLoadImage` only (`MGUI.Shared/Assets/IUIAssetProvider.cs:3-7`),
  a whole image by name, without a source rectangle and without any notion of time. A reflection test pins
  that shape (`MGUI.Tests/Architecture/AssetProviderTests.cs:22`).
- The `SourceName` setter unsubscribes and resubscribes `OnTextureAdded` and `OnTextureRemoved` on every
  change of value (`MGImage.cs:53-61`). A game UI that changes an image every frame therefore subscribes to
  events every frame.
- An `MGImage` cannot display a source that advances over time; it has no `UpdateSelf` override. Elements
  already receive a per-frame `UpdateSelf(ElementUpdateArgs)` with the frame's elapsed time
  (`MGElement.cs:4052`, `ElementUpdateArgs.cs:6`, `RenderLoopArgs.cs:32`), skipped for a collapsed element.
- `Canvas.Left` and `Canvas.Top` are metadata read through static methods (`MGUI.Core/UI/Containers/MGCanvas.cs:43-48`);
  they are not properties, so a binding cannot target them.
- A binding push reads the source and writes the target through `PropertyInfo.GetValue` and `SetValue`,
  boxing value types (`MGUI.Core/UI/DataBinding/DataBinding.cs:512, 526`). The `PropertyInfo` cache is keyed
  by object instance and never evicted (`DataBinding.cs:291-313`), so every bound object stays alive for the
  process lifetime. For the eight pilot properties of ADR-0005, `BuildTaggedWriter` allocates a closure on
  every push (`DataBinding.cs:402-411, 441`).
- Modern XAML stacks remove reflection from the push path with typed accessors: generated at build time by
  WinUI `x:Bind`, Avalonia compiled bindings and .NET MAUI compiled bindings. MGUI parses its XAML at run
  time, so a build-time generator cannot see which paths a document binds.

## Decision

- **Host resolution of image names.** `IUIAssetProvider` (in `MGUI.Shared`, which sees `IUIImageResource` and
  MonoGame's `Rectangle` but not `MGUI.Core`) gains a member, with a default implementation that resolves
  nothing, that turns a name into an image and an optional source rectangle. `MGResources.TryGetTexture`
  falls back to it when a name is unknown in the whole scope chain, builds the `MGTextureData`, and caches
  it at the root scope. The host decides what a name means (for CasaEngine: a sprite asset id or
  name) and owns the lifetime of what it resolves.
- **`SourceName` without event churn.** An `MGImage` subscribes to the texture events of its resources once,
  not on every change of `SourceName`.
- **Animated image sources.** MGUI declares a game-agnostic interface for an image source that advances with
  time, in `MGUI.Shared` next to the provider: advance by an elapsed time, read the current frame (an image, an
  optional source rectangle and a draw offset in pixels), restart at a given offset. The provider can create one per image from a name. `MGImage` advances it in `UpdateSelf` with the
  frame's elapsed time, never while collapsed, and exposes a start offset and a playing flag as ordinary
  bindable properties. Advancing and drawing allocate nothing.
- **Bindable canvas coordinates.** An element exposes its canvas left and top as properties that write the
  existing canvas metadata and invalidate the layout, so `Canvas.Left="{MGBinding ...}"` updates live.
- **Allocation-free binding pushes.** Accessors are compiled once per (type, path) with
  `System.Linq.Expressions` and cached per type; the `PropertyInfo` cache is keyed by type. The target side of a
  binding (pilot or not, its tagged writer and `LocalBinding` source) is decided once, when the binding is built.
  The push path is chosen each time the source property is resolved: first resolution, new source object, new
  data context, or a different declared type, since a data context is usually set after the markup is loaded
  and may change. While no source is resolved, the third path applies. The three push paths:
  - a pilot target of ADR-0005 goes through a typed tagged write (typed overloads of
    `UIPilotPropertyResolver.TrySetTagged` for `Thickness`, `int?` and `Color?`; reference-typed targets keep the
    `object` overload, which does not box), with its writer and its `LocalBinding` source built once per binding,
    so the pilot keeps its provenance;
  - any other target of the same type as its source, without converter or string format, gets a typed copy;
  - a binding with a converter, a string format or a type conversion keeps the previous path.
  No build-time generator: the accessor cache may be pre-filled by one later without changing this design.

## Consequences

- A game can name a catalogued image in markup, and an editor that previews the markup with the same
  provider shows the real image.
- Existing implementers of `IUIAssetProvider` keep compiling; the architecture test that pins the interface
  shape is updated to the new, deliberate shape.
- A binding push on the first two paths, a `SourceName` change, and an animated image frame cost no
  allocation, no reflection and no event subscription; tests assert it with
  `GC.GetAllocatedBytesForCurrentThread`, pilot targets included.
- A binding with a converter, a string format or a type conversion may still allocate: a converter takes and
  returns objects, and a format builds a string. Screens that care about per-frame cost bind values of the
  target's own type.
- Runtime compilation needs the JIT: an ahead-of-time or trimmed build of MGUI would need the generator
  variant. No project in the repository publishes that way today.
- A resolved name stays cached for the lifetime of the root resources; freeing it is the host's job, done
  when the desktop is disposed.
