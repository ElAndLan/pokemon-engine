# TESTING_STRATEGY

Status: **Phase 15B, 15C-1 through 15C-7, 15D-1 through 15D-7, 15E-1 through 15E-7, 15F-1 through 15F-6, and 15G-2 are complete; Phase 15 conformance continues at 173/937 and 15F-7 is next.**
xUnit suites and deterministic Core tests are active. The corpus manifest/inventory contract is
implemented in Cgm.Tools. Stable JSON/text snapshots use existing BCL serialization; Verify remains
unnecessary unless a later decision demonstrates value.

## Purpose
The test playbook: what gets tested at each layer, the golden-file (Verify) workflow, fixture
conventions, and the determinism rules that make battle replays reproducible.

## Must lock
- xUnit is active; Verify remains planned but is not currently installed. Core is graphics-free so
  most gameplay remains CI-testable.
- Golden-replay workflow: (seed + team + action script) → event log snapshot; changes only
  intentional, with a stated reason. Unexplained golden diffs fail review.
- Fixture conventions (`tests/fixtures/`, `samples/`); determinism rules (injected IRng, no
  wall-clock/sleep/network in tests); the standing full-game input-replay regression test.

## Phase 15 test layers

1. **Corpus inventory tests** — neutral fixture wrappers prove stable ordering/hashes/digest,
   duplicate/malformed rejection, name omission, and deterministic serialization.
2. **Primitive tests** — formulas, queries, conditions, targets, queues, mutations, and failure
   boundaries.
3. **Compiler tests** — valid generic data compiles; missing/unknown ops and params fail strictly.
4. **Resolver tests** — state and events in required singles/doubles contexts.
5. **Per-move conformance tests** — every reference key declares test IDs for each mechanic family;
   only passing required-context behavior advances status to `certified`.
6. **Golden replay tests** — seed + ruleset + topology + teams + action script produce stable event
   and effect-trace text/JSON. Golden changes require an explicit reason in the change report.
7. **Fuzz/property tests** — bounds, conservation, target legality, queue/hook termination, and
   replay determinism across the certified corpus.

## Phase 15 evidence progression

Manifest statuses advance only when the immediately preceding evidence exists:

| Status | Required generated evidence |
|---|---|
| `inventoryOnly` | Locked source ID/hash and sanitized structured observations |
| `normalized` | Complete generic definition hash, mechanic families, topology, ruleset, and no unknown requirement |
| `compiled` | Strict validator success plus typed compiler test IDs; all invalid-param siblings remain rejected |
| `certified` | Required valid and invalid contexts resolve with asserted state/events/traces and deterministic replay |

No status is inferred from a helper existing, a representative test passing, or a legacy audit PASS.
Counts are regenerated from the manifest/test registry and never edited by hand.

Every normalized conformance vector records: neutral reference key, normalized definition hash,
ruleset, topology, immutable definitions, initial battle state, action/target script, RNG seed or exact
draw sequence, expected state assertions, ordered event assertions, trace assertions, and test IDs.
When one entry declares multiple mechanic families or alternate valid contexts, its vector set must
assert each one before certification.

## Feature-package minimum test matrix

An implementation package selects every applicable row; omitting one requires a written reason:

| Behavior | Minimum evidence |
|---|---|
| Formula/query | Table tests for normal, threshold, min/max, zero guard, cap, and exact rounding; resolver path and trace |
| Serialized op | Valid compile plus missing, unknown, wrong-type, invalid-enum/range, incompatible-param, and duplicate tests |
| Mutation | Success, ordinary failure, event order, clamp/conservation, faint/full/empty state, switch/faint/end cleanup |
| Condition | Apply/block/duplicate/refresh/stack, duration, hooks, removal-by-tag, transfer if allowed, cleanup matrix |
| Target/topology | Singles and doubles valid cases, invalid explicit selection, stable spread order, random draw count, fainted target |
| Queue/timing | Due timing, insertion order, cancellation, PP/RNG skipped draws, switch/faint ownership, replay determinism |
| Multi-hit/target | Per-hit/per-target state, independent/shared rolls as specified, early faint stop, aggregate damage reuse |
| Move reference | Pool/exclusion order, PP/event ownership, target revalidation, recursion/loop termination |
| Ruleset difference | Same vector under each required profile with only the specified outcomes differing |
| AI awareness | Legal action/target, conservative reusable score signal, deterministic explanation, no resolver duplication |

After focused tests, run the full solution. Phase 15 package reports must include commands and pass
counts; “should pass” and previously green output are not evidence for the current change.

## Phase 15B execution acceptance matrix

The doubles execution packages are not complete until every row owned by that package passes through
the public controller path. Pure helper tests supplement these rows; they do not replace them.

