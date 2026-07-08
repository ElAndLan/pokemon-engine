# BATTLE_SYSTEM_SPEC

Status: **Stub** — current source is `MASTER_PLAN.md` §8 and `ARCHITECTURE_ADDENDUM.md` §8
(battle layers v0–v6). The **formula appendix must be written and calculator-cross-checked
BEFORE Phase 8 code**; the effect-op catalog grows per battle layer. Blocks: Phases 8–11, 14, 15.

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

**Migration status (in progress).** The engine currently compiles `Move.Effects` → typed `BattleMove`
fields and resolves them; the shared *primitives* already exist (`DamageCalc`, `ChangeStage`,
`Heal`/`Sap`/`DrainLife`, `EffectMath`) and no move has bespoke code. The migration converts resolution
to be **effect-list-driven with a primitive dispatcher** (`EffectContext` + `MoveEffect` records +
`ApplyEffect`), then converts statuses/volatiles to `ConditionDef`s with hooks, in tested chunks that
preserve determinism (RNG draw order) at each step. Layer targets follow catalog §12 (v0–v6).

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

The remaining v5 ops (chargeTurn, protect, hazards, weather, forceSwitchOut, leechSeed, bind,
multiTurnLock, counterDamage, accuracyBypass) are stateful and land with the interpreter in their own
spec'd batches; the palette stays **closed** — new ops require editing this section.

## Outline (remaining, per battle layer v0–v6)
State · Turn flow · Damage · Type · Status · Stages · Capture · Effect-op interpreter · AI · Events.
