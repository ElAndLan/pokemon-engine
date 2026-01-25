# Advanced Battle AI Specification (Expanded) — Revised

## 1. Purpose & Scope

*   **Objective**: Build an “Oppressive but Fair” Battle AI that makes optimal decisions via a **Utility-Based Scoring System** with lookahead, using complete-information assumptions about the opponent’s stats/moves to drive strategic pressure.
*   **Output**: The AI selects actions (Move, Switch, Item, etc.) by maximizing a well-defined score derived from a transparent set of components (Damage, Status, Setup, Tactics, Risk).

---

## 2. Architecture & Modules (Clear Interfaces)

### **StateReader**
*   Produces a **BattleState** snapshot that is immutable for evaluation and safe for repeated simulations.
*   **Encapsulates**: HP, status conditions, stat stages, speed, field effects (weather/terrain), active/benched Pokemon, remaining PP, etc.

### **Simulator**
*   Given a `BattleState` and an `Action`, returns a **new** `BattleState` representing the predicted outcome (without mutating the current state).
*   **Applies**: Move resolution, status changes, stat changes, switch outcomes, end-of-turn effects (recoil, damage over time, weather/terrain increments).

### **Scorer (Evaluator)**
*   **Input**: `BattleState`
*   **Output**: Numeric **utility score** representing the desirability of the resulting state from the AI’s perspective.
*   **Components**: `DamageScore`, `StatusScore`, `StatScore`, `TacticalBonus`, `RiskPenalty` (formulas below).

### **ActionPicker**
*   Generates candidate actions (all usable moves, potential switches, and allowed items).
*   For each candidate: runs `Simulator.run(state, action)` then `Scorer.score(simulatedState)`.
*   Returns the `Action` with the highest score (tie-breakers documented below).

---

## 3. Core Mechanics: Stats, Types, and Damage

### 3.1 Stat Stage Multipliers
Table (Stage $s \in [-6, +6]$):

| Stage | Multiplier | Stage | Multiplier |
| :--- | :--- | :--- | :--- |
| -6 | 0.25 | +1 | 1.50 |
| -5 | 0.28 | +2 | 2.00 |
| -4 | 0.33 | +3 | 2.50 |
| -3 | 0.40 | +4 | 3.00 |
| -2 | 0.50 | +5 | 3.50 |
| -1 | 0.66 | +6 | 4.00 |
| 0 | 1.00 | | |

*Use per-stat multipliers for Attack, Defense, SpA, SpD, Speed, and Accuracy/Evasion as applicable.*

### 3.2 Damage Formula (Consistent, Deterministic Baseline)

```math
Damage = floor( floor( floor( ((2 * Level / 5) + 2) * Power * A / D) / 50 ) + 2 ) * Modifier
```

Where:
*   **A** = Attacker’s effective stat (physical: Attack; special: SpA) after applying stat-stage multiplier.
*   **D** = Defender’s effective stat (physical: Defense; special: SpD) after applying stat-stage multiplier.
*   **Power** = `Move.bp` (base power)
*   **Modifier** = `STAB * Type * Crit * Random(0.85–1.00) * Field * Weather * OtherEffects`
    *   **STAB** = 1.5 if `Move.type` matches one of `Attacker.types`; else 1.0
    *   **Type** = Product of TypeEffectiveness against each of Defender's types.
    *   **Crit** = 1.0 (can be extended to 1.5 or 2.0 with a defined crit mechanism).
    *   **Random** = Random in [0.85, 1.0]; toggle off for testing.

> **Note**: Leverage a cap on overkill logic to improve scoring stability (see 4.1 DamageScore).

---

## 4. Scoring Framework (The Utility Function)

`TotalScore(Action) = DamageScore + StatusScore + StatScore + TacticalBonus - RiskPenalty`

### 4.1 DamageScore (Primary Pressure Metric)
1.  **PredictedDamage** = Damage (from formula, after modifiers).
2.  **DamagePercent** = `PredictedDamage / TargetCurrentHP`.
3.  **Base Score** = `DamagePercent * 100`.
4.  **KillShotBonus**: If `PredictedDamage >= TargetCurrentHP` → **+1000**.
5.  **AccuracyWeight**: Multiply by `(move.accuracy / 100)`.
6.  **Overkill Handling**: Cap `PredictedDamage` to `TargetCurrentHP` when computing base (avoids inflated scores).

### 4.2 StatusScore (Value of Status)
*Only apply when the target has no existing status.*

*   **Freeze / Sleep**: **+60** (Crippling, removal of turns).
*   **Burn**: **+40** (Reduces Atk, acts as residual dmg).
*   **Paralysis**: **+40** (Speed control).
*   **Poison**: **+20** (Continuous pressure).
*   **Self-Cure**: **+20** if move cures own status.

*If status delays key KO opportunities, apply moderation.*

### 4.3 StatScore (Setup & Debuffing)
**Buff Self** (e.g. Swords Dance):
*   **Base**: **+30** per stage.
*   **Condition**: AI HP > 70% → Multiplier/Extra Weight.
*   **Penalty**: AI HP < 40% → **-50** (Risky setup).

