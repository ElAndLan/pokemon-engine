# IMPLEMENTATION_PLAN — Rebased Development Lifecycle

Version 3.0 — 2026-07-10

Status: **Authoritative.** This plan replaces the prior phase-completion narrative. Git history
preserves the old plan and progress log. `ARCHITECTURE_ADDENDUM.md` remains authoritative for
architecture decisions; its older phase assignments are superseded where this plan records the
2026-07-10 user-directed scope rebase.

## 1. Product definition

Creature Game Maker is two products sharing one rules library:

1. **Cgm.Runtime** — a reusable, content-agnostic 2D creature-RPG engine.
2. **Cgm.Creator** — a desktop authoring application that builds project data and assets for that
   Runtime without requiring users to write code.

`Cgm.Core` is the shared contract. It owns schemas, validation, deterministic game rules, battle
resolution, progression, movement legality, saves, and export data models. Runtime presents and
persists Core state. Creator edits Core definitions and launches Runtime for playtest.

## 2. Status vocabulary

The old plan used “Core complete” beside phases whose actual user workflow did not exist. This plan
uses four states and never collapses them into one completion claim:

- **VERIFIED** — every phase exit criterion passed, including integration/manual gates.
- **CORE BASELINE** — substantial headless logic exists and is tested; product integration is not
  implied.
- **PARTIAL** — useful implementation exists, but major named deliverables are missing.
- **NOT STARTED** — no phase-level deliverable can be claimed.

A phase advances only when its own exit criteria pass. A test count alone never closes a phase.

## 3. Audited baseline

Baseline commit: `5bc0e1a` (`Expand data-driven battle move coverage`).

- Build: 0 warnings, 0 errors.
- Tests: 946 passed — 814 Core, 104 Creator, 21 Runtime, 7 Tools (after Phase 15A).
- Coverage: Core 96.04% line / 90.30% branch; Creator package 66.75% / 66.24%; Runtime
  package 48.45% / 36.64%.
- Core battle: deterministic singles, switching, capture, progression, statuses/stages, v5 effect
  operations, Smart AI baseline, and v6 ability/held-item/weather/form foundations.
- Creator: project shell plus type/move/item/ability/species editors; several advanced structures
  are raw JSON; asset/map algorithms exist without their required canvases.
- Runtime: window/input helpers and a demo-specific readable battle harness; no reusable overworld,
  asset-backed renderer, normal scene flow, save persistence, or audio.
- Export: JSON pack, config, local template copy, and local smoke path; no packaged assets,
  self-contained CI templates, Creator export workflow, or clean-machine verification.
- PokeAPI move corpus: 937 JSON files in `docs/pokeapi-results/move/`.
- Existing expressibility audit: 468 PASS / 469 FAIL. PASS means “current generic operations appear
  capable of expressing this move”; it does **not** yet mean end-to-end certification.
- Certified Phase 15 move coverage: **0/937** until the conformance harness defined below exists.

## 4. Rebased phase status

