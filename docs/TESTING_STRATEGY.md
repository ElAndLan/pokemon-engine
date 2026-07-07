# TESTING_STRATEGY

Status: **Stub** — current source is `MASTER_PLAN.md` §13 and each phase's "Testing suite" in
`IMPLEMENTATION_PLAN.md`. Full write due **Week 2 (Phase 2)**. Blocks: golden-file workflow from Phase 8.

## Purpose
The test playbook: what gets tested at each layer, the golden-file (Verify) workflow, fixture
conventions, and the determinism rules that make battle replays reproducible.

## Must lock
- xUnit + Verify; Core is graphics-free so ~90% of gameplay is CI-testable.
- Golden-replay workflow: (seed + team + action script) → event log snapshot; changes only
  intentional, with a stated reason. Unexplained golden diffs fail review.
- Fixture conventions (`tests/fixtures/`, `samples/`); determinism rules (injected IRng, no
  wall-clock/sleep/network in tests); the standing full-game input-replay regression test.

## Outline (to be written, Week 2)
Test layers · Golden workflow · Fixtures · Determinism rules · Per-phase suite index · CI gates.
