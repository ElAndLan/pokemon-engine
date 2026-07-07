# SCOPE_GUARD.md

Binding scope law for Creature Game Maker. Read before accepting any task.
Source of authority: ARCHITECTURE_ADDENDUM.md §3 (wins over MASTER_PLAN.md).

## Current phase

**Phase: 4 — Asset Import & Sprite Slicer (not started).** Phases 0–3 complete: data layer (P2,
review PASS) + Creator shell, entity CRUD, validation strip, and the 3 pathfinder editors (P3
code complete, 120 tests; manual UI run + review outstanding). Buildable work now: flesh out
ASSET_PIPELINE_SPEC v0–v2 first (doc-gate), then PNG import + slicing (v0 manual grid, v1
common-size suggestion, v2 gutter detection — all headless-testable) + the slicer canvas
(compile-verified), per IMPLEMENTATION_PLAN Phase 4. Update this line at every gate.

## The rule

If a task is not in the current phase's deliverables (MASTER_PLAN.md §15 +
ARCHITECTURE_ADDENDUM.md §4), it is not built — regardless of how small, how "almost
free," or how enthusiastic anyone is. It goes to the Idea Ledger below instead.

"Stubbing it out for later," "adding the field now to save time," and "it was easier to
just implement it" are all violations, not shortcuts. The one sanctioned placeholder is
the empty `forms[]` array in the species schema.

## Never (non-goals — do not ledger these, refuse them)

- Any existing game engine/framework (Unity, Godot, Unreal, MonoGame, Raylib, FNA, …)
- Shipping official Pokemon assets/names/cries/music/maps in any artifact incl. tests
- Multiplayer, netcode, real trading (trade evolutions = NPC/flag mechanism)
- 3D, general-purpose engine features, plugin API pre-1.0, user-facing scripting language
- Mobile/console export, in-app asset marketplace

## Deferred (each has a designated layer — build ONLY at that layer)

Phase assignments are authoritative in IMPLEMENTATION_PLAN.md; this table mirrors it.

| Item | Earliest layer/phase |
|---|---|
| Statuses, stat stages, priority | Battle v4 — Phase 11 |
| Advanced move effect ops (multi-hit, drain, protect, hazards…) | Battle v5 — Phase 14 |
| `smart` trainer AI (slice ships `basic`) | Phase 14 |
| Abilities, held items in battle, weather, forms/Mega/Gmax | Battle v6 — Phase 15 |
| Auto-slice heuristics beyond manual grid | Import v1–v2 — Phase 4 |
| Connected-component irregular slicing | Import v3 — Phase 17 |
| Animation grouping (manual clips Phase 4; character template helper Phase 13) | Import v4 — Phases 4/13 |
| Atlas packing / pack format | Import v5 — Phase 12 |
| Storage box UI (MVP = silent auto-deposit) | Phase 10 |
| Day/night, evolutions, centers/respawn, gyms/badges, audio | Phase 13 |
| Event system (fixed action vocabulary), surf/fishing, animated tiles, connected maps, NPC trades | Phase 16 |
| PokeAPI import wizard (private-use, user-initiated), bulk editing, custom pockets, templates | Phase 17 |
| Battle move animations, trainer approach animation, controller support, dex screen | Phase 18 |
| Installer (Velopack/MSIX), tutorial, user docs, crash reporting | Phase 19 |
| Embedded single-exe pack | Post-1.0 unless trivial in Phase 12 spike |
| Double battles, breeding/eggs, visual event-graph editor, localization, macOS/Linux | Post-1.0 (2.0 planning) |

## Idea Ledger (append-only; date + one line; implementing requires a phase decision)

- (empty)

## Scope-challenge protocol

When anyone (user or agent) proposes out-of-scope work: (1) name the section above that
covers it, (2) offer to ledger it, (3) proceed per the user's explicit decision. Agents
flag; the user rules. Silent compliance and silent refusal are both failures.