| Phase | Name | Status | Honest current position |
|---:|---|---|---|
| 0 | Product Architecture and Governance | VERIFIED | Stack, repository rules, ADRs, and product split exist |
| 1 | Toolchain and Solution Foundation | VERIFIED | .NET 10 solution, CI build/test, Creator and Runtime hosts |
| 2 | Schema, Serialization, and Validation Foundation | VERIFIED | Schema v3, migrations, loaders, IDs, validators; ongoing gaps remain phase-owned |
| 3 | Creator Shell and Editor Pattern | PARTIAL | Shell/undo/pathfinder editors exist; lifecycle/manual gates and shared controls incomplete |
| 4 | Asset Processing and Slicing | CORE BASELINE | Decode/slice/atlas algorithms tested; asset browser and slicer canvas absent |
| 5 | World Authoring | CORE BASELINE | Map layer/collision/tool helpers tested; tileset/map/entity UI absent |
| 6 | Runtime Platform and Rendering | PARTIAL | Window/input/viewport helpers exist; correct fixed-step scheduling and renderer absent |
| 7 | Overworld Rules | CORE BASELINE | Movement/collision/wander/interaction helpers exist; playable Runtime overworld absent |
| 8 | Battle Fundamentals | CORE BASELINE | Strong deterministic Core; ordinary asset-backed battle scene and goldens absent |
| 9 | Encounters, Capture, Progression, and Save Model | CORE BASELINE | Pure rules/model exist; Runtime loop and durable save repository absent |
| 10 | Economy, Inventory, and Storage | CORE BASELINE | Pure operations exist; player-facing flows absent |
| 11 | Trainers, Statuses, Stages, and Switching | CORE BASELINE | Core mechanics exist; trainer authoring/overworld/battle presentation incomplete |
| 12 | Pack and Export Data Path | PARTIAL | Data pack/template copy/smoke exist; assets/self-contained templates/UI/VM gate absent |
| 13 | Original Vertical Slice | NOT STARTED | Placeholder data and a battle harness are not a start-to-badge game |
| 14 | Advanced Effects, Smart AI, and v6 Foundations | CORE BASELINE | Many v5/v6 systems exist; the complete mechanic surface is not closed |
| **15** | **Complete Core Game Logic and Move Conformance** | **IN PROGRESS** | **15A complete; 937 inventoried, 0/937 certified; next 15B targeting/doubles topology** |
| 16 | Reusable Runtime Engine Completion | NOT STARTED | Begins only after Phase 15 |
| 17 | Creator Application Completion | NOT STARTED | Begins only after Runtime/Core contracts are stable |
| 18 | Integrated Vertical Slice and Production Export | NOT STARTED | Proves both products together |
| 19 | Release Hardening and 1.0 | NOT STARTED | Distribution, docs, migration matrix, beta, legal sweep |

Phases 3–14 are not reopened as independent current phases. Their useful code is preserved. Missing
product work is regrouped into Phases 16–18 so the project has one clear current objective.

---

## 5. Phase 15 — Complete Core Game Logic and Move Conformance

### 5.1 Goal

Finish the reusable, deterministic Core rules engine before resuming UI/runtime breadth.

The non-negotiable exit criterion is:

> Every move represented by the 937 JSON files in `docs/pokeapi-results/move/` is normalized into
> generic data, validates, compiles, and behaves correctly in every battle context required by that
> move, with no bespoke move-name resolver code and no unresolved/unsupported entries.

Phase 15 is Core-first. It may change Core schemas and tools when required, using the schema-change
workflow. It does not build final Creator screens, battle animation, or the production Runtime.

### 5.2 Correctness target

“Functions correctly” means all of the following:

1. The source entry is present in the locked corpus manifest and its content hash matches.
2. Its relevant PokeAPI metadata is normalized: type, class, power, accuracy, PP, priority, target,
   hit/turn counts, drain/recoil/healing, crit, ailment, flinch, stat changes, and effect semantics.
3. Its normalized definition uses reusable primitives, scoped conditions, query hooks, ruleset
   policies, and presets. No Core branch checks a move ID or official move name.
4. The definition passes strict validation. Unknown ops, params, hooks, tags, targets, or ruleset
   requirements fail validation.
5. The definition compiles into typed runtime effects.
6. It resolves without exception in each required topology and ruleset.
7. Its meaningful outcomes are asserted: damage/power formula, target set, timing, status/stage,
   state mutation, switch behavior, item/ability/type interaction, failure conditions, RNG draws,
   and emitted events as applicable.
8. A move that is inapplicable in one context fails correctly there **and** succeeds in its valid
   context. For example, an ally-targeting move failing in singles is not sufficient; it must also
   work in doubles.
9. Visual-only behavior uses an explicit presentation marker; true no-battle and post-battle
   behavior uses a reviewed `noBattleEffect`, `postBattleReward`, or overworld effect—not a silent
   placeholder.
10. Reference-data gaps are resolved from authoritative mechanics documentation or explicitly
    proven to have no executable effect. Phase exit permits zero `unknown`, `unmapped`, `disabled`,
    or `reference-blocked` moves.

### 5.3 Ruleset policy

The canonical conformance target is the latest behavior represented by the local PokeAPI entry.
Shared foundational formulas retain the project's Gen III/IV-inspired default profile. Where the
corpus supplies `past_values` or a move requires a later-generation rule, behavior is selected by
an explicit ruleset profile rather than by forking the battle engine.

Phase 15 must support:

- `gen4_like` for the project's default classic mechanics.
- `modern_reference` for moves whose correct behavior did not exist under Gen IV rules.
- Explicit per-generation overrides only when required to make corpus metadata unambiguous.

