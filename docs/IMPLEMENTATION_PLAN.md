# IMPLEMENTATION_PLAN — Rebased Development Lifecycle

Version 3.1 — 2026-07-11

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
| 2 | Schema, Serialization, and Validation Foundation | VERIFIED | Schema foundation, migrations, loaders, IDs, validators; current project schema is v4 after Phase 15 additions |
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
| **15** | **Complete Core Game Logic and Move Conformance** | **IN PROGRESS** | **15A complete; 15B active; 937 inventoried, 0/937 certified; reusable query/gate/HP packages landed but are not per-move certification** |
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

### 5.7 Autonomous Phase 15 execution contract

This section is written for an implementation model that will repeatedly take the next roadmap
item without the user re-explaining the architecture. It is binding together with `AGENTS.md`.

#### Source-of-truth order

When documents or counts disagree, use this order:

1. `SCOPE_GUARD.md` decides whether the work is allowed now.
2. This file decides work order, package status, and phase gates.
3. `BATTLE_SYSTEM_SPEC.md` defines executable battle semantics and timing.
4. `EFFECT_TYPES_CATALOG_v0_5.md` defines the reusable primitive/condition/query model.
5. `MOVE_AUDIT_SYSTEM_PLAN.md` maps the legacy audit rows to reusable capability families.
6. `docs/move-conformance/manifest.v1.json` is the authority for corpus and certification counts.
7. `MOVE_AUDIT_RESULTS.md` is a historical expressibility ledger. Its row table is useful input,
   but its grouped counts are not certification counts and may lag implemented capabilities.

Never reconcile a conflict silently. Fix the stale document in the same change or record the
conflict as a blocker.

#### What one roadmap iteration means

One iteration completes one reusable **feature package**, not one named move and not one tiny
scaffolding method. A feature package is the smallest coherent behavior family that can meet all
of these requirements in one mergeable change:

1. One or more audit rows prove the behavior is required.
2. Existing helpers, BCL features, and current primitives were checked first.
3. The owning spec defines the generic operation/query/condition, params, timing, target scope,
   failure behavior, RNG order, rounding, events, cleanup, and AI-visible outcome.
4. Serialized data validates strictly and compiles to typed reusable runtime data.
5. The normal resolver path executes it without a move-name/ID branch.
6. Pure math, validation/compiler, resolver/event, boundary, and deterministic tests pass as
   applicable.
7. Every affected normalized move has a conformance vector before any certification count rises.
8. The full solution builds and tests, this plan records evidence, and the package is committed.

If a package exposes a required foundation such as per-target effect contexts, build that
foundation as part of the package. Do not land an unused abstraction and call the feature done.

#### Promotion ladder for a missing move mechanic

Stop at the first level that can express the behavior exactly:

1. Author data with existing ops/helpers.
2. Add compiler mapping to an existing typed effect or query.
3. Add params, filters, or a formula to an existing reusable primitive.
4. Add a data-defined condition using existing scopes and hooks.
5. Add a new reusable query kind, mutation helper, queue payload, or target policy.
6. Add a new primitive only when the mechanic introduces a genuinely new timing, state scope, or
   resolver category and the spec explains why levels 1-5 cannot hold it.

Forbidden endpoints: `specialCase`, arbitrary scripting, a helper/type named for a source move,
or a switch on move ID/name. A source move may appear in audit/reference documentation and test
case metadata; executable fixtures use neutral IDs.

#### Required completion record for every feature package

Append a short progress record under its 15B-15J workstream containing:

- package name and status (`COMPLETE`, `PARTIAL`, or `BLOCKED`);
- audit capability groups and exact reference keys affected;
- spec sections changed;
- reusable helpers/ops/conditions added or reused;
- validation/compiler/resolver paths covered;
- tests and commands run, including pass counts;
- RNG draw and event/trace changes, or `none`;
- manifest status changes generated by tooling, or `none` with the reason;
- remaining gaps and the next eligible package; and
- commit hash after commit.

`PARTIAL` never advances corpus counts. `BLOCKED` names the missing decision/reference/capability.
Do not use “engine supports this” as a synonym for “move certified.”

#### Complete Phase 15 capability ledger

The following is the exhaustive engine backlog derived from the 20 legacy audit groups. Group
memberships overlap; counts are planning signals, not additive totals. A workstream is complete
only when every listed behavior it owns is implemented where required by the corpus and all
affected moves are certified.

