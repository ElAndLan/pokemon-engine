# Move Audit System Plan

Status: **Active Phase 15 execution companion.** Full move conformance is now the current phase's
primary exit gate per `IMPLEMENTATION_PLAN.md` v3.1. This document groups the audited failures into
reusable Core work; the implementation plan owns sequencing and the final 937/937 criteria.

## Purpose

`MOVE_AUDIT_RESULTS.md` is the exact audit ledger: 937 local move JSON files, one row per move,
with PASS/FAIL status and the reason for every failure. This document is the implementation
handoff that explains how to turn those failures into reusable `Cgm.Core` primitives.

The rule is strict: moves are data. An implementation may add generic effect ops, query
hooks, scoped conditions, target selectors, and battle-state helpers, but it must not add
resolver branches, classes, handlers, helpers, or compiler cases named after or keyed to individual
moves. If one move appears unique, first ask which reusable timing, query, condition, mutation,
state scope, ruleset, or targeting primitive it actually needs. Named presets are data only.

## Inputs

- Exact row audit: `MOVE_AUDIT_RESULTS.md`.
- Local mechanics reference: `docs/pokeapi-results/move/*.json`.
- Battle contract: `docs/BATTLE_SYSTEM_SPEC.md`.
- Effect architecture: `docs/EFFECT_TYPES_CATALOG_v0_5.md`.
- Data/schema contract: `docs/DATA_SCHEMA.md`.

The local PokeAPI files are design-time mechanics reference only. Do not copy official names,
assets, cries, music, maps, or reference JSON into samples, exports, packs, or runtime content.

### Evidence and count authority

The legacy row table currently contains 468 PASS and 469 FAIL entries. Its later 20-group inventory
was generated from an older 505-failure baseline and intentionally contains overlapping historical
memberships; it is a requirements-discovery aid, not a live status report. Do not sum group counts,
use them to change phase status, or delete a requirement merely because its row later became PASS.

Strict progress comes only from `docs/move-conformance/manifest.v1.json`: 937 inventory-only and
0 normalized/compiled/certified at the current baseline. Tool-generated manifest status and test IDs
supersede all legacy PASS/FAIL language. The roadmap owns which feature package runs next.

## Phase 15A Corpus Manifest Contract

`cgm audit-moves <corpus-folder> <manifest.json>` inventories the local reference corpus without
copying move names or source JSON into the generated artifact. The command is deterministic and
fails on malformed wrappers, non-move endpoints, missing/invalid source IDs, duplicate IDs,
duplicate reference keys, or an empty corpus.

Manifest format version 1:

```text
MoveCorpusManifest
  formatVersion            = 1
  corpusDigest             = lowercase SHA-256 over ordered "sourceId:fileHash\n" rows
  fileCount
  statusCounts             = count by conformance status
  entries[]                = ascending sourceId order

MoveCorpusEntry
  referenceKey             = "move-" + zero-padded numeric PokeAPI id; contains no move name
  sourceId                 = positive PokeAPI numeric id
  sourceFileHash           = lowercase SHA-256 of the complete local wrapper file bytes
  payloadContentHash       = wrapper content_hash, validated as lowercase/uppercase hex
  sourceTarget             = mechanics target key from payload.target.name, or "unknown"
  observedMechanicFamilies = conservative tags derived only from structured payload metadata
  requiredTopology         = "unclassified" until Phase 15B certifies target semantics
  requiredRuleset          = "unclassified" until the normalized definition declares it
  status                   = "inventoryOnly" at the Phase 15A baseline
  normalizedDefinitionHash = omitted until normalization exists
  testIds                  = empty until conformance tests are registered
```

The manifest is sanitized mechanics metadata. It must not contain `payload.name`, source filename,
effect prose, flavor text, URLs, official art/audio, or raw JSON. The local corpus remains ignored
and is never a build/runtime dependency.

### Conformance statuses

Statuses are monotonic evidence levels, not optimistic labels:

- `inventoryOnly` — source is hashed and structurally inventoried; no behavior claim.
- `normalized` — a complete generic definition and normalized hash exist.
- `compiled` — strict validation and typed compilation pass.
- `certified` — required-context behavioral assertions and deterministic event/trace evidence pass.
- `blockedReference` — exact mechanics source is insufficient.
- `blockedEngine` — a named reusable Core capability is missing.
- `invalid` — source/normalized data violates the contract.

Only `certified` counts toward 937/937. Phase 15A generates `inventoryOnly` for every entry, so the
strict baseline remains 0/937 even though the legacy expressibility audit reports 468 PASS.

### Structured family observation

Phase 15A may tag only facts directly present in structured PokeAPI fields: standard damage,
ailment, stat stage, drain, recoil, heal, multi-hit, multi-turn, critical, and flinch. Missing tags
do not imply missing behavior because prose-only and unique mechanics require later normalization.
An entry with no structured family receives `unclassified`; it is never treated as a no-op.

## Phase 15H source-field disposition and normalized Move boundary

The local PokeAPI wrappers are immutable evidence, not the game's move format. Never delete fields
from `docs/pokeapi-results/move/*.json`: doing so changes source hashes, invalidates the locked corpus
digest, and destroys the evidence needed to revisit an ambiguous normalization. “Remove” means omit
the field from the canonical normalized definition, project data, packs, fixtures, and Runtime.

### Complete corpus field audit

On 2026-07-13 a deterministic property scan loaded all 937 wrapper files whose ordered source hashes
produce manifest digest `5f4649b3ab84f1ac3c77ec91bfea3f89238d3fb858622ff07d6dadc18b492c5f`.
It inspected every wrapper/payload property and every non-null `payload.meta` object. Counts below are
`present / non-null / non-empty`; scalar zero and false are non-empty because they are real values.

Disposition meanings:

- **direct** — maps to one compact authored `Move` field after ID/enum normalization;
- **expand** — consumed into ordered generic effects, ruleset/profile data, topology, or derived tags,
  then omitted as a source-shaped field;
- **research** — may answer a mechanics ambiguity, but prose/source structure is never copied;
- **manifest** — retained only as sanitized conformance provenance;
- **validate** — checked against another source identity/value, then omitted; and
- **discard** — irrelevant to the engine's move mechanics and never enters normalized output.

#### Wrapper fields

| Source field | Count | Disposition | Destination/reason |
|---|---:|---|---|
| `content_hash` | 937 / 937 / 937 | manifest | `payloadContentHash`; never part of `Move` |
| `endpoint` | 937 / 937 / 937 | validate | Must equal `move` |
| `fetched_at` | 937 / 937 / 937 | discard | Fetch-time provenance has no mechanic meaning |
| `import_batch_id` | 937 / 937 / 937 | discard | Acquisition bookkeeping only |
| `payload` | 937 / 937 / 937 | expand | Source container; children are classified below |
| `resource_id` | 937 / 937 / 937 | validate | Must agree with `payload.id`; numeric key comes from the payload/manifest |
| `resource_name` | 937 / 937 / 937 | validate | Must agree with payload/file identity; never copied to sanitized output |
| `url` | 937 / 937 / 937 | discard | Network/source locator; Runtime and Creator never fetch it |

#### Payload fields

