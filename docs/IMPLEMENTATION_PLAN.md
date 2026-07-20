# IMPLEMENTATION_PLAN — Rebased Development Lifecycle

Version 4.0 — 2026-07-11

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

## 2.1 Roadmap execution law — no foreseeable specification stalls

This plan is an executable work contract, not a list of aspirations. Every package from the current
state through 1.0 has an owner, prerequisites, locked defaults, ordered implementation work,
required evidence, and an exit condition. The owning spec remains the authority for detailed
behavior, but a package may not be queued unless this plan either points to a completed spec section
or supplies the exact decisions the agent must first copy/reconcile into that spec.

### Package states

- **PLANNED:** scope, dependencies, outputs, acceptance, and decision authority are written here.
- **SPEC READY:** the owning spec contains all mechanically relevant rules and no unresolved marker.
- **IN PROGRESS:** implementation has at least one normal-path, tested checkpoint, but the package
  exit is unmet. Resume it before selecting another package.
- **IMPLEMENTED:** normal production paths and automated tests satisfy the spec.
- **VERIFIED:** package integration/manual/performance/accessibility/security evidence also passes.

An autonomous iteration always selects the first package whose prerequisites are complete. When it
is `PLANNED` but not `SPEC READY`, the iteration's first deliverable is the named spec-lock section.
The exact defaults in this plan are already user-authorized by the 2026-07-11 roadmap directive;
copying them into the owning spec does **not** require another confirmation. Code begins only after
that reconciliation is complete and reviewed in the same iteration or a preceding documentation
commit.

### Decisions an implementing agent owns

Without asking the user, an agent must decide and document ordinary implementation details using
this order:

1. binding ADR/addendum decision;
2. locked default or acceptance rule in this plan;
3. owning-spec rule;
4. existing pathfinder/helper and current public contract;
5. BCL/native platform or already-approved dependency behavior;
6. smallest deterministic, data-driven rule that satisfies all cited requirements.

For battle-reference ambiguity, research and cite the best available mechanics evidence, select the
applicable `gen4_like` or `modern_reference` behavior, and record the decision in the ruleset/formula/
condition registry with neutral tests. If sources genuinely conflict and neither profile dictates an
answer, use the latest behavior supported by the locked corpus as `modern_reference`; preserve the
classic project behavior as `gen4_like` when it already exists. This is an explicit project rule,
not permission for a move-ID branch or silent guess.

Only these conditions may block for user input:

- adding/replacing a dependency or game/framework technology;
- expanding scope into a permanent non-goal or a different product/platform;
- an irreversible migration or intentional loss of supported project/save data;
- a paid service, signing certificate, store account, external credential, or legal policy only the
  owner can provide; or
- two product behaviors that remain materially different after applying every authority above and
  neither is covered by a locked default below.

Missing prose, an incomplete spec, a choice of private type/file layout, a test-fixture design, a
UI microcopy choice, or an implementation technique is not a user blocker. Finish the named spec
contract, use the locked default, and proceed. If an external/manual gate cannot run locally, finish
all automatable work, create the exact reproducible checklist/artifact, mark only that gate pending,
and take the next independent package; do not label the entire phase blocked.

### Universal package completion record

Every package update records in this file:

- state transition and date;
- prerequisites verified and owning spec sections changed;
- production files and reusable behavior delivered;
- schema/migration or dependency impact (`none` when none);
- focused and full test commands with pass/fail counts;
- deterministic/golden/performance/accessibility/security evidence as applicable;
- manual/external evidence completed or the exact pending checklist;
- deviations, review findings, and their dispositions;
- next eligible package; and
- commit hash in the handoff report.

No package advances from representative tests alone when its exit calls for a complete registry,
matrix, corpus, workflow, or manual proof.

## 3. Audited baseline

Roadmap baseline commit: `b69bcb2` (`Lock Phase 15 doubles execution roadmap`).

- Build: 0 warnings, 0 errors.
- Tests: 979 passed — 847 Core, 104 Creator, 21 Runtime, 7 Tools.
- Last measured coverage at earlier baseline `5bc0e1a`: Core 96.04% line / 90.30% branch; Creator
  package 66.75% / 66.24%; Runtime package 48.45% / 36.64%. Coverage was not remeasured for this
  documentation baseline and is not used as current completion evidence.
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
- Certified Phase 15 move coverage: **118/937** from the generated 15B target/topology, 15C-2/3/4/5
  formula, 15D-2 action-gate/recharge, 15D-3 charge/semi-invulnerability, and 15D-4 delayed-action
  cohorts; the earlier
  0/937 value remains the locked Phase
  15A baseline.

Roadmap audit (2026-07-11): the prior plan fully locked 15B but still left 15C-15I and Phases 16-19
as capability summaries or “spec later” outlines. Version 4 replaces those with package IDs,
prerequisite order, authorized specification locks, deterministic/default decisions, acceptance
evidence, performance/manual gates, and reserved-decision rules. This is a documentation/governance
change only; it does not advance any implementation or certification state.
Verification for the v4 documentation change: solution build passed with 0 warnings/errors; full
suite passed 979 tests (847 Core, 104 Creator, 21 Runtime, 7 Tools); relative documentation links and
whitespace checks passed.

## 4. Rebased phase status

| Phase | Name | Status | Honest current position |
|---:|---|---|---|
| 0 | Product Architecture and Governance | VERIFIED | Stack, repository rules, ADRs, and product split exist |
| 1 | Toolchain and Solution Foundation | VERIFIED | .NET 10 solution, CI build/test, Creator and Runtime hosts |
| 2 | Schema, Serialization, and Validation Foundation | VERIFIED | Schema foundation, migrations, loaders, IDs, validators; current project schema is v7 after the additive Phase 15E-3 grounded-query hook vocabulary |
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
| **15** | **Complete Core Game Logic and Move Conformance** | **IN PROGRESS** | **15A, 15B, 15C-1/2/3/4/5/6/7, 15D-1/2/3/4/5/6/7, 15E-1/2/3/4/5/6/7, 15F-1/2/3/4/5/6/7, 15G-1, 15G-2, and 15G-3 complete; 937 inventoried, 175/937 certified; 15G-4 (healing/costs/cures/transfer/revival/HP-equalization) is the next package** |
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
of these requirements in one or more coherent mergeable changes:

1. One or more audit rows prove the behavior is required.
2. Existing helpers, BCL features, and current primitives were checked first.
3. The owning spec defines the generic operation/query/condition, params, timing, target scope,
   failure behavior, RNG order, rounding, events, cleanup, and AI-visible outcome.
4. Serialized data validates strictly and compiles to typed reusable runtime data.
5. The normal resolver path executes it without a move-name/ID branch.
6. Pure math, validation/compiler, resolver/event, boundary, and deterministic tests pass as
   applicable.
7. Every affected normalized move has a conformance vector before any certification count rises.
8. Every checkpoint is green; the full solution builds and tests at closeout; this plan records
   evidence and checkpoint/final commit hashes.

If a package exposes a required foundation such as per-target effect contexts, build that
foundation as part of the package. Do not land an unused abstraction and call the feature done.

#### Package continuity across model turns

A roadmap iteration is the **package objective**, not one model response, context window, or CLI
invocation. A substantial package may span multiple turns and multiple coherent commits while
remaining the single active package. At the start of every continuation, resume an `IN PROGRESS`
package before consulting the next queue item.

When a package cannot finish in the current model turn:

1. Implement the next smallest vertical checkpoint that is exercised by the normal production
   path; do not land unused types, parallel resolvers, or test-only scaffolding.
2. Add the checkpoint's focused tests, run the focused suite, and keep the repository buildable.
3. Prefer continuing in the same CLI session. If a context/time boundary forces a handoff, commit
   only a green coherent checkpoint, mark the package `IN PROGRESS`, and record completed acceptance
   rows plus the exact next code path/test to implement.
4. Resume the same package with `codex resume --last` (interactive) or the equivalent saved-session
   continuation. Do not begin the next package and do not advance manifest certification.
5. Run the full build/test/review and mark `IMPLEMENTED` only after every package acceptance row is
   satisfied.

“The refactor is substantial,” “this will not fit in one turn,” context pressure, or an estimate of
remaining work is not a blocker and not a valid zero-change terminal response. If the spec is
sufficient and the task is in scope, the agent must start a coherent checkpoint. A response with no
file changes is compliant only for a requested review or a demonstrated real blocker under §2.1.
Internal checkpoints do not become new roadmap slices; the full feature package remains the scope.

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

- package name and status (`COMPLETE`, `IN PROGRESS`, or `BLOCKED`);
- audit capability groups and exact reference keys affected;
- spec sections changed;
- reusable helpers/ops/conditions added or reused;
- validation/compiler/resolver paths covered;
- tests and commands run, including pass counts;
- RNG draw and event/trace changes, or `none`;
- manifest status changes generated by tooling, or `none` with the reason;
- remaining gaps and the next eligible package; and
- checkpoint commit hashes when `IN PROGRESS`, and final package commit hash when complete.

`IN PROGRESS` never advances corpus counts. `BLOCKED` names the missing decision/reference/capability.
Do not use “engine supports this” as a synonym for “move certified.”

#### Package admission and specification readiness

The roadmap previously put implementation-shaped work into the immediate queue while its owning
spec still described that work as deferred. That is a planning defect. From this revision onward,
every package has two independent states:

- **SPEC READY** means every applicable row in `BATTLE_SYSTEM_SPEC.md`'s feature-package gate has a
  mechanical answer and the tests that will prove it are named.
- **IMPLEMENTED** means that locked contract is present in normal Core paths and its required tests
  pass.

Only a `SPEC READY` package may enter the code portion of an iteration. A `PLANNED — SPEC LOCK
AUTHORIZED` package may enter for its required spec reconciliation and becomes code-eligible only
after that lock is complete. A goal, capability list, or prose description is not specification
readiness. If a later package needs a new decision, its first
roadmap item is a specification-lock package; implementation does not start in the same commit unless
the completed spec leaves no unresolved behavior and the change remains reviewable.

Current readiness ledger:

| Package | Spec state | Implementation state | Authority / next proof |
|---|---|---|---|
| 15B-1 topology and action foundations | SPEC READY | IMPLEMENTED | Battle spec target foundation; existing topology/action tests |
| 15B-2 doubles controller and per-slot actions | SPEC READY | IMPLEMENTED | Commit `aded927`; battle admission tests and progress record |
| 15B-3 typed selections and target materialization | SPEC READY | IMPLEMENTED | Commit `ea5a32f`; live materialization tests and progress record |
| 15B-4 spread and per-target execution | SPEC READY | IMPLEMENTED | Closeout review passed; singles/doubles resolver, trace, and family-golden evidence |
| 15B-5 redirection and position | SPEC READY | IMPLEMENTED | Position-swap and redirection acceptance matrix, deterministic trace, and closeout review passed |
| 15B-6 faint outcome and replacement | SPEC READY | IMPLEMENTED | Draw-capable outcome, atomic replacement loop, slot-addressed entry hooks, and replacement golden |
| 15C-1 unified query pipeline | SPEC READY | IMPLEMENTED | Exact integer/fraction service, modifier order/clamps, controller/AI consumers, and query traces |
| 15C-2 HP and status formulas | SPEC READY | IMPLEMENTED | Boundary/compiler/resolver/trace/AI matrices and 18 generated neutral formula vectors |
| 15C-3 speed and physical-metric formulas | SPEC READY | IMPLEMENTED | Exact speed/metric bands, schema v5 metrics, effective overlays, resolver/AI parity, and 2 generated certifications |
| 15C-4 action-history formulas | SPEC READY | IMPLEMENTED | Bounded typed attempts/streaks, resolver/AI parity, replacement-faint aging, and 4 generated certifications |
| 15C-5 party/resource formula families | SPEC READY | IMPLEMENTED | Exact filters/PP/stages/friendship/item/random tables, resolver/AI parity, trace/RNG evidence, and 6 generated certifications |
| 15C-6 field/type/class/stat/effectiveness queries | SPEC READY | IMPLEMENTED | Shared exact identity/damage query result, overlays/conditions, selectors, STAB/effectiveness profiles, spread snapshots, resolver/AI parity, trace, and golden evidence |
| 15C-7 accuracy/critical/priority/final-damage/healing queries | SPEC READY | IMPLEMENTED | Shared action-query helpers, strict ops, one-shot conditions, resolver/Smart-AI parity, traces, RNG matrix, doubles isolation, and golden evidence |
| 15D timing/queue/lock families | 15D-1/2/3/4/5/6 SPEC READY; 15D-7 PLANNED — SPEC LOCK AUTHORIZED | 15D-1/2/3/4/5/6 IMPLEMENTED; 15D-7 NOT ACTIVE | Typed intent queue, action gates, recharge, charge release, semi-invulnerability, delayed slot actions, multi-turn locks, condition-backed selection legality, ruleset fallback, and AI parity use shared deterministic paths; apply 15D-7 lifecycle defaults |
| 15E scoped conditions/hooks | 15E-1/2/3/4/5/6/7 SPEC READY | 15E-1/2/3/4/5/6/7 IMPLEMENTED | Workstream complete; retain focused regression coverage while later packages consume the shared conditions |
| 15F mutation/snapshots | 15F-1/2/3/4/5/6/7 SPEC LOCKED | 15F-1/2/3/4/5/6/7 IMPLEMENTED | Workstream complete: overlays, item/ability/creature-type/stat mutation, decoy/Transform/Mimic snapshots, and the unified move selector/executor over effective move lists |
| 15G switch/recovery/memory/non-battle | 15G-1/2/3 COMPLETE; 15G-4+ PLANNED — SPEC LOCK AUTHORIZED | 15G-1/2/3 IMPLEMENTED; 15G-4+ NOT ACTIVE | Switch intents (ADR-012), bounded action/damage memory, and the damage-memory consumers (Counter/Mirror Coat/revenge/Bide, last-hit + doubles source-addressed) are complete; healing/recovery lands in 15G-4 |
| 15H reference closure/normalization | PROCESS READY | NOT COMPLETE | Per-entry research record and routing contract below; capability implementation remains with 15B-15G |
| 15I AI awareness | PLANNED — SPEC LOCK AUTHORIZED | NOT IMPLEMENTED | Apply 15I-1 through 15I-5 legality/scoring/tuning defaults after mechanics stabilize |
| 15J certification/closeout | PROCESS READY | NOT COMPLETE | Generated manifest, registered tests, scans, fuzz/soak, and GO review |

For rows marked `PLANNED — SPEC LOCK AUTHORIZED`, the specification package is fully planned here so an
autonomous model has a concrete deliverable instead of discovering ambiguity during coding:

1. **15C-SPEC:** publish the exact query stage order; value types and fraction width; rounding/clamp
   rule at every stage; source/target/action-history inputs; one formula registry row per audited
   formula with params/defaults/ranges; RNG draw frequency for random tables; failure/fallback rules;
   and boundary vector IDs. Then mark only the locked formula family `SPEC READY`.
2. **15D-SPEC:** publish queue entry identity, owner and target snapshot policy, insertion/order,
   payload union, execution checkpoint, cancellation/transfer on switch/faint/end, PP and RNG rules,
   lock precedence, called-move recursion budget, and event/trace order. Provide a lifecycle matrix
   for every audited timing family before marking it ready.
3. **15E-SPEC:** publish condition instance fields, allowed scopes, hook precedence, duration tick
   point, refresh/replace/stack rules, source tracking, removal filters, bypass rules, loop guard,
   switch/faint/end cleanup, and event/AI output. Provide one hook/lifecycle table per condition
   family; content names may annotate evidence but never select code paths.
4. **15F-SPEC:** publish immutable-base/effective-overlay precedence for item, ability, types, stats,
   moves, form, substitute, and snapshots; mutation legality; PP ownership; copy depth; suppression;
   source/duration; switch/faint/end reversion; recursion limits; and events. Provide the complete
   mutation/reversion matrix before the first helper lands.
5. **15G-SPEC:** publish slot-addressed switch intents and transfer whitelist; trapping/escape and
   replacement failure; bounded damage/action-memory records and aging; healing/cost/revive/cure
   rounding and conservation; reward/post-battle ownership; overworld Core request payloads; and
   outcome/event ordering. Separate battle rules from Runtime presentation/map consumption.
6. **15I-SPEC:** publish legal-action enumeration as a consumer of Core resolvers, target-choice
   generation, expected-value inputs per primitive, unknown-op failure, delayed/field/switch value,
   simulation budget, deterministic tie order, memory visibility, and score-trace fields.

Each specification-lock package must cite the exact audit keys that require every rule, reconcile
generation differences through `RulesetProfile`, add or name neutral acceptance vectors, and update
this readiness ledger. `TBD`, “later resolver”, “as appropriate”, and an unnamed “generation rule”
are readiness failures.

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

1. **15B-2 — Doubles controller and action admission (`SPEC READY`).**
   - Add topology-aware construction that validates two unique, living party assignments per side;
     keep the current constructor as a singles adapter.
   - Make slot lookup authoritative and make every side-only active/action helper reject doubles.
   - Accept actions for occupied living slots, normalize through `BattleTurnActions`, capture each
     actor party index, and perform structural, individual, then collective validation without RNG or
     mutation.
   - Implement aggregate stock reservation, unique switch destinations, once-per-side form conflict,
     and doubles-capture rejection.
   - Execute switch, item, form, and move phases exactly as specified; use the shared speed/tie order
     and revalidate actor identity before mutation.
   - Migrate active-creature events to slot identity with singles compatibility constructors/
     properties. Do not create a parallel doubles event hierarchy.
   - **Acceptance:** constructor boundary table; malformed/collective action matrix; switch/item tie
     RNG vectors; actor-changed/fainted/resource invalidation tests; unchanged singles behavior.

2. **15B-3 — Typed selections and live target materialization (`SPEC READY`).**
   - Add the typed active-slot/party-member/move-reference selection union and validate selection kind,
     relationship, range, and faint requirements during admission.
   - Convert every `ResolvedTargetScope` into one active, party, side, field, or move-reference
     execution scope; never use an active target as a placeholder for a non-active scope.
   - Apply the execution-time invalidation table: slot-stable occupants, deterministic opponent
     fallback, no ally fallback, live spread filtering, and one random-opponent draw only for two or
     more candidates.
   - Spend PP and emit `MoveUsed` before an execution-time no-target failure; preserve zero spend for
     malformed admission, actor invalidation, and pre-move gates.
   - **Acceptance:** all 16 target shapes in singles and doubles; wrong selection-kind/side/range
     rejection; switch-occupant and fainted-target cases; 0/1/2 candidate RNG counts; PP/event order.

3. **15B-4 — Per-target action contexts and spread resolution (`SPEC READY`).**
   - Split the current side-only `EffectContext` into one action context plus ordered target contexts,
     carrying slot identity, per-hit/per-target damage, and action-total damage.
   - Run accuracy per target, standard hit-count once per action, critical/random damage per hit per
     target, then target effects, contact consequences, and action effects in the locked order.
   - Add the `3/4` spread modifier at the damage Targets stage only when at least two live active
     targets were snapshotted. Preserve full power with one target.
   - Record every performed/skipped draw and query/mutation/event link in `EffectTrace`; do not add a
     draw for deterministic or invalidated paths.
   - Make target-scoped damage consumers use target totals and action-scoped recoil/cost consumers use
     action totals. A mechanic needing per-target rounding receives a typed parameter in its owning
     package.
   - **Acceptance:** singles+two-target golden; one-target spread boundary; per-target miss/immunity/
     secondary tests; multi-hit target/hit order; source-faints-from-contact snapshot behavior;
     exact RNG trace and event sequence.

   Continuation checkpoints, in order, remain one 15B-4 package:

   1. Thread action and ordered target contexts through the existing normal resolver, migrate the
      current singles execution to them without changing outcomes, and pin singles compatibility.
   2. Resolve per-target accuracy/immunity/direct damage and the one-versus-two-target `3/4` spread
      boundary through that same resolver; add deterministic doubles damage tests.
   3. Complete shared hit-count, per-hit critical/random damage, target effects, contact consequences,
      per-target totals, and action-total drain/recoil/cost ordering; add the RNG/event matrices.
   4. Complete `EffectTrace`, singles+doubles family goldens, every remaining 15B-4 acceptance row,
      full regression, and package closeout. No checkpoint alone marks 15B-4 implemented or certifies
      a move cohort.

4. **15B-5 — Redirection and ally position changes (`SPEC READY`).**
   - Represent redirect eligibility as generic condition hooks with accepted/bypass move tags and an
     explicit priority; collect by priority, owner speed, then topology order without a tie draw.
   - Apply redirection only to one redirectable opposing active target and never to self/ally/spread/
     party/side/field/move scopes.
   - Implement atomic allied assignment swap. Creature-owned state follows the creature; slot-owned
     queued/condition state stays with the slot.
   - Re-run live target validation after redirection and trace rejected hooks and the winning hook.
   - **Acceptance:** competing redirector golden; accepted/bypassed class/tag table; original target
     empty/fainted; spread non-redirection; position swap state-ownership and subsequent-target tests.

5. **15B-6 — Faint outcome and replacement checkpoint (`SPEC READY`).**
   - Mark a fainted occupant unavailable to later actions without inserting a reserve mid-turn;
     invalidate that actor's captured action when reached.
   - Evaluate winner/draw only after a complete action or end-turn batch; add a draw-capable outcome
     and stop manufacturing a winner for simultaneous wipes.
   - Emit one `ReplacementRequested` per fillable empty slot, accept atomic unique party selections,
     apply them in topology order, and run each entry hook once.
   - Repeat replacement requests after entry-hook faints while reserves remain; allow an unfillable
     slot to stay empty and forbid the next ordinary turn while a fillable request is pending.
   - Route replacement through the same slot-addressed neutral switch helper used by voluntary
     switching. Leave Phase 15G transfer whitelists to 15G rather than copying state here.
   - **Acceptance:** one/both active faint, simultaneous side wipe/draw, reserves-versus-defeat,
     duplicate/invalid replacement choices, entry-hazard repeat, action invalidation, and event golden.

Package order is mandatory because every later package consumes the previous one's public Core
contract. A package may be split into reviewable commits only when each commit is used by the normal
resolver and green; scaffolding-only commits do not count. Required evidence is cumulative: the full
target-shape table, malformed action/selection matrix, multi-target RNG/event golden, spread damage
boundaries, redirection conflict golden, simultaneous faint/replacement golden, and all singles
goldens updated only for the intentional slot-aware event migration.

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
The next package is 15B-2: doubles controller construction, per-slot action admission/execution,
collective conflict handling, actor invalidation, and slot-aware events under the now-locked doubles
execution contract. Typed selection/materialization and spread execution follow as 15B-3 and 15B-4;
they are no longer combined into an ambiguous implementation slice.

Progress (2026-07-11): Phase 15B execution specification is now locked before further Core work.
It defines topology-aware construction, atomic action admission, actor identity, simultaneous switch/
item/form conflicts, typed selections, live-target invalidation and fallback, redirection precedence,
PP/RNG/hit/effect/event order, spread rounding, slot-aware events, draw outcomes, and replacement
checkpoints. Packages 15B-2 through 15B-6 now have separate entry/acceptance contracts. A phase-wide
readiness ledger also prevents 15C-15I implementation from entering the immediate queue before each
owning semantic contract is complete. Documentation verification: solution build succeeded with
0 warnings/errors; full suite passed 979 tests (847 Core, 104 Creator, 21 Runtime, 7 Tools).

Progress (2026-07-11): **15B-2 COMPLETE.** The controller now has topology-aware construction
with unique living start assignments and a slot-addressed turn API. Side-only active/action APIs
fail in doubles. Admission is atomic: it normalizes `BattleTurnActions`, previews queue gates,
validates individual actions, then rejects collective duplicate reserves, overdrawn item stock,
simultaneous temporary forms, and doubles capture before state, events, PP, stock, queues, or RNG
change. Doubles execution resolves switch, item, form, and move-scheduling phases in the locked
order; switches/items use the shared speed/tie shuffle, forms precede move scheduling, and each
submitted action captures its source slot and actor party index. Actor/resource/target invalidation
events and slot-aware move/switch/form/item events provide the new identity seam while existing
singles constructors/properties remain compatible. Live target materialization, PP/`MoveUsed`
execution boundaries, and target effects are deliberately not implemented here; they are 15B-3/4.

Evidence: `BattleDoublesAdmissionTests` covers construction, side-only API rejection, atomic
collective rejection, doubles-capture rejection, slot submission validation, speed-ordered switches,
and exact slot events. `D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj
--no-restore --filter Battle` passed 577 tests. The tooling regeneration compared byte-identical:
937 inventory-only entries, digest `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`,
and 0 certified. Audit capability group: target selection/topology (144 historical rows); exact
normalized reference keys affected: none, because 15H vectors have not yet been registered.
No schema or dependency change. Files: `BattleController.cs`, `BattleEvents.cs`, and
`BattleDoublesAdmissionTests.cs`. Next eligible package: **15B-3**.

Progress (2026-07-11): **15B-3 COMPLETE.** `BattleActionSubmission` now carries a typed
selection union (`ActiveSlotSelection`, `PartyMemberSelection`, or `MoveReferenceSelection`) rather
than an overloaded integer. Admission validates selection kind, relationship, topology membership,
party/move ranges, and the fainted-party requirement without mutation or RNG. The doubles move
checkpoint spends PP and emits slot-aware `MoveUsed` before live materialization; target failure
then emits `MoveFailed(TargetUnavailable)`. Live active targets follow the locked slot-stable
rules: selected opponents fall back in topology order, own allies never fall back, spread scopes
filter fainted slots, and random opponents draw exactly once only with two live candidates.
Side, field, fainted-party, and move-reference scopes materialize once without fake active targets.

Evidence: `BattleLiveTargetMaterializationTests` covers every authored target shape in doubles,
invalid selection rejection without PP/events/RNG, selected-opponent fallback, no ally fallback,
PP/`MoveUsed`/failure order, and 0/1/2 random-candidate draw counts. Focused battle tests passed
596 tests. Tooling regeneration remained byte-identical: 937 inventory-only entries, digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`, 0 certified. Audit group:
target selection/topology (144 historical rows); normalized reference keys: none until 15H vectors
are registered. No schema or dependency change. Files: `BattleTurnActions.cs`, `BattleEvents.cs`,
`BattleController.cs`, `BattleDoublesAdmissionTests.cs`, and
`BattleLiveTargetMaterializationTests.cs`. Next eligible package: **15B-4**.

Progress (2026-07-11): **15B-4 IN PROGRESS.** The first normal-path checkpoint adds the reusable
Targets-stage spread modifier: when two or more live direct targets were snapshotted it floors
damage by `3/4`; one target retains full damage. `DamageCalcTargetsTests` covers the one/two-target
boundary and exact floor. Continuation: thread ordered action/target contexts through the normal
resolver, then add per-target accuracy/hit/effect resolution, action-total accounting, and the
deterministic trace/event vectors before this package can be marked complete. No conformance status,
schema, or dependency change in this checkpoint.

Progress (2026-07-11): **15B-4 IN PROGRESS.** The second normal-path checkpoint threads the
existing singles move resolver through slot-aware `BattleActionContext` and ordered
`BattleTargetContext` instances. Every landed hit now contributes to both the target total and the
action total; target-scoped consumers retain the target total while drain and recoil use the
action total after direct hits. This preserves the singles event sequence while establishing the
shared context seam for later multi-target execution. `BattleV5OpTests` now pins that all target
hits precede the action-scoped drain/recoil effects; focused `BattleV5OpTests`,
`BattleLiveTargetMaterializationTests`, and `DamageCalcTargetsTests` passed 48 tests, and the
complete focused Battle suite passed 601 tests. `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx
--no-restore` passed with 0 warnings/errors, and `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx
--no-build` passed 1,010 tests (878 Core, 104 Creator, 21 Runtime, 7 Tools). Audit capability group:
target selection/topology (144 historical rows); normalized reference keys affected: none, because
15H vectors are not yet registered. RNG/event change: none for singles; the new context only
records the totals already used by the resolver. Manifest regeneration/certification: none; the
package remains incomplete. No schema or dependency change. Files: `EffectContext.cs`,
`BattleController.cs`, and `BattleV5OpTests.cs`. Next continuation: resolve per-target accuracy,
immunity, and direct damage through these contexts in the doubles scheduling path, then add
deterministic one-versus-two-target spread damage tests.

Progress (2026-07-11): **15B-4 IN PROGRESS.** Doubles move scheduling now materializes active
targets once and resolves standard direct damage through ordered action/target contexts. Accuracy
rolls once per target; immunity is checked before critical/damage draws; and the target-count
modifier is applied at the damage formula's Targets stage before all later modifiers. `MoveMissed`
and `DamageDealt` now carry slot identity while preserving their side compatibility properties.
`BattleDoublesDamageTests` covers two-target spread damage, the one-live-target boundary,
per-target miss continuation, and the exact no-hit-RNG immunity path. Focused target/damage tests
passed 52 tests; the complete focused Battle suite passed 605 tests. `D:\dotnet\dotnet.exe build
CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors, and `D:\dotnet\dotnet.exe test
CreatureGameMaker.slnx --no-build` passed 1,014 tests (882 Core, 104 Creator, 21 Runtime, 7 Tools).
Audit capability group: target
selection/topology (144 historical rows); normalized reference keys affected: none, because 15H
vectors are not yet registered. RNG/event change: two-target standard damage now consumes
accuracy per target and crit/damage draws only for accurate, non-immune targets, in topology order.
Manifest regeneration/certification: none; the package remains incomplete. No schema or dependency
change. Files: `BattleController.cs`, `BattleEvents.cs`, `DamageCalc.cs`, and
`BattleDoublesDamageTests.cs`. Next continuation: add the shared action-level multi-hit count,
per-hit critical/damage loops, target effects/contact consequences, and once-per-action aggregate
drain/recoil/cost ordering.

Progress (2026-07-11): **15B-4 IN PROGRESS.** Doubles standard multi-hit moves now roll one
action-scoped hit count after accuracy, then resolve each target in topology order and each hit in
ascending hit order. Target-scoped effects run after all direct damage for each eligible target;
action-scoped drain, recoil, and other existing action effects run once after the target batch and
read the action total. `BattleDoublesDamageTests` pins shared hit-count draws, target-then-hit event
order, per-target stat changes, and exactly-once aggregate drain/recoil. Focused doubles tests
passed 6 tests; the complete focused Battle suite passed 607 tests. `D:\dotnet\dotnet.exe build
CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors, and `D:\dotnet\dotnet.exe test
CreatureGameMaker.slnx --no-build` passed 1,016 tests (884 Core, 104 Creator, 21 Runtime, 7 Tools).
Audit capability group: target
selection/topology (144 historical rows); normalized reference keys affected: none, because 15H
vectors are not yet registered. RNG/event change: multi-hit count draws once after all accuracy
draws; each accurate, non-immune hit then draws critical followed by damage roll in target/hit order.
Manifest regeneration/certification: none; the package remains incomplete. No schema or dependency
change. Files: `BattleController.cs` and `BattleDoublesDamageTests.cs`. Next continuation: make
contact consequences slot-aware, then implement `EffectTrace` and the required singles/doubles
family goldens.

Progress (2026-07-11): **15B-4 IN PROGRESS.** Contact hooks now retain both side and precise slot
identity. The doubles resolver applies contact consequences after each contacted target's effects;
an ability or held effect on one opposing slot cannot trigger for its ally. A deterministic 100%
contact chance now consumes no RNG draw. `BattleDoublesDamageTests` proves that only the contacted
slot's hook changes the attacker, and existing singles hook tests remain green. Focused contact/
doubles tests passed 51 tests; the complete focused Battle suite passed 608 tests. `D:\dotnet\dotnet.exe
build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors, and `D:\dotnet\dotnet.exe test
CreatureGameMaker.slnx --no-build` passed 1,017 tests (885 Core, 104 Creator, 21 Runtime, 7 Tools).
Audit capability
group: target selection/topology (144 historical rows); normalized reference keys affected: none,
because 15H vectors are not yet registered. RNG/event change: deterministic contact effects no
longer draw; non-deterministic contact effects retain one chance draw per contacted target in
topology order. Manifest regeneration/certification: none; the package remains incomplete. No
schema or dependency change. Files: `BattleHookDispatcher.cs`, `BattleController.cs`, and
`BattleDoublesDamageTests.cs`. Next continuation: implement `EffectTrace`, family goldens, and the
remaining 15B-4 closeout acceptance evidence.