| Audit capability group | Owner | Reusable engine completion required |
|---|---|---|
| Target selection and topology (144) | 15B | One/two active slots per side; per-slot action submission and legality; all 16 target shapes; active/party/side/field/move scopes; selected/ally/random/spread targets; adjacency; stable order; redirection; slot-aware replacement and events. |
| Damage query modifiers (64) | 15C | One ordered query pipeline for move type/class, base power, offensive/defensive stat, accuracy, crit, effectiveness, and final damage; formula helpers for HP bands/ratios, status, speed, weight/metrics, friendship, item state/data, weather/terrain, consecutive use, prior actions/damage, party state, PP, stat stages/comparison, random tables, and ruleset overrides. |
| Timing, queues, and move gates (64) | 15D | One deterministic intent/condition path for recharge, charge and semi-invulnerability, delayed attacks/heals/status, first-use/repeat/prior-failure gates, target-action gates, focus/shell/contact setup, forced execution, cancellation, and switch/faint cleanup. |
| Volatiles, statuses, and lockouts (49) | 15D/15E | Generic creature conditions for infatuation, disable, encore, taunt, torment, imprison, heal/item/sound locks, yawn, perish, nightmare, curse, no-switch traps, ingrain, powder, rage, telekinesis/grounding, destiny/grudge, counters, duration, source, stacking, and switch cleanup. |
| Stat-stage expansion (63 historical group rows) | 15C/15F | Reuse ordered stage ops; add stage set/max/average/steal/guard/split/pass, derived-stat swap/average overlays, stockpile counters, random-stat selection, and explicit passable-state policies. |
| Field and side conditions (53) | 15E | Data-defined weather, snow, all terrains, rooms, gravity, sports, screens, safeguard, mist, tailwind, lucky chant, side guards, durations, replacement, removal, status/accuracy/damage/heal/turn-order hooks, and natural field inputs required by moves. |
| Held-item and berry mutation (19) | 15F | Shared inspect/require/consume/give/steal/swap/remove/destroy/restore/suppress helpers; consumed-item memory; item-derived type/power/effect queries; legality, failure events, and switch/battle cleanup. |
| Ability mutation (8) | 15F | Shared effective-ability overlay and copy/swap/replace/suppress/ignore/protect helpers integrated with hook lookup; immutable species definitions and deterministic restoration. |
| Creature and move type mutation (16) | 15F | Effective-type overlays for add/remove/replace/copy; move-type query overrides; grounding/STAB/effectiveness integration; temporary/permanent clear policies; no base-definition mutation. |
| Move copy/call/replace/force (13) | 15D/15F | One resolved-move selector/executor for known/target/last/party/random/environment pools; exclusion tags; PP and event ownership; recursion limits; temporary/permanent replacement policy; deterministic pool order/RNG. |
| Switch flow and state passing (8) | 15G | One switch-intent path for pivot, forced/self switch, escape variants, weather-plus-switch, trapping, replacement selection, slot ownership, Baton-style transfer whitelist, and cleanup/event order. |
| Hazard/screen/substitute/side cleanup (5) | 15E/15F | Remove/transfer by scope and tag; hazard/screen/volatile/substitute cleanup; side swap with source/duration preservation; grounded Poison-style absorption where required. |
| Protect variants/contact punishment (8) | 15E | One protection condition/filter model with chain decay, bypass tags, side variants, contact-block effect lists, failure events, and exact per-target behavior. |
| Healing, cures, costs, fractional HP (29) | 15G | Shared flat/fraction/full/formula/damage-derived/delayed/slot/ally/team healing; cure/transfer/rest/revive; HP costs; current/max HP damage; equalize/match HP; heal-block filters; weather/stat-derived healing; faint/full-HP boundaries. |
| Redirection and turn-order manipulation (11) | 15B/15D | Targeting hooks for redirect/spotlight/rage-powder filters and position swap; order intents for after-you/quash/helping/instruct; priority/speed/tie integration and deterministic conflict order. |
| Accuracy locks and exceptions (36) | 15C/15D | Accuracy/crit query conditions for next-hit/next-crit guarantees, evasion identification, weather and gravity rules, Minimize bonuses, semi-invulnerable hit exceptions, always-hit behavior, and expiration/consumption. |
| Substitute, Transform, snapshots (6) | 15F | Battle overlays for decoy HP and interception, copied stats/types/moves/ability/form, power/guard/speed swaps, Shed-style transfer, PP policy, query order, and switch/faint/end cleanup. |
| Counter, revenge, stored damage (8) | 15G | Bounded damage/action memory by turn, hit, source, target, class, cause, connection, and faint; shared queries for counter/revenge/hit count/stored release/last failure; no event-log parsing. |
| Non-battle/post-battle behavior (4) | 15G | Explicit no-battle, presentation-only, reward/money, and overworld Core actions; post-battle consumer and events where meaningful; manual review prevents unsupported mechanics being hidden as no-ops. |
| Reference-data gaps (46) | 15H | Exact mechanics notes from approved authoritative sources; normalized generic definitions; ruleset/topology assignment; zero guesses and zero reference-blocked entries at exit. |