| Owner | Required vectors |
|---|---|
| 15B-2 construction | singles and doubles valid assignments; too-small party; duplicate/out-of-range/fainted active assignment; side-only API rejection in doubles |
| 15B-2 admission | duplicate/missing/dead-slot submission; individual invalid move/switch/item/form; duplicate reserve; aggregate item shortage; two allied form requests; doubles capture; prove rejection changes no state/PP/stock/RNG |
| 15B-2 phase order | switch before item before form before move; two switch and two item speed orders; exact equal-speed Fisher-Yates draws; form-adjusted move speed; pass and queued-pass behavior |
| 15B-2 identity/events | actor switched/fainted/move-changed/resource-changed before execution; no inherited action; active-creature events carry the exact slot; singles compatibility event vector |
| 15B-3 target shapes | each of the 16 authored targets in every applicable singles/doubles context; active/party/side/field/move scope type; wrong selection kind, side, source, range, or faint state rejected |
| 15B-3 live invalidation | replacement occupies selected opponent/ally slot; empty selected opponent fallback; empty ally failure; spread filters dead slots; side/field executes once; PP and `MoveUsed` boundary |
| 15B-3 random | zero candidates fails with no draw after PP; one candidate succeeds with no draw; two candidates use exactly one `Next(2)` after filters; stable candidate order asserted |
| 15B-4 per-target | independent accuracy and secondary rolls; shared standard hit count; crit/random per hit per target; immunity/miss/faint paths; later target still receives snapshotted direct damage |
| 15B-4 spread/math | two live targets receive floor-at-Targets `3/4`; one live target receives full power; one later miss/immune still preserves the two-target modifier; action-total and per-target totals asserted |
| 15B-4 order/trace | action → target → hit → effect event order; exact RNG trace bounds/results/skipped draws; target effects before action-scoped recoil/cost; singles and doubles family goldens |
| 15B-5 redirect | eligible/ineligible tags and classes; bypass; multiple redirectors ordered by priority/speed/topology; no tie draw; spread/ally/side/field non-redirection; redirection target becomes invalid |
| 15B-5 position | legal atomic ally swap; singles/invalid rejection; creature-owned state follows creature; slot-owned state remains; subsequent selected-slot resolution asserted |
| 15B-6 outcome | one-side elimination; healthy reserve is not defeat; simultaneous action wipe draw; end-turn batch draw; remaining scheduled actions stop only on terminal outcome |
| 15B-6 replacement | one/two empty slots; unique atomic choices; invalid/fainted/active/duplicate choice rejection without mutation; topology event order; entry-hook faint/re-request; unfillable slot; next-turn admission blocked while pending |

Every RNG row supplies a counting scripted `IRng` and asserts both returned values and call bounds.
Every rejection row snapshots controller state, event count, PP, stock, queues, and RNG calls before
and after. The cumulative 15B golden records all four slots acting, at least one spread action, one
invalidation, simultaneous faints with reserves, and deterministic replacements.

Completed package evidence (2026-07-14): 15B-2 through 15B-6 have green controller-path matrices.
The three focused family goldens cover singles direct resolution, doubles spread resolution, and the
replacement checkpoint; `phase-15b-cumulative.golden` additionally spans four-slot admission,
spread resolution, two captured-action invalidations, atomic replacement, and a simultaneous draw.
The generated target/topology catalog registers 57 sanitized reference vectors whose exact target,
effect, contact, event-slot, and typed compilation assertions pass in doubles. The focused 15B exit
review found and fixed side-only active-event and timed-move lifecycle drift, then returned GO.

## Phase 15C-1 query evidence

The unified numeric-query suite covers every modifier operation, exact rational reduction,
per-multiplication integer flooring, stage/priority/scope/insertion ordering, replacement precedence,
registry clamps, invalid and overflow boundaries, and stable step traces. Controller integration
asserts action-addressed base-power/stat/final-damage/healing traces, slot-owned doubles hooks,
accuracy threshold flooring, and unchanged fixed damage. The full 15C-1 closeout is 1,141 green tests;
manifest/definition regeneration remains deterministic; the later 15C-2 package advances it to 72 certified entries.

## Phase 15C-3 physical-formula evidence

Speed and physical-metric tables cover every inclusive lower band edge, linear floors/caps,
effective speed stages and paralysis, immutable base versus overlaid metrics, maximum integers,
zero denominators, checked overflow, airborne/grounded invariance, resolver query traces, AI parity,
schema round-trip/migration, and invalid metric definitions. The generated catalog registers two
new speed-formula vectors and advances strict certification to 74; weight rows remain routed to
later condition/timing dependencies rather than being over-certified.

## Phase 15C-4 action-history evidence

Typed history tests cover first/repeat/cap boundaries, checked overflow, every terminal outcome,
different-action/Pass/switch/faint/miss/failure resets, one-turn aging, actual seeded tie order,
doubles owner isolation, same-turn ally reuse, replacement entry-hazard faints, immutable bounded
snapshots, BasePower traces, and Smart AI fairness. Compiler matrices reject unknown, duplicate,
chance-gated, non-damaging, nonpositive, and malformed formulas. Four generated history vectors
advance strict certification to 78; rows with separate sound/slicing or timing dependencies remain
uncertified. The changed instrumented Core production lines measured 98.82% line coverage.

