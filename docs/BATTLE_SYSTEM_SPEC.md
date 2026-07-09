# BATTLE_SYSTEM_SPEC

Status: **Partial / implemented sections are binding.** Damage formula lives in
`BATTLE_DAMAGE_CALC.md`; battle v0–v5 Core mechanics and the v5 effect-op palette are implemented
through the shared effect dispatcher. This document is still incomplete as a full battle spec:
event catalog, golden workflow, and final smart-AI scoring contract still need tightening before
1.0. Phase 14 smart AI is accepted as a verified Core baseline; full tuning is deferred until
Phase 15+ mechanics exist.

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
covered by the condition/effect machinery. No move should have bespoke resolver code.

## Effect-op numeric formulas (Battle v5, Phase 14)

The closed op palette lives on `Move.Effects` (`{ op, chance?, params }`). Ops split into pure numeric
math (here, in `EffectMath` — the same pure-helper category as DamageCalc/CaptureCalc) and stateful
resolution (interpreter wiring into `BattleController`, a later chunk). This section locks the math so
the ops and their tests are unambiguous. All damage/heal amounts are **≥1** unless stated.

- **multiHit** — hits 2–5 times, Gen III/IV distribution: 2→3/8, 3→3/8, 4→1/8, 5→1/8. A fixed-N variant
  hits exactly N (params `{ min, max }` equal → fixed). Each hit rolls damage (and crit) independently.
- **drain** — user heals `max(1, floor(damageDealt × num/den))` (default ½). No heal if 0 damage dealt.
- **recoil** — user takes `max(1, floor(damageDealt × num/den))` (¼ or ⅓). `crashOnMiss`: on a **miss**,
  the user instead takes `floor(maxHp × num/den)` crash damage (Gen IV: ½ maxHp), independent of drain.
- **fixedDamage** — ignores the damage formula: `flat` deals exactly `params.amount`; `levelBased` deals
  the user's level (Night Shade / Seismic Toss). Type immunity still applies (checked by the resolver).
- **ohko** — one-hit KO: accuracy = `userLevel − targetLevel + 30`, and the move **fails outright** if
  `targetLevel > userLevel` (accuracy 0). On hit, damage = target's current HP.
- **healFraction** — user heals `max(1, floor(maxHp × num/den))` (default ½). (Weather-scaled variant is
  a later slot; v5 is the flat fraction.)

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

## Outline (remaining, per battle layer v0-v6)
State · Turn flow · Damage · Type · Status · Stages · Capture · Effect-op interpreter · AI · Events.
