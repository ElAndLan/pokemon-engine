# TESTING_STRATEGY

Status: **Partial.** xUnit suites, fixtures, and deterministic Core tests are active and currently
green. The formal golden-replay workflow is still not fully written, and Verify is not currently
referenced by the test projects.

## Purpose
The test playbook: what gets tested at each layer, the golden-file (Verify) workflow, fixture
conventions, and the determinism rules that make battle replays reproducible.

## Must lock
- xUnit is active; Verify remains planned but is not currently installed. Core is graphics-free so
  most gameplay remains CI-testable.
- Golden-replay workflow: (seed + team + action script) → event log snapshot; changes only
  intentional, with a stated reason. Unexplained golden diffs fail review.
- Fixture conventions (`tests/fixtures/`, `samples/`); determinism rules (injected IRng, no
  wall-clock/sleep/network in tests); the standing full-game input-replay regression test.

## Outline (to be written, Week 2)
Test layers · Golden workflow · Fixtures · Determinism rules · Per-phase suite index · CI gates.