## Phase 15C-5 party/resource formula evidence

Twenty-three focused Core tests cover empty/full/duplicate party slots and every living/fainted/
contributing filter; friendship 0/1/254/255; PP 0/1/max plus ordinary spend and charge-release
snapshots; all seven positive-stage inputs; present, absent, unknown, nonpositive, and suppressed item
data; weighted endpoints, zero weights, single-entry no-draw, overflow, all-miss preservation, spread
and multi-hit reuse, deterministic replay/query traces, compiler/validation rejection, and Smart AI
expected-value/item parity. Six generated Tools theories validate every newly certified normalized
row through MoveRule, MoveCompiler, and BasePower query resolution. The full solution passes 1,382
tests; the dirty-worktree Core coverage run exercises 893/917 changed instrumented production lines
(97.38%); strict certification is 84/937.

## Phase 15G-2 damage-memory evidence

Twenty focused tests cover immutable complete records and stable source/target creature queries,
pending and completed attempts, current/previous-turn aging, battle-end reset, checked totals,
strict malformed/outcome rejection, target-level miss/protection/no-qualifying results, resolved
immunity/no-damage/substitute vectors, calculated versus mitigated versus actual overkill damage,
critical/contact/faint evidence, multi-hit stop/order, doubles topology/party isolation, fixed/level/
OHKO/counter/HP-formula causes, status-class HP equalization, and seeded replay identity. The full
Core suite passes 1,136 tests; the complete dirty-worktree Core coverage run exercises 574/586
changed instrumented production lines (97.95%). No new event, trace, RNG draw, schema, dependency,
or certification is introduced.

## Phase 15D-1 and 15E-1/2 stateful-foundation evidence

The typed intent suite covers ordered preview/consume/complete boundaries, same-checkpoint deferral,
all target policies, owner cleanup, atomic turn rejection, PP/RNG preservation, debug serialization,
and replay determinism. The scoped-condition suite covers all seven stores, exact owner/source
tuples, reject/refresh/replace/stack, duration 1/N and variable duration, topology ordering,
switch/faint/end cleanup, typed lifecycle events/traces, strict definition/application rejection,
and replay-stable snapshots. Tag/source removal and general transfer/swap remain 15E-7. The
completed 15E-2 dispatcher additionally covers
stable scope/priority/topology ordering, duplicate suppression/conflict rejection, bounded atomic
intent emission, immutable collection snapshots, and resolver/AI query parity; concrete condition
payload families remain with 15E-3 through 15E-7. The first 15E-3 weather checkpoint additionally
covers exact registry rows, source identity, same-weather no-op and replacement, duration-one and
ordinary expiry, typed damage modifiers, topology-ordered residual/immunity behavior, battle-end
cleanup, zero added RNG, resolver/AI parity, and replay. Weather corpus certification remains blocked
until the remaining stat/natural-input/ruleset interaction matrix passes. The
weather-accuracy checkpoint adds strict compiler rejection, bypass/no-draw behavior, override-before-
stage ordering, present/absent/unlisted weather, resolver hook/query traces, and Smart AI parity. The
weather-status checkpoint covers the exact sun/freeze row, absent/unlisted weather and status,
invalid-status rejection, no chance draw or status event on denial, no cure of an existing freeze,
immediate replacement semantics, unrelated-status preservation, resolver hook trace, and Smart AI
status-component parity. The weather-healing checkpoint covers strict table compilation and malformed
rows, every supported weather ratio, odd-max-HP direct rounding, self/target recipients, full-HP
event suppression, absent/unlisted weather, replacement and expiry, zero RNG, query/hook traces, and
missing-HP-capped Smart AI parity.
The weather-move checkpoint covers strict type/power/skip-charge compilation, present/absent/unlisted
rows, effective-type immunity/effectiveness/STAB/history proof, exact base-power floor/order,
skip-versus-retain/release PP timing, hook/query traces, zero added RNG, doubles parity, and Smart AI
parity.
The weather-family exit adds the modern snow registry/profile row, Sandstorm Rock Special Defense
and Snow Ice Defense query modifiers, exact stage-then-weather flooring, present/absent/type/stat/
ruleset tables, singles and per-target doubles resolution, Smart AI score parity, type-based airborne
residual proof, strict profile/input rejection, and battle-start natural weather source/duration/
event/expiry coverage. Terrain-owned natural environment selection remains in the terrain family;
weather-setting move certifications remain separate per-move evidence.

