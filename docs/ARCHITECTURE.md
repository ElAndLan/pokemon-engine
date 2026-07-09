# ARCHITECTURE

Status: **Partial** — `ARCHITECTURE_ADDENDUM.md` §2 (ADRs) and §6 (contracts) remain the
authoritative architecture source. This summary doc still needs the module-boundary digest and
diagrams; do not treat it as complete.

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
