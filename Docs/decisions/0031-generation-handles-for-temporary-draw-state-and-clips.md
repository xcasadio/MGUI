# ADR-0031: Temporary draw state changes and clips are designated by generation handles

- **Status**: Accepted
- **Date**: 2026-11-16
- **Source**: this chantier: CasaEngine plan `ai-agent/tasks/mgui-nonalloc-suite-tasks.md` (decisions D2, D3, D4 and open point O1), approved by the author on 2026-11-16; the author asked on 2026-11-15 for a modern design over API compatibility

## Context

Read in the code on 2026-11-16:

- `IUIRenderContext.SetDrawSettingsTemporary`, `SetRenderTargetTemporary`, `SetTransformTemporary` and `SetClipTargetTemporary` return an `IDisposable`. Both backends (`MGUI.MonoGame.Integration` and the CasaEngine backend) create a `TemporaryChange<T>` object and a delegate per call.
- `PushClipTemporary` and `PushRectangleClip` return the class `ClipScope`. Since ADR-0027 a backend keeps its scopes and re-arms them, so a rectangle clip allocates nothing. The cost of that design is written in the summary of `ClipScope`: a reference to a scope that was disposed and re-armed since designates the new clip, so a second `Dispose` through a kept reference releases a clip that belongs to someone else. The stencil and mask paths still create a `ClipScope` and a closure per push.
- `SetTransformTemporary` and `SetEffectTemporary` derive a `DrawSettings` per call (`CurrentSettings with { ... }`). `DrawSettings` is an immutable record class (ADR-0029), and `CurrentSettings` is read as a whole by callers: `CurrentSettings.Transform`, `CurrentSettings.UsesScissorTest`, and `CurrentSettings with { ... }` to derive other settings (brushes, clip executors).
- Callers write `using (condition ? context.Push...(...) : null)`: a null scope is a valid operand of `using`.

## Decision

- The methods that change draw state temporarily return the struct `DrawStateHandle`; the methods that push a clip return the struct `ClipHandle`. Both implement `IDisposable` and are meant for `using`. The class `ClipScope` and the `IDisposable` return types go away as their callers are migrated (plan tasks T1.2 and T1.4). Public API break, no `[Obsolete]` alias.
- A handle holds its owner (`IDrawStateHandleOwner` or `IClipHandleOwner`, implemented by the backend object that keeps the state to restore), a slot number and a generation. The owner keeps one slot per use in force (`GenerationSlots<TState>`): the slot holds what to restore, and its generation changes each time a use ends.
- `Dispose` restores only if the generation of the handle is the generation of its slot. A second `Dispose`, a `Dispose` on a copy, and a `Dispose` after the owner was re-armed do nothing, even when the slot was reused since. No exception: a double `Dispose` was not an error before.
- The default handle designates nothing and its `Dispose` does nothing. It replaces the null scope: `using (condition ? context.Push...(...) : default)`.
- `ClipHandle.Resolution` is read through the owner while the clip is in force. It throws `InvalidOperationException` for the default handle and for a released clip; `TryGetResolution` and `IsActive` do not throw.
- Slots are released in any order, as `TemporaryChange<T>` and the re-armed scopes allowed: each slot restores the value it recorded.
- Open point O1, temporary transform and effect without an object per call: a transaction keeps the settings it derived, found again by the settings they derive from and by the value of the transform or effect (`DerivedDrawSettingsCache`). Equal settings are the same settings (ADR-0029), so the settings of a new frame find what the frame before derived. Rejected: a stack of matrices and effects applied without a `DrawSettings`, because `CurrentSettings` would no longer be the settings in force for the callers that read or derive it; reusable settings objects written again at each use, because `DrawSettings` is immutable and references to it are kept (`PreviousSettings`, the kept rasterizer variants of the transactions).

## Consequences

- A temporary change and a clip allocate nothing once the slots exist; a backend allocates only when more uses are in force at once than ever before.
- A kept handle cannot release a later use of its slot: the hazard written in ADR-0027 no longer exists. ADR-0027 is superseded for the type of the scope; its goal (a rectangle clip without allocation) is kept.
- A handle stored or passed as an `IDisposable` is boxed (24 bytes per boxing): callers keep it typed, and a test double that implements `IUIRenderContext` returns `default`.
- `using var _ = condition ? null : context.Push...(...)` no longer compiles: the callers write `default`.
- A generation is an `int` that skips 0: a handle kept while its slot is released 4 294 967 295 times would designate a later use. Not handled.
- Limit of the answer to O1: a transform or effect whose value is new (a transform animated every frame) still derives one `DrawSettings` for that value. A transform that keeps its value from frame to frame allocates nothing. A backend keeps at most 32 derived settings (`DerivedDrawSettingsCache.Capacity`); a new one replaces the oldest, so more than 32 distinct temporary transforms or effects per frame allocate again. A kept entry holds its effect until it is replaced or the cache is cleared.
- The kept derived settings rely on ADR-0029: settings hold nothing but their parameters, so a kept object cannot be told from a new one.
- Tests: `MGUI.Tests/Rendering/GenerationHandleTests.cs`, `MGUI.Tests/Rendering/DerivedDrawSettingsCacheTests.cs`, and on a real GPU `MGUI.Tests/Integration/ClipScopeRemainingAllocationMeasurements.cs` and CasaEngine `CasaEngine.Tests/UI/Backend/DesktopFrameGpuAllocationTests.cs`.