The terrain intrinsic checkpoint covers all four closed registry rows, modern-only profile
admission, apply/same-no-op/replace/expiry source and event traces, natural/effective environment
fallback, grounded and airborne query traces, exact grounded-source boosts and grounded-target
Misty reduction, Electric/Misty status and confusion denial with skipped RNG, Psychic per-target
priority denial before accuracy, Grassy topology healing before expiry, field-action exclusion,
resolver/Smart-AI parity, and deterministic replay. The authored-interaction checkpoint adds strict
type/power/priority/spread/gate/removal/heal compilation, user/target grounded matrices, effective
type and power traces, action-order proof, doubles materialization, pre-PP/RNG failure, exact-store
removal traces, healing replacement, and Smart-AI parity. Deferred environment consumers and
per-move certification remain with their owning later packages. The lifecycle-hook checkpoint additionally
covers ability switch-in summon, post-event `onTerrainChange` replacement with bounded redispatch,
source-owned held duration extension versus an opposing holder, strict op/hook validation, schema-v6
round-trip and v5 no-op migration, exact condition source/duration, and zero added RNG.

The environment-input checkpoint covers all twelve valid natural values, rejects the four
terrain-only values and unknown enums as natural input, proves clear/apply/replace/remove/expire
effective-state transitions without changing the natural value, and requires value-identical
resolver/Smart-AI environment snapshots with no additional RNG. Called-move, conditional-secondary,
and type-mutation behavior is not claimed by this checkpoint.

The grounded-override checkpoint covers strict `groundedState` compilation and target admission,
effective-type inputs, field/creature/ability/item precedence, same-owner grounded/airborne
replacement, duration expiry, switch/faint cleanup, ordinary condition events/traces, zero effect
RNG, and resolver/Smart-AI terrain parity. Schema-v7 round-trip and v6 no-op migration prove the
additive `onGroundedQuery` hook. Gravity accuracy/move blocking remains open.

The terrain-seed exit checkpoint covers valid/missing/unknown/wrong-type/numeric/duplicate payloads,
battle-start topology order, post-`TerrainChanged` event order, switch-in activation, mismatch,
airborne holders, +6 deferral without consumption, consume-once state, Defense and Special Defense,
zero added RNG, deterministic event output, and Smart AI use of the resulting ordinary stat stages.
Together with the intrinsic, authored, environment-input, lifecycle-hook, and grounded-override
checkpoints, this closes the terrain family; Gravity accuracy/move blocking remains in the combined
room/gravity/sport criterion.

The room/gravity/sport exit covers strict `fieldCondition` and `fieldMoveGate` compilation,
coexistence, room toggle, duplicate rejection, one/many-turn expiry, priority-preserving Trick Room
speed reversal, Wonder Room defensive base/stage routing, Magic Room held-effect suppression without
payload mutation, Gravity grounding/accuracy/pre-PP legality, classic/modern sport fractions and
source/duration cleanup, shared lifecycle events/traces, resolver/Smart-AI parity, zero condition RNG,
and deterministic replay. No project/save schema vector changes because both ops use the existing
open effect payload shape.

The 15E-4 screen checkpoint covers strict side-condition/bypass/removal compilation, independent
side ownership and row coexistence, duplicate rejection without refresh, duration one/many and
source-held extension, physical/special/all-damage present/absent tables, exact singles/doubles
fractions, critical and explicit bypass, before-damage/after-hit/no-match removal order, modern snow
admission, per-target query/hook traces with one shared duration, Smart-AI parity, unchanged RNG
ownership, and deterministic replay. General tag/source selectors and side transfer/swap remain
15E-7.

The 15E-4 side-guard checkpoint covers persistent status and confusion, single/all-stat negative
deltas, self/ally/positive and stage-transform exclusions, contact sources, chance/duration RNG
skips, bypass and common-barrier removal, doubles side ownership, AI status parity, replay, and
unchanged audit regeneration. It adds no schema vector because the open effect payload and existing
typed side-condition op remain the serialized boundary.

The 15E-4 side speed/order checkpoint covers strict default/explicit duration compilation,
stage-paralysis-hook flooring, no current-turn reschedule, exact shared expiration, source switching,
both doubles slots from one side instance, Trick Room after effective Speed, speed-ratio resolver/AI
parity, query/hook trace ownership, and zero condition RNG. The existing open effect payload remains
the serialized boundary, so no schema vector changes.

The 15E-4 side critical-guard checkpoint covers strict compilation, owner/coexist/duplicate policy,
five-checkpoint lifecycle and source switching, opposing versus same-side scope, exact zero
`CriticalChance`, preserved per-hit crit/damage RNG order, crit-stage and stat-stage boundaries,
screen interaction, doubles/multi-hit shared ownership, query/hook/event/trace output, AI-neutral
visibility, and deterministic replay. It adds no schema vector or presentation event.

