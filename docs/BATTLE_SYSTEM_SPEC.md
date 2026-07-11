# BATTLE_SYSTEM_SPEC

Status: **Partial / implemented sections are binding; Phase 15 completion contract rebased
2026-07-10.** Damage formula lives in `BATTLE_DAMAGE_CALC.md`; substantial v0–v6 Core mechanics
exist through the shared effect dispatcher. Phase 15 now completes the reusable Core mechanic
surface and certifies all 937 entries in `docs/pokeapi-results/move/` per
`IMPLEMENTATION_PLAN.md` v3.2. Phase 15A locked the corpus manifest and conceptual trace contract.
Each later feature package must finish its exact target, timing, query, condition, mutation,
ruleset, event, and trace semantics here before the corresponding code lands.

## Purpose
The exact, testable battle contract: state model, turn flow, every formula with rounding order,
the closed effect-op catalog, status/stage tables, capture, AI scoring, and the event catalog.

## Must lock
- Damage formula with Gen-4 rounding order → **written in [BATTLE_DAMAGE_CALC.md](BATTLE_DAMAGE_CALC.md)**
  (type effectiveness, STAB, crit, stage multipliers, modifier order). This spec references it,
  does not duplicate it. Still to lock here: stat formulas (IV/EV/nature); accuracy; priority.
- Status behaviors and stat-stage multiplier tables (exact); capture formula + shake checks.
- The **closed** effect-op palette (moves are data, not code) — versioned; new ops need a spec edit.
- `BattleAction`/`BattleEvent` catalog; AI scoring weights (data block); golden-replay workflow.

## Effect architecture — normalization contract (EFFECT_TYPES_CATALOG v0.5)

`docs/EFFECT_TYPES_CATALOG_v0_5.md` is the binding effect-architecture contract. The target model is
**few primitives + many reusable helpers + data-driven conditions with hooks + presets**, never
one-function-per-move. A move is an *ordered list of data effects*; a resolver dispatches each to a
shared primitive (`deal_damage`, `apply_condition`, `modify_stat_stage`, `heal_hp`, `chance_gate`, …);
statuses/hazards/weather/traps are `ConditionDef`s with scope + hooks (`on_turn_end`, `on_before_move`,
`on_damage_query`, …). New primitives require the §0 promotion rule (a genuinely new timing/scope model).

**Migration status.** `Move.Effects` compile into typed `BattleMove` effects and resolve through
the shared dispatcher (`EffectContext` + `MoveEffect` records + `ApplyEffect`) and reusable
primitives (`DamageCalc`, stage changes, `Heal`/`Sap`/`DrainLife`, `EffectMath`). Statuses,
entry hazards, weather, traps, protect, force-switch, counter, charge, and rampage behavior are
covered by the condition/effect machinery. Multiple `statStage` ops remain as ordered effects
instead of collapsing to one stage change; `accuracy`/`evasion` stages use the existing
accuracy/evasion stage table during hit checks. Authored `Move.Target` now survives compilation
into `BattleMove`; in the current singles topology, `selected`, `all-opponents`, and
`all-other-pokemon` resolve to the opposing active creature for existing damage/secondary logic,
while ally, side-field, entire-field, and true multi-creature doubles resolution remain outside
this slice. No move should have bespoke resolver code.

**Singles active-target resolver.** `BattleTargetResolver.ResolveSinglesActiveCreatureSide`
is the generic Core helper for current one-active-per-side battle topology. It accepts an authored
`MoveTarget` plus the source side and returns the active creature side for creature-scoped effects:
`user` resolves to the source active creature; `selected`, `all-opponents`, and `all-other-pokemon`
resolve to the opposing active creature. `users-field` and `entire-field` are legal move scopes but
are not active creature targets; callers must handle their field/side ops explicitly instead of
pretending they select a creature. The helper emits no events and performs no battle mutation.
Validation: every defined `MoveTarget` is classified; requesting an active creature side for a
field scope throws an invalid-operation error; an unknown enum value throws an out-of-range error.
Promotion rationale: spread-target aliases unblock single-battle exactness without adding
move-name-specific branches or doubles topology.

**Singles field-target scope.** `BattleTargetResolver.ResolveSinglesScope` classifies the same
authored target into a generic scope before effect dispatch: `user`, `selected`, `all-opponents`,
and `all-other-pokemon` are active-creature scopes; `users-field` is the source side scope; and
`entire-field` is the whole battlefield scope. The helper emits no events and mutates no state.
Validation: every defined `MoveTarget` must classify, and unknown enum values throw an
out-of-range error. Promotion rationale: field-scoped effects such as weather can be expressed as
field/side effects without requiring a fake active creature target or full doubles topology.

## Effect-op numeric formulas (Battle v5, Phase 14)

The closed op palette lives on `Move.Effects` (`{ op, chance?, params }`). Ops split into pure numeric
math (here, in `EffectMath` — the same pure-helper category as DamageCalc/CaptureCalc) and stateful
resolution (interpreter wiring into `BattleController`, a later chunk). This section locks the math so
the ops and their tests are unambiguous. All damage/heal amounts are **≥1** unless stated.

- **multiHit** — hits 2–5 times, Gen III/IV distribution: 2→3/8, 3→3/8, 4→1/8, 5→1/8. A fixed-N variant
  hits exactly N (params `{ min, max }` equal → fixed). Each hit rolls damage (and crit) independently.
