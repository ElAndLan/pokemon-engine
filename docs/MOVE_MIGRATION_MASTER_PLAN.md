# Move Migration Master Plan

Status: **Binding for all move-engine, move-normalization, move-conformance, and move-migration
work.** Read this document before `BATTLE_SYSTEM_SPEC.md`, `EFFECT_TYPES_CATALOG_v0_5.md`, and
`MOVE_AUDIT_SYSTEM_PLAN.md` whenever a task can change or certify move data or behavior.

## 1. Authority And Purpose

This document defines how Creature Game Maker converts the locked local move-reference corpus into
generic engine mechanics and proves that every reference entry behaves correctly. It governs batch
selection, normalization, engine capability work, validation, compilation, resolution, evidence,
certification, storage, reporting, and final closure.

Authority order remains:

1. `AGENTS.md` and `SCOPE_GUARD.md` define binding repository and phase law.
2. `IMPLEMENTATION_PLAN.md` defines the active package order and phase gates.
3. This document defines the mandatory end-to-end move-migration workflow.
4. `BATTLE_SYSTEM_SPEC.md` and `EFFECT_TYPES_CATALOG_v0_5.md` define exact mechanics.
5. `MOVE_AUDIT_SYSTEM_PLAN.md` routes reference rows to reusable mechanic families.
6. `TESTING_STRATEGY.md` defines required proof.

If these documents conflict, stop and reconcile them explicitly. This plan does not authorize work
outside the active package or allow certification to advance ahead of its owning engine capability.

## 2. Non-Negotiable Outcome

Every one of the 937 locked reference entries must complete this evidence chain:

```text
inventoried
  -> mechanically understood
  -> normalized
  -> strictly validated
  -> compiled into typed effects
  -> resolved in every required context
  -> event/trace verified
  -> certified
```

The migration closes only at:

```text
937 certified
0 inventoryOnly
0 blockedReference
0 blockedEngine
0 invalid
0 unsupported or silently disabled
0 move-name or move-ID behavior branches in Core
```

Only the generated conformance manifest is count authority. Capability, a representative unit test,
successful compilation, or a legacy audit PASS does not certify a move.

## 3. Scope And Content Boundary

The local PokeAPI corpus is immutable design-time mechanics evidence. It is not project content and
must never become a runtime dependency. Never edit its files, change their hashes, or copy official
names, descriptions, art, audio, cries, maps, or raw JSON into samples, packs, exports, fixtures, or
original demo content.

Keep these two outputs separate:

### 3.1 Reference Conformance Definitions

Reference definitions prove engine expressibility and behavior. They use neutral keys and sanitized
type references, retain hashes and evidence metadata in the design-time envelope, and contain only
canonical mechanics in the normalized definition.

### 3.2 Project-Owned Move Entities

Actual games author original moves as individual `data/move/<slug>.json` files with stable local IDs,
display names, descriptions, type references, and mechanics. Export combines those entities into a
`.cgmpack`; Runtime loads them into `GameDb`.

Reference certification proves that the engine can express the mechanics. It does not create or
authorize an official-content move pack.

## 4. Mandatory Contract Gate M0

Do not begin bulk project move materialization until the authored `Move` contract is field-for-field
consistent across code, schema, Creator, serialization, migration, and tests.

### 4.1 Canonical Authored Shape

The intended project-owned boundary is:

```text
Move
  schemaVersion
  id                    immutable local move:* EntityId
  name                  player-facing project-authored display name
  description           player-facing project-authored mechanics description
  type                  local type:* EntityId
  damageClass           physical | special | status
  power                 nullable integer
  accuracy              nullable integer; null skips ordinary accuracy
  pp                    positive integer maximum PP
  priority              integer in the supported range
  critStage             integer critical stage
  makesContact          explicit boolean
  target                closed MoveTarget value
  effects[]             ordered { op, chance?, params? } entries
```

`description` is presentation data. It must never drive battle behavior, replace executable effects,
or enter the normalized mechanics hash. The current schema/model does not yet contain this field;
adding it requires the schema-change workflow, a schema-version bump, migration/default policy,
round-trip tests, Creator editing, and Runtime display consumption before it can be claimed supported.

### 4.2 Serialization Vocabulary