Progress (2026-07-11): **15B-4 IN PROGRESS.** The first concrete `EffectTrace` seam records ordered
doubles direct-resolution accuracy, shared hit-count, immunity, and damage entries with source/
target slots, performed-draw status, draw result/value, and emitted-event index range. It is
diagnostic only and does not alter simulation. `BattleDoublesDamageTests` pins the trace order and
event links. Focused trace tests passed 8 tests; `D:\dotnet\dotnet.exe build
CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors and the full test suite passed
1,018 tests (886 Core, 104 Creator, 21 Runtime, 7 Tools). Manifest/certification: none; package
remains incomplete. Next continuation: trace critical/damage-roll draws, extend singles coverage,
and add the required family goldens.

Progress (2026-07-12): **15B-4 IN PROGRESS.** Doubles direct-hit traces now include the actual
critical-check and damage-roll draws between each target's immunity decision and damage mutation.
The trace test pins the full target/hit order and raw draw values. Focused doubles/V5 tests passed
33 tests; `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/
errors and the full suite passed 1,018 tests (886 Core, 104 Creator, 21 Runtime, 7 Tools).
Manifest/certification: none; package remains incomplete. Next continuation: extend the same trace
entries to singles and add the required family goldens.

Progress (2026-07-12): **15B-4 IN PROGRESS.** Singles standard direct hits now produce the same
accuracy, hit-count, immunity, critical, damage-roll, and damage trace entries as doubles. The
singles trace regression pins those entries and raw draws without changing event behavior. Focused
singles/doubles trace tests passed 19 tests; the approved full build passed with 0 warnings/errors
and the full suite passed 1,019 tests (887 Core, 104 Creator, 21 Runtime, 7 Tools).
Manifest/certification: none; package remains incomplete. Next continuation: add neutral
singles/doubles family goldens and complete the remaining 15B-4 acceptance evidence.

Progress (2026-07-12): **15B-4 IN PROGRESS.** Added neutral file-backed family goldens under
`tests/Cgm.Core.Tests/Battle/Goldens/`: one singles direct-hit action and one doubles spread action.
They compare stable event and trace projections, so the fixtures contain no source-corpus content.
`BattleFamilyGoldenTests` passed 2 tests; the approved full build passed with 0 warnings/errors and
the full suite passed 1,021 tests (889 Core, 104 Creator, 21 Runtime, 7 Tools). Manifest/
certification: none; package remains incomplete. Next continuation: finish the remaining 15B-4
acceptance evidence (including contact-faint snapshot behavior), then run the package closeout
review before 15B-5.

Progress (2026-07-12): **15B-4 IN PROGRESS.** The generic `contactChanceEffect` payload now also
supports flat positive contact damage. It emits `ContactDamaged` and can faint the contacting source
after snapshotted direct targets have all resolved. Validation rejects missing, mixed, or nonpositive
payloads. The doubles snapshot test proves both targets take direct damage before the source faints.
Focused contact/validation tests passed 40 tests; the approved full build passed with 0 warnings/
errors and the full suite passed 1,022 tests (890 Core, 104 Creator, 21 Runtime, 7 Tools). Manifest/
certification: none; package remains incomplete. Next continuation: perform the 15B-4 closeout
review and resolve any remaining acceptance gaps before 15B-5.

Progress (2026-07-12): **15B-4 IN PROGRESS — closeout review: NO-GO.** Finding **FIX-NOW**:
doubles scheduling had spent PP before source action gates, so `moveGate` failure could announce and
consume a move. It now mirrors the singles source-gate order before PP/target work, resets the
turn-scoped volatile/damage state for every active slot, records accepted move use, and emits the
precise source slot for move-gate failure. Always-hit accuracy entries now explicitly record no
performed draw. `BattleDoublesDamageTests` adds blocked-move (no PP, target, trace, or RNG) and
always-hit trace regressions; focused gate/doubles/controller tests passed 27 tests. The approved
full build passed with 0 warnings/errors, the full suite passed 1,024 tests (892 Core, 104 Creator,
21 Runtime, 7 Tools), and `git diff --check` passed. Finding **FIX-LATER, package-blocking**: the
trace remains incomplete for source-gate/status, target-selection, and effect-chance draws and does
not yet satisfy the locked full diagnostic contract. Manifest/certification: none; package remains
incomplete. Next continuation: extend the trace across those resolver stages, add exact bounds/
skipped-draw evidence, then repeat the 15B-4 closeout review before 15B-5.

Progress (2026-07-12): **15B-4 IN PROGRESS.** `EffectTrace` now carries an optional raw draw
bound, populated for direct accuracy (100), critical (1), damage roll (16), and variable hit-count
draws. Doubles `randomOpponent` target materialization now records its selected slot, candidate
count, raw draw, and exact bound, while its singleton path is explicitly recorded as a skipped draw.
`BattleLiveTargetMaterializationTests` and `BattleDoublesDamageTests` pin those trace records and
the direct-draw bounds; focused target/damage/controller/golden tests passed 44 tests. The approved
full build passed with 0 warnings/errors, the full suite passed 1,025 tests (893 Core, 104 Creator,
21 Runtime, 7 Tools), and `git diff --check` passed. Manifest/certification: none; package remains
incomplete. Next continuation: trace source status/volatile gates and compiled effect chance draws,
including skipped-draw evidence for ineligible/fainted effects, then repeat the 15B-4 closeout
review before 15B-5.

Progress (2026-07-12): **15B-4 IN PROGRESS.** Source action gates now trace before PP or target
work: persistent status, flinch, confusion, and compiled `moveGate` each record their pass/block
result, raw draw/bound where applicable, and emitted-event range. The pure status/confusion helpers
now expose their existing draw through overloads, preserving their prior APIs and RNG order.
Neutral single/doubles family goldens include the new source-gate records. Focused source-gate/
damage/golden tests passed 36 tests; the approved full build passed with 0 warnings/errors, the full
suite passed 1,027 tests (895 Core, 104 Creator, 21 Runtime, 7 Tools), and `git diff --check`
passed. Manifest/certification: none; package remains incomplete. Next continuation: trace compiled
target/action effect chance draws, including skipped-draw evidence for ineligible or fainted targets,
then repeat the 15B-4 closeout review before 15B-5.

Progress (2026-07-12): **15B-4 IN PROGRESS.** All compiled `MoveEffect.Chance` resolver paths now
share one traceable chance gate. Eligible non-deterministic target effects record their per-target
`Next(100)` draw, bound, result, and event range; ineligible/fainted targets and deterministic 0%/
100% chances record a skipped draw. This also removes the prior unnecessary deterministic chance
draws while preserving the required random-effect order. `BattleDoublesDamageTests` pins two target
rolls and a fainted-target skip; the deterministic confusion fixture was aligned with the locked
no-draw rule. Focused effect/damage/controller/golden tests passed 53 tests; the approved full
build passed with 0 warnings/errors, the full suite passed 1,029 tests (897 Core, 104 Creator, 21
Runtime, 7 Tools), and `git diff --check` passed. Manifest/certification: none; package remains
incomplete. Next continuation: run the 15B-4 closeout review against the full trace contract,
resolve any remaining non-direct RNG/trace gaps, then repeat the package gate before 15B-5.

Progress (2026-07-12): **15B-4 IN PROGRESS — closeout review: NO-GO.** Finding **FIX-NOW**
resolved: `contactChanceEffect` now uses the shared deterministic chance gate after status/type/
hook eligibility, so it records each contacted slot's result/bound/event range and skips an immune
or deterministic chance without consuming RNG. `BattleDoublesDamageTests` pins both the 100% skip
and two contacted 50% draws. Focused contact/doubles/V5/golden tests passed 87 tests; the approved
full build passed with 0 warnings/errors, the full suite passed 1,030 tests (898 Core, 104 Creator,
21 Runtime, 7 Tools), and `git diff --check` passed. Remaining **FIX-NOW, package-blocking** trace
gaps: turn-order tie draws, multi-turn-lock/trap/confusion-duration draws, Protect, and forced
switch reserve selection. Capture’s separate action family is not a 15B-4 direct-move blocker.
Manifest/certification: none; package remains incomplete. Next continuation: trace the listed
non-direct move-resolution draws with bounds/results/skips, then repeat the 15B-4 closeout review
before 15B-5.

Progress (2026-07-12): **15B-4 IN PROGRESS.** `BattleTurnOrder` now accepts an optional
trace observer and the controller uses it for every scheduled phase. Each Fisher-Yates tie draw is
recorded before action resolution with its source slot, raw draw, exact bound, and selected index.
`BattleDoublesDamageTests` pins the two-action `Next(2)` record; focused turn/doubles/controller/
golden tests passed 40 tests. The approved full build passed with 0 warnings/errors, the full suite
passed 1,031 tests (899 Core, 104 Creator, 21 Runtime, 7 Tools), and `git diff --check` passed.
Manifest/certification: none; package remains incomplete. Next continuation: trace multi-turn-lock,
trap/confusion-duration, Protect, and forced-switch reserve draws, then repeat the 15B-4 closeout
review before 15B-5.

Progress (2026-07-13): **15B-4 IN PROGRESS.** Multi-turn-lock, partial-trap, and both target/self
confusion-duration paths now record performed duration draws in their actual resolver order. The
trace carries `DrawMinimum` when a range has a non-zero lower bound, so the duration draws record
their exact `[2,4)`, `[4,6)`, and `[1,5)` bounds; an already trapped target records a skipped trap
draw. `BattleVolatileTests` pins target and rampage self-confusion, lock and trap bounds/results,
event ranges, and the skipped repeat-bind draw. Focused trace tests passed 38 tests; the wider
Battle suite passed 624 tests. The approved full build passed with 0 warnings/errors, the full
suite passed 1,033 tests (901 Core, 104 Creator, 21 Runtime, 7 Tools), and `git diff --check`
passed. Manifest/certification: none; package remains incomplete. No schema or dependency change.
Files: `BATTLE_SYSTEM_SPEC.md`, `EffectTrace.cs`, `VolatileEffects.cs`, `BattleController.cs`, and
`BattleVolatileTests.cs`. Next continuation: trace Protect and forced-switch reserve-selection
draws, then repeat the 15B-4 closeout review before 15B-5.

Progress (2026-07-13): **15B-4 IN PROGRESS.** The generic Protect resolver now traces its one
`NextDouble()` draw for every resolved attempt, with source slot, bound `1`, success/failure value,
and the corresponding `Protected` or `ProtectFailed` event range. `BattleProtectTests` pins the
first-use success and consecutive-chain failure draws without altering the established chain policy.
Focused Protect/volatile/golden tests passed 16 tests; the wider Battle suite passed 624 tests. The
approved full build passed with 0 warnings/errors, the full suite passed 1,033 tests (901 Core, 104
Creator, 21 Runtime, 7 Tools), and `git diff --check` passed. Manifest/certification: none; package
remains incomplete. No schema or dependency change. Files: `BATTLE_SYSTEM_SPEC.md`,
`EffectTrace.cs`, `VolatileEffects.cs`, `BattleController.cs`, and `BattleProtectTests.cs`. Next
continuation: trace forced-switch reserve-selection draws, then repeat the 15B-4 closeout review
before 15B-5.

Progress (2026-07-13): **15B-4 IN PROGRESS.** The generic force-switch resolver now records its
reserve selection in `EffectTrace`: a multi-reserve target draws `Next(candidateCount)` once and
records its raw selection plus selected party index, while singleton, no-reserve, wild-flee, and
fainted-target paths visibly skip the draw. `BattleForceSwitchTests` pins singleton/no-reserve skips
and a two-reserve selection. Focused force-switch/V5/golden tests passed 32 tests; the wider Battle
suite passed 625 tests. The approved full build passed with 0 warnings/errors, the full suite passed
1,034 tests (902 Core, 104 Creator, 21 Runtime, 7 Tools), and `git diff --check` passed.
Manifest/certification: none; package remains incomplete. No schema or dependency change. Files:
`BATTLE_SYSTEM_SPEC.md`, `EffectTrace.cs`, `BattleController.cs`, and `BattleForceSwitchTests.cs`.
Next continuation: run the 15B-4 closeout review against the complete trace contract, resolve any
review finding, and repeat the package gate before 15B-5.

Progress (2026-07-13): **15B-4 IN PROGRESS — closeout review: NO-GO.** Finding **FIX-NOW**:
`ResolveDoublesMoveScheduling` emits `MoveUsed` but invokes the shared resolver only when
`move.Power` is non-null. Doubles status-only moves therefore materialize targets yet never create
an action context or dispatch their target/action effects; generic Protect and force-switch are
among the affected effects. This contradicts the 15B-4 required target/action-effect resolution and
leaves no doubles trace evidence for those effect paths. Required continuation: route status-only
materialized actions through the shared ordered effect resolver, preserve their no-direct-hit RNG
order, and add doubles resolver/trace vectors before repeating this review. The preceding focused
and full verification remains green (build 0 warnings/errors; 1,034 tests), but it does not cover
this missing required behavior. Manifest/certification: none; package remains incomplete. No
schema or dependency change. Do not begin 15B-5.

Progress (2026-07-13): **15B-4 COMPLETE — closeout review: GO.** The previous status-only
resolver finding is fixed: every materialized doubles action now creates the ordered action/target
contexts, runs per-target accuracy, and dispatches target effects followed by action effects without
direct-hit RNG for a status move. Protect and force-switch now have doubles resolver/trace vectors;
force-switch switches the selected target slot rather than using the singles adapter. The closeout
review also found and resolved the remaining non-creature scope gap: side scopes dispatch their
action effects once with the authored target side, field scopes dispatch field-safe action effects
once, and neither path runs target accuracy. Weather form reevaluation now addresses every active
slot, and queued action effects retain their doubles source slot. `BattleDoublesDamageTests` covers
Protect, force-switch at enemy slot 1, side hazard, field weather, and source-slot queue behavior.
Focused doubles/weather/protect/force-switch/stage/action-gate tests passed 58 tests. The approved
full build passed with 0 warnings/errors; the full suite passed 1,039 tests (907 Core, 104 Creator,
21 Runtime, 7 Tools); and `git diff --check` passed. Review findings: none. Manifest/certification:
none; no schema or dependency change. The next eligible package is 15B-5; no 15B-5 work started.

Progress (2026-07-13): **15B-5 IN PROGRESS.** The first normal-path checkpoint adds the generic
parameterless `positionSwap` move op. It compiles only for the authored `ally` target and atomically
exchanges the two allied active-slot party assignments. Creature-owned state therefore travels with
the creature, while slot-owned queued action gates stay on their original slots. The resolver emits
`PositionsSwapped` plus a no-draw `PositionSwap` trace record. `BattleDoublesDamageTests` covers
the atomic exchange, state ownership, event, and trace; `MoveCompilerTests` covers valid compilation
and invalid target/chance/params. Focused compiler/doubles tests passed 69 tests. The approved full
build passed with 0 warnings/errors; the full suite passed 1,042 tests (910 Core, 104 Creator, 21
Runtime, 7 Tools); and `git diff --check` passed. The serialized `Effect` object shape is unchanged,
so this compatible closed-palette addition requires neither a schema-version bump nor migration;
`DATA_SCHEMA.md` records the new op. Manifest/certification: none; no dependency change. Files:
`DATA_SCHEMA.md`, `BATTLE_SYSTEM_SPEC.md`, `BattleActiveSlots.cs`, `BattleController.cs`,
`BattleEvents.cs`, `EffectTrace.cs`, `MoveCompiler.cs`, `MoveEffects.cs`, and the focused tests.
Next continuation: implement deterministic generic redirection filters and precedence, including
their slot-aware events/traces and the 15B-5 redirect acceptance matrix.

Progress (2026-07-13): **15B-5 IN PROGRESS.** Began the shared controller redirection seam: turn-scoped
redirect conditions are cleared at turn start, eligible single-opponent targets are replaced before
random-target selection, and precedence is priority, owner speed, then topology order with no RNG.
This is an incomplete checkpoint: the serialized `redirect` op/compiler mapping plus the required
class/tag/bypass and competing-redirector acceptance vectors remain before it can be verified as a
normal-path deliverable. The Core project build passed with 0 warnings/errors; full-suite testing
has not yet been run for this incomplete checkpoint. Do not begin 15B-6.

Progress (2026-07-13): **15B-5 IN PROGRESS.** The generic `redirect` op now compiles a turn-scoped
class-filtered redirect condition with optional bypass classes. The shared materializer applies it
before random-target selection, replacing only redirectable single-opponent targets and emitting
`TargetRedirected` plus a no-draw `Redirection` trace. `MoveCompilerTests` covers compilation and
`BattleDoublesDamageTests` proves the selected target is replaced before damage. Focused tests passed
71 tests; full build passed with 0 warnings/errors and 1,044 tests passed (912 Core, 104 Creator, 21
Runtime, 7 Tools); `git diff --check` passed. Remaining 15B-5 work: tag filters, bypass/competing
precedence vectors, rejected-hook trace evidence, and the closeout review. Do not begin 15B-6.

Progress (2026-07-13): **15B-5 COMPLETE — closeout review: GO.** The reusable `positionSwap` and
`redirect` ops now satisfy the complete redirection/position contract for the target-selection and
topology capability group (historical audit group 144; no normalized reference keys are yet
registered). `redirect` strictly compiles required class filters and optional closed tag filters;
the normal controller path filters eligible redirectors by priority, speed, then topology order with
no redirect RNG. It handles selected and random-opponent targets before target selection, emits
slot-aware events/traces only when a target actually changes, and ignores all non-redirectable
scopes and invalidated redirectors. The acceptance vectors cover class/tag acceptance and bypass,
priority/speed/topology competition, random-target draw suppression, spread/ally/side/field
exclusion, position-state ownership, and a redirector fainting before a later action. The review
found and fixed one FIX-NOW event bug: a selected move already aimed at the redirector no longer
emits a false `TargetRedirected` event. RNG/event/trace change: position swap and redirection use no
draws; random-target redirection suppresses its target draw. Validation/compiler/resolver paths are
covered by `MoveCompilerTests` and `BattleDoublesDamageTests`; focused tests passed 78. Full
verification passed: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` (0 warnings/
errors), `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (1,051 tests: 919 Core,
104 Creator, 21 Runtime, 7 Tools), and `git diff --check`. Manifest/certification: none, because
15H normalization/conformance vectors remain open. No dependency or serialized-shape change; the
compatible closed-palette addition is recorded in `DATA_SCHEMA.md`. Final commit: not created in
this shared pre-existing worktree. Next eligible package: **15B-6 faint outcome and replacement**.

Progress (2026-07-14): **15B-6 COMPLETE — closeout review: GO.** Faints now leave their active
slots logically empty for later actions, captured actions invalidate rather than transferring to a
reserve, and outcome checks after each complete action or end-turn batch distinguish a winner from
a simultaneous draw. Surviving sides receive one `ReplacementRequested` per fillable empty slot in
topology order. `ResolveReplacements` atomically validates an exact typed choice set, rejects missing,
duplicate, out-of-range, fainted, or active party choices without state/event/PP/RNG mutation, then
applies accepted choices through the same slot-addressed neutral `SwitchTo` helper used by voluntary
and forced switching. Each entrant runs its switch-in hooks once; an entry-hazard faint completes the
batch and re-requests the slot while a healthy reserve remains. Unfillable slots remain empty, living
slots alone submit later actions, and every ordinary-turn API rejects while a request is pending.

Evidence: `BattleReplacementTests` covers one/two/both-side empty slots, atomic rejection, topology
application/event order, active/fainted/duplicate choices, pending-turn blocking, unfillable slots,
captured-action invalidation, and hazard-faint repetition. `BattleControllerTests` covers action and
end-turn simultaneous draws, `BattleSwitchTests`/`BattleHazardTests` use explicit replacement choices,
and `replacement-checkpoint.golden` pins the ordered request/switch event flow. The existing Runtime
showcase remains a thin consumer through `BattleScene.SubmitReplacements`; it does not choose or apply
Core rules. Focused Battle tests passed 650. Full verification passed:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` (0 warnings/errors),
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (1,059 tests: 927 Core, 104 Creator,
21 Runtime, 7 Tools), and `git diff --check`. Review findings: one stale controller comment was removed;
the Runtime showcase's ordinary-turn submission while replacement was pending was corrected to submit
the typed choices. No remaining FIX-NOW finding. RNG implication: replacement admission/application
draws nothing. Event/API changes: nullable-winner `BattleOutcome`/`BattleEnded`,
`ReplacementRequested`, `BattleReplacementSelection`, pending-slot inspection, and explicit
replacement submission. Manifest/certification: unchanged at 0/937; no normalized reference keys
were advanced. Schema/migration and dependency impact: none. No commit was created in the shared
pre-existing worktree. Next eligible work is the target/topology normalization/certification cohort,
the cumulative 15B golden, and the 15B exit review; Phase 15B is not yet closed.

Documentation reconciliation (2026-07-14): reviewed the Phase 15 commit sequence and the complete
shared worktree together. The implemented reusable surface now includes the Phase 15A manifest;
stable one-/two-slot topology and action ordering; queued move gates, HP helpers, and status-power
queries; 15B-2 action admission; 15B-3 typed materialization; 15B-4 per-target/spread execution and
concrete trace; 15B-5 redirection/position exchange; and 15B-6 outcome/replacement. The battle spec,
test strategy, scope status, owner map, and move-audit support snapshot now describe that surface
without treating representative package tests as per-move certification. Verified manifest state
remains 937 `inventoryOnly`, 0 `certified`, digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`. The three focused family
goldens are not the required cumulative 15B golden. Therefore 15B and Phase 15 remain **IN PROGRESS**;
the immediate queue below is unchanged in substance. Verification after documentation reconciliation:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors;
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` passed 1,059 tests (927 Core,
104 Creator, 21 Runtime, 7 Tools); and `git diff --check` passed.

Progress (2026-07-14): **15B COMPLETE — exit review: GO.** The deterministic audit command now
reads a reviewed decision catalog, derives sanitized generic definitions from the locked local
wrappers, compiles every definition, writes canonical-LF definition and manifest files, and refuses
missing evidence, duplicate/stale keys, or source-hash drift. The generated manifest advances 57
target/topology entries to `certified` and leaves 880 `inventoryOnly`, with the unchanged 937-file
digest `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`.

Exact certified keys: `move-0013`, `move-0037`, `move-0039`, `move-0043`, `move-0045`,
`move-0051`, `move-0075`, `move-0080`, `move-0081`, `move-0120`, `move-0129`, `move-0139`,
`move-0145`, `move-0153`, `move-0157`, `move-0178`, `move-0181`, `move-0196`, `move-0200`,
`move-0257`, `move-0284`, `move-0298`, `move-0304`, `move-0314`, `move-0323`, `move-0330`,
`move-0336`, `move-0435`, `move-0436`, `move-0464`, `move-0482`, `move-0522`, `move-0523`,
`move-0527`, `move-0545`, `move-0549`, `move-0555`, `move-0570`, `move-0572`, `move-0574`,
`move-0586`, `move-0591`, `move-0597`, `move-0605`, `move-0616`, `move-0618`, `move-0619`,
`move-0691`, `move-0693`, `move-0730`, `move-0784`, `move-0786`, `move-0820`, `move-0822`,
`move-0824`, `move-0825`, and `move-0833`. The decision catalog is the authoritative evidence and
contact-policy record; the generated definition catalog owns exact normalized hashes, mechanic
families, `modern_reference`/doubles context, and registered test IDs. Every remaining historical
target-shape row retains at least one dependency owned by 15C-15G or 15H; no target-only blocker
remains.

The per-reference theory validates `MoveRule`, typed compilation, exact doubles target sets,
ordered slot events, effect state, contact behavior, timed flags, and registered IDs. The new
`phase-15b-cumulative.golden` spans four-slot admission, spread damage, two captured-action
invalidations, atomic topology-order replacement, and a simultaneous all-slot draw. The exit review
found and fixed two FIX-NOW drifts: doubles had not shared singles' charge/rampage PP lifecycle, and
active-creature status/stage/heal/volatile events still lost slot identity. Timed moves now use one
shared preparation path and may complete a forced sequence after spending their last PP; active
events carry `BattleSlot` while singles constructors/properties remain compatible. RNG changes are
limited to the already-specified timed-move draws; target/effect draw order is unchanged. Schema and
dependency impact: none. Verification: full solution build/tests and deterministic regeneration
passed. `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` completed with 0 warnings/
errors; `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` passed 1,122 tests (930 Core,
104 Creator, 21 Runtime, 67 Tools); regeneration was byte-identical; and `git diff --check` passed.
Completed next package: **15C-1 unified query pipeline specification lock and implementation**.
The topological queue now advances to **15D-1 queued-intent foundation**.

#### 15C — Query hooks and variable formulas

Primary groups: damage query modifiers (64), special accuracy (36), stat expansion (63).

- Complete base-power formula families: speed, weight, friendship, HP/status/item/terrain/weather,
  consecutive use, prior damage/action, party state, and stat comparison.
- Complete type/class/accuracy/crit/priority/turn-order/stat/effectiveness/final-damage queries.
- Add HP equalization, HP floors, percent-current/max HP, metric and inventory formulas.

Ordered feature packages:

1. **15C-1 — Unified query pipeline (`IMPLEMENTED`; prerequisite 15B-4).** First lock the battle-spec
   query registry: query ID; integer or reduced-fraction value type; base source; ordered modifier
   stages; replace/add/multiply/min/max precedence; floor point after every multiplication; final
   clamp; source/target/field/ruleset inputs; and trace fields. The locked stage order is move
   identity → authored base → source/target state → ability/item/condition hooks → ruleset override
   → final clamp. Hooks at one stage order by hook priority, owner scope order, then insertion order;
   no dictionary ordering or tie RNG. Implement one query service used by damage, accuracy, speed,
   healing, AI preview, and trace. Direct legacy fields feed authored-base only. **Acceptance:** one
   vector per modifier kind, negative/zero/overflow guards, exact intermediate floors, stable hook
   ordering, unknown query/modifier rejection, and old fixed-damage results unchanged.
2. **15C-2 — HP and status formula registry (`IMPLEMENTED`; prerequisite 15C-1).** Inventory every
   HP/status audit key and lock rows for current/max/missing HP ratio, exact HP bands, source/target
   persistent/volatile status predicates, status-count, HP equalization, cannot-KO floor, current-HP
   and max-HP damage, and status-dependent secondary chance. Every row names numerator/denominator,
   comparison inclusivity, min/max result, zero behavior, target scope, and whether the formula
   damages, supplies power, or modifies a query. Reuse `statusPower`, `heal`, and `hpFraction` where
   exact. **Acceptance:** threshold±1 tables, 1 HP and full-HP boundaries, mismatch/no-status cases,
   floor/clamp conservation, normal resolver/trace, and all affected neutral conformance vectors.
3. **15C-3 — Speed, weight, and physical metric formulas (`IMPLEMENTED`; prerequisite 15C-1).** Lock
   ratio bands and inclusivity, effective-versus-base speed/weight choice, zero-denominator behavior,
   airborne/grounded inputs, height/size units, caps, and ruleset differences for every cited audit
   key. Weight/speed changes must enter central effective queries rather than mutate definitions.
   **Acceptance:** every band edge±1, minimum/maximum values, modified versus base inputs, grounded
   variants, overflow-safe arithmetic, resolver/trace evidence, and affected conformance vectors.
4. **15C-4 — Action history and consecutive-use formulas (`IMPLEMENTED`; prerequisite 15C-1).** Lock
   attempt/success/connect/fail
   meanings; turn/action/hit retention; source/target keys; consecutive reset on different action,
   switch, fail, or miss per formula row; moved-first/last checkpoint; retaliation qualification;
   ally-faint history; and caps. Implement queries only over bounded typed memory, never event-log
   parsing. Implement the minimal bounded action-attempt history this family needs; 15G-2 extends the
   same service with complete per-hit damage records. **Acceptance:** first use, repeat to cap, interruption/reset matrix, switch/faint boundary,
   doubles source/target isolation, turn aging, and replay-stable traces.
5. **15C-5 — Party, resource, stage, item, and random-table inputs (`IMPLEMENTED`; prerequisites 15C-1
   and effective-query portion of 15F-1).** Lock living/fainted/contributing party filters,
   friendship range, PP timing (before or after current spend), positive-stage sum and comparison,
   item-data lookup failure, and random-table ordered weights. Random tables use cumulative positive
   integer weights in authored order, one `Next(totalWeight)` draw, half-open ranges, and no draw for
   one nonzero entry. **Acceptance:** empty/full party, duplicates, PP 0/1/max, stage extremes/ties,
   missing/suppressed item, random endpoints/frequency-independent deterministic vectors, and trace.
6. **15C-6 — Field, type, class, stat, and effectiveness queries (`IMPLEMENTED`; prerequisites 15C-1
   and 15E/15F query contracts).** Lock effective move type/class, environment type, attacking and
   defending stat selectors, STAB source, immunity/effectiveness override, inverse/special charts,
   grounded state, weather/terrain inputs, and spread flag. Base definitions remain immutable;
   overlays and conditions provide ordered modifiers. **Acceptance:** single/dual type tables,
   immunity and override precedence, STAB/no-STAB, alternate stat/class selection, field absent/
   present, spread one/two target, and identical AI/resolver query results.
7. **15C-7 — Accuracy, critical, priority, final damage, and healing modifiers (`IMPLEMENTED`;
   prerequisite 15C-1; later condition integrations extend its tables).** Lock always-hit versus
   accuracy-stage behavior, weather/gravity/semi-invulnerable exceptions, next-hit/next-crit
   consumption, critical-stage caps, priority and turn-order modifiers, final damage floor/cap,
   healing multipliers/blocks, and exact skipped-draw rules. Consume one-shot conditions only at
   their specified successful query checkpoint. **Acceptance:** full stage tables, guaranteed/missed/
   immune cases, one-shot consumption matrix, priority ties, floor/cap edges, blocked healing, exact
   RNG bounds/order, and family goldens.

Every formula package requires table-driven boundary tests with exact intermediate rounding, compiler
validation for params/enums/ranges, resolver tests through the normal damage/heal path, and trace
assertions showing the base and final values. Never place a formula branch in `DamageCalc` when a
reusable query/formula helper can supply its input.

For each 15C package, the spec-lock commit must add the complete formula-registry rows before code.
The plan authorizes the decision hierarchy in §2.1; an implementing model does not stop merely
because a source generation differs. It records both required profile rows and proceeds.

Progress (2026-07-10): reusable status-conditioned base-power handling is in place through
`statusPower`. It targets the user or active target, matches a specific or any persistent status, and
can suppress the physical burn penalty only when its authored condition matches. Compiler validation,
numeric rounding, target/user resolution, and burn interaction are covered by tests. The remaining
formula families and normalized per-move conformance definitions remain open.

Progress (2026-07-14): **15C-1 COMPLETE.** The battle spec and effect catalog now lock the closed
numeric query registry, exact reduced-fraction values, immutable stage order, operation precedence,
per-multiplication floor points, hook priority/scope/insertion order, clamps, failure rules, context,
and deterministic step trace. `BattleQuery` is the one normal Core service for base power,
offensive/defensive stats, accuracy, speed, healing, final damage, and AI previews; critical chance,
priority, and effectiveness have typed registry seams for their later 15C owners. Existing HP/status
power modifiers, ability/held-item stat and damage hooks, and weather damage modifiers now produce
typed query modifiers. Damage hooks are slot-addressed in doubles. Fixed, level-based, OHKO, and
counter damage cross the final-damage query unchanged. Focused query tests cover every modifier kind,
exact reduction/flooring, ordering, first-replace behavior, clamps, negative/zero/overflow guards,
invalid IDs/stages/operands/context, resolver traces, doubles hook ownership, accuracy rounding, and
fixed-damage regression. Schema/migration impact: none. Dependency impact: none. RNG order is
unchanged; accuracy uses the queried integer threshold before the existing single draw. Battle events
and existing effect traces are unchanged; `BattleController.QueryTrace` adds deterministic query
evidence. Manifest status remains 57/937 because this foundation alone does not close a new reference
formula family; regeneration was byte-identical. Verification: `dotnet build CreatureGameMaker.slnx`
passed with 0 warnings/errors; `dotnet test CreatureGameMaker.slnx --no-build` passed 1,141 tests
(949 Core, 104 Creator, 21 Runtime, 67 Tools). Focused review findings (critical-chance clamp and
fixed-family final-query coverage) were fixed; verdict GO. Next eligible package: **15D-1**.

Progress (2026-07-14): **15C-2 COMPLETE.** The battle spec and effect catalog now publish the closed
HP/status formula registry and exact integer semantics for HP thresholds, current/missing-HP linear
power, authored HP bands, persistent/volatile status predicates and counts, status-conditioned
secondary chance, HP averaging and source matching, current/max-HP mutation, and cannot-KO floors.
All formula power enters the 15C-1 base-power query; secondary chance has its own clamped query ID;
source-matching HP reduction uses the shared damage path and observes type immunity; and formula
damage is visible to Smart AI without bespoke move identities. Compiler validation rejects unknown,
duplicate, malformed, mis-targeted, and invalidly composed formula ops. Resolver evidence covers
threshold±1, every band edge, 1/full HP, persistent and every supported volatile predicate,
no-status/mismatch, per-target doubles snapshots, rounding/clamping, immunity, damage bookkeeping,
cannot-KO interaction, unchanged chance-draw counts, events, and query/effect traces. The generated
catalog adds 15 newly complete entries and attaches HP/status vectors to three already-certified
topology entries, for 937 inventoried / 72 certified with unchanged corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`. Schema/migration and
dependency impact: none. `dotnet build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/
errors; `dotnet test CreatureGameMaker.slnx --no-build` passed 1,258 tests (1,047 Core, 104 Creator,
21 Runtime, 86 Tools); the complete Core and Tools coverage runs exercised 383/383 newly coverable
production lines (100%); deterministic regeneration was byte-identical; and `git diff --check`
passed. The closeout review found and fixed current-HP damage bypassing its cannot-KO floor,
source-matching HP damage bypassing immunity/bookkeeping, and Smart AI omitting formula previews;
verdict GO. Next eligible package: **15C-3**.

Progress (2026-07-14): **15C-3 COMPLETE.** The battle spec and effect catalog now lock exact linear
and banded effective-speed ratios plus direct and ratio-based physical metrics. Species schema v5
adds positive hectogram weight and decimeter height with a v4-to-v5 migration; battle instances
carry those immutable base values while the existing 15F-1 overlay store supplies effective metric
replacements. `speedRatioPower`, `metricBandPower`, and `metricRatioPower` compile through strict
typed validation, replace base power through the shared 15C-1 query trace, and are scored by Smart
AI through the same formula path. Boundary evidence covers every speed, weight, and weight-ratio
band edge, stage/paralysis inputs, base and overlaid values, grounded/airborne invariance, maximum
integers, zero denominators, checked overflow, resolver damage/traces, AI choice, schema round-trip,
migration, and validation. The generated catalog adds the two fully closed speed rows for 937
inventoried / 74 certified with unchanged corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; four weight-formula rows
remain uncertified because their separate semi-invulnerability, Minimize, or transformation gates
belong to later packages. Dependency impact: none. `dotnet build CreatureGameMaker.slnx
--no-restore` passed with 0 warnings/errors; `dotnet test CreatureGameMaker.slnx --no-build` passed
1,304 tests (1,091 Core, 104 Creator, 21 Runtime, 88 Tools); the complete Core coverage run exercised
75/75 coverable lines in the new formula service (100%); deterministic regeneration was
byte-identical; and `git diff --check` passed. The focused closeout review corrected eager overlay
resolution for unrelated moves, routed stat overlays into effective Speed, and verified that
later-dependent weight rows remain uncertified;
verdict GO. Next eligible package: **15C-4**.

Progress (2026-07-14): **15C-4 COMPLETE.** The battle spec and effect catalog now lock typed
`prevented`/`failed`/`missed`/`succeeded`/`connected` outcomes, bounded current/previous-turn attempt
records, creature-connected and side-attempted streaks, actual moved-before/after state, immediately
previous failure, and previous-turn ally-faint memory. `consecutivePower` supplies capped exponential
or linear replacement power and `historyPower` supplies a conditional rational multiplier through
the shared 15C-1 BasePower query. Singles and doubles use the same controller lifecycle; every faint
source uses one history-aware path, including repeat replacement entry-hazard faints. Switch, faint,
different action, prevention, failure, miss, turn-gap, Pass/item, and side/creature ownership reset
semantics are deterministic. Smart AI previews only public effective Speed under the locked
equal-priority/tie-neutral rule while reusing visible streak/faint memory; private submitted action
kinds remain resolver-only. The generated catalog adds `move-0371`, `move-0514`, `move-0707`, and
`move-0915` for 937 inventoried / 78 certified with unchanged corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; consecutive rows whose
sound/slicing interactions are still open remain uncertified. Schema/migration and dependency
impact: none. Verification: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore`
passed with 0 warnings/errors; `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build
--no-restore` passed 1,333 tests (1,116 Core, 104 Creator, 21 Runtime, 92 Tools); the complete Core
coverage run exercised 167/169 changed instrumented production lines (98.82%); deterministic
regeneration was byte-identical; and `git diff --check` passed. The focused closeout review found and
fixed side-chain prior-turn counting, move-gate/Protect failure classification, source-faint state
recreation, and target-fraction damage bookkeeping; verdict GO. Next eligible package: **15G-2**.