- **drain** — user heals `max(1, floor(damageDealt × num/den))` (default ½). No heal if 0 damage dealt.
- **recoil** — user takes `max(1, floor(damageDealt × num/den))` (¼ or ⅓). `onMiss: true`: if the move
  misses, is blocked by Protect, or has no effect, the user instead takes `floor(maxHp × num/den)` crash
  damage (Gen IV: ½ maxHp), independent of drain.
- **fixedDamage** — ignores the damage formula: `flat` deals exactly `params.amount`; `levelBased` deals
  the user's level (Night Shade / Seismic Toss). Type immunity still applies (checked by the resolver).
- **ohko** — one-hit KO: accuracy = `userLevel − targetLevel + 30`, and the move **fails outright** if
  `targetLevel > userLevel` (accuracy 0). On hit, damage = target's current HP.
- **healFraction** — `heal` restores `max(1, floor(maxHp × num/den))` (default ½) to
  `recipient: self|target` (default self). (Weather-scaled variant is a later slot; v5 is the
  flat fraction.)
- **hpFraction** — applies a fractional HP mutation to `recipient: self|target`. Params are
  `{ recipient, operation: "heal"|"damage", basis: "maxHp"|"currentHp", num, den }`;
  all are required and `num`/`den` must be positive. The amount is
  `max(1, floor(selectedBasis × num/den))`; fainted recipients are unaffected, healing clamps at
  max HP, and damage can faint. It draws no RNG, cannot be chance-gated, and emits the ordinary
  heal/faint events plus `HpFractionDamaged` for fractional non-move damage. This covers shared
  fraction-current/max-HP mutation without embedding a named move formula.
- **ailment** — applies a persistent status or confusion. Params use `{ ailment }`; `{ status }` remains
  accepted as a legacy alias for existing project data.
- **statStageAll** — expands to five ordered `statStage` changes against Atk, Def, Spa, Spd, and Spe
  (never HP, Accuracy, or Evasion). Params: `{ delta: int, onSelf?: bool }`, where `delta` is nonzero
  and within -6..6. The chance gate belongs to the whole all-stat bundle: one chance roll decides
  whether all five stage changes apply. Targeting follows `onSelf`, or defaults to self when
  `Move.Target == user`; otherwise it targets the selected opposing active. Events are emitted in
  Atk, Def, Spa, Spd, Spe order. Promotion rationale: this covers all-stat boost/drop archetypes
  without requiring five authored effects with duplicated chance rolls.
- **hpCost** — user pays `max(1, floor(maxHp * num/den))` before later authored effects. Params:
  `{ num: int, den: int, allowFaint?: bool }`; `num` and `den` must be positive and `chance` is not
  allowed. If `allowFaint` is false and the user has HP less than or equal to the cost, the move's later
  effects do not resolve and no HP is paid. If `allowFaint` is true, the cost may faint the user and
  later effects stop because the source fainted. Emits `HpCostPaid` when HP is paid.
- **statStageReset** — resets stat stages to 0. Params: `{ scope: "self"|"target"|"both" }`. Affects all
  seven stage slots: Atk, Def, Spa, Spd, Spe, Accuracy, and Evasion. Emits one `StatStageChanged` event
  per changed slot, using the delta needed to return that slot to 0.
- **statStageCopy** — copies all seven stage slots from one active creature to the other. Params:
  `{ from: "self"|"target", to: "self"|"target" }`; `from` and `to` must differ. Emits one
  `StatStageChanged` event per changed destination slot.
- **statStageSwap** — swaps stage slots between the user and target. Params: `{ group?: "all"|"offense"|"defense" }`.
  `all` means all seven stage slots, `offense` means Atk/Spa, and `defense` means Def/Spd. Emits one
  `StatStageChanged` event for each changed side/stat.
- **statStageInvert** — multiplies all seven stage slots by -1 for one active creature. Params:
  `{ onSelf?: bool }`; targeting follows `onSelf`, or defaults to self when `Move.Target == user`, otherwise
  target. Emits one `StatStageChanged` event per changed slot.
  Promotion rationale: these helpers cover generic stage reset/copy/swap/invert and HP-cost setup moves
  without per-move resolver branches. They intentionally do not cover average-actual-stat effects or
  pass-stages-on-switch effects, which require separate stat overlay and switch-flow primitives.
- **noBattleEffect** / **postBattleReward** — explicit no-op/reward markers for moves whose visible effect
  is outside battle-state mutation. They compile successfully and emit no battle effect by themselves.
- **weather** — sets battlefield weather. Params: `{ weather: "rain"|"sun"|"sandstorm"|"hail" }`;
  `weather` is required, unknown params are rejected, and no events are emitted by the compiler. At
  resolution the shared field-condition path emits `WeatherChanged` when the weather changes.
- **damageStatOverride** — changes which battle stats feed the normal damage formula. Params:
  `{ offensiveStat?: "atk"|"def"|"spa"|"spd", defensiveStat?: "def"|"spd" }`; at least one param is
  required, `chance` is not allowed, and no events are emitted. The move's authored damage class still
  controls physical/special bookkeeping, burn handling, and counter-style memory; this op only changes
  the queried attacking/defending stat values before existing stage, hook, crit, weather, STAB, and type
  logic run. Promotion rationale: this covers stat-query damage archetypes without forking
  `DamageCalc` or adding per-move damage branches.