Before bulk generation, reconcile the documented kebab-case target vocabulary with the current
camelCase enum serializer. Lock one canonical on-disk representation and test every target value.
Generated data must never depend on a writer/reader spelling mismatch.

### 4.3 Nulls, Defaults, And Ordering

Lock and test these rules:

- Status moves and replacement-formula moves may omit or explicitly store null `power` as specified.
- Null `accuracy` means the ordinary accuracy check is skipped.
- Omitted effect `chance` means 100 percent.
- Generated definitions carry an explicit `makesContact` decision.
- Priority and critical stage defaults are explicit and deterministic.
- Effects execute in authored array order.
- Presets expand before normalized hashing.
- Unknown ops, parameters, enum values, and incompatible combinations are rejected.

### 4.4 Gate M0 Exit

M0 is green only when:

- `Move.cs` and `DATA_SCHEMA.md` agree exactly.
- Old and current move JSON migrate and round-trip correctly.
- Canonical target spellings round-trip for all 16 targets.
- Name and description behavior is locked and tested.
- One canonical damaging, status, formula-power, and multi-target example passes load/save/compile.
- The normalizer emits deterministic canonical mechanics.

## 5. Source Evidence And Manifest Discipline

For every reference entry:

1. Preserve the source bytes.
2. Verify the neutral reference key, source hash, and payload hash.
3. Read structured mechanics fields before prose.
4. Record which evidence proves each enriched or additional effect.
5. Route insufficient evidence to `blockedReference`.
6. Never infer behavior from a move name.
7. Never substitute a plausible default for missing chance, duration, target, PP, or formula data.

Acquisition metadata, URLs, localization arrays, contest data, learnsets, machines, and prose are not
runtime move fields. Prose may support approved research, but prose itself is never executable data.

## 6. Classification Before Conversion

Every remaining entry must be classified before it enters a certification cohort. Record:

- primary mechanic family;
- every secondary mechanic family;
- required topology and target scope;
- required ruleset profile;
- required reusable engine capabilities;
- source-evidence quality;
- complexity tier;
- current blocker and owning package, if any;
- required conformance test IDs.

A move may belong to several families. It cannot be certified until every declared family and
interaction is green.

## 7. Work By Reusable Family, Review By Cohort

Implementation packages are reusable mechanic families, never arbitrary groups of named moves.
Numerical source order does not determine engine work order. `IMPLEMENTATION_PLAN.md` selects the
next eligible family based on dependencies.

Within a green or active family, use bounded cohorts to keep normalization and review legible:

| Cohort type | Typical size | Use when |
|---|---:|---|
| Complex | 3-5 | Multi-turn state, move references, transform/substitute, redirection, compound conditions, or several ordered RNG draws |
| Default | 10 | Shared mechanic family with comparable topology and test contexts |
| Simple | 15-20 | Proven primitive; rows differ only by ordinary data such as numbers, type, chance, or target |

These sizes control review volume only. They are not permission to split an incomplete reusable
package, certify only its easiest rows, or implement ten unrelated mechanics together.

If one entry exposes a missing behavior, find every entry requiring the same generic behavior, route
them to the owning package, implement the complete reusable primitive, and then certify eligible
cohorts. Never create a one-move implementation slice.

## 8. Mechanic-Family Dependency Order

Follow the active package order in `IMPLEMENTATION_PLAN.md`. Subject to that authority, families
normally progress from foundations to consumers:

1. target selection and topology;
2. ordinary damage and the query pipeline;
3. accuracy, critical hits, priority, and effectiveness;
4. HP/status, speed/metric, action-history, party/resource, and random power formulas;
5. persistent statuses, volatile conditions, and stat stages;
6. multi-hit, drain, recoil, healing, HP costs, and fractional HP;
7. weather, terrain, field, and side conditions;
8. protect, redirection, contact reactions, and turn-order manipulation;
9. queued, delayed, charge, and multi-turn behavior;
10. switching, forced switching, and state transfer;
11. hazards, screens, substitute, and cleanup;
12. move copy/call/replace/force behavior;
13. ability, held-item, type, form, and snapshot interactions;
14. counter/revenge and stored-damage behavior;
15. non-battle and post-battle effects;
16. reference gaps and ruleset conflicts.

