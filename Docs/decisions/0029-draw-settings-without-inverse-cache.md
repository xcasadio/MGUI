# ADR-0029: DrawSettings holds nothing but its parameters

- **Status**: Accepted
- **Date**: 2026-11-15
- **Source**: this chantier: the allocation-free desktop frame (task T3.7 of the CasaEngine plan `ai-agent/tasks/mgui-desktop-frame-nonalloc-tasks.md`, decision D9, requested by the author on 2026-11-15)

## Context

`DrawSettings` is a `record class`: its generated equality compares every instance field. Besides its ten parameters it carried two members that are not parameters:

- `InverseTransform`, computed on first read and kept in a private nullable field. A settings object whose inverse had been read no longer equalled a new object with the same values. A backend that keeps settings across frames (CasaEngine's `CasaDrawTransaction`, engine ADR-0097) had to keep a never-read copy of each kept object to detect that.
- `IsIdentityTransform`, computed once by the primary constructor.

Both went stale on a copy: `settings with { Transform = other }` copies the fields of the source, then sets `Transform`, so the copy kept the cached inverse and the identity flag of the source. Both draw transactions of the repositories build their temporary transform that way (`SetTransformTemporary`).

Read in the code on 2026-11-15: no file of MGUI or of CasaEngine reads `InverseTransform` or `IsIdentityTransform`, apart from `DrawSettings` itself and two lines of an engine test written to prove the kept copies.

## Decision

- `DrawSettings.InverseTransform`, its private cache and `DrawSettings.IsIdentityTransform` are removed. `DrawSettings` keeps its ten parameters and `UsesScissorTest`, which is computed at each read.
- Public API break, requested by the author on 2026-11-15: no `[Obsolete]` alias. A caller that needs the inverse computes it from `Transform` (`Matrix.Invert`) and keeps it where it is used.
- `DrawSettings` is `sealed`: a derived record could add state that changes after construction, and a backend that keeps a settings object relies on it never changing. No type derived from it in MGUI or CasaEngine.
- The equality stays the one the record generates. It is now an equality of values: two settings with the same parameters are equal whatever happened to either object.

## Consequences

- A host that read `InverseTransform` or `IsIdentityTransform` no longer builds. None is known.
- `SetDrawSettings` of a backend that skips equal settings now also skips settings that differed only by the state of the cache; it used to end the batch for them.
- A backend can keep a settings object and hand it out again without a guard. CasaEngine drops its kept copies (task T3.8 of the plan).
- Test: `MGUI.Tests/Rendering/DrawSettingsEqualityTests.cs`, which also fails if an instance field that is not a parameter is added to the record.
- `DrawSettings.Default` still returns a new object at each read.