Progress (2026-07-15): **15C-5 COMPLETE.** The battle spec and effect catalog now lock six typed,
mutually exclusive replacement-power ops: party counts over living/fainted/contributing slots,
current/missing friendship, PP bands captured before or after the actual spend, positive sums across
all seven stat stages, effective held-item `flingPower`, and authored-order weighted random tables.
`PartyResourceFormulas` supplies checked/clamped integer math and immutable action inputs;
`BattleController` routes every result through the shared BasePower query, fails unavailable item
data after PP/`MoveUsed` but before accuracy or RNG, and selects random power once per action after
accuracy for reuse across spread targets and multi-hits. `EffectTraceKind.PowerTable` records the
selection; Smart AI uses its own visible party/item data plus the exact floored weighted mean without
an extra formula draw. Runtime battle boot now supplies the exported item catalog. Friendship is
carried from the existing creature instance, so schema/migration and dependency impact are none.

Changed production files: `PartyResourceFormulas.cs`, `MoveEffects.cs`, `MoveCompiler.cs`,
`HpStatusFormulas.cs`, `BattleController.cs`, `BattleCreature.cs`, `BattleEvents.cs`,
`EffectTrace.cs`, `PhysicalMetricFormulas.cs`, `SmartAi.cs`, `MoveRules.cs`,
`ExportedGameBoot.cs`, and `MoveConformanceNormalizer.cs`. Focused Core and Tools tests add compiler,
formula-boundary, party-duplicate/filter, PP charge/spend, stage, item failure/suppression, exact RNG
endpoint/no-draw/spread/multi-hit/replay, AI, validation, and generated conformance evidence. The
generated catalog adds six fully closed party/resource rows for 937 inventoried / 84 certified
with unchanged corpus digest `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`;
Magnitude, Present, Fling, Beat Up, and Last Respects remain uncertified because their non-formula
behavior or cumulative-faint history belongs to later packages. Verification:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore`
passed with 0 warnings/errors; `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build
--no-restore` passed 1,382 tests (1,159 Core, 104 Creator, 21 Runtime, 98 Tools); the complete dirty-
worktree Core coverage run exercised 893/917 changed instrumented production lines (97.38%); the
audit command reported 84 certifications and regenerated both artifacts byte-identically; and
`git diff --check` passed. Focused
closeout review found and fixed missing MoveRule registration, zero-power friendship boundaries,
single-entry trace bounds, and checked linear overflow/clamping; verdict GO. Next eligible package:
**15E-3**.

Progress (2026-07-17): **15C-6 COMPLETE.** `BattleDamageQueries` now produces one immutable, exact
identity/damage-query result shared by the resolver and Smart AI. Its precedence is authored move
identity, effective-value overlays, then eligible weather/terrain type replacement; it resolves
natural/effective environment, fixed or higher-offense class, owner-qualified attacking/defending
stats, effective creature types, exact STAB, standard/inverse/additional/defender-override
effectiveness, and the snapshotted spread flag. `BattleController` consumes the result before final
immunity and combat RNG in ordinary, fixed, level, OHKO, and target-HP formula damage, uses effective
class/type for screens and damage memory, and records a typed per-target query trace. Self HP costs
remain independent of opponent effectiveness. Smart AI uses the same service and effective overlay,
field, stat, type, spread, and screen inputs. No named-move branch, schema change, dependency, or new
certification was required.

Changed production files: `BattleDamageQueries.cs`, `BattleController.cs`, `SmartAi.cs`,
`MoveCompiler.cs`, `MoveEffects.cs`, `PhysicalMetricFormulas.cs`, and `TypeChart.cs`. Owning spec and
test strategy changes are in `BATTLE_SYSTEM_SPEC.md`, `EFFECT_TYPES_CATALOG_v0_5.md`,
`BATTLE_AI_SPEC.md`, and `TESTING_STRATEGY.md`. `BattleDamageQueryTests.cs` and
`damage-query.golden` cover compiler/direct-boundary rejection, identity precedence and environment,
effective overlays, staged class selection, owner-qualified stats, standard/inverse/additional/type-
override effectiveness, STAB sources, final immunity and skipped RNG, self-HP isolation, doubles
spread snapshots, resolver/AI parity, and replay-stable output. Verification:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors;
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build --no-restore` passed 1,653 tests
(1,430 Core, 104 Creator, 21 Runtime, 98 Tools); the move audit reported 937 inventoried / 84
certified with corpus digest `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`
and byte-identical manifest/definitions hashes; and `git diff --check` passed. Focused review found and
fixed self-HP effectiveness leakage, field-type preview ambiguity, public typed-input validation,
formula-damage effectiveness observability, and effective-class failure bookkeeping; verdict GO.
Next eligible package: **15C-7 accuracy, critical, priority, final-damage, and healing modifiers**.

Progress (2026-07-17): **15C-7 COMPLETE.** `BattleActionQueries` is the shared chance-free move
modifier service for Accuracy, CriticalChance, Priority, FinalDamage, and Healing. The closed
`queryModifier`, `accuracyRule`, and `nextQuery` ops compile through strict query/operation/operand/
duration/target validation. Accuracy composes weather, source/target stages, Gravity, explicit
bypass or ignored evasion, and source/target-bound next-accuracy state in one trace. Critical chance
uses the exact stage table and target-side guard hooks; an eligible next-critical condition resolves
without a critical draw and remains present when immunity or a later guard suppresses eligibility.
Priority is snapshotted through the same helper before scheduling. Standard, fixed, level, OHKO, and
counter damage enter the final-damage query without allowing a modifier to resurrect type immunity.
Move-originated direct, drain, and HP-fraction healing enter the healing query before missing-HP
clamping. Smart AI reuses the same helpers and exact normal/critical expectation without adding RNG.

Changed production files: `BattleActionQueries.cs`, `BattleController.cs`, `MoveCompiler.cs`,
`MoveEffects.cs`, and `SmartAi.cs`. Owning contracts were reconciled in `BATTLE_SYSTEM_SPEC.md`,
`EFFECT_TYPES_CATALOG_v0_5.md`, `BATTLE_DAMAGE_CALC.md`, `BATTLE_AI_SPEC.md`, and
`TESTING_STRATEGY.md`. Focused evidence in `BattleActionQueryTests.cs`, the checked-in
`action-query.golden`, and adjusted critical-expectation regression tests covers strict compilation,
all Accuracy/Evasion stage pairs, bypass/ignore-evasion, one-shot lifecycle and source/target
isolation in singles/doubles, exact critical stages and guarded preservation, priority clamping/order,
ordinary/fixed final-damage floor/cap and immunity, healing multiplication/block/clamp, resolver/AI
parity, replay, traces, and skipped-draw order. Schema/migration and dependency impact: none. No
named-move branch or new move certification was required.

Verification: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0
warnings/errors; `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build --no-restore` passed
1,667 tests (1,444 Core, 104 Creator, 21 Runtime, 98 Tools); the move audit reported 937 inventoried /
84 certified with corpus digest `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`
and byte-identical manifest/definitions/decisions hashes; and `git diff --check` passed. Focused review
found and fixed fixed-damage immunity resurrection, overbroad next-accuracy targeting, fractional
critical-operand rejection, and missing doubles source/target proof; verdict GO. Next eligible
package: **15D-2 action gates and recharge**.

Exit: formula families have table tests, exact rounding, compiler/validation coverage, and all
affected moves receive conformance cases.

#### 15D — Action timing, locks, and queued effects

Primary groups: turn timing/queued gates (64), volatile/status lockouts (49), move call/copy (13).

- Recharge, delayed attacks/heals, charge variants, semi-invulnerability, first/last-turn gates.
- Taunt, Encore, Disable, Torment, Imprison, Heal Block, throat/sound locks, perish/yawn/nightmare.
- Call/copy/repeat/force/replace moves and exclusion tags.
- One deterministic intent queue; no move-specific flags.

Ordered feature packages:

1. **15D-1 — Typed intent queue (`IMPLEMENTED`; prerequisite 15B-6).** Lock one queue record with stable
   sequence number, due turn, timing checkpoint, owner scope and creature identity, target policy
   (`snapshotSlot`, `liveSlot`, `source`, `side`, `field`), typed payload, source move metadata,
   ruleset, and switch/faint/end cleanup. Ordering is due turn → checkpoint order → insertion sequence.
   Preview entries without mutation during action admission; consume immediately before execution;
   newly queued work at the current checkpoint waits for the next matching checkpoint. Put a hard
   per-checkpoint execution cap derived from queue length at entry and trace deferrals. **Acceptance:**
   ordering, same-checkpoint insertion, snapshot/live replacement, cancellation/transfer matrix,
   serialization/debug snapshot, no unintended draws, and deterministic replay.
2. **15D-2 — Action gates and recharge (`IMPLEMENTED`; prerequisite 15D-1).** Extend the shared legality
   service with first-action, cannot-repeat, prior-failure, moved-before/after, target action class,
   interrupt-if-hit, recharge, and generic next-action block. Selection-time gates reject admission;
   execution-time invalidation spends no PP unless the locked gate row explicitly occurs after
   `MoveUsed`. Recharge is creature-owned, blocks every selectable action except forced pass, consumes
   once, and clears on switch/faint. Preserve the implemented `moveGate`/`queueActionGate` behavior.
   **Acceptance:** selection/execution boundary, PP/RNG/event matrix, doubles actor isolation,
   recharge switch/faint cleanup, and conformance vectors.
3. **15D-3 — Charge and semi-invulnerability (`IMPLEMENTED`; prerequisites 15D-1 and 15C-7 query seam).**
   Lock charge/release payload, source creature ownership, stored target policy, first-turn PP payment,
   skip-charge predicate, cancellation, and semi-invulnerable tags (`air`, `underground`, `underwater`,
   `vanished`). A charge action pays PP/announces once; release does not pay again. Hit exceptions are
   data tags with optional power modifiers, evaluated in accuracy then damage query order. Switch,
   faint, forced action, and battle end clear the state. **Acceptance:** normal/skip/cancel/release,
   target replacement, each hit/bypass tag, PP and RNG counts, and charge golden.
4. **15D-4 — Delayed slot actions (`IMPLEMENTED`; prerequisites 15D-1 and 15B-6).** Lock delayed damage,
   healing, status, and replacement payloads with stored source level/stats/type/class/ruleset where
   calculation requires a snapshot and live destination slot where replacement should receive it.
   Delayed work resolves at its timing checkpoint even if the source switched or fainted unless its
   row declares source-required; failure is eventful and never refunds original PP. Multiple entries
   on one slot resolve insertion order. **Acceptance:** source gone, target replacement/empty, same-slot
   multiples, immunity/heal-block at execution, stored-versus-live query proof, and delayed golden.
5. **15D-5 — Multi-turn locks and forced execution (`IMPLEMENTED`; prerequisites 15D-1/2).** Lock duration
   draw timing/range, selected move/target ownership, ramp step/cap, forced-repeat legality,
   interruption, post-lock confusion, and cleanup. The first execution pays normal PP; each forced
   repeat pays PP only when its registry row says so; no legal repeat ends the lock visibly. Conditions
   cannot recursively force themselves in the same action. **Acceptance:** min/max duration, ramp/cap,
   miss/fail/disable/no-PP interruption, switch/faint cleanup, exact draws, and termination golden.

Progress (2026-07-18): **15D-5 COMPLETE — focused review: GO.** `multiTurnLock` now compiles to one
bounded typed profile covering inclusive duration, stored move/selection ownership, repeat-PP policy,
connected-use power step/cap, failure policy, end effect, and optional keyed power boost. The existing
turn scheduler replaces submissions with the stored action before admission; it emits typed start,
continue, and closed-reason end events; uses action-history results for interruption; and cleans up on
no PP, forced switch, or faint. Fixed five-use ramps compose ordinary `BasePower` query modifiers
through 16x without duration RNG. `multiTurnPowerBoost` supplies the reusable switch-scoped defense-
boost interaction. Smart AI exposes one zero-RNG `forcedRepeat` candidate. Seven generated vectors
cover `move-0037`, `move-0080`, `move-0111`, `move-0200`, `move-0205`, `move-0301`, and `move-0833`;
the three newly normalized rows advance strict certification to 121/937 with unchanged corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`.

Files: `src/Cgm.Core/Battle/BattleCreature.cs`, `BattleController.cs`, `BattleEvents.cs`,
`MoveCompiler.cs`, `MoveEffects.cs`, `SmartAi.cs`; `src/Cgm.Tools/MoveAudit/MoveConformanceNormalizer.cs`;
`tests/Cgm.Core.Tests/Battle/BattleMultiTurnLockPackageTests.cs`, `BattleRampageTests.cs`, and
`Goldens/multi-turn-lock-termination.golden`; `tests/Cgm.Tools.Tests/MultiTurnLockConformanceTests.cs`;
the battle/effect/AI/testing/scope/plan contracts; and generated conformance decisions, definitions,
and manifest. Schema/migration impact: none. Dependency impact: none. Verification:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors;
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build --no-restore` passed 1,799 tests
(1,530 Core, 104 Creator, 21 Runtime, 144 Tools). Focused multi-turn Core tests passed 31/31 and
generated conformance passed 7/7. Repeated regeneration was byte-identical (`manifest`
`c74a9afd895858a606651e883b4b0de9f374efa3e91fa9922e3239397cb8d5e9`, `definitions`
`79fde2f69c99e313cd9129e87ca769f60e277ff1b95bb835545c44e614c86947`, decisions
`d420cf0104b903808174e5111ee3d5522868aa7e56bb608cc4e2f453d2e47c21`). Next eligible package:
**15D-6 selection lockouts and legal fallback**.
6. **15D-6 — Selection lockouts and legal fallback (`IMPLEMENTED`; prerequisites 15D-2 and 15E-1
   condition store).** Implement Disable, Encore, Taunt, Torment, Imprison, heal/item/sound locks,
   infatuation and related action filters as data-defined conditions consumed by one legality service.
   Lock source, duration tick, duplicate policy, affected action tags, bypass, and cleanup per registry
   row. If no ordinary move is legal, expose one generic fallback action compiled from ruleset data;
   it is not a named move special case. **Acceptance:** each filter allow/block matrix, overlapping
   precedence, all-moves-blocked fallback, duration/refresh/switch cleanup, AI legality parity.

Progress (2026-07-18): **15D-6 COMPLETE — focused review: GO.** One shared
`BattleActionLegality` result now owns PP, held-choice, condition-filter, move-tag, opposing-source,
and battle-item admission for controller availability and Smart AI. Nine shared creature-condition
rows cover stored-move disable/force, status/last/source-known/tag/item filters, and the execution-
time chance gate with typed lifecycle/source cleanup. `UseFallback` is exposed only when no ordinary
move remains and compiles from the active ruleset profile; it is not an ordinary move slot or named
branch. Multi-turn repeats recheck condition filters and terminate visibly when blocked. `moveTags`
and `actionFilter` compile from the open effect payload without schema or dependency changes. Nine
numeric reference rows normalize and certify, advancing strict certification from 121 to 130/937
with unchanged corpus digest `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`.

Changed production files: `ActionFilterConditions.cs`, `BattleFallbackRules.cs`,
`BattleConditions.cs`, `BattleConditionRegistry.cs`, `BattleController.cs`, `BattleCreature.cs`,
`BattleEvents.cs`, `MoveCompiler.cs`, `MoveEffects.cs`, `SmartAi.cs`, and
`MoveConformanceNormalizer.cs`. Tests: `BattleActionFilterTests.cs`, compiler/Smart-AI regressions,
and `ActionFilterConformanceTests.cs`; generated decisions, definitions, and manifest updated.
Schema/migration impact: none. Dependency impact: none. Verification:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors;
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build --no-restore` passed 1,817 tests
(1,538 Core, 104 Creator, 21 Runtime, 154 Tools). Focused Core D6 tests passed 9/9 and generated
conformance passed 9/9. Repeated regeneration was byte-identical (`manifest`
`557417568681ef56b48d8a3f60096fee579059f733dc0bfe52c4e0f34213050f`, `definitions`
`63916c99a6f8dbb64212fa2817817e0db29b6aa5e5bcfe8561c2503cdec74e41`, decisions
`a2f78c585e0fa17b81c7cfc5d11d9fd013020d7dee26986a39bd96cdc7fd824d`). Focused scope,
architecture, determinism, schema, compiler/resolver/event/AI, and generated-artifact review is GO.
Next eligible package: **15D-7 move references and turn-order intents**.
7. **15D-7 — Move references and turn-order intents (`IMPLEMENTED`; prerequisites 15B-4, 15C-7, 15D-1).**
   Lock selectors for known, target-known, last-used, party-known, random pool, environment pool, and
   explicit move reference; exclusion tags; authored-order candidate list; one draw only for multiple
   candidates; source versus called move PP ownership; target revalidation; event attribution; and a
   maximum nested execution depth of 8. After-you/quash/helping/instruct-style intents mutate the
   current scheduled-action record through typed priority/order flags; executed actions cannot be
   scheduled again. **Acceptance:** empty/single/multiple pool, exclusion, PP/event ownership, target
   invalidation, depth/loop termination, doubles order conflicts, and exact RNG/event golden.

Progress (2026-07-18): **15D-7 COMPLETE — focused review: GO.** `callMove` now resolves the closed
known/target/last/party/authored/environment/explicit selector vocabulary through one stable
candidate path, a battle-owned compiled move catalog, exclusion tags, caller/called PP policy,
live target revalidation, attributed call edges, final-move history, and an eight-edge execution
ceiling. Empty/single pools draw zero times and multi-candidate pools draw once. `turnOrderIntent`
mutates only pending current-turn records for act-next, act-last, power boost, or one repeat; an
executed/absent target fails explicitly. Generic reciprocal `pairedAction` profiles implement
follow-up and combined ally actions, shared power/type overrides, and the existing three paired
side-condition results without move-ID branches or private timers. Smart AI previews only a unique
visible called candidate and otherwise records neutral named evidence without reading submitted
actions. Runtime supplies the compiled project move catalog to both resolver and AI. Focused review
found and fixed ordinary-move target revalidation leaking into charge/delayed snapshots, external
called moves being treated as source-owned slots, catalog-definition PP mutation risk, pairing
before move gates, and active-user leakage from the party-known selector. The generated catalog adds
8 complete rows (`move-0267`, `move-0270`, `move-0495`, `move-0496`, `move-0511`, `move-0518`,
`move-0519`, and `move-0520`) for 937 inventoried / 138 certified with unchanged corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; repeated regeneration was
byte-identical (manifest `9f1ddc3e529ca3b1fef44e358c8c8adae5e46385c51db61df21bc6621ccea787`,
definitions `55719c1a6496a47286e2724b9d6e761a429b707c49da136fae6e08916d7c21b7`, decisions
`6e47845ffeb398b64be3ccc243296111dd9e74393c55d09252bd6304e8c5b77a`). Schema/migration impact:
none. Dependency impact: none. Verification: full solution build passed with 0 warnings/errors;
focused package tests passed 14/14, generated conformance passed 8/8, and the final full solution
passed 1,840 tests (1,552 Core, 104 Creator, 21 Runtime, 163 Tools). Next eligible package:
**15F-2 held-item mutation**.

Progress (2026-07-14): **15D-1 COMPLETE — focused review: GO.** The battle spec and effect catalog
now lock the typed queue record, stable sequence and checkpoint order, owner/target policies,
preview/consume boundary, same-checkpoint deferral cap, cleanup matrix, target-resolution matrix,
trace metadata, and current `skipAction` payload semantics. `BattleIntentQueue` validates immutable
typed records, previews without mutation, atomically consumes exact previews, rejects stale/foreign
previews, resolves snapshot/live/source/side/field targets, applies switch/faint/end cleanup, and
exports a stable JSON-serializable debug snapshot. The existing `queueActionGate` effect now enqueues
slot-owned intents through that shared path in singles and doubles. Whole-turn validation completes
before consumption, so a rejected submission leaves due work pending; multiple due gates consume in
sequence but emit one `ActionSkipped` per topology-ordered slot, spend no PP, and draw no RNG.
Enqueue/consume/defer/cancel/transfer traces carry sequence, checkpoint, payload, and neutral source
move metadata. Focused review found and fixed stale-preview acceptance after owner transfer and
missing invalid slot/side validation. Schema/migration impact: none. Dependency impact: none. No new
move cohort is certified because 15D-1 is the shared foundation; deterministic regeneration remained
937 inventoried / 57 certified with digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`. Verification:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors;
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` passed 1,151 tests (959 Core,
104 Creator, 21 Runtime, 67 Tools); focused intent/action-gate tests passed; regeneration was
byte-identical; and `git diff --check` passed. Next eligible package: **15E-1**.

Progress (2026-07-17): **15D-2 COMPLETE — focused review: GO.** The battle/effect/AI/testing specs
now lock one typed `moveGate` registry across selection, before-move, and after-`MoveUsed` timing;
source-history, source/target order, target action-class, and received-damage predicates; plus one
creature-owned `recharge` path through the existing typed intent queue. Selection rejection is
atomic. Before-move rejection spends no PP and draws no RNG; after-`MoveUsed` rejection records the
use and spent PP without resolving targets. Turn-plan move classes and current-turn positive direct
damage are immutable gate inputs. Recharge is queued once only after positive direct damage, blocks
move/switch/item/form/pass admission through the shared pre-action skip, and cancels on switch or
faint; the existing slot-owned generic gate retains its stay/persist behavior. Smart AI filters only
source-local known gates and never reads the opponent's submitted action. Focused coverage proves
compiler boundaries, every gate class, order/history aging, PP/RNG/events, miss/immunity/protection,
all action kinds, switch/faint cleanup, doubles isolation, AI fairness, replay, and golden output.
The generated catalog adds 10 complete reference rows (`move-0063`, `move-0252`, `move-0264`,
`move-0307`, `move-0308`, `move-0338`, `move-0389`, `move-0416`, `move-0660`, and `move-0704`) for
937 inventoried / 94 certified with unchanged corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; repeated full-argument
regeneration produced byte-identical manifest and definitions. Schema/migration impact: none.
Dependency impact: none. Verification: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx
--no-restore` passed with 0 warnings/errors; `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx
--no-build --no-restore` passed 1,706 tests (1,472 Core, 104 Creator, 21 Runtime, 109 Tools); focused
gate/history/AI/doubles tests passed 98/98, focused conformance tests passed 14/14, and the combined
topology/action-gate conformance set passed 68/68. Review found and fixed undefined direct damage-
class acceptance and the topology harness's missing required-damage setup. Next eligible package:
**15D-3 charge and semi-invulnerability**.

Progress (2026-07-17): **15D-3 COMPLETE — focused review: GO.** The battle/effect/AI/testing specs
now lock typed charge metadata, creature-owned queued release, authored live/snapshot target policy,
first-turn-only PP payment, weather skip-charge, charge-start stat changes, four semi-invulnerable
states, and data-defined hit/power exceptions. Release overrides the submitted action without a
second PP spend; status, confusion, flinch, switch, faint, forced switch, and battle end clear the
state and queued intent. Singles and doubles preserve live versus snapshotted occupants, random
targets draw only when release materializes them, and semi-invulnerable misses consume no accuracy
RNG. Matching exceptions run through the shared Accuracy then BasePower queries, with Smart AI
forced-release and visible-state legality parity. Focused review found and fixed the singles adapter
discarding queued snapshot identity, then added explicit persistent-status, confusion, and flinch
interruption tests. The generated catalog adds 18 complete reference rows for 937 inventoried / 112
certified with unchanged corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; repeated regeneration was
byte-identical (`manifest` `f78360314f5a3c80442b39912f06516ee03cec39b63546a429680d9bcf7dcb51`,
`definitions` `2b73ba31ebf090300862fedf307c3d007016cfd26a9e5abf7e999d131495d413`,
`decisions` `f6b438f65ff17dc35c18f894d6d470f805192b9e735a0ca3d24f2e24076db2dd`).
Schema/migration impact: none. Dependency impact: none. Verification:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors;
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build --no-restore` passed 1,751 tests
(1,495 Core, 104 Creator, 21 Runtime, 131 Tools); the full Battle suite passed 1,211/1,211 and the
focused charge package passed 23/23. Next eligible package: **15D-4 delayed slot actions**.

Progress (2026-07-17): **15D-4 COMPLETE — focused review: GO.** The shared typed intent queue now
owns delayed fixed-power damage, delayed healing, delayed persistent status, and switch-in
replacement restoration. Damage snapshots source calculation inputs but re-evaluates the live slot,
defense, immunity, and field/side damage hooks; healing/status revalidate their execution-time
target; source-required, empty/replaced target, slot uniqueness, insertion order, hazard-first
replacement, no-reserve, and deferred replacement failures are eventful and deterministic. The
closed compiler vocabulary, diagnostic payload snapshot, ordinary damage/history/heal/status/PP
events, effect traces, seeded golden, and Smart-AI named components use existing shared paths with no
private scheduler or move-ID branch. Review found and fixed mutable healing modifier input,
nondeterministic modifier insertion numbering, overflow-prone healing multiplication, and silent
acceptance of unsupported dynamic/effectiveness power formulas. The generated catalog adds six
complete rows (`move-0248`, `move-0273`, `move-0281`, `move-0353`, `move-0361`, and `move-0461`) for
937 inventoried / 118 certified with unchanged corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; repeated regeneration was
byte-identical (`manifest` `744fbc9c56ae200e7e12ce16ec064cdb45f2ceb5e29931d8a9c4e8c8484907a5`,
`definitions` `e13f649c771403a7801c4babb744ee6b2eeebd11b755dc245a032e33c7dae914`,
`decisions` `6428acd90e915588eb28a843cbf38e7ec4ec3774f81e7bde77cbebc21b9134ee`).
Schema/migration impact: none. Dependency impact: none. Verification:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors;
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build --no-restore` passed 1,778 tests
(1,516 Core, 104 Creator, 21 Runtime, 137 Tools); the full Battle suite passed 1,230/1,230, focused
delayed Core tests passed 18/18, and generated delayed conformance passed 6/6. Next eligible package:
**15D-5 multi-turn locks and forced execution**.

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

1. **15E-1 — Scoped condition model and stores (`IMPLEMENTED`; prerequisite 15B-6).** Lock definition and
   instance fields: generic ID, scope, owner, source slot/creature, applied turn/sequence, duration,
   counters, tags, hook list, stacking key/policy (`reject`, `refresh`, `replace`, `stack`), maximum
   stacks, and switch/faint/battle-end cleanup. Stores exist for creature, side, slot, field, weather,
   terrain, and room; one condition cannot be silently stored in a different scope. Durations decrement
   at the registry row's named checkpoint and expire after that checkpoint's hooks finish. **Acceptance:**
   every scope, duplicate policy, duration 1/N, source identity, cleanup/transfer matrix, stable
   enumeration, strict validation, events/traces, and round-trip if serialized shape changes.
2. **15E-2 — Hook dispatcher (`IMPLEMENTED`; prerequisite 15E-1).** Lock checkpoints from selection through
   end turn and collect hooks in checkpoint → hook priority descending → scope order (field, side,
   slot, creature, ability, item, move) → owner topology order → condition sequence. Hooks return typed
   query modifiers, filters, or intents; they do not mutate while enumeration is active. Enqueue
   mutations for the checkpoint tail. Limit one hook invocation per `(action, checkpoint, instance,
   payload)` and cap nested emitted intents at 64 with a visible engine error. **Acceptance:** complete
   order golden, add/remove during dispatch, duplicate suppression, cap failure, no dictionary drift,
   and identical resolver/AI query collection.
3. **15E-3 — Weather, terrain, room, gravity, and sport families (`COMPLETE`; prerequisites 15E-1/2
   and 15C query seam).** Build these as field-scoped condition definitions. Each registry row locks
   duration/default, replace/coexist rules, damage/accuracy/status/heal/type/grounded/order hooks,
   residual timing, source, and removal. Weather and terrain each permit one effective instance;
   room/gravity/sport coexist when tags differ. Reapplying the same instance refreshes only when the
   row permits. **Acceptance:** start/replace/refresh/expire, every query hook present/absent, residual
   ordering, field coexistence, ruleset difference vectors, and family goldens.
4. **15E-4 — Side conditions and guards (`COMPLETE`; prerequisites 15E-1/2).** Lock screens, status/
   stage guards, speed/order modifiers, critical guard, pledges, and side-wide protection as side
   conditions with exact duration, doubles multiplier, bypass tags, stacking, source, and removal.
   A side hook evaluates once per affected target but owns one shared duration/counter. **Acceptance:**
   singles/doubles values, bypass, duplicate/refresh, opponent versus owner scope, removal, expiration,
   and AI-visible query outcomes.
5. **15E-5 — Entry hazards (`COMPLETE`; prerequisites 15E-1/2 and 15B-6 replacement).** Lock generic
   hazard layer count/max, grounded filter, switch-in checkpoint, fraction/stage/status payload,
   type/effectiveness use, absorption predicate, removal tags, and source credit. Entry hooks execute
   after assignment in slot order and condition sequence; a faint triggers the replacement loop only
   after the complete entry-hook batch. **Acceptance:** 0/max layers, grounded/airborne, immunity,
   absorption, removal, two-slot entry order, repeat faint/replacement, and hazard golden.