| Source field | Count | Disposition | Destination/reason |
|---|---:|---|---|
| `accuracy` | 937 / 649 / 649 | direct | `Move.Accuracy`; null is the explicit bypass/no-check value |
| `contest_combos` | 937 / 205 / 205 | discard | Contest subsystem is outside the product |
| `contest_effect` | 937 / 354 / 354 | discard | Contest subsystem is outside the product |
| `contest_type` | 937 / 467 / 467 | discard | Contest subsystem is outside the product |
| `damage_class` | 937 / 937 / 937 | direct | `Move.DamageClass`, normalized to the closed enum |
| `effect_chance` | 937 / 218 / 218 | expand | Copied only onto the exact effect/gate it qualifies |
| `effect_changes` | 937 / 937 / 39 | research | Historical prose/reference input; normalized as explicit ruleset effects |
| `effect_entries` | 937 / 937 / 826 | research | Semantics evidence only; prose is never stored or shipped |
| `flavor_text_entries` | 937 / 937 / 914 | discard | Localization/flavor content, not mechanics |
| `generation` | 937 / 937 / 937 | expand | Normalization routing/profile evidence; not an authored move field |
| `id` | 937 / 937 / 937 | manifest | `sourceId` and neutral `referenceKey`; never a project `EntityId` |
| `learned_by_pokemon` | 937 / 937 / 833 | discard | Learnsets belong to authored species definitions |
| `machines` | 937 / 937 / 358 | discard | Machine/item acquisition belongs to content/progression data |
| `meta` | 937 / 827 / 827 | expand | Structured mechanic source; children are classified below |
| `name` | 937 / 937 / 937 | validate | File/source identity only; official names never enter sanitized definitions |
| `names` | 937 / 937 / 937 | discard | Localized display strings; projects author original names |
| `past_values` | 937 / 937 / 168 | expand | Explicit ruleset/profile overrides, then removed from `Move` |
| `power` | 937 / 599 / 599 | direct | `Move.Power`; null means no fixed standard base-power value |
| `pp` | 937 / 919 / 919 | direct | `Move.Pp`; the 18 null rows are normalization gaps, not default zero/one |
| `priority` | 937 / 937 / 937 | direct | `Move.Priority` |
| `stat_changes` | 937 / 937 / 174 | expand | Ordered generic `statStage` effects with explicit chance/target |
| `super_contest_effect` | 937 / 467 / 467 | discard | Contest subsystem is outside the product |
| `target` | 937 / 937 / 937 | direct | Closed `Move.Target`; URLs are discarded |
| `type` | 937 / 937 / 937 | direct | `Move.Type`; source name maps to a local `type:*` ID, URL discarded |

#### `payload.meta` fields

`meta` is absent on 110 entries. Absence means structured hints are unavailable; it does not prove
that a move has no executable effect.

| Source field | Count | Disposition | Destination/reason |
|---|---:|---|---|
| `ailment` | 827 / 827 / 827 | expand | Generic condition/ailment effect; `none` expands to nothing |
| `ailment_chance` | 827 / 827 / 827 | expand | Chance on the exact ailment effect/gate |
| `category` | 827 / 827 / 827 | expand | Routing/derived mechanic tags only; not stored |
| `crit_rate` | 827 / 827 / 827 | direct | `Move.CritStage`; missing meta defaults explicitly to stage 0 |
| `drain` | 827 / 827 / 827 | expand | Positive drain or negative recoil effect, with exact signed percentage |
| `flinch_chance` | 827 / 827 / 827 | expand | Generic flinch effect/gate |
| `healing` | 827 / 827 / 827 | expand | Generic heal effect with exact percentage |
| `max_hits` | 827 / 28 / 28 | expand | Multi-hit effect bound; paired and validated with `min_hits` |
| `max_turns` | 827 / 44 / 44 | expand | Queue/lock/duration data; paired with `min_turns` |
| `min_hits` | 827 / 28 / 28 | expand | Multi-hit effect bound; paired and validated with `max_hits` |
| `min_turns` | 827 / 44 / 44 | expand | Queue/lock/duration data; paired with `max_turns` |
| `stat_chance` | 827 / 827 / 827 | expand | Chance on the exact secondary stat-stage effects |

Reference objects contribute only their normalized `name` key where a mapping above needs it. Every
reference `url`, localized string, effect prose body, version-group wrapper, and source collection
shape is discarded after its information has been converted to generic data. Array order is retained
only where it carries mechanics (`stat_changes`, `past_values`, and the final explicit effect list).

The null-PP gap is exactly `move-10001` through `move-10018`. Each remains unresolved until its
selection/PP behavior is explicitly normalized from approved mechanics evidence; membership in this
numeric range is audit evidence, never permission for a range check in Core.

### Locked compact Move structure

The project/pack move remains the smallest mechanics-complete authored object:

```text
Move
  schemaVersion
  id                    local immutable `move:*` EntityId
  name                  original project-authored display name
  type                  local `type:*` EntityId
  damageClass           physical | special | status
  power                 nullable integer
  accuracy              nullable integer; null = skip the ordinary accuracy check
  pp                    required positive integer
  priority              integer
  critStage             integer
  makesContact          explicit boolean; not present in this PokeAPI payload and must be enriched
  target                closed `MoveTarget`
  effects[]             ordered `{ op, chance?, params? }` from the closed catalogs
```

Hit/turn counts, drain/recoil/healing, ailments, flinch, stat changes, variable formulas, conditions,
queues, move references, ruleset differences, presentation-only markers, and non-/post-battle actions
belong in ordered effects/conditions/policies. They do not justify parallel top-level PokeAPI-shaped
fields. Mechanic-family tags and topology are derived from `target` plus expanded effects. Official
source identity, hashes, ruleset/topology certification requirements, definition hash, status, and
test IDs belong to the design-time envelope below, never to `Move`.

```text
NormalizedMoveRecordV1                 # design-time only; sanitized
  referenceKey                         # neutral numeric key
  sourceFileHash
  payloadContentHash
  requiredRuleset
  requiredTopology
  mechanicFamilies[]                   # sorted derived tags
  mechanics                            # canonical Move fields excluding project id/name/schemaVersion
  normalizedDefinitionHash             # hash of canonical mechanics only
  status
  testIds[]
```

Canonical mechanics use stable property order, explicit nullable values, explicit defaults, closed
enum/op/param/tag vocabularies, and authored effect order. Presets expand before hashing. Source names,
URLs, prose, fetch metadata, contest data, learnsets, and machine data cannot affect the normalized
hash. A required direct field that is missing, an unknown effect, or semantics that structured data
cannot prove produces a reference/engine routing row; generation never guesses or silently no-ops.

### Schema consequence and non-conflicting handoff

This audit locks the boundary but deliberately does not edit `Move.cs`, `DATA_SCHEMA.md`,
`BATTLE_SYSTEM_SPEC.md`, `MoveCompiler`, or the resolver while the active Phase 15 move-engine package
owns those files. The existing top-level `Move` shape already matches the compact boundary. Future
schema changes are justified only by a corpus-required generic effect/condition/policy that cannot be
expressed through the closed effect structure; they follow the schema-change workflow and must not
reintroduce discarded PokeAPI fields.

## Current Support Snapshot

The engine already supports normal damage, type effectiveness, accuracy, crits, priority,
statuses, stat stages including accuracy/evasion, ordered multiple `statStage` effects, drain,
recoil, crash recoil via `recoil` plus `onMiss: true`, healing, multi-hit, fixed damage, OHKO,
crit boost, self destruct, leech seed, spikes, stealth rock, weather, bind, protect, force
switch, counter damage, accuracy bypass, charge turns, rampage locks, ailment, flinch,
damage-stat overrides for offensive/defensive damage queries, the complete 15C-2 HP/status,
15C-3 effective-speed/physical-metric, 15C-4 action-history, and 15C-5 party/resource/stage/item/
random-table formula registries, explicit `noBattleEffect`, explicit
`postBattleReward`,
and the Phase 15 ability/held-item/form hook slice.

Authored `Move.Target` now compiles into `BattleMove`. The shared topology supports one or two active
slots per side, all 16 typed target shapes, live target materialization/fallback, ordered spread
resolution, ally selection and position exchange, redirection, side/field action scopes, and
slot-addressed faint replacement. Party-member and move-reference scopes are typed but their
mechanic-specific execution remains with later Phase 15 packages. Capability is not certification.
The target/topology cohort has sanitized definitions, registered vectors, generated statuses, the
cumulative golden, and a GO exit review. The 15C-1 exact numeric-query foundation, 15C-2 HP/status,
15C-3 speed/metric, 15C-4 action-history, and 15C-5 party/resource formula packages are complete at
84 certifications; later formula packages
own the remaining entries.

## Iteration Protocol For Future Agents

1. Take the first eligible feature package from `IMPLEMENTATION_PLAN.md` section 10; use the group
   lists below to collect every affected requirement, not to select one named move.
2. Re-read `BATTLE_SYSTEM_SPEC.md` and complete the feature-package specification gate before code.
3. Climb the promotion ladder in `IMPLEMENTATION_PLAN.md` section 5.7 and stop at the first existing
   helper/op/condition/query level that can express the family exactly.
4. Implement the complete reusable package: data contract, strict validation, typed compilation,
   normal resolver/queue/query/mutation path, events/traces, cleanup, and AI visibility as applicable.