Supporting every undocumented historical version of every move is not required. Silently applying
Gen IV behavior to a modern-only move is also not acceptable.

### 5.4 Corpus and IP boundary

- The local PokeAPI corpus is design-time mechanics reference only and remains excluded from builds,
  samples, exports, and release artifacts.
- A Phase 15 audit tool may read `docs/pokeapi-results/move/` locally.
- Checked-in conformance vectors use neutral numeric reference keys and mechanics data; they do not
  introduce official assets, audio, maps, or shipped content.
- Runtime/demo projects continue using original creature and move names.
- The corpus manifest records file count, stable numeric key, source hash, mechanics-family tags,
  normalized-definition hash, required topology/ruleset, conformance status, and test identifiers.

### 5.5 Coverage metrics

Three metrics are tracked separately:

| Metric | Starting value | Phase 15 exit |
|---|---:|---:|
| Corpus entries inventoried | 937/937 | 937/937 |
| Expressibility audit PASS | 468/937 | 937/937 |
| End-to-end conformance certified | 0/937 | 937/937 |
| FAIL/unknown/reference-blocked | 469 audit FAIL | 0 |
| Bespoke move branches | 0 known | 0 |

Changing the local corpus changes the manifest hash and reopens certification for affected entries.
Counts never update by hand; the audit tool generates them.

### 5.6 Required Core architecture

Complete only the reusable capabilities demanded by at least one corpus move:

1. **Target resolver and battle topology** — singles and doubles active slots; self, selected,
   ally, user-or-ally, all allies, all opponents, all others, side, field, random, and party-slot
   targets; stable target order; spread reduction and adjacency policy.
2. **Ordered effect execution** — effect-local chance/failure semantics, per-target contexts,
   per-hit state, damage-result reuse, and correct faint/target invalidation behavior.
3. **Battle query hooks** — move type/class, base power, accuracy, crit, priority, turn order,
   offensive/defensive stat, effectiveness, final damage, healing, and grounded state.
4. **Scoped condition engine** — persistent, volatile, side, slot, field, weather, terrain, room,
   item, ability, form, and ruleset state with deterministic hooks, duration, counters, stacking,
   transfer, removal, and switch cleanup.
5. **Queued intents** — recharge, delayed damage/healing, two-turn and semi-invulnerable actions,
   next-turn gates, future attacks, Wish-style slot effects, and end-turn scheduled effects.
6. **Mutation helpers** — HP/resource costs, item consume/give/steal/swap/restore/suppress,
   ability copy/swap/replace/suppress/ignore, creature/move type overlays, and creature metrics.
7. **Move references** — call/copy/repeat/replace/force moves through one resolved-move path while
   preserving PP ownership, source, legality, exclusions, and deterministic RNG.
8. **Snapshot overlays** — substitute, transform/copied stats/types/moves, temporary form/type
   overlays, and revert invariants without mutating immutable definitions.
9. **Damage memory** — damage received/dealt by turn, hit, category, source, target, and cause for
   counter, revenge, Bide/stored damage, and conditional power.
10. **Switch flow** — forced switch, pivot, replacement, trapping, passable state, slot effects,
    and correct doubles slot selection.
11. **Ruleset policies** — generation/custom differences remain data/policy, never a parallel
    battle controller.
12. **Event and trace catalog** — every meaningful mutation, failure, immunity, RNG draw, hook,
    expiration, and queued action is replayable and inspectable.
13. **Legacy preset normalization** — interim named/preset-shaped Core fields and typed effects
    (for example named hazard/seed booleans) are migrated to generic condition/effect records when
    the behavior is shared. Compatibility is protected by tests during the migration.

### 5.7 Phase 15 workstreams and order

Work one smallest spec-complete slice at a time. Each slice updates the battle spec first, adds
validation, compiler mapping, resolver behavior, tests, and regenerated audit results.

#### 15A — Contract and conformance harness

- Complete the Phase 15 sections of `BATTLE_SYSTEM_SPEC.md` and `TESTING_STRATEGY.md`.
- Define the event catalog, effect trace, ruleset profile, target vocabulary, and conformance result
  schema.
- Implement `cgm audit-moves <corpus>` or an equivalent deterministic tool.
- Generate the locked 937-entry manifest and sanitized test vectors.
- Reclassify all existing 468 PASS rows under the stronger certification definition.