**Debuff Enemy** (e.g. Growl):
*   **Base**: **+15** per stage.
*   **Synergy**: Higher weight if debuff turns 3HKO into 2HKO.

### 4.4 TacticalBonus
*   **Priority**: **+500** if Priority Move (Quick Attack) results in a Kill on a faster opponent.
*   **Field Synergy**: **+30–60** for Weather/Terrain that benefits team.
*   **Force Switch**: **+40–80** if forcing opponent into poor matchup.

### 4.5 RiskPenalty
*   **Accuracy**: Non-linear penalty/exclusion for < 80% accuracy unless sole kill option.
*   **Recoil**: Penalty if recoil pushes AI into "One Shot" range.
*   **Predictability**: Small penalty for repetitive moves to encourage variety.

---

## 5. Implementation Roadmap

### Phase 2.1: Foundation (Core Math)
1.  **Refactor `PokemonInstance`**: Support Stat Stage multipliers (-6 to +6).
2.  **Implement `DamageCalculator`**: Use the exact Damage Formula above.
3.  **Build `BattleState`**: Snapshot interface/class for safe cloning.

### Phase 2.2: The Brain (Evaluator)
1.  **Implement `BattleAI`**: `getBestAction(state): Action`.
2.  **Simulation Loop**:
    *   `Simulator.run(state, action)` → `simulatedState`.
    *   `Scorer.score(simulatedState)` → `score`.
    *   Track best action.

### Phase 2.3: Sophistication (Status, Prediction, & Risk)
1.  Add `StatusScore` weights and conditional checks.
2.  Add `Accuracy/Risk` weights.
3.  Implement **Kill-Confirm** logic.

### Phase 2.4: Integration
1.  Hook `BattleAI` into `BattleScene.executeEnemyTurn`.
2.  Add **Thinking Time** (delay) for UX.

---

## 6. Testing & Validation

### 6.1 Validation Criteria
*   **Scenario A (Typing)**: Opponent has Water move; AI must respect Water resistance/weakness logic.
*   **Scenario B (Kill vs Risk)**: AI chooses 100% Acc move (Kill) over 70% Acc move (Overkill).
*   **Scenario C (Status vs Damage)**: AI burns a Physical Sweeper instead of dealing chip damage.
*   **Scenario D (Setup)**: AI Sets up (+Atk) when HP is high and KO is not immediate.

### 6.2 Edge Cases
*   **Sleep**: Account for wake-up probability.
*   **Multi-Hit**: Aggregate damage correctly.
*   **Switching**: Weigh switch cost (turn loss) against matchup gain.

---

## 7. Data Models & API

### API Boundaries
```typescript
StateReader: getBattleState() -> BattleStateSnapshot
Simulator.run(state, action) -> BattleState
Scorer.score(state) -> number
ActionPicker.getBestAction(state) -> Action
```

### Action Structure
```typescript
interface Action {
    type: 'move' | 'switch' | 'item';
    actor: string; // ID
    target?: string;
    move?: MoveData;
    switchTo?: string;
    score?: number;
    debugLogs?: string[]; // Breakdown of score components
}
```

---

## 8. Item Usage (AI Logic)
The AI evaluates items if it is part of a "Trainer" encounter. Wild Pokemon never use items.
*   **Evaluation**: The AI simulates using Medicine (Healing/Status) using the `ItemHandler`.
*   **Tactical Scoring**:
    *   **Healing Score**: Increases based on the % of HP restored, capped at the Pokemon's Max HP.
    *   **Status Cure Score**: High priority if the status prevents the Pokemon from attacking (e.g., Sleep/Freeze) or crippling its output (e.g., Burn on physical attacker).
    *   **Priority**: The AI defaults to a "Kill Priority" — it will choose an available KO over healing unless the healing is critical for survival against a faster opponent.

---

## 9. Example Scenarios (Expanded)

### Scenario A: The Finisher
*   **AI**: Jolteon (10 HP, Fast)
*   **Player**: Blastoise (15 HP)
*   **Moves**:
    *   `Thunder` (110 BP, 70% Acc) → Dmg 40 (Lethal). Score: 186.
    *   `Thunderbolt` (90 BP, 100% Acc) → Dmg 35 (Lethal). Score: 233 (No risk penalty).
    *   `Quick Attack` (40 BP, 100% Acc) → Dmg 10. Score: 66.
*   **Decision**: **Thunderbolt**. It guarantees the win without the risk of missing.

### Scenario B: The Wall
*   **AI**: Weezing (High Def) vs **Player**: Machamp (High Atk)
*   **Moves**: `Sludge Bomb` vs `Will-O-Wisp` (Burn).
*   **Decision**: **Will-O-Wisp**. Crippling the physical attacker (Burn = 0.5x Atk) is valued higher than chip damage.

### Scenario C: The Setup
*   **AI**: Dragonite (100% HP)
*   **Moves**: `Dragon Claw` vs `Dragon Dance`.
*   **Decision**: **Dragon Dance**. High HP enables safe setup, unlocking sweep potential.
