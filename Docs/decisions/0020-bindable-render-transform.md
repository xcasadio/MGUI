# ADR-0020: Bindable render transform, through two new attributes renamed onto the existing nested target path

- **Status**: Accepted
- **Date**: 2026-09-26
- **Source**: `docs/plan-portrait-inventaire.md` §1.5, §2.3 (P1) and §3 (PI3) of the parent repository (CasaEngine gap
  G9, `CasaEngineMonogame/ai-agent/audits/mgui-gaps-from-xaml-screens.md`). Branch `chantier/render-transform-bindings`
  from `develop`.

## Context

- `UIRenderTransform` (`MGUI.Core/UI/Animation/UIRenderTransform.cs:15-96`) holds `Translation`, `Scale` (both
  `Vector2`), `Rotation` and `Origin`. It is `sealed`, `INotifyPropertyChanged` only, and render-only: its setters
  notify without ever invalidating layout (ADR-0006). `MGElement.RenderTransform` allocates it on first access and
  never replaces the instance (`MGUI.Core/UI/MGElement.cs:4922-4940`).
- XAML declares it through the `RenderTransform` DTO (`MGUI.Core/UI/XAML/Animation.cs:238-291`), whose `Translation`,
  `Scale` and `Origin` are strings parsed by `AnimationXamlParser.ParseVector2`/`TryParseVector2`
  (`Animation.cs:403-430`) and applied once, at load time, to `MGElement.RenderTransform`
  (`MGUI.Core/UI/XAML/Element.cs`, the settings-application method, right after the other appearance settings).
- That DTO is not `XAMLBindableBase`. A nested bindable object only receives an `{MGBinding}` if its runtime
  counterpart is itself `XAMLBindableBase` (`Element.cs`'s `CopyBindings`/`ProcessBindings`, and
  `XAMLBindableBase.GetNestedBindableObjects`): `UIRenderTransform` is not, and making it one would mean deriving a
  `sealed`, render-only value type from a heavier base built for XAML elements, for two components.
- A second, already-proven path exists: an `{MGBinding}` on an `Element` attribute can target a **nested** property
  path on the runtime element. `BindingPathMappings` (`Element.cs:1018-1028`) renames the XAML attribute onto that
  path (e.g. `Background` → `BackgroundBrush.NormalValue`); `DataBinding.ResolvePath` walks it
  (`MGUI.Core/UI/DataBinding/DataBinding.cs:298-338`); when the source and the resolved target property share the
  same CLR type, the push compiles to `TypedAccessorCache.GetOrBuildCopy` (`DataBinding.cs:507-513`,
  `PushStrategy.TypedCopy`, ADR-0016): no boxing, no allocation, no reflection on the hot path.
- The DTO already has a same-named-but-different attribute, `RenderScale` (`Element.cs:292`, a `float?`, the
  state-driven scale applied around the element's centre) — the new attributes must not collide with it.

## Decision

- `Element` (`MGUI.Core/UI/XAML/Element.cs`) gains two string attributes, `RenderTransformTranslation` and
  `RenderTransformScale`. A literal (`"x,y"` or a single number, validated the same way the DTO's own
  `Translation`/`Scale`/`Origin` are, via `AnimationXamlParser.ValidateVector2`/`ParseVector2`) is applied to
  `Element.RenderTransform.Translation`/`.Scale` **after** the `RenderTransform` DTO is applied, so a literal on
  either new attribute overrides the DTO's matching component. A null value (unset, or an `{MGBinding}`'s
  placeholder returned by `MGBinding.ProvideValue`, `MGBinding.cs:92-108`) writes nothing.
- `BindingPathMappings` gains `RenderTransformTranslation` → `RenderTransform.Translation` and
  `RenderTransformScale` → `RenderTransform.Scale`. `ProcessBindings` (`Element.cs`) renames the binding before
  handing it to `DataBindingManager.AddBinding`, whose `DataBinding` then resolves `RenderTransform.Translation`/
  `.Scale` on the live `MGElement` through the existing nested-path walk — the same mechanism `Background` and
  `CanvasLeft` already use, not a new one.
- Neither `UIRenderTransform` nor the `RenderTransform` DTO nor `RenderScale` changes. No new public type.
- **Rejected**: making `UIRenderTransform` derive from `XAMLBindableBase` so the DTO's nested object could carry
  bindings directly. Rejected because it would add a heavier base to a `sealed`, render-only value holder for two
  components, when the nested-target-path route already reaches the same instance, with the same allocation-free
  push, through infrastructure the codebase already relies on for other nested targets.

## Consequences

- A view model with `Vector2` `Translation`/`Scale` properties binds an element's render transform with an ordinary
  `{MGBinding}`, taking `PushStrategy.TypedCopy`: no allocation per push (`MGUI.Tests/Architecture/
  RenderTransformBindingTests.cs`), and no layout invalidation, since the write lands on `UIRenderTransform`'s own
  setters, unchanged by this decision.
- `Width`/`Height` remain the only bindable attributes that invalidate layout; render transform bindings never do
  (D3 of the portrait-inventaire plan).
- Composition under a parent's own `RenderTransform` (e.g. a root `Canvas` carrying a window's whole integer scale,
  as Alundra's screens do) is unaffected: the child's local matrix still composes before the parent's, exactly as
  for a literal or a directly-set transform (`RenderTransformBindingTests.Composition_UnderIntegerParentScale_
  MatchesMatrixComposition`).
- `MGUI.Samples/Features/RenderTransformBinding.xaml(.cs)` demonstrates both attributes on an `Image` inside a
  `Canvas`, flying and growing from the canvas' top-left corner (the default `Origin`).
- Engine follow-up: CasaEngine's `ai-agent/audits/mgui-gaps-from-xaml-screens.md` §G9 is marked corrected, citing
  this record, once the engine's MGUI submodule pointer picks up this commit.
