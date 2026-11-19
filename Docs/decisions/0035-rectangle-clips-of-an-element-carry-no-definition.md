# ADR-0035: The rectangle clips of an element are pushed as rectangles, without a clip definition

- **Status**: Accepted
- **Date**: 2026-11-16
- **Source**: CasaEngine plan `ai-agent/tasks/mgui-nonalloc-suite-tasks.md`, decision D16 (open point O8), recommended by the agent and followed on the request of the author of 2026-11-15, task T5.5

## Context

`MGElement.Draw` pushes two clips around an element, the self clip and the contents clip, which the hooks `GetSelfClipDefinition` and `GetContentsClipDefinition` returned as `ClipDefinition`, with `DA.Context.PushClipTemporary(definition)`. Read in the code and measured on 2026-11-16:

- A `ClipDefinition` is an immutable `record class` (about 100 bytes with its `ClipShape`). To avoid one per draw, `MGElement` kept the definition of the previous draw per hook and a ring of the 16 last definitions, and `MGScrollViewer` kept its own: a definition was reused only while the bounds (and the debug name, by reference) were the same.
- Content that is scrolled continuously draws at bounds it never had: each frame gives new rectangle clips (the content, and every element of it that clips to its bounds), which the 16 entry ring cannot hold. A scroll viewer over 100 text blocks, moved by one pixel at each of 500 frames after 500 warm-up frames (`ControlFamilyZeroAllocationTests.ScrollViewerWithTallContent_Draw_OverOffsetsThatAreAllDifferent_AllocatesNothing`, bench `ControlBench`), allocated **9888 B per frame** in `Draw` (4,944,000 B over the 500 measured frames), all of it clip definitions.
- A rectangle definition resolves to the scissor in every case (`ClipStrategyResolver.Resolve`: `ClipKind.Rectangle` gives `ClipStrategy.Scissor`, the effective definition being the requested one). `PushRectangleClip(bounds, true)` does the same push: `ClipManager.PushRectangle` (MGUI) and `CasaClipManager.PushRectangle` (engine) count one scissor clip and call `PushRectangleClipCore(bounds, intersect)`, which `PushScissor` calls too for a definition, with the same bounds and the same intersection with the clip in force. The only difference is that the handle reports the rectangle it was asked for without a definition behind it (`ClipSlotState.ScissorRectangle`).
- The hooks are `internal`. `CreateRectangleClipDefinition` was `protected`; `rg` found no caller outside `MGUI.Core` in MGUI, `CasaEngine`, `CasaEngine.Editor`, `CasaEngine.EditorServices` and the tests.
- `DebugName` of a definition is read by the product code only for the messages of exceptions and as the key of those caches.
- `Clipping/ElementClipRequest.cs` (a record struct with a `Push` method) had no caller.

## Decision

An element whose clip is a rectangle no longer creates a `ClipDefinition`, whatever its bounds.

- The hooks `GetSelfClipDefinition` and `GetContentsClipDefinition` (internal virtual, same names) return an `ElementClipRequest`, now a readonly struct: no clip (`ElementClipRequest.None`), a rectangle (`ElementClipRequest.Rectangle(bounds)`) or a definition (`ElementClipRequest.FromDefinition(definition)`, which is none for null). It replaces the unused record struct of the same name and keeps its `Push(IUIRenderContext)` method.
- `MGElement.Draw` pushes the two requests with `request.Push(DA.Context)`: `PushRectangleClip(bounds, true)` for a rectangle (the intersection with the clip in force that `ClipDefinition.Rectangle(bounds, true)` had), `PushClipTemporary(definition)` for a definition, and the default handle (its disposal does nothing) for none. Push and pop semantics are unchanged.
- The rounded and geometry clips (`MGBorder` and `MGTextBox` contents with a rounded inside, `MGEllipse`, `MGPolygon`, `MGPathLite`) stay definitions pushed with `PushClipTemporary`, with their debug names.
- Removed, being dead once a rectangle carries no definition: the cached self and contents definitions of `MGElement`, the ring of 16 recent definitions (`ReuseRectangleClipDefinition`, `RememberRectangleClipDefinition`), `SelfClipDebugName`, the viewport definition and name that `MGScrollViewer` kept, and the name field of `MGTextBox` (it uses `ContentsClipDebugName`, built once, for the rounded case).
- `MGElement.CreateRectangleClipDefinition` (protected) is removed: no caller outside `MGUI.Core` exists, and nothing inside needs it any more. This is a breaking change of a protected member of a public class, accepted by the plan (D16) because there is no external caller. `CreateRoundedClipDefinition` and `CreateGeometryClipDefinition` are unchanged.
- `CreateBorderBackedContentsClipDefinition` (protected, unchanged signature) stays for `MGGridColorPicker`, which pushes its swatch clip itself with `PushClipTemporary` and keeps doing so (its clip is a definition named `.Swatches`, in the journal as before). It is built on the new internal `GetBorderBackedContentsClip`, which `MGBorder` and `MGTextBox` return from their hook. In the rare case where the inside of the border of the picker is a rectangle (a rounded host whose inner radius is zero) the definition is new at each draw, as the rounded one always was there.
- The rendering on screen does not change. The draw call journal changes for these clips only: a `PushClipTemporary/1` of a rectangular definition named `<Type>.Self`, `<Type>.Contents` or `<Type>.Viewport` becomes `PushRectangleClip/2` with the same bounds and the same intersect flag, and carries no debug name.
- The engine needs no code change: `CasaDrawTransaction` and `CasaClipManager` already implement `PushRectangleClip` the way MGUI does, and no code of `CasaEngine` or `CasaEngine.Editor` uses the hooks or the removed helper.

## Consequences

