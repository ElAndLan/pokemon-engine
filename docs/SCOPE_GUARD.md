# SCOPE_GUARD — Binding Scope Law

Version 4.0 — 2026-07-11

Source of current phase truth: `IMPLEMENTATION_PLAN.md` v4.0. Architecture decisions remain in
`ARCHITECTURE_ADDENDUM.md`; older phase assignments there are superseded by the explicit
2026-07-10 user-directed rebase. The 2026-07-11 no-stall roadmap directive authorizes agents to
complete owning specification sections using IMPLEMENTATION_PLAN v4's locked package defaults before
implementation. Only the reserved decisions in v4 §2.1 require another user response.

## Current phase

**Phase 15 — Complete Core Game Logic and Move Conformance**

Starting baseline:

- 937 move JSON files in `docs/pokeapi-results/move/`.
- Legacy expressibility audit: 468 PASS / 469 FAIL.
- Phase 15A manifest: 937/937 inventory-only, corpus digest
  `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`.
- Strict end-to-end conformance certification: 94/937. Phase 15A, the complete 15B target/topology
  workstream, and 15C-2/3/4/5/6 HP/status/speed/action-history/party-resource/damage-query formulas are complete. Their generated normalized
  definitions and
  per-reference formula/doubles vectors are green; the 15B cumulative golden and focused exit review
  are also green. Remaining entries are owned by
  15C-15G mechanics or later 15H reference closure. The 15C-1 exact numeric-query, 15D-1 typed-intent,
  15E-1 scoped-condition-store, 15E-2 typed-hook-dispatcher, 15F-1 effective-value-overlay, and
  15G-2 bounded action/damage-memory foundations, plus reusable queued-gate and HP-mutation packages,
  have also landed outside that certified cohort. The complete 15E-3 weather family and the terrain
  intrinsic lifecycle/grounded/damage/status/priority/healing, authored-interaction, and shared
  natural/effective-environment input, lifecycle-hook, grounded-override, and terrain-seed
  checkpoints have landed; the terrain family is complete. The combined room/gravity/sport
  criterion is also complete, including Gravity accuracy and move availability, room interactions,
  sport profile vectors, source cleanup, and AI parity. Phase 15E-3 and the complete 15E-4 side-
  condition family are green, including screens, status/stage guards, side speed/order, critical
  guard, paired-action side effects, side-wide protection, and the complete 15E-5 generic entry-
  hazard family, 15E-6 protect/contact-block family, and 15E-7 generic condition
  cleanup/transfer/swap family. The 15E workstream and the complete 15C query workstream are green,
  including accuracy, critical, priority, final-damage, healing, one-shot query conditions, and
  resolver/Smart-AI parity. The complete 15D-2 action-gate/recharge registry, shared legality and
  intent-queue integration, Smart-AI source-gate filtering, and 10 generated conformance vectors are
  green. The next eligible work is 15D-3 charge and semi-invulnerability. Pair
  recognition/combined execution stays with 15D-7. Deferred
  environment consumers and individual conformance vectors remain with their owning later packages
  without advancing those packages.
- The 2026-07-11 Phase 15B specification-lock baseline had 979 green tests. Later package reports in
  `IMPLEMENTATION_PLAN.md` record the growing verified suite; test count is evidence, not phase exit.

Phase 15 ends only when all 937 moves validate, compile, and behave correctly in every required
context with reusable data-driven primitives and zero unsupported entries.

## Scope decision recorded

On 2026-07-10 the user explicitly replaced two older scope assumptions:

1. “Full official move coverage is retired” is no longer true for Core mechanics testing.
2. Doubles Core topology is no longer post-1.0 when a corpus move requires ally, spread,
   redirection, multi-slot, or doubles-only behavior.

This permits Core mechanics needed for complete move conformance. It does **not** permit official
content in shipped games or expand the project into multiplayer/netcode.

## Allowed in Phase 15

- Core effect primitives, queries, conditions, targets, queues, ruleset policies, events, traces,
  battle topology, singles/doubles resolution, move references, snapshot overlays, switch flow,
  damage memory, and mutation helpers required by at least one corpus move.
- Core ability/held-item/weather/terrain/form interactions required by move correctness.
- Core overworld/reward actions when a move has a real non-battle effect.
- Strict validation, compiler mappings, deterministic tests, goldens, fuzz/property tests, and
  local audit tooling.
- Schema changes that follow `DATA_SCHEMA.md`, version bump, migration, and fixture requirements.
- Smart AI legality/scoring updates required to understand the completed primitive surface.
- Documentation needed to lock mechanics, RNG, timing, ordering, and failure semantics.

## Not allowed in Phase 15

- Bespoke code for a named move or checks against move IDs/names.
- Final Creator screens, Runtime rendering, battle animations, audio, or production export work.
- Shipping or bundling PokeAPI names/data/assets as game content.
- Building a PokeAPI import wizard or official-content sample pack.
- Breeding, multiplayer/netcode, trading between players, marketplace, localization, installer,
  plugin API, or general-purpose scripting.
- Speculative primitives not required by the corpus or an already-owned Core rule.

## PokeAPI boundary

The corpus is local design-time mechanics reference. Phase 15 tools may read it, and generated
audit/reference documents may identify its entries. It must never be copied into Runtime packs,
Creator templates, samples, exports, or releases. Checked-in executable fixtures use original
neutral content or sanitized numeric conformance keys.

## Phase 15 exit summary

- Corpus manifest: exactly 937 files and hashes.
- Expressibility: 937 PASS / 0 FAIL.
- End-to-end certification: 937 certified / 0 unknown, unmapped, disabled, or reference-blocked.
- Correct singles/doubles contexts and explicit ruleset behavior.
- Strict validation and typed compilation for every definition.
- Deterministic events/traces and per-move conformance coverage.
- No bespoke move branches.
- All unit, golden, fuzz, schema, and full-solution tests green.
- Focused review returns GO.

## Later phases

| Work | Phase |
|---|---:|
| Content-agnostic Runtime, renderer, overworld, battle/UI/save/audio integration | 16 |
| Complete Creator authoring workflows and structured effect editor | 17 |
| Original vertical slice, asset pack, self-contained export, clean-VM verification | 18 |
| Installer/distribution, docs/tutorial, migration audit, beta, 1.0 release | 19 |

## Permanent non-goals

- Existing game engines/frameworks.
- Shipping official Pokemon assets, names, cries, music, maps, or content packs.
- Multiplayer/netcode or real player trading.
- 3D or a general-purpose game engine.
- Mobile/console export or an in-app asset marketplace.
- User-facing programming language or plugin API before 1.0.

## Idea ledger

- (empty)

## Scope challenge protocol

For a proposal outside this file: name the conflict, offer the Idea Ledger when appropriate, and
wait for explicit user direction. The user may amend scope, but the decision must update this file
and `IMPLEMENTATION_PLAN.md`; silent drift is forbidden.
