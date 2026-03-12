# Overlay Clip Policy

The clip stack now distinguishes three categories of visuals:

- Self visuals: background, border, element chrome, and the default overlay brush.
- Content visuals: hosted children and anything intentionally drawn inside the element's content region.
- Global overlays: diagnostics or docking decorations that must ignore local element clipping.

Current policy:

- `MGElement.Draw(...)` applies `self clip` around background, self, and overlay brush drawing.
- `content clip` is pushed only around content children and is disposed before `DrawOverlayBrush(...)`.
- Hover and pressed overlays that come from element background brushes therefore follow the self clip, not the content clip.
- Docking overlays (`MGDockPreviewOverlay`, `MGDockDropIndicators`) explicitly opt out of self/content clips so they can render outside local bounds when needed.

This keeps element chrome stable while preventing content-only clips from accidentally cutting off focus or docking visuals.