Exit: repeatable baseline reports 937 inventoried, 0 certified, 469 known audit failures, and no
unexplained row/count mismatch.

**Status: COMPLETE (2026-07-10).** `cgm audit-moves` generated
`docs/move-conformance/manifest.v1.json`: 937 inventory-only entries, 0 certified, corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`. Regeneration is
byte-identical and a complete source-name scan found zero names in the sanitized output. Seven
neutral Tools tests cover ordering, hashes, digest sensitivity, sanitization, duplicate IDs,
endpoint/hash validation, filename integrity, and empty input. The legacy 468/469 expressibility
ledger remains historical input; strict certification is reset to 0/937 by design.

#### 15B — Targeting and doubles topology

Primary audit group: target selection and battle topology (144 moves; groups overlap).

- Generalize battle sides to stable active slots without rewriting singles behavior.
- Resolve all corpus target shapes.
- Implement doubles action collection, legality, ordering, spread targets, adjacency, redirection
  seams, faint replacement, and multi-target event order.
- Keep UI out; tests submit actions directly.

Exit: every move whose only blocker was topology/targeting compiles and resolves in its valid
singles/doubles context; old singles tests remain green.

Progress (2026-07-10): target-vocabulary foundation complete. The serialized move target enum now
covers all 16 target shapes present in the locked corpus. `BattleTopology` and the pure
`BattleTargetResolver.ResolveScope` establish stable singles/doubles slot ordering, active/side/
field/fainted-party/move-reference scopes, explicit ally/selected-target legality, and deferred
random-opponent selection with no RNG draw at resolution. The existing singles adapter preserves
implemented one-active mechanics and fails loudly for topology-dependent targets until the action
resolver lands. Schema v4 records the compatible enum expansion and v3-to-v4 migration is a no-op.
Focused target and migration/serialization tests pass; the next 15B slice is to move active-party
state and submitted actions from one active index per side to the stable slot model.

#### 15C — Query hooks and variable formulas

Primary groups: damage query modifiers (64), special accuracy (36), stat expansion (63).

- Complete base-power formula families: speed, weight, friendship, HP/status/item/terrain/weather,
  consecutive use, prior damage/action, party state, and stat comparison.
- Complete type/class/accuracy/crit/priority/turn-order/stat/effectiveness/final-damage queries.
- Add HP equalization, HP floors, percent-current/max HP, metric and inventory formulas.

Exit: formula families have table tests, exact rounding, compiler/validation coverage, and all
affected moves receive conformance cases.

#### 15D — Action timing, locks, and queued effects

Primary groups: turn timing/queued gates (64), volatile/status lockouts (49), move call/copy (13).

- Recharge, delayed attacks/heals, charge variants, semi-invulnerability, first/last-turn gates.
- Taunt, Encore, Disable, Torment, Imprison, Heal Block, throat/sound locks, perish/yawn/nightmare.
- Call/copy/repeat/force/replace moves and exclusion tags.
- One deterministic intent queue; no move-specific flags.

Exit: timing-sensitive goldens pin selection, execution, duration, failure, RNG, and cleanup order.

#### 15E — Field, side, slot, and condition completion

Primary groups: field/side conditions (53), cleanup (5), protect variants (8).

- Weather, terrain, rooms, gravity, screens, Safeguard/Mist/Tailwind, side protection.
- All entry hazards, removal, absorption, transfer, and side swap.
- Substitute and protect-family filters/contact consequences.
- Slot conditions for future attacks, Wish, replacement healing, and delayed effects.

Exit: condition definitions declare scope, hooks, duration, stacking, source, removal, events, and
AI-visible semantics; no condition behavior branches on a move ID.

#### 15F — Item, ability, type, form, and snapshot mutation

Primary groups: held item/berry mutation (19), ability mutation (8), type mutation (16), snapshot
overlays (6).

- Complete reusable mutation primitives required by moves.
- Finish Transform/substitute/form/type overlay and revert behavior.
- Complete once-per-battle action wrappers required by special corpus moves.
- Preserve held-item, ability, PP, form, and switch invariants.

Exit: mutation and reversion matrices pass across switch, faint, form, item consumption, suppression,
and battle end.

#### 15G — Switching, recovery, counters, rewards, and overworld move effects

Primary groups: switch/state passing (8), healing/cures/HP costs (29), counter/stored damage (8),
non-battle/post-battle effects (4).

- Pivot/forced switch/Baton-style transfer and trapping.
- Party cures, revival, sacrifice, drain/recoil/crash, delayed and formula healing.
- Counter/revenge/Bide/stored damage using shared damage memory.
- Reward and overworld operations required by move semantics use explicit Core actions.

Exit: every affected move has state-conservation and event-order coverage; UI remains a consumer.

#### 15H — Reference-gap closure and full normalization

Primary group: 46 moves with insufficient local English effect data, plus any new gaps found by
the strict harness.

- Research authoritative mechanics sources and record concise source notes outside shipped content.
- Normalize every move into generic operations/presets.
- Manually review every use of `noBattleEffect`, `postBattleReward`, unsupported tags, and ruleset
  override.
- Zero silent guesses.

Exit: 937 normalized definitions; zero unknown/unmapped/reference-blocked rows.

#### 15I — AI and simulation awareness

- AI legality uses the same target/topology resolver.
- Scoring understands every effect family at a conservative useful level or explicitly values the
  shared primitive outcomes; it never duplicates resolution.
- Smart AI supports doubles legal actions when a doubles battle is configured.
- Re-run seeded determinism, side-balance, and difficulty measurements after mechanics stabilize.

Exit: AI never selects illegal actions, never crashes on a certified move, and remains deterministic.
Perfect competitive play is not required.

#### 15J — Certification and closeout

- Run all 937 per-move conformance cases.
- Run family unit tests, formula tables, schema/validation tests, and cross-system goldens.
- Run seeded singles and doubles fuzz/soak battles using the entire normalized move set.
- Review Core for move-name/ID branches and unsupported fallbacks.
- Freeze the Core public battle contract for Phase 16 integration.

Exit: every Phase 15 criterion below passes and the closeout review is logged.

### 5.8 Mandatory testing layers

1. **Primitive unit tests** — pass/fail/boundary tests per op, query, condition, and formula.
2. **Compiler tests** — serialized data to typed effects, including strict invalid-param cases.
3. **Resolver tests** — state changes and events in singles/doubles.
4. **Per-move conformance tests** — at least one generated compile/resolve case for each of 937
   entries and assertions for every declared mechanic family.
5. **Golden replays** — full ordered event/trace snapshots for each mechanic family and interaction
   boundary; intentional changes require a reason.
6. **Property/fuzz tests** — HP/PP/stage bounds, creature conservation, no invalid targets, no
   infinite hooks/queues, deterministic replay, battle termination where rules permit.
7. **Regression** — all existing tests remain green; corpus coverage cannot decrease.

No new test package is required. Stable checked-in JSON/text snapshots are acceptable.

### 5.9 Phase 15 exit gate

All must be true:

- [ ] Corpus manifest locks exactly 937 source files and their hashes.
- [ ] Expressibility audit: 937 PASS, 0 FAIL.
- [ ] End-to-end conformance: 937 certified, 0 unknown/unmapped/disabled/reference-blocked.
- [ ] Every required target works in the correct singles or doubles topology.
- [ ] Every move-required primitive/query/condition/ruleset policy is specified and tested.
- [ ] Every certified move validates, compiles, resolves, emits deterministic events, and has a
      traceable conformance test.
- [ ] No move ID/name branch exists in Core.
- [ ] Static review finds no move-named resolver/compiler/helper/type and no content-key dispatch;
      named presets exist only in data and expand to reusable primitives.
- [ ] No official reference content is copied into samples, exports, or runtime packs.
- [ ] Event catalog and golden workflow are written and active.
- [ ] Seeded singles and doubles corpus fuzz tests complete without crashes, illegal state, or
      nondeterministic replay.
- [ ] Smart AI produces legal deterministic actions across the certified move set.
- [ ] Schema docs/migrations match every serialized change.
- [ ] Full build and test suite are green in CI.
- [ ] Focused Phase 15 review returns GO.

Phase 15 does **not** require final battle graphics, Creator effect editors, or a shipped PokeAPI
content pack. Those belong to Phases 16–18.

### 5.10 Phase 15 exclusions

- No bespoke move implementations.
- No final Runtime renderer or Creator UI work except headless test harnesses/tools.
- No breeding, multiplayer/netcode, official asset packaging, marketplace, or scripting language.
- No importing/shipping the official corpus as a playable pack.
- No mechanics added solely because they are imaginable; each primitive must be required by at
  least one corpus entry or an already-owned Core rule.

---

## 6. Phase 16 — Reusable Runtime Engine Completion

Goal: turn the completed Core into a content-agnostic playable engine.

Deliverables:

1. Remove all demo-specific IDs from Runtime boot.
2. Correct fixed-step tick scheduling and render interpolation.
3. `IRenderer`, OpenGL sprite batch, texture/atlas loading, virtual resolution, tilemap chunks,
   camera, sprite animation, and reusable UI primitives.
4. Raw-project and packed asset databases with equivalent GameDb/asset behavior.
5. Boot → Title → Overworld → Battle/Menu scene flow.
6. Asset-backed overworld movement, NPCs, dialogue, warps, encounters, trainers, and battle UI.
7. Party, bag, storage, shops, progression, evolution, save/continue/backup, options, and audio.
8. Singles and doubles battle presentation driven only by Core actions/events.
9. Headless input-replay seam and Runtime integration tests.

Exit gate: an original fixture project plays from New Game through walking, encounter, battle,
capture, save, reload, trainer battle, and doubles debug battle in raw and packed modes.

## 7. Phase 17 — Creator Application Completion

Goal: make every required engine capability authorable without hand-editing JSON.

Deliverables:

1. Complete project lifecycle, settings, recent projects, unsaved guard, validation navigation.
2. Asset browser and reusable slicer/map canvas.
3. Structured tileset, map, encounter, species, move/effect, item, ability, form, trainer, inventory,
   storage, event, and export editors.
4. Effect editor is generated from the closed Core effect/condition/query catalogs and shows
   ruleset/topology requirements.
5. Reference pickers, usage search, safe deletion, undo/redo, validation overlays.
6. Play, play-from-map, and focused battle sandbox launch the real Runtime process.
7. Large-project performance and autosave/recovery.

Exit gate: a user authors an original two-map game with creatures, a representative advanced move,
trainer, encounter, assets, and a doubles battle without editing JSON, then playtests it.

## 8. Phase 18 — Integrated Vertical Slice and Production Export

Goal: prove the Runtime and Creator as one reusable product.

Deliverables:

1. Original demo: at least 10 species, 30 representative moves, 3 maps, encounters, trainers,
   center, mart, storage, gym/badge, evolution, day/night, audio, and one doubles encounter.
2. Pack JSON, atlases, audio, and metadata; validate asset existence/hashes.
3. CI-built self-contained win-x64 debug/release templates.
4. Creator export UI, version/icon/config, smoke with distinct failure codes.
5. Deterministic new-game-to-badge input replay.
6. Clean Windows VM play/save/relaunch/continue test.
7. Stranger playtest from project creation through exported game.

Exit gate: a new user creates and exports a small original game, and another clean machine runs it
without .NET or developer tools.

## 9. Phase 19 — Release Hardening and 1.0

Goal: ship a stable Creator and versioned Runtime template.

Deliverables:

1. Creator distribution/update decision and packaging.
2. Tutorial, user manual, effect/move/ability/event references, and export guide.
3. Project/save/pack migration matrices from every released version.
4. Legal/IP and dependency-license sweep.
5. Crash diagnostics, malformed-project fuzzing, stress export corpus, performance gates.
6. External beta, zero open data-loss/FIX-NOW defects, release CI, changelog, semver policy.

Exit gate: a stranger installs Creator, completes the tutorial, builds and exports a two-map game,
and runs it on a clean machine without assistance.

## 10. Immediate next queue

1. Phase 15B: spec-lock the generalized target vocabulary and singles/doubles topology.
2. Re-audit the target/topology subset of the 468 legacy PASS rows under the strict definition.
3. Implement the smallest shared active-slot/target-resolution slice without changing UI.
4. Add the first per-move conformance cases and a singles+doubles golden.
5. Regenerate status/test IDs for only the moves proven by that slice.
6. Continue Phase 15B by target family; never hand-edit counts.

## 11. Change control

Changing Phase 15's corpus, correctness target, or zero-unsupported exit gate requires an explicit
user decision and updates to this file, `SCOPE_GUARD.md`, and the owning battle/testing specs in the
same change. Architecture changes require an ADR. New dependencies require TECH_STACK.md and user
approval.