5. Add every applicable test from `TESTING_STRATEGY.md`'s feature-package matrix. Representative
   tests prove the primitive; per-reference conformance vectors prove the moves.
6. Regenerate normalization hashes, test IDs, and manifest statuses through tooling. Do not hand-edit
   certification counts or promote a row because the engine capability merely exists.
7. Update the package progress record and immediate queue in `IMPLEMENTATION_PLAN.md`.
8. Run:
   - `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx`
   - `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build`

Do not implement doubles UI, battle animation, runtime rendering, import wizard behavior, or
sample official content as part of this work. The reusable Core primitive comes first; UI/editor
surfaces only follow when the owning phase says they are in scope.

## Reusable Primitive Families

These are the only acceptable directions for new engine work:

| Primitive family | What it does | Guardrail |
|---|---|---|
| Target selector | Resolves authored target data into battle targets, sides, party slots, or field scopes. | Build Core topology first; do not build doubles UI in this slice. |
| Ordered effect execution | Executes multiple generic effects in authored order. | Do not turn this into a scripting language. |
| Battle query hooks | Modifies base power, damage stat, move type, category, accuracy, crit, priority, effectiveness, healing, or final damage. | Reuse `DamageCalc`, `BattleHookDispatcher`, and existing effect dispatch shape. |
| Scoped conditions | Represents volatile, side, field, weather, terrain, screen, room, lockout, trap, protect, and timed state. | Use data-defined `ConditionDef` style records, not one class per move. |
| Queued intents | Handles recharge, delayed attacks, future damage, charge variants, first-turn gates, previous-turn gates, target-action gates, and end-turn delayed effects. | One deterministic queue/timer path; no ad-hoc move flags. |
| Mutation helpers | Mutates held items, berries, abilities, types, side conditions, HP, cures, and switch-linked state. | Keep in `Cgm.Core`; Runtime only renders state and submits actions. |
| Move references | Selects and executes referenced moves for copy/call/force/replace effects while preserving PP ownership and determinism. | Use one `executeResolvedMove` path; no separate move callers. |
| Snapshot overlays | Represents substitute HP decoys, transform/copied stats/types/moves, type overlays, stat swaps, and passable shields. | Do not rewrite species or move definitions during battle. |
| Damage memory | Tracks damage by turn, side, source, target, category, and hit count. | Reuse for counter, revenge, stored damage, and hit-count damage. |
| Explicit no-op/post-battle ops | Marks celebration, money/reward, and intentional no battle-state changes. | Supported markers already compile; author data must choose them explicitly. |

## Primitive Family Contracts

This section is the implementation handoff for each primitive family. It explains what the
primitive owns, the smallest shape that should work, and the guardrails that keep the code simple.
Names below are descriptive, not mandatory type names; prefer existing project names when a nearby
type already does the job.

### Target Selector

Target selection turns authored target data into a deterministic set of battle objects: active
creatures, party slots, side scopes, or whole-field scopes. It should run before effect execution
so every effect receives a resolved target set instead of reinterpreting `Move.Target`.

Smallest useful shape:

- A closed target enum or value object matching the authored target vocabulary.
- One Core resolver method that receives battle topology, user side/slot, selected target, and
  previous-move context.
- A `ResolvedTargets` value containing ordered creature targets plus optional side/field scopes.
- A clear unsupported result for selectors that require topology not yet present, such as ally
  positions in singles.

Execution rules:

- Singles may collapse `selected`, `all-opponents`, and `all-other-pokemon` to the opposing active
  creature when that is mechanically honest.
- Side and field effects must resolve to side/field scopes, not fake creature targets.
- Party-slot targets, including fainted-party targets, must stay separate from active battlers.
- Ordering must be stable so multi-target RNG and event logs replay deterministically.

Guardrails:

- Do not build doubles UI, party-selection UI, or Runtime menu changes here.
- Do not put target checks inside individual effect ops once the resolver can answer them.
- Do not add one resolver method per target string; use one switch over the closed selector set.
- Validation must reject unknown target strings and must not silently reinterpret them.

### Ordered Effect Execution

Ordered execution means a move is an ordered list of generic effect ops. The battle resolver walks
the list and applies each op to the already-resolved targets. This is the replacement for "one move
does one special thing" assumptions.

Smallest useful shape:

- Keep `Move.Effects` as the data list and the existing effect dispatcher as the entry point.
- Each effect receives an `EffectContext` with user, move, resolved targets, current hit/damage
  result when relevant, and deterministic RNG.
- Effects emit normal battle events through the existing event stream.

Execution rules:

- Run effects in authored order.
- A chance gate applies only to the effect it owns unless the data explicitly wraps a sub-list.
- Damage-dependent effects, such as drain/recoil, read the damage result from the current move
  context, not by recomputing damage.
- If a target faints mid-list, later target-specific effects skip that target unless the effect is
  explicitly allowed to affect fainted party slots.

Guardrails:

- Do not add loops, variables, arbitrary expressions, or a scripting language.
- Do not add a bespoke "combo" op when two existing ops in order express the behavior.
- Do not make effects know about UI labels or authored move names.
- New ops need exact validation and at least one representative Core test before any audit row
  changes from FAIL to PASS.

### Battle Query Hooks

Query hooks answer questions the battle engine already asks: what is this move's base power, type,
category, accuracy, crit stage, priority, offensive stat, defensive stat, effectiveness, healing,
or final damage modifier? They should extend the current damage and hook machinery instead of
forking damage calculation.

Smallest useful shape:

- A small set of typed query kinds, added only as failing moves require them.
- A query object with source, target, move, base value, current value, and reason/event metadata.
- One dispatcher path that lets active conditions, abilities, held items, and move effects modify
  the query in the documented hook order.
- A result value returned to `DamageCalc` or the caller that originally asked the question.

Execution rules:

- Query hooks are pure modifiers where possible: input query in, modified query out.
- Rounding stays where the owning formula says it belongs; hooks should not invent independent
  rounding unless the spec requires it.
- Multiple modifiers apply in a documented deterministic order.
- Immunity and type-effectiveness changes must be visible to the same damage pipeline as ordinary
  type chart results.

Guardrails:

- Do not duplicate `DamageCalc`.
- Do not store temporary query answers on `BattleCreature` unless they must persist past the query.
- Do not add broad hook points "for later"; add the next exact query kind and tests.
- Do not special-case move IDs in hook dispatch. Data decides which hook exists.

### Scoped Conditions

Scoped conditions represent temporary or persistent battle state with hooks: volatiles, side
conditions, field conditions, weather, terrain, rooms, traps, screens, lockouts, and protect
variants. A condition definition describes behavior; a condition instance stores runtime state such
as owner, duration, counters, and source.

Smallest useful shape:

- `ConditionScope`: creature, side, field, weather, terrain, room, or party slot as needed.
- `ConditionDef`: ID, scope, duration policy, switch-clear behavior, tags, and hook effect lists.
- `ConditionInstance`: definition ID plus runtime owner, remaining duration, counters, and source.
- Shared helpers to add, replace, remove-by-tag, tick duration, and dispatch hooks.

Execution rules:

- Conditions tick in one documented end-turn order.
- Switch-clear behavior is data, not hidden in the condition name.
- Tags drive cleanup and interaction, such as `hazard`, `screen`, `trap`, `protect`, or `terrain`.
- A condition may hold small counters only when the mechanic requires memory across turns.

Guardrails:

- Do not create one C# class per move or per condition.
- Do not let condition names become code branches except in data lookup.
- Do not merge unrelated scopes into creature volatiles because it is convenient.
- Do not implement terrain, rooms, or doubles-only side behavior until a move group needs the
  exact hook.

### Queued Intents

Queued intents represent actions or consequences scheduled for a later timing point: recharge,
future damage, charge-turn release, fail-if-hit checks, first-turn gates, target-action gates,
cannot-repeat gates, and delayed end-turn status. They are battle-state tasks, not UI prompts.

Smallest useful shape:

- A queue entry with timing, owner, optional target, source move/effect ID, remaining turns, and a
  compact payload.
- One queue processor called from existing turn phases: before action, after action, and end turn.
- Deterministic ordering by scheduled turn, priority bucket, side, slot, then insertion order.

Execution rules:

- Queue entries must survive exactly as long as their mechanic says, including switch/faint
  cancellation rules.