Cross-cutting work not represented by one legacy group is also mandatory:

- strict effect/condition/query/tag catalogs and validation;
- ordered per-target/per-hit `EffectContext` execution and damage-result reuse;
- deterministic `EffectTrace` with RNG/query/mutation/event linkage;
- explicit `gen4_like` and `modern_reference` policies;
- generic damage models and multi-hit wrappers, HP floors, spread reduction, and bypass filters;
- AI legality/scoring awareness without duplicating resolution;
- generated normalization/conformance registries and monotonic manifest statuses; and
- family goldens plus corpus fuzz/property/soak coverage.

### 5.8 Phase 15 workstreams and order

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

Ordered feature packages:

1. **Doubles controller state** - construct one- or two-slot topologies; map each active slot to a
   unique party member; expose slot-based state/actions without duplicating the singles controller.
2. **Per-slot action validation/execution** - accept one action per live slot, validate source/PP/
   switch/item/form legality independently, preserve stable order, and skip invalidated actions after
   earlier faints/switches.
3. **Full target materialization** - turn every `ResolvedTargetScope` into ordered active, party,
   side, field, or move-reference targets. Explicit selections must be validated against the authored
   shape; random targets draw exactly once after legality/redirection filters.
4. **Spread/per-target resolution** - fork an effect context per target; apply spread damage policy;
   preserve per-target hit, immunity, secondary, drain/recoil, faint, and event ordering.
5. **Redirection and position changes** - implement redirection filters/precedence, ally position
   swap, adjacency where required, and interaction with selected/random/spread targets.
6. **Faint replacement and switching** - request/validate replacements per empty slot, handle
   simultaneous faints deterministically, and apply slot/side entry effects once.

Required evidence: target-shape table tests in singles and doubles; malformed action/selection tests;
multi-target RNG/event golden; spread-reduction damage test; redirection conflict golden; simultaneous
faint/replacement golden; all existing singles goldens unchanged unless the spec intentionally changes
their event shape.

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

Progress (2026-07-10): active-party storage now uses `BattleActiveSlots`, a reusable stable-slot
to party-index mapping with duplicate-assignment guards. `BattleController` initializes the
singles slot assignments through that mapping and exposes slot-based active lookup while retaining
the existing singles action API and event stream. This is deliberately only the state migration:
doubles action collection, ordering, replacement, and multi-target execution remain open. Focused
slot, switch, and force-switch tests pass.

Progress (2026-07-10): action collection and turn ordering now form one reusable slot-based
package. `BattleTurnActions` requires exactly one action from every topology slot, rejects malformed
submissions, and normalizes actions to stable topology order. `BattleTurnOrder` orders every
scheduled action by priority and effective speed, with deterministic Fisher-Yates tie groups that
preserve the legacy two-way speed-tie RNG mapping. The existing singles controller now resolves
through this collection/order path, so it is protected by the ordinary battle regression suite.
The next 15B package is doubles action execution: controller construction/configuration for two
active slots, per-slot legality, selected/spread target resolution, and slot-aware events.

#### 15C — Query hooks and variable formulas

Primary groups: damage query modifiers (64), special accuracy (36), stat expansion (63).

- Complete base-power formula families: speed, weight, friendship, HP/status/item/terrain/weather,
  consecutive use, prior damage/action, party state, and stat comparison.
- Complete type/class/accuracy/crit/priority/turn-order/stat/effectiveness/final-damage queries.
- Add HP equalization, HP floors, percent-current/max HP, metric and inventory formulas.

Ordered feature packages:

1. **Unified query contract** - represent base/current/final values, source/target/field/ruleset,
   ordered modifiers, rounding point, and trace output for every query. Existing direct fields may
   remain as compatibility inputs only until normalized to this path.
2. **HP and status formulas** - finish HP bands, missing/current/max ratios, target/user status and
   status-specific secondary rules, HP matching/equalization, cannot-KO floors, and self-current-HP
   damage. Existing threshold/ratio/status power helpers are inputs, not completion of the family.
3. **Speed, weight, and metric formulas** - ratio/band tables, grounded/airborne metrics, modified
   weight, target height/size where referenced, exact caps, floors, and zero guards.
4. **History and consecutive formulas** - prior damage/action/failure, moved-first/last, consecutive
   use, hit count, times struck, ally faint history, turn number, retaliation, and reset rules.