- **targetHpThresholdPower** — multiplies base power when the active target's current HP is at or
  below a max-HP fraction. Params: `{ thresholdNum: int, thresholdDen: int, multiplierNum: int,
  multiplierDen: int }`; all values must be positive, `chance` is not allowed, unknown params are
  rejected, and no events are emitted. The power query runs once per hit before `DamageCalc`; the
  multiplied power is floored and clamped to at least 1. Promotion rationale: this covers
  target-HP-threshold power archetypes through the normal damage formula without a move-name branch.
- **hpRatioPower** — scales base power by a battler's current HP ratio. Params:
  `{ source: "user"|"target" }`; `source` is required, `chance` is not allowed, unknown params are
  rejected, and no events are emitted. The selected battler is the move user for `user` and the
  active target for `target`. The power query runs once per hit before `DamageCalc` as
  `max(1, floor(basePower * currentHp / maxHp))`; if the selected battler has no positive max HP,
  base power is left unchanged. Promotion rationale: this covers HP-ratio base-power archetypes
  through the normal damage formula without forking damage calculation or adding move-name branches.
- **statusPower** - conditionally multiplies base power when either the move user or active target has
  a matching persistent status. Params: `{ subject: "user"|"target", status: "any"|persistentStatus,
  multiplierNum: int, multiplierDen: int, ignoreSourceBurnPenalty?: bool }`; subject, status, and both
  multiplier values are required, multiplier values must be positive, `chance` is not allowed, unknown
  params are rejected, and a move may declare at most one. The query runs once per hit before
  `DamageCalc`; when the subject has no status or a nonmatching status, base power is unchanged.
  `ignoreSourceBurnPenalty` takes effect only when the condition matches, and only suppresses the normal
  physical burn penalty for the source. Promotion rationale: this covers user/target status base-power
  archetypes through the normal damage formula without a move-name branch.

Rounding is floor throughout (matches Gen III/IV integer math and BATTLE_DAMAGE_CALC).

### Stateful ops — batch 1 (self-targeting)

- **critBoost** — a self-buff (Focus Energy): raises the user's persistent crit-stage bonus by `params.stages`
  (default 2), added to each of its later moves' crit stage. Volatile: cleared on switch-out. Draws no RNG.
- **selfDestruct** — the user faints after the move connects and deals its damage (Explosion). v1
  simplification: on a **miss** the user does not self-faint (documented ceiling; extend if needed).

The remaining v5 ops from the original batch list (chargeTurn, protect, hazards, weather,
forceSwitchOut, leechSeed, bind, multiTurnLock, counterDamage, accuracyBypass) have landed in
Core. The palette stays **closed** — new ops require editing this section.

## Smart AI status (Phase 14)

`BATTLE_AI_SPEC.md` owns the trainer AI design. Core now has `random`, `basic`, and `smart`
profile dispatch through `TrainerAi`. The smart chooser returns legal `BattleAction`s, scores
moves with named components, exposes a score table, tracks seen/repeated player moves, and can
make bounded voluntary switches with a cooldown. It values current v5-supported move categories:
damage, KOs, status, setup, hazards, protect, force-switch, recovery, trainer healing items, recoil, and self-KO risk.

`UseBattleItem` supports finite-stock fixed-HP healing items for trainer AI difficulty tuning.
Phase 14 is verified for now at Smart-vs-Basic 69.0% @400; full tuning resumes after Phase 15+
mechanics are available.

## Battle v6 contract (Phase 15)

Phase 15 adds battle hooks for abilities, held items, weather interactions, and forms. These are
data-driven effects, not per-ability or per-item code. The resolver dispatches hook ops from data
records, and any new primitive must be added to this section before code uses it.

### Hook order

When multiple Phase 15 systems can react to the same event, dispatch in this order:

1. Forced form checks (`permanent` and `condition`) that affect the active creature's current
   battle form.
2. Active creature ability hooks.
3. Active creature held-item hooks.
4. Opposing active creature ability hooks, when the hook targets incoming damage/status/contact.
5. Opposing active creature held-item hooks, when the hook targets incoming damage/status/contact.
6. Existing v5 field/side/volatile hooks (weather residual, hazards, bind, leech seed, protect).

Switch-in uses the same ordering, then applies existing entry hazards. If a hook changes weather,
`onWeatherChange` hooks run immediately after the `WeatherChanged` event in the same 1-6 order.
Later switch-in weather wins by replacing the current weather and resetting its duration.
The Core event stream locks this visible switch-in order: `SwitchedIn`, any condition-form
`FormChanged` from the pre-hook check, `WeatherChanged` from switch-in hooks, any weather-triggered
condition-form `FormChanged`, then end-turn held-item events.

Damage modifiers are multiplicative and floor only after the final product, matching the existing
weather damage-query behavior. Apply modifier sources in this order: outgoing ability, outgoing
held item, incoming ability, incoming held item, weather, then move-specific v5 modifiers.

End-of-turn order is: existing status/volatile residuals, held-item residual heal/damage, ability
residual heal/damage, weather residual, weather expiry, form timed-expiry/revert. A faint caused by
any step stops later hooks for that fainted creature.