- Future damage uses the original source metadata needed for event logs and type/category rules.
- Recharge and cannot-repeat gates should reject or replace illegal actions through normal Core
  action validation.
- Charge-turn release should execute through the same move/effect path as ordinary damage.

Guardrails:

- Do not add ad-hoc booleans like `MustRecharge` or `UsedFakeOut` if a queue/gate entry can hold
  it generically.
- Do not let queued intents choose UI actions.
- Do not implement full simultaneous-action/doubles complexity unless target topology requires it.
- Every queued behavior needs a seeded determinism test.

### Mutation Helpers

Mutation helpers are the small, shared state changes that many moves need: held item movement,
berry consumption, ability override/suppression, type overlays, side cleanup, HP costs, healing,
cures, revivals, and switch-linked transfer. They keep low-level state edits in one place.

Smallest useful shape:

- Plain Core helper methods on the battle state/controller layer, grouped by state they mutate.
- Each helper validates legality, applies the mutation, and emits the matching event.
- Helpers return a small success/failure result instead of throwing for normal battle failure.

Execution rules:

- Item helpers must distinguish consumed, removed, stolen, swapped, destroyed, restored, and
  temporarily disabled items.
- Ability helpers must affect hook lookup without rewriting species definitions.
- Type helpers should use battle overlays or query hooks, not permanent species edits.
- HP helpers must centralize floor/clamp rules and faint/revival event emission.

Guardrails:

- Do not duplicate direct field mutation at call sites.
- Do not add a general "set arbitrary battle state" helper.
- Do not make Runtime or Creator apply battle mutations.
- Do not add new serialized shapes without `DATA_SCHEMA.md`, schema version, migration note, and
  tests.

### Move References

Move references select another move and execute, copy, replace, or force it. The primitive is not
"Metronome code" or "Copycat code"; it is a shared way to resolve a move reference and route it
through the normal move executor.

Smallest useful shape:

- A reference selector for sources such as last move, user's known moves, ally/party moves, random
  legal move pool, target's move, or authored replacement target.
- One `executeResolvedMove` path used by normal moves and referenced moves.
- Metadata that records visible source, PP owner, event owner, selected target, and RNG source.

Execution rules:

- PP remains owned by the move slot or mechanic specified by data.
- Forced execution must still validate target legality and battle restrictions.
- Random selection must use injected battle RNG and stable ordering.
- Replacement/copy effects must define whether they last for battle, until switch, or permanently
  in save data before implementation.

Guardrails:

- Do not implement separate executors for copy, call, force, and random effects.
- Do not include official move pools in shipped content.
- Do not bypass validation because a move was called indirectly.
- Do not implement permanent learned-move mutation unless the owning data/schema phase explicitly
  allows it.

### Snapshot Overlays

Snapshot overlays represent temporary battle views of a creature: substitute HP decoy,
transformed stats/types/moves, type overlays, stat swaps, and passable shields. They sit on top of
base species/creature data for battle resolution.

Smallest useful shape:

- A list of active overlays on a battle creature, each with kind, source, duration/clear rule, and
  compact payload.
- Query helpers that ask for effective stats, types, moves, and decoy HP by applying overlays in a
  documented order.
- Cleanup helpers for switch, faint, battle end, and moves that remove overlays by tag.

Execution rules:

- Base creature/species definitions remain unchanged.
- Overlays must participate in the same query hooks as ordinary data.
- Substitute-like decoys intercept damage/status according to hooks, then remove themselves when
  HP reaches zero.
- Transform-like snapshots copy exactly the fields the spec permits and no more.

Guardrails:

- Do not rewrite species records, move definitions, or saved creature data during battle.
- Do not store duplicated "effective" stats unless a test proves recomputing is a problem.
- Do not build sprite/form presentation in this primitive.
- Do not let overlays outlive their explicit clear rule.

### Damage Memory

Damage memory records what happened recently so later effects can refer to it: counter damage,
revenge power, damage taken this turn, hit-count scaling, and switch interception. It is an event
index, not a second event log.

Smallest useful shape:

- A per-battle memory list or ring buffer keyed by turn number.
- Records with source side/slot, target side/slot, damage amount, damage category, move ID,
  hit index, whether it connected, and whether it caused faint.
- Query helpers for common needs: last damage to target this turn, total damage stored by owner,
  times hit, last move failed, last ally fainted.

Execution rules:

- Write memory from the same place damage events are emitted.
- Clear or age out records by turn according to the mechanic using them.
- Multi-hit moves should write per-hit records and allow total queries.
- Counter-style effects must fail cleanly when no qualifying record exists.

Guardrails:

- Do not parse the human-readable battle log to answer mechanics.
- Do not keep unbounded history unless a mechanic requires whole-battle memory.
- Do not add separate memory stores per move group.
- Do not use wall-clock time or nondeterministic ordering.

### Explicit No-Op And Post-Battle Ops

Some moves intentionally do nothing in battle, or affect rewards/money after battle. These need
explicit data markers so validation can distinguish "supported no battle-state change" from
"missing mechanic."

Smallest useful shape:

- Keep `noBattleEffect` and `postBattleReward` as explicit supported ops.
- Add only the smallest params needed for the next reward or no-op category.
- Emit an event only if the player needs visible confirmation or tests need a stable marker.

Execution rules:

- No-op means no battle-state mutation.
- Post-battle reward markers are consumed by the post-battle reward path, not by damage/effect
  resolution.
- Validation must require authors to choose these ops intentionally.

Guardrails:

- Do not fake unsupported mechanics as no-op to make audit rows pass.
- Do not implement economy, reward UI, or celebration presentation in the battle primitive.
- Do not broaden this into an arbitrary post-battle scripting system.

## Build Order Authority

`IMPLEMENTATION_PLAN.md` section 10 is the only current build order. The older family-level order
has been superseded because 15B target/doubles execution and its 57-entry normalization/
certification cohort are complete, while the remaining 15C-15G packages have explicit dependency
ordering. Use the primitive-family sections below to classify requirements, never to bypass that
queue.

## Engine Work Groups

### Target Selection And Battle Topology

Add a reusable target resolver for all opponents, all other creatures, allies, user-or-ally,
user-and-allies, all-allies, all-pokemon, fainted party targets, side fields, opponent fields,
entire field, and specific previous-move targets. The resolver should produce a deterministic
ordered target set and a target scope passed into generic effect execution.
Implemented slice: the pure resolver classifies all 16 target shapes into stable active, party,
side, field, or move-reference scopes for singles and doubles. The controller materializes live
active targets with the specified invalidation/fallback and random-draw rules, resolves ordered
spread actions, and keeps side/field scopes distinct from creature targets. Ally selection,
redirection, allied position exchange, slot-aware outcomes, and typed replacement are implemented.

Completed evidence includes pure resolver coverage, doubles admission/materialization matrices,
per-target RNG/event/trace tests, redirect/position vectors, outcome/replacement vectors, three
focused family goldens, the cumulative 15B golden, and 57 generated per-reference definitions and
vectors. The 15B exit review is GO. Rows in this historical move list that remain uncertified retain
at least one dependency owned by 15C-15G or 15H; their target shape alone is no longer a blocker.