Do not certify a consumer before all of its prerequisite families are implemented and tested.

## 9. Reusable Capability Workflow

When a cohort needs missing engine behavior, complete this package before promoting its rows:

1. Lock the owning-spec contract: op/query/condition name, params, timing, scope, targets, ruleset,
   rounding, RNG draws, failure/no-op cases, cleanup, events, and trace.
2. Climb the promotion ladder and reuse the earliest existing data shape, typed effect, helper,
   query, condition, or primitive that works.
3. Add strict validation for required, unknown, mistyped, duplicate, out-of-range, and incompatible
   values.
4. Compile serialized effects into typed records.
5. Resolve through the shared controller/query/condition/mutation path.
6. Emit sufficient events and trace for Runtime, debugging, replay, and conformance consumers.
7. Preserve deterministic RNG ownership and documented draw order.
8. Integrate AI legality/scoring only through shared Core queries; AI must not reimplement rules.
9. Add primitive, interaction, regression, and deterministic tests.
10. Run the package review gate before cohort certification.

No named move, reference key, source ID, or display name may select Core behavior.

## 10. Per-Cohort Conversion Procedure

Every cohort uses the same procedure.

### Step 1: Select

Choose entries with a shared primary family, satisfied prerequisites, compatible required contexts,
and a reviewable combined mechanic matrix.

### Step 2: Verify Evidence

Verify source identity/hashes and every direct or enriched mechanics decision. A mismatch stops the
entry before normalization.

### Step 3: Complete The Mechanics Worksheet

Record type, class, power, accuracy, PP, priority, critical stage, contact, target, ordered effects,
conditions, queries, RNG, failure behavior, cleanup, events, and AI implications.

### Step 4: Route Missing Capabilities

Apply the promotion ladder. Missing exact evidence becomes `blockedReference`; missing generic Core
behavior becomes `blockedEngine`. Never guess or silently no-op.

### Step 5: Normalize

Generate stable canonical mechanics plus the design-time envelope: neutral reference key, hashes,
ruleset, topology, sorted families, normalized definition hash, status, and test IDs. Do not hand-edit
generated definitions.

### Step 6: Validate And Compile

Run strict project validation and `MoveCompiler`. Prove valid payload acceptance and invalid sibling
rejection. Compilation proves typed expressibility only.

### Step 7: Resolve Required Contexts

Exercise all applicable contexts: singles/doubles, selected/spread/ally/side/field scopes, immunity,
miss, block, invalidated/fainted targets, source faint, HP/status/stage boundaries, switch, cleanup,
and ruleset variants.

### Step 8: Verify Determinism And Observability

Assert RNG draw count/order/bounds/skips, effect and target order, query inputs/outputs, state changes,
battle events, effect traces, and faint/cleanup order.

### Step 9: Regenerate

Regenerate definitions, hashes, test IDs, manifest statuses, and counts through tooling. Never edit a
certification count or status by hand.

### Step 10: Review And Close

A certification cohort closes only when every selected entry is certified. If research processing
finds blockers, certified rows may retain their evidence, but report the cohort as processed rather
than fully certified and leave the owning reusable package open where applicable.

## 11. Per-Move Certification Matrix

Every move must satisfy all applicable rows.

### Data And Evidence

- Source and payload hashes match.
- Every required field has approved evidence.
- Contact, target, nulls, defaults, and effect order are explicit.
- Mechanic families, topology, ruleset, and test IDs are complete.

### Validation And Compilation

- Valid definition passes.
- Missing, unknown, mistyped, duplicate, out-of-range, and incompatible data fails clearly.
- Typed compilation preserves target and authored effect order.
- No effect is dropped, guessed, or converted to a silent no-op.

### Resolution

- State mutation, target scope, timing, failure/no-op, query inputs, and cleanup are exact.
- Required singles/doubles and ruleset contexts pass.
- Interactions with immunity, protect, status, faint, switch, conditions, overlays, and memory pass.

### Determinism And Events

- All randomness uses injected `IRng`.
- Draw order, bounds, results, and skipped draws are asserted.
- Meaningful events and traces are sufficient without presentation code inferring rule outcomes.

### AI And Presentation Boundary

