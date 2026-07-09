# AGENTS.md — Working in this repo (all AI agents and humans)

## Reading order
1. `/CLAUDE.md` — binding rules (applies to every agent, not just Claude)
2. `docs/SCOPE_GUARD.md` — current phase + scope law
3. `docs/ARCHITECTURE_ADDENDUM.md` — wins over MASTER_PLAN.md on conflicts
4. `docs/MASTER_PLAN.md` — full plan
5. The owning spec for your task (map below). Stub spec = blocked task: complete the
   spec first, confirm, then implement.

## Doc ownership map

| Area | Owning spec | Status |
|---|---|---|
| Stack & dependencies | TECH_STACK.md | Written |
| Scope | SCOPE_GUARD.md | Written |
| Code style & invariants | CODING_STANDARDS.md | Written |
| Module boundaries, ADRs | ARCHITECTURE.md + docs/adr/ | Partial — addendum/ADRs authoritative; summary doc still thin |
| Serialized shapes, IDs, migration | DATA_SCHEMA.md | **Schema v3** (Phase 15 contact hooks + ability/form/held battle data) |
| Damage formula, type/STAB/crit | BATTLE_DAMAGE_CALC.md | **Reference-frozen v1** |
| Trainer battle AI tiers/scoring | BATTLE_AI_SPEC.md | Phase 14 verified Core baseline; full tuning deferred until Phase 15+ mechanics |
| Creator screens, undo, validation UI | CREATOR_APP_SPEC.md | Shell + editor pattern + pathfinders written (Phase 3) |
| Runtime loop, renderer, input, scenes | ENGINE_RUNTIME_SPEC.md | Partial - headless helpers, export boot/smoke, and minimal GL rectangle showcase written; full renderer/UI/dev-mode host pending |
| Import/slicing layers, pack format | ASSET_PIPELINE_SPEC.md | Partial — v0–v2 slicing + v5 atlas spec/code; canvas/UI and v3 pending |
| Map editor tools & layers | MAP_EDITOR_SPEC.md | Partial — layer/tool ops written; canvas/entity UI pending |
| Battle formulas, effect ops, events, AI | BATTLE_SYSTEM_SPEC.md | Partial — v5 Core + smart AI baseline verified; 1.0 event/golden spec still thin |
| Export & smoke test | EXPORT_PIPELINE_SPEC.md | Partial - pack/config/data export, local template copy, and Runtime `--smoke` written; icon/metadata patch, Creator UI, CI templates, clean-VM pending |
| Phase status & deviations log | IMPLEMENTATION_PLAN.md | Living |
| Test policy, goldens, fixtures | TESTING_STRATEGY.md | Partial — xUnit suites/fixtures active; golden workflow not fully written |
| Vision & legal boundary | PROJECT_OVERVIEW.md | See MASTER_PLAN §1–2 until written |

## Build & test
- Build: `dotnet build CreatureGameMaker.slnx` (`.slnx` = .NET 10 default solution format)
- Test: `dotnet test CreatureGameMaker.slnx` (must be green before any "done" claim)
- Run creator: `dotnet run --project src/Cgm.Creator`
- Run runtime: `dotnet run --project src/Cgm.Runtime -- --debug` (minimal host; exported
  config/pack loading and rectangle showcase work, raw-folder dev-mode loading is not wired yet)
- Tools CLI: `dotnet run --project src/Cgm.Tools -- --help`
- **This machine:** `dotnet` may resolve to the old C: 8.0 in pre-existing shells — use
  `D:/dotnet/dotnet.exe` and export `DOTNET_ROOT=D:/dotnet`, `NUGET_PACKAGES=D:/.nuget-packages`.
  See TECH_STACK.md → "Local dev environment".

## Non-negotiable invariants (details in CODING_STANDARDS.md)
- Cgm.Core: no UI/graphics/audio/windowing deps (enforced by an architecture test)
- All rules/math in Core; Runtime is rendering/IO glue; Creator edits data only
- Injected `IRng` everywhere in sim code; determinism protects golden tests
- EntityIds immutable; schema changes ship with DATA_SCHEMA.md diff + version bump
- Closed dependency list (TECH_STACK.md); new packages need user sign-off
- No official Pokemon content anywhere, including fixtures

## Definition of done
Spec-conformant (or spec updated in same change) → build+tests green with new tests for
new behavior → no scope creep, dead code, or future-phase TODOs → IMPLEMENTATION_PLAN.md
updated → deviations reported explicitly. Never report done without running the tests.

## Cadence
Build passes alternate with review passes (prompts in ARCHITECTURE_ADDENDUM.md §12).
Review findings are tagged FIX-NOW / FIX-LATER / ACCEPT and logged in IMPLEMENTATION_PLAN.md.
