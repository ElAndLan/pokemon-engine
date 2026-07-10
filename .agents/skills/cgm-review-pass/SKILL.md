---
name: cgm-review-pass
description: Review Creature Game Maker changes, PRs, phase gates, or suspected drift for correctness, scope creep, spec/code mismatch, missing tests, dependency violations, determinism breaks, schema migration gaps, and architecture boundary violations. Use when the user asks for a review, phase review, audit of a change, go/no-go decision, or validation that work is done.
---

# CGM Review Pass

## Overview

Use this skill for review, not implementation. Findings come first, ordered by severity, with file references and a clear fix/accept tag. The main job is to catch defects and scope drift before they become project shape.

Run `cgm-scope-gate` first so the review uses the current phase, not the agent's assumptions.

## Required Reads

Always read:

1. `AGENTS.md`
2. `docs/SCOPE_GUARD.md`
3. `docs/IMPLEMENTATION_PLAN.md`
4. `docs/ARCHITECTURE_ADDENDUM.md`
5. `docs/AGENTS.md`
6. `docs/CODING_STANDARDS.md`

Then read the owning specs for touched areas:

- Schema/data: `docs/DATA_SCHEMA.md`
- Battle/effects: `docs/BATTLE_SYSTEM_SPEC.md`, `docs/EFFECT_TYPES_CATALOG_v0_5.md`, `docs/BATTLE_DAMAGE_CALC.md`
- AI: `docs/BATTLE_AI_SPEC.md`
- Creator: `docs/CREATOR_APP_SPEC.md`
- Runtime: `docs/ENGINE_RUNTIME_SPEC.md`
- Assets: `docs/ASSET_PIPELINE_SPEC.md`
- Maps: `docs/MAP_EDITOR_SPEC.md`
- Export: `docs/EXPORT_PIPELINE_SPEC.md`
- Tests/goldens: `docs/TESTING_STRATEGY.md`
- Dependencies: `docs/TECH_STACK.md`

## Review Procedure

1. Identify changed files with `git status --short` and, when useful, `git diff --stat`.
2. Read the changed code, changed docs, and tests together.
3. Trace the real flow end to end for each behavior. Do not review only the named symptom.
4. Compare code against the owning spec. Spec/code mismatch is high severity.
5. Check phase scope and deferred/never lists.
6. Check architecture boundaries:
   - Core pure
   - rules in Core
   - Runtime as IO/render glue
   - Creator edits only
   - UI through undo stack
7. Check determinism:
   - injected `IRng`
   - no wall-clock in sim
   - no static mutable state
   - no dictionary-order-dependent sim decisions
8. Check serialized shape changes:
   - `DATA_SCHEMA.md`
   - version bump
   - migration
   - old fixture
9. Check tests are the right kind, not just higher count.
10. Run or inspect test results if available. Do not claim green unless actually run.

## Finding Tags

Use these tags:

- `FIX-NOW`: blocks merge/phase gate or can corrupt data, break determinism, violate scope, or contradict a spec.
- `FIX-LATER`: valid issue but not blocking for this phase/change.
- `ACCEPT`: intentional tradeoff, documented deviation, or harmless limitation.

Severity order:

1. Data loss/save or schema break
2. Scope violation/forbidden dependency/IP violation
3. Core purity/rules boundary violation
4. Determinism/RNG/golden replay break
5. Incorrect gameplay/math/AI behavior
6. Missing tests for required behavior
7. UI/undo/validation pattern break
8. Maintainability/over-engineering

## Output Format

Start with findings. If none, say so plainly.

```text
Findings
- FIX-NOW: src/Cgm.Core/...:42 - Code writes a new serialized field but DATA_SCHEMA.md was not updated and no migration/fixture exists.
- FIX-LATER: docs/...:18 - Spec says the review outcome should be logged, but IMPLEMENTATION_PLAN.md was not updated.

Open Questions
- Is this intended to advance Phase 14, or only close a tuning task?

Verdict
No-go for phase exit until FIX-NOW items are resolved.

Tests Checked
- D:\dotnet\dotnet.exe test CreatureGameMaker.slnx: not run in this review.
```

## Special Checks

### Battle Review

- No bespoke move branches.
- Effect ops are closed, named, and spec'd.
- RNG draw order is deliberate.
- Events cover presentation/debug needs.
- AI scoring updates account for new effects where needed.

### AI Review

- Legal actions only.
- No selected-action reading.
- Named score components.
- Seeded determinism.
- Tuning metrics include old/new, seeds, and benchmark teams.

### Creator Review

- Edits go through undo commands.
- ViewModels are headless-testable.
- Views stay thin.
- Validation uses Core.
- No in-process gameplay simulation.

### Schema Review

- DATA_SCHEMA matches code field-for-field.
- Version/migration/fixture exists when shape changes.
- Unknown fields remain tolerated.
- Output remains byte-stable.

### Dependency/IP Review

- No new package without `TECH_STACK.md` and user sign-off.
- No game engine/framework.
- No official Pokemon assets/names/cries/music/maps in samples, fixtures, tests, packs, or exports.

## Completion Report

End with the verdict: go, no-go, or no blocking findings. Include tests run or not run. Keep summaries secondary to findings.
