# BATTLE_DAMAGE_CALC

Status: **Reference-frozen v1 (2026-07-06).** The exact, testable damage pipeline. Target
fidelity: **Gen III/IV**. This is the formula appendix that BATTLE_SYSTEM_SPEC.md points to;
Phase 8 (Battle v0–v1) implements it and cross-checks every worked example below against two
independent reference calculators before the goldens are committed. Lives in Cgm.Core, headless,
deterministic (injected `IRng`).

Scope: one damaging hit of one move, attacker → one defender, 1v1. Status moves (`power: null`)
do no damage and skip this entirely. Type chart data comes from `type:*` entities (DATA_SCHEMA §4.2).

---

## 1. Pipeline overview
```
if move.damageClass == status: no damage.
if effectiveness == 0:         damage = 0 (immune) — stop, emit DamageDealt{0, immune}.
else:
  base = baseDamage(level, power, A, D)
  dmg  = applyModifiers(base)      # sequential, floor after each step (§4)
  dmg  = max(1, dmg)               # never less than 1 when not immune
```

## 2. Base damage
```
base = floor( floor( floor(2 * Level / 5 + 2) * Power * A / D ) / 50 ) + 2
```
- `Level` = attacker level. `Power` = move.power.
- `A` / `D` selection by `move.damageClass`:
  - physical → A = attacker Attack (`atk`), D = defender Defense (`def`)
  - special  → A = attacker Sp.Atk (`spa`), D = defender Sp.Def (`spd`)
- `A` and `D` are **stat-stage-modified** (§7), except under the crit rule (§6).
- Every division floors. Integer math throughout.

## 3. Stats going in (recap)
Battle stats are computed once on send-in from species base + IV + EV + nature (formulas in
BATTLE_SYSTEM_SPEC.md), then stage multipliers apply per §7. HP is not used in the damage
formula (only as the pool damage subtracts from).

## 4. Modifier sequence (Gen IV order — floor after each multiply)
Applied to `base` in this exact order; each line floors its result:
1. **Targets** — ×0.75 if the move hit multiple targets (doubles). MVP is 1v1 → ×1 (skip).
2. **Weather** — ×1.5 / ×0.5 for boosted/weakened type (rain↔water/fire, sun↔fire/water, etc.).
   No weather in MVP → ×1 (slot reserved; Battle v5/v6 fills it).
3. **Critical** — ×2 on a crit (§6), else ×1.
4. **Random** — `floor(dmg * randInt(85,100) / 100)`. One `IRng` draw; the only randomness here.
5. **STAB** — ×1.5 if `move.type` ∈ attacker's `types`, else ×1 (§5).
6. **Type effectiveness** — ×`typeMultiplier` (§5), the product over the defender's types.
7. **Burn** — ×0.5 if the attacker is burned **and** the move is physical, else ×1.
8. **Other** — screens (Reflect/Light Screen ×0.5), items, abilities. All ×1 in MVP (slot for later).

> The order is the spec. Changing it changes goldens — never reorder without a BATTLE_SYSTEM_SPEC
> edit and a documented golden refresh.

## 5. Type effectiveness & STAB
**Per-type multiplier** for the move's type against one defender type, from the `type:*` data:
- defender type ∈ move.type `noDamageTo`  → **0** (immunity; short-circuits the whole hit)
- defender type ∈ move.type `doubleDamageTo` → **2**
- defender type ∈ move.type `halfDamageTo`   → **0.5**
- otherwise → **1**

**Dual-type:** multiply the two per-type multipliers. Possible results: `0, 0.25, 0.5, 1, 2, 4`.
Example: Fire vs (Grass, Steel) = 2 × 2 = 4 (double weak). Ground vs (Flying, anything) = 0 (immune).

**STAB** (Same-Type Attack Bonus): ×1.5 when the move's type matches one of the attacker's own
types. (Adaptability-style ×2 is an ability → Battle v6.)