5. **Party/resource/formula inputs** - living/fainted party counts, contributing party members,
   friendship, PP remaining, stat stages, positive-stage totals, stat comparisons, item presence/data,
   and deterministic random tables.
6. **Field/type/class queries** - weather/terrain power and type, environment type, move type/class
   override, offensive/defensive stat selection, unusual type effectiveness, STAB policy, and spread.
7. **Accuracy/crit/priority/final modifiers** - next-hit/next-crit guarantees, weather/gravity/
   semi-invulnerable exceptions, turn-order queries, damage floors/caps/multipliers, and healing query.

Every formula package requires table-driven boundary tests with exact intermediate rounding, compiler
validation for params/enums/ranges, resolver tests through the normal damage/heal path, and trace
assertions showing the base and final values. Never place a formula branch in `DamageCalc` when a
reusable query/formula helper can supply its input.

Progress (2026-07-10): reusable status-conditioned base-power handling is in place through
`statusPower`. It targets the user or active target, matches a specific or any persistent status, and
can suppress the physical burn penalty only when its authored condition matches. Compiler validation,
numeric rounding, target/user resolution, and burn interaction are covered by tests. The remaining
formula families and normalized per-move conformance definitions remain open.

Exit: formula families have table tests, exact rounding, compiler/validation coverage, and all
affected moves receive conformance cases.

#### 15D — Action timing, locks, and queued effects

Primary groups: turn timing/queued gates (64), volatile/status lockouts (49), move call/copy (13).

- Recharge, delayed attacks/heals, charge variants, semi-invulnerability, first/last-turn gates.
- Taunt, Encore, Disable, Torment, Imprison, Heal Block, throat/sound locks, perish/yawn/nightmare.
- Call/copy/repeat/force/replace moves and exclusion tags.
- One deterministic intent queue; no move-specific flags.

Ordered feature packages:

1. General queue entries with timing point, due turn, owner slot/creature/side, target snapshot or
   live-target policy, payload, insertion order, and explicit switch/faint/battle-end cleanup.
2. Recharge, first-action, cannot-repeat, prior-failure, moved-before/after, target-action-category,
   and interrupt-if-hit gates through normal action validation.
3. Charge/release actions, semi-invulnerable state, skipped-charge policies, moves that can hit each
   semi-invulnerable state, and power/effect exceptions.
4. Delayed slot damage/healing/status and replacement effects with stored source/type/class/ruleset
   metadata and deterministic target-slot behavior.
5. Forced repeats, multi-turn locks, ramping sequences, post-lock confusion/cleanup, and cancellation.
6. Disable/Encore/Taunt/Torment/Imprison/heal/sound/item locks plus Struggle-like legal-action
   fallback; all selection and execution checks share the same legality service.
7. Move-reference execution and order manipulation through one resolved action path with recursion/
   loop limits.

Required evidence: queue ordering and serialization/debug snapshots; seeded RNG-order tests; every
gate proves no unintended PP/RNG spend; switch/faint cancellation matrix; delayed-action and charge
goldens; recursion/termination tests for forced/called moves.

Exit: timing-sensitive goldens pin selection, execution, duration, failure, RNG, and cleanup order.

Progress (2026-07-10): the first complete generic move-gate handler family is implemented out of
the written workstream order at the user's direction to prioritize complete data-driven move
handlers. `moveGate` covers first-action and not-previous-move legality before PP/RNG/effects;
`queueActionGate` queues a deterministic future action skip that blocks all action kinds. Compiler
validation, Core events, resolver behavior, and focused tests are complete. Audit rows remain
uncertified until normalized numeric definitions and per-move conformance vectors are added.

#### 15E — Field, side, slot, and condition completion

Primary groups: field/side conditions (53), cleanup (5), protect variants (8).

- Weather, terrain, rooms, gravity, screens, Safeguard/Mist/Tailwind, side protection.
- All entry hazards, removal, absorption, transfer, and side swap.
- Substitute and protect-family filters/contact consequences.
- Slot conditions for future attacks, Wish, replacement healing, and delayed effects.

Ordered feature packages:

1. Generic `ConditionDef`/instance/store support for creature, side, slot, field, weather, terrain,
   and room scopes, including source, owner, duration, counters, stacking/replacement, tags, and
   switch/faint/battle-end policy.
2. One deterministic hook collector/order for action selection, targeting, priority, accuracy,
   try-hit, damage/healing/status queries, contact, switch, and end turn.
3. Weather/terrain/room/gravity/sport definitions and their required damage, accuracy, status,
   healing, type, grounded, and turn-order hooks.
4. Screens, Safeguard, Mist, Tailwind, Lucky Chant, pledge effects, and side protection filters.
5. Generic entry-hazard damage/status/stage conditions, layers, grounded checks, absorption,
   removal, and switch-in order.
