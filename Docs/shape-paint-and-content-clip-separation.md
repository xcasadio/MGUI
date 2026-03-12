# Shape Paint And Content Clip Separation

The rounded-shape paint pipeline and the composable clip pipeline are intentionally separate.

- Shape paint remains owned by the existing box-shape brushes and geometry builders.
- Clip selection remains owned by clip definitions resolved through the shared rendering pipeline.
- Rounded controls do not need stencil just to draw a rounded background or border.
- Stencil is only required when a control asks for a non-rectangular clip on its contents.

Current examples:

- `MGBorder.DrawBackground(...)` and `MGBorder.DrawSelf(...)` still render via `MGBoxShape` + `MGBoxGeometry`.
- `MGBorder.GetContentsClipDefinition(...)` now reuses that same geometry only to describe a rounded content clip.
- `MGElement.Draw(...)` applies self clip first, then content clip, then disposes the content clip before overlays.

This keeps the rendering architecture composable:

- paint code chooses how to draw visuals;
- clip code chooses how to constrain subsequent drawing;
- the strategy resolver decides whether the constraint uses scissor, stencil, or mask.