Moves: `acupressure`, `aromatherapy`, `aromatic-mist`, `aurora-veil`, `bleakwind-storm`,
`blizzard`, `burning-jealousy`, `captivate`, `chilly-reception`, `clanging-scales`,
`clangorous-soulblaze`, `coaching`, `comeuppance`, `core-enforcer`, `corrosive-gas`,
`cotton-spore`, `counter`, `court-change`, `crafty-shield`, `curse`, `dark-void`,
`diamond-storm`, `discharge`, `dragon-cheer`, `dragon-energy`, `earthquake`, `electric-terrain`,
`electroweb`, `explosion`,
`fairy-lock`, `fiery-wrath`, `flower-shield`, `gear-up`, `glacial-lance`, `glaciate`,
`grassy-terrain`, `gravity`, `growl`, `happy-hour`, `haze`, `heal-bell`, `heal-block`,
`heat-wave`, `helping-hand`, `hold-hands`, `howl`, `hyper-voice`, `icy-wind`, `incinerate`,
`ion-deluge`, `jungle-healing`, `lands-wrath`, `last-respects`, `lava-plume`, `leer`,
`life-dew`, `light-screen`, `lucky-chant`, `lunar-blessing`, `magic-room`, `magnetic-flux`,
`magnitude`, `make-it-rain`, `mat-block`, `max-airstream`, `max-darkness`, `max-flare`,
`max-flutterby`, `max-geyser`, `max-hailstorm`, `max-knuckle`, `max-lightning`,
`max-mindstorm`, `max-ooze`, `max-overgrowth`, `max-phantasm`, `max-quake`, `max-rockfall`,
`max-starfall`, `max-steelspike`, `max-strike`, `max-wyrmwind`, `me-first`, `metal-burst`,
`mind-blown`, `mirror-coat`, `mist`, `misty-explosion`, `misty-terrain`, `mortal-spin`,
`muddy-water`, `mud-sport`, `origin-pulse`, `overdrive`, `parabolic-charge`, `perish-song`,
`petal-blizzard`, `poison-gas`, `powder-snow`, `precipice-blades`, `psychic-terrain`,
`quick-guard`, `razor-leaf`, `razor-wind`, `reflect`, `relic-song`,
`revival-blessing`, `rock-slide`, `rototiller`, `safeguard`, `sandsear-storm`,
`searing-shot`, `self-destruct`, `shadow-half`, `shadow-shed`, `shadow-sky`, `shell-trap`,
`sludge-wave`, `snarl`, `snowscape`, `sparkling-aria`, `splishy-splash`, `springtide-storm`,
`string-shot`, `struggle-bug`, `surf`, `sweet-scent`, `swift`, `synchronoise`,
`tail-whip`, `tailwind`, `take-heart`, `teatime`, `teeter-dance`, `tera-starstorm`,
`thousand-arrows`, `thousand-waves`, `trick-room`, `twister`, `venom-drench`, `water-sport`,
`wide-guard`, `wildbolt-storm`, `wonder-room`.

### Damage Query Modifiers

Add query hooks for base power, move type, damage category, type effectiveness,
accuracy, crit, priority, healing, and final damage. These should extend the existing damage
calculation path rather than fork it.
Implementation note: `damageStatOverride` now covers direct offensive/defensive damage-stat
overrides through the normal damage pipeline. `targetHpThresholdPower` now covers target-current-HP
threshold base-power multiplication through the same damage path. `hpRatioPower` now covers
user-current-HP and target-current-HP ratio base power through the same damage path.

Representative tests: HP-ratio power, status-dependent power, speed-ratio power, item-dependent
power, target-weight power, terrain/weather type and power changes, and final-damage modifiers.

Moves: `acrobatics`, `assurance`, `avalanche`, `barb-barrage`, `beat-up`,
`bolt-beak`, `camouflage`, `collision-course`, `defense-curl`,
`echoed-voice`, `electro-ball`, `electro-drift`, `expanding-force`, `facade`,
`fishious-rend`, `flail`, `fling`, `flying-press`, `foul-play`, `freeze-dry`, `frustration`,
`fury-cutter`, `grass-knot`, `gyro-ball`, `hex`, `hidden-power`, `ice-ball`, `infernal-parade`,
`knock-off`, `lash-out`, `last-respects`, `light-that-burns-the-sky`, `low-kick`, `magnitude`,
`mud-sport`, `natural-gift`, `payback`, `photon-geyser`, `power-trip`, `present`, `psyblade`,
`psywave`, `punishment`, `rage-fist`, `retaliate`, `return`,
`revenge`, `reversal`, `rollout`, `round`, `shell-side-arm`, `smelling-salts`,
`solar-beam`, `solar-blade`, `spit-up`, `stomping-tantrum`, `stored-power`, `temper-flare`,
`terrain-pulse`, `trump-card`, `venoshock`, `wake-up-slap`, `water-sport`,
`weather-ball`.

### Turn Timing, Queued Effects, And Move Gates

Add one queued-intent path for delayed damage, recharge, charge variants, future hits,
fail-if-hit, first-turn gates, previous-turn gates, target-action gates, cannot-repeat gates,
miss-conditional effects, and end-turn delayed statuses.

Representative tests: recharge prevents the next selected action; delayed damage lands on the
scheduled turn; first-turn-only moves fail later; cannot-repeat moves reject consecutive use;
target-selected-attack gates fail when the target did not choose a matching action.

Moves: `alluring-voice`, `aurora-veil`, `barb-barrage`, `beak-blast`, `bide`, `blast-burn`,
`blood-moon`, `bolt-beak`, `bounce`, `crafty-shield`, `destiny-bond`, `dig`, `dive`,
`doom-desire`, `earthquake`, `electro-shot`, `endure`, `fake-out`, `first-impression`, `fishious-rend`,
`fly`, `focus-punch`, `frenzy-plant`, `future-sight`, `giga-impact`, `gigaton-hammer`, `gust`,
`hurricane`, `hydro-cannon`, `hyper-beam`, `infernal-parade`, `light-screen`, `lucky-chant`,
`magic-coat`, `mat-block`, `metal-burst`, `meteor-beam`, `mirror-move`, `mist`, `order-up`,
`phantom-force`, `psyblade`, `quick-guard`, `reflect`, `retaliate`, `safeguard`, `shadow-force`,
`shell-trap`, `skull-bash`, `snore`, `sticky-web`, `stomping-tantrum`, `sucker-punch`, `surf`,
`tailwind`, `tera-blast`, `thunder`, `thunderclap`, `toxic-spikes`, `twister`, `upper-hand`,
`whirlpool`, `wide-guard`, `yawn`.

### Volatile Conditions, Statuses, And Move Lockouts

Generalize volatile conditions and lockouts: infatuation, disable, encore, taunt, torment,
yawn, perish, nightmare, curse, no-switch traps, throat/silence, powder, rage, ingrain,
telekinesis, and related state.

Representative tests: duration countdown, switch-clear behavior, before-move prevention,
end-turn damage/status application, trapping switch rejection, and condition event emission.

Moves: `anchor-shot`, `aqua-ring`, `attract`, `baton-pass`, `block`, `blood-moon`, `charge`,
`destiny-bond`, `dire-claw`, `disable`, `embargo`, `encore`, `foresight`, `gigaton-hammer`,
`grudge`, `heal-block`, `imprison`, `ingrain`, `jaw-lock`, `leech-seed`, `magic-coat`,
`mean-look`, `miracle-eye`, `nightmare`, `octolock`, `odor-sleuth`, `perish-song`,
`poison-powder`, `powder`, `rage`, `sappy-seed`, `sleep-powder`, `smack-down`, `snore`,
`spider-web`, `spite`, `spore`, `stun-spore`, `substitute`, `tar-shot`, `taunt`, `telekinesis`,
`thousand-arrows`, `thousand-waves`, `throat-chop`, `topsy-turvy`, `torment`, `tri-attack`,
`yawn`.

### Stat Stage Model Expansion

Multiple ordered `statStage` effects, accuracy/evasion stages, `statStageAll`, stage reset/copy/swap/invert,
and HP-cost setup moves are already implemented. Remaining work is stage averaging, Baton Pass-style
stage transfer, stockpile-like counters, and move-specific topology or side-condition gaps.

Representative tests: Haze clears both sides;
Clear Smog clears target stages after damage; Power Split/Guard Split average values
deterministically; Baton Pass passes only whitelisted state.

Moves: `armor-cannon`, `baton-pass`, `bulk-up`, `calm-mind`,
`clangorous-soulblaze`, `close-combat`, `coaching`, `coil`,
`cosmic-power`, `decorate`, `defend-order`, `defog`, `double-team`, `dragon-ascent`,
`dragon-dance`, `flash`, `gear-up`, `geomancy`, `growth`, `guard-split`,
`headlong-rush`, `hone-claws`, `kinesis`, `leaf-tornado`,
`magnetic-flux`, `memento`, `minimize`, `mirror-shot`, `mud-bomb`, `muddy-water`, `mud-slap`,
`night-daze`, `noble-roar`, `no-retreat`, `octazooka`, `octolock`,
`parting-shot`, `power-split`, `power-trip`, `punishment`,
`quiver-dance`, `rototiller`, `sand-attack`, `shell-smash`, `shift-gear`,
`smokescreen`, `speed-swap`, `stockpile`, `stored-power`, `superpower`, `sweet-scent`,
`tearful-look`, `tickle`, `tidy-up`, `v-create`, `venom-drench`,
`victory-dance`, `work-up`, `zippy-zap`.