### Ability hooks and ops

Ability definitions are project data keyed by `ability:*` IDs. Species expose 1-2 normal ability
slots plus an optional hidden slot; generated creature instances store the chosen ability ID.

Phase 15 supports only these hook points: `onSwitchIn`, `onModifyOutgoingDamage`,
`onModifyIncomingDamage`, `onStatusAttempt`, `onEndOfTurn`, `onContactReceived`, and
`onWeatherChange`. `statModify` effects run through the outgoing/incoming damage hooks because
they modify damage-stat queries during damage calculation. `onModifyStat` and `onFaint` are reserved
until a closed op needs those timings; Phase 15 validation rejects them.

Closed ability-op palette for the first v6 slice:

- `statModify` - multiply or add to a queried damage stat. Params:
  `{ stat: atk|def|spa|spd, multiplierPercent?: int, add?: int }`; at least one modifier
  is required.
- `typeDamageModify` - multiply damage for a move type or incoming type. Params:
  `{ type: string, multiplierPercent: int }`.
- `statusImmunity` - block a specific persistent status. Params: `{ status: string }`.
- `weatherSummon` - set weather and duration. Params:
  `{ weather: rain|sun|sandstorm|hail, duration?: int }`.
- `contactChanceEffect` - when the holder receives contact from a move with `makesContact`,
  chance-gate and apply one effect to the contacting attacker. Params:
  `{ status: burn|poison|toxic|paralysis|sleep|freeze }` or `{ stat: atk|def|spa|spd|spe, delta: int }`.
- `residualHeal` - heal a fraction of max HP at end of turn. Params: `{ num: int, den: int }`.
- `residualDamage` - damage a fraction of max HP at end of turn. Params: `{ num: int, den: int }`.

### Held-item hooks and ops

Held-item battle effects live on the existing `item:*` data and run only when the holder has that
item. Consumable held items mark themselves consumed in battle state; restore rules are outside
battle resolution.

Closed held-op palette for the first v6 slice:

- `thresholdHeal` - consume below an HP threshold and heal a fixed amount or fraction. Params:
  `{ thresholdPercent: int, healAmount?: int, healFractionPercent?: int }`; exactly one heal
  amount style must be authored. Consumed once per battle after it heals.
- `statusCure` - consume to cure one specific persistent status. Params: `{ status: string }`.
- `typeDamageBoost` - multiply outgoing damage for one move type. Params:
  `{ type: string, multiplierPercent: int }`.
- `choiceLock` - multiply outgoing damage for one damage class and lock future move choice until
  switch-out. Params: `{ damageClass: physical|special, multiplierPercent: int }`.
- `residualHeal` - non-consumable end-turn healing fraction.
- `surviveFromFull` - consume to survive a would-be KO at 1 HP when starting the hit at full HP.
  Takes no params.
- `weatherDurationExtend` - extend holder-summoned weather by a fixed turn count. Params:
  `{ turns: int }`.

### Forms

`forms[]` becomes active in schema v2. A form has a stable `formId`, stat/type/ability/sprite
overrides, and exactly one activation: `permanent`, `battle_temporary`, `battle_timed`, or
`condition`.

Battle-temporary forms require a held item on the creature plus a trainer key item on the side and
set a once-per-battle side flag. Core exposes active battle forms as
`ActivateForm(formId, moveIndex)`: the form activates before the selected move resolves. For
battle-temporary forms, the trainer key item is required but not consumed, and the side cannot
activate another temporary form later in the same battle. Battle-timed forms use their authored
`turns` count, may apply `hpMultiplierPercent`, and expire after weather end-of-turn processing on
their final active turn. `moveRemap` swaps the move data executed from an existing move slot while
the form is active; PP remains attached to the original slot, so transformed moves spend and display
the original slot's remaining/max PP and reversion preserves the spent PP. Timed forms store
remaining turns in battle state. Temporary and timed forms revert on faint and battle end; condition
forms re-evaluate after weather, held-item, or switch changes. Stat stages do not reset on form
change; max-HP changes preserve current HP by the same ratio, floored and clamped to at least 1
unless the creature is already fainted.

### Phase 15 testing contract

Required tests: hook-ordering goldens for switch-in, damage, status-attempt, weather-change, and
end-turn; one unit file per ability/held op; weather-summon precedence; held-item consume-once;
choice-lock legality; form activation matrix; form HP/stat/stage invariants; and schema v1-to-v2
migration coverage when the serialized shapes land.

## Phase 15 rebase — complete move conformance

The v6 ability/held-item/weather/form contract above is the implemented foundation, not the Phase
15 ceiling. The 2026-07-10 user scope decision makes full local-corpus move conformance the phase
exit gate.

### No hard-coded moves

Core must not contain:

- a branch keyed by a move `EntityId`, slug, display name, PokeAPI name, or source file;
- a resolver/compiler/helper/type whose only purpose is one named move;
- an enum case or boolean field that hides a single-move special case;
- a move-specific fallback when generic validation/compiler/resolution fails; or
- a move-specific AI scoring branch.

Reusable code is organized by behavior: target selection, query modification, condition scope,
timing hook, queue action, state mutation, damage/heal model, switch flow, snapshot overlay, move
reference, ruleset policy, or event emission. A named preset may exist only as data that expands to
those reusable primitives. Custom-authored moves must be able to use the same primitives.