6. Protect-family definitions with chain state, filter/bypass rules, side variants, and reusable
   contact-block effect lists.
7. Remove/transfer/swap operations by scope, tag, source, and owner; no cleanup helper may enumerate
   named content presets.

Required evidence: condition lifecycle matrix; hook-order goldens; duration/refresh/stack tests;
weather/terrain/room interaction tables; side/slot ownership tests; hazard switch-in and cleanup
goldens; protect contact/bypass matrix; hook-loop termination guard.

Exit: condition definitions declare scope, hooks, duration, stacking, source, removal, events, and
AI-visible semantics; no condition behavior branches on a move ID.

#### 15F — Item, ability, type, form, and snapshot mutation

Primary groups: held item/berry mutation (19), ability mutation (8), type mutation (16), snapshot
overlays (6).

- Complete reusable mutation primitives required by moves.
- Finish Transform/substitute/form/type overlay and revert behavior.
- Complete once-per-battle action wrappers required by special corpus moves.
- Preserve held-item, ability, PP, form, and switch invariants.

Ordered feature packages:

1. Central effective item/ability/type/stat/move queries over immutable base definitions plus battle
   overlays; document overlay precedence before adding mutations.
2. Item helpers for requirement, consume, transfer, removal, destruction, restoration, suppression,
   consumed-item history, item-derived effects/type/power, and explicit failure reasons.
3. Ability helpers for copy/swap/replace/suppress/ignore/protect and hook-dispatch integration.
4. Creature and move type overlays for add/remove/replace/copy, source/duration, grounding, STAB,
   effectiveness, and switch cleanup.
5. Stage/derived-stat helpers for set/max/average/split/swap/pass plus creature metric overlays.
6. Substitute/decoy, Transform/copy-snapshot, temporary move replacement, form/gimmick wrappers,
   PP ownership, HP-ratio preservation, and reversion.
7. Move-reference selector/executor for known, target, last, party, random, or environment-selected
   moves, with exclusions, recursion guards, and temporary/permanent mutation boundaries.

Required evidence: mutation legality/event tests; switch/faint/end reversion matrices; immutable
definition regression tests; hook lookup after ability/item changes; type/STAB/effectiveness tests;
substitute interception matrix; Transform/PP/form goldens; deterministic move-pool selection.

Exit: mutation and reversion matrices pass across switch, faint, form, item consumption, suppression,
and battle end.

#### 15G — Switching, recovery, counters, rewards, and overworld move effects

Primary groups: switch/state passing (8), healing/cures/HP costs (29), counter/stored damage (8),
non-battle/post-battle effects (4).

- Pivot/forced switch/Baton-style transfer and trapping.
- Party cures, revival, sacrifice, drain/recoil/crash, delayed and formula healing.
- Counter/revenge/Bide/stored damage using shared damage memory.
- Reward and overworld operations required by move semantics use explicit Core actions.

Ordered feature packages:

1. One switch-intent service for forced switch, pivot, self switch, escape, trapping, replacement,
   slot selection, and passable-state transfer.
2. One bounded battle-memory service recording action attempts and per-hit damage by turn, source,
   target, class, cause, connection, amount, and faint result.
3. Counter/revenge/stored-release/final-current-HP/hit-count operations querying that memory; normal
   failure returns events/results rather than exceptions.
4. Complete healing/cost/cure/status-transfer/revival/equalization helpers over active, ally, party,
   fainted-party, side, and slot targets.
5. Delayed and replacement-slot healing/cure effects integrated with the shared queue/condition path.
6. Explicit post-battle reward/money operations and content-agnostic overworld action requests for
   move field effects; presentation and map mutation remain consumers of Core results.

Required evidence: creature/HP/item conservation tests; switch transfer/cleanup goldens; counter
qualification tables; bounded-memory/turn-aging tests; party/fainted-target validation; reward and
overworld action tests; manual review of every no-battle/post-battle marker.

Exit: every affected move has state-conservation and event-order coverage; UI remains a consumer.

Progress (2026-07-10): HP-mutation handling is expanded through reusable operations. `heal` now
declares a self or target recipient, and `hpFraction` supports deterministic current/max-HP healing
or damage through the existing `Heal` and `Sap` primitives. Compiler validation and resolver/event
tests cover both paths. Team/party healing, cures, revival, HP equalization, switch-linked recovery,
and the normalized per-move conformance definitions remain open.

#### 15H — Reference-gap closure and full normalization

Primary group: 46 moves with insufficient local English effect data, plus any new gaps found by
the strict harness.