The effectiveness value is emitted on the event (`DamageDealt.effectiveness`) so the UI can print
"It's super effective!" / "not very effective…" / "It doesn't affect …" — the UI reads it, it
does not compute it.

## 6. Critical hits
- **Probability by crit stage** (Gen III/IV table): stage 0 → 1/16, 1 → 1/8, 2 → 1/4, 3 → 1/3,
  ≥4 → 1/2. Move base stage = `move.critStage`; boosts (focus-energy, high-crit moves) add stages.
- **Multiplier:** ×2 (Gen III/IV). (Gen VI's ×1.5 is not our target.)
- **Stage-ignore rule:** a crit ignores stat-stage changes that would *reduce* the hit — it drops
  the attacker's **negative** offensive stages and the defender's **positive** defensive stages
  (uses the unmodified stat for those), keeping the ones that help the attacker.
- One `IRng` draw for the crit check, taken before the random-roll draw. Draw order is fixed for
  determinism.
- A live Phase 15C-7 next-critical condition replaces the chance with one after immunity and, when
  no later guard suppresses it, consumes at that query and resolves critical without a crit draw;
  the damage-roll draw remains next. This is the only no-draw critical path.
- The Phase 15E side critical guard contributes a target-side `CriticalChance` clamp to zero
  after the stage-derived chance is authored and before the per-hit critical draw. The draw is not
  skipped. A guarded hit is noncritical for the damage multiplier, stage-ignore rule, screen
  eligibility, events, damage memory, and traces. Formula-bypassing damage performs no critical
  query or draw.

## 7. Stat-stage multipliers (offensive/defensive)
Stages clamp to −6..+6. For Attack/Defense/Sp.Atk/Sp.Def used in damage:
| stage | −6 | −5 | −4 | −3 | −2 | −1 | 0 | +1 | +2 | +3 | +4 | +5 | +6 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ×  | 2/8 | 2/7 | 2/6 | 2/5 | 2/4 | 2/3 | 1 | 3/2 | 4/2 | 5/2 | 6/2 | 7/2 | 8/2 |
i.e. positive = `(2+stage)/2`, negative = `2/(2+|stage|)`. Applied to the raw stat, floored.
(Accuracy/Evasion use a different table — see BATTLE_SYSTEM_SPEC.md; they affect hit chance, not
this formula.)

## 8. Worked examples (to become golden test values)
Numbers below are illustrative and must be re-verified against reference calculators in Phase 8;
once verified they become committed goldens.

1. **Ember (Fire, 40 pow, special) vs a Grass/Poison target.**
   Fire→Grass = 2, Fire→Poison = 1 → effectiveness 2. STAB if attacker is Fire-type.
   `base → ×random(0.85..1) → ×1.5 STAB → ×2 type`. Expect a clean super-effective range.
2. **Physical move, attacker burned.** Same base, final ×0.5 at step 7 → roughly half.
3. **Ground move vs a Flying defender.** effectiveness 0 → damage 0, `DamageDealt{0, immune}`,
   no min-1 floor. Confirms the immunity short-circuit.

## 9. Determinism & test hooks
- Fixed `IRng` draw order per hit: **crit check → damage random roll → secondary-effect rolls**
  (secondary effects handled by the move resolver, not here).
- The Phase 15E paired-action chance row multiplies each outgoing damaging-move secondary chance by
  `2/1` through `SecondaryChance` and clamps at 100 without adding or removing that effect's one
  ordinary draw. Its end-turn side residual is non-move HP loss, not standard damage: it uses `Sap`
  for `max(1, floor(maxHp / 8))`, emits `ResidualDamage`, and skips an effective Fire type.
- Pure function: `computeDamage(attacker, defender, move, field, rng) → DamageResult{amount,
  effectiveness, crit}`. No state mutation; the resolver applies the result to HP.
- Tests: a table of ≥40 hand-verified `(level, power, A, D, types, stages, crit, burn) → amount`
  rows (TESTING_STRATEGY.md), plus per-hit golden replays that pin the exact numbers for a seed.