### Field And Side Conditions

Add screens, safeguard, mist, terrains, rooms, gravity, tailwind, snow, field damage modifiers,
weather accuracy changes, weather healing changes, and side/field duration handling.

Representative tests: screen halves eligible damage; terrain changes damage/type/status rules;
Trick Room changes turn order; weather expires deterministically; Court Change swaps eligible
side conditions only.

Moves: `aurora-veil`, `blizzard`, `body-slam`, `camouflage`, `chilly-reception`, `court-change`,
`crafty-shield`, `defog`, `echoed-voice`, `electric-terrain`, `expanding-force`, `fairy-lock`,
`grassy-terrain`, `gravity`, `growth`, `happy-hour`, `haze`, `hurricane`, `ion-deluge`,
`light-screen`, `lucky-chant`, `magic-room`, `mat-block`, `mist`, `misty-terrain`, `moonlight`,
`morning-sun`, `mud-sport`, `nature-power`, `psychic-terrain`, `quick-guard`, `reflect`,
`safeguard`, `secret-power`, `shadow-half`, `shadow-shed`, `shadow-sky`, `shore-up`,
`snowscape`, `solar-beam`, `solar-blade`, `steel-roller`, `sticky-web`, `synthesis`,
`tailwind`, `terrain-pulse`, `thunder`, `toxic-spikes`, `trick-room`, `water-sport`,
`weather-ball`, `wide-guard`, `wonder-room`.

The 15E-3 weather-status audit found seven immutable source rows whose generic `ailment: freeze`
attempt must pass through the sun field filter: `move-0008`, `move-0058`, `move-0059`, `move-0181`,
`move-0423`, `move-0573`, and `move-0821`. This is a shared status-admission requirement, not seven
move implementations. These rows remain uncertified until their other declared mechanics are green.

The weather-healing audit found four immutable source rows requiring authored `heal.weather`
fractions: `move-0234`, `move-0235`, and `move-0236` use clear `1/2`, sun `2/3`, and
rain/sandstorm/hail `1/4`; `move-0659` uses clear `1/2` and sandstorm `2/3` while other weather stays
neutral. They remain inventory-only until the complete weather family and their conformance vectors
close together.

### Held Item And Berry Mutation

Add helpers to query, consume, steal, swap, remove, destroy, restore, and depend on held items
and berries. Reuse the Phase 15 held-item effect storage; do not create move-only item state.

Moves: `acrobatics`, `belch`, `bestow`, `corrosive-gas`, `covet`, `embargo`, `fling`,
`incinerate`, `judgment`, `knock-off`, `magic-room`, `multi-attack`, `natural-gift`, `recycle`,
`switcheroo`, `teatime`, `techno-blast`, `thief`, `trick`.

### Ability Mutation

Add battle-state ability override and suppression layered on the existing Phase 15 hook
dispatcher. Ability mutation must affect hook queries without rewriting species definitions.

Moves: `doodle`, `entrainment`, `gastro-acid`, `psychic-noise`, `role-play`, `simple-beam`,
`skill-swap`, `worry-seed`.

### Creature And Move Type Mutation

Add temporary type overlays for creatures and move type query hooks for move type changes.
These overlays must clear according to authored condition rules and must not mutate base
species data.

Moves: `burn-up`, `camouflage`, `conversion`, `conversion-2`, `double-shock`, `electrify`,
`forests-curse`, `ion-deluge`, `magic-powder`, `reflect-type`, `revelation-dance`, `roost`,
`soak`, `tera-blast`, `tera-starstorm`, `trick-or-treat`.

### Move Copy, Call, Replace, And Forced Execution

Add a move-reference selector and one `executeResolvedMove` path for random calls, last-move
calls, copied moves, party move calls, forced repeat execution, and permanent move replacement.
It must preserve PP ownership, event ownership, target legality, and deterministic RNG order.

Moves: `assist`, `copycat`, `instruct`, `me-first`, `metronome`, `mimic`, `mirror-move`,
`nature-power`, `psych-up`, `reflect-type`, `sketch`, `sleep-talk`, `snatch`.

### Switch Flow And State Passing

Add post-move user switch intents, weather-plus-switch, escape/switch variants, and Baton
Pass-style state transfer. Keep switch legality in Core and make the passable-state whitelist
explicit.

Moves: `baton-pass`, `chilly-reception`, `flip-turn`, `parting-shot`, `shed-tail`, `teleport`,
`u-turn`, `volt-switch`.

### Hazard, Screen, Substitute, And Side Cleanup

Add side-condition operations for `removeByTag`, `clearHazards`, `clearScreens`,
`clearVolatilesByTag`, and `swapSides`. Cleanup moves should call helpers, not inspect
individual condition names.

Moves: `court-change`, `defog`, `mortal-spin`, `rapid-spin`, `tidy-up`.

### Protect Variants With Contact Punish

Add protect-family conditions with an `onContactBlocked` effect list. Existing Protect behavior
should remain the base case; variants compose block plus contact punish effects.

Moves: `baneful-bunker`, `burning-bulwark`, `kings-shield`, `max-guard`, `mighty-cleave`,
`obstruct`, `silk-trap`, `spiky-shield`.

### Healing, Cures, HP Costs, And Fractional HP Effects

Add target/ally/team healing, party-wide status cure, self sleep plus full heal, status
transfer, HP costs, HP equalization, fractional current-HP damage, strength-sap-style stat
query healing, revival, wish-style delayed healing, and healing block rules.

Moves: `aromatherapy`, `dream-eater`, `endeavor`, `floral-healing`, `heal-bell`, `heal-block`,
`healing-wish`, `heal-pulse`, `jungle-healing`, `life-dew`, `lunar-blessing`, `lunar-dance`,
`moonlight`, `morning-sun`, `natures-madness`, `pain-split`, `present`, `psycho-shift`,
`purify`, `refresh`, `rest`, `revival-blessing`, `roost`, `shore-up`, `smelling-salts`,
`strength-sap`, `super-fang`, `synthesis`, `wish`.

### Redirection And Turn Order Manipulation

Add turn-order and target-redirection conditions resolved before action execution. In singles,
ally-position effects may be explicit no-ops or validation failures until topology supports them.

Moves: `after-you`, `ally-switch`, `coaching`, `decorate`, `follow-me`, `helping-hand`,
`hold-hands`, `instruct`, `quash`, `rage-powder`, `spotlight`.

### Accuracy Locks And Special Accuracy Exceptions

Add accuracy and crit query hooks for next-hit guarantees, identify/ignore-evasion effects,
Minimize exceptions, weather accuracy exceptions, and semi-invulnerable target exceptions.

Moves: `blizzard`, `body-slam`, `coil`, `dragon-rush`, `earthquake`, `flash`, `foresight`,
`gust`, `heat-crash`, `heavy-slam`, `hone-claws`, `hurricane`, `kinesis`, `laser-focus`,
`leaf-tornado`, `lock-on`, `magnet-rise`, `mind-reader`, `miracle-eye`, `mirror-shot`,
`mud-bomb`, `muddy-water`, `mud-slap`, `night-daze`, `octazooka`, `odor-sleuth`,
`sand-attack`, `smack-down`, `smokescreen`, `steamroller`, `stomp`, `surf`, `telekinesis`,
`thunder`, `twister`, `whirlpool`.

### Substitute, Transform, And Creature Snapshot Effects

Add battle overlays for substitute HP decoys, transferred decoys, copied stats/types/moves,
temporary stat swaps, and transform-like snapshots. These are runtime battle overlays only.

Moves: `power-trick`, `relic-song`, `shed-tail`, `substitute`, `tidy-up`, `transform`.

### Counter, Revenge, And Stored Damage

The 15G-2 foundation now records bounded typed per-hit damage by turn/action, stable side/party
source and target plus resolution slot, move, class/type, cause, outcome, calculated/applied/actual
amounts, hit count, critical/contact/substitute, and faint result. Immutable queries retain current
and previous-turn evidence without parsing events. Reusing those records for counter, revenge,
stored damage, switch interception, and hit-count scaling remains the 15G-3 consumer package; the
existing counter resolver has not yet been migrated to query the new store.

Moves: `bide`, `comeuppance`, `counter`, `final-gambit`, `metal-burst`, `mirror-coat`,
`pursuit`, `rage-fist`.

