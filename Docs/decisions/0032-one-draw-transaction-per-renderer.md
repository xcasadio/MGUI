# ADR-0032: The MonoGame backend keeps one draw transaction per renderer

- **Status**: Accepted
- **Date**: 2026-11-16
- **Source**: this chantier: CasaEngine plan `ai-agent/tasks/mgui-nonalloc-suite-tasks.md` (decision D5, task T1.6), approved by the author on 2026-11-16; the contract is the one the CasaEngine backend already has (CasaEngine ADR-0097)

## Context

Read in the code on 2026-11-16:

- `MainRenderer.BeginDrawTransaction` and `MainRenderer.DrawViews` created a `DrawTransaction` at each call. `MGDesktop.Draw(float, DrawSettings)` asks its runtime for a transaction at each frame, so a frame of the MonoGame backend allocated a transaction, its clip manager, its primitive drawing helper and, since ADR-0031, its slot tables.
- The callers of `BeginDrawTransaction` outside the tests are `MGDesktop.Draw` and `MainRenderer.DrawViews`; both dispose the transaction before they return. `MGUI.Samples` wraps the renderer in `BoundImagesAssetProvider`, which forwards the call. None keeps two transactions open (searched on 2026-11-16).
- The CasaEngine backend keeps one transaction per runtime and re-arms it (CasaEngine ADR-0097).

## Decision

- A `MainRenderer` has one `DrawTransaction`. `BeginDrawTransaction` creates it at the first call and re-arms it at each later call: the object returned is the same from call to call, in the state a new transaction built with the same arguments would be in.
- One transaction is open at a time. `BeginDrawTransaction` while the transaction is open throws `InvalidOperationException`. Public behaviour break: two transactions of one renderer could be open together before.
- `DrawTransaction.Dispose` ends the batch and closes the transaction, whether ending the batch succeeds or throws. The transaction is not to be used after its `Dispose`: the next draw of the renderer receives the same object.
- A re-arm resets the initial settings, the previous settings, the context, the projection of the primitives (from the current viewport) and the clip diagnostics. It forgets the temporary changes and the clips that the previous use left open: their handles restore nothing (ADR-0031). What the previous use left on the `GraphicsDevice` stays, as it does for a new transaction.
- If the re-arm or the begin of the batch throws, the transaction stays closed and the next call succeeds.
- Kept from one use to the next, because a new transaction could not tell them from what it creates: the slots of the handles, the kept rasterizer variants when the initial settings are one of the pair, and the derived settings of the renderer (ADR-0031).

## Consequences

- A frame drawn through `MGDesktop.Draw` on the MonoGame backend no longer creates a transaction.
- A caller that kept a transaction open while asking for another one gets an exception. A caller that needs two draws in a row disposes the first transaction first.
- A reference to the transaction kept after `Dispose` designates the next use of the renderer.
- A mask clip left open by a use keeps the render target it rented: it is not returned to the pool by the re-arm.
- Tests: `MGUI.Tests/Integration/MainRendererDrawTransactionGpuTests.cs`.