The 15E-4 paired-action side-effect checkpoint covers strict source/target side compilation, four-
checkpoint lifecycle, coexistence and duplicate rejection, stable boost-then-reduction Speed order,
exact quarter Speed, end-turn `1/8` residual floor and effective-type exclusion, one shared doubles
instance with per-slot application, damaging-only secondary chance doubling and 100 clamp, preserved
chance draws, source switching, query/hook/lifecycle/event traces, Smart-AI parity, and deterministic
replay. Pair recognition and combined execution remain 15D-7; generic removal/transfer remains
15E-7. The open effect payload gains only an optional closed `side` parameter, so no schema version
or migration vector changes.

The 15E-4 side-wide protection exit covers all four closed behavior rows, exact one-checkpoint
lifecycle, coexistence and duplicate rejection, authored-versus-resolved priority profiles,
authored multi-target scope in singles/doubles, active-target status exclusions, damaging-move and
first-action composition, eligible allied spread targets, per-target mixed doubles outcomes,
pre-accuracy/effect RNG skips, tagged bypass and removal, source-faint persistence, damage-memory,
condition hook and effect traces, Smart-AI damage/status parity, and deterministic replay. Classic
guard success-chain sharing and personal/contact variants remain 15E-6. The open effect payload and
existing tag selectors require no schema or migration vector.

The 15E-5 entry-hazard exit covers strict generic damage/status/stage compilation and malformed
siblings; immutable permanent side conditions; zero/cap and duplicate layers; source credit;
grounded/airborne and effective-type immunity; layered and typed fraction math; status progression,
ordinary status immunity, type absorption, and status/stage side guards; tagged removal; stable
condition sequence within topology-ordered two-slot entry; hazard faint followed by repeated
replacement; lifecycle/event/effect/query/hook traces; AI layer-cap and switch-cost parity; zero RNG;
and the checked-in `entry-hazard.golden`. Legacy op aliases compile to the same generic profile and
no named controller state remains. General transfer and atomic side swap remain 15E-7. The existing
open effect payload requires no schema or migration vector.

The 15E-6 exit covers strict personal/side profile compilation, the legacy alias, every malformed
scope/filter/chain/draw/contact/bypass sibling, immutable payload capture, first/repeated/failure and
Pass/item/switch/ordinary/prevention reset boundaries, exact `gen4_like` factor-2 and
`modern_reference` factor-3 fractions, guaranteed draw/no-draw policy, creature/side condition
lifecycle, per-target mixed spread protection, all-protection bypass, noncontact exclusion, ordered
damage/status/stage contact payloads with existing guards, source-faint stopping, damage-memory,
events, condition/effect traces, Smart-AI fraction parity, deterministic replay, and the checked-in
`protection.golden`. The open effect payload adds only new closed op params, so no schema migration
vector changes.

The 15E-7 exit covers strict remove/transfer/swap compilation and every missing, unknown, mistyped,
duplicate-selector, incompatible-scope/owner/target/reset sibling; generic condition-ID, tag, all,
and user/target/environment source selectors; zero/one/many matches; field/weather/terrain/room/
side/slot/creature removal; cross-side and intra-side transfer; duration/counter/source/sequence
preservation and explicit reset; side/slot empty/occupied swap; destination stacking conflicts and
complete rollback; deterministic lifecycle event/condition/effect trace order; unchanged hook
enumeration; zero RNG; Smart-AI snapshot visibility without speculative scoring; replay identity;
and the checked-in `condition-mutation.golden`. The existing open effect payload adds only closed op
params, so no schema migration vector changes.

The 15F-2 exit covers strict `itemRequire`/`itemMutation` compilation; require/consume/give/steal/
swap/remove/destroy/restore/suppress success and failure; catalog holdability, capacity, key/item and
sticky-ability protection; atomic two-item preflight; latest-consumption aging and successful-restore
spend; switch identity, faint/switch/end cleanup, timed suppression, conservation, effective item
power and held-hook refresh, legacy consume-marker reset, owner-addressed events, exact effect trace
ranges, Smart-AI known-own versus hidden-target parity, zero mutation RNG, deterministic regeneration,
six per-reference transfer/restore vectors, and the checked-in `item-mutation.golden`. The existing
open effect payload adds only closed params, so no schema migration vector changes.

The 15C-6 exit covers strict compilation for extended `damageStatOverride`, `damageClassQuery`, and
`effectivenessQuery` rows plus every missing, duplicate, mistyped, unknown, non-type, nonpositive,
and incompatible sibling; standard single/dual tables; immunity and override precedence; inverse and
additional attacking types; all STAB sources; fixed and higher-offense class selection; alternate
stat owners; effective move/type/class/stat/type overlays; Wonder Room; absent/present natural and
effective environment; one/two-target spread snapshots; ordinary and formula-bypassing damage;
skipped RNG on final immunity; exact identity/effectiveness traces; structurally identical Smart-AI
and resolver query results; replay identity; and the checked-in `damage-query.golden`. The existing
open effect payload adds only closed params, so no schema migration vector changes.