### Non-Battle Or Post-Battle Effects

Use existing `noBattleEffect` and `postBattleReward` markers for intentional no-op,
celebration, reward, or money effects. These moves need correct data authoring, not bespoke
battle resolver code.

Moves: `celebrate`, `happy-hour`, `hold-hands`, `make-it-rain`.

### Reference Data Gaps

These moves have no structured metadata or insufficient English effect text in the local JSON.
Before adding engine code, enrich their move data from the local descriptor if it is precise
enough; if it is not precise enough, use an approved mechanics reference and document the exact
generic ops chosen. Do not guess from vague flavor text.

Reference data gaps are not automatically engine gaps. Check existing primitives before adding
code. For example, `axe-kick` and `supercell-slam` share crash-recoil behavior with the existing
`recoil` op plus `onMiss: true`; their remaining blocker is exact authoring data such as the
secondary chance or crash amount.

Moves: `axe-kick`, `blazing-torque`, `chloroblast`, `combat-torque`, `fickle-beam`,
`fillet-away`, `glaive-rush`, `hard-press`, `hydro-steam`, `hyper-drill`, `ice-spinner`,
`jet-punch`, `kowtow-cleave`, `lumina-crash`, `magical-torque`, `mountain-gale`,
`mystical-power`, `noxious-torque`, `population-bomb`, `pounce`, `power-shift`,
`psyshield-bash`, `raging-bull`, `ruination`, `salt-cure`, `shadow-blitz`, `shadow-break`,
`shadow-down`, `shadow-end`, `shadow-hold`, `shadow-mist`, `shadow-rave`, `shadow-storm`,
`shadow-wave`, `shelter`, `spicy-extract`, `spin-out`, `stone-axe`, `supercell-slam`,
`syrup-bomb`, `tachyon-cutter`, `torch-song`, `triple-dive`, `twin-beam`, `wave-crash`,
`wicked-torque`.

## No-Meta Descriptor Ledger

This table lists failed rows where `meta` is absent or insufficient. The "descriptor-derived
behavior" column is taken from the audited move descriptor/effect text in `MOVE_AUDIT_RESULTS.md`.
Rows with "Reference Data Gaps" still need exact authoring data before the engine can honestly
claim support.

