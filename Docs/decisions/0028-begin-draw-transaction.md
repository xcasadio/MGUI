# ADR-0028: The runtime method that opens a draw transaction is named BeginDrawTransaction

- **Status**: Accepted
- **Date**: 2026-11-15
- **Source**: this chantier: the allocation-free desktop frame (task T3.5 of the CasaEngine plan `ai-agent/tasks/mgui-desktop-frame-nonalloc-tasks.md`, requested by the author on 2026-11-15)

## Context

`IUIDesktopRuntime` required a method named `CreateDrawTransaction(DrawSettings, bool)`. The name says that each call creates a new object. That is no longer a promise of the contract: CasaEngine's `CasaDesktopRuntime` owns one transaction for its whole life and hands the same object back at each call, in the state of a new transaction (engine ADR-0097). The method opens a transaction that `Dispose` closes; whether the object is new is the business of the runtime.

## Decision

- `IUIDesktopRuntime.CreateDrawTransaction` is renamed `BeginDrawTransaction`. The signature and the behaviour are unchanged.
- Public API break, requested by the author on 2026-11-15: there is no `[Obsolete]` alias and no forwarding member. Every implementation and every caller is renamed (`MainRenderer`, `MGDesktop.Draw`, the samples and the tests).
- `MainRenderer` still creates a new transaction at each call: only its name changes. The internal overload with a default `DrawContext` is renamed with it.

## Consequences

- A host that implements `IUIDesktopRuntime` or calls the method must rename it; the build fails until it does.
- The documents `docs/rendering-architecture.md` and `docs/custom-render-backend-integration.md` use the new name. The ADRs written before this one keep the old name where they describe the code of their time.
