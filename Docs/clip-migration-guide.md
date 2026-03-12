# Clip Migration Guide

The composable clip pipeline is now the preferred extension point, but the old rectangle-only API remains available during migration.

Current transition points:

- Existing rectangle-only code can keep calling `SetClipTargetTemporary(...)`.
- New rectangle-only code should prefer `PushRectangleClip(...)`.
- New controls that declare intent should override `GetSelfClipDefinition(...)` and/or `GetContentsClipDefinition(...)`.
- New non-rectangular clips should use `ClipDefinition` and let the resolver choose scissor, stencil, or mask.

Migrating an existing control:

1. Keep existing draw logic unchanged.
2. Move clip intent into `GetSelfClipDefinition(...)` or `GetContentsClipDefinition(...)`.
3. Return `ClipDefinition.Rectangle(...)`, `ClipDefinition.RoundedRectangle(...)`, or `ClipDefinition.ArbitraryGeometry(...)`.
4. Delete any local clip scope that only existed to emulate the same intent inside `Draw(...)`.

Adding a new clip kind later:

1. Extend `ClipKind` and `ClipDefinition`.
2. Teach `ClipStrategyResolver` how to pick an effective backend.
3. Implement the backend push/pop in `ClipManager`.
4. Keep controls unaware of backend details by returning only logical clip definitions.