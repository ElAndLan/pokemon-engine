---
name: cgm-battle-effect-op
description: Implement or review Creature Game Maker battle move effects, statuses, hazards, weather, volatile conditions, move compilation, and BattleController resolution as data-driven effect ops. Use before editing BATTLE_SYSTEM_SPEC.md, EFFECT_TYPES_CATALOG_v0_5.md, Move.Effects schema, MoveCompiler, MoveEffects, EffectMath, EffectContext, BattleController.ApplyEffect, BattleEvents, or tests under tests/Cgm.Core.Tests/Battle.
---

# CGM Battle Effect Op

## Overview

Use this skill to keep battle mechanics data-driven. The project's named failure mode is implementing specific moves as bespoke code; this skill forces changes through the closed effect-op palette and shared resolver primitives.

Run `cgm-scope-gate` first. If the effect belongs to Battle v6 or later, stop.

## Required Reads

1. `docs/SCOPE_GUARD.md`
2. `docs/ARCHITECTURE_ADDENDUM.md` section 8, battle v0-v6 layers
3. `docs/BATTLE_SYSTEM_SPEC.md`
4. `docs/EFFECT_TYPES_CATALOG_v0_5.md`
5. `docs/BATTLE_DAMAGE_CALC.md` for damage formula or rounding changes
6. `src/Cgm.Core/Model/Entities/Move.cs` when serialized effect payloads change
7. `src/Cgm.Core/Battle/MoveCompiler.cs`
8. `src/Cgm.Core/Battle/MoveEffects.cs`
9. `src/Cgm.Core/Battle/EffectMath.cs`
10. `src/Cgm.Core/Battle/BattleController.cs`
11. Related tests in `tests/Cgm.Core.Tests/Battle`

If serialized `Move.Effects` shape changes, also use `cgm-schema-change`.

## Decision Ladder

Before writing code, classify the request:

1. **Existing op/data can express it**: change data or tests only.
2. **Existing compiled `MoveEffect` can express it**: update compiler mapping only.
3. **Existing primitive can express it**: add a small typed effect record and route to the shared primitive.
4. **New primitive is truly needed**: update the spec first, then implement the smallest primitive.
5. **Specific move branch is requested**: refuse that shape and propose the reusable op.

Stop at the earliest rung that works.

## Implementation Workflow

1. Confirm the op is allowed in the current battle layer.
2. Write or update the spec entry first:
   - op name
   - params
   - timing hook
   - RNG draws and order
   - rounding rules
   - failure/no-op cases
   - emitted events
3. Keep numeric formulas in pure helpers such as `EffectMath`, not buried in resolver branches.
4. Compile serialized `Move.Effects` into typed `MoveEffect` records in `MoveCompiler`.
5. Resolve typed effects in the shared dispatcher path, currently `BattleController.ApplyEffect`.
6. Reuse primitives:
   - `Heal` for HP restoration
   - `Sap` for non-move HP loss
   - `DrainLife` for victim-to-beneficiary drains
   - `DamageCalc` for standard damage
   - `EffectMath` for op math
   - condition and volatile helpers for stateful status logic
7. Emit `BattleEvent`s for presentation/debug consumers. Do not make UI infer outcomes by state diff.
8. Preserve deterministic RNG order. A new draw must be named in the spec and proven by tests.

## Hard Rules

- No bespoke code for a named move.
- No runtime/UI computation of battle rules.
- No `new Random()`, wall-clock, static mutable state, sleeps, or network.
- No hidden future-phase systems: abilities, held-item battle hooks, forms, doubles, breeding, or v6 weather interactions before their phase.
- No unexplained golden/event-log changes.
- No vague effect op such as `specialCase` or `customScript`.
- No adding a dependency for battle logic.

## Test Requirements

Add focused tests in `tests/Cgm.Core.Tests/Battle`:

- Pure math table tests for formula helpers.
- Compiler tests from serialized op payload to typed effect.
- Resolver tests for state changes and events.
- RNG-order-sensitive tests when the op draws randomness.
- Boundary tests: immunity, miss, fainted target, full HP, zero damage, status already present, caps.
- Regression tests for any existing behavior touched by the shared path.

Run:

```powershell
D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --filter Battle
```

If public APIs/schema changed, run the full solution:

```powershell
D:\dotnet\dotnet.exe test CreatureGameMaker.slnx
```

## Review Checklist

- Does every move using the behavior go through the same op?
- Is the op named by behavior, not by move?
- Are params general enough for current known uses but not speculative?
- Is the failure behavior explicit?
- Are events sufficient for UI/debug without UI reaching into battle internals?
- Does AI scoring need to know about the op? If yes, use `cgm-smart-ai` too.
- Does validation need to reject bad params? If yes, add a validation rule and tests.
- Did any fixture/golden change? State why.

## Completion Report

Report the op name, files changed, tests added, RNG/event implications, and any intentional exclusions. If a requested named move was not implemented directly, say which reusable op covers it.