Legacy implementation shapes such as named hazard/seed booleans or typed effects that encode one
content preset are Phase 15 architecture debt. They may remain temporarily while tests protect
behavior, but Phase 15 cannot close until they are represented by generic condition/effect data
where the behavior is shared (for example: type-scaled entry hazard rather than a named hazard
effect, and source-linked recurring drain rather than a named seed effect).

### Conformance contract

For each of the 937 locked source entries, the Phase 15 harness records a neutral reference key,
source hash, mechanic families, normalized definition hash, required ruleset/topology, and tests.
A move is certified only when it:

1. normalizes completely into generic data;
2. passes strict validation;
3. compiles into typed reusable effects;
4. resolves correctly in every required singles/doubles and ruleset context;
5. emits deterministic events and effect traces;
6. has assertions for every declared mechanic family and failure condition; and
7. has no unknown, disabled, unsupported, or reference-blocked requirement.

An inapplicable-context failure is tested but does not certify the move unless its valid context
also works. Intentional visual-only, no-battle, post-battle, and overworld effects must be explicit
data operations and manually reviewed; a silent no-op never counts.

### Required architecture expansion

Phase 15 adds only capabilities demanded by the corpus: complete target/doubles topology, ordered
per-target effects, query hooks, scoped condition stores, queued intents, item/ability/type/form
mutation, move references, snapshot overlays, damage memory, switch/state transfer, ruleset
policies, and deterministic event/trace output. Detailed sequencing and the 937/937 exit checklist
live in `IMPLEMENTATION_PLAN.md`; failure group handoff lives in `MOVE_AUDIT_SYSTEM_PLAN.md`.

### Feature-package specification gate

Before a Phase 15 implementation package edits Core, this spec must answer all applicable rows:

| Contract question | Required answer |
|---|---|
| Behavior shape | Existing op/condition/query/helper reused, or promotion-ladder proof for a new one |
| Data | Exact op/params/enums/defaults/ranges; unknown and incompatible values rejected |
| Scope | Source, selected target, per-target, party, slot, side, field, or ruleset ownership |
| Timing | Selection, ordering, before move, accuracy, hit, after damage, after move, switch, faint, or end turn |
| Failure | No-op versus visible failure, PP spend, target invalidation, immunity, faint/full-HP behavior |
| RNG | Draw kind, bounds, order, per-action/per-target/per-hit frequency, and skipped-draw cases |
| Math | Formula, integer/fraction width, rounding point, clamps, caps, zero/overflow handling |
| State | Mutation helper, duration/counter/stacking, source tracking, transfer, and cleanup |
| Output | Ordered `BattleEvent`s and diagnostic trace fields; AI-visible outcome |
| Evidence | Pure/compiler/resolver/boundary/golden/conformance tests required for completion |

A package is blocked when one of these answers is mechanically significant but unknown. Do not
bury an unanswered rule in code or infer it from a source move's name.

### Target topology contract (Phase 15B foundation)

`BattleTopology` defines immutable active slots in stable order: Player slots by ascending position,
then Enemy slots by ascending position. Only one- and two-slot topologies are valid. `BattleSlot`
is the stable `(side, position)` identity used by targeting, action selection, events, and later
replacement flow; it is not a party index.

`BattleTargetResolver.ResolveScope` is a pure resolver. Given a topology, source slot, authored
target shape, and a required explicit selection where applicable, it returns an ordered target
scope. It does not consult battle HP, mutate state, draw RNG, redirect, or apply effects. The
returned active-slot order is always topology order; `randomOpponent` returns the eligible opponent
slots together with the `RandomOpponent` selection policy, leaving the single RNG draw to the action
resolver.

The normalized target vocabulary is: `selected`, `user`, `allOpponents`,
`allOtherPokemon`, `usersField`, `entireField`, `allAllies`, `allPokemon`, `ally`,
`opponentsField`, `randomOpponent`, `selectedPokemonMeFirst`, `specificMove`,
`userAndAllies`, `userOrAlly`, and `faintingPokemon`.

- Active-slot targets resolve as follows: self; all opponents; all slots except self; all slots;
  allies except self; all own slots; or the explicitly selected valid slot. `ally` requires a
  selected non-source own slot. `userOrAlly` requires a selected own slot in doubles and resolves
  to self in singles. `selected` and `selectedPokemonMeFirst` require a caller selection, except
  that the legacy singles adapter supplies the opposing active slot.
- `usersField` and `opponentsField` resolve to a side scope; `entireField` resolves to the field.
- `randomOpponent` exposes all opposing active slots plus a random-selection policy. Exactly one
  draw occurs later, only after normal action legality and redirection filters are applied.
- `faintingPokemon` resolves to the user's fainted-party scope; a later action supplies the party
  member. `specificMove` resolves to a move-reference scope; a later move-reference resolver
  supplies the eligible move. Neither invents a party index or move choice at this layer.

The existing singles helper remains a compatibility adapter for already-implemented one-active
mechanics. It may resolve only the old one-creature targets; attempting to execute a newly added
topology-dependent target through that adapter fails loudly; the Phase 15B action resolver specified
below uses `ResolveScope`. This prevents a doubles-only target from silently acting on the opposing
single creature.