The 15C-7 exit additionally covers every Accuracy/Evasion stage combination and cap; ordinary/null/
bypass/ignore-evasion plus weather/Gravity ordering; next-accuracy and next-critical apply/reapply/
expiry/source-owner-switch/faint/wrong-target/protected/missed/immune/guarded/success consumption;
critical stages below zero through above cap; exact ordinary versus guaranteed draw bounds/order;
priority clamp/snapshot/equal-speed tie behavior; final-damage replace/multiply/floor/cap on ordinary
and fixed families; move healing multiply/replace-zero/missing-HP clamp with no event on zero;
singles/doubles per-target isolation; resolver/Smart-AI parity; replay; and a deterministic family
golden. Query metadata has strict enum/operand/composition validation and no named-move branch.

The 15D-2 exit covers every action-gate kind/timing and every missing, unknown, duplicate,
mis-targeted, mistimed, or incompatible param sibling; atomic selection rejection with no state/PP/
event/queue/RNG mutation; before-versus-after-`MoveUsed` PP and event order; previous-result and
before/after target history; target any/damaging/status plans without AI hidden-action access;
required/forbidden any/physical/special current-turn damage; miss/protection/immunity/no-damage
boundaries; generic slot block versus creature-owned recharge; all submitted action kinds;
switch/faint cancellation; singles/doubles actor isolation; Smart-AI source-history legality;
intent/effect trace order; replay identity; zero added RNG; sanitized conformance vectors; and the
checked-in `action-gate.golden`. The open effect payload adds closed params only, so no schema
migration vector changes.

The 15D-3 exit covers missing/unknown/duplicate charge, state, target-policy, start-stat, and hit-
exception params; first-turn PP/event/trace and zero-draw behavior; skip-charge immediate execution;
one start-effect application; release action replacement with no second PP; live versus snapshot
replacement; random/spread/self target preservation; status/flinch/confusion/gate interruption;
switch/forced-switch/faint/end cleanup; all four semi-invulnerable states; ordinary no-draw miss;
matching accuracy followed by optional base-power multiplication; protection/weather/field
composition; singles/doubles isolation; Smart-AI forced release and visible-state rejection;
replay identity; sanitized conformance vectors; and the checked-in `charge.golden`. The open effect
payload and diagnostic queue state add no serialized schema migration vector.

The 15D-5 exit covers default and fixed typed profiles; compiler rejection of chance, unknown,
duplicate, malformed, non-damaging, and charge-composed locks; exact min/max duration draw counts;
stored move/action/selected-slot ownership; first-use and row-controlled repeat PP; zero-PP visible
termination; connected ramp steps through the 16x cap; miss/failure/prevention termination policy;
forced switch and faint cleanup reasons; post-lock confusion order; keyed power-boost persistence and
switch cleanup; doubles isolation; Smart-AI forced-candidate parity; `BasePower`/effect traces; the
checked-in `multi-turn-lock-termination.golden`; and generated per-reference vectors for all seven
affected rows. The corpus remains 937 entries and strict certification advances to 121/937.

The 15D-6 exit covers all eight closed selection filters plus the execution-time chance gate;
compiler rejection of unknown tags/filter/owner/duration/chance combinations; PP, choice, owner,
and opposing-source precedence; all-moves-blocked `UseFallback` admission and damage/recoil; timed
refresh/expiry and owner/source switch cleanup; multi-turn selection-block termination; exact
infatuation draw/event/PP boundaries; singles/doubles owner isolation; Smart-AI legality parity; and
nine sanitized generated vectors. Repeated normalization must be byte-identical. The corpus remains
937 entries and strict certification advances to 130/937.

## Phase 15F-4 type-overlay evidence

The type-overlay exit covers strict compilation for requirements, replace/add/remove/copy,
environment and damage-history sources, move-local queries, and target/all-active turn rules;
atomic failure and immutable definitions; the three-type cap, duplicate/empty fallback rules,
overlay precedence and cleanup; shared STAB/effectiveness/grounding and resolver/AI consumers;
stable doubles owner/event order; exact resistance-selection draws and trace ranges; and the neutral
`type-mutation.golden`. Eleven sanitized generated vectors cover every currently eligible reference
row, raising strict certification to 163/937. Three typeless-state rows remain routed to 15H because
the locked effective-type model requires a nonempty list, and two Tera/form rows remain routed to
15F-6. Repeated normalization is byte-identical.

## Phase 15F-5 stage-mutation checkpoint evidence

The first 15F-5 checkpoint covers strict `statStageMutation` compilation and malformed siblings;
maximize with ordinary +6 clamping; random eligible-stat pools at zero, one, and many candidates;
fixed enum ordering and the exact one-draw/bound policy; atomic positive-stage steal from a mixed
positive/negative snapshot; capped user gains; stable existing stage events; typed no-op and applied
traces; Smart-AI setup/no-effect parity without preview RNG; and damaging-steal ordering before the
same hit's damage query in both singles and doubles.

