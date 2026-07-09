---
name: cgm-smart-ai
description: Implement, tune, or review Creature Game Maker trainer battle AI, SmartAi scoring, TrainerAi profile dispatch, AI memory, switch/item/move choice, score-table debug output, seeded AI-vs-AI tests, and Phase 14 difficulty tuning. Use before editing BATTLE_AI_SPEC.md, SmartAi.cs, TrainerAi.cs, AI battle tests, difficulty harnesses, or battle controller APIs added for AI context.
---

# CGM Smart AI

## Overview

Use this skill for Phase 14 Smart AI work. The AI must be strong enough to tune, deterministic enough to test, and fair enough that it never acts like it read the player's selected action.

Run `cgm-scope-gate` first. Use `cgm-battle-effect-op` too if the AI change depends on new or changed move-effect behavior.

## Required Reads

1. `docs/SCOPE_GUARD.md`
2. `docs/BATTLE_AI_SPEC.md`
3. `docs/BATTLE_SYSTEM_SPEC.md` Smart AI and event/effect sections
4. `docs/IMPLEMENTATION_PLAN.md` Phase 14 status
5. `src/Cgm.Core/Battle/SmartAi.cs`
6. `src/Cgm.Core/Battle/TrainerAi.cs`
7. `src/Cgm.Core/Battle/BattleController.cs` only for legal action/context APIs
8. `tests/Cgm.Core.Tests/Battle/SmartAiTests.cs`
9. `tests/Cgm.Core.Tests/Battle/AiBattleSimTests.cs`
10. `tests/Cgm.Core.Tests/Battle/AiDifficultyTests.cs`

## AI Contract

The AI may:

- Read its own active creature, party, moves, PP, status, stages, and finite item stock.
- Read visible player active information: HP, status, stages, types, current field state.
- Use explicit memory such as seen player moves and repeated patterns.
- Use injected `IRng` for tie-breaks/noise.
- Return a legal `BattleAction`.
- Expose named score components for every non-random decision.

The AI must not:

- Read the player's selected action this turn.
- Read future RNG.
- Mutate battle state directly.
- Inspect unrevealed player moves/items unless a later explicit party-preview/open-sheet rule exists.
- Add Expert/open-team-sheet schema or UI during Phase 14.
- Bypass `BattleController` validation.

## Implementation Workflow

1. State the capability being changed: move scoring, switch scoring, item use, memory, prediction, dispatch, or tuning.
2. Confirm it is Phase 14 scope. If it touches abilities, held items as passive hooks, forms, doubles, or Expert open-sheet logic, stop.
3. Add or adjust named score components, not opaque arithmetic.
4. Keep action generation legal:
   - only moves with PP
   - switch only to non-fainted reserves
   - no voluntary switch while trapped, charging, or locked
   - finite item stock and valid healing targets
5. Keep randomness injected and seed-replayable.
6. Update tests before tuning numbers unless the change is pure data/weight tuning.
7. For tuning, measure before and after with the seeded harness and state the sample size.
8. Prefer fixing the real scoring term over raising noise or adding one-off exceptions.

## Score Component Rules

Each meaningful factor should be visible in `AiCandidateScore.Components`:

- `damage`
- `ko`
- `status`
- `setup`
- `hazard`
- `protect`
- `forceSwitch`
- `recovery`
- `itemHeal`
- `recoilRisk`
- `selfKoRisk`
- `switchTempo`
- `incomingDamage`
- `damageAvoided`
- `switchInHazard`
- `prediction`, only when prediction is implemented
- `noise`, only from injected RNG

Names are part of the debug/tuning surface. Do not rename casually.

## Tuning Rules

- Use seeded simulations. Never eyeball one battle and call it tuned.
- Keep `Random` and `Basic` behavior intact unless the task explicitly targets them.
- Tune against meaningful benchmark teams, not only identical simple attackers.
- State old vs new metric, seeds, and team setup.
- If a weight change only hides a model defect, prefer the model fix.
- Respect the spec's target: Advanced should be challenging but not perfect.

Current known tuning context from docs:

- Noise was reduced from 0.10 to 0.05.
- `SwitchThreshold` was raised from 35 to 50 because switch scoring overvalued marginal switches.
- Hazard-aware switch-in damage has been plumbed into context.
- Remaining Phase 14 work includes deeper switch-value formula/prediction, review, and display/debug integration.

## Required Tests

For new behavior, add the narrowest tests that would fail if the AI regresses:

- Unit test for each decision category and score component.
- Legal-action test for the edge case: no PP, trapped, fainted reserve, full HP item target, no item stock.
- Memory test when seen/repeated player move state matters.
- Seeded determinism test for new randomness.
- AI-vs-AI termination test if the change affects action selection broadly.
- Difficulty harness update for tuning changes.

Run:

```powershell
D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --filter "SmartAi|TrainerAi|Ai"
```

If battle controller APIs or shared battle behavior changed, also run:

```powershell
D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --filter Battle
```

## Completion Report

Report:

- Capability changed.
- Score components added/changed.
- Fairness boundary checked.
- Seeded metrics before/after, if tuning.
- Tests run.
- Remaining excluded work, especially prediction, Expert/open-sheet, UI/debug integration, or Phase 15 systems.
