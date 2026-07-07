# ARCHITECTURE

Status: **Stub** — current source is `MASTER_PLAN.md` §4 and `ARCHITECTURE_ADDENDUM.md` §2 (ADRs)
and §6 (contracts). Full write due **Phase 3**. Blocks: cross-module work once >1 subsystem exists.

## Purpose
The system-level map: solution layout, module boundaries and dependency rules, the scene stack,
and the message contracts between subsystems (Overworld↔Battle, Creator↔Runtime).

## Must lock
- Dependency rules: Core references only BCL+serialization; Creator/Runtime/Tools depend on Core,
  never on each other; enforced by the Core-purity architecture test.
- Scene stack model and the BattleRequest/BattleResult and BattleAction/BattleEvent boundaries.
- The §6 contracts (FixedStepClock, EntityId, Project, IRenderer, IInputSource, save migrator).

## Outline (to be written)
Solution layout · Module boundaries · Dependency diagram · Scene stack · Message contracts · ADR index.
