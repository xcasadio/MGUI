# Architecture Decision Records

This folder records the architecture decisions of this repository: architecture, data formats, public APIs, backends, and the rules that govern the AI agents working on it.

## Rules

- One file per decision, named `NNNN-short-title.md` with an increasing four-digit number. Decisions taken together on the same theme may share one file.
- Written in English, from the template [template.md](template.md): Status, Date, Source, Context, Decision, Consequences.
- Every decision taken during a plan or a discussion is recorded here, at the time it is taken (skill `adr`).
- Backfilled decisions keep a `Source` pointing at the original document. Existing audits are read-only: the decision is copied here, the audit is not edited.
- A decision is never rewritten: a change is a new record that supersedes the old one, whose status becomes `Superseded by ADR-XXXX`.

## Index

| ADR | Title | Status | Date |
|---|---|---|---|
| ADR-0001 | Tie DynamicResource subscriptions to tree membership with a single weak scope link | Accepted | 2026-09-07 |
| ADR-0002 | Align the docking part vocabulary on the framework's shared roles | Accepted | 2026-09-07 |
| ADR-0003 | Clear the MGTabControl selection when the removed selected tab has no successor | Accepted | 2026-09-07 |
| ADR-0004 | Resolve hit-test occlusion from the window that displays an element | Accepted | 2026-09-07 |
| ADR-0005 | Per-element resolved value store for seven pilot properties | Accepted | 2026-09-07 |
| ADR-0006 | Animation system: render-only transform, desktop-owned engine and value model | Accepted | 2026-09-12 |
| ADR-0007 | Animation V2: composition, keyframes, named visual states and style integration | Accepted | 2026-09-12 |
| ADR-0008 | Animation V3: editor readiness, remaining pilot limits and ad hoc migrations | Accepted | 2026-09-13 |
| ADR-0009 | Animation V4: brushes as freezable, animatable data objects | Accepted | 2026-09-14 |
| ADR-0010 | XAML editor V1: text as the source of truth, loader source positions, separate MGUI.Editor library | Proposed | 2026-09-17 |
| ADR-0011 | Animation V5: awaitable animations, smooth scrolling, texture frames, layout transitions, enter and exit animations | Accepted | 2026-09-17 |
| ADR-0012 | Keep emptied docking tab groups as placeholders and persist panel places in layout format 2.0 | Accepted | 2026-09-17 |
| ADR-0013 | One rule for keyboard focus navigation, and text entry controls as navigation targets | Proposed | 2026-09-18 |