`BattleActiveSlots` owns the mutable slot-to-party assignment. Its topology is immutable; each
active slot is assigned exactly one non-negative party index, and a party member cannot occupy two
slots on the same side. Singles controllers initialize position zero for each side, preserving the
existing action API and event stream. A party index remains distinct from a battle slot, so later
doubles action collection, forced replacement, and slot conditions must address the slot first and
then use this mapping. This foundation adds no doubles action behavior or RNG draws.

`BattleTurnActions` collects exactly one submitted action for every active slot and normalizes the
collection to topology order. It rejects duplicate, missing, out-of-topology, and null actions
before action legality is evaluated. `BattleTurnOrder` then orders scheduled actions by priority
descending and effective speed descending. Equal priority/speed groups are shuffled in their stable
slot order using Fisher-Yates, drawing `Next(k)` for `k = groupCount` down to `2`; a two-action tie
therefore preserves the existing single `Next(2)` draw. It owns no legality, target selection, or
effect resolution. The singles controller uses this path now; the Phase 15B resolver specified
below schedules and executes all doubles submissions through the same contract.

### Phase 15B doubles execution contract

This section closes the execution decisions that the topology foundation intentionally deferred.
It is the authority for Phase 15B implementation. These are Creature Game Maker rules: when source
generations differ, this deterministic contract wins until a named `RulesetProfile` explicitly
overrides a policy. An implementation package must not choose a different behavior locally.

#### Controller state and turn admission

- A controller owns one immutable `BattleTopology`. Singles has one slot per side; doubles has two.
  Each battle-start slot must be assigned a distinct, non-fainted party member from its side. A side
  without enough eligible party members cannot start a doubles battle.
- Slot APIs are authoritative. Existing side-only APIs are singles adapters and must reject a
  doubles topology instead of assuming position zero.
- At the selection checkpoint every occupied, non-fainted slot submits exactly one action. Empty or
  fainted slots submit nothing. Before the next ordinary turn, the replacement checkpoint below
  must either fill every fillable slot or establish that the battle has ended.
- Admission is non-mutating and consumes no RNG. Validate structure first; preview due queue gates
  and form the effective submissions without consuming them; validate effective individual legality
  in topology order; then validate collective conflicts. If any effective action is invalid, reject
  the entire turn before state, PP, stock, queues, events, or RNG change. After successful admission,
  phase 1 consumes exactly the queue entries that were previewed and emits their skip events.
- Each admitted action captures `(sourceSlot, actorPartyIndex)`. A later creature never inherits an
  action merely because it occupies the same slot. Immediately before an action phase and again
  before move execution, an actor that is fainted, no longer assigned to its captured slot, or no
  longer owns the selected move invalidates that action. Emit `ActionInvalidated`; spend no PP or
  item stock and draw no RNG.

Collective validation rejects all of the following before mutation:

- two allied switches selecting the same reserve, any destination already active at selection, a
  switch to an invalid/fainted destination, or a switch submitted by a trapped/locked source;
- more than one temporary once-per-battle form activation for one side in the same turn;
- aggregate item requests exceeding the side's stock; and
- capture in a doubles topology. Capture remains a wild-singles action until a later explicit
  ruleset contract says otherwise; the move corpus does not require doubles capture.

#### Turn phases and simultaneous action conflicts

One admitted turn executes these checkpoints in order:

1. Consume due pre-action queue gates in topology order and replace blocked submissions with pass.
2. Resolve voluntary switches before all other actions. Order multiple switches by the captured
   actor's effective speed descending; shuffle equal-speed groups with the existing
   `BattleTurnOrder` Fisher-Yates rule. All destinations were reserved during collective validation.
3. Resolve capture, then battle items. Capture is singles-only. Order multiple item actions by
   effective speed descending with the same tie rule. Revalidate the target and stock immediately
   before use; a target made full-HP or otherwise ineligible by an earlier action invalidates the
   later action without consuming stock.
4. Apply admitted form activations in topology order. The captured actor must still occupy the
   source slot. Form changes precede move scheduling, so the activated form's effective speed is
   used for that turn's move order.
5. Schedule moves by priority descending and post-form effective speed descending. Shuffle only
   equal-priority/equal-speed groups using `BattleTurnOrder`. Revalidate captured actor identity
   immediately before each scheduled move.
6. After all scheduled moves, resolve end-turn condition hooks from the shared hook order.
7. Evaluate battle outcome for the full checkpoint, then enter replacement selection for surviving
   sides, then run switch-in hooks. Replacement-hook faints repeat replacement selection as needed.

Switches and items are deliberately separate action classes, so even the highest-priority move does
not precede them. A pass produces no event unless it replaced an action through a visible gate. PP
and move legality are not reserved across actions; they are revalidated at execution for effects
that may alter moves or PP later in Phase 15.

#### Typed selections and live target materialization

An action selection is a typed value, never an overloaded integer:

- `ActiveSlot(BattleSlot)` for selected active-creature scopes;
- `PartyMember(BattleSide, partyIndex)` for `faintingPokemon`; or
- `MoveReference(BattleSlot, moveIndex)` for `specificMove`.

