# TESTING_STRATEGY

Status: **Phase 15A contract active.** xUnit suites and deterministic Core tests are active. The
corpus manifest/inventory contract is implemented in Cgm.Tools. Stable JSON/text snapshots use
existing BCL serialization; Verify remains unnecessary unless a later decision demonstrates value.

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

## Phase 15 test layers

1. **Corpus inventory tests** — neutral fixture wrappers prove stable ordering/hashes/digest,
   duplicate/malformed rejection, name omission, and deterministic serialization.
2. **Primitive tests** — formulas, queries, conditions, targets, queues, mutations, and failure
   boundaries.
3. **Compiler tests** — valid generic data compiles; missing/unknown ops and params fail strictly.
4. **Resolver tests** — state and events in required singles/doubles contexts.
5. **Per-move conformance tests** — every reference key declares test IDs for each mechanic family;
   only passing required-context behavior advances status to `certified`.
6. **Golden replay tests** — seed + ruleset + topology + teams + action script produce stable event
   and effect-trace text/JSON. Golden changes require an explicit reason in the change report.
7. **Fuzz/property tests** — bounds, conservation, target legality, queue/hook termination, and
   replay determinism across the certified corpus.

## Phase 15 evidence progression

Manifest statuses advance only when the immediately preceding evidence exists:

| Status | Required generated evidence |
|---|---|
| `inventoryOnly` | Locked source ID/hash and sanitized structured observations |
| `normalized` | Complete generic definition hash, mechanic families, topology, ruleset, and no unknown requirement |
| `compiled` | Strict validator success plus typed compiler test IDs; all invalid-param siblings remain rejected |
| `certified` | Required valid and invalid contexts resolve with asserted state/events/traces and deterministic replay |

No status is inferred from a helper existing, a representative test passing, or a legacy audit PASS.
Counts are regenerated from the manifest/test registry and never edited by hand.

Every normalized conformance vector records: neutral reference key, normalized definition hash,
ruleset, topology, immutable definitions, initial battle state, action/target script, RNG seed or exact
draw sequence, expected state assertions, ordered event assertions, trace assertions, and test IDs.
When one entry declares multiple mechanic families or alternate valid contexts, its vector set must
assert each one before certification.

## Feature-package minimum test matrix

An implementation package selects every applicable row; omitting one requires a written reason:

| Behavior | Minimum evidence |
|---|---|
| Formula/query | Table tests for normal, threshold, min/max, zero guard, cap, and exact rounding; resolver path and trace |
| Serialized op | Valid compile plus missing, unknown, wrong-type, invalid-enum/range, incompatible-param, and duplicate tests |
| Mutation | Success, ordinary failure, event order, clamp/conservation, faint/full/empty state, switch/faint/end cleanup |
| Condition | Apply/block/duplicate/refresh/stack, duration, hooks, removal-by-tag, transfer if allowed, cleanup matrix |
| Target/topology | Singles and doubles valid cases, invalid explicit selection, stable spread order, random draw count, fainted target |
| Queue/timing | Due timing, insertion order, cancellation, PP/RNG skipped draws, switch/faint ownership, replay determinism |
| Multi-hit/target | Per-hit/per-target state, independent/shared rolls as specified, early faint stop, aggregate damage reuse |
| Move reference | Pool/exclusion order, PP/event ownership, target revalidation, recursion/loop termination |
| Ruleset difference | Same vector under each required profile with only the specified outcomes differing |
| AI awareness | Legal action/target, conservative reusable score signal, deterministic explanation, no resolver duplication |

After focused tests, run the full solution. Phase 15 package reports must include commands and pass
counts; “should pass” and previously green output are not evidence for the current change.

## Golden format

A golden input records ruleset ID, topology, RNG seed/state, immutable definitions, initial battle
state, and submitted actions. Output records ordered `BattleEvent`s plus ordered effect traces.
Dynamic paths, timestamps, object hashes, and display-localized text are excluded. Entity IDs in
checked-in executable fixtures are original neutral content.

Goldens live under `tests/Cgm.Core.Tests/Battle/Goldens/`. An update must state which spec change
altered ordering or values. Regenerating snapshots merely to make tests pass is forbidden.

## Corpus fixture boundary

The local `docs/pokeapi-results` corpus is not required on CI. Cgm.Tools tests build small neutral
wrapper fixtures. The generated sanitized manifest may be committed because it contains numeric
reference keys, hashes, structured mechanics tags, and conformance metadata only—never official
names, prose, assets, URLs, or raw reference JSON.

## Phase 15A CI gate

- Tool unit tests pass without the local corpus.
- When the local corpus is available, generation reports exactly 937 entries and 0 certified.
- Regenerating the manifest from unchanged files is byte-identical.
- Generated output contains no payload names or source filenames.
- Full solution build/tests remain green.