- Research authoritative mechanics sources and record concise source notes outside shipped content.
- Normalize every move into generic operations/presets.
- Manually review every use of `noBattleEffect`, `postBattleReward`, unsupported tags, and ruleset
  override.
- Zero silent guesses.

For each blocked reference key, record: source hash, ambiguous requirement, approved source citation,
chosen ruleset behavior, normalized primitive graph, reviewer, and tests. Reference research may add
concise mechanics notes but never copied prose or shipped official content. After enrichment, route the
entry back to the owning 15B-15G capability package; do not implement code inside the research package.

Normalization itself is a deliverable: every entry must have a deterministic generic definition hash,
mechanic-family tags, topology, ruleset, strict validation result, compiler test ID, resolver/golden
test IDs, and monotonic manifest status. Normalization aliases/presets are data expansion only.

Exit: 937 normalized definitions; zero unknown/unmapped/reference-blocked rows.

#### 15I — AI and simulation awareness

- AI legality uses the same target/topology resolver.
- Scoring understands every effect family at a conservative useful level or explicitly values the
  shared primitive outcomes; it never duplicates resolution.
- Smart AI supports doubles legal actions when a doubles battle is configured.
- Re-run seeded determinism, side-balance, and difficulty measurements after mechanics stabilize.

Implementation packages:

1. Use the completed target resolver to enumerate legal singles/doubles moves, switches, items,
   forms, and selections; AI never constructs targets independently.
2. Map every primitive/query/condition to conservative reusable scoring signals (expected HP swing,
   status/control, setup, field/side value, switch tempo, delayed value, self-cost, failure risk).
3. Simulate through the real resolver or pure query helpers; do not duplicate formulas or inspect the
   player's selected action.
4. Extend memory and score-table explanations for new conditions/queues/mutations.
5. Add seeded corpus smoke simulations and retune only after legality/crash coverage is complete.

Required evidence: every certified move yields at least one legal AI evaluation; no unsupported-op
switch defaults silently to zero; doubles target legality tests; deterministic score tables; seeded
side-balance and Smart-vs-Basic results logged with teams/seeds.

Exit: AI never selects illegal actions, never crashes on a certified move, and remains deterministic.
Perfect competitive play is not required.

#### 15J — Certification and closeout

- Run all 937 per-move conformance cases.
- Run family unit tests, formula tables, schema/validation tests, and cross-system goldens.
- Run seeded singles and doubles fuzz/soak battles using the entire normalized move set.
- Review Core for move-name/ID branches and unsupported fallbacks.
- Freeze the Core public battle contract for Phase 16 integration.

Closeout packages:

1. Regenerate the manifest and all derived counts from source/normalization/test registries.
2. Run one compile/resolve conformance vector per entry plus all declared failure/alternate contexts.
3. Run family goldens and seeded singles/doubles corpus fuzz/soak with replay-on-failure artifacts.
4. Static-scan Core source and public types for move names/IDs, named preset handlers, unsupported
   fallbacks, direct definition mutation, nondeterministic APIs, and unbounded hook/queue recursion.
5. Reconcile every spec/code/schema/test mismatch and run a focused `cgm-review-pass` GO review.
6. Record the frozen public action/state/event/trace contract consumed by Runtime and Creator.

No closeout checkbox may be marked from a hand-edited count or a representative subset.

Exit: every Phase 15 criterion below passes and the closeout review is logged.

### 5.9 Mandatory testing layers

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

### 5.10 Phase 15 exit gate

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

### 5.11 Phase 15 exclusions

- No bespoke move implementations.
- No final Runtime renderer or Creator UI work except headless test harnesses/tools.
- No breeding, multiplayer/netcode, official asset packaging, marketplace, or scripting language.
- No importing/shipping the official corpus as a playable pack.
- No mechanics added solely because they are imaginable; each primitive must be required by at
  least one corpus entry or an already-owned Core rule.

---

## 6. Phase 16 — Reusable Runtime Engine Completion

Goal: turn the completed Core into a content-agnostic playable engine.

Prerequisite: Phase 15 is GO and the Core action/state/event/trace contract is frozen. Runtime may
adapt presentation and IO around that contract; it may not add or reinterpret game rules.

Ordered work packages:

1. **16A - Host and data parity.** Remove demo IDs and built-in battle assumptions; make raw project
   and packed `GameDb` boot paths behaviorally equivalent; fail clearly on missing/incompatible data.
2. **16B - Frame and rendering foundation.** Wire the existing fixed-step clock correctly; render
   interpolation only; implement the named `IRenderer` seam, GL 3.3 sprite batch, texture/atlas loader,
   virtual resolution, letterboxing, camera, tile chunks, animation, and resource disposal.
