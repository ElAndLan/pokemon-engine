# SCOPE_GUARD.md

Binding scope law for Creature Game Maker. Read before accepting any task.
Source of authority: ARCHITECTURE_ADDENDUM.md §3 (wins over MASTER_PLAN.md).

## Current phase

**Phase: 15 - Abilities, Held Items, Weather & Forms.** Phase 14 is verified as a Core baseline, not final-tuned. Phases 0-13 are implemented
mostly as headless/Core logic, with many display-dependent pieces still deferred. Current Core
work now includes the complete v5 effect palette plus the Phase 14 smart-AI Core slice:
`TrainerAi` profile dispatch, smart move/switch action selection, named score tables, seen-move
memory, status/setup/hazard/protect/force-switch/recovery/item scoring, finite trainer healing item actions, switch cooldown tests,
table-driven decision-branch fringe tests, `TrainerAi` profile-dispatch routing tests, multi-hit per-hit crit-independence coverage, a seeded AI-vs-AI integration smoke (termination + per-seed determinism), item-interaction fringes (KO-over-heal, strongest-item), in-battle item path (bench-heal, no-overheal cap), and a mirror-match difficulty measurement harness (785 tests locally). Smart-vs-Basic mirror win rate tuned from 53.5% → 58.8% @400 (NoiseFraction 0.10→0.05); weight-tuning against greedy Basic is at its ceiling. Benchmark teams then ENRICHED (4 mons; hazards/priority/protect/force-switch/setup/status/recover): Smart-vs-Basic = 52.5% @400 — the full toolkit exposed that a non-switching opponent can't fairly value hazards/force-switch (cutting them "gains" but overfits; setup is validated — cutting it drops to 42%). Self-play tuning then found the big lever: **`SwitchThreshold` 35→50** (default over-switched into hazards/priority because switch scoring is hazard-blind) — Smart-vs-Basic 52.5% → **69.0% @400**, and beats the old behavior ~83% in self-play. Setup validated/kept. Hazard-aware switch scoring added (controller exposes hazard state → SmartAiContext → switch value subtracts expected switch-in damage; unit-tested). Phase 14 is verified for now; full tuning is deferred until Phase 15+ mechanics exist.
2026-07-09 note: switch scoring now gates on relative gain over staying, then adds that gain to the
best stay/move baseline for final ranking. Default `SwitchThreshold` is 100 after the corrected
relative formula over-switched at 35.

2026-07-09 verification note: Phase 14 is accepted for now with 785 passing tests, Smart-vs-Basic
at 69.0% @400, and Smart-vs-Smart side balance at 49.2%. Further AI tuning is deferred until
Phase 15+ battle mechanics are available; display/debug-console score-table integration is
presentation work and not a Phase 14 gate.

2026-07-09 Phase 15 demo-showcase note: Core v6 hook/form work, minimal Creator authoring
surfaces, sample showcase data, standalone export template-copy, Runtime `--smoke`, exported
config/pack loading, and the playable/readable exported 3v3 showcase fight are green at 865 tests.
Use `docs/IMPLEMENTATION_PLAN.md` -> "Phase 15 remaining work priority queue" as the authoritative
next-work order; the next unfinished item is the Phase 15 exit review/closeout audit.

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
| `smart` trainer AI (slice ships `basic`) | Phase 14 — verified baseline |
| Abilities, held items in battle, weather ability/item/form interactions, forms/Mega/Gmax | Battle v6 — Phase 15 |
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