| Move | Descriptor source | Descriptor-derived behavior | Assigned reusable group(s) |
|---|---|---|---|
| `alluring-voice` | flavor_text fallback | damage power=80; target=selected-pokemon; status: confusion; conditional secondary/effect | Turn Timing, Queued Effects, And Move Gates |
| `armor-cannon` | flavor_text fallback | damage power=120; target=selected-pokemon; multiple stat changes | Stat Stage Model Expansion |
| `axe-kick` | flavor_text fallback | damage power=120; target=selected-pokemon; status: confusion; recoil/crash damage | Reference Data Gaps; existing crash recoil op covers miss recoil once exact params are authored |
| `barb-barrage` | flavor_text fallback | damage power=60; target=selected-pokemon; status: poison; dynamic power/modifier; conditional secondary/effect | Damage Query Modifiers, Turn Timing, Queued Effects, And Move Gates |
| `blazing-torque` | no English text | damage power=80; target=selected-pokemon; no English effect_entries or flavor_text_entries in local JSON | Reference Data Gaps |
| `bleakwind-storm` | flavor_text fallback | damage power=100; target=all-opponents | Target Selection And Battle Topology |
| `blood-moon` | flavor_text fallback | damage power=140; target=selected-pokemon; repeat-use lockout | Turn Timing, Queued Effects, And Move Gates; Volatile Conditions, Statuses, And Move Lockouts |
| `burning-bulwark` | flavor_text fallback | target=user; priority 4; status: burn | Protect Variants With Contact Punish |
| `chilly-reception` | flavor_text fallback | target=entire-field; accuracy bypass/no accuracy check; user switch effect; snow weather | Target Selection And Battle Topology; Field And Side Conditions; Switch Flow And State Passing |
| `chloroblast` | flavor_text fallback | damage power=150; target=selected-pokemon; recoil/crash damage | Reference Data Gaps; likely existing recoil op once exact params are authored |
| `collision-course` | flavor_text fallback | damage power=100; target=selected-pokemon; dynamic power/modifier | Damage Query Modifiers |
| `combat-torque` | no English text | damage power=100; target=selected-pokemon; no English effect_entries or flavor_text_entries in local JSON | Reference Data Gaps |
| `comeuppance` | flavor_text fallback | damage power=1; target=specific-move | Target Selection And Battle Topology; Counter, Revenge, And Stored Damage |
| `dire-claw` | flavor_text fallback | damage power=80; target=selected-pokemon; status: poison; status: paralysis; status: sleep | Volatile Conditions, Statuses, And Move Lockouts |
| `doodle` | flavor_text fallback | target=selected-pokemon; ability mutation/copy | Ability Mutation |
| `double-shock` | flavor_text fallback | damage power=120; target=selected-pokemon; type mutation | Creature And Move Type Mutation |
| `dragon-cheer` | flavor_text fallback | target=all-allies; crit-stage interaction; multi-target or ally targeting | Target Selection And Battle Topology |
| `electro-drift` | flavor_text fallback | damage power=100; target=selected-pokemon; dynamic power/modifier | Damage Query Modifiers |
| `electro-shot` | flavor_text fallback | damage power=130; target=selected-pokemon | Turn Timing, Queued Effects, And Move Gates |
| `fickle-beam` | flavor_text fallback | damage power=80; target=selected-pokemon | Reference Data Gaps |
| `fillet-away` | flavor_text fallback | target=user; accuracy bypass/no accuracy check | Reference Data Gaps |
| `gigaton-hammer` | flavor_text fallback | damage power=160; target=selected-pokemon; repeat-use lockout | Turn Timing, Queued Effects, And Move Gates; Volatile Conditions, Statuses, And Move Lockouts |
| `glaive-rush` | flavor_text fallback | damage power=120; target=selected-pokemon | Reference Data Gaps |
| `hard-press` | flavor_text fallback | target=selected-pokemon | Reference Data Gaps |
| `headlong-rush` | flavor_text fallback | damage power=120; target=selected-pokemon; multiple stat changes | Stat Stage Model Expansion |
| `hydro-steam` | flavor_text fallback | damage power=80; target=selected-pokemon | Reference Data Gaps |
| `hyper-drill` | flavor_text fallback | damage power=100; target=selected-pokemon | Reference Data Gaps |
| `ice-spinner` | flavor_text fallback | damage power=80; target=selected-pokemon | Reference Data Gaps |
| `infernal-parade` | flavor_text fallback | damage power=60; target=selected-pokemon; status: burn; dynamic power/modifier; conditional secondary/effect | Damage Query Modifiers; Turn Timing, Queued Effects, And Move Gates |
| `jet-punch` | flavor_text fallback | damage power=60; target=selected-pokemon; priority 1 | Reference Data Gaps |
| `kowtow-cleave` | flavor_text fallback | damage power=85; target=selected-pokemon; accuracy bypass/no accuracy check | Reference Data Gaps |
| `last-respects` | flavor_text fallback | damage power=50; target=selected-pokemon; multi-target or ally targeting | Target Selection And Battle Topology; Damage Query Modifiers |
| `lumina-crash` | flavor_text fallback | damage power=80; target=selected-pokemon | Reference Data Gaps |
| `lunar-blessing` | flavor_text fallback | target=all-allies; accuracy bypass/no accuracy check | Target Selection And Battle Topology; Healing, Cures, HP Costs, And Fractional HP Effects |
| `magical-torque` | no English text | damage power=100; target=selected-pokemon; no English effect_entries or flavor_text_entries in local JSON | Reference Data Gaps |
| `make-it-rain` | flavor_text fallback | damage power=120; target=all-opponents | Target Selection And Battle Topology; Non-Battle Or Post-Battle Effects |
| `mighty-cleave` | flavor_text fallback | damage power=95; target=selected-pokemon | Protect Variants With Contact Punish |
| `mortal-spin` | flavor_text fallback | damage power=30; target=all-opponents; status: poison | Target Selection And Battle Topology; Hazard, Screen, Substitute, And Side Cleanup |
| `mountain-gale` | flavor_text fallback | damage power=100; target=selected-pokemon | Reference Data Gaps |
| `mystical-power` | flavor_text fallback | damage power=70; target=selected-pokemon | Reference Data Gaps |
| `noxious-torque` | no English text | damage power=100; target=selected-pokemon; no English effect_entries or flavor_text_entries in local JSON | Reference Data Gaps |
| `order-up` | flavor_text fallback | damage power=80; target=selected-pokemon; conditional secondary/effect | Turn Timing, Queued Effects, And Move Gates |
| `population-bomb` | flavor_text fallback | damage power=20; target=selected-pokemon | Reference Data Gaps |
| `pounce` | flavor_text fallback | damage power=50; target=selected-pokemon | Reference Data Gaps |
| `power-shift` | flavor_text fallback | target=user; accuracy bypass/no accuracy check | Reference Data Gaps |
| `psyblade` | flavor_text fallback | damage power=80; target=selected-pokemon; dynamic power/modifier; conditional secondary/effect | Damage Query Modifiers; Turn Timing, Queued Effects, And Move Gates |
| `psychic-noise` | flavor_text fallback | damage power=75; target=selected-pokemon; ability mutation/copy | Ability Mutation |
| `psyshield-bash` | flavor_text fallback | damage power=70; target=selected-pokemon | Reference Data Gaps |
| `rage-fist` | flavor_text fallback | damage power=50; target=selected-pokemon | Damage Query Modifiers; Counter, Revenge, And Stored Damage |
| `raging-bull` | flavor_text fallback | damage power=90; target=selected-pokemon | Reference Data Gaps |
| `revival-blessing` | flavor_text fallback | target=fainting-pokemon; accuracy bypass/no accuracy check | Target Selection And Battle Topology; Healing, Cures, HP Costs, And Fractional HP Effects |
| `ruination` | flavor_text fallback | damage power=1; target=selected-pokemon | Reference Data Gaps |
| `salt-cure` | flavor_text fallback | damage power=40; target=selected-pokemon | Reference Data Gaps |
| `sandsear-storm` | flavor_text fallback | damage power=100; target=all-opponents; status: burn | Target Selection And Battle Topology |
| `shadow-blitz` | effect_entries | damage power=40; target=selected-pokemon | Reference Data Gaps |
| `shadow-break` | effect_entries | damage power=75; target=selected-pokemon | Reference Data Gaps |
| `shadow-down` | effect_entries | target=opponents-field | Reference Data Gaps |
| `shadow-end` | effect_entries | damage power=120; target=selected-pokemon | Reference Data Gaps |
| `shadow-half` | effect_entries | target=entire-field | Target Selection And Battle Topology; Field And Side Conditions |
| `shadow-hold` | effect_entries | target=opponents-field; accuracy bypass/no accuracy check | Reference Data Gaps |
| `shadow-mist` | effect_entries | target=opponents-field | Reference Data Gaps |
| `shadow-rave` | effect_entries | damage power=70; target=opponents-field | Reference Data Gaps |
| `shadow-shed` | effect_entries | target=entire-field; accuracy bypass/no accuracy check | Target Selection And Battle Topology; Field And Side Conditions |
| `shadow-sky` | effect_entries | target=entire-field; accuracy bypass/no accuracy check | Target Selection And Battle Topology; Field And Side Conditions |
| `shadow-storm` | effect_entries | damage power=95; target=opponents-field | Reference Data Gaps |
| `shadow-wave` | effect_entries | damage power=50; target=opponents-field | Reference Data Gaps |
| `shed-tail` | flavor_text fallback | target=user; accuracy bypass/no accuracy check; user switch effect | Switch Flow And State Passing; Substitute, Transform, And Creature Snapshot Effects |
| `shelter` | flavor_text fallback | target=user; accuracy bypass/no accuracy check | Reference Data Gaps |
| `silk-trap` | flavor_text fallback | target=user; priority 4; accuracy bypass/no accuracy check | Protect Variants With Contact Punish |
| `snowscape` | flavor_text fallback | target=entire-field; accuracy bypass/no accuracy check; snow weather | Target Selection And Battle Topology; Field And Side Conditions |
| `spicy-extract` | flavor_text fallback | target=selected-pokemon; accuracy bypass/no accuracy check | Reference Data Gaps |
| `spin-out` | flavor_text fallback | damage power=100; target=selected-pokemon | Reference Data Gaps |
| `springtide-storm` | flavor_text fallback | damage power=100; target=all-opponents | Target Selection And Battle Topology |
| `stone-axe` | flavor_text fallback | damage power=65; target=selected-pokemon | Reference Data Gaps |
| `supercell-slam` | flavor_text fallback | damage power=100; target=selected-pokemon; recoil/crash damage | Reference Data Gaps; existing crash recoil op covers miss recoil once exact params are authored |
| `tachyon-cutter` | flavor_text fallback | damage power=50; target=selected-pokemon | Reference Data Gaps |
| `take-heart` | flavor_text fallback | target=all-allies; accuracy bypass/no accuracy check | Target Selection And Battle Topology |
| `temper-flare` | flavor_text fallback | damage power=75; target=selected-pokemon; dynamic power/modifier | Damage Query Modifiers |
| `tera-blast` | flavor_text fallback | damage power=80; target=selected-pokemon; conditional secondary/effect | Turn Timing, Queued Effects, And Move Gates; Creature And Move Type Mutation |
| `tera-starstorm` | flavor_text fallback | damage power=120; target=all-opponents; multi-target or ally targeting | Target Selection And Battle Topology; Creature And Move Type Mutation |
| `thunderclap` | flavor_text fallback | damage power=70; target=selected-pokemon; priority 1 | Turn Timing, Queued Effects, And Move Gates |
| `tidy-up` | flavor_text fallback | target=user; accuracy bypass/no accuracy check; sets spikes hazard | Stat Stage Model Expansion; Hazard, Screen, Substitute, And Side Cleanup; Substitute, Transform, And Creature Snapshot Effects |
| `torch-song` | flavor_text fallback | damage power=80; target=selected-pokemon | Reference Data Gaps |
| `triple-dive` | flavor_text fallback | damage power=30; target=selected-pokemon | Reference Data Gaps |
| `twin-beam` | flavor_text fallback | damage power=40; target=selected-pokemon | Reference Data Gaps |
| `upper-hand` | flavor_text fallback | damage power=65; target=selected-pokemon; priority 3 | Turn Timing, Queued Effects, And Move Gates |
| `victory-dance` | flavor_text fallback | target=user; accuracy bypass/no accuracy check | Stat Stage Model Expansion |
| `wave-crash` | flavor_text fallback | damage power=120; target=selected-pokemon; recoil/crash damage | Reference Data Gaps; likely existing recoil op once exact params are authored |
| `wicked-torque` | no English text | damage power=80; target=selected-pokemon; no English effect_entries or flavor_text_entries in local JSON | Reference Data Gaps |
| `wildbolt-storm` | flavor_text fallback | damage power=100; target=all-opponents; status: paralysis | Target Selection And Battle Topology |

## Acceptance Checklist

A group is not complete until:

- The generic op/query/condition is documented in `BATTLE_SYSTEM_SPEC.md`.
- The compiler accepts valid params and rejects missing, invalid, or unknown params.
- Core tests cover the primitive boundaries and every affected move has a generated conformance
  case; one representative move is not enough for Phase 15 certification.
- Deterministic tests cover any RNG, ordering, queue, copy, or multi-target behavior.
- `MOVE_AUDIT_RESULTS.md` rows are updated only for moves that are exactly expressible.
- `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` passes.
- `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` passes.

## Final Closeout Criteria

Full move coverage is ready to claim only when every one of the 937 audited rows is certified:

- its source hash is locked;
- it normalizes into reusable generic Core data;
- it validates and compiles strictly;
- it resolves correctly in every required singles/doubles and ruleset context;
- its mechanic families and meaningful events are asserted by conformance tests; and
- intentional non-battle/post-battle behavior is explicit and reviewed.

There must be 937 certified, 0 FAIL/unknown/unmapped/disabled/reference-blocked, no bespoke
move-name/ID code anywhere in Core, no official content copied into samples or exports, and no
untested generic primitive in the battle path. A static review/search gate must inspect compiler,
resolver, helper, and type names for move-specific special cases before Phase 15 closes.