3. **16C - Reusable UI and scene flow.** Boot/Title/Overworld/Battle/Menu scene transitions plus
   9-slice panels, bitmap text/typewriter, cursor/list/grid navigation, HP/resource bars, prompts,
   transitions, and input rebinding/controller mapping.
4. **16D - Overworld integration.** Asset-backed maps; grid movement/collision/ledges/warps;
   encounters; NPC movement/dialogue; trainers; pickups; centers/marts/PCs; flags and blackout flow.
5. **16E - Player systems.** Party, bag, storage, shops/money, capture/progression/evolution, options,
   save/continue/backup, clock/day-night, audio streaming/SFX, and debug overlays using Core rules.
6. **16F - Battle presentation.** Singles/doubles action selection, target selection, replacement,
   items/forms/switching, and event-driven animation/text. Presentation consumes Core events and never
   infers mechanics from state differences.
7. **16G - Runtime verification.** Headless input replay, raw/pack parity, save/reload, resource-leak
   checks, fixed-step catch-up tests, scene-transition tests, and a playable original fixture loop.

Each package updates `ENGINE_RUNTIME_SPEC.md` before implementation and adds Runtime integration tests.
Do not add Creator workflows, export packaging, original demo breadth, or new Core mechanics here;
discovering a missing rule reopens Phase 15 through change control.

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

Prerequisite: Phase 16 exposes stable Runtime launch/debug contracts and Phase 15 catalogs are frozen.
Creator writes project data through Core schemas/validation and launches Runtime out of process.

Ordered work packages:

1. **17A - Lifecycle and shared editor infrastructure.** Complete new/open/recent/save/close,
   unsaved guards, document ownership, usage search, reference picker, safe delete, validation
   navigation, undo/redo consistency, recovery/autosave, and large-project virtualization.
2. **17B - Asset authoring.** Asset browser/import/reimport, slicer canvas with manual/common/gutter/
   connected-component workflows, animation grouping/preview, audio metadata, atlas diagnostics, and
   missing/orphan usage validation.
3. **17C - World authoring.** Tileset/object editors; chunked map canvas; tile/collision/encounter/
   trigger overlays; brush/rect/bucket/eyedropper/erase; entities, warps, NPC paths, trainers, shops,
   pickups, dialogue, flags, and play-from-map launch.
4. **17D - Data and mechanic authoring.** Complete species/forms/evolution, moves, effects,
   conditions, rulesets, items/held effects, abilities/hooks, trainers/AI, encounters, inventory,
   storage, project settings, and save/start configuration editors.
5. **17E - Catalog-driven effect editor.** Generate available ops, params, enums, ranges, targets,
   timing, topology/ruleset requirements, help, and validation from the frozen Core catalogs. The UI
   must not maintain a second handwritten mechanics list.
6. **17F - Playtest and export workflows.** Play, play-from-map, focused battle sandbox, validation
   gate, process logs/crash results, and export configuration all launch the real Runtime/Tools paths.
7. **17G - Creator verification.** Headless ViewModel tests, undo/redo/dirty matrices, keyboard and
   accessibility pass, malformed-project recovery, performance budgets, and a no-JSON authoring trial.

Every editor follows `CREATOR_APP_SPEC.md`'s pathfinder pattern; every mutation is undoable; views stay
thin. Do not implement game simulation, alternate schema models, production demo content, or installer
work in Creator.

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

Prerequisite: Phases 15-17 are GO. Phase 18 integrates and proves existing contracts; mechanic/editor/
runtime gaps found here return to their owning phase rather than receiving demo-only workarounds.

Ordered work packages:

1. **18A - Original vertical-slice design.** Freeze an original content brief and acceptance script:
   at least 10 species, 30 mechanics-representative moves, three maps, encounters, trainers, center,
   mart, storage, gym/badge gate, evolution, day/night, audio, and a doubles encounter.
2. **18B - Author entirely through Creator.** Build all data/assets through normal editor workflows;
   every manual JSON edit is a Creator defect unless explicitly a fixture-maintenance operation.
3. **18C - Production asset/pack path.** Pack data, atlases, audio, and metadata; verify hashes and raw/
   packed parity; produce CI-built self-contained debug/release runtime templates.
4. **18D - Export product flow.** Creator export UI, executable name/icon/version/config, validation,
   distinct smoke failure codes, safe overwrite behavior, and export-folder completeness checks.
5. **18E - End-to-end proof.** Deterministic new-game-to-badge input replay, save/relaunch/continue,
   clean Windows VM run without SDK/tools, and stranger playtests for both authoring and playing.