The selection kind, side relationship, topology membership, party range, faint requirement, and
move range are validated at admission. `selected` accepts any non-source active slot;
`selectedPokemonMeFirst` accepts an opposing active slot; `ally` accepts a non-source own slot; and
`userOrAlly` accepts either own slot. `BattleTargetResolver.ResolveScope` remains the pure authored
shape resolver. At move execution the action resolver converts that scope into live targets in this
order: authored scope, valid live candidates, applicable redirection hooks, target policy, then
stable topology order. In a two-by-two topology every distinct active slot is adjacent; no hidden
distance rule exists.

Active-slot invalidation follows this table:

| Authored target | Execution-time behavior |
|---|---|
| `user` | The captured source slot; actor invalidation cancels the action. |
| `selected`, `selectedPokemonMeFirst` | The selected slot remains the target if its occupant changed. If an opposing selected slot is empty/fainted, choose the first live legal opposing slot in topology order. An empty own-side `selected` slot does not retarget. If no permitted target remains, target failure. |
| `ally`, `userOrAlly` | The selected own slot remains the target if its occupant changed. If empty/fainted, do not retarget; target failure. |
| `randomOpponent` | Build live legal opponents after redirection filters. Draw `Next(count)` once only when `count > 1`; use the only candidate without a draw; target failure at zero. |
| spread active scopes | Recompute all live eligible slots at execution and preserve topology order. Empty/fainted slots are omitted; zero targets is target failure. |
| side/field scope | Resolve once for that side/field, never once per active creature. |
| fainted-party/move-reference scope | Resolve the admitted typed selection; no active-slot fallback or random choice. |

Selected targets are slot-stable, not occupant-stable: a voluntary or forced replacement occupying
the selected slot can receive the action. The only automatic fallback is the selected-opponent rule
above. It is deterministic and draws no RNG. A ruleset that wants random fallback must define that
as a target policy and its extra draw explicitly.

Target failure occurs after actor action gates but after the move is announced and its PP is spent;
emit `MoveUsed` followed by `MoveFailed(TargetUnavailable)`. Admission-time malformed selections,
actor invalidation, and pre-move action gates spend no PP and emit no `MoveUsed`.

Redirection applies only to a redirectable authored single-opponent scope, including
`randomOpponent`; an eligible redirector fixes the target and suppresses the random-target draw. It
never redirects self, ally, party, side, field, spread, or move-reference scopes. Eligible redirect conditions are ordered
by explicit redirect priority descending, then condition owner's effective speed descending, then
topology order; exact ties use topology order and draw no RNG. The first eligible hook wins. A
redirection hook must declare the move tags/classes it accepts and its bypass tags. Position-swap
effects exchange the two party assignments atomically after legality succeeds; queued slot effects
stay on their slots and creature-owned effects travel with their creatures.

#### PP, RNG, hit, effect, and event order

A move action has one action context and an ordered target context for every materialized target.
The action context owns source, move, PP payment, shared hit-count result, action-total damage, and
action-scoped effects. A target context owns target identity, accuracy/immune/hit results,
per-hit damage, target-total damage, and target-scoped effects. Side/field effects use one scoped
context and are never multiplied by the number of active slots.

The exact resolver order is:

1. Revalidate actor identity, move index, lock state, and PP; then run the existing source action-gate
   and status/volatile action checks in their specified order. Their source-level RNG draws occur
   here. A blocked actor stops without PP, target, accuracy, hit-count, or effect draws.
2. Spend one PP and emit one slot-aware `MoveUsed`.
3. Materialize live targets and redirection. Random targeting draws here. Zero targets emits the
   target failure and stops.
4. Snapshot the ordered direct targets and whether spread reduction applies. It applies when at
   least two live active-creature targets were materialized, even if one later misses or is immune.
5. Roll accuracy once for each target in topology order and retain the success set. If every target
   misses, stop without hit-count, critical, damage, or secondary draws.
6. Determine the action-scoped hit count once when at least one accurate target remains and the move
   has the standard multi-hit wrapper. The same intended count applies to every accurate target. A
   future per-target hit-count policy must be an explicit typed parameter, not an implementation
   accident.
7. For each accurate target in topology order, check move/effect immunity once. An immune target emits
   its immunity result and receives no hit RNG. Otherwise resolve hits in ascending hit number: each
   hit rolls critical then the damage random factor, applies damage, records memory, and emits damage/
   faint events. Stop remaining hits for that target when it faints; continue the snapshotted direct
   damage against later targets even if an earlier contact/recoil consequence will eventually faint
   the source.
8. After direct damage for all targets, resolve target-scoped non-damage effects and contact
   consequences in topology order, each effect in compiled order. A standard secondary chance is
   rolled once per eligible target after that target's damage, not once for the whole spread action.
9. Resolve action-scoped self, aggregate drain/recoil/crash/cost, field, and side effects once in
   compiled order. Action-total damage is the sum of actual HP removed from all targets/hits.
10. Emit action completion trace, evaluate the post-action outcome checkpoint, then continue or end.

No RNG call is made for a deterministic result, a singleton random candidate, an always-hit move,
an invalidated action, an immune effect whose chance is not consulted, or hits after a target faints.
The deterministic trace records each performed draw in the order above with action, target, hit,
bound, and result. Standard spread damage applies the `3/4` target modifier at the damage formula's
Targets step with integer floor, independently for every damaging hit. One remaining live target
receives no spread reduction.