6. **15E-6 — Protect and contact-block families (`COMPLETE`; prerequisites 15E-1/2).** Lock protect
   scope, accepted move tags, bypass, chain counter ownership, success fraction and RNG draw point,
   reset conditions, side variants, and ordered contact-block payloads. Standard chain probability
   is a ruleset fraction queried once on use; guaranteed first use draws only if the profile requires
   it. Spread moves evaluate protection per target. **Acceptance:** first/repeated/reset boundaries,
   exact chance draws, bypass and non-contact cases, spread mixed targets, contact punishment order,
   source faint handling, and golden.
7. **15E-7 — Generic condition cleanup/transfer/swap (`COMPLETE`; prerequisites 15E-1).** Implement
   selectors by scope, tag, source, owner, and condition ID plus operations remove, transfer, and
   atomic side swap. Preserve instance duration/counters/source unless the operation's typed params
   request reset. Validate incompatible source/destination scopes before mutation. **Acceptance:**
   no-match visible no-op policy, partial/all tag selection, source filtering, atomic failure,
   duration/source preservation, slot/side swap, and proof that no helper enumerates content names.

Progress (2026-07-14): **15E-1 COMPLETE — focused review: GO.** The battle spec and effect catalog
now lock condition IDs, the complete hook-ID list, immutable definitions, instances, seven exact
stores (`field`, `weather`, `terrain`, `room`, `side`, `slot`, `creature`), scope-owner/source tuples,
duration completion, reject/refresh/replace/stack behavior, topology-stable enumeration, lifecycle
events/traces, and switch/faint/end cleanup. `BattleConditionRegistry` strictly validates and
normalizes definitions; `BattleConditionStores` applies them atomically, preserves source and applied
metadata, ticks finite durations only after their named checkpoint, follows/removes creature owners,
and clears every scope at battle end. Neutral tests cover all scopes, every duplicate policy,
duration 1/N and variable durations, active/inactive source and owner identity, cleanup/transfer,
invalid tuples/enums/tags/counters/hooks/families, deterministic events/traces, and replay-stable JSON.
The general condition test matrix's hook execution is owned by 15E-2 and removal-by-tag/explicit
cross-owner transfer/side swap are owned by 15E-7; 15E-1 implements only its prerequisite model and
lifecycle, not those later behaviors. Focused review fixed null lifecycle trace transitions, optional
inactive-creature ownership, and primary-type file layout before returning GO. Schema/migration
impact: none. Dependency impact: none. RNG impact: none; the store has no RNG dependency. No move
cohort is newly certified because definitions and hook payloads arrive in later packages;
deterministic regeneration remained 937 inventoried / 57 certified with digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`. Verification:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors;
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` passed 1,163 tests (971 Core,
104 Creator, 21 Runtime, 67 Tools); the focused condition suite passed 12 tests and the Battle filter
passed 694 tests; regeneration was byte-identical; and `git diff --check` passed. Next eligible
package: **15E-2**.

Progress (2026-07-14): **15E-2 COMPLETE — focused review: GO.** `BattleHookDispatcher` now owns the
typed Phase 15E collection path while retaining the earlier Battle v6 ability/item methods as
compatibility adapters until their owning mechanic packages migrate. A collection captures all
registrations before filtering, validates condition hook membership and scope-exact owners, and
orders by checkpoint, descending priority, field/side/slot/creature/ability/item/move scope, owner
topology, sequence, stable instance ID, and payload index. Exact duplicate invocation identities run
once and trace suppression; conflicting duplicates fail admission, so caller or dictionary order
cannot select behavior. The closed output union carries query modifiers, allow/deny filters, or
validated intent requests. Query collection is immutable and shared by resolver/AI consumers; one-
shot completion atomically enqueues intents at checkpoint tail. Root-action intent accounting is
carried across nested collections and a 65th intent fails the whole snapshot with a typed visible
`HookDispatchFailed` event and no queue mutation. Conditions or registrations added/removed after
capture wait for the next checkpoint. Neutral tests cover the complete order and every checkpoint,
condition add/remove snapshot behavior, duplicate suppression/conflict rejection, exact/capped
intent budgets, atomic one-shot tail completion, resolver/AI query parity, dictionary-order drift,
and strict registration/condition admission. Concrete weather, terrain, room, side, hazard, protect,
item, ability, and move hook payload families remain with 15E-3 through 15E-7 and 15F; no content-name
branches or parallel stores were added. Schema/migration impact: none. Dependency impact: none. RNG
impact: none; collection and completion draw no RNG. No move cohort is newly certified because this
is the shared dispatcher foundation; deterministic regeneration remained 937 inventoried / 57
certified with digest `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f` and byte-identical outputs.
Verification: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0
warnings/errors; `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` passed 1,173 tests
(981 Core, 104 Creator, 21 Runtime, 67 Tools); the focused dispatcher suite passed 9 tests and the
combined dispatcher/legacy-hook/condition/intent/query filter passed 51 tests; regeneration and
`git diff --check` passed. Next eligible package: **15F-1**.

Progress (2026-07-15): **15E-3 WEATHER SHARED-STORE CHECKPOINT COMPLETE; PACKAGE REMAINS IN
PROGRESS — focused review: GO.** The four currently supported weather rows now use
`BattleConditionStores` as their sole state and duration owner: rain/sun publish typed exact
`DamageQuery` multipliers through `BattleHookDispatcher`, sandstorm/hail run topology-ordered
`TurnEnd` residuals, and the common checkpoint ticks/expires only after the weather hook. Apply,
replace, tick, expire, and battle-end cleanup publish generic condition events/traces alongside the
existing weather presentation events. Same-weather move or ability application is a no-op and does
not restart duration; different weather captures source slot/party and replaces the instance.
Condition-triggered forms retain their event order. `BattleController` exposes immutable condition
and hook snapshots, and Runtime passes that snapshot to Smart AI, which now collects the same exact
weather damage registrations as resolver queries. The old private weather enum/timer and unused
parallel damage helper were removed. New neutral tests cover all four definitions, source identity,
same/different application, duration one and ordinary expiry, exact boosted/weakened/absent query
rows, residual topology and immunity, battle-end cleanup, no added RNG, AI choice parity, and replay.
Owning-spec, AI, catalog, test-strategy, and roadmap documents record the checkpoint boundary.

This is not the 15E-3 weather-family exit: accuracy, freeze/status, healing, move type/base-power/
charge, grounded/stat, natural-input, and ruleset-difference hooks remain. Review removed a proposed
four-row weather certification because those setting moves are not yet 100% intended while that
interaction matrix is incomplete. Schema/migration impact: none. Dependency impact: none. RNG
impact: none. Changed files for this checkpoint: `docs/BATTLE_SYSTEM_SPEC.md`,
`docs/BATTLE_AI_SPEC.md`, `docs/EFFECT_TYPES_CATALOG_v0_5.md`, `docs/TESTING_STRATEGY.md`, this plan,
`src/Cgm.Core/Battle/WeatherConditions.cs`, `src/Cgm.Core/Battle/BattleController.cs`,
`src/Cgm.Core/Battle/SmartAi.cs`, `src/Cgm.Runtime/Engine/ExportedGameBoot.cs`, and
`tests/Cgm.Core.Tests/Battle/BattleWeatherConditionTests.cs`. Verification:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors;
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build --no-restore` passed 1,397 tests
(1,174 Core, 104 Creator, 21 Runtime, 98 Tools); the focused weather/legacy/doubles/hook filter
passed 95 tests; the new weather-condition suite contributes 15 cases; measured checkpoint
production lines exercised 193/194 (99.48%); deterministic regeneration remained 937 inventoried /
84 certified with digest `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`
and byte-identical outputs; and `git diff --check` passed. Next continuation: finish the remaining
weather query/ruleset interaction matrix, then add weather conformance rows only when every setting
move is fully intended.

Progress (2026-07-15): **15E-3 WEATHER ACCURACY CHECKPOINT COMPLETE; PACKAGE REMAINS IN PROGRESS.**
The reusable `weatherAccuracy` move op strictly compiles authored bypass-weather and accuracy-
override rows. Rain/sun/hail conditions publish typed `AccuracyQuery` hooks through the existing
dispatcher: bypass rows ignore accuracy/evasion and consume no accuracy draw, while override rows
replace authored accuracy before stage multiplication and retain one per-target d100. Resolver query/
effect/hook traces and Smart AI expected-value scoring consume the same snapshot; missing/unlisted
weather is neutral, no new event or RNG source exists, and no move ID/name branch was added. Neutral
tests cover compilation and every invalid row, bypass/stage/draw boundaries, absent/unlisted weather,
resolver integration, and AI choice parity. Exact reference families affected are `move-0059`,
`move-0087`, and `move-0542`; certification remains unchanged because their remaining ailment,
semi-invulnerable, protection, and multi-turn requirements are not all closed. Schema/migration and
dependency impact: none. Remaining weather work: freeze/status, healing, move type/base-power/charge,
grounded/stat, natural field inputs, and ruleset differences; only then close weather and proceed to
terrain/room/gravity/sport inside 15E-3. Verification: the focused compiler/weather/Smart-AI filter
passed 118 tests; the complete Core battle filter passed 917 tests; the full Core coverage run passed
1,194 tests and the final full solution passed 1,418 tests (1,195 Core, 104 Creator, 21 Runtime, 98
Tools). `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors;
the 149 instrumented production lines belonging to this checkpoint were all exercised (100%);
deterministic audit regeneration was byte-identical at 937 inventoried / 84 certified with digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; and `git diff --check`
passed. Focused review found no scope, schema, dependency, determinism, AI-fairness, or named-move
branch defect: GO for this checkpoint, not for 15E-3 package exit. No checkpoint commit was created
because the shared worktree already contained overlapping uncommitted Phase 15 package changes;
unrelated changes were not staged or rewritten.

Progress (2026-07-15): **15E-3 WEATHER STATUS CHECKPOINT COMPLETE; PACKAGE REMAINS IN PROGRESS.**
The shared condition hook catalog now includes `StatusAttempt`. Sun declares that hook and owns a
data row denying `freeze`; the resolver runs it through the generic ailment admission boundary, so a
blocked attempt emits no status event and consumes no secondary-status RNG. Other statuses and
weather rows remain neutral, existing freeze is not cured, and replacement/expiry changes admission
immediately. Smart AI collects the identical visible condition snapshot and removes only the denied
`status` score component. The immutable corpus audit found seven affected generic freeze rows:
`move-0008`, `move-0058`, `move-0059`, `move-0181`, `move-0423`, `move-0573`, and `move-0821`; none
is newly certified because its full move contract is not yet closed. Schema/migration and dependency
impact: none. Remaining weather work: healing, move type/base-power/charge, grounded/stat, natural
field inputs, and ruleset difference rows; only then close weather and proceed to terrain/room/
gravity/sport inside 15E-3. Verification: the focused weather suite passed 37 tests, the focused
weather/status/Smart-AI/hook suite passed 86 tests, and the complete Battle filter passed 930 tests.
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors;
the final full solution passed 1,431 tests (1,208 Core, 104 Creator, 21 Runtime, 98 Tools); the full
Core coverage run passed 1,208 tests and exercised all 50 instrumented checkpoint production lines
(100%); deterministic audit regeneration was byte-identical at 937 inventoried / 84 certified with
digest `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; and
`git diff --check` passed. Focused review found no blocking scope, schema, dependency, determinism,
AI-fairness, IP, or named-move branch defect: GO for this checkpoint, not for 15E-3 package exit.

Progress (2026-07-15): **15E-3 WEATHER HEALING CHECKPOINT COMPLETE; PACKAGE REMAINS IN PROGRESS.**
Generic `heal` effects can now author a strict weather-to-fraction table. The active field weather
publishes a typed `HealingQuery` replacement using the recipient's maximum HP, so direct fraction
rounding occurs before normal HP clamping; absent or unlisted weather preserves the authored base
fraction. Self and target recipients share the same resolver boundary, hook/query traces expose the
decision, actual `Healed` events remain clamped to missing HP, and no RNG is consumed. Smart AI uses
the identical visible condition snapshot and caps expected recovery at missing HP. The immutable
corpus audit identified the exact affected authored family as `move-0234`, `move-0235`, `move-0236`,
and `move-0659`; certification remains unchanged because their complete move contracts are not yet
closed. Changed production files: `BattleConditions.cs`, `MoveEffects.cs`, `MoveCompiler.cs`,
`WeatherConditions.cs`, `BattleController.cs`, and `SmartAi.cs`. Changed focused tests:
`MoveCompilerTests.cs` and `BattleWeatherConditionTests.cs`. Contract/evidence updates:
`BATTLE_SYSTEM_SPEC.md`, `EFFECT_TYPES_CATALOG_v0_5.md`, `BATTLE_AI_SPEC.md`,
`TESTING_STRATEGY.md`, `MOVE_AUDIT_SYSTEM_PLAN.md`, `SCOPE_GUARD.md`, `docs/AGENTS.md`, and this
plan. Schema/migration and dependency impact: none. Remaining weather work: move type/base-power/
charge, grounded/stat, natural field inputs, and ruleset difference rows; only then close weather and
proceed to terrain/room/gravity/sport inside 15E-3. Verification: the focused compiler/weather/
Smart-AI filter passed 157 tests; the weather suite passed 50 tests; the complete Battle filter passed
956 tests; and the final full solution passed 1,457 tests (1,234 Core, 104 Creator, 21 Runtime, 98
Tools). `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/
errors. The full Core coverage run passed 1,234 tests; repository line coverage was 94.77%, while all
111 instrumented production lines belonging to this checkpoint were exercised (100%). Deterministic
audit regeneration was byte-identical at 937 inventoried / 84 certified with digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; `git diff --check` passed.
Focused review found no blocking scope, schema, dependency, determinism, AI-fairness, IP, event/RNG,
or named-move branch defect: GO for this checkpoint, not for 15E-3 package exit. No checkpoint commit
was created because the shared worktree contains overlapping uncommitted Phase 15 package changes;
unrelated changes were not staged or rewritten.

Progress (2026-07-16): **15E-3 WEATHER MOVE TYPE/BASE-POWER/CHARGE CHECKPOINT COMPLETE; PACKAGE
REMAINS IN PROGRESS.** The reusable `weatherMove` op strictly compiles authored type replacement,
exact base-power multiplier, and charge-skip tables. Active weather publishes typed `MoveTypeQuery`,
`BasePowerQuery`, and `ChargeStart` hook payloads through the shared dispatcher. Effective type owns
immunity, effectiveness, STAB, final weather damage, and typed damage history without mutating the
authored move; base-power rows run at the Hook stage; and charge denial occurs before PP/setup,
while an already charging move releases under the then-current weather. Resolver and Smart AI read
the same immutable condition snapshot. The exact affected reference keys are `move-0311`,
`move-0076`, and `move-0669`; certification remains unchanged because the complete multi-turn and
weather package contracts remain open. Schema/migration and dependency impact: none. RNG impact:
none beyond the move's existing accuracy/crit/damage draws. New events: none; hook/query traces and
damage history expose the effective rows. Remaining weather work: grounded/stat, natural field
inputs, and ruleset-difference rows; only then close weather and proceed to terrain/room/gravity/
sport inside 15E-3.
Verification: the focused compiler/weather/charge/hook/validation filter passed 190 tests; the
complete Battle filter passed 974 tests; `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx
--no-restore` passed with 0 warnings/errors; and the final full solution passed 1,479 tests (1,256
Core, 104 Creator, 21 Runtime, 98 Tools). Deterministic audit regeneration was byte-identical at 937
inventoried / 84 certified with digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; `git diff --check`
passed. Focused review found and fixed missing `ChargeStart` declarations for authored non-sun skip
rows and missing project-level type-reference validation; final verdict GO for this checkpoint, not
for 15E-3 package exit. Implementation checkpoint: `8f4eeba`.

Progress (2026-07-16): **15E-3 WEATHER FAMILY COMPLETE; PACKAGE REMAINS IN PROGRESS — focused
review: GO.** The weather registry now includes profile-gated modern snow alongside legacy hail.
Sandstorm contributes a field-owned `3/2` Rock Special Defense modifier and modern snow contributes
the equivalent Ice Defense modifier through the shared `StatQuery`/`DefensiveStat` pipeline after
ordinary stages, with exact per-multiply flooring. Resolver and Smart AI consume the same immutable
condition snapshot and ruleset; Runtime forwards the controller's profile into AI context. Weather
residual immunity remains type-based and explicitly affects airborne non-immune creatures. New
`BattleFieldInputs` admits validated battle-start natural weather through the same condition store,
including environment source, optional positive duration, events, trace, expiration, and profile
admission. `gen4_like` admits hail and rejects snow; `modern_reference` admits snow and rejects hail;
rain, sun, and sandstorm are shared. Strict move compilation now accepts snow and continues rejecting
unknown weather. No move ID/name branch, new dependency, schema/migration, event type, or RNG draw was
added; certification remains 84/937 because weather-setting rows still require their individual
complete conformance vectors. Terrain-owned natural environment selection for Nature Power/Secret
Power/Camouflage remains with the terrain family rather than the completed weather family.
Changed production files: `src/Cgm.Core/Battle/BattleConditions.cs`, `BattleQuery.cs`,
`BattleIntentQueue.cs`, `WeatherConditions.cs`, `BattleController.cs`, `SmartAi.cs`, and
`src/Cgm.Runtime/Engine/ExportedGameBoot.cs`. Changed tests:
`tests/Cgm.Core.Tests/Battle/BattleWeatherConditionTests.cs`, `MoveCompilerTests.cs`, and
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`. Contract/evidence updates:
`docs/BATTLE_SYSTEM_SPEC.md`, `docs/BATTLE_AI_SPEC.md`, `docs/EFFECT_TYPES_CATALOG_v0_5.md`,
`docs/TESTING_STRATEGY.md`, and this plan. Verification: focused compiler/weather/validation passed
185 tests; complete Battle passed 984 tests; Smart/Trainer/AI passed 207 tests. Final full-solution
build passed with 0 warnings/errors and all 1,488 tests passed (1,265 Core, 104 Creator, 21 Runtime,
98 Tools). Deterministic audit regeneration was byte-identical at 937 inventoried / 84 certified
with corpus digest `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`;
`git diff --check` passed. Focused review found no blocking scope, architecture, schema, dependency,
determinism, AI-fairness, IP, event/RNG, or named-move branch defect: GO for weather-family exit,
not for 15E-3 package exit. Implementation checkpoint: `630b817`. Next slice:
continue 15E-3 with the complete terrain family, including grounded queries and natural environment
selection, before room/gravity/sport.

Progress (2026-07-16): **15E-3 TERRAIN INTRINSIC CHECKPOINT COMPLETE; TERRAIN FAMILY AND PACKAGE
REMAIN IN PROGRESS — focused review: GO.** The closed electric/grassy/misty/psychic registry now
uses the shared terrain store with one effective instance, five-turn replacement lifecycle, exact
source identity, profile admission, lifecycle events/traces, and validated natural/initial field
inputs. `BattleQueryId.Grounded` supplies the shared intrinsic Flying-type filter. Grounded sources
receive exact Electric/Grass/Psychic `3/2` damage rows; grounded targets receive Misty Dragon `1/2`,
Electric sleep denial, Misty persistent-status/confusion denial, Psychic opposing-priority denial
before accuracy, and topology-ordered Grassy `1/16` end-turn healing before duration expiry. The
resolver and Smart AI collect the same immutable terrain condition snapshot; field actions are not
misclassified as priority attacks, denied status/confusion consumes no effect/duration RNG, and no
move ID/name branch was added. Effective environment maps active terrain over the validated natural
environment and returns to natural on expiry. The strict generic `terrain` op supplies field
application; certification remains 84/937 because individual terrain-setting and terrain-sensitive
moves still need their complete normalized vectors.

Changed production files: `src/Cgm.Core/Battle/TerrainConditions.cs`, `BattleQuery.cs`,
`BattleActionHistory.cs`, `BattleController.cs`, `BattleEvents.cs`,
`MoveEffects.cs`, `MoveCompiler.cs`, and `SmartAi.cs`. Changed focused tests:
`tests/Cgm.Core.Tests/Battle/BattleTerrainConditionTests.cs` and `MoveCompilerTests.cs`. Contract/
evidence updates: `docs/BATTLE_SYSTEM_SPEC.md`, `docs/BATTLE_AI_SPEC.md`,
`docs/EFFECT_TYPES_CATALOG_v0_5.md`, `docs/TESTING_STRATEGY.md`, `docs/SCOPE_GUARD.md`, and this plan. Schema/migration and
dependency impact: none. New simulation randomness: none. Verification: the focused terrain/compiler
filter passed 12 tests; complete Battle passed 998 tests;
Smart/Trainer/AI passed 218 tests; final build passed with 0 warnings/errors; and the final full
solution passed 1,500 tests (1,277 Core, 104 Creator, 21 Runtime, 98 Tools). Deterministic audit
regeneration was byte-identical at 937 inventoried / 84 certified with corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; `git diff --check` passed.
Focused review found and fixed positive-priority field actions being incorrectly eligible for the
Psychic opposing-target block; final verdict GO for this checkpoint, not for terrain-family or
15E-3 exit.

The intrinsic checkpoint's recorded continuation was: add strict authored terrain type/power/priority/spread/gate/removal/heal
rows; finish natural/effective environment selection inputs for Nature Power, Secret Power, and
Camouflage while leaving called-move execution with 15D-7; integrate grounded overrides and
`TerrainChange`/duration hooks from gravity, volatiles, abilities, and items at their owning
checkpoints; then close the full terrain interaction matrix before room/gravity/sport.

Progress (2026-07-16): **15E-3 TERRAIN AUTHORED-INTERACTION CHECKPOINT COMPLETE; TERRAIN FAMILY AND
PACKAGE REMAIN IN PROGRESS — focused review: GO.** Strict generic `terrainMove` rows now provide
field-, grounded-user-, and grounded-target-sensitive move type, base power, priority, and selected-
target-to-all-opponents materialization. `terrainGate` fails before PP/RNG when terrain is absent;
`removeTerrain` removes the exact active terrain through the shared store with `Effect` cleanup,
ordinary condition trace/event evidence, and `TerrainEnded`; `heal.terrain` replaces the authored
healing amount through `HealingQuery`. Type references validate through the existing move rule.
Resolver and Smart AI share the same immutable condition snapshot, grounded subjects, query
modifiers, effective type/power/priority, gate state, and healing replacement; no move-ID/name
branch, schema change, dependency, or new simulation RNG was added. Doubles spread retains authored
selection admission, applies ordinary multi-target damage scaling, and does not redirect.

Changed production files: `src/Cgm.Core/Battle/BattleConditions.cs`, `BattleController.cs`,
`BattleEvents.cs`, `MoveCompiler.cs`, `MoveEffects.cs`, `SmartAi.cs`, `TerrainConditions.cs`, and
`src/Cgm.Core/Validation/Rules/MoveRules.cs`. Changed focused tests:
`tests/Cgm.Core.Tests/Battle/BattleTerrainConditionTests.cs`, `MoveCompilerTests.cs`, and
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`. Contract/evidence updates:
`docs/BATTLE_SYSTEM_SPEC.md`, `BATTLE_AI_SPEC.md`, `EFFECT_TYPES_CATALOG_v0_5.md`,
`TESTING_STRATEGY.md`, and this plan. Verification: focused terrain/compiler/Smart-AI/validation
passed 172 tests; complete Battle passed 1,003 tests; Smart/Trainer/AI passed 224 tests;
full solution build passed with 0 warnings/
errors; final full solution passed 1,505 tests (1,282 Core, 104 Creator, 21 Runtime, 98 Tools).
Deterministic audit regeneration remained 937 inventoried / 84 certified with corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; `git diff --check` passed.
Focused review found and fixed preview-source coupling in terrain priority scoring; final verdict GO
for this checkpoint, not for terrain-family or 15E-3 exit. Next slice: finish natural/effective
environment selection inputs for Nature Power, Secret Power, and Camouflage while leaving called-
move execution with 15D-7, then continue grounded overrides and terrain change/duration hooks.

Progress (2026-07-16): **15E-3 TERRAIN ENVIRONMENT-INPUT CHECKPOINT COMPLETE; TERRAIN FAMILY AND
PACKAGE REMAIN IN PROGRESS — focused review: GO.** `BattleEnvironmentState` is now the single
immutable natural/effective selector input for the resolver and Smart AI. All twelve non-terrain
natural values are accepted; the four terrain-only values and unknown enums are rejected as natural
battle inputs. The natural value remains fixed while the effective value is derived from the active
terrain condition snapshot, so application/replacement selects the terrain environment and explicit
removal or expiry restores natural without parallel state, new events/traces, or RNG. This is the
shared input for environment-selected called moves, secondary effects, and creature types; it does
not execute those deferred consumers. Called-move execution remains 15D-7, type mutation remains
15F, and conditional-secondary resolution remains with its owning move-effect package.

Changed production files: `src/Cgm.Core/Battle/TerrainConditions.cs`, `BattleController.cs`, and
`SmartAi.cs`. Changed focused tests: `tests/Cgm.Core.Tests/Battle/BattleTerrainConditionTests.cs`.
Contract/evidence updates: `docs/BATTLE_SYSTEM_SPEC.md`, `BATTLE_AI_SPEC.md`,
`EFFECT_TYPES_CATALOG_v0_5.md`, `TESTING_STRATEGY.md`, `SCOPE_GUARD.md`, and this plan. Schema/
migration and dependency impact: none. New simulation randomness: none. Verification: focused
terrain/Smart-AI passed 65 tests; complete Battle passed 1,020 tests; Smart/Trainer/AI passed 241
tests; full solution build passed with 0 warnings/errors; final full solution passed 1,522 tests
(1,299 Core, 104 Creator, 21 Runtime, 98 Tools). Deterministic audit regeneration remained 937
inventoried / 84 certified with corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; `git diff --check` passed.
Focused review found no blocking scope, architecture, determinism, schema, dependency, or AI-
fairness issue; final verdict GO for this checkpoint, not for terrain-family or 15E-3 exit. Next
slice: integrate grounded-query overrides and terrain change/duration hooks at their owning
gravity, volatile, ability, and item seams, then close the terrain interaction matrix before
room/gravity/sport.

Progress (2026-07-16): **15E-3 TERRAIN LIFECYCLE-HOOK CHECKPOINT COMPLETE; TERRAIN FAMILY AND
PACKAGE REMAIN IN PROGRESS — focused review: GO.** The generic `terrainSummon` ability op now
applies modern terrain through the shared condition store on switch-in; `onTerrainChange` dispatches
after `TerrainChanged`, permits one replacement, and suppresses nested redispatch. The generic
`terrainDurationExtend` held-item op adds positive turns only when that holder's ability summoned
the terrain, so opposing holders, move-authored terrain, and battle-start terrain cannot extend it.
Strict validation rejects unsupported hook/op pairings, missing/unknown/numeric terrain values,
mistyped duration, unknown params, and invalid extension turns. All paths retain exact condition
source/duration evidence and add no simulation or AI RNG; Smart AI observes only the resulting
immutable condition snapshot through its existing terrain queries.

The serialized `AbilityHookPoint` vocabulary now includes `onTerrainChange`, so project schema v6
adds a no-op v5→v6 migration and an old-shape ability fixture; existing hooks remain unchanged.
Production files: `src/Cgm.Core/Battle/BattleController.cs`, `BattleHookDispatcher.cs`,
`src/Cgm.Core/Model/Entities/Ability.cs`, `SchemaVersions.cs`,
`src/Cgm.Core/Serialization/Migrator.cs`, and
`src/Cgm.Core/Validation/Rules/BattleV6Rules.cs`. Tests/fixtures:
`BattleTerrainConditionTests.cs`, `ValidationTests.cs`, `MigratorTests.cs`,
`SchemaV2SerializationTests.cs`, and `tests/fixtures/schema-v5/ability.json`. Contract/evidence:
`docs/DATA_SCHEMA.md`, `BATTLE_SYSTEM_SPEC.md`, `BATTLE_AI_SPEC.md`,
`EFFECT_TYPES_CATALOG_v0_5.md`, `TESTING_STRATEGY.md`, `SCOPE_GUARD.md`, and this plan. Dependency
impact: none. Verification: focused terrain/validation/serialization/migration passed 80 tests;
complete Battle passed 1,024 tests; full solution build passed with 0 warnings/errors; full solution
passed 1,527 tests (1,304 Core, 104 Creator, 21 Runtime, 98 Tools). Deterministic audit regeneration
remained 937 inventoried / 84 certified with corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`. Focused review found and
fixed the unsupported hook/op admission and numeric/unknown-param validation gaps; final verdict GO
for this checkpoint, not for terrain-family or 15E-3 exit. Next slice: complete the shared grounded
override matrix for gravity, airborne volatiles, abilities, and items before terrain seeds and
terrain-family closeout.

Progress (2026-07-16): **15E-3 GROUNDED-OVERRIDE CHECKPOINT COMPLETE; TERRAIN FAMILY AND PACKAGE
REMAIN IN PROGRESS — focused review: GO.** The shared `Grounded` query now consumes effective
creature types plus field/creature conditions and passive ability/item rows under one locked
precedence: field grounding, explicit grounded state, explicit airborne state, then intrinsic
typing. Strict generic `groundedState` rows apply target-owned grounded/airborne conditions or the
field-owned grounded component required by Gravity; invalid chance, numeric enums, unknown params,
nonpositive duration, duplicate rows, field-airborne rows, and non-creature target scopes fail
compilation. Creature siblings replace under one stacking key, tick at `TurnEnd`, and clean up on
switch/faint; ordinary condition events/traces expose application, replacement, expiry, and cleanup.
`onGroundedQuery` ability hooks and held `groundedModify` rows feed the same query without condition
mutation or RNG. Resolver and Smart AI share effective typing, visible condition snapshots,
ability/item rows, and terrain damage/status/priority outcomes; no move ID/name branch was added.
This checkpoint implements only Gravity's grounded component; its accuracy and move-availability
rows remain open with the complete room/gravity/sport interaction criterion.

Schema impact: project schema v7 adds the serialized `onGroundedQuery` enum value with a no-op
v6→v7 migration and old-shape fixture; save format is unchanged. Dependency impact: none. New
production files: `src/Cgm.Core/Battle/GroundedConditions.cs`. Changed production files:
`BattleConditions.cs`, `BattleController.cs`, `MoveCompiler.cs`, `MoveEffects.cs`, `SmartAi.cs`,
`Model/Entities/Ability.cs`, `Model/SchemaVersions.cs`, `Serialization/Migrator.cs`, and
`Validation/Rules/BattleV6Rules.cs`. Focused tests: `BattleGroundedConditionTests.cs`,
`MoveCompilerTests.cs`, `ValidationTests.cs`, `MigratorTests.cs`, and
`SchemaV2SerializationTests.cs`, plus `tests/fixtures/schema-v6/ability.json`. Contract/evidence
updates: `BATTLE_SYSTEM_SPEC.md`, `EFFECT_TYPES_CATALOG_v0_5.md`, `BATTLE_AI_SPEC.md`,
`DATA_SCHEMA.md`, `TESTING_STRATEGY.md`, `docs/AGENTS.md`, and this plan. Verification: the focused
compiler/schema/validation/grounding filter passed 144 tests; complete Battle passed 1,029 tests;
full Core passed 1,311 tests; full solution build passed with 0 warnings/errors; full solution passed
1,534 tests (1,311 Core, 104 Creator, 21 Runtime, 98 Tools). Deterministic audit regeneration was
byte-identical at 937 inventoried / 84 certified with corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; `git diff --check` passed.
Next slice: complete terrain seed activation/consumption and resolver/AI interaction evidence, then
close the terrain-family matrix before room/gravity/sport.

Progress (2026-07-17): **15E-3 TERRAIN-SEED EXIT CHECKPOINT AND TERRAIN FAMILY COMPLETE; PACKAGE
REMAINS IN PROGRESS — focused review: GO.** The generic held `terrainSeed { terrain, stat }` op now
activates for an initial active holder, a holder switching into matching terrain, and an active
holder after `TerrainChanged`. It consumes once and raises authored `def` or `spd` by one stage in
deterministic topology/hook order; it does not require grounding or draw RNG. Mismatched terrain,
prior consumption, a fainted holder, and a +6 stat are no-ops; the capped-stat path deliberately
does not consume, so a later eligible checkpoint can activate it. Strict validation rejects missing,
unknown, wrong-type, numeric-string, chance-gated, extra-param, non-defensive-stat, and duplicate
rows. Ordinary `HeldItemConsumed` and `StatStageChanged` events expose the result. Smart AI consumes
the already-mutated visible stat stages through its existing damage scoring without a seed-specific
component, hidden-item prediction, or resolver duplication.

Production changes: `src/Cgm.Core/Battle/BattleController.cs` and
`src/Cgm.Core/Validation/Rules/BattleV6Rules.cs`. Tests:
`tests/Cgm.Core.Tests/Battle/BattleTerrainConditionTests.cs` and
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`. Contract/evidence updates:
`docs/BATTLE_SYSTEM_SPEC.md`, `docs/BATTLE_AI_SPEC.md`, `docs/EFFECT_TYPES_CATALOG_v0_5.md`,
`docs/TESTING_STRATEGY.md`, `docs/SCOPE_GUARD.md`, and this plan. Schema/migration impact: none; the
existing open `Effect.Op` payload shape and schema v7 remain unchanged. Dependency impact: none.
Verification: the focused terrain/validation filter passed 71 tests; full Core passed 1,314 tests;
full solution build passed with 0 warnings/errors; full solution passed 1,537 tests (1,314 Core,
104 Creator, 21 Runtime, 98 Tools). Decision-catalog audit regeneration was byte-identical at 937
inventoried / 84 certified with corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; `git diff --check` passed.
Focused review found no blocking scope, architecture, schema, dependency, determinism, AI-fairness,
IP, event-order, or named-move issue; it strengthened doubles topology and numeric-string rejection
evidence before returning GO. No conformance count advances because this checkpoint closes a shared
interaction family rather than certifying individual move rows. Next slice: complete the combined
room/gravity/sport interaction criterion, including Gravity accuracy and move-availability rows.

