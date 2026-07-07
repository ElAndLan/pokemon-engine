# CODING_STANDARDS.md

C# / .NET 10 standards for Creature Game Maker. These exist to keep months of
AI-assisted code coherent, testable, and deterministic — not for style aesthetics.

## Language & style
- C# 14, nullable reference types **enabled** everywhere; warnings-as-errors in CI.
- File-scoped namespaces; records for data, classes for behavior. One *primary* type per file,
  plus its closely-related small collaborators (an entity + its nested value types; a group of
  sibling validation rules for one domain). Don't split tightly-coupled 10-line types into
  separate files — fewest files that stay readable (Ponytail).
- Naming: standard .NET conventions. No abbreviations in public APIs (`EncounterTable`,
  not `EncTbl`).
- Comments state constraints the code can't (`// Gen 4 rounds down between each factor`),
  never narrate the code or the change history. Zero boilerplate comments.

## Architecture invariants (violations are defects, not preferences)
1. **Core purity.** `Cgm.Core` references BCL + serialization only. An architecture test
   asserts this; keep it passing.
2. **Rules in Core.** Damage math, movement legality, capture, exp, evolution, validation,
   save model. `Cgm.Runtime` renders/plays/reads input; `Cgm.Creator` edits data.
3. **Determinism.** Sim code takes `IRng` (injected) and tick counts. Forbidden in sim
   code: `new Random()`, `Random.Shared`, `DateTime.Now/UtcNow`, `Environment.TickCount`,
   static mutable state, dictionary-order dependence. In-game clock is sim state.
4. **Immutability of definitions.** Loaded entity definitions (species, moves, maps…) are
   immutable records. Runtime *instances* (a caught creature, battle state) are mutable
   and clearly separated from definitions.
5. **Action → validate → resolve → state.** UI (battle or editor) never mutates state
   directly. Editors mutate only through the undo command stack (whole-record snapshot
   commands for small entities; per-cell commands for map layers).
6. **Events over queries for presentation.** Battle emits `BattleEvent`s; presentation
   consumes them. Presentation never derives "what happened" by diffing state.
7. **Named seams only.** Interfaces exist where the addendum names them (`IRenderer`,
   `IRng`, `IInputSource`, `IValidationRule`) or a second implementation exists. No
   speculative interfaces, factories, or layers.
8. **One way per thing.** Before building something shaped like an existing thing (an
   editor screen, an effect op, a validation rule), read the pathfinder and mirror it.

## Data & IDs
- `EntityId` = `category:slug`, slug `[a-z0-9_]+`, immutable after creation.
- Every serialized file carries `schemaVersion`. Shape changes = DATA_SCHEMA.md update +
  version bump + migration function + old-shape fixture test, all in the same change.
- Serialization: System.Text.Json with source generators; unknown fields tolerated on
  read; output byte-stable (stable property order) so fixtures and git diffs stay honest.

## Testing policy
- New behavior ⇒ new tests in the same change. Specifically mandatory: one pass + one
  fail test per validation rule; unit tests per effect op; table tests for every battle
  formula; round-trip tests per schema; migration tests per version bump.
- **Golden replays** (Verify snapshots of battle event logs from seed + action script)
  change only intentionally; the change description must say why. An unexplained golden
  diff fails review.
- Tests use fixtures from `tests/fixtures/` and `samples/fixture-min/`; never edit a
  fixture to make a failing test pass without understanding why it failed.
- No sleeping, no wall-clock, no network in tests.

## Dependencies
Closed list in TECH_STACK.md. Adding a package: update TECH_STACK.md (name, version,
justification, engine-rule check, fallback) in the same change AND flag to the user
before merging. Forbidden list is absolute.

## Change hygiene
Small increments that build and pass tests. No dead code, no commented-out code, no
TODOs referencing future phases (SCOPE_GUARD's ledger is where futures live). Commit
messages state what and why; deviations from the task are listed in the report, not
hidden.