Target-scoped effects read that target's damage result. Action-scoped damage-derived effects read
action-total damage. When a mechanic requires per-target rounding before aggregation, its typed
effect parameter must say so; the resolver must not infer it from a move identity. Recoil and other
source costs execute once after all direct targets. Effects that cannot affect a fainted target are
skipped visibly in trace without a chance draw.

#### Slot-aware events and action identity

Every event about an active creature carries `BattleSlot`; `BattleSide` may remain as a derived
compatibility property. This includes move use/miss/failure, action invalidation, charging/status
action failures, damage/healing, status/stage/volatile changes, form changes, faint, switch-in/out,
and contact consequences. Side/field events remain side/field scoped. Party-target item and
replacement events carry both slot where applicable and party index.

The minimum new failure identity is:

- `ActionInvalidated(sourceSlot, reason)` with `ActorChanged`, `ActorFainted`, `MoveChanged`,
  `ResourceChanged`, or `TargetStateChanged`;
- `MoveFailed(sourceSlot, move, reason)` with `TargetUnavailable` in addition to existing reasons;
  and
- `ReplacementRequested(slot)` for every fillable empty slot.

Event order is authoritative and replayable: turn phase order, scheduled action order, target
topology order, hit number, compiled effect order. `MoveUsed` occurs once; target miss/immunity/
damage/faint events follow for each target; target effects follow direct damage; source/side/field
effects follow target effects. The singles adapter constructs position-zero slots, so singles and
doubles share event types rather than maintaining two event streams.

#### Faint, outcome, and replacement checkpoint

A faint empties the slot logically for later targeting but does not insert a reserve during the
current move or between scheduled moves. Actions captured for that fainted actor invalidate when
reached. Outcome is checked after each complete action and after the complete end-turn hook batch:

- if exactly one side has no non-fainted party member, that side loses and remaining actions stop;
- if both sides have no non-fainted party member at the same checkpoint, the result is a draw; and
- a side with healthy reserves is not defeated merely because all active slots are empty.

`BattleOutcome` therefore represents either a winner or draw; it must not manufacture a winner for
a simultaneous wipe. End-turn hooks finish their deterministic batch before this check so a shared
residual checkpoint can produce a draw.

For every surviving side with empty slots and healthy non-active party members, the controller emits
`ReplacementRequested` in topology order and waits for one typed party choice per fillable slot.
Replacement admission is atomic: choices must be in range, non-fainted, non-active, and unique for
that side. Apply accepted replacements in topology order and run each switch-in hook once. If an
entry hook immediately faints a replacement, finish that hook batch, check outcome, and request
another replacement for the slot when a reserve remains. An unfillable slot stays empty. No ordinary
turn begins while a fillable slot awaits replacement.

Voluntary switch, forced switch, pivot, and faint replacement must ultimately use the same
slot-addressed switch helper, but Phase 15G adds their transfer and cancellation policies. Phase 15B
implements the replacement checkpoint and the neutral no-transfer path; it must not guess the later
passable-state whitelist.

### Queued action gates (Phase 15 move-handler family)

Three generic effect operations govern action timing without any move identity in Core:

- `moveGate` has required `{ kind: "firstAction"|"notPreviousMove" }`. It is a pre-move gate,
  draws no RNG, and cannot be chance-gated. `firstAction` permits only the creature's first move
  use after entering battle; `notPreviousMove` rejects a move whose ID equals that creature's last
  used move. Failure spends no PP, performs no accuracy/damage/effect work, and emits `MoveFailed`
  with the gate reason.
- `queueActionGate` has optional `{ turns: positive int }` (default 1). It is a post-effect action
  queue entry for the source slot, draws no RNG, and cannot be chance-gated. At the start of the
  due turn it replaces that slot's submitted action with a generic pass, emits `ActionSkipped`, and
  consumes the entry. It therefore blocks switching, items, forms, and move use uniformly.

The queue is ordered by due turn then insertion order. This first handler family uses only its
source-slot action gate, but later delayed damage, forced execution, and target-action gates extend
the same deterministic queue rather than adding per-move booleans. Switch-out clears a creature's
first-action and last-used-move gate state; queue entries are later given explicit switch cleanup
policies as their mechanics require.

### Event trace contract

`BattleEvent` remains the stable presentation-facing statement of what happened. Phase 15 also
requires a deterministic internal effect trace so conformance failures explain how the engine
arrived there without exposing UI concerns.

Each trace entry conceptually records:

- turn and action sequence;
- source side/slot and target side/slot or side/field scope;
- normalized effect/condition/query identifier (behavior ID, never move name);
- hook/timing point;
- gate/filter result;
- RNG draw kind, bound/result, and draw sequence when a draw occurs;
- before/base/modified/final numeric values for queries and formulas;
- state mutation summary; and
- emitted event index range.

Trace ordering follows actual resolution ordering and uses stable numeric/reference IDs. Traces do
not contain timestamps, memory addresses, filesystem paths, localized display text, or official
content names. A trace is diagnostic evidence; it never drives simulation or presentation.

Phase 15A locks this conceptual shape and the corpus manifest. The concrete Core trace types land
with the first resolver slice that needs them, rather than adding unused abstraction now.

## Outline (remaining, per battle layer v0-v6)
State · Turn flow · Damage · Type · Status · Stages · Capture · Effect-op interpreter · AI · Events.