6. **18F - Integration closeout.** Fix ownership-boundary defects, document performance, and preserve
   the original fixture as the standing regression project without shipping reference-corpus content.

Do not add installer/update infrastructure, plugin APIs, localization, multiplayer, or official
content. A demo-specific branch in Core/Runtime/Creator is a Phase 18 blocker.

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

Prerequisite: Phase 18 passes its clean-machine and stranger gates with no data-loss defects.

Ordered work packages:

1. **19A - Versioning and migration.** Freeze project/save/pack/runtime compatibility policy; test
   every released schema/save/pack migration path; preserve backups and safe refusal of newer data.
2. **19B - Reliability and security.** Malformed-project/pack/save fuzzing, crash diagnostics,
   recovery, path traversal/export overwrite checks, stress projects, performance/memory/startup gates,
   and deterministic failure artifacts.
3. **19C - Distribution.** Decide zip/installer/update channel, sign or document unsigned behavior,
   create release CI, verify self-contained artifacts, licenses, notices, and clean-machine installs.
4. **19D - Documentation.** Tutorial, user manual, Creator workflows, effect/condition/query reference,
   Runtime/export guide, migration guide, troubleshooting, sample license, and legal/IP sweep.
5. **19E - Beta.** External author/player cohort, issue triage, migration rehearsal, accessibility and
   hardware coverage, no open data-loss/FIX-NOW defects, and release-candidate soak.
6. **19F - 1.0 release.** Semver/changelog, immutable release artifacts/hashes, tagged Runtime template,
   rollback plan, and post-release support policy.

No 1.0 gate is satisfied by unit tests alone; distribution, clean-machine, migration, documentation,
and external-user evidence are required.

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

Always take the first incomplete item whose prerequisites are satisfied:

1. Finish Phase 15B doubles controller construction, per-slot action legality/execution, and
   selected/spread target materialization over the existing topology/action foundations.
2. Add the concrete per-target effect context and deterministic trace needed to prove a complete
   multi-target action; commit one singles+doubles family golden.
3. Normalize and certify the first target/topology cohort end to end; regenerate manifest statuses
   and test IDs through tooling. Do not hand-edit counts.
4. Finish the remaining target shapes, random/redirection/position policies, simultaneous faint
   replacement, and target-only certification cohort before declaring 15B complete.
5. Continue 15C as complete formula families in this order: HP/status; speed/weight/metrics;
   history/consecutive; party/resource/random; field/type/class; accuracy/crit/final modifiers.
6. Then proceed through 15D-15H in written order, except that a blocking prerequisite may be built
   in the package that first requires it. Record the dependency rather than silently jumping scope.
7. Run 15I only after primitive semantics stabilize, then 15J closeout. Do not begin Phase 16 until
   every Phase 15 exit checkbox is generated or evidenced and the review verdict is GO.

## 11. Change control

Changing Phase 15's corpus, correctness target, or zero-unsupported exit gate requires an explicit
user decision and updates to this file, `SCOPE_GUARD.md`, and the owning battle/testing specs in the
same change. Architecture changes require an ADR. New dependencies require TECH_STACK.md and user
approval.

## 12. Autonomous implementation handoff prompt

The user may hand the repository to another model with this prompt:

> Continue Creature Game Maker from the authoritative roadmap. Read `/AGENTS.md`,
> `docs/SCOPE_GUARD.md`, `docs/IMPLEMENTATION_PLAN.md`, `docs/ARCHITECTURE_ADDENDUM.md`,
> `docs/MASTER_PLAN.md`, `docs/AGENTS.md`, and the owning specs completely before acting. Current
> work is Phase 15. Take the first eligible feature package from IMPLEMENTATION_PLAN section 10 and
> complete the whole reusable behavior family. Do not implement a named move, move-ID branch,
> one-off handler, arbitrary script op, UI, or future-phase feature. Use the promotion ladder in
> section 5.7; update the battle spec before code; add strict validation, typed compilation, normal
> resolver behavior, events/traces, cleanup, AI visibility, and every applicable test from
> TESTING_STRATEGY. Per-move certification requires normalized definitions and conformance vectors;
> never hand-edit counts or treat representative tests as certification. Run the focused tests,
> `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx`, and
> `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build`. Fix change-caused failures, update
> the package progress record and immediate queue, review for scope/spec/determinism/schema drift,
> commit the complete change, and stop only if complete or genuinely blocked. Do not advance to
> Phase 16 until every Phase 15 exit gate is evidenced and the focused review is GO.

The implementing model should report: package outcome, reusable behavior added, audit groups/reference
keys affected, files/specs changed, tests and counts, RNG/events/trace implications, manifest status
delta, remaining first eligible package, blockers/deviations, and commit hash.
