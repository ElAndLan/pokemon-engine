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

## Outline (to be written, per battle layer v0–v6)
State · Turn flow · Damage · Type · Status · Stages · Capture · Effect ops · AI · Events.
