# ADR-0026: Text engines measure and draw a span of characters

- **Status**: Accepted
- **Date**: 2026-11-14
- **Source**: this chantier: the allocation-free `MGTextBlock` pipeline (decision D2 of the CasaEngine plan `ai-agent/tasks/mgui-text-nonalloc-tasks.md`, answered by the author on 2026-11-14)

## Context

`ITextMeasurementEngine.MeasureText`, `ITextDrawEngine.DrawText` and `IUIDrawContext.DrawTextViaEngine` took a `string` only. A text that changes every frame (a value label, a counter) therefore needed a new `string` per change, whatever the rest of the pipeline did.

MonoGame's `SpriteFont` and FontStashSharp's `SpriteFontBase` take no `ReadOnlySpan<char>`; both take a `StringBuilder` for measuring and drawing (MonoGame.Framework.DesktopGL 3.8.5.1, FontStashSharp.MonoGame 1.5.7).

## Decision

- The three members gain a `ReadOnlySpan<char>` overload. The `string` overloads stay.
- Each new interface member has a default implementation that builds a `string` and calls the `string` overload, so an engine or a draw context written before this ADR keeps compiling and keeps its behaviour.
- `SpriteFontTextEngine`, `FontStashSharpTextEngine` and `DrawTransaction` implement the overloads without allocating: the engine copies the characters into a `StringBuilder` it keeps, and hands it to the font.
- A span overload returns and draws exactly what the `string` overload does for the same characters.

## Consequences

- A caller that keeps its text in a buffer can measure and draw it with no allocation on the two engines above. On any other engine the default implementation allocates one `string` per call: a backend that wants the allocation-free path overrides the overloads.
- An engine instance is not thread-safe for the span overloads (one shared buffer per engine). Text engines were already used from the UI thread only.
- `HostRuntimeContractTests` now expects two `DrawText` overloads on `ITextDrawEngine`.
- Proof: `MGUI.Tests/Integration/TextSpanGpuTests.cs` (real GPU; equal size, equal pixels, zero bytes per measure and per draw). The zero tests fail with the default implementations (9600 bytes for 200 calls).