The second checkpoint covers strict `derivedStatMutation` and `metricMutation` compilation;
effective pre-snapshot average/split/swap with odd-floor arithmetic; terminal additive-layer values
that do not double earlier contributions; derived Speed and physical-metric formula consumption;
positive metric replacement, add-to-one clamping, and user/target swap; atomic batch validation and
overflow/fainted rollback; switch/faint/end and TurnEnd cleanup; immutable base definitions; stable
owner/stat event and typed trace order; zero RNG; and named neutral Smart-AI visibility.

The third checkpoint covers the stage-pass seam independently of 15G-1 switch selection: immutable
seven-slot capture in fixed stat order, side/party identity across slot changes and doubles positions,
replacement of an incoming creature's prior stages, single-use consumption, same-creature and fainted
target failure without prospective mutation, cancellation/source-faint/battle-end discard, and an
explicit exclusion matrix for HP, status, PP, types, ability, held item, confusion, critical stage,
seed, trap, and action locks. The transferable-condition registry remains empty until 15G-1.

The package-close matrix composes pre-damage stage steal, damage, atomic derived-stat split, and
metric mutation in `stat-mutation.golden`, asserts byte-stable replay, and registers seven sanitized
generated reference vectors covering maximize plus HP cost, random target selection, offense and
defense split, weight addition, Speed swap, and damaging positive-stage steal. Repeated normalization
is byte-identical and strict certification advances from 163/937 to 170/937.

## Phase 15F-6 decoy lifecycle checkpoint evidence

`BattleDecoyTests` covers strict closed-op compilation; default/custom fractions; malformed chance,
target, class, range, and duplicate rows; exact HP cost; duplicate/insufficient atomicity; immutable
base values; breaking-hit no-overflow; post-break secondary blocking; substitute damage-memory
fields; multi-hit rematerialization; status and damage bypass; drain/recoil accounting; direct target
healing; switch cleanup; delayed damage; doubles owner isolation; Smart-AI preflight; stable events,
traces, no extra RNG, and `decoy-lifecycle.golden`. `DecoyConformanceTests` owns the single eligible
generated row. Repeated normalization is byte-identical and strict certification advances from
170/937 to 171/937. Transform, copied PP, form transitions, and temporary move replacement remain
required before the encompassing 15F-6 package can close.

## Phase 15F-6 Transform snapshot checkpoint evidence

`BattleTransformTests` owns strict compile validation; exact copied/excluded field matrices; effective
pre-state capture; immutable collection and definition regressions; independent five-PP move pools;
stage capture; nested/fainted/decoy atomic failures; action selection, legality, damage/type/Speed and
ability-hook consumption; prior/later overlay precedence; switch/faint/end cleanup; doubles owner
isolation; Smart-AI parity; stable events/traces; no extra RNG; and `transform-snapshot.golden`.
Generated Transform conformance owns one sanitized reference vector. Form transition ownership and
temporary move replacement remain required before the encompassing 15F-6 package can close. The
focused resolver suite passes 11 tests, generated conformance passes 2 tests, the Battle regression
filter passes 1,345 tests, and the full solution passes 1,962 tests; repeated generation is byte
identical and strict certification advances from 171/937 to 172/937.

## Phase 15F-6 form-transition checkpoint evidence

The existing `BattleV6HookExecutionTests` form matrix now also owns wide-intermediate HP-ratio
flooring and the living 1-HP boundary; the side-owned once-per-battle resource after switching to a
second eligible creature; atomic doubles rejection of two allied temporary activations; and prior/
later same-layer overlay precedence through the shared effective-value resolver. The deterministic
`form-transition.golden` composes activation, remapped move use on both active turns, timed expiry,
HP-ratio reversion, and exact form-overlay removal. Existing form tests retain key-item requirement,
pre-move activation and Speed ordering, PP-slot preservation, condition/weather/held-item changes,
faint/capture/battle-end reversion, stage preservation, and immutable authored-form coverage.
Battle regression proof covers resolver/Smart-AI consumers of the shared overlay result. No move row
is newly certifiable from this controller action alone, so strict certification remains 172/937.
Two repeated normalization/audit runs are byte-identical at the existing definitions and manifest
hashes.

## Phase 15F-6 temporary move-replacement checkpoint evidence

`BattleTemporaryMoveReplacementTests` owns the closed compiler shape; automatic exclusion tag;
last-successful history despite a later miss; exact independent five-PP runtime copy; fallback,
tagged, duplicate-known, missing, failed-current-turn, and decoy failure atomicity; slot-only overlay
precedence; switch/faint/battle-end cleanup; effective type/class capture; event/trace/no-RNG behavior;
Smart-AI parity; and the deterministic `temporary-move-replacement.golden`. Shared action-history
coverage proves successful identities survive passes and unsuccessful attempts but clear on switch,
faint, and battle end. Generated conformance owns one sanitized reference vector and advances strict
certification from 172/937 to 173/937. The encompassing 15F-6 package still requires its cumulative
nested interaction/reversion matrix and package golden before closure. Focused replacement/history
tests pass 35/35, generated conformance passes 2/2, the Battle regression filter passes 1,359/1,359,
and the full solution passes 1,977/1,977 (1,645 Core, 104 Creator, 21 Runtime, 207 Tools).