- Proof that the draw calls are the same, the journal being the only difference. The 37 frozen journals of `ControlFamilyZeroAllocationTests`, `DesktopFrameCharacterisationTests` and `DesktopFrameLayoutTextTests` were captured on the unchanged code, then normalised by `ElementClipJournal.Normalise` (test project, `Graph/OrderedRecordingDrawTransaction.cs`): each `PushClipTemporary/1` of a rectangular definition whose name ends with `.Self`, `.Contents` or `.Viewport` becomes the `PushRectangleClip/2` line of the same scope, bounds and intersect flag; every other line, the `.Swatches` clip in particular, is kept. The journals of the changed code equal the normalised journals of the unchanged code, line count and hash (SHA-256, first 16 hex digits), for the 37 of them. Nine contain no element clip and did not change at all. The 28 fingerprints that changed were re-frozen on the normalised values. `FrozenJournal.Assert` also fails when a journal holds the rectangle clip of an element as a definition, so the proof stays in the suite. The table gives, for the 28 journals that changed, the number of lines, the number of lines rewritten, the fingerprint before and the fingerprint after.

| Journal | Lines | Rewritten | Before | After |
| --- | ---: | ---: | --- | --- |
| BareWindow draw journal | 130 | 20 | 7465221FAD2D49C8 | D2CF59A9BD3CD31B |
| DefaultWindow draw journal | 322 | 63 | CA21F30ACC8E2699 | 3B64773DAF20101F |
| Full drag journal | 800 | 150 | 68E93B778DFA70A0 | 28216027FE3E2430 |
| Full draw journal | 340 | 75 | 4EA6EED126EC08FA | C33570F304CF3B13 |
| Full text draw journal | 3421 | 750 | 41F2D22E04C15451 | CBD830BC2DE6173D |
| FullInDockHost drag journal | 1270 | 340 | 52957F04D8CFA4E5 | 086B76BB5245F345 |
| FullInDockHost idle journal | 575 | 170 | C2490D7D7541C9AD | 9D2F0166D829607D |
| FullInDockHost text draw journal | 5771 | 1700 | B5F7B15D0E65D0AB | 619CAF35A17399FD |
| FullInDockPanel drag journal | 820 | 160 | 03BDAA5F77FFA1B3 | 52A2244390EF6E04 |
| FullInDockPanel idle journal | 350 | 80 | 02355343A2F879CC | 7DFE601A92B64C8A |
| FullInDockPanel text draw journal | 3521 | 800 | 12367A53E24DBF44 | F1AA7094430063C7 |
| FullInGrid drag journal | 820 | 160 | FEB8EB37392C2D41 | 23E93872079BE9FB |
| FullInGrid idle journal | 350 | 80 | E313EED065EDC31C | 9B362C0EA9AA6648 |
| FullInGrid text draw journal | 3568 | 800 | 499E2F3D359CB059 | 0791109678B802E1 |
| context menu draw journal | 1440 | 500 | 8C19560187849240 | 9266ECA7AE7728A6 |
| dock auto-hidden draw journal | 688 | 232 | A771A7614050A38E | 5C0A025A62A967D5 |
| dock drag draw journal | 3100 | 882 | 5B1B87801887E010 | BA04E47B44FF69B8 |
| dock tab behind draw journal | 672 | 228 | A79CC28083C4B3B9 | 33C33BE1B0090BDF |
| dock two groups draw journal | 632 | 212 | C81E268CEED773E7 | D83BEE98675FFA9B |
| grid draw journal | 1500 | 380 | 49B5CDC15160B011 | 1517D35FB852D0F5 |
| list box draw journal | 5876 | 1690 | 4F799554548B59F5 | BC458FAB8AE994E8 |
| list view draw journal | 4430 | 1200 | C44BDC8BCB28886C | AD29804048F6D65C |
| menu bar draw journal | 1111 | 360 | 8491EDE04F750CF0 | 41CFD489AC31D6E0 |
| menu bar dropdown draw journal | 1983 | 720 | C9B27046C894BB4E | CDFDC93B0E0E8ED2 |
| scroll viewer draw journal | 2912 | 912 | DDC79E6AEAE75E01 | 9E62836A6CD2B516 |
| tab control draw journal | 1100 | 300 | C3124B89E3FC9739 | 4A61ACA0BF83E614 |
| text box draw journal | 936 | 208 | 70E142E4BD23747E | A006940EB631CE11 |
| tree view draw journal | 4280 | 1890 | 0F2EE6FCC35D9D5F | 7593866D83B716B1 |

- Allocation: the same scroll (500 offsets that are all different, 500 frames after 500 warm-up frames) allocates 0 bytes in `Draw`, down from 9888 B per frame (`ScrollViewerWithTallContent_Draw_OverOffsetsThatAreAllDifferent_AllocatesNothing`).
- Pixels are proved unchanged on a real GPU by `CasaEngine.Tests` (`DesktopFrameGpuAllocationTests`, `DrawTransactionRearmGpuTests`, the reference frames of `ReferenceDesktopFramePixels`), whose fingerprints this change does not edit.
- A tool that read the debug name of an element clip in the journal (`GraphNoOpDrawTransaction.ClipPushes`, the resolution of a `ClipHandle`) no longer finds it for a rectangle: it reads the bounds. `TransformedClipTests` reads the viewport clip where it is in force (the clip bounds when the content of the scroll viewer begins to draw) instead of by the name `ScrollViewer.Viewport`.
- The names of the hooks keep `ClipDefinition` in them although they now return a request: renaming the 11 overrides, the architecture tests and the docs was left out as churn. `DrawClippedFillBrush` of `MGShapeElementBase` and the swatch clip of `MGGridColorPicker` push definitions themselves and are not element hooks.
