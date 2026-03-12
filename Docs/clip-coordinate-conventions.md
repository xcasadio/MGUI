# Clip Coordinate Conventions

The composable clip pipeline uses two coordinate conventions at the same time:

- Rectangle clip bounds are expressed in the current render-target space.
- Geometry clip vertices stay in the element's local draw space.
- The active `DrawTransaction` transform is responsible for mapping local geometry into render-target space.

Implications:

- Scissor clips receive already-transformed bounds.
- Stencil clips reuse local rounded geometry and therefore continue to follow `RenderScale` and parent transforms.
- Mask clips allocate their temporary render target from the transformed bounds, then reuse the active transform to shift local content into that surface.

`MGElement.Draw(...)` applies `RenderScale` before it asks the element for its clip definitions. That means:

- rectangle-only elements keep using the transformed `TargetBounds` fast path;
- rounded/self-described geometry clips can continue to describe their vertices in local space;
- controls do not need to know whether the renderer will resolve the clip through scissor, stencil, or mask.