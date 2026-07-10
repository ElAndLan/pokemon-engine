# SCOPE_GUARD — Binding Scope Law

Version 3.0 — 2026-07-10

Source of current phase truth: `IMPLEMENTATION_PLAN.md` v3. Architecture decisions remain in
`ARCHITECTURE_ADDENDUM.md`; older phase assignments there are superseded by the explicit
2026-07-10 user-directed rebase.

## Current phase

**Phase 15 — Complete Core Game Logic and Move Conformance**

Starting baseline:

- 937 move JSON files in `docs/pokeapi-results/move/`.
- Legacy expressibility audit: 468 PASS / 469 FAIL.
- Phase 15A manifest: 937/937 inventory-only, corpus digest
  `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`.
- Strict end-to-end conformance certification: 0/937; Phase 15A is complete and Phase 15B is next.
- Core v0–v6 foundations and 946 tests exist, but the mechanic surface is not complete.

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