Progress (2026-07-17): **15E-3 COMPLETE — room/gravity/sport criterion.** The owning specs now lock
the generic `fieldCondition` and `fieldMoveGate` payloads, coexist/toggle/reject semantics, exact
turn-order and defensive-stat routing, Magic Room held-effect suppression, Gravity grounding/
accuracy/pre-PP legality, and classic-versus-modern sport lifecycle and power fractions.
`FieldConditions` registers typed room/field rows; `BattleController`, `GroundedConditions`, and
`BattleTurnOrder` consume them through shared queries and condition snapshots; `SmartAi` shares the
same damage, accuracy, legality, grounding, held-item, stat, and action-history inputs. Classic sport
cleanup uses the condition source identity and generic `source_bound` tag rather than move or content
names. Production changes: `src/Cgm.Core/Battle/FieldConditions.cs`, `BattleConditions.cs`,
`BattleController.cs`, `BattleTurnActions.cs`, `GroundedConditions.cs`, `MoveCompiler.cs`,
`MoveEffects.cs`, `BattleEvents.cs`, and `SmartAi.cs`. Tests:
`tests/Cgm.Core.Tests/Battle/BattleFieldConditionTests.cs`. Contract/evidence updates:
`docs/BATTLE_SYSTEM_SPEC.md`, `docs/BATTLE_AI_SPEC.md`, `docs/EFFECT_TYPES_CATALOG_v0_5.md`,
`docs/TESTING_STRATEGY.md`, `docs/SCOPE_GUARD.md`, and this plan. Schema/migration impact: none; the
existing open `Effect.Op` payload and schema v7 remain unchanged. Dependency impact: none.
Verification: the focused compiler/condition/order/AI filter passed 153 tests; full solution build
passed with 0 warnings/errors; full solution passed 1,548 tests (1,325 Core, 104 Creator, 21 Runtime,
98 Tools). Decision-catalog audit regeneration was byte-identical at 937 inventoried / 84 certified
with corpus digest `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`;
`git diff --check` passed. Focused review found no blocking scope, architecture, schema, dependency,
determinism, AI-fairness, IP, event-order, or named-move issue. No conformance count advances because
this closes the reusable interaction family rather than certifying individual move rows.
Next slice: begin 15E-4 with the complete reusable side-condition duration/damage interaction
criterion, not a named screen move.

**15E-4 screen-family checkpoint (`IN PROGRESS`; 2026-07-17).** The reusable screen criterion is
green: typed side-owned physical, special, and all-damage conditions share the condition registry,
five-turn lifecycle, duplicate rejection without refresh, independent stacking keys, exact singles
`1/2` and doubles `2/3` final-damage queries, critical and explicit/ability bypass, before-damage and
after-hit tagged removal, held duration extension, snow/ruleset gating, deterministic events/traces,
and Smart AI query parity. Removal is a generic tag-and-owner store operation; no move ID or content
name enters production behavior. Production changes: `SideConditions.cs`, `BattleConditions.cs`,
`BattleController.cs`, `BattleEvents.cs`, `EffectTrace.cs`, `MoveCompiler.cs`, `MoveEffects.cs`,
`SmartAi.cs`, and `BattleV6Rules.cs`. Tests: `BattleSideConditionTests.cs` plus focused ability/held
validation vectors in `ValidationTests.cs`.
Owning contracts updated: `BATTLE_SYSTEM_SPEC.md`, `EFFECT_TYPES_CATALOG_v0_5.md`,
`BATTLE_AI_SPEC.md`, and `TESTING_STRATEGY.md`. Schema/migration and dependency impact: none; the
existing open effect payload remains unchanged. Verification: focused screen/validation tests passed
17/17; Core passed 1,341 tests; full solution build passed with 0 warnings/errors; full solution passed
1,564 tests (1,341 Core, 104 Creator, 21 Runtime, 98 Tools). Decision-catalog regeneration remained
byte-identical at 937 inventoried / 84 certified with corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; `git diff --check` passed. The package
remains `IN PROGRESS`: status/stage guards are next; Tailwind/order and side-wide protection remain
separate later criteria. No per-move certification count advances at this checkpoint.

**15E-4 side-guard checkpoint (`IN PROGRESS`; 2026-07-17).** The paired status/stage-guard
criterion is green. `statusGuard` blocks opposing persistent-status and confusion attempts before
chance/duration RNG; `stageDropGuard` blocks opposing negative single/all-stat deltas before chance
RNG while preserving self/allied drops, positive boosts, and reset/copy/swap/invert semantics. Both
are typed five-turn side conditions with distinct reject-on-duplicate stacking keys, shared
source-independent lifecycle, per-side doubles ownership, hook traces, move/outgoing-ability bypass,
and shared `barrier` removal with screens. Contact ability payloads use the same source/recipient
guard path. Smart AI consumes the status guard through its existing named `status` component; no
speculative stage-debuff score was added. Production changes: `SideConditions.cs`,
`BattleController.cs`, `MoveCompiler.cs`, `SmartAi.cs`, and `BattleV6Rules.cs`. Tests:
`BattleSideGuardTests.cs` plus the closed ability-bypass validation vector in `ValidationTests.cs`.
Owning contracts updated: `BATTLE_SYSTEM_SPEC.md`, `EFFECT_TYPES_CATALOG_v0_5.md`,
`BATTLE_AI_SPEC.md`, and `TESTING_STRATEGY.md`. Schema/migration and dependency impact: none.
Verification: focused guard/validation tests passed 11/11; Core passed 1,351 tests; full solution
build passed with 0 warnings/errors; full solution passed 1,574 tests (1,351 Core, 104 Creator,
21 Runtime, 98 Tools). Decision-catalog regeneration remained byte-identical at 937 inventoried /
84 certified with corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; `git diff --check` passed.
Focused review corrected the initial damaging-only bypass admission so serialized status/stage
guard bypass rows now require their compatible authored `ailment` or negative-stage consumer;
no blocking scope, schema, dependency, determinism, AI-fairness, IP, lifecycle, or event-order
finding remains. The package remains `IN PROGRESS`:
Tailwind/order is next; critical guards, pledges, and side-wide protection remain later criteria.
No per-move certification count advances at this checkpoint.

**15E-4 side speed/order checkpoint (`IN PROGRESS`; 2026-07-17).** The reusable speed/order
criterion is green. `speedBoost` is a typed side-owned `StatQuery` condition with a four-checkpoint
generic default and explicit three/four checkpoint classic/modern reference normalization contract.
It doubles effective Speed after stages, paralysis, and overlays for every active creature on the
owning side while retaining one shared duration, source, and duplicate-reject stacking key. Current
turn schedules remain immutable; later move/switch/item/redirection ordering and speed-ratio/action-
history inputs consume the shared query, with Trick Room reversing only the completed order. Source
switch/faint does not remove the condition. Smart AI consumes the same query for its existing
speed-derived predictions without a new score term or hidden information. Production changes:
`SideConditions.cs`, `PhysicalMetricFormulas.cs`, `BattleController.cs`, `MoveCompiler.cs`, and
`SmartAi.cs`. Tests: `BattleSideOrderConditionTests.cs`. Owning contracts updated:
`BATTLE_SYSTEM_SPEC.md`, `EFFECT_TYPES_CATALOG_v0_5.md`, `BATTLE_AI_SPEC.md`,
`TESTING_STRATEGY.md`, `SCOPE_GUARD.md`, and this plan. Schema/migration and dependency impact: none;
the existing open effect payload remains unchanged. Verification: focused speed/order tests passed
9/9 and the broader side/physical-metric/AI/turn-order/field regression filter passed 136 tests;
full solution build passed with 0 warnings/errors; full solution passed 1,583 tests (1,360 Core,
104 Creator, 21 Runtime, 98 Tools). Decision-catalog regeneration was byte-identical at 937
inventoried / 84 certified with corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; `git diff --check`
passed. The package remains `IN PROGRESS`: critical guards are next; pledges and side-wide
protection remain later criteria. No per-move certification count advances at this checkpoint.

**15E-4 side critical-guard checkpoint (`IN PROGRESS`; 2026-07-17).** The reusable critical-guard
criterion is green. `criticalGuard` is a typed five-checkpoint side-owned `CriticalQuery` condition
that clamps opposing per-hit critical chance to exact zero for either owning-side doubles slot.
The ordinary critical draw remains before the damage roll, so RNG count/order is unchanged while
crit damage, crit-only stage bypass, and crit-based screen bypass are suppressed. Duplicate
application rejects without refresh; source switch/faint does not remove the shared instance; same-
side and formula-bypassing damage remain outside the filter. Resolver traces expose the exact
critical query and side hook. Smart AI sees the immutable condition but remains intentionally neutral
because its current shared damage estimate is already noncritical; no speculative score term or RNG
was added. Production changes: `SideConditions.cs`, `BattleRolls.cs`, and `BattleController.cs`.
Tests: `BattleSideCriticalGuardTests.cs` plus exact-rational crit-stage assertions in
`BattleRollsTests.cs`. Owning contracts updated: `BATTLE_SYSTEM_SPEC.md`,
`BATTLE_DAMAGE_CALC.md`, `EFFECT_TYPES_CATALOG_v0_5.md`, `BATTLE_AI_SPEC.md`,
`TESTING_STRATEGY.md`, `SCOPE_GUARD.md`, and this plan. Schema/migration, dependency, and new-event
impact: none. Verification: focused critical-guard tests passed 8/8; the broader side/roll/damage/
query/AI/replay/doubles regression filter passed 187 tests; full solution build passed with 0
warnings/errors; full solution passed 1,591 tests (1,368 Core, 104 Creator, 21 Runtime, 98 Tools).
Decision-catalog regeneration was byte-identical at 937 inventoried / 84 certified with corpus
digest `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; `git diff --check`
passed. Focused review changed the initial zero replacement to a zero clamp so same-stage source
critical additions/multipliers cannot override the guard; no blocking scope, schema, dependency,
determinism, AI-fairness, IP, lifecycle, query-order, or event finding remains. The package remains
`IN PROGRESS`: pledge side conditions are next; side-wide protection remains the final criterion.
No per-move certification count advances at this checkpoint.

**15E-4 paired-action side-effect checkpoint (`IN PROGRESS`; 2026-07-17).** The three reusable side
results are green without a named-move branch. `speedReduction` and `residualDamage` are strict
target-side rows; `secondaryChanceBoost` is a strict source-side row. Each owns four shared
`TurnEnd` checkpoints, rejects duplicates without refresh, coexists under a distinct stacking key,
persists across its source switching/fainting, and serves both active doubles slots. Speed reduction
is exact `1/4` after the existing `2/1` boost in application-independent hook order. Residual damage
uses `Sap` for `max(1, floor(maxHp / 8))` once per live eligible owning-side active before the shared
tick, emits `ResidualDamage`, and skips effective Fire types. Secondary-chance boost doubles each
outgoing damaging-move effect through `SecondaryChance`, clamps at 100, preserves its one ordinary
draw, excludes status-move primary effects, and feeds the existing Smart-AI status probability.
The side-condition op now carries a closed source/target selector, and the singles adapter admits
the already-supported opposing-field scope. Screen duration extension was also constrained to rows
actually tagged `screen`, preventing unrelated side rows from inheriting that held effect.

Owning contracts updated: `BATTLE_SYSTEM_SPEC.md`, `BATTLE_DAMAGE_CALC.md`,
`EFFECT_TYPES_CATALOG_v0_5.md`, `BATTLE_AI_SPEC.md`, `TESTING_STRATEGY.md`, `SCOPE_GUARD.md`, and
this plan. Production changes: `MoveEffects.cs`, `MoveCompiler.cs`, `BattleTargetResolver.cs`,
`SideConditions.cs`, `HpStatusFormulas.cs`, `BattleController.cs`, and `SmartAi.cs`. Tests:
`BattleSideComboConditionTests.cs`. Schema/migration, dependency, and new-event impact: none; the
open effect payload gains only the optional closed `side` parameter and reuses `ResidualDamage`.
Verification: focused tests passed 10/10; the broader Battle/Smart-AI/replay filter passed 1,095 tests;
full solution build passed with 0 warnings/errors; full solution passed 1,601 tests (1,378 Core,
104 Creator, 21 Runtime, 98 Tools). Decision-catalog regeneration was byte-identical at 937
inventoried / 84 certified with corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; `git diff --check`
passed. Pair recognition, ally prioritization, combined power/type, and side-row selection remain
15D-7 action work; no move row is prematurely certified here. The package remains `IN PROGRESS`:
side-wide protection is the final 15E-4 criterion.

**15E-4 COMPLETE — side-wide protection exit and focused review: GO (2026-07-17).** Four generic
one-checkpoint side rows now filter positive-priority, authored multi-target, externally directed
status, and damaging moves through the shared `TryHit` hook before accuracy. One side instance serves
both doubles slots, persists if its source faints, rejects duplicates without refresh, coexists with
the other rows, and expires at the application turn's `TurnEnd`. The resolver records per-target
protected damage-memory results, `MoveBlocked`, condition-hook traces, and `SideProtection` effect
traces while skipping accuracy and later RNG for blocked targets. Eligible allied spread targets use
the same side-owned predicate. Existing `sideConditionBypass` and tagged before-damage removal now
admit `side_protection`; the existing first-action move gate composes with damage protection. Smart
AI uses the same immutable filter to zero existing damage/KO value or omit blocked status value.

Owning contracts updated: `BATTLE_SYSTEM_SPEC.md`, `EFFECT_TYPES_CATALOG_v0_5.md`,
`BATTLE_AI_SPEC.md`, `TESTING_STRATEGY.md`, `SCOPE_GUARD.md`, and this plan. Production changes:
`SideConditions.cs`, `MoveCompiler.cs`, `BattleController.cs`,
`EffectTrace.cs`, `SmartAi.cs`, and `BattleV6Rules.cs`. Tests:
`BattleSideProtectionTests.cs` plus the closed ability-bypass validation vector in
`ValidationTests.cs`. Schema/migration and dependency impact: none; the existing open effect payload
and tag selectors remain the serialized boundary. RNG impact: none; blocked targets skip ordinary
accuracy/effect draws and the conditions add no draws. New presentation surface: the typed
`SideProtection` effect trace; existing `MoveBlocked` and condition lifecycle events remain the event
contract.

Verification: focused protection/validation tests passed 13/13; the broader battle/Smart-AI filter
passed 1,137 tests; full solution build passed with 0 warnings/errors; full solution passed 1,613
tests (1,390 Core, 104 Creator, 21 Runtime, 98 Tools). Decision-catalog regeneration was
byte-identical at 937 inventoried / 84 certified with corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; `git diff --check`
passed. Focused review found no blocking scope, architecture, schema, dependency, determinism,
AI-fairness, IP, lifecycle, target-order, event, trace, or named-move issue. Classic side-guard
success-chain sharing and personal/contact variants remain 15E-6 as specified; no reference row is
prematurely certified here. This closes 15E-4. Next eligible package: **15E-5 entry hazards**.

**15E-5 COMPLETE — generic entry hazards and focused review: GO (2026-07-17).** Entry hazards now
use immutable typed profiles attached to permanent side-scoped `SwitchIn` conditions instead of
controller-owned named flags. Strict generic damage, type-scaled damage, status, and stage ops lock
layer maxima, grounded filtering, fractions, type effectiveness, absorption types, status rows, and
stage payloads; the two legacy compiler aliases normalize into the same profiles. Application,
stacking, tagged removal, switch-in evaluation, source credit, presentation events, and traces all
flow through the shared condition store. Entry evaluates in slot then condition-sequence order;
damage can trigger the existing repeat-replacement loop after the complete batch. Status and stage
hazards reuse the existing type, ability, weather, terrain, and side-guard paths. Smart AI reads the
immutable condition snapshot, refuses capped setup, and charges switch candidates only for visible
direct-damage rows using the resolver's grounding, effective-type, fraction, and effectiveness math.

Owning contracts updated: `BATTLE_SYSTEM_SPEC.md`, `EFFECT_TYPES_CATALOG_v0_5.md`,
`BATTLE_AI_SPEC.md`, `TESTING_STRATEGY.md`, `SCOPE_GUARD.md`, and this plan. Production changes:
`EntryHazardConditions.cs`, `BattleConditions.cs`, `BattleConditionRegistry.cs`, `MoveEffects.cs`,
`MoveCompiler.cs`, `BattleCreature.cs`, `BattleController.cs`, `BattleEvents.cs`, `EffectMath.cs`,
`EffectTrace.cs`, `SmartAi.cs`, and Runtime's context-construction adapter. Tests update the hazard,
replacement, doubles, action-history, Smart-AI, and difficulty suites and add the intentional
`entry-hazard.golden` event/trace baseline. Schema/migration and dependency impact: none; the open
effect payload remains the serialized boundary. RNG impact: hazards add no draws. Presentation
surface: generic `EntryHazardSet`, `EntryHazardTriggered`, and `EntryHazardAbsorbed` events plus a
source-aware `EntryHazard` trace replace the prior named events.

Verification: focused hazard/replacement/AI tests passed 13/13; the broader Battle suite passed
1,111 tests; full solution build passed with 0 warnings/errors; full solution passed 1,617 tests
(1,394 Core, 104 Creator, 21 Runtime, 98 Tools). Audit regeneration was byte-identical at 937
inventoried / 84 certified with corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; `git diff --check`
passed. Focused review found no blocking scope, architecture, schema, dependency, determinism,
AI-fairness, IP, condition-lifecycle, replacement-order, event, trace, or named-move finding. Hazard
transfer and atomic side swap remain 15E-7; no reference row is prematurely certified here. This
closes 15E-5. Next eligible package: **15E-6 protect and contact-block families**.

**15E-6 COMPLETE — protect/contact families and focused review: GO (2026-07-17).** Personal and
side protection now compile into immutable typed profiles with closed scope, filter, chain,
guaranteed-draw, bypass, and ordered contact-payload data. Personal protection is a one-turn
creature-scoped `TryHit` condition; the existing priority/multi-target side rows remain the shared
side store. Both paths dispatch typed filters per materialized target before accuracy and record
protected damage-memory outcomes. One shared creature counter supplies exact `gen4_like` factor-2
and `modern_reference` factor-3 fractions with capped exponent, profile-controlled guaranteed
draws, and reset on Pass, item, switch, ordinary actions, prevention, or failed protection.
Move- and ability-authored `protectionBypass` skip all protection without removal. Contact damage,
status, and negative stage payloads execute in authored order through existing guards, stop after
source faint, and do not stop later spread targets. Smart AI reads the immutable snapshot and
multiplies its existing protect value by the resolver's exact fraction without mutation or RNG.

Owning contracts updated: `BATTLE_SYSTEM_SPEC.md`, `EFFECT_TYPES_CATALOG_v0_5.md`,
`BATTLE_AI_SPEC.md`, `TESTING_STRATEGY.md`, `SCOPE_GUARD.md`, and this plan. Production changes:
`ProtectionConditions.cs`, the shared condition registry/store, move compiler/effects, controller,
creature chain state, events/traces, HP-status formula inputs, Smart AI, and strict ability-op
validation. Tests update every legacy typed-protect construction and add compiler rejection,
immutability, ruleset/draw, lifecycle/reset, personal/side, bypass, contact guard, source-faint,
doubles, damage-memory, AI, replay, and checked-in `protection.golden` evidence. Schema/migration and
dependency impact: none; the existing open effect payload remains the serialized boundary. RNG
impact is exactly one protection draw when the profile and current fraction require it; blocked
targets skip accuracy and later move draws. Presentation additions are `ProtectionBlocked` and
`ProtectionContactDamaged`; generic condition events and typed effect/hook traces expose lifecycle.

Verification: focused protection/side-protection/doubles/damage-memory/Smart-AI/validation tests
passed 139/139; full solution build passed with 0 warnings/errors; full solution passed 1,629 tests
(1,406 Core, 104 Creator, 21 Runtime, 98 Tools). Definition-aware audit regeneration was
byte-identical at 937 inventoried / 84 certified with corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`; `git diff --check`
passed. Focused review found no blocking scope, architecture, schema, dependency, determinism,
AI-fairness, IP, condition-lifecycle, target-order, event, trace, or named-move finding. No reference
row is prematurely certified. This closes 15E-6. Next eligible package: **15E-7 generic condition
cleanup/transfer/swap**.

**15E-7 COMPLETE — generic condition mutation and focused review: GO (2026-07-17).** Three
chance-free typed ops now remove, transfer, or atomically swap shared condition instances by exact
scope plus one condition-ID, tag, or all selector and an optional stable source filter. The shared
store owns deterministic selection, destination preflight, stacking-key conflict rollback, optional
duration/counter/stack reset, provenance and sequence preservation, and side/slot/creature owner
movement. Source filters use stable side/party identity rather than transient slot position.
Zero-match operations emit a visible no-op; runtime conflicts emit a visible rejection and leave the
entire store unchanged. Applied operations reuse generic condition events/traces in original
condition sequence and add no RNG draw or content-name branch. Smart AI adds no speculative score
and continues to observe the immutable post-mutation condition snapshot through existing consumers.

Owning contracts updated: `BATTLE_SYSTEM_SPEC.md`, `EFFECT_TYPES_CATALOG_v0_5.md`,
`BATTLE_AI_SPEC.md`, `TESTING_STRATEGY.md`, `SCOPE_GUARD.md`, and this plan. Production changes:
the shared condition store, move effect union/compiler, controller resolver, events, and effect trace
kinds. Tests add strict compiler rejection; ID/tag/all and user/target/environment source selection;
all-scope removal; preserved and reset instance state; cross-side and intra-side transfer; side/slot
swap including an empty owner; atomic conflict rollback; resolver no-op/rejection; sequence and hook
order; zero-RNG proof; and checked-in `condition-mutation.golden` replay evidence. Schema/migration
and dependency impact: none; the existing open effect payload remains the serialized boundary.

Verification: the focused condition/hazard suite passed 49/49; full solution build passed with 0
warnings/errors; full solution passed 1,641 tests (1,418 Core, 104 Creator, 21 Runtime, 98 Tools).
Definition-aware audit regeneration was byte-identical at 937 inventoried / 84 certified with corpus
digest `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`.
Focused review found no blocking scope, architecture, schema, dependency, determinism, AI-fairness,
IP, lifecycle, atomicity, owner-topology, event, trace, hook-order, or named-move finding. No reference
row is prematurely certified. This closes 15E-7 and the 15E workstream. Next eligible package:
**15C-6 field, type, class, stat, and effectiveness queries**.

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

1. **15F-1 — Effective-value overlays (`IMPLEMENTED`; prerequisite 15C-1).** Lock immutable base plus
   overlay records for held item, ability, creature types, move type/class, derived stats/metrics,
   move list/PP owner, form, and decoy. Effective precedence is base → permanent instance data →
   form/snapshot replacement → additive overlays → suppression/ignore filters → query hooks. Later
   sequence wins only within the same precedence and key. Every overlay has source, owner, duration,
   cleanup, and trace identity. **Acceptance:** each layer alone/combined, suppression versus replace,
   stable ordering, definition immutability, switch/faint/end cleanup, and shared resolver/AI result.
2. **15F-2 — Held-item mutation (`PLANNED`; prerequisites 15F-1 and 15E hooks).** Lock require,
   consume, give, steal, swap, remove, destroy, restore-last-consumed, and suppress helpers. Validate
   holdability, empty/occupied targets, unremovable/sticky protection tags, source/target faint state,
   and one-item capacity before atomic mutation. Consumption records item ID, owner creature, turn,
   and cause; restore consumes that history only on success. Hook lookup changes at checkpoint tail.
   **Acceptance:** every success/failure pair, protected/empty/full, atomic swap, history aging,
   item-derived type/power/effect, hook refresh, events, conservation, and cleanup.
3. **15F-3 — Ability mutation (`COMPLETE`; prerequisites 15F-1 and 15E hooks).** Lock copy, swap,
   replace, suppress, ignore-for-query, and protect operations over effective abilities. Immutable base
   ability returns after temporary overlays clear. Validate unchangeable/protected tags and identical/
   missing ability behavior before atomic mutation. Newly effective hooks start at the next safe
   checkpoint, never halfway through the hook enumeration that changed them. **Acceptance:** operation
   matrix, protection, hook activation timing, suppression/ignore distinction, switch/faint/end
   reversion, doubles identity, and events/traces.
4. **15F-4 — Creature and move type overlays (`COMPLETE`; prerequisites 15F-1 and 15C-6).** Lock
   replace/add/remove/copy operations, maximum effective type count, duplicate elimination preserving
   first occurrence, empty-type fallback, source/duration/cleanup, and move-type override precedence.
   Grounded, STAB, effectiveness, immunity, and type-derived item/field queries consume the same
   effective list. **Acceptance:** mono/dual/add/remove/copy, duplicates/empty, overlay conflicts,
   STAB/effectiveness/grounding integration, switch cleanup, and conformance vectors.
5. **15F-5 — Stage, derived-stat, and metric mutation (`COMPLETE`; prerequisites 15F-1 and 15C
   queries).** Reuse stage bounds and lock set, maximize, average with floor, split, swap, steal,
   random-stat selection, pass, and temporary derived-stat/metric overlays. Operations calculate all
   outputs from one pre-mutation snapshot and commit atomically; random eligible stats use enum order
   and one draw only for multiple choices. **Acceptance:** bounds, odd averages, mixed positive/
   negative, empty random pool, atomic multi-target state, exact draw, pass whitelist, and cleanup.

   Progress (2026-07-19): **15F-5 IN PROGRESS — stat-stage mutation arithmetic engine landed.** The
   battle spec now locks the closed stage operations `set`/`reset`/`maximize`/`invert`/`copy`/`swap`/
   `steal`/`average`/`randomRaise` over the seven stageable stats. `BattleStageMutation` is a pure
   static core: every operation derives outputs from the caller's captured pre-mutation snapshot and
   clamps through `StatStages` (−6..+6); `swap`/`steal`/`average` are two-owner atomic; `steal` moves
   only positive boosts and zeroes them while leaving negatives; `average` uses `floor((a+b)/2)`;
   `randomRaise` considers only sub-max stats in `StatKind` enum order and draws exactly one `IRng`
   value (empty pool → no change, no draw, no chosen stat). Stat subsets are validated (nonempty,
   unique, no `Hp`) and snapshots must define all seven stats in range. 13 focused tests cover bounds/
   clamp, subset scoping, copy, swap atomicity, steal positive-only + ceiling clamp, floor-averaging
   (including negative odd cases), random one-draw + enum-order skip-maxed, empty pool, and validation.
   Schema/migration/RNG-order impact: none.

   Progress (2026-07-19): **15F-5 steal and random-raise ops wired into the controller.** Reconciled
   the engine with the existing stage effects: reset/copy/swap/invert already exist as controller
   effects and a Belly-Drum "maximize" is an ordinary large `statStage` delta, so `BattleStageMutation`
   was trimmed to the two operations those cannot express — `Steal` and `RandomRaise` — removing the
   duplicative set/maximize/invert/copy/swap/average methods. Added the `statStageSteal` and
   `statStageRandomRaise { delta?, onSelf? }` move ops (`MoveCompiler`), the `StatStealEffect`/
   `RandomStatRaiseEffect` records, and controller `ApplyStatSteal`/`ApplyRandomStatRaise` that follow
   the existing inline stage-effect pattern (snapshot → `BattleStageMutation` → `SetStage` with
   `StatStageChanged` events and the shared effect-chance trace); both are registered in the two
   opponent-targeting predicates. Spectral-Thief steal and Acupressure single-draw raise are proven
   end-to-end through `ResolveTurn`. Schema/migration impact: none; RNG draws exactly one value for the
   random raise, none for steal. Full solution passed **1,915/1,915** (1,602 Core, 104 Creator, 21
   Runtime, 188 Tools).

   Progress (2026-07-19): **15F-5 Speed Swap (derived-stat overlay) wired.** Added the `derivedStatSwap
   { stat }` move op, `DerivedStatSwapEffect`, and controller `ApplyDerivedStatSwap`, which reads each
   participant's effective stat from `EffectiveValues().Stats`, writes reciprocal additive
   `StatDeltaOverlay` contributions (each side's effective stat becomes the other's), and emits paired
   `DerivedStatMutated` events. The overlays clear on switch/faint/battle-end and are consumed by the
   existing effective-stats damage/turn-order path, so no consumer changes. Speed is the only supported
   stat for now (the op rejects others). Proven end-to-end: a Speed-Swap resolve emits the reciprocal
   events and persists ±(speed difference) additive overlays. Schema/migration/RNG impact: none. Full
   solution passed **1,916/1,916** (1,603 Core, 104 Creator, 21 Runtime, 188 Tools).

   Progress (2026-07-19): **15F-5 Power/Guard Split (derived-stat averaging) wired.** Added the
   `derivedStatSplit { group: offense|defense }` op, `DerivedStatSplitEffect`, and controller
   `ApplyDerivedStatSplit`, which averages each group stat (offense = Atk+Spa, defense = Def+Spd) across
   user and target using integer floor and writes reciprocal additive `StatDeltaOverlay`s. Generalized
   the derived-stat helpers (`DerivedStat`/`StatDelta` now cover Atk/Def/Spa/Spd/Spe) and gave each
   contribution a **per-stat** overlay key so a two-stat split keeps both contributions instead of the
   second overwriting the first (a collision I caught and fixed before it shipped). Paired
   `DerivedStatMutated` events fire only for stats that actually change. Proven end-to-end: Power Split
   over Atk 60/120 → 90 and Spa 41/80 → 60 (floor of 60.5) emits the four expected events and persists
   four reciprocal per-stat overlays. Schema/migration/RNG impact: none. Full solution passed
   **1,917/1,917** (1,604 Core, 104 Creator, 21 Runtime, 188 Tools).

   Progress (2026-07-19): **15F-5 Smart-AI parity and conformance vectors (159 → 163/937).** `SmartAi`
   now names neutral `statStageSteal`, `statStageRandomRaise`, and `derivedStatMutation` score components
   so the new ops don't fall through scoring. The conformance normalizer emits
   `StatMutationConformanceTests.Certified(...)` for the four new ops, and neutral decisions certify
   Spectral Thief (`statStageSteal`), Speed Swap (`derivedStatSwap`), Power Split and Guard Split
   (`derivedStatSplit`); regeneration kept the corpus digest
   `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f` and was byte-identical. Acupressure
   (`statStageRandomRaise`) is intentionally **not** certified: its `user-or-ally` target lands in the
   target-topology cohort, whose harness does not yet drive that selection, so random-raise stays proven
   by the engine/controller tests rather than a generated vector (a small follow-on if the harness gains
   user-or-ally support). Added `StatMutationConformanceTests` (per-row + operation-coverage) and a
   Smart-AI parity test. Full solution passed **1,922/1,922** (1,604 Core, 104 Creator, 21 Runtime, 193
   Tools). Remaining for 15F-5: a `cgm-review-pass` go/no-go to close; the Baton Pass stage-carry
   whitelist stays with 15G-1.

   Progress (2026-07-19): **15F-5 COMPLETE — focused review: GO.** A `cgm-review-pass` over the six
   15F-5 commits found no blocking scope, determinism, schema, dependency, IP, or architecture issue.
   Determinism holds: `randomRaise` draws exactly one `IRng` value and steal/swap/split draw none;
   overlay contribution keys are distinct per op/stat (`derived_swap_speed`,
   `derived_split_{group}_{stat}`), so multi-stat and multi-op writes compose instead of colliding.
   Both the damage path and turn order read the effective stat through `EffectiveValues`/`SpeedQuery`,
   so Speed Swap and the splits actually change ordering and damage. `BattleStageMutation` stays pure;
   reset/copy/swap/invert remain their existing effects (no duplicate system). 15F-5 delivers the
   `statStageSteal` and `statStageRandomRaise` stage ops and the `derivedStatSwap`/`derivedStatSplit`
   derived-stat ops with events, traces, Smart-AI parity, and four certified corpus vectors (163/937).
   Two documented carve-outs: Acupressure certification awaits target-topology harness `user-or-ally`
   support (random-raise is engine/controller tested), and Baton Pass stage-carry belongs to 15G-1.
   Schema/migration/RNG impact: none. Full solution **1,922/1,922** (1,604 Core, 104 Creator, 21
   Runtime, 193 Tools). Next eligible package: **15F-6 decoy/transform/snapshots/forms**.