## Phase 15F-6 package-close evidence

`BattleOverlayStoreTests` now composes decoy, Transform, temporary move replacement, and a later form
group on one owner. The matrix proves form-first visibility, exact field-by-field reveal back through
replacement, Transform, authored values, and independent decoy state; immutable base PP; switch
removal of decoy/Transform/replacement while the form group transfers; transferred-form faint cleanup;
and atomic all-family battle-end cleanup. `snapshot-package.golden` records the same reveal and cleanup
sequence twice with byte-identical state/trace output. The five family/overlay suites pass 88/88,
affected generated conformance passes 6/6, the Battle regression filter passes 1,361/1,361, and the
full solution passes 1,979/1,979 (1,647 Core, 104 Creator, 21 Runtime, 207 Tools). Together with the
four family checkpoints above, this closes 15F-6 at 173/937 without a new certifiable reference row.

## Golden format

A golden input records ruleset ID, topology, RNG seed/state, immutable definitions, initial battle
state, and submitted actions. Output records ordered `BattleEvent`s plus ordered effect traces.
Dynamic paths, timestamps, object hashes, and display-localized text are excluded. Entity IDs in
checked-in executable fixtures are original neutral content.

Goldens live under `tests/Cgm.Core.Tests/Battle/Goldens/`. An update must state which spec change
altered ordering or values. Regenerating snapshots merely to make tests pass is forbidden.

## Corpus fixture boundary

The local `docs/pokeapi-results` corpus is not required on CI. Cgm.Tools tests build small neutral
wrapper fixtures. The generated sanitized manifest may be committed because it contains numeric
reference keys, hashes, structured mechanics tags, and conformance metadata only—never official
names, prose, assets, URLs, or raw reference JSON.

## Phase 15A CI gate

- Tool unit tests pass without the local corpus.
- The Phase 15A baseline command (without a decision catalog) reports exactly 937 entries and 0
   certified; the current Phase 15 command with its decision catalog reports 163 certified.
- Regenerating the manifest from unchanged files is byte-identical.
- Generated output contains no payload names or source filenames.
- Full solution build/tests remain green.

## Phase 16-19 product evidence contract

`IMPLEMENTATION_PLAN.md` v4 supplies the numeric budgets and package-specific vectors. The following
layers are mandatory when those phases become current; each owning spec copies its applicable rows
before implementation.

| Area | Automated evidence | Manual/external evidence |
|---|---|---|
| Runtime boot/data | raw/pack equality; argument/config/version/hash/asset exit codes; partial-failure disposal | friendly release error on supported Windows |
| Fixed step/render | synthetic tick/input edges; null-renderer command goldens; viewport/batch/resource tests | hidden/visible GL smoke and representative screenshot |
| Scenes/UI/input | lifecycle/focus/navigation/typewriter/rebind scripts | keyboard and gamepad complete required flow |
| Overworld/player/save | deterministic movement/encounter; conservation; save/temp/backup/migration; audio fallback | play/relaunch and device-loss smoke |
| Battle presentation | Core event-catalog completeness; action/target/replacement scripts; state conservation | singles/doubles readability and fast-forward smoke |
| Creator lifecycle/editors | headless ViewModel undo/dirty/save/recovery; schema-to-editor coverage; malformed input | keyboard/accessibility/scaling and no-JSON authoring |
| Assets/maps | algorithm fixtures; transaction rollback; tool/layer/entity undo; large-canvas budget | import/slice/reimport and two-map workflow |
| Playtest/export | process/quoting/crash/temp cleanup; transactional export; smoke codes/completeness | Creator-to-Runtime workflow and clean VM |
| Migration/security | every format matrix cell; seeded fuzz; path-root/symlink checks; stress/soak artifacts | backup recovery rehearsal |
| Release/docs | release manifest/checksums/license/link checks; offline clean-install script | tutorial, beta cohort, RC soak, final stranger test |

Performance tests record hardware/OS/build configuration, warm-up, sample count, p50/p95/max, and
threshold. A failure is not waived by rerunning until green; investigate and record the cause. Manual
evidence uses dated checklists committed under `docs/verification/` with version/commit, operator,
steps, expected/actual, artifacts, and issue links. External-user names may be anonymized.

Every end-to-end input replay records data/asset hashes, runtime/template version, initial save,
input-by-tick stream, injected RNG seed/state, ordered Core events, scene transitions, final Core/save
state, and screenshot checkpoints. Presentation timing may change only with an intentional golden
reason; gameplay state/events must remain identical across raw and packed modes.