- AI uses shared legality/query surfaces and does not duplicate formulas.
- Player-facing name and description come from project data.
- Description never controls mechanics.
- Reference names/descriptions never enter shipped original content.

### Evidence

- Primitive tests pass.
- Family interaction tests pass.
- Per-reference conformance vector passes.
- Normalized hash and test IDs are generated.
- Manifest status is generated as `certified`.

## 12. Blocker Taxonomy

### `blockedReference`

Exact mechanics cannot be proven from approved evidence: missing chance/duration/formula, ambiguous
ruleset behavior, conflicting evidence, or no usable mechanics source. Do not guess.

### `blockedEngine`

Exact mechanics are known, but a named generic Core capability is missing. State the reusable
requirement, for example `requires:queued_intent.delayed_slot_attack`, never "special move logic."

### `invalid`

Source or normalized data violates the locked contract.

A blocker clears only when approved evidence resolves it or its reusable engine package is complete.
An approximation, default, disabled effect, or move-specific branch does not clear a blocker.

## 13. Test And Verification Policy

Use three proof layers:

1. **Primitive tests** prove formulas, validation, timing, conditions, bounds, and RNG rules.
2. **Family tests** prove shared interactions, topology, hooks, queries, cleanup, events, and trace.
3. **Per-reference tests** prove every normalized entry's complete declared mechanics in required
   contexts.

One representative test may prove a primitive; it cannot certify every consumer. Before package or
cohort closure, run the focused suites required by the owning spec, then:

```powershell
D:\dotnet\dotnet.exe build CreatureGameMaker.slnx
D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build
```

Failures caused by the change must be fixed. Unrelated failures must be reported with exact evidence;
they do not justify false certification.

## 14. Storage And Generated Artifacts

Editable original project moves remain one file per entity:

```text
data/move/<slug>.json
```

Design-time conformance truth remains generated and sanitized under:

```text
docs/move-conformance/manifest.v1.json
docs/move-conformance/definitions.v1.json
docs/move-conformance/*-decisions.v1.json
```

Export combines project entities into `.cgmpack`; Runtime loads the resulting entities into the
in-memory `GameDb` dictionary. Do not introduce a hand-edited master move dex. A combined catalog is
allowed only as a generated audit/export artifact.

## 15. Required Batch Report

Every processed cohort reports:

```text
Cohort:
Primary family:
Reference keys:
Attempted:
Certified:
Blocked reference:
Blocked engine:
Invalid:

Reusable mechanics changed:
Validation/compiler/resolver changes:
Events/traces/AI changes:
Focused tests:
Full build/test:
Manifest before/after:
Remaining work:
Next eligible family/cohort:
```

Use generated counts. Separate normalized, compiled, resolved, tested, and certified proof; never
flatten them into a broad "implemented" claim.

## 16. Execution Cadence

For each dependency-eligible reusable family:

1. Research every affected reference requirement.
2. Lock the complete family contract.
3. Implement the complete reusable engine package.
4. Pass primitive and interaction matrices.
5. Process the first default cohort of approximately ten.
6. Fix family-level gaps exposed by that cohort.
7. Process remaining complex/default/simple cohorts.
8. Run the family closeout review.
9. Regenerate the complete manifest and counts.
10. Advance only to the next package authorized by `IMPLEMENTATION_PLAN.md`.

The first cohort in a family is expected to be slower because it proves the shared engine path.
Later cohorts should become primarily deterministic normalization and conformance work.

## 17. Final Static And Behavioral Audit

Before declaring 937/937:

- verify all source hashes and the locked corpus digest;
- regenerate every normalized definition and manifest row from scratch;
- require 937 certified and zero other statuses;
- scan compiler, resolver, helpers, queries, conditions, events, AI, and tests for move names/IDs;
- verify every op/condition/query has validation, typed compilation, resolution, and tests;
- verify every reference has required behavioral assertions, not only a compilation test;
- verify raw-folder and pack loading produce equivalent `GameDb` content;
- verify no official content entered samples, fixtures, packs, or exports;
- run the full build and test suite;
- perform a final Phase 15 review gate against the owning specs.

## 18. Governing Rule

> Implement mechanics by complete reusable family, review conversions in bounded cohorts, and
> certify every move individually with generated deterministic evidence.