6. **15F-6 — Decoy, Transform, snapshots, forms, and temporary move replacement (`COMPLETE`;
   prerequisites 15F-1/4/5 and 15D-7).** Lock decoy HP creation/cost/interception/bypass; snapshot
   copied fields and exclusions; copied move PP pool; original HP ratio preservation on max-HP form
   changes; once-per-battle form ownership; replacement duration; and switch/faint/end reversion.
   Snapshots copy effective values at application but never share mutable collections or mutate the
   target definition. Decoy receives eligible damage/status before the owner and emits distinct
   events. **Acceptance:** insufficient cost, exact decoy break/overflow policy, bypass matrix,
   snapshot depth/independence, copied PP, form HP ratio edges, nested overlay precedence, and golden.

   Progress (2026-07-19): **15F-6 IN PROGRESS — spec locked; decoy creation/interception arithmetic
   landed.** The battle spec now locks the decoy portion: creation costs `floor(maxHp × fraction)`
   (min 1), fails on an existing decoy (`AlreadyPresent`) or when current HP does not strictly exceed the
   cost (`InsufficientHp`), and produces a `BattleDecoyState(cost, cost)`; interception absorbs
   `min(incoming, decoyHp)`, breaks at zero, and passes **no overflow** to the owner. Implemented the
   pure `BattleDecoy.Create`/`Intercept` helper (no RNG, no owner mutation) over the existing 15F-1
   `DecoyOverlay`/`BattleDecoyState` foundation. 9 focused tests cover exact/floored/min-1 cost, the
   strict-HP requirement, already-present, partial absorb, exact and overkill break without overflow, and
   input validation. Schema/migration/RNG impact: none. Full solution passed **1,931/1,931** (1,613 Core,
   104 Creator, 21 Runtime, 193 Tools).

   Progress (2026-07-19): **15F-6 decoy creation wired into the controller.** Added the `decoy { num?,
   den? }` move op (default 1/4, must target the user), the `DecoyEffect` record, and controller
   `ApplyDecoy`, which calls `BattleDecoy.Create` on the source's live HP, pays the HP cost through the
   existing `Sap` path on success, writes a form/snapshot-layer `DecoyOverlay` cleared on
   switch/faint/battle-end, and emits a `DecoyCreated` event plus a `Decoy` effect trace; failures mark
   the action failed and change nothing. Proven end-to-end: Substitute deducts 1/4 max HP and persists a
   `(cost, cost)` decoy overlay; it fails without paying HP when current HP is not above the cost, and a
   second cast fails while one is present. Schema/migration/RNG impact: none. Full solution passed
   **1,934/1,934** (1,616 Core, 104 Creator, 21 Runtime, 193 Tools).

   Progress (2026-07-19): **15F-6 decoy damage interception wired into the standard-hit paths.** Added
   `InterceptWithDecoy` and the `BypassesDecoy` predicate (sound-tag moves bypass), routed into both the
   singles `ResolveMove` and doubles `ResolveDoublesMove` standard-damage hit sites: when the target has
   an effective `DecoyOverlay` and the move does not bypass, `BattleDecoy.Intercept` absorbs the hit, the
   overlay is reduced or cleared, `DecoyHit`/`DecoyBroke` fire, the owner takes nothing (no overflow), and
   the damage record is flagged `Substitute`. Interception fires only when a decoy is present, so every
   decoy-free golden is byte-unchanged (full suite stayed green). Proven end-to-end: a Substitute absorbs
   a standard hit with the owner untouched, an overkill breaks it with no overflow, and a sound move
   bypasses it to hit the owner. Schema/migration/RNG impact: none. Full solution passed **1,937/1,937**
   (1,619 Core, 104 Creator, 21 Runtime, 193 Tools).

   Progress (2026-07-19): **15F-6 decoy status/stat-drop block wired.** A present, non-bypassed decoy on
   the target now blocks move-inflicted status (`ApplyAilment`, `ApplyConfusion`) and opposing stat drops
   (`ApplyStatChange`, `ApplyStatChangeAll`) via a shared `DecoyBlocks(targetSlot, move)` guard reusing
   `BypassesDecoy`. Correct for pure status moves and non-breaking hits; a secondary that runs after the
   decoy broke this turn is a documented follow-on (needs per-turn "hit the substitute" state). Still
   gated on decoy-present, so goldens are unchanged. Proven: a Substitute blocks an opposing paralysis
   move (owner stays statusless) while a sound-tagged status move bypasses and paralyzes. Full solution
   passed **1,939/1,939** (1,621 Core, 104 Creator, 21 Runtime, 193 Tools).

   Progress (2026-07-19): **15F-6 post-break secondary block closed.** Added a per-move-resolution
   `_hitSubstitute` slot set, cleared at the top of both `ResolveMove` and `ResolveDoublesMove` and filled
   by `InterceptWithDecoy`; `DecoyBlocks` now blocks a move-inflicted status/stat-drop when the target's
   decoy is present **or** already absorbed a hit from this move, so a secondary that runs after the
   substitute broke is still blocked. Proven: a 250-power move with a 100% paralysis secondary breaks the
   substitute yet the owner is neither damaged (no overflow) nor paralysed. Still gated on decoy-present-
   or-hit, so goldens are unchanged. Full solution passed **1,940/1,940** (1,622 Core, 104 Creator, 21
   Runtime, 193 Tools).

   Progress (2026-07-19): **15F-6 decoy interception extended to fixed/OHKO damage.** Routed the
   fixed-damage/OHKO `DealMoveDamage` sites in both `ResolveMove` and `ResolveDoublesMove` through
   `InterceptWithDecoy`, so a substitute absorbs OHKO and fixed-damage moves (breaking as usual) and the
   owner takes nothing. Proven: a substitute blocks an OHKO (owner survives, sub breaks) and absorbs a
   40-point fixed hit without breaking a 50-HP sub. Still gated on decoy-present, so goldens are
   unchanged. The counter/Mirror-Coat, HP-formula (Endeavor/Pain Split), and delayed (Future Sight)
   damage paths intentionally do **not** intercept — Substitute does not shield those in reference
   mechanics — so no further damage-path wiring is required. Full solution passed **1,942/1,942** (1,624
   Core, 104 Creator, 21 Runtime, 193 Tools).

   Progress (2026-07-19): **15F-6 Transform value copy landed.** Spec-locked Transform and added the
   `transform` move op, `TransformEffect`, and controller `ApplyTransform`, which reads the target's
   effective creature types, derived stats, and ability and writes them as form/snapshot
   `CreatureTypesOverlay`/`StatsOverlay`/`AbilityOverlay` on the user plus a `transform` `FormOverlay`
   marker; the user keeps its own HP (the copied stat struct preserves the user's HP field). Snapshots
   copy values at application (types via `NormalizeTypes` `ToArray`, stats by struct value), so a later
   target change leaves the transformed user unchanged and the target definition is never mutated.
   Transform fails without change while already transformed and is blocked by Protect (registered in both
   opponent-targeting predicates). All overlays clear on switch/faint/battle-end. Proven: types/stats/
   ability copied with the user's HP preserved, snapshot independence, already-transformed failure, and
   battle-end reversion. Schema/migration/RNG impact: none. Full solution passed **1,946/1,946** (1,628
   Core, 104 Creator, 21 Runtime, 193 Tools).

   Progress (2026-07-19): **15F-6 multi-hit substitute leak fixed; move-selection architecture finding
   recorded.** `InterceptWithDecoy` now blocks every later hit of a move once that move has hit the
   substitute, so a multi-hit move that breaks the substitute mid-sequence no longer leaks its remaining
   hits to the owner (the bypass/immunity checks were also hoisted above the decoy lookup). Still
   decoy-present-or-hit gated, so goldens are unchanged; proven with a 2-hit move that breaks a 50-HP
   substitute leaving the owner untouched. **Architecture finding:** move *selection* and PP spend read
   the live `creature.Moves` via `MoveAt`/`EffectiveMoveIndex` — the `MoveListOverlay` is consumed only
   by damage/STAB queries, not selection. So Transform's move-list copy, temporary move replacement
   (Mimic/Sketch), and any overlay-driven move set need either a move-selection pass through the effective
   move list or a runtime move-list swap with reversion — an ADR-level decision, not an incremental
   slice; writing a `MoveListOverlay` for Transform without that rearchitecture would desync selection
   from STAB and is intentionally deferred. Full solution passed **1,947/1,947** (1,629 Core, 104
   Creator, 21 Runtime, 193 Tools). Remaining for 15F-6 (needs the selection decision): Transform copied-
   move PP pool, temporary move replacement; plus form changes (HP-ratio/ownership) and an infiltrator-
   style decoy bypass. Recommend an ADR before the next 15F-6 move-set slice.

   Progress (2026-07-19): **15F-6 Transform move copy landed via ADR-011 (Option B).** Recorded the
   user-directed decision in `docs/adr/ADR-011-transform-move-selection.md`: a runtime
   `BattleCreature.OverrideMoves`/`RestoreMoves` pair rather than routing move selection through the
   effective move list. `ApplyTransform` now copies the target's moves onto the user with a fresh PP pool
   (`min(5, base PP)`); `RestoreMoves` runs from `ClearVolatiles` (switch-out) and the faint cleanup, so
   the move set reverts alongside the type/stat/ability overlays. Move selection, legality, PP, and AI
   keep reading `creature.Moves` unchanged — no engine-wide rewire, confirmed by the full move suite
   staying green. Proven: a transformed creature carries the target's moves at 5 PP each, spends the
   copied PP independently of its originals, and the override round-trips through `RestoreMoves`/
   `ClearVolatiles`. Schema/migration/RNG impact: none. Full solution passed **1,949/1,949** (1,631 Core,
   104 Creator, 21 Runtime, 193 Tools).

   Progress (2026-07-19): **15F-6 temporary move replacement (Mimic) landed.** Added the `replaceMove`
   move op, `MoveReplaceEffect`, and controller `ApplyMoveReplace`, which copies the target's last-used
   move (`LastMoveUsed`) into the Mimic move's own slot with a fresh `min(5, base PP)` pool via the
   ADR-011 `OverrideMoves` path, emitting a `MoveReplaced` event and `MoveReplacement` trace. It fails
   without change when the target has not used a move or the user already knows that move, and reverts on
   switch/faint through the same `RestoreMoves` hook as Transform. Proven: Mimic copies the target's
   tackle into slot 0 at 5 PP, and fails when the target never moved. Sketch (permanent replacement) is
   intentionally deferred — it would need to rewrite the immutable base move list, which the ADR-011
   temporary override does not do. Schema/migration/RNG impact: none. Full solution passed **1,951/1,951**
   (1,633 Core, 104 Creator, 21 Runtime, 193 Tools). Remaining for 15F-6: form changes (HP-ratio/once-
   per-battle ownership — scope-check against the Forms/Mega deferral first) and an infiltrator-style
   decoy bypass; then a `cgm-review-pass` to close 15F-6 and advance to 15F-7.

   Progress (2026-07-19): **15F-6 COMPLETE — focused review: GO (163 → 166/937).** Scope-gated the
   remaining items: the form-change acceptance (HP-ratio preservation on a max-HP change, once-per-battle
   ownership, stat/type/ability/move remap, faint reversion) is already satisfied by the existing
   `BattleCreature.ApplyForm`/`BattleFormRuntime` infrastructure (`CurrentHp = max(1, oldHp*newMax/oldMax)`),
   so a new move-driven form change would be a speculative primitive no corpus move requires (SCOPE_GUARD).
   Certified Substitute, Transform, and Mimic as generated conformance vectors via a new
   `SnapshotConformanceTests`/normalizer rule (digest unchanged, byte-identical rerun). The focused
   review found no blocking scope, determinism, schema, dependency, IP, or architecture issue: decoy
   interception draws no extra RNG and is decoy-present-or-hit gated (every decoy-free golden
   byte-unchanged), Transform/Mimic draw none, ADR-011's `OverrideMoves`/`RestoreMoves` keeps move
   selection reading `creature.Moves` unchanged, and all new state clears on switch/faint/battle-end.
   15F-6 delivers decoy create + interception (standard/multi-hit/fixed/OHKO, overflow policy, status/
   stat-drop block, post-break, sound bypass), Transform value + move copy with a fresh PP pool, and
   temporary move replacement (Mimic). Documented carve-outs: Sketch (permanent move overwrite — needs
   base-move rewrite, out of the ADR-011 temporary override) and an infiltrator-style decoy bypass (the
   ability is not built) are deferred follow-ons. Schema/migration/RNG impact: none. Full solution
   passed **1,955/1,955** (1,633 Core, 104 Creator, 21 Runtime, 197 Tools). Next eligible package:
   **15F-7 unified move selector/executor**.
7. **15F-7 — Unified move selector/executor (`COMPLETE`; prerequisite 15D-7).** Finish selectors for
   known, target, last, party, random, environment, and temporarily replaced moves over effective
   move lists. Lock pool ordering, exclusions, target/PP/event ownership, recursion depth 8, and
   temporary/permanent replacement cleanup. This is the only entry to called/copied move execution;
   no selector invokes `BattleController` recursively outside the typed execution stack. **Acceptance:**
   pool and exclusion matrix, one-candidate no draw, multi-candidate exact draw, PP/event attribution,
   replacement reversion, target invalidation, and recursion golden.

   Progress (2026-07-19): **15F-7 spec-locked; effective-move-selection integration proven.** The
   unified selector/executor was already implemented by 15D-7 — `MoveReferenceResolver.Select` (PP/tag
   filter, dedupe-by-move, one-candidate no-draw, multi-candidate single draw), all eight selectors
   (`UserKnown`/`TargetKnown`/`UserLastUsed`/`TargetLastUsed`/`PartyKnown`/`AuthoredPool`/`EnvironmentPool`/
   `ExplicitReference`) in `MoveReferenceCandidates`, the typed `ResolveMoveInvocation` chain with
   `MaximumDepth` 8 + `DepthExceeded`, PP-owner attribution, target revalidation, and `MoveCalled`/
   `MoveSelection` events/traces. The one item this package adds — selection **over the effective move
   list** (ADR-011) — needs no new code because the known/last/party selectors read the live
   `creature.Moves`, which `OverrideMoves` replaces on Transform/Mimic. Spec-locked the contract in
   `BATTLE_SYSTEM_SPEC.md §Unified move selector/executor` and added
   `UserKnownSelector_ReadsTheEffectiveOverriddenMoveList`, proving a `callMove(UserKnown)` selects a
   move injected by `OverrideMoves` with the caller excluded and no draw. Schema/migration/RNG impact:
   none. Full solution passed **1,956/1,956** (1,634 Core, 104 Creator, 21 Runtime, 197 Tools). Remaining
   for 15F-7: a `cgm-review-pass` to confirm the acceptance matrix (already covered by the 15D-7
   move-reference package tests plus the new integration test) and close the package.

   Progress (2026-07-19): **15F-7 COMPLETE — focused review: GO. 15F workstream closed.** The acceptance
   matrix is fully covered by `BattleMoveReferencePackageTests`: pool ordering/exclusion + multi-candidate
   single draw (`Selector_PreservesAuthoredOrderFiltersAndUsesOnlyRequiredDraw`), PP/event attribution
   (`CalledMove_UsesDeclaredPpOwnerAndAttributesCallAndDamage`), target invalidation
   (`CalledMove_RevalidatesItsDifferentTargetShape`, `CalledMove_FailsWhenItsRevalidatedTargetDisappears`),
   depth-8 recursion and RNG goldens (`CallLoop_StopsAtDepthEightWithoutAnUnneededDraw`,
   `MoveReferenceSelection_MatchesExactRngAndEventGolden`), environment/party selectors
   (`EnvironmentAndPartySelectors_UseSharedBattleStateInStableOrder`), and effective-list selection
   (`UserKnownSelector_ReadsTheEffectiveOverriddenMoveList`); replacement reversion is covered by the
   Transform/Mimic `OverrideMoves`/`RestoreMoves` suites. The review found no blocking scope, determinism,
   schema, dependency, IP, or architecture issue — the selector/executor draws RNG only on a genuine
   multi-candidate choice, never re-enters `BattleController` recursively (typed `ResolveMoveInvocation`
   chain, depth 8), and reads the effective move list so temporarily replaced/copied moves behave
   natively. This closes Phase **15F** (overlays → item/ability/creature-type/stat mutation →
   decoy/Transform/Mimic snapshots → unified selector). Schema/migration/RNG impact: none. Full solution
   **1,956/1,956** (1,634 Core, 104 Creator, 21 Runtime, 197 Tools). Next eligible package: **15G-1
   unified switch intents** (which owns the Baton Pass stage-carry deferred from 15F-5).

Progress (2026-07-14): **15F-1 COMPLETE — focused review: GO.** The battle spec and effect catalog
now lock one runtime-only effective-value path with immutable captured base values and typed overlays
for held item, ability, creature types, derived stats, move list/PP owner, per-slot move type/class,
form, decoy, and weight/height metrics. `BattleOverlayStore` admits source/owner/layer/duration/cleanup
metadata atomically, resolves base → permanent instance → form/snapshot → additive → suppression,
and leaves numeric/typed query hooks as the final shared dispatcher stage owned by the applicable
15C package. Fixed replacement keys select the latest sequence only inside their layer/key;
source-keyed additive contributions combine deterministically, duplicate types preserve first
occurrence, and stat/metric additions clamp at one with checked arithmetic. Suppression can remove
only effective item or ability and can be bypassed only by an exact owned suppression sequence.
Switch-cleanup overlays remove while survivors follow the creature to its destination/reserve;
faint cleanup, duration tick/expiry, and unconditional battle-end cleanup return source-addressed
trace rows with removal reasons. Resolution returns the same immutable values/trace for normal
resolver and AI preview consumers and never mutates definitions, emits presentation events, or draws
RNG. Existing v6 form projection remains a compatibility consumer; concrete item/ability/type/form/
decoy mutations and their events are intentionally owned by 15F-2 through 15F-6. Neutral tests cover
base and every layer alone, every typed replacement, combined precedence, additive replacement/
combination/clamp, suppression/ignore, definition/input capture, PP-owner preservation, stable replay,
strict validation, duration, switch/faint/end cleanup, transfer, trace identity, and resolver/AI
parity. Focused review found no blocking scope, determinism, schema, dependency, or architecture
issues. Schema/migration impact: none. Dependency impact: none. RNG impact: none. No move cohort is
newly certified because this package is the shared overlay foundation; deterministic regeneration
remained 937 inventoried / 57 certified with digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f` and byte-identical outputs.
Verification: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0
warnings/errors; `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` passed 1,181 tests
(989 Core, 104 Creator, 21 Runtime, 67 Tools); the focused overlay suite passed 8 tests, the related
overlay/form/hook/query/AI filter passed 112 tests, and the Battle filter passed 712 tests;
regeneration and `git diff --check` passed. Next eligible package: **15C-2**.

Progress (2026-07-18): **15F-2 COMPLETE — focused review: GO.** The battle spec and effect catalog
now lock closed `itemRequire` and `itemMutation` ops over the existing effective-value overlay path.
`BattleItemState` atomically covers require, consume, give, steal, swap, remove, destroy,
restore-last-consumed, and timed suppression with catalog holdability, one-item capacity, key/item
guards, active-ability sticky guards, faint-state checks, and complete preflight before any write.
Consumption history is creature-identity-owned, ages to the latest consumed item, survives switching,
is spent only by successful restore, and clears at battle end. Effective item power and held-effect
lookup use the new overlay immediately after the current dispatcher snapshot; transferred/restored
items clear legacy consume-once markers, while Magic Room and timed suppression preserve ownership.
Successful mutations emit owner-addressed `HeldItemMutated` events plus compatibility consumption
events and one deterministic no-RNG effect trace. Smart AI rejects only known failing own-item
requirements, keeps hidden target-item requirements neutral, and records item mutation as a neutral
named component. The generated reference cohort adds six fully closed give/steal/swap/restore rows,
raising strict certification from 138 to **144/937** with 793 inventory-only and zero normalized,
compiled, blocked, or invalid rows; repeated regeneration was byte-identical with corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`.
Schema/migration impact: none (existing open effect params). Dependency impact: none. RNG impact:
none. Golden impact: one new neutral `item-mutation.golden`; no existing golden changed. Focused
review fixed swap's second-item preflight and restored-item consume-marker cleanup, then found no
remaining blocking scope, determinism, schema, dependency, IP, or architecture issue. Verification:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors;
the item package passed 10 tests, generated item conformance passed 7 tests, the full Battle filter
passed 1,278 tests, and the full solution passed **1,857/1,857** (1,562 Core, 104 Creator, 21 Runtime,
170 Tools). Next eligible package: **15F-3 ability mutation**.

Progress (2026-07-18): **15F-3 COMPLETE — focused review: GO.** The battle spec and effect catalog
now lock closed, data-defined `abilityMutation` copy/swap/replace/suppress operations over the shared
effective-value overlay path. `BattleAbilityState` preflights live participant identity, catalog
existence, missing/identical values, definition guard markers, replacement references, and complete
doubles recipient sets before any write. Copy supports target-to-user, user-to-target, and atomic
user-plus-allies projection while skipping already-equal recipients; swap is two-owner atomic;
replace names a validated catalog ability; suppression remains query-bypassable only by its exact
overlay sequence. All mutation overlays clear on switch, faint, or battle end without changing
`Ability`, `Species`, saved creature, or form definitions. Effective hook lookup now drives ordinary
dispatch, mutation guards, protection/side-condition bypass, grounded queries, and Smart AI; an
already-captured dispatcher snapshot remains unchanged and the next checkpoint sees the new hooks.
Success emits stable owner-ordered `AbilityMutated` events and one no-RNG `AbilityMutation` trace.
The historical eight-row audit group resolved to seven true ability mutations plus one incorrectly
classified two-turn healing lock, which is now certified through the existing action-filter family.
Strict certification rose from 144 to **152/937** with 785 inventory-only and zero normalized,
compiled, blocked, or invalid rows. Repeated generation was byte-identical: definitions SHA-256
`ffe851f9b076ea04c023fc74b6e5ced504b0dc78cada1bc09040f5ea9c474257`, manifest SHA-256
`f5caa9b979b2513715222d6f1dd51f20ed054bf1b87d9cb0d603b4d469ebaeb9`, and corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`.
Schema/migration impact: none (existing open effect params and runtime-only battle identity).
Dependency impact: none. RNG impact: none. Golden impact: one new neutral
`ability-mutation.golden`; no existing golden changed. Focused review fixed effective grounded-hook
lookup and mixed unchanged/changed group-copy handling, then found no remaining blocking scope,
determinism, schema, dependency, IP, AI-fairness, or architecture issue. Verification:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors;
the focused ability/validation package passed 15 tests, focused ability plus reclassified conformance
passed 18 tests, the full Battle filter passed **1,291/1,291**, and the full solution passed
**1,881/1,881** (1,577 Core, 104 Creator, 21 Runtime, 179 Tools). Next eligible package:
**15F-4 creature and move type overlays**.

Progress (2026-07-18): **15F-4 IN PROGRESS — creature-type mutation engine landed.** The battle spec
now locks the closed `replace`/`add`/`remove`/`copy` creature-type operations over the shared
effective-value overlay path. `BattleCreatureTypeState` reads the subject's current effective type
list, computes the result, deduplicates preserving first occurrence, and admits one permanent-instance
`CreatureTypesOverlay` atomically; `Species`, forms, and saved creatures stay immutable. It preflights
fainted subject/copy-source, enforces a configured maximum effective type count (`ExceedsMax`), applies
a configured empty-type fallback or fails `WouldEmptyTypes`, rejects no-op results (`NoChange`), and
requires a distinct source for `copy` and a unique nonempty valid type list for the list operations.
Cleared overlays restore base types on switch, faint, or battle end; existing grounded/STAB/
effectiveness/immunity consumers already read the same effective list, so no consumer special-cases
mutated types. The engine draws no RNG and emits no events; the owning controller will emit the
`CreatureTypesMutated` event and effect trace. Schema/migration/RNG impact: none; no move cohort newly
certified (shared engine). Verification: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx
--no-restore` passed with 0 warnings/errors; the new `BattleCreatureTypeMutationTests` passed 11
tests, the full Battle filter passed 1,302, and the full solution passed **1,892/1,892** (1,588 Core,
104 Creator, 21 Runtime, 179 Tools). Remaining for 15F-4: `typeMutation` move-op compile/validation,
`BattleController` dispatch + `CreatureTypesMutated` event/trace, Smart AI parity, move-type override
precedence, and generated conformance vectors.

Progress (2026-07-19): **15F-4 pipeline wired — creature-type `typeMutation` move op live.** The
effect catalog and battle spec now realize the closed authored op
`typeMutation { operation:replace|add|remove|copy, subject?, source?, types? }`. `MoveCompiler` parses
and shape-locks it (no chance, single effect, list-vs-copy exclusivity, distinct copy source, unique
nonempty `type:*` list, target compatibility mirroring `abilityMutation`); `BattleController` dispatches
it to `BattleCreatureTypeState`, emits one owner-addressed `CreatureTypesMutated` event on success, and
records one `CreatureTypesMutation` effect trace with the exact event range on every attempt.
`MoveRules` now reports missing `type:*` references from the op, `SmartAi` names a neutral `typeMutation`
score component, and `TargetsTheTarget` recognizes a target-subject mutation for redirection. Existing
grounded/STAB/effectiveness/immunity consumers already read the shared effective list, so mutated types
take effect with no consumer change. Schema/migration/RNG impact: none (existing open effect params,
runtime-only battle identity). No move cohort newly certified (no conformance vectors generated yet).
Verification: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx --no-restore` passed with 0
warnings/errors; `BattleCreatureTypeMutationTests` passed 16 (11 engine + 5 pipeline), and the full
solution passed **1,897/1,897** (1,593 Core, 104 Creator, 21 Runtime, 179 Tools). Remaining for 15F-4:
per-slot move-type override op/precedence and generated corpus conformance vectors.

Progress (2026-07-19): **15F-4 type-immunity consumers now read the effective list.** Audited every
residual base-`Types` read in `BattleController` and routed the type-derived immunity/query sites
through the shared `EffectiveTypes(slot)` overlay path: Leech Seed self-type immunity, on-hit and
contact-triggered status type-immunity, weather stat-hook typing, and weather residual-damage immunity.
With no overlay these return the base types (the exact path the damage/STAB calc already used), so
non-mutation behavior is byte-identical; a mutated creature now correctly gains or loses type-based
immunities. Schema/migration/RNG impact: none. Verification: `D:\dotnet\dotnet.exe build
CreatureGameMaker.slnx --no-restore` passed with 0 warnings/errors; a new end-to-end test proves a
`Soak`-style target mutation flips burn type-immunity (`BattleCreatureTypeMutationTests` now 17), and
the full solution passed **1,898/1,898** (1,594 Core, 104 Creator, 21 Runtime, 179 Tools). The
empty-type fallback stays `WouldEmptyTypes` (no invented typeless type); its ruleset wiring and the
per-slot move-type override op plus generated corpus conformance vectors remain for 15F-4.

Progress (2026-07-19): **15F-4 move-type override precedence locked by test.** Confirmed the effective
per-slot move type already flows through `EffectiveMoveIdentity` → `BattleDamageQueries.Identity`, so
STAB and effectiveness read `identity.EffectiveType` (the overlay-resolved `BattleEffectiveMove.Type`),
never the authored `move.Type`. Added `BattleMoveTypeOverrideTests` proving end-to-end that a
`MoveTypeOverlay` on a move slot grants STAB in the real damage pipeline (a Water attacker's authored
Normal move gains 1.5x STAB once its slot type is overridden to Water, raising the emitted
`DamageDealt` amount). No production change was required — the consumer path was already correct; this
closes the 15F-4 "STAB/effectiveness integration" acceptance item for move-type overrides. Full
solution passed **1,899/1,899** (1,595 Core, 104 Creator, 21 Runtime, 179 Tools). Remaining for 15F-4:
a data-driven move-type override *writer* op (only if a corpus move needs it), the empty-type fallback
ruleset decision, and generated corpus conformance vectors via the audit/normalizer tooling.

Progress (2026-07-19): **15F-4 conformance generator now recognizes the typeMutation family.**
`MoveConformanceNormalizer.Normalize` emits `TypeMutationConformanceTests.Certified(<key>)` for any
normalized move whose effects include `typeMutation`, mirroring the existing item/ability-mutation
family rules; `Families` already surfaces `typeMutation` from the effect op. Added a fixture-driven
normalizer unit test (`Build_RegistersTypeMutationVectors`) proving a `typeMutation` decision produces
exactly that test id and the `typeMutation` mechanic family — no real corpus required. This is the
generator half of the conformance work; the corpus (`docs/pokeapi-results/`) is gitignored and absent
from this worktree, so authoring the neutral `typeMutation` decisions in
`target-topology-decisions.v1.json`, adding `TypeMutationConformanceTests`, and regenerating
`definitions.v1.json`/`manifest.v1.json` against the local corpus is the next slice (which advances
the certified count past 152/937). Full solution passed **1,900/1,900** (1,595 Core, 104 Creator, 21
Runtime, 180 Tools).

Progress (2026-07-19): **15F-4 non-emptying type-mutation corpus cohort certified (152 → 157/937).**
Authored five neutral `typeMutation` decisions in `target-topology-decisions.v1.json` covering the
replace/add/copy operations (Soak/Magic-Powder replace onto the target, Trick-or-Treat/Forest's-Curse
add onto the target, Reflect-Type copy target→user), each keyed to a genuine status corpus move with no
competing mechanics, using sanitized `type:reference_NN` IDs. Regenerated `definitions.v1.json` and
`manifest.v1.json` from the local corpus: certified rose from 152 to **157/937**, the corpus digest
stayed `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`, and a second run was
byte-identical. Added `TypeMutationConformanceTests` (per-row certification theory + an
operation-coverage fact asserting replace/add/copy). Full solution passed **1,906/1,906** (1,595 Core,
104 Creator, 21 Runtime, 186 Tools). Remaining for 15F-4: the `remove` operation and the empty-type
fallback ruleset decision (Burn Up / Double Shock leave a mono-typed user typeless), which certifies the
last type-mutation cohort and lets 15F-4 close.

Progress (2026-07-19): **15F-4 `remove` cohort certified and empty-type fallback locked (157 → 159/937).**
Added Burn Up (`move-0682`) and Double Shock (`move-0892`) as self-type `remove` decisions, completing
operation coverage (replace/add/remove/copy); the coverage test now asserts all four. Regenerated the
catalog: certified rose to **159/937**, corpus digest stayed
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`, byte-identical on rerun. Locked the
empty-type fallback decision in `BATTLE_SYSTEM_SPEC.md`: `modern_reference` configures **no** fallback,
so an emptying mutation fails `WouldEmptyTypes` rather than materializing an unauthorized typeless
type; a ruleset that later defines a neutral typeless `type:*` may supply it with no engine change
(`// ponytail:` ceiling noted at the controller's `BattleCreatureTypeState` construction). Full solution
passed **1,908/1,908** (1,595 Core, 104 Creator, 21 Runtime, 188 Tools). 15F-4's creature-type mutation
and move-type override precedence deliverables are implemented, integrated, and certified; a focused
`cgm-review-pass` go/no-go is the only step before flipping 15F-4 to COMPLETE. A data-driven move-type
override *writer* op (Electrify-style) is the sole potentially-unexpressed corpus case and, if required,
is a small follow-on package.

Progress (2026-07-19): **15F-4 COMPLETE — focused review: GO.** A `cgm-review-pass` over the seven
15F-4 commits found no blocking scope, determinism, schema, dependency, IP, or architecture issue:
every in-battle type-effectiveness/immunity read routes through `EffectiveValues`/`EffectiveTypes`
(the only residual controller `.Types` is the mutation effect's own param); the creature-type engine
draws no RNG and mutates only overlays, leaving `Species`/forms/saves immutable; the corpus digest
stayed `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f` and byte-identical on rerun;
and the new conformance decisions use neutral `type:reference_NN`/`move-NNNN` keys only. 15F-4 delivers
the `typeMutation` replace/add/remove/copy engine + move op + controller event/trace + Smart-AI
component + validation, move-type override precedence, and seven certified corpus vectors (159/937).
Schema/migration/RNG impact: none. Full solution **1,908/1,908** (1,595 Core, 104 Creator, 21 Runtime,
188 Tools). The Electrify-style move-type override *writer* op is logged in `SCOPE_GUARD.md` §Idea
Ledger as a possible follow-on. Next eligible package: **15F-5 stage/derived-stat/metric mutation**.

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

1. **15G-1 — Unified switch intents (`COMPLETE`; prerequisites 15B-6 and 15E condition cleanup).**
   Lock voluntary, forced-target, random-forced, pivot-after-hit, self-switch, escape, and replacement
   intent payloads; source/target slot; candidate filter/order; trap/bypass; success checkpoint;
   failure; and transfer policy. Candidate lists use party index order; random selection draws once
   only for multiple candidates. All switches use the slot-addressed helper. Passable state is an
   explicit whitelist of eligible stat stages and registry-tagged creature conditions; identity,
   persistent status, HP, item, ability, non-passable volatiles, queues, and slot conditions never
   transfer. **Acceptance:** every intent valid/no-candidate/trapped, pivot miss/faint, random draws,
   doubles slot, entry hooks, transfer/cleanup matrix, creature conservation, and golden.

   Progress (2026-07-19): **15G-1 IN PROGRESS — Baton Pass stat-stage transfer landed (the 15F-5
   deferral).** Audited the existing switch flow: forced-target (`ForceSwitch`), voluntary (`Switch`),
   and post-faint replacement already route through the slot-addressed `SwitchTo`; the self-switch/pivot
   family and state transfer were missing. Added the `batonPass` move op, `BatonPassEffect`, controller
   `ApplyBatonPass`, and a `SwitchTo(slot, index, passStages)` overload that captures the outgoing
   creature's stage snapshot **before** its switch-out reset and applies the whitelist (the seven stat
   stages only) to the incoming creature after it materializes, emitting a `StatePassed` event.
   Identity/status/HP/item/ability/non-passable volatiles do not transfer. Proven: Baton Pass carries a
   +2 Atk / −1 Spe snapshot to the reserve, a voluntary switch carries nothing, and Baton Pass fails
   without a reserve. Schema/migration/RNG impact: none. Full solution passed **1,959/1,959** (1,637
   Core, 104 Creator, 21 Runtime, 197 Tools).

   Progress (2026-07-19): **15G-1 pivot-after-hit (U-turn/Volt Switch) landed.** Extracted a shared
   `SelfSwitch(slot, passStages)` helper (single healthy reserve, optional stage snapshot) used by both
   Baton Pass and the new `pivotSwitch` op/`PivotSwitchEffect`: a damaging move resolves its damage, then
   its terminal `pivotSwitch` secondary switches the user out to its sole reserve with no state transfer.
   Unlike Baton Pass, a pivot with no reserve does **not** fail — the move already hit. Proven: a 70-power
   pivot deals damage then switches (no `StatePassed`), and still succeeds (hits, no switch) when there is
   no reserve. Schema/migration/RNG impact: none. Full solution passed **1,961/1,961** (1,639 Core, 104
   Creator, 21 Runtime, 197 Tools).

   Progress (2026-07-19): **15G-1 trap gate on self-switch.** `SelfSwitch` now honors the same trap rule
   as voluntary switching (`Active(slot).IsTrapped`): a trapped user's Baton Pass fails and a trapped
   pivot still deals its damage but does not switch. Proven both ways in one test. Schema/migration/RNG
   impact: none. Full solution passed **1,962/1,962** (1,640 Core, 104 Creator, 21 Runtime, 197 Tools).
   Remaining for 15G-1: multi-reserve self-switch **selection** (the mid-turn replacement pick, shared by
   Baton Pass and pivots) and registry-tagged passable volatiles.

   Progress (2026-07-19): **15G-1 switch-intent cohort certified (166 → 170/937).** Added a normalizer
   rule emitting `SwitchIntentConformanceTests.Certified(...)` for the `batonPass`/`pivotSwitch` ops and
   neutral decisions certifying Baton Pass (`batonPass`), U-turn / Volt Switch (`pivotSwitch`, 70-power)
   and Flip Turn (`pivotSwitch`, 60-power). Regeneration kept the corpus digest
   `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f` and was byte-identical. Added
   `SwitchIntentConformanceTests` (per-row + Baton-Pass/pivot coverage). Full solution passed
   **1,967/1,967** (1,640 Core, 104 Creator, 21 Runtime, 202 Tools).

   Progress (2026-07-19): **15G-1 Baton Pass passable-volatile transfer.** Generalized the transfer from a
   stage dictionary to a `BattlePassedState` (stat stages + the passable creature volatiles: Leech Seed,
   confusion, and crit-stage boost); `SwitchTo` applies the whole snapshot to the incoming creature.
   Identity/status/HP/item/ability and non-passable volatiles still never transfer. Proven: Baton Pass
   carries Leech Seed and a +2 crit boost to the reserve (alongside the already-tested stage carry). Pivot
   still passes nothing. Schema/migration/RNG impact: none. Full solution passed **1,968/1,968** (1,641
   Core, 104 Creator, 21 Runtime, 202 Tools).

   Progress (2026-07-19): **15G-1 multi-reserve self-switch resolved via ADR-012 (Option C).** Recorded
   the user-directed decision in `docs/adr/ADR-012-mid-turn-self-switch-selection.md`: Core switches a
   self-switch to the **party-index-first** healthy reserve immediately and deterministically (no mid-turn
   pause), and the interactive player pick is a Runtime/Phase-16 concern on the existing submission seam.
   `SelfSwitch` now switches for any reserve count (was single-reserve only), so Baton Pass and pivots
   work with a full party. Proven: a Baton Pass user with two reserves switches to the party-index-first
   reserve carrying its stages, leaving the other reserve untouched. Determinism/golden-replay preserved
   (full suite unchanged). Schema/migration/RNG impact: none. Full solution passed **1,969/1,969** (1,642
   Core, 104 Creator, 21 Runtime, 202 Tools). 15G-1 now covers Baton Pass (stages + passable volatiles),
   pivot-after-hit, trap gating, multi-reserve self-switch, and certified conformance; a
   `cgm-review-pass` is the remaining step to close it and advance to 15G-3.

   Progress (2026-07-19): **15G-1 COMPLETE — focused review: GO.** The passable-state whitelist
   (`CapturePassedState`) captures and applies only stat stages, confusion, Leech Seed, and the crit-stage
   boost — identity, persistent status, HP, item, ability, trap, and other volatiles never transfer.
   Self-switch is deterministic (party-index-first reserve, ADR-012; no RNG), draws none of its own, and
   honors the trap gate; the transfer refactor left every switch golden byte-unchanged (full suite green).
   No blocking scope, determinism, schema, dependency, IP, or architecture issue. 15G-1 delivers the
   forced/voluntary/replacement flows (pre-existing) plus Baton Pass (stages + passable volatiles),
   pivot-after-hit (U-turn/Volt Switch/Flip Turn), trap gating, and multi-reserve self-switch, with four
   certified corpus vectors. The interactive player pick for a self-switch is a documented Runtime/Phase-16
   concern (ADR-012). Schema/migration/RNG impact: none. Full solution **1,969/1,969** (1,642 Core, 104
   Creator, 21 Runtime, 202 Tools). Next eligible package: **15G-3 counter/revenge/stored-release
   consumers** over 15G-2's damage memory.
2. **15G-2 — Bounded action and damage memory (`IMPLEMENTED`; prerequisite 15B-4).** Extend the minimal
   history service introduced by 15C-4 with typed action-attempt and per-hit records: turn, action
   sequence, source/target slot+creature, move, class/type, cause, attempted/connected/failed reason,
   amount before/after mitigation, actual HP removed, critical/contact/substitute flags, hit number,
   and faint result. Keep only the current and immediately previous completed turn plus active
   multi-turn aggregates; expose queries, never mutable lists. **Acceptance:** record completeness,
   per-hit/order, miss/immune/decoy, doubles isolation, aging/reset, bounded size, replay identity.
3. **15G-3 — Counter, revenge, stored-release, and damage-memory consumers (`IN PROGRESS`; prerequisite
   15G-2).** Lock qualifying source/target/class/cause/window for each registry row; last versus sum;
   multiplier/fixed return; target fallback; cannot-KO floor; and failure event. Stored release owns a
   bounded accumulator condition and clears on release, switch, faint, or expiration. **Acceptance:**
   qualifying/nonqualifying damage table, multiple hits/sources, decoy/residual exclusion, target gone,
   overflow-safe multiplication/clamp, normal failure, and event/trace golden.

   Progress (2026-07-19): **15G-3 IN PROGRESS — revenge (Metal Burst/Comeuppance) landed.** Audited the
   existing consumers: Counter/Mirror Coat already return 2× the per-class **sum** of damage taken this
   turn (via `PhysicalDamageTaken`/`SpecialDamageTaken`, reset at turn start), fizzling on none. Added the
   `revengeDamage { num?, den? }` op (default 3/2), `RevengeDamageEffect`, and a damage-resolution branch
   parallel to the counter branch: it returns `floor(multiplier × (physical + special taken this turn))`
   to the target with no RNG draw, the cannot-KO floor, and the `NoQualifyingDamage` fizzle; substitute-
   absorbed damage is excluded because only own-HP loss feeds the accumulators; multiplication is checked.
   Registered in the damaging gate. Proven: Metal Burst returns exactly 1.5× the damage taken, and
   fizzles when the user took none. Schema/migration/RNG impact: none. Full solution passed **1,971/1,971**
   (1,644 Core, 104 Creator, 21 Runtime, 202 Tools).

   Progress (2026-07-19): **15G-3 damage-memory cohort certified (170 → 174/937).** Relaxed the
   `revengeDamage` op's target check to match `counterDamage` (the corpus uses `specific-move`) and
   exempted revenge from the "damaging move needs a base-power formula" compiler guard; taught the
   normalizer that `counterDamage`/`revengeDamage` are replacement-power ops and emit
   `DamageMemoryConformanceTests.Certified(...)`. Certified Counter (`counterDamage:physical`), Mirror
   Coat (`counterDamage:special`), Metal Burst and Comeuppance (`revengeDamage`); regeneration kept the
   corpus digest `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f` and was
   byte-identical, which also exercised the revenge compiler path on the real corpus (Comeuppance's
   power-1 shape included). Added `DamageMemoryConformanceTests` (per-row + Counter/Mirror-Coat/revenge
   coverage). Full solution passed **1,976/1,976** (1,644 Core, 104 Creator, 21 Runtime, 207 Tools).

   Progress (2026-07-20): **15G-3 Bide mechanic landed (cross-turn stored-release consumer).** Added
   the `bide { turns? }` op (default 2) → `BideEffect`, exempt from the no-power compiler guard like
   revenge. `BattleCreature` gains a dedicated `BideDamage`/`BideTurns` store (separate from the
   per-turn Counter accumulators so it persists across turns); `RecordDamageTaken` feeds it while
   biding, and it clears on switch-out, faint, and unleash. The controller force-locks the user into
   Bide via the same submission-override path as multi-turn lock (submitted switches/moves are
   replaced; switching blocked; PP spent once), storing for N turns then unleashing `2 × BideDamage`
   at the singles opponent with the cannot-KO floor, fizzling `NoQualifyingDamage` when nothing was
   stored. New `BideStoring`/`BideUnleashed` events. Added `BattleBideTests` (store→unleash 2× the
   summed hits, zero-store fizzle, forced-move-over-submitted, forced-over-switch). Full solution
   **1,980/1,980** (1,648 Core, 104 Creator, 21 Runtime, 207 Tools). Spec-locked in BATTLE_SYSTEM_SPEC
   §"Counter, revenge, and stored-release consumers". Certified count unchanged at 174/937 — corpus
   certification of the Bide move (decision + normalizer testId + regen) is the next slice.
   Remaining for 15G-3: certify Bide's corpus move; per-hit/last-versus-sum consumers over the 15G-2
   `BattleActionHistory`; and doubles source-addressed targeting.

   Progress (2026-07-20): **15G-3 Bide certified (174 → 175/937).** Bide's corpus target is `user`
   (unlike Counter's `specific-move`), so the resolver now retargets any Bide move's damage phase to
   the opposing slot (singles; doubles addressing deferred with the source-addressed item). Taught the
   normalizer that `bide` is a replacement-power op and emits `DamageMemoryConformanceTests.Certified`;
   authored the neutral `move-0117` decision (`op: bide`, contact). Regeneration certified Bide (effects
   `[damage, bide]`, target `user`, power null) and kept the corpus digest
   `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`, byte-identical on rerun.
   Extended `DamageMemoryConformanceTests` to accept `BideEffect` and added a Target=User unleash test.
   Full solution **1,982/1,982** (1,649 Core, 104 Creator, 21 Runtime, 208 Tools).

   Progress (2026-07-20): **15G-3 last-versus-sum consumer fixed (Counter/Mirror Coat/revenge → last
   hit).** A corpus scan showed Counter's wording is "twice the damage from the **last physical hit**"
   — not the turn sum my accumulators computed. Added `BattleActionHistory.LastActualDamageTo(target,
   turn, class?)` (most recently resolved qualifying per-hit `ActualHpRemoved`) and switched the Counter
   and revenge branches to it. Deleted the now-dead per-turn `PhysicalDamageTaken`/`SpecialDamageTaken`
   accumulators and `ResetDamageTaken`; the per-hit history is the single source of truth, and
   `RecordDamageTaken` collapsed into a Bide-only `AccumulateBideDamage(int)`. Behavior is identical in
   single-hit singles (last == sum) and now correct under multi-hit/doubles. New
   `Counter_ReturnsTwiceTheLastHitOnly_NotTheSumOfAMultiHitMove` proves the divergence (2-hit move →
   2× the second strike, not 2× the sum). Full solution **1,983/1,983** (1,650 Core, 104 Creator, 21
   Runtime, 208 Tools). Spec updated. Remaining for 15G-3: doubles source-addressed targeting for
   counter/revenge/Bide, then a review pass to close the package.

   Progress (2026-07-20): **15G-3 doubles source-addressed reflection landed (Counter/Mirror
   Coat/revenge).** The doubles resolver silently no-op'd these no-power moves (the `!HasBasePower`
   fallthrough); now `ResolveDoublesReflect` intercepts them before target materialization and reflects
   at the exact attacker on the last qualifying `BattleDamageRecord.Source` — not the opposing slot 0 —
   fizzling `NoQualifyingDamage` when nothing qualifies or that attacker has left the field. Added
   `BattleActionHistory.LastDamageRecordTo` (record-returning sibling of `LastActualDamageTo`, which now
   delegates to it). New `BattleDoublesReflectTests`: Counter reflects to the slot-1 attacker leaving
   slot 0 untouched, Metal Burst any-class, wrong-category fizzle. Full solution **1,986/1,986** (1,653
   Core, 104 Creator, 21 Runtime, 208 Tools). Only residual 15G-3 item: Bide-in-doubles, which needs the
   deferred doubles forced-action handling — a `cgm-review-pass` should confirm that deferral and close
   the package.

   Progress (2026-07-20): **15G-3 COMPLETE — `cgm-review-pass`: GO.** Review of the damage-memory change
   set found one **FIX-NOW**, now fixed: the doubles-reflect fizzle recorded against
   `HistoryOwner(opposing slot 0)`, which throws when that slot is empty (a foe fainted mid-turn awaiting
   replacement) — a latent crash on a doubles Counter/revenge fizzle. Switched the nominal owner to
   `sourceOwner` (always valid; a non-`Connected` `NoQualifyingDamage` record never feeds a future
   reflect read) and added `Counter_FizzlesGracefullyWhenOpposingSlotZeroIsEmpty` (ally KOs foe slot 0,
   then Counter fizzles). Other dimensions clean: Core-pure, deterministic (no reflect RNG draws; history
   reads are deterministically ordered), no new deps, no serialized-schema change (the conformance JSON
   is a tools artifact; corpus digest unchanged), closed named ops only. **Bide-in-doubles is an
   accepted scoped deferral** — biding is force-locked in doubles too, but its unleash is a benign no-op
   there (state clears safely via `AdvanceBide`/`EndBide`/`ClearVolatiles`; `BideDamage` is never read
   outside the singles branch); it rides the deferred doubles forced-action layer. Full solution
   **1,987/1,987** (1,654 Core, 104 Creator, 21 Runtime, 208 Tools). 175/937 certified. Next package:
   **15G-4** (healing/costs/cures/transfer/revival/HP-equalization) — audit first; several ops already
   exist, so it should be a batch-certification cohort.
4. **15G-4 — Healing, costs, cures, transfer, revival, and HP equalization (`PLANNED`; prerequisites
   15C-2 and typed selections).** Lock flat/fraction/full/formula/damage-derived healing; current/max
   HP damage and costs; drain/recoil/crash; persistent/volatile cure; status transfer; sacrifice;
   revive fraction; and HP match/equalize over active, ally, party, fainted-party, side, and slot.
   Fractions floor, successful positive healing clamps to max, revival requires fainted and restores
   at least 1 HP, costs validate affordability before other mutations unless marked sacrificial, and
   failed atomic composites change nothing. **Acceptance:** 0/1/full/fainted boundaries, odd fractions,
   heal block, multi-target rounding basis, cost conservation, cure/transfer conflicts, party scope,
   atomic failure, and family goldens.
5. **15G-5 — Delayed/replacement healing and cures (`PLANNED`; prerequisites 15D-4, 15E conditions,
   and 15G-4).** Encode future healing/cure as slot-owned queue/condition payloads with live occupant
   resolution, stored source metadata, exact due checkpoint, and empty-slot persistence/expiration.
   Replacement occupants receive live-slot effects; creature-snapshot effects follow only their
   creature. **Acceptance:** original versus replacement occupant, empty then filled, source gone,
   simultaneous entries, heal block at resolution, expiration, and event/RNG golden.
6. **15G-6 — Post-battle rewards and overworld Core actions (`PLANNED`; prerequisite battle outcome).**
   Lock typed Core results for money/reward adjustment and content-agnostic overworld requests
   (`cutObstacle`, `moveObstacle`, `travelWater`, `illuminate`, `teleport`, or other corpus-required
   generic action tags). Battle resolver emits the result/request and never performs filesystem,
   scene, map, animation, or UI work. Each request carries actor, action tag, target requirement,
   validation result, and consumption policy; Runtime later supplies world context and applies Core
   legality. Presentation-only and true no-effect markers are distinct and manually reviewed.
   **Acceptance:** battle/post-battle timing, win/loss/cancel, money clamp, missing world context,
   request validation, no Runtime dependency, and every marker classified.

Required evidence: creature/HP/item conservation tests; switch transfer/cleanup goldens; counter
qualification tables; bounded-memory/turn-aging tests; party/fainted-target validation; reward and
overworld action tests; manual review of every no-battle/post-battle marker.

Exit: every affected move has state-conservation and event-order coverage; UI remains a consumer.

Progress (2026-07-10): HP-mutation handling is expanded through reusable operations. `heal` now
declares a self or target recipient, and `hpFraction` supports deterministic current/max-HP healing
or damage through the existing `Heal` and `Sap` primitives. Compiler validation and resolver/event
tests cover both paths. Team/party healing, cures, revival, HP equalization, switch-linked recovery,
and the normalized per-move conformance definitions remain open.

Progress (2026-07-14): **15G-2 COMPLETE — focused review: GO.** `BattleActionHistory` now owns
immutable typed per-hit damage records beside its 15C-4 attempts, bounded to the current and
immediately previous turn. Records preserve stable side/party creature identity plus the slot at
resolution, move/class/type, standard/fixed/level/OHKO/counter/HP-formula cause, target-level or
resolved-hit outcome, calculated/applied/actual damage, hit number, critical/contact/substitute,
and faint evidence. Source/target queries match stable creature identity across slot movement,
return copies, sum only actual creature HP removal with checked overflow, and survive switch/faint
until normal aging or battle end. The shared singles/doubles resolver writes the same service beside
existing miss/block/damage paths for standard, spread, multi-hit, fixed, level, OHKO, legacy counter,
target HP fraction, and status-class source-matching HP damage without adding events, traces, RNG,
schema, dependencies, or a second store. Substitute interception remains a validated typed service
vector for its owning overlay package; 15G-3 remains the consumer migration package.

Files: `src/Cgm.Core/Battle/BattleActionHistory.cs`, `src/Cgm.Core/Battle/BattleController.cs`,
`tests/Cgm.Core.Tests/Battle/BattleDamageHistoryTests.cs`,
`tests/Cgm.Core.Tests/Battle/BattleDamageMemoryIntegrationTests.cs`, `docs/BATTLE_SYSTEM_SPEC.md`,
`docs/EFFECT_TYPES_CATALOG_v0_5.md`, `docs/TESTING_STRATEGY.md`, `docs/MOVE_AUDIT_SYSTEM_PLAN.md`,
`docs/AGENTS.md`, `docs/SCOPE_GUARD.md`, and this plan. Schema/migration and dependency impact: none.
No new move is certified because this is a shared evidence foundation; deterministic full-argument
normalization remained byte-identical at 937 inventoried / 78 certified with corpus digest
`5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`. Verification:
the focused 15G-2 matrix passed 20 tests; `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx
--no-restore` passed with 0 warnings/errors; `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx
--no-build --no-restore` passed 1,353 tests (1,136 Core, 104 Creator, 21 Runtime, 92 Tools); and the
complete Core coverage run exercised 574/586 changed instrumented production lines (97.95%). The
review found and fixed non-comparable slot sorting, status-class HP-formula admission, typed cause on
pre-hit failures, and exact-slot query matching that would lose a creature's records after position
movement; `git diff --check` passed. Next eligible package: **15C-5**.

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

Ordered packages:

1. **15H-1 — Reference decision registry (`PROCESS READY`).** Generate the blocked-key list from the
   manifest; for each key record source hash, exact unanswered question, structured metadata, concise
   cited mechanics conclusion, ruleset/profile, topology, generic capability owner, and reviewer.
   Use §2.1's conflict default rather than stalling on generation differences.
2. **15H-2 — Normalized definition registry (`PROCESS READY`; runs continuously).** Produce one
   canonical generic definition per key with stable property/effect ordering, explicit defaulted
   values, capability tags, topology/profile, and definition hash. Unknown op/param/tag or a silent
   no-op fails generation. Presets expand before hashing and cannot contain executable content names.
3. **15H-3 — Capability routing (`PROCESS READY`).** Group failed normalization/compile/resolve rows
   by reusable missing behavior and attach them to the first owning 15B-15G package. A group contains
   all known keys, required success/failure contexts, and proposed lowest promotion-ladder level.
4. **15H-4 — Zero-gap review (`PROCESS READY`; prerequisite 15B-15G).** Manually review every
   `presentationOnly`, `noBattleEffect`, `postBattleReward`, overworld request, ruleset override, and
   remaining alias; regenerate hashes/counts and require zero unknown/unmapped/reference-blocked.

Acceptance: registry rows are complete and cited; generation is byte-identical; source names/prose do
not enter shipped/test fixture content; every key has a normalized hash and registered test IDs; and
all capability failures are routed rather than hidden.

Exit: 937 normalized definitions; zero unknown/unmapped/reference-blocked rows.

#### 15I — AI and simulation awareness

- AI legality uses the same target/topology resolver.
- Scoring understands every effect family at a conservative useful level or explicitly values the
  shared primitive outcomes; it never duplicates resolution.
- Smart AI supports doubles legal actions when a doubles battle is configured.
- Re-run seeded determinism, side-balance, and difficulty measurements after mechanics stabilize.

Implementation packages:

1. **15I-1 — Legal action enumeration (`PLANNED`; prerequisites 15B-15H mechanics).** Enumerate moves,
   typed targets, switches, items, forms, replacements, and pass/fallback exclusively through Core
   legality APIs. Candidate order is action category, source slot, authored move index, typed target
   topology/party order, then item/form ID. AI cannot inspect the opponent's submitted action or
   hidden runtime state. **Acceptance:** every certified move has its valid contexts, invalid targets
   absent, doubles collective conflicts filtered, replacement mode, and deterministic order.
2. **15I-2 — Primitive outcome scoring (`PLANNED`; prerequisite 15I-1).** Lock a score registry for
   expected own/opponent HP delta, KO, status/control, stat setup, field/side condition, switch tempo,
   delayed value, resource gain/cost, self-risk, failure probability, and information-neutral
   uncertainty. Every compiled primitive/query/condition must map to one or more signals or an
   explicit neutral presentation-only classification; unknown executable behavior is an error, not
   zero. **Acceptance:** registry completeness test against catalogs and sign/magnitude boundaries.
3. **15I-3 — Preview and combination (`PLANNED`; prerequisites 15I-1/2).** Use pure Core query helpers
   or a cloned-state resolver preview with deterministic expected values; never reimplement formulas.
   Combine signals additively after normalized HP percentages, cap any single non-KO setup/control
   signal below a guaranteed KO, and apply profile weights from `BATTLE_AI_SPEC`. Preview does not
   consume live RNG/state. **Acceptance:** resolver/preview parity tables, immunity/failure risk,
   multi-target aggregation, self-cost, delayed/switch cases, and no live-state mutation.
4. **15I-4 — Memory and explanation (`PLANNED`; prerequisite 15I-3).** Extend AI memory only with
   player-observable action/outcome data and produce a stable score table listing candidate, legality,
   component values, total, and selected tie/noise draw. Memory is bounded by battle duration and
   cleared at battle end. **Acceptance:** hidden-info audit, stable explanation golden, switch/item/
   condition memory, and bounded size.
5. **15I-5 — Deterministic selection and tuning (`PLANNED`; prerequisite 15I-4).** Select highest
   score; exact ties use one `Next(tieCount)` over candidate order. Basic remains damage-greedy and
   Random selects uniformly from legal candidates. Run at least 100 seeds per benchmark pairing in
   singles and doubles; log teams, ruleset, seeds, win/turn/crash/illegal-action rates. Tune weights
   only after zero crashes/illegal actions. **Acceptance:** corpus smoke covers every certified move,
   deterministic replay, side-swapped benchmark within documented variance, Smart exceeds Basic on
   the challenge fixture without forbidden information.

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

Ordered closeout packages (all `PROCESS READY`; prerequisite 15I complete):

- **15J-1 registry freeze:** generator must fail on missing/duplicate key, stale source hash,
  unregistered definition/test ID, unknown capability tag, or backward status transition.
- **15J-2 conformance:** run all required valid, failure, alternate-topology, and alternate-profile
  vectors; 937 passing primary vectors alone is insufficient when an entry declares more contexts.
- **15J-3 fuzz/soak:** run at least 10,000 seeded battles total, including at least 2,000 doubles and
  every certified move in a successfully selected action; cap each battle at 1,000 turns and treat
  nontermination, exception, invalid state, or replay mismatch as failure with seed/input artifact.
- **15J-4 static audit:** scan production Core code and serialized catalogs for official names,
  content-key branches, named handler types, fallback no-ops, nondeterministic APIs, mutable base
  definitions, direct dictionary-order decisions, and unbounded recursion/queues.
- **15J-5 compatibility/review:** run schema migrations, public API consumers, all goldens, full
  solution tests, and `cgm-review-pass`; resolve every FIX-NOW and explicitly accept/defer each lower
  finding before GO.
- **15J-6 contract freeze:** document public construction/state/action/selection/outcome/event/trace/
  legality/query surfaces, threading/ownership, versioning rule, and examples using neutral content.

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

### 6.1 Locked Runtime defaults

- Target is Windows `win-x64`, OpenGL 3.3 core, 60 fixed simulation ticks/second, maximum five catch-up
  ticks per rendered frame, and render interpolation that never mutates simulation state.
- Default virtual resolution is 240×160. Scale is the largest positive integer fitting the client;
  smaller windows use scale 1 with symmetric clipping/letterbox. Black letterbox bars; nearest-neighbor
  texture sampling; no mipmaps, rotation, shaders beyond textured/color quads, or dynamic atlasing.
- Input actions are Up/Down/Left/Right/Confirm/Cancel/Menu/Run plus DebugToggle in debug builds.
  Keyboard defaults are arrows/WASD, Enter/Z, Escape/X, and Shift. One keyboard and first connected
  gamepad are supported; simultaneous devices merge by action. Rebinding rejects duplicate bindings
  within one device unless explicitly swapped and always preserves Confirm/Cancel recovery defaults.
  Press/release edges observed on a render frame with zero simulation ticks remain buffered until the
  first later tick and are delivered once; held state reflects the latest poll.
- Runtime owns one top-level scene stack. Only the top scene receives input/update; covered scenes
  render only when the top scene declares overlay. Transitions block input, run in simulation ticks,
  and never load content on a render callback.
- No unlisted package is authorized. `Silk.NET.OpenAL` 2.23.0 is approved for Phase 16E and recorded
  in `TECH_STACK.md`; add its Runtime project reference only when 16E becomes current. Do not
  substitute another library or add audio code during Phase 15.
- Version 1.0 audio source is uncompressed PCM WAV only: RIFF/WAVE, little-endian signed 16-bit,
  mono/stereo, 44.1 or 48 kHz. Other formats, including OGG, fail validation with a conversion hint
  until an explicitly approved decoder is added. Runtime streams music PCM buffers and loads short
  SFX; OpenAL performs playback/mixing, not decoding.
- Runtime data source is exactly one of `--project <folder>` or adjacent `config.json`+pack. Both
  produce equivalent `GameDb` and asset lookup. Missing/incompatible content shows one actionable
  error, exits nonzero before scene construction, and never falls back to built-in demo data.
- Save writes are temp-file → flush → replace with previous file retained as `.bak`. Load tries the
  primary only; corruption offers the validated backup rather than silently replacing state.

### 6.2 Ordered Runtime packages

1. **16A — Content-agnostic host and raw/pack parity (`PLANNED`; prerequisite Phase 15 GO).**
   - **Spec lock:** complete `ENGINE_RUNTIME_SPEC` boot arguments, error taxonomy, data/asset database,
     ownership/disposal, and startup state machine using the defaults above.
   - Remove showcase move/species/map IDs, embedded battle construction, and implicit fixture paths.
     Parse mutually exclusive dev/export modes; validate config/pack/project and required start IDs;
     build the same immutable `GameDb` plus `IAssetSource` contract from either source.
   - Define exit codes: 0 success, 2 arguments/config, 3 content validation/version, 4 asset load,
     5 save, 6 runtime initialization, 10 smoke assertion. Log one structured diagnostic to stderr;
     release UI shows friendly summary without stack trace, debug log retains exception detail.
   - **Acceptance:** valid raw/pack database equality; missing/invalid/version/hash/start-map/asset
     cases and exit codes; no official/demo ID scan; disposal on partial failure; exported smoke still
     passes. Exit: host reaches BootScene from either source with no content assumptions.
2. **16B — Fixed-step host and renderer (`PLANNED`; prerequisite 16A).**
   - **Spec lock:** exact host loop, `IRenderer` calls, coordinate systems, blend/sort rules, atlas/
     texture lifetime, context-loss refusal, and frame diagnostics.
   - Feed real elapsed time only into `FixedStepClock`; poll input once per outer frame, expose pressed
     edges to the first due tick only, run 0–5 ticks, render once with interpolation alpha, and reset
     clock after suspend/resize stalls. Simulation never reads wall time.
   - Implement the named renderer directly over approved Silk OpenGL: one dynamic quad batch, premultiplied
     alpha blend, stable submission order by layer then sequence, flush on texture/capacity/layer break,
     atlas texture load, source rectangles, flip, UI/world projection, scissor, tile chunks, and
     idempotent reverse-order disposal. Start capacity 2,048 quads and grow to the smallest power of
     two needed; no pooling layer until a measured allocation fails the budget.
   - **Acceptance:** synthetic loop 0/1/5/overflow ticks and edge input; virtual viewport matrix;
     null-renderer command golden; atlas/source/flip/layer batch tests; hidden GL smoke screenshot of
     neutral fixture; zero GL object leak across 100 load/unload cycles. Exit: animated fixture map
     renders correctly at integer scale while headless sim replay remains byte-identical.
3. **16C — UI kit, input, and scene flow (`PLANNED`; prerequisite 16B).**
   - **Spec lock:** scene lifecycle (`Enter`, fixed `Update`, `Render`, `Exit`, `Dispose`), overlay rule,
     transition timeline, focus/navigation, text layout, typewriter skip, and rebinding persistence.
   - Implement Boot → Title → New/Continue → Overworld with push/pop Menu and replacement Battle.
     UI primitives are 9-slice panel, bitmap text with newline/wrap/alignment, typewriter, cursor,
     vertical list, grid, prompt/choice, HP/resource bar, fade, and message log. Navigation wraps only
     where the control opts in, disabled entries remain visible but skipped, Cancel restores previous
     focus, and every menu is operable without a pointer.
   - Persist options separately from game saves; invalid bindings revert to defaults with warning.
   - **Acceptance:** lifecycle/transition/focus goldens, list/grid disabled/empty/overflow, text wrap
     and typewriter tick tables, resize/rebind/gamepad disconnect, and headless input scripts reaching
     every scene. Exit: title/new/continue/menu flow is reusable and content-driven.
4. **16D — Asset-backed overworld integration (`PLANNED`; prerequisites 16A-C).**
   - **Spec lock:** OverworldScene state ownership, map/entity instantiation, render layers, interaction
     priority, encounter/trainer trigger order, dialogue commands already represented in Core data,
     warp/blackout transitions, and debug spawn contract.
   - Load tile chunks and sprites from assets; drive Core movement/collision/ledge/warp decisions at
     fixed ticks; queue one movement intent at a time. Interaction priority is facing entity → facing
     trigger/object → current tile. On completed step: warp → tile trigger → trainer sight → random
     encounter, stopping at the first transition. NPCs update in stable entity-ID order with injected
     Core RNG. Pickups/flags mutate save state through Core operations.
   - Centers heal then set blackout return; mart/PC/NPC objects open reusable scenes; blackout restores
     party per Core rule, deducts Core-calculated money, moves to checkpoint, and saves only on explicit
     save/autosave policy.
   - **Acceptance:** collision/ledge/warp/interaction priority tables, deterministic NPC/encounter
     replay, trainer once/defeat flag, pickup persistence, blackout, chunk boundary/camera, missing
     asset diagnostics, and raw/pack parity. Exit: fixture supports walking, dialogue, encounter,
     trainer, warp, pickup, center, mart, and PC entry.
5. **16E — Player systems, save, clock, and audio (`PLANNED`; prerequisite 16D).**
   - **Spec lock:** party/bag/storage/shop scene contracts, progression/evolution prompts, save slots,
     game clock/day-night input, audio buses/loop/crossfade, and debug overlay content.
   - Implement one manual save slot plus `.bak`; New Game initializes project start state, Continue
     validates content/save versions, and save is allowed from overworld/menu only. Party is six;
     overflow capture uses Core storage. Bag groups authored pockets; shop buy/sell validates money/
     capacity atomically. Evolution/move-learn prompts consume Core results and allow decline where
     rules permit. Game clock advances one minute per 3,600 sim ticks and is persisted.
   - Audio has Music/Sfx master volumes 0–100, one streamed music track with 30-tick linear crossfade,
     up to 16 simultaneous one-shot SFX dropping the oldest completed voice first, and clean no-audio
     fallback with warning. Debug overlay shows FPS/ticks, scene, player/map, RNG seed/replay state,
     validation/event tail, and collision; compiled out or inaccessible in release flavor.
   - **Acceptance:** save/temp/backup/corrupt/newer-version matrix; full party/storage/shop conservation;
     level/evolution prompt replay; clock rollover/day-night; audio missing/device loss/volume/crossfade;
     published `win-x64` output contains and loads the package-provided native OpenAL implementation
     on a machine without system OpenAL, with its license/notice recorded; debug release exclusion.
     Exit: player systems survive save/relaunch identically.
6. **16F — Event-driven battle presentation (`PLANNED`; prerequisites 16C/E and Phase 15 contract).**
   - **Spec lock:** BattleScene state machine, action/typed-target/replacement menus, Core request/
     response boundary, event-to-presentation catalog, animation queue, skip/fast-forward, and outcome
     return. Runtime never predicts damage, legality, target fallback, faint, or status from state.
   - Render singles/doubles layouts from slots; enumerate legal actions/targets from Core; support
     move, form, switch, item, pass/fallback, replacement, and capture where legal. Submit only complete
     atomic action sets. Consume every Core event in order into generic animation/text commands; unknown
     event shows a debug error and fails tests rather than disappearing.
   - Animation is presentation-only: 6-tick minimum event beat, Confirm fast-forwards current beat,
     held Confirm uses 4× presentation speed without changing simulation/event order. Battle return
     applies Core outcome/progression/reward/save mutations once.
   - **Acceptance:** event-catalog completeness test, singles/doubles action and target navigation,
     invalid resubmission, simultaneous replacement/draw, capture/trainer restrictions, fast-forward
     event identity, and battle→overworld state conservation. Exit: every certified primitive can be
     presented generically even when it has no bespoke animation.
7. **16G — Runtime verification and phase gate (`PLANNED`; prerequisites 16A-F).**
   - Build a neutral original fixture script covering new game, movement, dialogue, encounter,
     capture, party/storage, item/shop, save/reload, trainer, evolution, blackout, and doubles debug.
     Run identical scripted inputs in raw and packed modes and compare Core state, save bytes excluding
     allowed timestamps, events, scene transitions, and final screenshot hashes/tolerances.
   - Budgets on the documented reference machine: 60 Hz without missed updates for the fixture;
     p95 update ≤4 ms and render ≤12 ms over 10,000 frames; steady-state managed allocation ≤1 KB/
     frame outside loads; startup to title ≤3 s warm/≤6 s cold; 100 scene/map/battle cycles with no
     monotonic managed/native/GL resource growth beyond 5% after GC and disposal.
   - **Acceptance:** headless replay deterministic twice, raw/pack parity, save/relaunch, all scene
     transitions, resource/performance report, keyboard+gamepad smoke, malformed-content errors, full
     build/tests, and focused review GO. Exit: the Phase 16 fixture loop passes end to end.

Phase 16 excludes Creator workflows, export-template production, original demo breadth, Core rule
changes, scripting, localization, networking, installer, and additional renderer backends. A missing
Core rule is recorded as a Phase 15 regression and fixed there before Runtime consumes it.

Phase 16 GO requires all:

- [ ] 16A-16G are VERIFIED and their `ENGINE_RUNTIME_SPEC` sections contain no unresolved marker.
- [ ] Raw and packed fixture replays produce equal Core/save/event outcomes.
- [ ] No Runtime content ID or game-rule branch exists.
- [ ] Fixed-step, renderer/resource, input, scene, overworld, player/save/audio, and battle event
      matrices pass; the approved OpenAL reference is added only in Phase 16E.
- [ ] The complete fixture route and singles/doubles presentation pass keyboard and gamepad smoke.
- [ ] Runtime performance/resource budgets and focused review are GO.

## 7. Phase 17 — Creator Application Completion

Goal: make every required engine capability authorable without hand-editing JSON.

Prerequisite: Phase 16 exposes stable Runtime launch/debug contracts and Phase 15 catalogs are frozen.
Creator writes project data through Core schemas/validation and launches Runtime out of process.

### 7.1 Locked Creator defaults

- One project is open per Creator process. Documents are tabs owned by `ProjectSession`; all edits
  execute through `UndoStack`. Views contain binding/adaptation only; Core schemas and validators are
  never duplicated in ViewModels.
- Autosave writes recovery snapshots—not source files—after 120 seconds of dirty inactivity and on
  app deactivation, retaining the newest five. Explicit Save is the only operation that replaces
  project source. Recovery is offered after an unclean close and never applied without confirmation.
- Recent list retains ten canonical absolute folders, newest first; missing paths remain visible with
  remove action. File dialogs start at the last successful folder. Stable IDs never rename.
- Destructive delete always runs reference usage search. Referenced entities cannot be deleted until
  references are removed or an explicit Core-supported replacement is selected; no blanket cascade.
- Lists with more than 200 rows and canvases larger than the viewport virtualize. Validation debounce
  is 400 ms; Save/Play/Export force immediate complete validation. Errors block Play/Export; warnings
  require one explicit continue per invocation.
- Keyboard baseline: menu shortcuts, tab traversal, arrow navigation, Ctrl+Z/Y/S, Ctrl+W, F5 Play,
  Shift+F5 Play-from-map, and accessible labels/tooltips for every non-text control. Pointer-only
  authoring is not acceptable.
- No additional dependency is authorized. Reuse Avalonia, existing toolkit, Core catalogs, BCL, and
  current canvas patterns.

### 7.2 Ordered Creator packages

1. **17A — Project lifecycle and shared infrastructure (`PLANNED`; prerequisite Phase 16 launch
   contract).**
   - **Spec lock:** complete `CREATOR_APP_SPEC` session ownership, lifecycle state machine, recent/
     recovery formats, save transaction, document close, usage/safe-delete, validation navigation,
     reference picker, undo transaction/grouping, and virtualization defaults.
   - Implement New/Open/Recent/Save/Save All/Close with unsaved guard choices Save/Discard/Cancel.
     Save validates models and serializes every dirty file into a staging directory first. A journal
     then backs up originals and replaces files in canonical relative-path order; any failure rolls
     replaced files back, and startup completes rollback from an unfinished journal. Only a successful
     transaction updates saved undo positions. Prevent a second
     process from writing the same project using a lock file containing PID/start marker; stale locks
     may be removed after process absence confirmation.
   - Build shared searchable reference picker, usage results grouped by entity/field, safe delete,
     validation issue navigation to document+field, undo grouping, recovery manager, and virtualized
     entity navigation. **Acceptance:** complete lifecycle/dirty/failure matrix, atomic partial-write
     prevention, lock/recovery/recent tests, reference replace/delete, navigation, 100-step undo,
     keyboard shortcuts, and headless ViewModel coverage. Exit: fixture projects can be managed with
     no lost edits or direct filesystem mutations from Views.
2. **17B — Asset authoring (`PLANNED`; prerequisite 17A).**
   - **Spec lock:** complete `ASSET_PIPELINE_SPEC` import/reimport transaction, asset ID/path/hash,
     connected-component algorithm, canvas coordinate/selection, slice acceptance/naming, animation,
     audio metadata, orphan handling, and atlas diagnostics.
   - Import copies supported PNG and approved audio formats into canonical project asset folders only
     after decode/validation, computes SHA-256, and resolves name collisions by prompting replace or
     new slug. Reimport preserves asset ID and authored slice metadata, reports invalidated rectangles,
     and commits only on confirmation. Source is never modified.
   - Implement asset browser filters/preview/usage, manual/common-size/gutter/connected-component
     suggestions, zoom/pan/grid/rect editing, include/exclude, batch `{n}` naming, animation grouping
     and fixed-tick preview, audio kind/loop/volume metadata, atlas placement preview, missing/changed/
     orphan validation. Audio audition launches the real Runtime's preview mode out of process after
     16E; Creator does not add a second decoder/player. Connected components use 4-neighbor opaque flood-fill, discard fully
     transparent and one-pixel noise components by default, merge bounds that overlap or are within
     the authored merge threshold (default 2 px), leave snap disabled by default, and sort top-to-
     bottom then left-to-right.
   - **Acceptance:** all slicer fixtures and boundaries, transparent/large/malformed images, import/
     reimport rollback, collision naming, animation order, audio missing/invalid, atlas overflow,
     usage/orphan validation, undo/redo, keyboard canvas operations, and performance at 4096² image.
3. **17C — World authoring (`PLANNED`; prerequisites 17A/B).**
   - **Spec lock:** complete `MAP_EDITOR_SPEC` canvas/chunks, tilesets/objects, all visual/overlay
     layers, tool pointer semantics, stroke undo, entity schemas/forms, warp/path/trigger validation,
     selection, resize, clipboard, and play-from-map arguments.
   - Implement tileset editor for tile rect/animation/solid/encounter/ledge flags and object footprints;
     map canvas with 32×32-tile visual chunks, zoom 25–800%, pan, layer visibility/lock, palette,
     paint/rect/bucket/eyedropper/erase, collision override, encounter and trigger overlays. One pointer
     down-drag-up stroke is one undo command; repeated cells store original once and final once.
   - Implement select/move/configure for player start, NPC static/wander/patrol, warp, trainer, pickup,
     sign, shop/PC/center, fixed encounter, and trigger. Paths are 4-connected tiles and validate
     collision; warp picker chooses map+tile; overlays show broken refs/unreachable start/warp warnings.
   - **Acceptance:** coordinate/zoom/chunk edges, every tool/layer and undo, resize preservation,
     entity placement/move/delete/config, path/warp validation, layer lock, large map 256×256 budget,
     save/reopen equality, and play-from-map exact process arguments.
4. **17D — Structured data authoring (`PLANNED`; prerequisites 17A and frozen Core schema).**
   - **Spec lock:** inventory every serialized entity/category/field and assign editor control,
     reference source, null/default behavior, validation display, create/duplicate/delete, and help.
     The generated coverage registry must fail when a schema field lacks an authoring disposition.
   - Complete editors for settings/start/save config, types, species/base stats/forms/learnsets/
     evolutions, moves, items/held effects, abilities/hooks, rulesets, conditions, trainers/AI/party/
     inventory, encounters, pockets, storage, shops, dialogue/flags/events, maps/tilesets/assets, and
     export config. Use structured collection rows, searchable references, numeric bounds, enum
     dropdowns, optional-field enable toggles, and raw JSON only as a read-only diagnostics view.
   - **Acceptance:** schema-to-editor coverage 100%, create/edit/duplicate/delete/save/reopen for every
     category, invalid/null/default/broken-ref matrices, undo/dirty consistency, and no writable raw
     JSON control.
5. **17E — Catalog-driven mechanics editor (`PLANNED`; prerequisite 17D and Phase 15 catalogs).**
   - **Spec lock:** publish machine-readable catalog descriptors for operation/query/condition/hook,
     params, types, required/default, range, enum, compatibility, target/scope/timing, topology,
     ruleset, help key, and deprecation. Core owns descriptors; Creator supplies generic controls.
   - Build searchable add menu grouped by family, parameter form generation, incompatible-field
     suppression, reorder/duplicate/remove, nested typed payload editing only where catalog permits,
     immediate Core validation, and normalized preview/trace explanation. Unknown catalog entries
     render as blocking unsupported data without losing their serialized payload.
   - **Acceptance:** descriptor completeness against compiler catalogs, every param type/default/range,
     incompatible combinations, unknown/deprecated preservation, reorder/undo, topology/ruleset help,
     and author/save/compile/resolve a representative entry from every mechanic family.
6. **17F — Playtest, sandbox, and export workflows (`PLANNED`; prerequisites 17B-E and Phase 16).**
   - **Spec lock:** process command lines, validation gate, temporary snapshot ownership, debug party/
     spawn/battle request formats, concurrent-process policy, log capture, cancel/terminate, crash
     artifacts, and export invocation/result contract.
   - F5 saves after confirmation then launches real Runtime `--project`; play-from-map adds validated
     map/tile spawn; battle sandbox creates a temporary neutral playtest request referencing project
     data and launches Runtime—never simulates in Creator. One playtest per project; launching another
     focuses or asks to terminate. Capture stdout/stderr/exit code and link diagnostics without
     blocking UI. Export calls the production Tools/API path and shows validation/warnings/progress/
     written files/smoke result.
   - **Acceptance:** error/warning/cancel/save choices, command quoting/space paths, process start/
     exit/crash/terminate, temp cleanup, map and doubles sandbox arguments, Creator crash isolation,
     and export result display.
7. **17G — Creator verification and phase gate (`PLANNED`; prerequisites 17A-F).**
   - Run headless ViewModel matrices for every editor, undo/redo/dirty/save/recovery, Core validation,
     keyboard focus, and malformed/newer project refusal. Automated UI smoke traverses New→author→
     validate→playtest→save→reopen on Windows.
   - Budgets: open 10,000-entity project ≤5 s warm/≤10 s cold; filter/navigation response ≤100 ms p95;
     validation ≤2 s complete and ≤500 ms debounced for ordinary edit; 256×256 map pan/zoom ≥30 FPS;
     autosave UI pause ≤100 ms; managed memory ≤1.5 GB stress fixture with no monotonic growth across
     50 open/close cycles.
   - Perform keyboard-only and screen-reader-name audit, 125/150/200% scaling, high contrast, and one
     no-JSON authoring trial creating the exact two-map acceptance project. Record every manual step
     and defect. **Exit:** project is authored and launched without source JSON edits, data loss,
     inaccessible required controls, or FIX-NOW findings.

Phase 17 excludes in-process simulation, alternate schemas/validation, production demo content,
installer/update work, plugin/scripting APIs, and Core/Runtime rule fixes hidden in Creator.

Phase 17 GO requires all:

- [ ] 17A-17G are VERIFIED and all schema fields/catalog descriptors have authoring dispositions.
- [ ] Lifecycle/save/recovery/undo tests prove no partial or silent source mutation.
- [ ] Asset and map workflows pass algorithm, rollback, undo, validation, and stress budgets.
- [ ] Every required entity/mechanic is structured-authorable; no writable raw JSON escape hatch.
- [ ] Play/play-from-map/battle sandbox/export invoke real external processes and report failures.
- [ ] Keyboard/accessibility/scaling checks and the two-map no-JSON trial pass.
- [ ] Creator performance budgets and focused review are GO.

## 8. Phase 18 — Integrated Vertical Slice and Production Export

Goal: prove the Runtime and Creator as one reusable product.

Prerequisite: Phases 15-17 are GO. Phase 18 integrates and proves existing contracts; mechanic/editor/
runtime gaps found here return to their owning phase rather than receiving demo-only workarounds.

### 8.1 Locked integration/export defaults

- All demo names, writing, art, sprites, icons, maps, music, and SFX are original or clearly licensed
  for redistribution with attribution recorded. The local mechanics corpus is absent from project,
  pack, export, screenshots, docs, and executable strings except sanitized numeric test metadata.
- Demo minimum is 10 original species, 30 original moves spanning every Phase 15 mechanic family at
  least once where practical, 3 connected-by-warps maps, 5 ordinary trainers plus one gym leader,
  one wild and one trainer doubles battle, center, mart, PC storage, 3 encounter tables, 10 items,
  one level evolution, one alternate-condition evolution, day/night difference, music and SFX.
- Acceptance route is 20–40 minutes for a first-time player: New Game → first capture → trainer →
  center/mart/storage → evolution opportunity → doubles encounter → gym badge → save/quit/continue.
- Export target is self-contained `win-x64` folder, not single-file, containing renamed exe, required
  runtime files, `config.json`, `game.cgmpack`, licenses/notices, and no SDK/tool requirement. Debug and
  release templates are version-matched and built by CI from the same commit.
- 1.0 remains unsigned self-contained zip unless the user later supplies a signing certificate; the
  product documents Windows warning behavior. No installer or updater is built in Phase 18.

### 8.2 Ordered integration packages

1. **18A — Original content and acceptance design (`PLANNED`; prerequisite Phases 15-17 GO).** Write
   a content brief with IDs, map graph, progression flags, trainer/encounter tables, species/move/item/
   ability/evolution roster, asset/audio list and licenses, dialogue outline, difficulty targets, and
   exact acceptance route checkpoints. Map every selected move to certified mechanics and every UI/
   Runtime/Creator feature to at least one route step. **Acceptance:** reference scan clean, all refs
   planned, no new mechanic/editor/runtime requirement, estimated route within target, review GO.
2. **18B — Creator-only authoring (`PLANNED`; prerequisite 18A).** Create the project exclusively via
   released Creator controls while recording session steps and defects. Raw JSON may be inspected
   read-only but not edited; test-fixture maintenance tools are not allowed for demo authoring. Import
   licensed assets, slice/animate, build maps/data, validate continuously, and playtest each route
   segment. Any blocked field returns to Phase 17; any rule/presentation defect returns to 15/16.
   **Acceptance:** version-control diff contains only Creator-normal output/assets; zero validation
   errors; every content ID used; route completes in raw mode; authoring defect log closed.
3. **18C — Production pack and CI templates (`PLANNED`; prerequisites 18B and export spec lock).**
   Extend pack sections to deterministic data, atlas RGBA, audio bytes, and asset metadata using the
   existing versioned index and approved codecs; hash uncompressed payloads in section order. Build
   self-contained debug/release `win-x64` Runtime templates in CI, attach runtime version manifest,
   licenses, and checksums, and test template/pack mismatch refusal. **Acceptance:** repeat build has
   identical content payload hashes, every asset reference resolves, raw/pack state parity, tamper/
   missing section failures, CI artifact installs without SDK, and no reference-corpus payload.
4. **18D — Production export workflow (`PLANNED`; prerequisite 18C).** Complete `EXPORT_PIPELINE_SPEC`
   with output naming, safe overwrite, executable metadata/icon strategy, validation override policy,
   template selection/version, transactional staging, smoke exit codes, cleanup, and completeness.
   Export to sibling temporary directory, run pack verification/smoke, then atomically rename to the
   chosen empty/nonexistent destination. Never merge into a nonempty folder; replacement moves the
   old folder to backup until success. Executable filename is sanitized game name; icon patch is
   optional and skipped with warning when no approved built-in method exists—no dependency is added.
   **Acceptance:** paths/spaces/unsafe names, existing/locked destination, rollback, debug/release,
   version/icon/no-icon, every smoke code, completeness manifest, Creator display, and zip creation.
5. **18E — End-to-end and clean-machine proof (`PLANNED`; prerequisite 18D).** Commit a deterministic
   input replay from New Game to badge plus checkpoint state assertions and screenshot/event hashes.
   Run release export on a clean supported Windows VM with no .NET SDK/runtime, developer tools, or
   project source; play route, save, close, relaunch, continue, finish, and verify AppData save path.
   Conduct at least three authoring and three player sessions with people who did not implement the
   feature; record task completion, assistance, time, crashes, data loss, and prioritized defects.
   **Acceptance:** replay twice identical, clean VM success, no missing licenses/files, all users
   complete core route, zero data-loss/crash/FIX-NOW defect.
6. **18F — Integration closeout (`PLANNED`; prerequisite 18E).** Route every defect to its owning
   phase and remove all demo-specific production branches. Add the demo project as the standing
   validation/export/smoke/replay regression using only redistributable original content. Record
   startup/frame/memory/export-size/time budgets, known accepted limitations, asset licenses, and
   raw/pack/export hashes. Run full build/tests, security/IP scan, package completeness, and focused
   review. **Exit:** a new user creates/exports a small original game and a clean machine runs it
   without SDK/tools; all Phase 18 gates are evidenced.

Phase 18 excludes installer/update infrastructure, store publishing, signing procurement, plugins,
localization, multiplayer, and demo-specific production branches.

Phase 18 GO requires all:

- [ ] Original/licensed content inventory and IP scan are clean.
- [ ] Demo was authored through Creator with zero validation errors and no manual JSON repair.
- [ ] Raw/pack/export parity, asset hashes, CI templates, transactional export, and all smoke codes pass.
- [ ] New-game-to-badge replay is deterministic and save/relaunch/continue succeeds.
- [ ] Release export runs on a clean supported VM without SDK/runtime/developer tools.
- [ ] Author/player trials have zero open data-loss/crash/FIX-NOW defects.
- [ ] No demo-specific production branch or reference-corpus content exists; focused review is GO.

## 9. Phase 19 — Release Hardening and 1.0

Goal: ship a stable Creator and versioned Runtime template.

Prerequisite: Phase 18 passes its clean-machine and stranger gates with no data-loss defects.

### 9.1 Locked 1.0 policy

- Versioning is SemVer for Creator/Runtime templates. Project `schemaVersion`, save format, and pack
  format remain independent monotonic integers. 1.0 reads every format released during public beta,
  migrates older supported data through every intermediate version, and safely refuses newer data.
- Project migrations create a timestamped full project backup before first write. Save migration keeps
  original plus `.bak`. Pack formats are never migrated in place; rebuild from project source.
- Distribution is a self-contained `win-x64` Creator zip plus version-matched Runtime template files.
  No installer, automatic updater, telemetry, account, cloud service, or network requirement at 1.0.
  Artifacts are unsigned unless the user supplies a certificate; SHA-256 checksums and warning docs
  are mandatory. This resolves the old “decide distribution” blocker.
- Support floor is 64-bit Windows 10/11 with OpenGL 3.3-capable hardware. Other OS/architectures are
  not tested or advertised. User projects/assets remain local; crash reports are saved locally and
  shared only by explicit user action.
- Release blockers are any data loss/corruption, migration failure, security/path escape, deterministic
  replay break, export/clean-machine failure, critical accessibility blocker, crash in tutorial route,
  missing license, or unresolved FIX-NOW review finding.

### 9.2 Ordered release packages

1. **19A — Compatibility and migration freeze (`PLANNED`; prerequisite Phase 18 GO).** Inventory every
   committed project schema, save format, pack format, runtime config, recovery, and catalog version.
   Write a compatibility matrix stating read/migrate/refuse/rebuild for each producer→1.0 consumer.
   Add fixtures from each version, chain migrations on copies, validate after each step, preserve
   unknown tolerated fields where contract requires, and test interruption/rollback/disk-full behavior.
   Publish the 1.x policy: additive compatible fields may be minor releases; required/removal/semantic
   changes require migration and appropriate SemVer. **Acceptance:** every matrix cell tested, byte-
   stable latest output, backup restore, newer refusal, no silent pack migration, review GO.
2. **19B — Reliability, security, and performance hardening (`PLANNED`; prerequisite 19A).** Fuzz JSON,
   pack headers/index/compression/hashes, saves, asset dimensions, paths, and CLI args with bounded
   generated inputs; assert controlled error, no path escape, no overwrite outside selected roots,
   no unbounded allocation/loop, and reproducible seed artifact. Canonicalize then verify every import,
   project, export, backup, and zip path remains under its root; reject absolute/rooted/`..` escapes and
   symlink/reparse traversal at write time. Redact user paths/content from shareable diagnostics unless
   explicitly included. Stress 10k entities, 256×256 maps, 4096² sheets, maximum catalog effects, 100
   save/export cycles, and 8-hour replay soak. **Acceptance:** zero crash/hang/corruption/path escape,
   Phase 16/17 budgets met or explicitly tightened, deterministic minimized failure artifacts.
3. **19C — Release CI and distribution (`PLANNED`; prerequisite 19B).** Build clean tagged Release
   artifacts from locked SDK/dependencies; run tests, demo validation/export/smoke/replay, license scan,
   checksum, and clean Windows install script before publishing. Zip layout has one Creator folder,
   bundled templates, docs/licenses, no caches/source/reference corpus. Generate SHA-256SUMS and a
   machine-readable release manifest with version/commit/runtime/schema/save/pack compatibility.
   Document unsigned SmartScreen flow. If a certificate later exists, signing is a separately approved
   additive step; its absence does not block the locked unsigned release. **Acceptance:** reproduce
   artifacts twice with identical payload manifests (timestamps may differ only where documented),
   clean-machine launch/export, offline operation, uninstall by folder deletion without user-save loss.
4. **19D — Complete documentation and legal/IP review (`PLANNED`; can run after 19A).** Write a
   tutorial that produces/exports a two-map game; user manual for lifecycle/assets/maps/data/mechanics/
   playtest/export; generated operation/query/condition/event reference from catalogs; Runtime controls/
   saves/debug; migration/recovery; troubleshooting/error codes; licensing guide and original sample
   license. Validate every command/path/screenshot against the release candidate. Inventory every
   dependency and bundled asset with license/notice/source; scan names/assets/content for reference-
   corpus leakage. **Acceptance:** fresh-user tutorial completion, link/command checks, catalog reference
   completeness, license approval, zero official-content leakage.
5. **19E — External beta and release candidate (`PLANNED`; prerequisites 19A-D).** Recruit at least 10
   participants: at least five primarily authors and five primarily players, spanning Windows 10/11,
   integrated/discrete graphics, keyboard/gamepad, and one screen-reader/high-scaling workflow. Use
   issue template with version, steps, expected/actual, logs, severity, and data-loss flag. Rehearse one
   project/save migration from each beta version. Freeze RC for seven days and run nightly demo replay,
   export smoke, migration, and 8-hour soak. **Acceptance:** tutorial/task completion ≥80% without live
   intervention, zero open data-loss/security/FIX-NOW/tutorial-crash issues, all others dispositioned,
   accessibility required flow complete, RC soak green.
6. **19F — 1.0 release and rollback (`PLANNED`; prerequisite 19E).** Update version/changelog and
   compatibility docs; tag immutable commit; run release CI; publish Creator zip, templates, checksums,
   manifest, docs, source/license notices; archive exact artifacts. Verify download hashes and clean
   install/export once. Rollback means unpublish/mark affected artifact, restore prior known-good links,
   publish advisory, never downgrade user projects/saves, and issue a forward-fix version. Support
   policy: retain 1.0 docs/artifacts, accept reproducible data-loss/security defects as priority, and
   preserve format migration in later releases. **Exit:** a stranger downloads Creator, completes the
   tutorial, exports a two-map game, and runs it on a clean supported machine without assistance.

No 1.0 gate is satisfied by unit tests alone. Installer/updater, signing purchase, telemetry, cloud,
accounts, localization, plugins, networking, stores, and non-Windows support remain outside 1.0.

Phase 19/1.0 GO requires all:

- [ ] Every supported project/save/config/catalog version migrates or refuses exactly per matrix.
- [ ] Seeded fuzz, path traversal, stress, eight-hour soak, backup/recovery, and performance gates pass.
- [ ] Offline unsigned `win-x64` zip, templates, manifest, checksums, licenses, and clean-install proof exist.
- [ ] Tutorial/manual/generated mechanic reference/migration/troubleshooting/legal docs are verified.
- [ ] Beta/RC cohort, accessibility/hardware coverage, migration rehearsal, and seven-day soak pass.
- [ ] Zero release blocker or FIX-NOW issue remains; all other issues have explicit disposition.
- [ ] Tagged artifacts/hashes and rollback/support policy are archived and final stranger test passes.

## 10. Immediate next queue

Always take the first incomplete eligible item. Implement `SPEC READY`; for `PLANNED — SPEC LOCK
AUTHORIZED`, first reconcile its locked defaults into the owning spec, then implement. Never combine
items across a numbered gate merely to keep a model busy:

1. **COMPLETE — 15B-2 (`aded927`).** Doubles construction, slot-authoritative APIs, action admission,
   collective conflicts, execution phases, actor snapshots/invalidation, and slot-aware events;
   evidence is recorded in the 15B progress log.
2. **COMPLETE — 15B-3 (`ea5a32f`).** Typed selections and all live target scopes, including target
   invalidation/fallback, PP/event boundary, and exact random-candidate draw counts; evidence is
   recorded in the 15B progress log.
3. **COMPLETE — 15B-4.** Action/target contexts, spread resolution, deterministic trace, family
   goldens, and closeout review are recorded in the 15B progress log.
4. **COMPLETE.** The generated target/topology catalog certifies 57 entries and writes manifest
   statuses/test IDs through tooling.
5. **COMPLETE — 15B-5, 15B-6, and 15B exit.** Redirection/position, outcome/replacement, the
   cumulative golden, remaining target-only certification, and focused exit review are GO.
6. **COMPLETE — 15C-1/2/3/4/5/6/7, 15D-1/2/3/4/5/6/7, 15E-1/2/3/4/5/6/7, 15F-1/2/3/4/5/6/7, 15G-1, and 15G-2. ACTIVE — 15G-3.** Follow this remaining topological package order; each ID means spec lock → implementation →
   affected normalization/conformance → focused review → commit before the next ID:
   **15F-4** through **15F-7**; **15G-1**; then **15G-3** through **15G-6**. This order resolves every declared
   cross-workstream prerequisite; do not substitute the alphabetical workstream order.
7. Run 15H-1 through 15H-3 continuously alongside each completed capability package: enrich blocked
   references, regenerate normalized definitions, route newly exposed capabilities, and certify every
   now-complete cohort. Run 15H-4 only after 15B-15G and require zero gaps.
8. Complete **15I-1** through **15I-5** in order after 15H-4, including catalog completeness and
   seeded singles/doubles measurements.
9. Run 15J only after primitive semantics and normalization stabilize. Do not begin Phase 16 until
   every Phase 15 exit checkbox is generated/evidenced and the focused review verdict is GO.

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
> work is Phase 15. Take the first incomplete eligible feature package from IMPLEMENTATION_PLAN
> section 10 and complete the whole reusable behavior family. Implement `SPEC READY`; for `PLANNED —
> SPEC LOCK AUTHORIZED`, complete its named specification package and readiness evidence before implementation;
> a capability description alone is not permission to invent execution rules. Do not implement a
> named move, move-ID branch,
> one-off handler, arbitrary script op, UI, or future-phase feature. Use the promotion ladder in
> section 5.7; update the battle spec before code; add strict validation, typed compilation, normal
> resolver behavior, events/traces, cleanup, AI visibility, and every applicable test from
> TESTING_STRATEGY. Per-move certification requires normalized definitions and conformance vectors;
> never hand-edit counts or treat representative tests as certification. Run the focused tests,
> `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx`, and
> `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build`. Fix change-caused failures, update
> the package progress record and immediate queue, review for scope/spec/determinism/schema drift,
> commit the complete change, and stop only if complete or genuinely blocked. Do not advance to
> Phase 16 until every Phase 15 exit gate is evidenced and the focused review is GO. Missing spec
> prose is not a blocker when v4 supplies locked defaults; reconcile the spec and proceed. Escalate
> only the reserved decisions in v4 §2.1. A package may span multiple model turns. Do not stop merely
> because the refactor is substantial or cannot finish in one response: implement a normal-path green
> checkpoint, mark the package IN PROGRESS if a context boundary forces handoff, and resume the same
> session/package until complete. A zero-change response without a demonstrated blocker is not a
> valid iteration. Never start the next package while one is IN PROGRESS.

The implementing model should report: package outcome, reusable behavior added, audit groups/reference
keys affected, files/specs changed, tests and counts, RNG/events/trace implications, manifest status
delta, remaining first eligible package, blockers/deviations, and commit hash.
